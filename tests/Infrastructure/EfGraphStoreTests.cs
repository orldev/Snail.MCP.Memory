using Snail.MCP.Memory.Tests.TestDoubles;

namespace Snail.MCP.Memory.Tests.Infrastructure;

public sealed class EfGraphStoreTests : IDisposable
{
    private readonly MemoryHarness _harness = new();
    private readonly IGraphStore _store;

    public EfGraphStoreTests() => _store = _harness.Graph;

    public void Dispose() => _harness.Dispose();

    private static GraphEdge Edge(string source, string target, string type = LinkTypes.Uses, string note = "") =>
        new(source, target, type, note, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Upsert_CreatesThenUpdatesByIdentity()
    {
        Assert.True(await _store.UpsertAsync(Edge("A", "B", note: "first")));
        Assert.False(await _store.UpsertAsync(Edge("a", "b", note: "second")));

        var edge = Assert.Single(await _store.GetAllAsync());

        Assert.Equal("second", edge.Note);
    }

    /// The same pair under a different type is a different fact, not an update.
    [Fact]
    public async Task Upsert_KeepsDistinctTypesAsDistinctEdges()
    {
        await _store.UpsertAsync(Edge("A", "B", LinkTypes.Uses));
        await _store.UpsertAsync(Edge("A", "B", LinkTypes.Documents));

        Assert.Equal(2, (await _store.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Remove_WorksInBothDirectionsAndByType()
    {
        await _store.UpsertAsync(Edge("A", "B", LinkTypes.Uses));
        await _store.UpsertAsync(Edge("B", "A", LinkTypes.Documents));

        Assert.Equal(1, await _store.RemoveAsync("a", "b", LinkTypes.Documents));
        Assert.Equal(1, await _store.RemoveAsync("a", "b", ""));
        Assert.Empty(await _store.GetAllAsync());
    }

    [Fact]
    public async Task GetForProjects_SeesBothDirections()
    {
        await _store.UpsertAsync(Edge("A", "B"));
        await _store.UpsertAsync(Edge("C", "A"));
        await _store.UpsertAsync(Edge("C", "D"));

        Assert.Equal(2, (await _store.GetForProjectsAsync(["a"])).Count);
    }

    [Fact]
    public async Task RenameProject_RewritesBothEndsAndTheDisplayName()
    {
        await _store.UpsertAsync(Edge("Old", "B"));
        await _store.UpsertAsync(Edge("C", "Old"));

        Assert.Equal(2, await _store.RenameProjectAsync("old", "New"));

        var edges = await _store.GetAllAsync();

        Assert.Contains(edges, edge => edge.Source == "New");
        Assert.Contains(edges, edge => edge.Target == "New");
        Assert.Equal(2, (await _store.GetForProjectsAsync(["new"])).Count);
    }

    /// Each partitioned store links inside its own workspace; the same pair may carry different
    /// edges in different workspaces, and the unpartitioned store sees them all.
    [Fact]
    public async Task Workspace_PartitionsTheEdges()
    {
        var alpha = _harness.GraphIn("alpha");
        var beta = _harness.GraphIn("beta");

        Assert.True(await alpha.UpsertAsync(Edge("A", "B", note: "alpha's")));
        Assert.True(await beta.UpsertAsync(Edge("A", "B", note: "beta's")));

        Assert.Equal("alpha's", Assert.Single(await alpha.GetAllAsync()).Note);
        Assert.Equal(2, (await _store.GetAllAsync()).Count);
        Assert.Equal(0, await beta.RemoveAsync("a", "b", LinkTypes.Documents));
    }
}
