using CommunityToolkit.VectorData.SqliteVec;
using Microsoft.EntityFrameworkCore;
using Snail.MCP.Memory.Infrastructure.Persistence;

namespace Snail.MCP.Memory.Tests.TestDoubles;

/// <summary>
/// One real SQLite database with all three stores around it — the setup every store and
/// application service test needs, built once here instead of drifting between test files.
/// Stores for other workspaces over the same database come from the <c>*In</c> methods, which is
/// how the partition contract is tested.
/// </summary>
public sealed class MemoryHarness : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"smharness-{Guid.NewGuid()}");
    private readonly string _connectionString;
    private readonly int _dimensions;

    public MemoryHarness(int dimensions = 16, string workspace = "")
    {
        _dimensions = dimensions;
        _connectionString = $"Data Source={Path.Combine(_tempDir, "memory.db")}";
        Directory.CreateDirectory(_tempDir);

        var options = new DbContextOptionsBuilder<MemoryDbContext>().UseSqlite(_connectionString).Options;
        Database = new MemoryDb(new Factory(options));
        Records = RecordsIn(workspace);
        Projects = ProjectsIn(workspace);
        Graph = GraphIn(workspace);
        Pending = PendingIn(workspace);
        Embedder = new FakeEmbedder(dimensions: dimensions);
        Time = new FixedTimeProvider(DateTimeOffset.Parse("2026-08-25T12:00:00Z"));
    }

    public MemoryDb Database { get; }

    public IRecordStore Records { get; }

    public IProjectStore Projects { get; }

    public IGraphStore Graph { get; }

    public IPendingLinkStore Pending { get; }

    public FakeEmbedder Embedder { get; }

    public FixedTimeProvider Time { get; }

    /// <summary>A record store over the same database, partitioned to <paramref name="workspace"/>.</summary>
    public VectorRecordStore RecordsIn(string workspace) => new(
        new SqliteCollection<string, RecordEntity>(_connectionString, VectorRecordStore.CollectionName,
            new SqliteCollectionOptions { Definition = RecordEntity.Definition(_dimensions) }),
        _dimensions, workspace);

    /// <summary>A project store over the same database, partitioned to <paramref name="workspace"/>.</summary>
    public EfProjectStore ProjectsIn(string workspace) => new(Database, workspace);

    /// <summary>A graph store over the same database, partitioned to <paramref name="workspace"/>.</summary>
    public EfGraphStore GraphIn(string workspace) => new(Database, workspace);

    /// <summary>A pending-link store over the same database, partitioned to <paramref name="workspace"/>.</summary>
    public EfPendingLinkStore PendingIn(string workspace) => new(Database, workspace);

    public void Dispose()
    {
        Database.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    private sealed class Factory(DbContextOptions<MemoryDbContext> options) : IDbContextFactory<MemoryDbContext>
    {
        public MemoryDbContext CreateDbContext() => new(options);
    }
}

/// <summary>
/// A clock the test moves by hand. The instant is stored as ticks behind <see cref="Interlocked"/>
/// because watcher tests read it from another thread, and a torn read of the
/// <see cref="DateTimeOffset"/> struct would be a flake no one could reproduce.
/// </summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    private long _utcTicks = now.UtcTicks;

    public DateTimeOffset Now
    {
        get => new(Interlocked.Read(ref _utcTicks), TimeSpan.Zero);
        set => Interlocked.Exchange(ref _utcTicks, value.UtcTicks);
    }

    public override DateTimeOffset GetUtcNow() => Now;
}
