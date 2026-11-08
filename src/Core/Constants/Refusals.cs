namespace Snail.MCP.Memory.Core.Constants;

/// <summary>The kinds of failure the tools answer with, the envelope's <c>type</c>: words a program can branch on.</summary>
public static class Refusals
{
    /// <summary>A project name, or both ends of a link, left empty.</summary>
    public const string EmptyName = "emptyName";

    /// <summary>No name was given and the client's folder names no project.</summary>
    public const string UnresolvedName = "unresolvedName";

    /// <summary>A project or a record that is not in memory.</summary>
    public const string NotFound = "notFound";

    /// <summary>A rename to a name another project has.</summary>
    public const string NameTaken = "nameTaken";

    /// <summary>A record id that is not one.</summary>
    public const string InvalidId = "invalidId";

    /// <summary>A record refused because a very similar one exists.</summary>
    public const string Duplicate = "duplicate";

    /// <summary>A project linked to itself.</summary>
    public const string SelfLink = "selfLink";

    /// <summary>An edge type outside the closed list.</summary>
    public const string UnknownLinkType = "unknownLinkType";
}
