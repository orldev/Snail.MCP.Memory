namespace Snail.MCP.Memory.Core.Answers;

/// <summary>A project forgotten whole, or what forgetting it would delete.</summary>
/// <param name="Project">The project, as the registry names it.</param>
/// <param name="IsDeleted">Whether it is gone; false — nothing was deleted, and the counts are what a confirmed call deletes.</param>
/// <param name="Records">Its records.</param>
/// <param name="Edges">Graph edges touching it, in either direction.</param>
/// <param name="Suggestions">Its open link suggestions.</param>
public sealed record ProjectForgotten(string Project, bool IsDeleted, int Records, int Edges, int Suggestions);
