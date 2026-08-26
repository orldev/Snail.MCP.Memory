using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Snail.MCP.Memory.Infrastructure.Persistence;
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
        Assert.True(await _harness.Pending.ProposeAsync(Suggestion()));
        Assert.False(await _harness.Pending.ProposeAsync(Suggestion()));
        Assert.False(await _harness.Pending.ProposeAsync(Suggestion(project: "b", other: "a")));

        var open = await _harness.Pending.ListAsync("a");

        Assert.Single(open);
        Assert.Equal("seen in a record", open[0].Evidence);
    }

    [Fact]
    public async Task List_SeesThePairFromEitherEndAndEverythingWithoutAKey()
    {
        await _harness.Pending.ProposeAsync(Suggestion());
        await _harness.Pending.ProposeAsync(Suggestion(project: "C", other: "D"));

        Assert.Single(await _harness.Pending.ListAsync("b"));
        Assert.Single(await _harness.Pending.ListAsync("a"));
        Assert.Empty(await _harness.Pending.ListAsync("ghost"));
        Assert.Equal(2, (await _harness.Pending.ListAsync()).Count);
    }

    [Fact]
    public async Task Resolve_RemovesThePairInEitherOrder()
    {
        await _harness.Pending.ProposeAsync(Suggestion());

        Assert.Equal(1, await _harness.Pending.ResolveAsync("b", "a"));
        Assert.Empty(await _harness.Pending.ListAsync());
        Assert.True(await _harness.Pending.ProposeAsync(Suggestion()));
    }

    /// A dismissed pair leaves the listing but keeps blocking re-proposal — that is the whole
    /// point of keeping the row.
    [Fact]
    public async Task Dismiss_HidesThePairAndBlocksReproposal()
    {
        await _harness.Pending.ProposeAsync(Suggestion());
        await _harness.Pending.DismissAsync("B", "A", _harness.Time.GetUtcNow());

        Assert.Empty(await _harness.Pending.ListAsync());
        Assert.False(await _harness.Pending.ProposeAsync(Suggestion()));
    }

    [Fact]
    public async Task Dismiss_OfANeverProposedPairStillBlocksFutureProposals()
    {
        await _harness.Pending.DismissAsync("A", "B", _harness.Time.GetUtcNow());

        Assert.False(await _harness.Pending.ProposeAsync(Suggestion()));
    }

    [Fact]
    public async Task Rename_RewritesBothEndsOfTheSuggestions()
    {
        await _harness.Pending.ProposeAsync(Suggestion());
        await _harness.Pending.ProposeAsync(Suggestion(project: "C", other: "A"));

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

        await left.ProposeAsync(Suggestion());

        Assert.Single(await left.ListAsync());
        Assert.Empty(await right.ListAsync());
        Assert.True(await right.ProposeAsync(Suggestion()));
        Assert.Equal(2, (await _harness.PendingIn("").ListAsync()).Count);
    }

    /// A database created before pending_links existed makes EF's wholesale table creation
    /// throw; the upgrade DDL must add just the missing table.
    [Fact]
    public async Task Initialization_AddsThePendingTableToAPreExistingDatabase()
    {
        var tempDir = System.IO.Directory.CreateTempSubdirectory("smupgrade");

        try
        {
            var connectionString = $"Data Source={Path.Combine(tempDir.FullName, "memory.db")}";

            await using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    CREATE TABLE projects ("Workspace" TEXT NOT NULL, "Key" TEXT NOT NULL, "Name" TEXT NOT NULL,
                        "Description" TEXT NOT NULL, "Created" BIGINT NOT NULL, PRIMARY KEY ("Workspace", "Key"));
                    CREATE TABLE edges ("Workspace" TEXT NOT NULL, "SourceKey" TEXT NOT NULL, "TargetKey" TEXT NOT NULL,
                        "Type" TEXT NOT NULL, "Source" TEXT NOT NULL, "Target" TEXT NOT NULL, "Note" TEXT NOT NULL,
                        "Updated" BIGINT NOT NULL, PRIMARY KEY ("Workspace", "SourceKey", "TargetKey", "Type"));
                    """;
                await command.ExecuteNonQueryAsync();
            }

            var options = new DbContextOptionsBuilder<MemoryDbContext>().UseSqlite(connectionString).Options;
            using var database = new MemoryDb(new UpgradeFactory(options));
            var store = new EfPendingLinkStore(database, "");

            Assert.True(await store.ProposeAsync(Suggestion()));
            Assert.Single(await store.ListAsync());
            Assert.True(await new EfAuditStore(database, "").TryClaimAsync(TimeSpan.FromDays(7), _harness.Time.GetUtcNow()));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            tempDir.Delete(recursive: true);
        }
    }

    private sealed class UpgradeFactory(DbContextOptions<MemoryDbContext> options) : IDbContextFactory<MemoryDbContext>
    {
        public MemoryDbContext CreateDbContext() => new(options);
    }
}
