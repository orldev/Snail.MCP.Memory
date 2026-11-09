using Snail.MCP.Memory.Tests.TestDoubles;

namespace Snail.MCP.Memory.Tests.Application;

/// Linking rules and graph reading over a real database.
public sealed class KnowledgeGraphTests : IDisposable
{
    private readonly MemoryHarness _harness = new();
    private readonly KnowledgeGraph _service;

    public KnowledgeGraphTests()
    {
        _service = new KnowledgeGraph(_harness.Graph, _harness.Projects, _harness.Pending, _harness.Time);
    }

    public void Dispose() => _harness.Dispose();

    private Task Register(params string[] names) =>
        Task.WhenAll(names.Select(name =>
            _harness.Projects.TryCreateAsync(new ProjectInfo(name, "", _harness.Time.GetUtcNow()))));

    [Fact]
    public async Task Link_CreatesThenUpdates()
    {
        await Register("A", "B");

        Assert.Equal(new Linked("A", LinkTypes.Uses, "B", IsCreated: true, 0), (await _service.LinkAsync("A", "B", LinkTypes.Uses)).Value);
        Assert.Equal(new Linked("A", LinkTypes.Uses, "B", IsCreated: false, 0), (await _service.LinkAsync("A", "B", LinkTypes.Uses, "note")).Value);
    }

    /// An edge to a project that has no memory is a phantom node; both ends must exist.
    [Fact]
    public async Task Link_RefusesAnUnregisteredEnd()
    {
        await Register("A");

        Refused.As(Refusals.NotFound, Messages.ProjectNotFound("Ghost"), await _service.LinkAsync("A", "Ghost", LinkTypes.Uses));
        Assert.Empty(await _harness.Graph.GetAllAsync());
    }

    [Fact]
    public async Task Link_RefusesSelfAndEmptyEnds()
    {
        Refused.As(Refusals.SelfLink, Messages.SelfLinkForbidden, await _service.LinkAsync("A", "a", LinkTypes.Uses));
        Refused.As(Refusals.EmptyName, Messages.BothProjectsRequired, await _service.LinkAsync("A", " ", LinkTypes.Uses));
    }

    [Fact]
    public async Task Unlink_RemovesByTypeOrEverythingAndRejectsInventedTypes()
    {
        await Register("A", "B");
        await _service.LinkAsync("A", "B", LinkTypes.Uses);
        await _service.LinkAsync("B", "A", LinkTypes.Documents);

        var invented = await _service.UnlinkAsync("A", "B", "inspired_by");

        Refused.As(Refusals.UnknownLinkType, Messages.UnknownLinkType("inspired_by"), invented);
        Assert.Contains(LinkTypes.DependsOn, invented.Hint, StringComparison.Ordinal);
        Assert.Equal(new Unlinked("A", "B", LinkTypes.Documents, 1), (await _service.UnlinkAsync("A", "B", LinkTypes.Documents)).Value);
        Assert.Equal(1, (await _service.UnlinkAsync("A", "B")).Value!.Removed);
        Assert.Equal(0, (await _service.UnlinkAsync("A", "B")).Value!.Removed);
    }

    [Fact]
    public async Task Read_WholeGraphAndEmptyGraph()
    {
        Assert.Empty((await _service.ReadAsync()).Edges);

        await Register("A", "B");
        await _service.LinkAsync("A", "B", LinkTypes.Uses, "runtime");

        var whole = await _service.ReadAsync();
        var edge = Assert.Single(whole.Edges);

        Assert.Empty(whole.Roots);
        Assert.Equal(("A", LinkTypes.Uses, "B", "runtime"), (edge.Source, edge.Type, edge.Target, edge.Note));
    }

    /// Confirming an edge settles the pair's suggestion — the queue empties and the answer
    /// counts it.
    [Fact]
    public async Task Link_ResolvesThePendingSuggestionOfThePair()
    {
        await Register("A", "B");
        await _harness.Pending.TryProposeAsync(new PendingLink("B", "A", "", _harness.Time.GetUtcNow()));

        var answer = await _service.LinkAsync("A", "B", LinkTypes.Uses);

        Assert.Equal(1, answer.Value!.PendingResolved);
        Assert.Empty(await _harness.Pending.ListAsync());
    }

    [Fact]
    public async Task Dismiss_RejectsThePairAndValidatesLikeLink()
    {
        await Register("A", "B");
        await _harness.Pending.TryProposeAsync(new PendingLink("A", "B", "", _harness.Time.GetUtcNow()));

        Refused.As(Refusals.NotFound, Messages.ProjectNotFound("Ghost"), await _service.DismissAsync("A", "Ghost"));
        Refused.As(Refusals.SelfLink, Messages.SelfLinkForbidden, await _service.DismissAsync("A", "a"));
        Assert.Equal(new Dismissed("A", "B"), (await _service.DismissAsync("A", "B")).Value);
        Assert.Empty(await _harness.Pending.ListAsync());
    }

    /// The mention scan proposes only registered strangers: the own project, an already
    /// linked project and an unknown name all stay out of the queue.
    [Fact]
    public async Task ProposeMentions_QueuesOnlyUnlinkedRegisteredProjects()
    {
        await Register("Alpha", "Beta", "Gamma");
        await _service.LinkAsync("Alpha", "Gamma", LinkTypes.Uses);

        var proposed = await _service.ProposeMentionsAsync(
            "Alpha", "Alpha talks to Beta and Gamma, and to the unknown Delta", "some record", CancellationToken.None);

        var link = Assert.Single(proposed);

        Assert.Equal("Beta", link.Other);
        Assert.Equal("some record", link.Evidence);
        Assert.Single(await _harness.Pending.ListAsync("alpha"));
    }

    /// Sync writes auto edges and resolves the pair's suggestion; a target without memory is
    /// skipped and named, not guessed into existence.
    [Fact]
    public async Task Sync_CreatesAutoEdgesResolvesPendingAndSkipsUnknownTargets()
    {
        await Register("A", "B");
        await _harness.Pending.TryProposeAsync(new PendingLink("A", "B", "", _harness.Time.GetUtcNow()));

        var answer = await _service.SyncAsync("A",
            [new RecordLink("B", LinkType.DependsOn), new RecordLink("Ghost", LinkType.Uses)], "csproj");

        Assert.Equal((1, 0, 0, 0), Counts(answer));
        Assert.Equal(["Ghost"], answer.Value!.Skipped);
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

        Assert.Equal((0, 0, 1, 1), Counts(second));

        var edges = await _harness.Graph.GetAllAsync();

        Assert.Equal("hand-written", edges.Single(e => e.Target == "B").Note);
        Assert.StartsWith(Messages.AutoNote("obsidian") + Messages.StaleMarker, edges.Single(e => e.Target == "C").Note);

        var third = await _service.SyncAsync("A", [new RecordLink("B", LinkType.Uses)], "obsidian");

        Assert.Equal((0, 0, 1, 0), Counts(third));
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

        Assert.Equal((0, 1, 0, 0), Counts(answer));
        Assert.Equal(Messages.AutoNote("obsidian"), Assert.Single(await _harness.Graph.GetAllAsync()).Note);
    }

    /// Depth expands through neighbors: from A with depth 2 the far edge B→C becomes visible,
    /// with depth 1 it does not.
    [Fact]
    public async Task Read_ExpandsByDepth()
    {
        await Register("A", "B", "C");
        await _service.LinkAsync("A", "B", LinkTypes.Uses);
        await _service.LinkAsync("B", "C", LinkTypes.Uses);

        Assert.DoesNotContain((await _service.ReadAsync("A", depth: 1)).Edges, edge => edge.Source == "B" && edge.Target == "C");
        Assert.Contains((await _service.ReadAsync("A", depth: 2)).Edges, edge => edge.Source == "B" && edge.Target == "C");
    }

    private static (int Created, int Refreshed, int Kept, int Stale) Counts(Answer<Synced> answer)
    {
        Assert.True(answer.TryGet(out var synced), answer.Error);

        return (synced.Created, synced.Refreshed, synced.Kept, synced.Stale);
    }
}
