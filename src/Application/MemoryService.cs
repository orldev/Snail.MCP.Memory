using Snail.MCP.Memory.Core.Constants;
using Snail.MCP.Memory.Core.Formatting;
using Snail.MCP.Memory.Core.Lifecycle;

namespace Snail.MCP.Memory.Application;

/// <summary>The record lifecycle: remember with supersession, recall with fallback, forget, review.</summary>
/// <remarks>The server surfaces mechanics; judgment calls stay with the agent.</remarks>
public sealed class MemoryService(
    IRecordStore records,
    IProjectStore projects,
    IEmbedder embedder,
    TimeProvider timeProvider,
    double duplicateThreshold,
    double similarityFloor = 0,
    RetentionPolicy? retention = null)
{
    private readonly RetentionPolicy _retention = retention ?? RetentionPolicy.Default;

    /// <summary>Stores one record: a near-duplicate blocks the insert unless forced, <paramref name="replaceIdRaw"/> supersedes keeping the creation date, a missing project is registered on the fly.</summary>
    public async Task<string> RememberAsync(
        string projectName, string content, string kind = "", string title = "",
        string replaceIdRaw = "", bool force = false, CancellationToken cancellationToken = default)
    {
        var project = ProjectIdentity.Normalize(projectName);

        if (project.Length == 0)
            return Messages.ProjectNameEmpty;

        var now = timeProvider.GetUtcNow();
        await EnsureProjectAsync(project, now, cancellationToken);

        var normalizedKind = RecordKinds.Normalize(kind);
        var embedding = await embedder.TryEmbedAsync(EmbeddingText(title, content), EmbeddingPurpose.Passage, cancellationToken);

        MemoryRecord? replaced = null;

        if (!string.IsNullOrWhiteSpace(replaceIdRaw))
        {
            if (!Guid.TryParse(replaceIdRaw.Trim(), out var replaceId))
                return Messages.RecordIdInvalid(replaceIdRaw.Trim());

            replaced = await records.GetAsync(replaceId, cancellationToken);

            if (replaced is null)
                return Messages.RecordNotFound(replaceId);
        }
        else if (!force && embedding is { } vector &&
                 await FindDuplicateAsync(vector, ProjectIdentity.Key(project), cancellationToken) is { } duplicate)
        {
            var builder = new StringBuilder();
            builder.AppendLine(Messages.DuplicateFound(duplicate.Similarity!.Value));
            builder.AppendLine();
            MemoryFormatter.AppendRecord(builder, duplicate.Record, duplicate.Similarity);

            return builder.ToString().TrimEnd();
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

        var message = replaced is null
            ? Messages.Recorded(record.Kind, record.Project, record.Id)
            : Messages.RecordReplaced(replaced.Id, record.Id);

        var purged = await records.PurgeEventsBeforeAsync(now - _retention.EventTtl, cancellationToken);

        return purged > 0 ? message + Messages.PurgeNotice(purged) : message;
    }

    /// <summary>Finds records by meaning: an empty query renders an overview, an empty project searches the whole memory, returned records are marked as accessed.</summary>
    public async Task<string> RecallAsync(
        string query = "", string projectName = "", string kind = "", int limit = 8,
        CancellationToken cancellationToken = default)
    {
        var project = ProjectIdentity.Normalize(projectName);

        if (project.Length > 0 && await projects.GetAsync(ProjectIdentity.Key(project), cancellationToken) is null)
            return Messages.ProjectNotFound(project);

        var filter = new RecordFilter(
            project.Length > 0 ? ProjectIdentity.Key(project) : null,
            string.IsNullOrWhiteSpace(kind) ? null : RecordKinds.Normalize(kind));
        var top = Math.Clamp(limit, 1, 50);
        var kindLabel = MemoryFormatter.KindLabel(filter.Kind);

        if (string.IsNullOrWhiteSpace(query))
        {
            var listed = await records.ListAsync(filter, top, cancellationToken);
            await MarkAccessedAsync(listed, cancellationToken);

            return MemoryFormatter.RenderOverview(
                project.Length > 0 ? project : Messages.ScopeWholeMemory, kindLabel, listed);
        }

        var embedding = await embedder.TryEmbedAsync(query.Trim(), EmbeddingPurpose.Query, cancellationToken);

        IReadOnlyList<ScoredRecord> found = embedding is { } vector
            ? [.. (await records.SearchAsync(vector, filter, top, cancellationToken))
                .Where(scored => scored.Similarity >= similarityFloor)]
            : [.. (await records.SearchTextAsync(query, filter, top, cancellationToken))
                .Select(record => new ScoredRecord(record, null))];

        await MarkAccessedAsync(found.Select(scored => scored.Record), cancellationToken);

        return MemoryFormatter.RenderRecall(
            query.Trim(), MemoryFormatter.Scope(project), kindLabel, found, usedTextFallback: embedding is null);
    }

    /// <summary>Deletes records by id; several ids come comma- or space-separated.</summary>
    public async Task<string> ForgetAsync(string idsRaw, CancellationToken cancellationToken = default)
    {
        var ids = new List<Guid>();

        foreach (var raw in idsRaw.Split([',', ' ', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(raw, out var id))
                return Messages.RecordIdInvalid(raw);

            ids.Add(id);
        }

        if (ids.Count == 0)
            return Messages.RecordIdInvalid(idsRaw.Trim());

        return Messages.RecordsForgotten(await records.DeleteAsync(ids, cancellationToken));
    }

    /// <summary>Nominates deletion candidates — never-recalled durable records and untouched tasks; deletion stays a confirmed <c>forget</c>.</summary>
    public async Task<string> ReviewAsync(string projectName = "", CancellationToken cancellationToken = default)
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

        return MemoryFormatter.RenderReview(staleDurable, staleTasks, _retention.StaleAfter.Days);
    }

    private async Task EnsureProjectAsync(string project, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await projects.GetAsync(ProjectIdentity.Key(project), cancellationToken) is null)
            await projects.CreateAsync(new ProjectInfo(project, "", now), cancellationToken);
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

    private static string EmbeddingText(string title, string content) =>
        string.IsNullOrWhiteSpace(title) ? content.Trim() : $"{title.Trim()}\n{content.Trim()}";
}
