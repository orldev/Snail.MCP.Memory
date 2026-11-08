using System.Text.Json.Nodes;
using Snail.MCP.Memory.Core.Constants;
using Snail.MCP.Memory.Core.Lifecycle;
using Snail.Sdk.Mcp.Hosting.Answers;

namespace Snail.MCP.Memory.Application;

/// <summary>The record lifecycle: remember with supersession, recall with fallback, forget, review.</summary>
/// <remarks>The server surfaces mechanics; judgment calls stay with the agent.</remarks>
public sealed class RecordLifecycle(
    IRecordStore records,
    IProjectStore projects,
    IEmbedder embedder,
    KnowledgeGraph graph,
    TimeProvider timeProvider,
    double duplicateThreshold,
    double similarityFloor = 0,
    RetentionPolicy? retention = null)
{
    private readonly RetentionPolicy _retention = retention ?? RetentionPolicy.Default;

    /// <summary>Stores one record: a near-duplicate blocks the insert unless forced, <paramref name="replaceIdRaw"/> supersedes keeping the creation date, a missing project is registered on the fly, <paramref name="links"/> creates edges atomically, and mentioned-but-unlinked projects queue pending suggestions.</summary>
    /// <param name="projectName">The project the record belongs to.</param>
    /// <param name="content">The knowledge itself.</param>
    /// <param name="kind">The record's kind; empty is <c>note</c>.</param>
    /// <param name="title">A short label; may be empty.</param>
    /// <param name="replaceIdRaw">The id of the record this one supersedes; empty for none.</param>
    /// <param name="force">Whether to store despite a near-duplicate.</param>
    /// <param name="links">Edges to create from the record's project.</param>
    /// <param name="duplicateOf">How a refused near-duplicate is told to the caller, as the failure's details.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public async Task<Answer<Remembered>> RememberAsync(
        string projectName, string content, string kind = "", string title = "",
        string replaceIdRaw = "", bool force = false, IReadOnlyList<RecordLink>? links = null,
        Func<ScoredRecord, JsonNode>? duplicateOf = null, CancellationToken cancellationToken = default)
    {
        var project = ProjectIdentity.Normalize(projectName);

        if (project.Length == 0)
            return Answer.Failed(Messages.ProjectNameEmpty, Messages.ProjectNameEmptyHint, Refusals.EmptyName);

        var now = timeProvider.GetUtcNow();
        await EnsureProjectAsync(project, now, cancellationToken);

        var normalizedKind = RecordKinds.Normalize(kind);
        var embedding = await embedder.TryEmbedAsync(EmbeddingText(title, content), EmbeddingPurpose.Passage, cancellationToken);

        MemoryRecord? replaced = null;

        if (!string.IsNullOrWhiteSpace(replaceIdRaw))
        {
            if (!Guid.TryParse(replaceIdRaw.Trim(), out var replaceId))
                return Answer.Failed(Messages.RecordIdInvalid(replaceIdRaw.Trim()), Messages.RecordIdInvalidHint, Refusals.InvalidId);

            replaced = await records.GetAsync(replaceId, cancellationToken);

            if (replaced is null)
                return Answer.Failed(Messages.RecordNotFound(replaceId), Messages.RecordNotFoundHint, Refusals.NotFound);
        }
        else if (!force && embedding is { } vector &&
                 await FindDuplicateAsync(vector, ProjectIdentity.Key(project), cancellationToken) is { } duplicate)
        {
            return new AnswerFailure(Messages.DuplicateFound(duplicate.Similarity!.Value), Messages.DuplicateFoundHint, Refusals.Duplicate)
            {
                Details = duplicateOf?.Invoke(duplicate),
            };
        }

        var record = new MemoryRecord
        {
            Id = Guid.NewGuid(),
            Project = project,
            Kind = normalizedKind,
            Title = title.Trim(),
            Content = content.Trim(),
            Created = replaced?.Created ?? now,
            Updated = now
        };

        await records.UpsertAsync(record, embedding, cancellationToken);

        if (replaced is not null)
            await records.DeleteAsync([replaced.Id], cancellationToken);

        var linked = new List<Answer<Linked>>();

        foreach (var link in links ?? [])
            linked.Add(await graph.LinkAsync(project, link.To, link.Type.Wire(), link.Note, cancellationToken));

        var proposed = await graph.ProposeMentionsAsync(project, EmbeddingText(title, content), record.Label, cancellationToken);
        var purged = await records.PurgeEventsBeforeAsync(now - _retention.EventTtl, cancellationToken);

        return Answer.Ok(new Remembered(record, replaced?.Id, linked, proposed, purged));
    }

    /// <summary>Finds records by meaning: an empty query answers an overview, an empty project searches the whole memory, returned records are marked as accessed.</summary>
    public async Task<Answer<Recall>> RecallAsync(
        string query = "", string projectName = "", string kind = "", int limit = 8,
        CancellationToken cancellationToken = default)
    {
        var project = ProjectIdentity.Normalize(projectName);

        if (project.Length > 0 && await projects.GetAsync(ProjectIdentity.Key(project), cancellationToken) is null)
            return Answer.Failed(Messages.ProjectNotFound(project), Messages.ProjectNotFoundHint, Refusals.NotFound);

        var filter = new RecordFilter(
            project.Length > 0 ? ProjectIdentity.Key(project) : null,
            string.IsNullOrWhiteSpace(kind) ? null : RecordKinds.Normalize(kind));
        var top = Math.Clamp(limit, 1, 50);
        var pendingLinks = project.Length == 0 ? [] : await graph.PendingAsync(ProjectIdentity.Key(project), cancellationToken);

        if (string.IsNullOrWhiteSpace(query))
        {
            var listed = await records.ListAsync(filter, top, cancellationToken);
            await MarkAccessedAsync(listed, cancellationToken);

            var newest = listed
                .OrderBy(record => KindRank(record.Kind))
                .ThenByDescending(record => record.Updated)
                .Select(record => new ScoredRecord(record, null))
                .ToList();

            return Answer.Ok(new Recall(project, "", filter.Kind ?? "", RecallSearch.Overview, newest, pendingLinks));
        }

        var embedding = await embedder.TryEmbedAsync(query.Trim(), EmbeddingPurpose.Query, cancellationToken);

        IReadOnlyList<ScoredRecord> found = embedding is { } vector
            ? [.. (await records.SearchAsync(vector, filter, top, cancellationToken))
                .Where(scored => scored.Similarity >= similarityFloor)]
            : [.. (await records.SearchTextAsync(query, filter, top, cancellationToken))
                .Select(record => new ScoredRecord(record, null))];

        await MarkAccessedAsync(found.Select(scored => scored.Record), cancellationToken);

        var search = embedding is null ? RecallSearch.Text : RecallSearch.Meaning;

        return Answer.Ok(new Recall(project, query.Trim(), filter.Kind ?? "", search, found, pendingLinks));
    }

    /// <summary>Deletes records by id; several ids come comma- or space-separated.</summary>
    public async Task<Answer<Forgotten>> ForgetAsync(string idsRaw, CancellationToken cancellationToken = default)
    {
        var ids = new List<Guid>();

        foreach (var raw in idsRaw.Split([',', ' ', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(raw, out var id))
                return Answer.Failed(Messages.RecordIdInvalid(raw), Messages.RecordIdInvalidHint, Refusals.InvalidId);

            ids.Add(id);
        }

        if (ids.Count == 0)
            return Answer.Failed(Messages.RecordIdInvalid(idsRaw.Trim()), Messages.RecordIdInvalidHint, Refusals.InvalidId);

        return Answer.Ok(new Forgotten(await records.DeleteAsync(ids, cancellationToken)));
    }

    /// <summary>Nominates deletion candidates — never-recalled durable records and untouched tasks; deletion stays a confirmed <c>forget</c>.</summary>
    public async Task<Review> ReviewAsync(string projectName = "", CancellationToken cancellationToken = default)
    {
        var project = ProjectIdentity.Normalize(projectName);
        var filter = new RecordFilter(project.Length > 0 ? ProjectIdentity.Key(project) : null);
        var cutoff = timeProvider.GetUtcNow() - _retention.StaleAfter;

        var all = await records.ListAsync(filter, int.MaxValue, cancellationToken);

        var staleDurable = all
            .Where(record => record.Kind != RecordKinds.Task && !RecordKinds.IsEphemeral(record.Kind))
            .Where(record => record.LastAccessed is null && record.Created <= cutoff)
            .ToList();

        var staleTasks = all
            .Where(record => record.Kind == RecordKinds.Task && record.Updated <= cutoff)
            .ToList();

        return new Review(project, _retention.StaleAfter.Days, staleDurable, staleTasks);
    }

    private async Task EnsureProjectAsync(string project, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await projects.GetAsync(ProjectIdentity.Key(project), cancellationToken) is null)
            await projects.TryCreateAsync(new ProjectInfo(project, "", now), cancellationToken);
    }

    private async Task<ScoredRecord?> FindDuplicateAsync(
        ReadOnlyMemory<float> vector, string projectKey, CancellationToken cancellationToken)
    {
        var nearest = await records.SearchAsync(vector, new RecordFilter(projectKey), 1, cancellationToken);

        return nearest is [{ Similarity: { } similarity } candidate, ..] && similarity >= duplicateThreshold
            ? candidate
            : null;
    }

    private Task MarkAccessedAsync(IEnumerable<MemoryRecord> found, CancellationToken cancellationToken)
    {
        var ids = found.Select(record => record.Id).ToList();

        return ids.Count == 0
            ? Task.CompletedTask
            : records.MarkAccessedAsync(ids, timeProvider.GetUtcNow(), cancellationToken);
    }

    /// <summary>Where a kind stands in an overview: the canonical kinds in their order, every other tag after them.</summary>
    private static int KindRank(string kind)
    {
        var index = RecordKinds.Canonical.ToList().IndexOf(kind);

        return index < 0 ? RecordKinds.Canonical.Count : index;
    }

    private static string EmbeddingText(string title, string content) =>
        string.IsNullOrWhiteSpace(title) ? content.Trim() : $"{title.Trim()}\n{content.Trim()}";
}
