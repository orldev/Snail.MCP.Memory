namespace Snail.MCP.Memory.Core.Graph;

/// <summary>A directed, typed relation between two projects.</summary>
/// <remarks>Symmetric "related" knowledge is never stored — vector similarity derives it at query time.</remarks>
/// <param name="Source">The project the edge starts at, by its display name.</param>
/// <param name="Target">The project the edge points to, by its display name.</param>
/// <param name="Type">Edge type from <see cref="LinkTypes"/>.</param>
/// <param name="Note">What exactly connects them — one line; may be empty.</param>
/// <param name="Updated">When the edge was last written.</param>
public sealed record GraphEdge(string Source, string Target, string Type, string Note, DateTimeOffset Updated);
