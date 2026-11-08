namespace Snail.MCP.Memory.Core.Answers;

/// <summary>A project renamed everywhere.</summary>
/// <param name="From">The old name.</param>
/// <param name="To">The new name.</param>
/// <param name="Records">Records moved to the new name.</param>
/// <param name="Edges">Graph edges moved to the new name.</param>
public sealed record Renamed(string From, string To, int Records, int Edges);
