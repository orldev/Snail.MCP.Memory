namespace Snail.MCP.Memory.Core.Graph;

/// <summary>Literal detection of project names inside record text.</summary>
/// <remarks>
/// <para>Deliberately verbatim: a mention must quote the name — semantic matching is the background audit's business.</para>
/// <para>
/// Two kinds of match are words rather than mentions. A name of one or two letters (a project called <c>UI</c>) is a
/// word of the text far more often than a project, and the folder of a home path (<c>/Users/orldev/…</c>) names the
/// person whose machine it is: on 09.11.2026 they had queued 18 and 42 suggestions, every one of them false.
/// </para>
/// </remarks>
public static class ProjectMentions
{
    /// <summary>The fewest characters a name needs to be matched at all.</summary>
    internal const int ShortestName = 3;

    private static readonly string[] HomeFolders = ["/Users/", "/home/", "\\Users\\"];

    /// <summary>Whether <paramref name="text"/> mentions <paramref name="name"/> as a whole word, case-insensitively, outside a home path's user folder.</summary>
    public static bool Mentions(string text, string name)
    {
        if (name.Length < ShortestName)
            return false;

        var index = 0;

        while (index <= text.Length - name.Length &&
               (index = text.IndexOf(name, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            if (IsBoundaryAt(text, index - 1, outward: -1) &&
                IsBoundaryAt(text, index + name.Length, outward: +1) &&
                !IsHomeFolderAt(text, index))
                return true;

            index += 1;
        }

        return false;
    }

    private static bool IsHomeFolderAt(string text, int index)
    {
        var before = text.AsSpan(0, index);

        foreach (var folder in HomeFolders)
        {
            if (before.EndsWith(folder, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Whether the character next to the match ends the name there. A joiner ('.', '-', '_')
    /// ends it only when nothing alphanumeric continues past it in the <paramref name="outward"/>
    /// direction — so a trailing sentence dot is a boundary, while <c>Snail.MCP.Memory.Tests</c>
    /// does not contain the name <c>Snail.MCP.Memory</c>.
    /// </summary>
    private static bool IsBoundaryAt(string text, int position, int outward)
    {
        if (position < 0 || position >= text.Length)
            return true;

        var character = text[position];

        if (char.IsLetterOrDigit(character))
            return false;

        if (character is not ('.' or '-' or '_'))
            return true;

        var beyond = position + outward;

        return beyond < 0 || beyond >= text.Length || !char.IsLetterOrDigit(text[beyond]);
    }
}
