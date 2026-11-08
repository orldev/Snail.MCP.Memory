namespace Snail.MCP.Memory.Core.Answers;

/// <summary>A suggested pair rejected for good.</summary>
/// <param name="From">The first project of the pair.</param>
/// <param name="To">The second project of the pair.</param>
public sealed record Dismissed(string From, string To);
