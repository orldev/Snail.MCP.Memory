namespace Snail.MCP.Memory.Core.Graph;

/// <summary>Literal detection of project names inside record text.</summary>
/// <remarks>Deliberately verbatim: a mention must quote the name — semantic matching is the background audit's business.</remarks>
public static class ProjectMentions
{
    /// <summary>Whether <paramref name="text"/> mentions <paramref name="name"/> as a whole word, case-insensitively.</summary>
    public static bool Mentions(string text, string name)
    {
        if (name.Length == 0)
            return false;

        var index = 0;

        while (index <= text.Length - name.Length &&
               (index = text.IndexOf(name, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            if (BoundaryAt(text, index - 1, outward: -1) && BoundaryAt(text, index + name.Length, outward: +1))
                return true;

            index += 1;
        }

        return false;
    }

    /// <summary>
    /// Whether the character next to the match ends the name there. A joiner ('.', '-', '_')
    /// ends it only when nothing alphanumeric continues past it in the <paramref name="outward"/>
    /// direction — so a trailing sentence dot is a boundary, while <c>Snail.MCP.Memory.Tests</c>
    /// does not contain the name <c>Snail.MCP.Memory</c>.
    /// </summary>
    private static bool BoundaryAt(string text, int position, int outward)
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
