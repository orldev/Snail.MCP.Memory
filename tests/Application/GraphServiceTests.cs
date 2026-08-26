using Snail.MCP.Memory.Tests.TestDoubles;

namespace Snail.MCP.Memory.Tests.Application;

/// Linking rules and graph rendering over a real database.
public sealed class GraphServiceTests : IDisposable
{
    private readonly MemoryHarness _harness = new();
    private readonly GraphService _service;

    public GraphServiceTests()
    {
        _service = new GraphService(_harness.Graph, _harness.Projects, _harness.Pending, _harness.Time);
    }

    public void Dispose() => _harness.Dispose();

    private Task Register(params string[] names) =>
        Task.WhenAll(names.Select(name =>
            _harness.Projects.CreateAsync(new ProjectInfo(name, "", _harness.Time.GetUtcNow()))));

    [Fact]
    public async Task Link_CreatesThenUpdates()
    {
        await Register("A", "B");

        Assert.Equal(Messages.LinkCreated("A", LinkTypes.Uses, "B"), await _service.LinkAsync("A", "B", LinkTypes.Uses));
        Assert.Equal(Messages.LinkUpdated("A", LinkTypes.Uses, "B"), await _service.LinkAsync("A", "B", LinkTypes.Uses, "note"));
    }

    /// An edge to a project that has no memory is a phantom node; both ends must exist.
    [Fact]
    public async Task Link_RefusesAnUnregisteredEnd()
    {
        await Register("A");

        Assert.Equal(Messages.ProjectNotFound("Ghost"), await _service.LinkAsync("A", "Ghost", LinkTypes.Uses));
        Assert.Empty(await _harness.Graph.GetAllAsync());
    }

    [Fact]
    public async Task Link_RefusesSelfAndEmptyEnds()
    {
        Assert.Equal(Messages.SelfLinkForbidden, await _service.LinkAsync("A", "a", LinkTypes.Uses));
        Assert.Equal(Messages.BothProjectsRequired, await _service.LinkAsync("A", " ", LinkTypes.Uses));
    }

    [Fact]
    public async Task Unlink_RemovesByTypeOrEverythingAndRejectsInventedTypes()
    {
        await Register("A", "B");
        await _service.LinkAsync("A", "B", LinkTypes.Uses);
        await _service.LinkAsync("B", "A", LinkTypes.Documents);

        Assert.Equal(Messages.UnknownLinkType("inspired_by", LinkTypes.AllowedList()),
            await _service.UnlinkAsync("A", "B", "inspired_by"));
        Assert.Equal(Messages.LinksRemoved(1, "A", "B"), await _service.UnlinkAsync("A", "B", LinkTypes.Documents));
        Assert.Equal(Messages.LinksRemoved(1, "A", "B"), await _service.UnlinkAsync("A", "B"));
        Assert.Equal(Messages.NoLinksBetween("A", "B"), await _service.UnlinkAsync("A", "B"));
    }

    [Fact]
    public async Task Render_WholeGraphAndEmptyGraph()
    {
        Assert.Contains(Messages.GraphEmpty, await _service.RenderAsync());

        await Register("A", "B");
        await _service.LinkAsync("A", "B", LinkTypes.Uses, "runtime");

        var rendered = await _service.RenderAsync();

        Assert.Contains(Messages.WholeGraphTitle, rendered);
        Assert.Contains("| A | uses | B | runtime |", rendered);
    }

    /// Confirming an edge settles the pair's suggestion — the queue empties and the answer
    /// says so.
    [Fact]
    public async Task Link_ResolvesThePendingSuggestionOfThePair()
    {
        await Register("A", "B");
        await _harness.Pending.ProposeAsync(new PendingLink("B", "A", "", _harness.Time.GetUtcNow()));

        var answer = await _service.LinkAsync("A", "B", LinkTypes.Uses);

        Assert.Equal(Messages.LinkCreated("A", LinkTypes.Uses, "B") + Messages.PendingResolved(1), answer);
        Assert.Empty(await _harness.Pending.ListAsync());
    }

    [Fact]
    public async Task Dismiss_RejectsThePairAndValidatesLikeLink()
    {
        await Register("A", "B");
        await _harness.Pending.ProposeAsync(new PendingLink("A", "B", "", _harness.Time.GetUtcNow()));

        Assert.Equal(Messages.ProjectNotFound("Ghost"), await _service.DismissAsync("A", "Ghost"));
        Assert.Equal(Messages.SelfLinkForbidden, await _service.DismissAsync("A", "a"));
        Assert.Equal(Messages.LinkDismissed("A", "B"), await _service.DismissAsync("A", "B"));
        Assert.Empty(await _harness.Pending.ListAsync());
    }

    /// The mention scan proposes only registered strangers: the own project, an already
    /// linked project and an unknown name all stay out of the queue.
    [Fact]
    public async Task ProposeMentions_QueuesOnlyUnlinkedRegisteredProjects()
    {
        await Register("A", "B", "C");
        await _service.LinkAsync("A", "C", LinkTypes.Uses);

        var proposed = await _service.ProposeMentionsAsync(
            "A", "A talks to B and C, and to the unknown D", "some record", CancellationToken.None);

        var link = Assert.Single(proposed);

        Assert.Equal("B", link.Other);
        Assert.Equal("some record", link.Evidence);
        Assert.Single(await _harness.Pending.ListAsync("a"));
    }

    /// Sync writes auto edges and resolves the pair's suggestion; a target without memory is
    /// skipped and named, not guessed into existence.
    [Fact]
    public async Task Sync_CreatesAutoEdgesResolvesPendingAndSkipsUnknownTargets()
    {
        await Register("A", "B");
        await _harness.Pending.ProposeAsync(new PendingLink("A", "B", "", _harness.Time.GetUtcNow()));

        var answer = await _service.SyncAsync("A",
            [new RecordLink("B", LinkType.DependsOn), new RecordLink("Ghost", LinkType.Uses)], "csproj");

        Assert.Contains(Messages.SyncSummary("csproj", "A", 1, 0, 0, 0), answer);
        Assert.Contains(Messages.SyncSkipped("Ghost"), answer);
        Assert.Equal(Messages.AutoNote("csproj"), Assert.Single(await _harness.Graph.GetAllAsync()).Note);
        Assert.Empty(await _harness.Pending.ListAsync());
    }

    /// A manual edge outranks the sync; an auto edge that left the source is marked stale but
    /// never deleted; the same payload twice changes nothing.
    [Fact]
    public async Task Sync_KeepsManualEdgesMarksDisappearedAutoEdgesStaleAndStaysIdempotent()
    {
        await Register("A", "B", "C");
        await _service.LinkAsync("A", "B", LinkTypes.Uses, "hand-written");
        await _service.SyncAsync("A", [new RecordLink("C", LinkType.DependsOn)], "obsidian");

        var second = await _service.SyncAsync("A", [new RecordLink("B", LinkType.Uses)], "obsidian");

        Assert.Contains(Messages.SyncSummary("obsidian", "A", 0, 0, 1, 1), second);

        var edges = await _harness.Graph.GetAllAsync();

        Assert.Equal("hand-written", edges.Single(e => e.Target == "B").Note);
        Assert.StartsWith(Messages.AutoNote("obsidian") + Messages.StaleMarker, edges.Single(e => e.Target == "C").Note);

        var third = await _service.SyncAsync("A", [new RecordLink("B", LinkType.Uses)], "obsidian");

        Assert.Contains(Messages.SyncSummary("obsidian", "A", 0, 0, 1, 0), third);
    }

    /// A relation that returns to the source clears the stale marker: the refresh rewrites the
    /// note to the clean auto tag.
    [Fact]
    public async Task Sync_RefreshRevivesAStaleEdge()
    {
        await Register("A", "C");
        await _service.SyncAsync("A", [new RecordLink("C", LinkType.DependsOn)], "obsidian");
        await _service.SyncAsync("A", [], "obsidian");

        var answer = await _service.SyncAsync("A", [new RecordLink("C", LinkType.DependsOn)], "obsidian");

        Assert.Contains(Messages.SyncSummary("obsidian", "A", 0, 1, 0, 0), answer);
        Assert.Equal(Messages.AutoNote("obsidian"), Assert.Single(await _harness.Graph.GetAllAsync()).Note);
    }

    /// Depth expands through neighbors: from A with depth 2 the far edge B→C becomes visible,
    /// with depth 1 it does not.
    [Fact]
    public async Task Render_ExpandsByDepth()
    {
        await Register("A", "B", "C");
        await _service.LinkAsync("A", "B", LinkTypes.Uses);
        await _service.LinkAsync("B", "C", LinkTypes.Uses);

        Assert.DoesNotContain("| B | uses | C |", await _service.RenderAsync("A", depth: 1));
        Assert.Contains("| B | uses | C |", await _service.RenderAsync("A", depth: 2));
    }
}
