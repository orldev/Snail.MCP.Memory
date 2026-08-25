namespace Snail.MCP.Memory.Core.Utilities;

/// <summary>
/// Normalizes a project name into the canonical identity used across the whole application.
/// </summary>
/// <remarks>
/// Knows only about identity: surrounding whitespace, case-insensitive comparison and the
/// case-folded storage key. Storage concerns deliberately do not belong here.
/// </remarks>
internal static class ProjectIdentity
{
    /// <summary>How project and section names are compared everywhere.</summary>
    public static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>Trims the name; <c>null</c> becomes an empty string.</summary>
    public static string Normalize(string? projectName) => projectName?.Trim() ?? "";

    /// <summary>
    /// The storage key of a project: the trimmed name case-folded with the invariant culture, so
    /// that non-ASCII names compare the same way SQL equality does.
    /// </summary>
    public static string Key(string? projectName) => Normalize(projectName).ToLowerInvariant();

    /// <summary>Case-insensitive equality of two project names.</summary>
    public static bool Equal(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>Case-insensitive prefix test; an empty prefix matches every name.</summary>
    public static bool StartsWith(string? projectName, string? prefix) =>
        string.IsNullOrWhiteSpace(prefix) ||
        Normalize(projectName).StartsWith(prefix.Trim(), StringComparison.OrdinalIgnoreCase);
}
