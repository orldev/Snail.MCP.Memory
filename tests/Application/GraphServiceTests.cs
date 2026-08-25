using Snail.MCP.Memory.Tests.TestDoubles;

namespace Snail.MCP.Memory.Tests.Application;

/// Linking rules and graph rendering over a real database.
public sealed class GraphServiceTests : IDisposable
{
    private readonly MemoryHarness _harness = new();
    private readonly GraphService _service;

    public GraphServiceTests()
    {
        _service = new GraphService(_harness.Graph, _harness.Projects, _harness.Time);
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
