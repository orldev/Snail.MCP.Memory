namespace Snail.MCP.Memory.Core.Interfaces;

/// <summary>Persistence of the directed edges between projects.</summary>
public interface IGraphStore
{
    /// <summary>Creates or updates an edge; returns <c>true</c> when it was created.</summary>
    Task<bool> UpsertAsync(GraphEdge edge, CancellationToken cancellationToken = default);

    /// <summary>Removes edges between two projects in both directions; empty type — every type. Returns the count.</summary>
    Task<int> RemoveAsync(string sourceKey, string targetKey, string type, CancellationToken cancellationToken = default);

    /// <summary>Edges touching any of the given project keys, in either direction.</summary>
    Task<IReadOnlyList<GraphEdge>> GetForProjectsAsync(IReadOnlyCollection<string> projectKeys, CancellationToken cancellationToken = default);

    /// <summary>Every edge of the graph.</summary>
    Task<IReadOnlyList<GraphEdge>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Rewrites a project name on every edge touching it; returns how many edges changed.</summary>
    Task<int> RenameProjectAsync(string oldKey, string newName, CancellationToken cancellationToken = default);
}
