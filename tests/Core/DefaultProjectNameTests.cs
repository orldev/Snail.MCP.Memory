using Snail.MCP.Memory.Core.Utilities;

namespace Snail.MCP.Memory.Tests.Core;

/// The working directory is what makes init_project_memory usable without arguments, so the two
/// halves of that promise are pinned here: a real project folder becomes the name, and a
/// container folder is refused instead of becoming memory shared by everything underneath it.
public class DefaultProjectNameTests
{
    private static string Path(params string[] segments) =>
        System.IO.Path.Combine([System.IO.Path.GetTempPath(), .. segments]);

    [Fact]
    public void FromDirectory_TakesTheLastSegment()
    {
        Assert.Equal("ProjectA", DefaultProjectName.FromDirectory(Path("work", "ProjectA")));
    }

    [Fact]
    public void FromDirectory_IgnoresATrailingSeparator()
    {
        Assert.Equal("ProjectA", DefaultProjectName.FromDirectory(Path("work", "ProjectA") + System.IO.Path.DirectorySeparatorChar));
    }

    [Theory]
    [InlineData("src")]
    [InlineData("Projects")]
    [InlineData("WORK")]
    [InlineData("design")]
    public void FromDirectory_RefusesAContainerFolder(string folder)
    {
        Assert.Equal("", DefaultProjectName.FromDirectory(Path(folder)));
    }

    [Fact]
    public void FromDirectory_RefusesTheProfileItself()
    {
        var home = Path("home-of-someone");

        Assert.Equal("", DefaultProjectName.FromDirectory(home, home));
    }

    /// A folder whose name happens to match the profile's is still a project when it sits
    /// somewhere else — the refusal is about the path, not about the word.
    [Fact]
    public void FromDirectory_KeepsAFolderNamedLikeTheProfile()
    {
        var home = Path("orldev");

        Assert.Equal("orldev", DefaultProjectName.FromDirectory(Path("work", "orldev"), home));
    }

    [Fact]
    public void FromDirectory_RefusesAFilesystemRoot()
    {
        Assert.Equal("", DefaultProjectName.FromDirectory(System.IO.Path.GetPathRoot(System.IO.Path.GetTempPath())));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromDirectory_RefusesAnEmptyPath(string? directory)
    {
        Assert.Equal("", DefaultProjectName.FromDirectory(directory));
    }
}
