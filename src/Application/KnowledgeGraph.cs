using Snail.MCP.Memory.Core.Constants;
using Snail.Sdk.Mcp.Hosting.Answers;

namespace Snail.MCP.Memory.Application;

/// <summary>The directed knowledge graph: linking, unlinking, reading, and the pending suggestion queue.</summary>
/// <remarks>Both ends of a link must be registered projects — an edge to a name without memory is a phantom.</remarks>
public sealed class KnowledgeGraph(
    IGraphStore graph, IProjectStore projects, IPendingLinkStore pending, TimeProvider timeProvider)
{
    /// <summary>Depth bounds of a graph expansion.</summary>
    internal const int MaxDepth = 5;

    /// <summary>Creates or updates one directed edge; a confirmed edge settles the pair's pending suggestion.</summary>
    public async Task<Answer<Linked>> LinkAsync(
        string from, string to, string type, string note = "", CancellationToken cancellationToken = default)
    {
        var source = ProjectIdentity.Normalize(from);
        var target = ProjectIdentity.Normalize(to);

        if (await RefusedPairAsync(source, target, cancellationToken) is { } refused)
            return refused;

        var created = await graph.TryInsertAsync(
            new GraphEdge(source, target, type, note.Trim(), timeProvider.GetUtcNow()), cancellationToken);

        var resolved = await pending.ResolveAsync(
            ProjectIdentity.Key(source), ProjectIdentity.Key(target), cancellationToken);

        return Answer.Ok(new Linked(source, type, target, created, resolved));
    }

    /// <summary>Rejects a suggested pair so it is never proposed again.</summary>
    public async Task<Answer<Dismissed>> DismissAsync(string from, string to, CancellationToken cancellationToken = default)
    {
        var source = ProjectIdentity.Normalize(from);
        var target = ProjectIdentity.Normalize(to);

        if (await RefusedPairAsync(source, target, cancellationToken) is { } refused)
            return refused;

        await pending.DismissAsync(source, target, timeProvider.GetUtcNow(), cancellationToken);

        return Answer.Ok(new Dismissed(source, target));
    }

    /// <summary>
    /// Queues a suggestion for every registered project the text mentions that has neither an
    /// edge to <paramref name="projectName"/> nor an earlier suggestion; returns what was
    /// newly queued.
    /// </summary>
    public async Task<IReadOnlyList<PendingLink>> ProposeMentionsAsync(
        string projectName, string text, string evidence, CancellationToken cancellationToken = default)
    {
        var project = ProjectIdentity.Normalize(projectName);
        var projectKey = ProjectIdentity.Key(project);

        var mentioned = (await projects.ListAsync(cancellationToken))
            .Where(candidate => ProjectIdentity.Key(candidate.Name) != projectKey)
            .Where(candidate => ProjectMentions.Mentions(text, candidate.Name))
            .ToList();

        if (mentioned.Count == 0)
            return [];

        var linked = (await graph.GetForProjectsAsync([projectKey], cancellationToken))
            .SelectMany(edge => new[] { ProjectIdentity.Key(edge.Source), ProjectIdentity.Key(edge.Target) })
            .ToHashSet();

        var now = timeProvider.GetUtcNow();
        var proposed = new List<PendingLink>();

        foreach (var candidate in mentioned.Where(c => !linked.Contains(ProjectIdentity.Key(c.Name))))
        {
            var link = new PendingLink(project, candidate.Name, evidence, now);

            if (await pending.TryProposeAsync(link, cancellationToken))
                proposed.Add(link);
        }

        return proposed;
    }

    /// <summary>The open suggestions touching a project.</summary>
    public Task<IReadOnlyList<PendingLink>> PendingAsync(string projectKey, CancellationToken cancellationToken = default) =>
        pending.ListAsync(projectKey, cancellationToken);

    /// <summary>
    /// Upserts machine-extracted relations as auto edges: a manual edge is never overwritten,
    /// an auto edge missing from its source is only marked stale, a target without memory is
    /// skipped and reported. Idempotent — the same payload twice changes nothing.
    /// </summary>
    public async Task<Answer<Synced>> SyncAsync(
        string projectName, IReadOnlyList<RecordLink> links, string source, CancellationToken cancellationToken = default)
    {
        var project = ProjectIdentity.Normalize(projectName);

        if (project.Length == 0)
            return Answer.Failed(Messages.ProjectNameEmpty, Messages.ProjectNameEmptyHint, Refusals.EmptyName);

        var projectKey = ProjectIdentity.Key(project);

        if (await projects.GetAsync(projectKey, cancellationToken) is null)
            return Answer.Failed(Messages.ProjectNotFound(project), Messages.ProjectNotFoundHint, Refusals.NotFound);

        var sourceTag = source.Trim() is { Length: > 0 } tag ? tag : "sync";
        var autoNote = Messages.AutoNote(sourceTag);
        var now = timeProvider.GetUtcNow();

        var outgoing = (await graph.GetForProjectsAsync([projectKey], cancellationToken))
            .Where(edge => ProjectIdentity.Key(edge.Source) == projectKey)
            .ToDictionary(edge => (ProjectIdentity.Key(edge.Target), edge.Type));

        var desired = new HashSet<(string, string)>();
        var skipped = new List<string>();
        int created = 0, refreshed = 0, kept = 0;

        foreach (var link in links)
        {
            var target = ProjectIdentity.Normalize(link.To);
            var targetKey = ProjectIdentity.Key(target);

            if (target.Length == 0 || targetKey == projectKey)
                continue;

            if (await projects.GetAsync(targetKey, cancellationToken) is not { } registered)
            {
                skipped.Add(target);
                continue;
            }

            var type = link.Type.Wire();

            if (!desired.Add((targetKey, type)))
                continue;

            if (!outgoing.TryGetValue((targetKey, type), out var existing))
            {
                await graph.TryInsertAsync(new GraphEdge(project, registered.Name, type, autoNote, now), cancellationToken);
                await pending.ResolveAsync(projectKey, targetKey, cancellationToken);
                created++;
            }
            else if (existing.Note.StartsWith(Messages.AutoNotePrefix, StringComparison.Ordinal))
            {
                await graph.TryInsertAsync(existing with { Note = autoNote, Updated = now }, cancellationToken);
                refreshed++;
            }
            else
            {
                kept++;
            }
        }

        var stale = 0;

        foreach (var (slot, edge) in outgoing)
        {
            if (desired.Contains(slot) ||
                !edge.Note.StartsWith(autoNote, StringComparison.Ordinal) ||
                edge.Note.Contains(Messages.StaleMarker, StringComparison.Ordinal))
                continue;

            await graph.TryInsertAsync(
                edge with { Note = edge.Note + Messages.StaleSuffix(Day.Of(now)), Updated = now },
                cancellationToken);
            stale++;
        }

        return Answer.Ok(new Synced(project, sourceTag, created, refreshed, kept, stale, skipped));
    }

    /// <summary>Removes edges between two projects regardless of direction; empty type — every type.</summary>
    public async Task<Answer<Unlinked>> UnlinkAsync(
        string from, string to, string type = "", CancellationToken cancellationToken = default)
    {
        var source = ProjectIdentity.Normalize(from);
        var target = ProjectIdentity.Normalize(to);

        if (source.Length == 0 || target.Length == 0)
            return Answer.Failed(Messages.BothProjectsRequired, Messages.BothProjectsRequiredHint, Refusals.EmptyName);

        var normalizedType = "";

        if (!string.IsNullOrWhiteSpace(type) && !LinkTypes.TryNormalize(type, out normalizedType))
            return Answer.Failed(Messages.UnknownLinkType(type), Messages.UnknownLinkTypeHint(LinkTypes.AllowedList()), Refusals.UnknownLinkType);

        var removed = await graph.RemoveAsync(
            ProjectIdentity.Key(source), ProjectIdentity.Key(target), normalizedType, cancellationToken);

        return Answer.Ok(new Unlinked(source, target, normalizedType, removed));
    }

    /// <summary>The edges around the selected projects (joined by <c>+</c>), expanding <paramref name="depth"/> hops; empty selection — the whole graph.</summary>
    public async Task<GraphView> ReadAsync(
        string projectName = "", int depth = 1, CancellationToken cancellationToken = default)
    {
        var roots = projectName
            .Split(['+', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ProjectIdentity.Normalize)
            .ToList();

        if (roots.Count == 0)
            return new GraphView([], 0, Ordered(await graph.GetAllAsync(cancellationToken)));

        var boundedDepth = Math.Clamp(depth, 1, MaxDepth);
        var visited = new HashSet<string>(roots.Select(ProjectIdentity.Key));
        var frontier = visited.ToList();
        var edges = new Dictionary<(string, string, string), GraphEdge>();

        for (var hop = 0; hop < boundedDepth && frontier.Count > 0; hop++)
        {
            var found = await graph.GetForProjectsAsync(frontier, cancellationToken);
            frontier = [];

            foreach (var edge in found)
            {
                edges[(ProjectIdentity.Key(edge.Source), ProjectIdentity.Key(edge.Target), edge.Type)] = edge;

                foreach (var end in new[] { edge.Source, edge.Target })
                {
                    var key = ProjectIdentity.Key(end);

                    if (visited.Add(key))
                        frontier.Add(key);
                }
            }
        }

        return new GraphView(roots, boundedDepth, Ordered([.. edges.Values]));
    }

    /// <summary>Why a pair cannot be linked or dismissed: an empty end, a project paired with itself, an end without memory; null when it can.</summary>
    private async Task<AnswerFailure?> RefusedPairAsync(string source, string target, CancellationToken cancellationToken)
    {
        if (source.Length == 0 || target.Length == 0)
            return Answer.Failed(Messages.BothProjectsRequired, Messages.BothProjectsRequiredHint, Refusals.EmptyName);

        if (ProjectIdentity.Equal(source, target))
            return Answer.Failed(Messages.SelfLinkForbidden, Messages.SelfLinkForbiddenHint, Refusals.SelfLink);

        foreach (var end in new[] { source, target })
        {
            if (await projects.GetAsync(ProjectIdentity.Key(end), cancellationToken) is null)
                return Answer.Failed(Messages.ProjectNotFound(end), Messages.ProjectNotFoundHint, Refusals.NotFound);
        }

        return null;
    }

    private static List<GraphEdge> Ordered(IEnumerable<GraphEdge> edges) =>
        [.. edges.OrderBy(edge => edge.Source, ProjectIdentity.Comparer).ThenBy(edge => edge.Type, StringComparer.Ordinal)];
}
