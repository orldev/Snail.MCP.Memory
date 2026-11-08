namespace Snail.MCP.Memory.Core.Answers;

/// <summary>What a sync of machine-extracted relations changed.</summary>
/// <param name="Project">The project the edges start at.</param>
/// <param name="Source">What was parsed, stamped into the auto notes.</param>
/// <param name="Created">Auto edges made.</param>
/// <param name="Refreshed">Auto edges found again and restamped.</param>
/// <param name="Kept">Manual edges left as they are.</param>
/// <param name="Stale">Auto edges of this source gone from it, marked stale.</param>
/// <param name="Skipped">Targets with no memory, left out.</param>
public sealed record Synced(
    string Project, string Source, int Created, int Refreshed, int Kept, int Stale, IReadOnlyList<string> Skipped);
