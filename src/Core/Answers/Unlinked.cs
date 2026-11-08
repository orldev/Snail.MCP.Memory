namespace Snail.MCP.Memory.Core.Answers;

/// <summary>Edges removed between two projects, either direction.</summary>
/// <param name="From">The first project.</param>
/// <param name="To">The second project.</param>
/// <param name="Type">The type removed; empty for every type.</param>
/// <param name="Removed">How many edges were removed.</param>
public sealed record Unlinked(string From, string To, string Type, int Removed);
