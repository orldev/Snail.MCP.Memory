namespace Snail.MCP.Memory.Core.Answers;

/// <summary>How many records <c>forget</c> deleted.</summary>
/// <param name="Count">The records deleted; an id that named no record counts nothing.</param>
public readonly record struct Forgotten(int Count);
