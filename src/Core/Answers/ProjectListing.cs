namespace Snail.MCP.Memory.Core.Answers;

/// <summary>The projects that have memory, ordered by name.</summary>
/// <param name="Prefix">The prefix the listing was filtered by; empty for every project.</param>
/// <param name="Projects">The projects listed.</param>
public sealed record ProjectListing(string Prefix, IReadOnlyList<ProjectInfo> Projects);
