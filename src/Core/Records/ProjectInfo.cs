namespace Snail.MCP.Memory.Core.Records;

/// <summary>
/// A registered project: the display name, what it is, and when its memory was created.
/// </summary>
/// <param name="Name">Display name; identity is its case-folded key.</param>
/// <param name="Description">Short description given at init; may be empty.</param>
/// <param name="Created">When the memory was initialized.</param>
public sealed record ProjectInfo(string Name, string Description, DateTimeOffset Created);
