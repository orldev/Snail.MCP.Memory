namespace Snail.MCP.Memory.Core.Answers;

/// <summary>A project whose records come near a description.</summary>
/// <param name="Project">The project's display name.</param>
/// <param name="Similarity">The best similarity among its records, in <c>[0..1]</c>.</param>
public readonly record struct SimilarProject(string Project, double Similarity);
