using Snail.MCP.Memory.Core.Constants;
using Snail.MCP.Memory.Core.Formatting;

namespace Snail.MCP.Memory.Application;

/// <summary>The directed knowledge graph: linking, unlinking and rendering edges between projects.</summary>
/// <remarks>Both ends of a link must be registered projects — an edge to a name without memory is a phantom.</remarks>
public sealed class GraphService(IGraphStore graph, IProjectStore projects, TimeProvider timeProvider)
{
    /// <summary>Depth bounds of a graph expansion.</summary>
    internal const int MaxDepth = 5;

    /// <summary>Creates or updates one directed edge.</summary>
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

        return created
            ? Messages.LinkCreated(source, type, target)
            : Messages.LinkUpdated(source, type, target);
    }

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
