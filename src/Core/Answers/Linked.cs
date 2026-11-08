namespace Snail.MCP.Memory.Core.Answers;

/// <summary>A directed edge written.</summary>
/// <param name="From">The source project.</param>
/// <param name="Type">The edge type, a word of <see cref="LinkTypes"/>.</param>
/// <param name="To">The target project.</param>
/// <param name="IsCreated">Whether the edge is new; false when an edge of that type was updated.</param>
/// <param name="PendingResolved">Link suggestions of the pair the edge settled.</param>
public sealed record Linked(string From, string Type, string To, bool IsCreated, int PendingResolved);
