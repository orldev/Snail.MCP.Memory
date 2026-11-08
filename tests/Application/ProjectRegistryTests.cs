using Snail.MCP.Memory.Tests.TestDoubles;

namespace Snail.MCP.Memory.Tests.Application;

/// Init with the experience briefing, listing and renaming over a real database.
public sealed class ProjectRegistryTests : IDisposable
{
    private readonly MemoryHarness _harness = new();
    private readonly ProjectRegistry _service;
    private readonly RecordLifecycle _memory;

    public ProjectRegistryTests()
    {
        _service = new ProjectRegistry(
            _harness.Projects, _harness.Records, _harness.Graph, _harness.Pending, _harness.Embedder, _harness.Time,
            similarityFloor: 0.5);
        _memory = new RecordLifecycle(
            _harness.Records, _harness.Projects, _harness.Embedder,
            new KnowledgeGraph(_harness.Graph, _harness.Projects, _harness.Pending, _harness.Time), _harness.Time, 0.95, 0.5);
    }

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Init_CreatesOnceAndReportsAnExistingProject()
    {
        var first = await _service.InitAsync("ProjectA");
        var second = await _service.InitAsync("projecta");

        Assert.Equal(("ProjectA", true), (first.Value!.Project, first.Value.IsCreated));
        Assert.Equal(("ProjectA", false), (second.Value!.Project, second.Value.IsCreated));
    }

    [Fact]
    public async Task Init_WithoutDescriptionOrSeeds_LooksForNoBriefing()
    {
        var briefing = (await _service.InitAsync("ProjectA")).Value!.Briefing;

        Assert.False(briefing.WasAsked);
        Assert.Empty(briefing.Experience);
    }

    [Fact]
    public async Task Init_WithAnEmptyName_IsRefused()
    {
        Refused.As(Refusals.EmptyName, Messages.ProjectNameEmpty, await _service.InitAsync("  "));
    }

    /// The briefing is the experience transfer: a description similar to another project's
    /// records surfaces that project and its transferable knowledge in the init response.
    [Fact]
    public async Task Init_WithDescriptionBriefsFromSimilarProjects()
    {
        await _memory.RememberAsync("OldProject", "experience with MCP servers", kind: "lesson");

        var briefing = (await _service.InitAsync("NewProject", description: "experience with MCP servers")).Value!.Briefing;

        Assert.True(briefing.WasAsked);
        Assert.Equal("OldProject", Assert.Single(briefing.SimilarProjects).Project);
        Assert.Equal(RecordKinds.Lesson, Assert.Single(briefing.Experience).Record.Kind);
    }

    [Fact]
    public async Task Init_SeedFromPullsTransferableRecordsAndNotesMissingDonors()
    {
        await _memory.RememberAsync("Donor", "a decision about the stack", kind: "decision");
        await _memory.RememberAsync("Donor", "a fleeting event", kind: "event");

        var briefing = (await _service.InitAsync("NewProject", seedFrom: "Donor + Ghost")).Value!.Briefing;

        Assert.Equal("a decision about the stack", Assert.Single(briefing.Experience).Record.Content);
        Assert.Equal(["Ghost"], briefing.MissingSeeds);
    }

    [Fact]
    public async Task List_FiltersByPrefixCaseInsensitively()
    {
        await _service.InitAsync("Alpha");
        await _service.InitAsync("Beta");

        var listing = await _service.ListAsync("al");

        Assert.Equal("al", listing.Prefix);
        Assert.Equal("Alpha", Assert.Single(listing.Projects).Name);
    }

    /// Re-initializing an existing project surfaces its unresolved suggestions — the queue
    /// greets every session start until it is worked off.
    [Fact]
    public async Task Init_OfAnExistingProjectListsItsPendingSuggestions()
    {
        await _service.InitAsync("A");
        await _service.InitAsync("B");
        await _harness.Pending.TryProposeAsync(new PendingLink("A", "B", "a record", _harness.Time.GetUtcNow()));

        var answer = await _service.InitAsync("A");

        Assert.Equal("a record", Assert.Single(answer.Value!.Pending).Evidence);
    }

    [Fact]
    public async Task Rename_MovesPendingSuggestionsToo()
    {
        await _service.InitAsync("Old");
        await _service.InitAsync("Other");
        await _harness.Pending.TryProposeAsync(new PendingLink("Old", "Other", "", _harness.Time.GetUtcNow()));

        await _service.RenameAsync("Old", "Fresh");

        Assert.Equal("Fresh", Assert.Single(await _harness.Pending.ListAsync("fresh")).Project);
        Assert.Empty(await _harness.Pending.ListAsync("old"));
    }

    [Fact]
    public async Task Rename_MovesRegistryRecordsAndEdges()
    {
        await _memory.RememberAsync("Old", "knowledge");
        await _service.InitAsync("Other");
        await _harness.Graph.TryInsertAsync(new GraphEdge("Old", "Other", LinkTypes.Uses, "", _harness.Time.GetUtcNow()));

        var answer = await _service.RenameAsync("old", "Fresh");

        Assert.Equal(new Renamed("old", "Fresh", 1, 1), answer.Value);
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

        Refused.As(Refusals.NotFound, Messages.ProjectNotFound("Ghost"), await _service.RenameAsync("Ghost", "C"));
        Refused.As(Refusals.NameTaken, Messages.RenameTargetTaken("B"), await _service.RenameAsync("A", "b"));
    }

    /// Changing only the letter case of a name is a legal rename, not a collision with itself.
    [Fact]
    public async Task Rename_AllowsChangingOnlyTheCase()
    {
        await _service.InitAsync("myproject");

        var answer = await _service.RenameAsync("myproject", "MyProject");

        Assert.Equal(("myproject", "MyProject"), (answer.Value!.From, answer.Value.To));
        Assert.Equal("MyProject", (await _harness.Projects.GetAsync("myproject"))!.Name);
    }
}
