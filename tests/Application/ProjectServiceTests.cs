using Snail.MCP.Memory.Tests.TestDoubles;

namespace Snail.MCP.Memory.Tests.Application;

/// Init with the experience briefing, listing and renaming over a real database.
public sealed class ProjectServiceTests : IDisposable
{
    private readonly MemoryHarness _harness = new();
    private readonly ProjectService _service;
    private readonly MemoryService _memory;

    public ProjectServiceTests()
    {
        _service = new ProjectService(
            _harness.Projects, _harness.Records, _harness.Graph, _harness.Embedder, _harness.Time,
            similarityFloor: 0.5);
        _memory = new MemoryService(
            _harness.Records, _harness.Projects, _harness.Embedder, _harness.Time, 0.95, 0.5);
    }

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Init_CreatesOnceAndReportsAnExistingProject()
    {
        Assert.StartsWith(Messages.ProjectCreated("ProjectA"), await _service.InitAsync("ProjectA"));
        Assert.StartsWith(Messages.ProjectAlreadyExists("ProjectA"), await _service.InitAsync("projecta"));
    }

    [Fact]
    public async Task Init_WithoutDescriptionHintsHowToGetABriefing()
    {
        Assert.Contains(Messages.BriefingHint, await _service.InitAsync("ProjectA"));
    }

    /// The briefing is the experience transfer: a description similar to another project's
    /// records surfaces that project and its transferable knowledge in the init response.
    [Fact]
    public async Task Init_WithDescriptionBriefsFromSimilarProjects()
    {
        await _memory.RememberAsync("OldProject", "experience with MCP servers", kind: "lesson");

        var answer = await _service.InitAsync("NewProject", description: "experience with MCP servers");

        Assert.Contains(Messages.BriefingTitle, answer);
        Assert.Contains("OldProject", answer);
        Assert.Contains("[lesson]", answer);
    }

    [Fact]
    public async Task Init_SeedFromPullsTransferableRecordsAndNotesMissingDonors()
    {
        await _memory.RememberAsync("Donor", "a decision about the stack", kind: "decision");
        await _memory.RememberAsync("Donor", "a fleeting event", kind: "event");

        var answer = await _service.InitAsync("NewProject", seedFrom: "Donor + Ghost");

        Assert.Contains("a decision about the stack", answer);
        Assert.DoesNotContain("a fleeting event", answer);
        Assert.Contains(Messages.SeedProjectMissing("Ghost"), answer);
    }

    [Fact]
    public async Task List_FiltersByPrefixCaseInsensitively()
    {
        await _service.InitAsync("Alpha");
        await _service.InitAsync("Beta");

        var answer = await _service.ListAsync("al");

        Assert.Contains("Alpha", answer);
        Assert.DoesNotContain("Beta", answer);
    }

    [Fact]
    public async Task Rename_MovesRegistryRecordsAndEdges()
    {
        await _memory.RememberAsync("Old", "knowledge");
        await _service.InitAsync("Other");
        await _harness.Graph.UpsertAsync(new GraphEdge("Old", "Other", LinkTypes.Uses, "", _harness.Time.GetUtcNow()));

        var answer = await _service.RenameAsync("old", "Fresh");

        Assert.Equal(Messages.ProjectRenamed("old", "Fresh", 1, 1), answer);
        Assert.Null(await _harness.Projects.GetAsync("old"));
        Assert.Equal("Fresh", (await _harness.Projects.GetAsync("fresh"))!.Name);
        Assert.Equal("Fresh", Assert.Single(await _harness.Records.ListAsync(new RecordFilter("fresh"), 10)).Project);
        Assert.Equal("Fresh", Assert.Single(await _harness.Graph.GetAllAsync()).Source);
    }

    [Fact]
    public async Task Rename_RefusesAMissingSourceAndATakenTarget()
    {
        await _service.InitAsync("A");
        await _service.InitAsync("B");

        Assert.Equal(Messages.ProjectNotFound("Ghost"), await _service.RenameAsync("Ghost", "C"));
        Assert.Equal(Messages.RenameTargetTaken("B"), await _service.RenameAsync("A", "b"));
    }

    /// Changing only the letter case of a name is a legal rename, not a collision with itself.
    [Fact]
    public async Task Rename_AllowsChangingOnlyTheCase()
    {
        await _service.InitAsync("myproject");

        var answer = await _service.RenameAsync("myproject", "MyProject");

        Assert.StartsWith("Project 'myproject' renamed to 'MyProject'", answer);
        Assert.Equal("MyProject", (await _harness.Projects.GetAsync("myproject"))!.Name);
    }
}
