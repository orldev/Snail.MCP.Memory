namespace Snail.MCP.Memory.Tests.Core;

/// Mention detection is deliberately literal; these tests pin where a name ends.
public sealed class ProjectMentionsTests
{
    [Theory]
    [InlineData("depends on Snail.MCP.Figma at runtime")]
    [InlineData("порт конфигурации из Snail.MCP.Figma.")]
    [InlineData("snail.mcp.figma is the donor")]
    [InlineData("(Snail.MCP.Figma)")]
    public void Mentions_FindsTheNameAsAWholeWord(string text)
    {
        Assert.True(ProjectMentions.Mentions(text, "Snail.MCP.Figma"));
    }

    /// A longer identifier that merely contains the name is a different project.
    [Theory]
    [InlineData("Snail.MCP.Figma.Tests is green")]
    [InlineData("see MySnail.MCP.Figma instead")]
    [InlineData("Snail.MCP.Figma2 shipped")]
    [InlineData("pre-Snail.MCP.Figma era")]
    public void Mentions_RefusesTheNameInsideALongerIdentifier(string text)
    {
        Assert.False(ProjectMentions.Mentions(text, "Snail.MCP.Figma"));
    }

    [Fact]
    public void Mentions_RefusesAnEmptyNameAndAnAbsentName()
    {
        Assert.False(ProjectMentions.Mentions("some text", ""));
        Assert.False(ProjectMentions.Mentions("some text", "Keynex_App"));
    }

    /// A project named UI was suggested as a link to every record that said "UI" — eighteen of them, none a mention.
    [Theory]
    [InlineData("Design and UI", "UI")]
    [InlineData("v 2.0 ships", "v")]
    public void Mentions_RefusesANameOfFewerThanThreeLetters(string text, string name)
    {
        Assert.False(ProjectMentions.Mentions(text, name));
    }

    [Fact]
    public void Mentions_MatchesANameOfThreeLetters()
    {
        Assert.True(ProjectMentions.Mentions("the Pix bot", "Pix"));
    }

    /// The home folder is the person whose machine it is: every path under it named a project called orldev.
    [Theory]
    [InlineData("hooks in /Users/orldev/.claude/settings.json")]
    [InlineData("see /home/orldev/projects")]
    [InlineData(@"C:\Users\orldev\AppData")]
    public void Mentions_RefusesTheUserFolderOfAHomePath(string text)
    {
        Assert.False(ProjectMentions.Mentions(text, "orldev"));
    }

    /// Only the user folder is refused: a project's own folder in a path, or the name in prose, still counts.
    [Theory]
    [InlineData("/Users/orldev/Projects/Snail.MCP.Figma/src", "Snail.MCP.Figma")]
    [InlineData("orldev wrote it, see /Users/orldev/x", "orldev")]
    public void Mentions_MatchesANameOutsideTheUserFolder(string text, string name)
    {
        Assert.True(ProjectMentions.Mentions(text, name));
    }
}
