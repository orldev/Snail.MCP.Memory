using Snail.MCP.Memory.Tests.TestDoubles;

namespace Snail.MCP.Memory.Tests.Infrastructure;

public sealed class EfProjectStoreTests : IDisposable
{
    private readonly MemoryHarness _harness = new();
    private readonly IProjectStore _store;

    public EfProjectStoreTests() => _store = _harness.Projects;

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Create_RegistersOnceAndKeepsTheDisplayName()
    {
        Assert.True(await _store.CreateAsync(new ProjectInfo("ProjectA", "a description", DateTimeOffset.UtcNow)));
        Assert.False(await _store.CreateAsync(new ProjectInfo("projecta", "duplicate", DateTimeOffset.UtcNow)));

        var loaded = await _store.GetAsync("projecta");

        Assert.Equal("ProjectA", loaded!.Name);
        Assert.Equal("a description", loaded.Description);
    }

    [Fact]
    public async Task Get_MissesOnUnknownKey() => Assert.Null(await _store.GetAsync("nope"));

    [Fact]
    public async Task List_ReturnsEveryRegisteredProject()
    {
        await _store.CreateAsync(new ProjectInfo("B", "", DateTimeOffset.UtcNow));
        await _store.CreateAsync(new ProjectInfo("A", "", DateTimeOffset.UtcNow));

        Assert.Equal(2, (await _store.ListAsync()).Count);
    }

    [Fact]
    public async Task Rename_MovesTheKeyAndRefusesATakenTarget()
    {
        await _store.CreateAsync(new ProjectInfo("Old", "", DateTimeOffset.UtcNow));
        await _store.CreateAsync(new ProjectInfo("Taken", "", DateTimeOffset.UtcNow));

        Assert.False(await _store.RenameAsync("old", "taken"));
        Assert.True(await _store.RenameAsync("old", "New"));
        Assert.Null(await _store.GetAsync("old"));
        Assert.Equal("New", (await _store.GetAsync("new"))!.Name);
    }

    /// A case-only rename keeps the key; only the display name changes.
    [Fact]
    public async Task Rename_AllowsChangingOnlyTheCase()
    {
        await _store.CreateAsync(new ProjectInfo("myproject", "", DateTimeOffset.UtcNow));

        Assert.True(await _store.RenameAsync("myproject", "MyProject"));
        Assert.Equal("MyProject", (await _store.GetAsync("myproject"))!.Name);
    }

    [Fact]
    public async Task Delete_ReportsWhetherTheProjectExisted()
    {
        await _store.CreateAsync(new ProjectInfo("Gone", "", DateTimeOffset.UtcNow));

        Assert.True(await _store.DeleteAsync("gone"));
        Assert.False(await _store.DeleteAsync("gone"));
    }

    /// Two workspaces may hold a project of the same name; each partitioned store sees its own,
    /// the unpartitioned one sees both.
    [Fact]
    public async Task Workspace_PartitionsTheRegistry()
    {
        var alpha = _harness.ProjectsIn("alpha");
        var beta = _harness.ProjectsIn("beta");

        Assert.True(await alpha.CreateAsync(new ProjectInfo("Shared", "alpha's", DateTimeOffset.UtcNow)));
        Assert.True(await beta.CreateAsync(new ProjectInfo("Shared", "beta's", DateTimeOffset.UtcNow)));

        Assert.Equal("alpha's", (await alpha.GetAsync("shared"))!.Description);
        Assert.Single(await alpha.ListAsync());
        Assert.Equal(2, (await _store.ListAsync()).Count);
    }
}
