namespace Snail.MCP.Memory.Core.Graph;

/// <summary>Closed vocabulary of edge types — all of them directed.</summary>
/// <remarks>Closed so the model cannot invent synonyms that make the graph unqueryable.</remarks>
public static class LinkTypes
{
    /// <summary>Source uses target.</summary>
    public const string Uses = "uses";

    /// <summary>Source depends on target.</summary>
    public const string DependsOn = "depends_on";

    /// <summary>Source is the design of target.</summary>
    public const string DesignOf = "design_of";

    /// <summary>Source is part of target.</summary>
    public const string PartOf = "part_of";

    /// <summary>Source implements target.</summary>
    public const string Implements = "implements";

    /// <summary>Source documents target.</summary>
    public const string Documents = "documents";

    /// <summary>Every allowed type.</summary>
    public static readonly IReadOnlyList<string> All =
        [Uses, DependsOn, DesignOf, PartOf, Implements, Documents];

    /// <summary>Human-readable wording used when an edge is rendered.</summary>
    public static string Describe(string type) => type switch
    {
        Uses => "uses",
        DependsOn => "depends on",
        DesignOf => "design of",
        PartOf => "part of",
        Implements => "implements",
        Documents => "documents",
        _ => type
    };

    /// <summary>Normalizes user input to a canonical type; <c>false</c> means the caller must report the allowed list.</summary>
    public static bool TryNormalize(string? raw, out string type)
    {
        type = (raw ?? "").Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');

        return All.Contains(type);
    }

    /// <summary>Comma-separated list of allowed types for error messages.</summary>
    public static string AllowedList() => string.Join(", ", All);
}
