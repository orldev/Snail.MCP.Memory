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
}
