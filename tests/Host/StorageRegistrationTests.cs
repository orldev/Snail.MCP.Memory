using Microsoft.Extensions.DependencyInjection;
using Snail.MCP.Memory.Application;
using Snail.MCP.Memory.Host.Extensions;
using Snail.MCP.Memory.Infrastructure.Configuration;
using Snail.MCP.Memory.Infrastructure.Embeddings;

namespace Snail.MCP.Memory.Tests.Host;

/// Provider and embedder selection is the one place where options meet the service graph;
/// the defaults and the switches are each pinned here.
public sealed class StorageRegistrationTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"smdi-{Guid.NewGuid()}");

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    private ServiceProvider Build(Action<StorageOptions>? configure = null)
    {
        var options = new StorageOptions { Directory = _tempDir };
        configure?.Invoke(options);

        return new ServiceCollection()
            .AddLogging()
            .AddStorage(options)
            .BuildServiceProvider();
    }

    [Fact]
    public void Default_IsSqliteProvider()
    {
        using var sp = Build();

        Assert.IsType<VectorRecordStore>(sp.GetRequiredService<IRecordStore>());
        Assert.IsType<EfProjectStore>(sp.GetRequiredService<IProjectStore>());
        Assert.IsType<EfGraphStore>(sp.GetRequiredService<IGraphStore>());
        Assert.Equal(Provider.Sqlite, sp.GetRequiredService<StorageOptions>().Provider);
    }

    /// The Postgres provider registers the same store types — that sameness is the whole point
    /// of the EF/VectorData abstraction; only the collections and the DbContext options differ.
    [Fact]
    public void Postgres_RegistersTheSameStoreShapes()
    {
        using var sp = Build(o =>
        {
            o.Provider = Provider.Postgres;
            o.ConnectionString = "Host=localhost;Database=memory;Username=x;Password=x";
        });

        Assert.IsType<VectorRecordStore>(sp.GetRequiredService<IRecordStore>());
        Assert.IsType<EfProjectStore>(sp.GetRequiredService<IProjectStore>());
        Assert.IsType<EfGraphStore>(sp.GetRequiredService<IGraphStore>());
    }

    [Fact]
    public void Default_EmbedderIsOnnx()
    {
        using var sp = Build();

        Assert.IsType<OnnxEmbedder>(sp.GetRequiredService<IEmbedder>());
    }

    /// Switching embeddings off must not remove the embedder from the graph — the services
    /// always talk to one; it merely answers "unavailable".
    [Fact]
    public void EmbeddingDisabled_RegistersTheNullEmbedder()
    {
        using var sp = Build(o => o.EmbeddingEnabled = false);

        Assert.IsType<NullEmbedder>(sp.GetRequiredService<IEmbedder>());
        Assert.Equal(StorageOptions.DefaultEmbeddingDimensions, sp.GetRequiredService<IEmbedder>().Dimensions);
    }

    [Fact]
    public void ApplicationServices_AreResolvable()
    {
        using var sp = Build();

        Assert.NotNull(sp.GetRequiredService<MemoryService>());
        Assert.NotNull(sp.GetRequiredService<ProjectService>());
        Assert.NotNull(sp.GetRequiredService<GraphService>());
    }
}
