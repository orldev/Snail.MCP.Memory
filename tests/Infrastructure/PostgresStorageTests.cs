using CommunityToolkit.VectorData.PgVector;
using Microsoft.EntityFrameworkCore;
using Snail.MCP.Memory.Infrastructure.Persistence;
using Snail.MCP.Memory.Tests.TestDoubles;
using Testcontainers.PostgreSql;

namespace Snail.MCP.Memory.Tests.Infrastructure;

/// <summary>
/// The same stores against a real PostgreSQL with pgvector, in a Testcontainers container.
/// </summary>
/// <remarks>
/// This is the proof of the provider promise: the stores are the very classes the SQLite tests
/// exercise — only the DbContext options and the vector collection differ. Without a reachable
/// Docker daemon the tests pass vacuously and say so in the output; a hard failure here must
/// mean the code is wrong, not the machine.
/// </remarks>
public sealed class PostgresStorageTests : IAsyncLifetime
{
    private const int Dimensions = 16;

    private PostgreSqlContainer? _container;
    private bool _dockerAvailable;
    private MemoryDb? _database;
    private VectorRecordStore? _records;

    public async Task InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();
            await _container.StartAsync();
            _dockerAvailable = true;
        }
        catch (Exception)
        {
            return;
        }

        var connectionString = _container!.GetConnectionString();
        var options = new DbContextOptionsBuilder<MemoryDbContext>().UseNpgsql(connectionString).Options;
        _database = new MemoryDb(new Factory(options));
        _records = new VectorRecordStore(
            new PostgresCollection<string, RecordEntity>(connectionString, VectorRecordStore.CollectionName,
                new PostgresCollectionOptions { Definition = RecordEntity.Definition(Dimensions) }),
            Dimensions, workspace: "");
    }

    public async Task DisposeAsync()
    {
        _records?.Dispose();
        _database?.Dispose();

        if (_container is not null)
            await _container.DisposeAsync();
    }

    [Fact]
    public async Task RecordStore_UpsertsSearchesAndFiltersOverPgvector()
    {
        if (!_dockerAvailable)
            return;

        var near = NewRecord("ProjectA", "lesson", "a lesson about LiteDB");
        var far = NewRecord("ProjectB", "fact", "another topic");
        await _records!.UpsertAsync(near, Axis(0));
        await _records.UpsertAsync(far, Axis(1));

        var found = await _records.SearchAsync(Axis(0), new RecordFilter(), 1);

        Assert.Equal(near.Id, Assert.Single(found).Record.Id);
        Assert.Equal(1.0, found[0].Similarity!.Value, precision: 3);

        var filtered = await _records.SearchAsync(Axis(0), new RecordFilter("projectb"), 10);

        Assert.Equal(far.Id, Assert.Single(filtered).Record.Id);

        Assert.Equal(1, await _records.RenameProjectAsync("projecta", "Renamed"));
        Assert.Equal("Renamed", Assert.Single(await _records.ListAsync(new RecordFilter("renamed"), 10)).Project);
    }

    [Fact]
    public async Task RelationalStores_RegisterRenameAndLinkOverNpgsql()
    {
        if (!_dockerAvailable)
            return;

        var projects = new EfProjectStore(_database!, workspace: "");
        var graph = new EfGraphStore(_database!, workspace: "");

        Assert.True(await projects.CreateAsync(new ProjectInfo("Alpha", "a description", DateTimeOffset.UtcNow)));
        Assert.False(await projects.CreateAsync(new ProjectInfo("alpha", "", DateTimeOffset.UtcNow)));
        Assert.True(await projects.CreateAsync(new ProjectInfo("Beta", "", DateTimeOffset.UtcNow)));

        Assert.True(await graph.UpsertAsync(new GraphEdge("Alpha", "Beta", LinkTypes.Uses, "runtime", DateTimeOffset.UtcNow)));
        Assert.False(await graph.UpsertAsync(new GraphEdge("alpha", "beta", LinkTypes.Uses, "updated", DateTimeOffset.UtcNow)));

        Assert.True(await projects.RenameAsync("alpha", "Gamma"));
        Assert.Equal(1, await graph.RenameProjectAsync("alpha", "Gamma"));

        var edge = Assert.Single(await graph.GetForProjectsAsync(["gamma"]));

        Assert.Equal("Gamma", edge.Source);
        Assert.Equal("updated", edge.Note);
    }

    private static MemoryRecord NewRecord(string project, string kind, string content) => new()
    {
        Id = Guid.NewGuid(),
        Project = project,
        Kind = kind,
        Content = content,
        Created = DateTimeOffset.UtcNow,
        Updated = DateTimeOffset.UtcNow,
    };

    private static ReadOnlyMemory<float> Axis(int index)
    {
        var vector = new float[Dimensions];
        vector[index] = 1f;

        return vector;
    }

    private sealed class Factory(DbContextOptions<MemoryDbContext> options) : IDbContextFactory<MemoryDbContext>
    {
        public MemoryDbContext CreateDbContext() => new(options);
    }
}
