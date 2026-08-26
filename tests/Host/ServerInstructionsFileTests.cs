namespace Snail.MCP.Memory.Tests.Host;

/// <summary>
/// The repository's <c>server-instructions.md</c> is what users paste into their agent's
/// instruction file; it quotes the same closed vocabularies the tool descriptions quote, and a
/// quote drifts silently. These tests read the file straight from the repository.
/// </summary>
public sealed class ServerInstructionsFileTests
{
    /// <summary>The manual shortcuts the instructions promise, as shipped in .claude/commands.</summary>
    private static readonly string[] CommandNames =
        ["mem0-init", "mem0-recall", "mem0-remember", "mem0-link", "mem0-graph", "mem0-list"];

    private static string Text { get; } = File.ReadAllText(FindInRepository("server-instructions.md"));

    [Fact]
    public void Instructions_QuoteEveryCanonicalKind()
    {
        foreach (var kind in RecordKinds.Canonical)
            Assert.Contains(kind, Text);
    }

    [Fact]
    public void Instructions_QuoteEveryAllowedLinkType()
    {
        foreach (var type in LinkTypes.All)
            Assert.Contains(type, Text);
    }

    /// The pending queue is useless if the instructions do not say how to empty it.
    [Fact]
    public void Instructions_NameBothResolutionsOfAPendingSuggestion()
    {
        Assert.Contains("dismiss_link", Text);
        Assert.Contains("link_projects", Text);
        Assert.Contains("links", Text);
        Assert.Contains("sync_project_links", Text);
    }

    [Fact]
    public void Instructions_NameEveryShippedCommand()
    {
        foreach (var command in CommandNames)
        {
            Assert.Contains(command, Text);
            Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(FindInRepository("server-instructions.md"))!,
                ".claude", "commands", $"{command}.md")), $"the instructions promise /{command} but the repo ships no such command");
        }
    }

    /// <summary>Walks up from the test binaries to the repository root, which holds the file.</summary>
    private static string FindInRepository(string fileName)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, fileName);

            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException($"'{fileName}' was not found above '{AppContext.BaseDirectory}'.");
    }
}
