using Snail.MCP.Memory.Tests.TestDoubles;

namespace Snail.MCP.Memory.Tests.Infrastructure;

/// The pending suggestion lifecycle over a real database: propose once per pair, list open
/// ones, resolve on confirmation, dismiss forever, rename, honor the workspace partition.
public sealed class EfPendingLinkStoreTests : IDisposable
{
    private readonly MemoryHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    private PendingLink Suggestion(string project = "A", string other = "B", string evidence = "seen in a record") =>
        new(project, other, evidence, _harness.Time.GetUtcNow());

    [Fact]
    public async Task Propose_StoresOncePerPairRegardlessOfOrderAndCase()
    {
        Assert.True(await _harness.Pending.TryProposeAsync(Suggestion()));
        Assert.False(await _harness.Pending.TryProposeAsync(Suggestion()));
        Assert.False(await _harness.Pending.TryProposeAsync(Suggestion(project: "b", other: "a")));

        var open = await _harness.Pending.ListAsync("a");

        Assert.Single(open);
        Assert.Equal("seen in a record", open[0].Evidence);
    }

    [Fact]
    public async Task List_SeesThePairFromEitherEndAndEverythingWithoutAKey()
    {
        await _harness.Pending.TryProposeAsync(Suggestion());
        await _harness.Pending.TryProposeAsync(Suggestion(project: "C", other: "D"));

        Assert.Single(await _harness.Pending.ListAsync("b"));
        Assert.Single(await _harness.Pending.ListAsync("a"));
        Assert.Empty(await _harness.Pending.ListAsync("ghost"));
        Assert.Equal(2, (await _harness.Pending.ListAsync()).Count);
    }

    [Fact]
    public async Task Resolve_RemovesThePairInEitherOrder()
    {
        await _harness.Pending.TryProposeAsync(Suggestion());

        Assert.Equal(1, await _harness.Pending.ResolveAsync("b", "a"));
        Assert.Empty(await _harness.Pending.ListAsync());
        Assert.True(await _harness.Pending.TryProposeAsync(Suggestion()));
    }

    /// A dismissed pair leaves the listing but keeps blocking re-proposal — that is the whole
    /// point of keeping the row.
    [Fact]
    public async Task Dismiss_HidesThePairAndBlocksReproposal()
    {
        await _harness.Pending.TryProposeAsync(Suggestion());
        await _harness.Pending.DismissAsync("B", "A", _harness.Time.GetUtcNow());

        Assert.Empty(await _harness.Pending.ListAsync());
        Assert.False(await _harness.Pending.TryProposeAsync(Suggestion()));
    }

    [Fact]
    public async Task Dismiss_OfANeverProposedPairStillBlocksFutureProposals()
    {
        await _harness.Pending.DismissAsync("A", "B", _harness.Time.GetUtcNow());

        Assert.False(await _harness.Pending.TryProposeAsync(Suggestion()));
    }

    [Fact]
    public async Task Rename_RewritesBothEndsOfTheSuggestions()
    {
        await _harness.Pending.TryProposeAsync(Suggestion());
        await _harness.Pending.TryProposeAsync(Suggestion(project: "C", other: "A"));

        Assert.Equal(2, await _harness.Pending.RenameProjectAsync("a", "Alpha"));

        var open = await _harness.Pending.ListAsync("alpha");

        Assert.Equal(2, open.Count);
        Assert.All(open, link => Assert.Contains("Alpha", new[] { link.Project, link.Other }));
    }

    /// Two workspaces over one database never see each other's suggestions; the empty
    /// workspace reads the whole base.
    [Fact]
    public async Task Workspaces_PartitionTheSuggestions()
    {
        var left = _harness.PendingIn("left");
        var right = _harness.PendingIn("right");

        await left.TryProposeAsync(Suggestion());

        Assert.Single(await left.ListAsync());
        Assert.Empty(await right.ListAsync());
        Assert.True(await right.TryProposeAsync(Suggestion()));
        Assert.Equal(2, (await _harness.PendingIn("").ListAsync()).Count);
    }
}
