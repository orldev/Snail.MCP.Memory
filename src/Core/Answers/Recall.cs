namespace Snail.MCP.Memory.Core.Answers;

/// <summary>What a recall found.</summary>
/// <param name="Project">The project searched; empty for the whole memory.</param>
/// <param name="Query">What was asked; empty for an overview.</param>
/// <param name="Kind">The kind searched; empty for every kind.</param>
/// <param name="Search">How the records were found.</param>
/// <param name="Records">The records, an overview's grouped by kind, a search's nearest first.</param>
/// <param name="Pending">The project's link suggestions still waiting for a decision; none for the whole memory.</param>
public sealed record Recall(
    string Project,
    string Query,
    string Kind,
    RecallSearch Search,
    IReadOnlyList<ScoredRecord> Records,
    IReadOnlyList<PendingLink> Pending);
