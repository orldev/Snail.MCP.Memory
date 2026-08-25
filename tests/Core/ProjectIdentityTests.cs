using Snail.MCP.Memory.Core.Utilities;

namespace Snail.MCP.Memory.Tests.Core;

/// Project identity is a Core notion, so only normalization and comparison are covered here.
public class ProjectIdentityTests
{
    [Fact]
    public void Normalize_LeavesValidNameUnchanged()
    {
        Assert.Equal("Project", ProjectIdentity.Normalize("Project"));
    }

    [Fact]
    public void Normalize_TrimsWhitespace()
    {
        Assert.Equal("Project", ProjectIdentity.Normalize("  Project  "));
    }

    [Fact]
    public void Normalize_NullBecomesEmpty()
    {
        Assert.Equal("", ProjectIdentity.Normalize(null));
    }

    /// The project name is a database value, not a file name: file-system restrictions do not
    /// apply to it, and Core must not impose them.
    [Fact]
    public void Normalize_KeepsCharactersForbiddenInFileNames()
    {
        Assert.Equal("My/Project", ProjectIdentity.Normalize("My/Project"));
    }

    [Fact]
    public void Equal_IgnoresCase()
    {
        Assert.True(ProjectIdentity.Equal("ProjectA", "PROJECTA"));
        Assert.False(ProjectIdentity.Equal("ProjectA", "ProjectA_Figma"));
    }

    [Fact]
    public void StartsWith_IgnoresCaseAndSurroundingWhitespace()
    {
        Assert.True(ProjectIdentity.StartsWith("ProjectA_Figma", " proj "));
        Assert.False(ProjectIdentity.StartsWith("ProjectA_Figma", "figma"));
    }

    [Fact]
    public void StartsWith_EmptyPrefixMatchesEverything()
    {
        Assert.True(ProjectIdentity.StartsWith("ProjectA", ""));
        Assert.True(ProjectIdentity.StartsWith("ProjectA", null));
    }

}
