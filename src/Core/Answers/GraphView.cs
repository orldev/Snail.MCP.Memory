namespace Snail.MCP.Memory.Core.Answers;

/// <summary>The edges around the projects asked for, or the whole graph.</summary>
/// <param name="Roots">The projects the expansion started at; none for the whole graph.</param>
/// <param name="Depth">How many hops were expanded; zero for the whole graph.</param>
/// <param name="Edges">The edges found, ordered by source and type.</param>
public sealed record GraphView(IReadOnlyList<string> Roots, int Depth, IReadOnlyList<GraphEdge> Edges);
