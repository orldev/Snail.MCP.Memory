using Snail.MCP.Memory.Core.Constants;
using Snail.MCP.Memory.Core.Formatting;

namespace Snail.MCP.Memory.Application;

/// <summary>The directed knowledge graph: linking, unlinking, rendering, and the pending suggestion queue.</summary>
/// <remarks>Both ends of a link must be registered projects — an edge to a name without memory is a phantom.</remarks>
public sealed class GraphService(
    IGraphStore graph, IProjectStore projects, IPendingLinkStore pending, TimeProvider timeProvider)
{
    /// <summary>Depth bounds of a graph expansion.</summary>
    internal const int MaxDepth = 5;

    /// <summary>Creates or updates one directed edge; a confirmed edge settles the pair's pending suggestion.</summary>
    public async Task<string> LinkAsync(
        string from, string to, string type, string note = "", CancellationToken cancellationToken = default)
    {
        var source = ProjectIdentity.Normalize(from);
        var target = ProjectIdentity.Normalize(to);

        if (source.Length == 0 || target.Length == 0)
            return Messages.BothProjectsRequired;

        if (ProjectIdentity.Equal(source, target))
            return Messages.SelfLinkForbidden;

        foreach (var end in new[] { source, target })
            if (await projects.GetAsync(ProjectIdentity.Key(end), cancellationToken) is null)
                return Messages.ProjectNotFound(end);

        var created = await graph.UpsertAsync(
            new GraphEdge(source, target, type, note.Trim(), timeProvider.GetUtcNow()), cancellationToken);

        var resolved = await pending.ResolveAsync(
            ProjectIdentity.Key(source), ProjectIdentity.Key(target), cancellationToken);

        var message = created
            ? Messages.LinkCreated(source, type, target)
            : Messages.LinkUpdated(source, type, target);

        return resolved > 0 ? message + Messages.PendingResolved(resolved) : message;
    }

    /// <summary>Rejects a suggested pair so it is never proposed again.</summary>
    public async Task<string> DismissAsync(string from, string to, CancellationToken cancellationToken = default)
    {
        var source = ProjectIdentity.Normalize(from);
        var target = ProjectIdentity.Normalize(to);

        if (source.Length == 0 || target.Length == 0)
            return Messages.BothProjectsRequired;

        if (ProjectIdentity.Equal(source, target))
            return Messages.SelfLinkForbidden;

        foreach (var end in new[] { source, target })
            if (await projects.GetAsync(ProjectIdentity.Key(end), cancellationToken) is null)
                return Messages.ProjectNotFound(end);

        await pending.DismissAsync(source, target, timeProvider.GetUtcNow(), cancellationToken);

        return Messages.LinkDismissed(source, target);
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

            if (await pending.ProposeAsync(link, cancellationToken))
                proposed.Add(link);
        }

        return proposed;
    }

    /// <summary>The open suggestions touching a project.</summary>
    public Task<IReadOnlyList<PendingLink>> PendingAsync(string projectKey, CancellationToken cancellationToken = default) =>
        pending.ListAsync(projectKey, cancellationToken);

    /// <summary>Removes edges between two projects regardless of direction; empty type — every type.</summary>
    public async Task<string> UnlinkAsync(
        string from, string to, string type = "", CancellationToken cancellationToken = default)
    {
        var source = ProjectIdentity.Normalize(from);
        var target = ProjectIdentity.Normalize(to);

        if (source.Length == 0 || target.Length == 0)
            return Messages.BothProjectsRequired;

        var normalizedType = "";

        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!LinkTypes.TryNormalize(type, out normalizedType))
                return Messages.UnknownLinkType(type, LinkTypes.AllowedList());
        }

        var removed = await graph.RemoveAsync(
            ProjectIdentity.Key(source), ProjectIdentity.Key(target), normalizedType, cancellationToken);

        return removed == 0
            ? Messages.NoLinksBetween(source, target)
            : Messages.LinksRemoved(removed, source, target);
    }

    /// <summary>Renders the edges around the selected projects (joined by <c>+</c>), expanding <paramref name="depth"/> hops; empty selection — the whole graph.</summary>
    public async Task<string> RenderAsync(
        string projectName = "", int depth = 1, CancellationToken cancellationToken = default)
    {
        var roots = projectName
            .Split(['+', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ProjectIdentity.Normalize)
            .ToList();

        if (roots.Count == 0)
            return MemoryFormatter.RenderGraph(Messages.WholeGraphTitle, await graph.GetAllAsync(cancellationToken));

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

        return MemoryFormatter.RenderGraph(
            Messages.GraphTitle(string.Join(" + ", roots), boundedDepth), [.. edges.Values]);
    }
}
