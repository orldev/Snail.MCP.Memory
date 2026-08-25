namespace Snail.MCP.Memory.Core.Records;

/// <summary>What a search or listing is narrowed to; empty parts mean "any".</summary>
/// <param name="ProjectKey">Case-folded project key (<see cref="Utilities.ProjectIdentity.Key"/>); empty — every project.</param>
/// <param name="Kind">Normalized kind; empty — every kind.</param>
public readonly record struct RecordFilter(string? ProjectKey = null, string? Kind = null)
{
    /// <summary>Whether the filter narrows by project.</summary>
    public bool HasProject => !string.IsNullOrEmpty(ProjectKey);

    /// <summary>Whether the filter narrows by kind.</summary>
    public bool HasKind => !string.IsNullOrEmpty(Kind);
}
