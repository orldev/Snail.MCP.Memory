using Microsoft.Extensions.DependencyInjection;
using Snail.MCP.Memory.Host.Extensions;
using Snail.MCP.Memory.Infrastructure.Configuration;
using Snail.MCP.Memory.Infrastructure.Embeddings;
using Snail.MCP.Memory.Tests.TestDoubles;
using Snail.Sdk.Mcp.Hosting;

namespace Snail.MCP.Memory.Tests.Host;

/// Provider and embedder selection is the one place where options meet the service graph;
/// the defaults and the switches are each pinned here.
public sealed class StorageRegistrationTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(TestFolder.Create(), "data");

    public void Dispose()
    {
        SqlitePools.Release(new StorageOptions { Directory = _tempDir }.ResolveConnectionString());
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

    /// A provider bound from a number no provider has was refused with a bare InvalidOperationException while the
    /// services were registered, outside the refusal of the start, and the program crashed with a trace.
    [Fact]
    public void UnknownProvider_IsRefusedNamingTheVariable()
    {
        var refusal = Assert.Throws<StartRefusedException>(() => Build(o => o.Provider = (Provider)5));

        Assert.StartsWith("SNAIL_MCP_MEMORY_PROVIDER (or Provider in the settings file) is '5'", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("Sqlite, Postgres", refusal.Message, StringComparison.Ordinal);
    }

    /// The same provider as the program meets it: one line naming the variable and exit code 1, not a trace and 134.
    [Fact]
    public async Task Program_UnknownProvider_ExitsNamingTheVariable()
    {
        var environment = new Dictionary<string, string?> { ["SNAIL_MCP_MEMORY_PROVIDER"] = "5" };

        var exited = await Assert.ThrowsAsync<ServerExitedException>(() => ServerProgram.StartAsync("Snail.MCP.Memory", "SNAIL_MEMORY_SERVER", environment));

        Assert.Equal(1, exited.ExitCode);
        Assert.StartsWith("SNAIL_MCP_MEMORY_PROVIDER (or Provider in the settings file) is '5'", exited.Stderr.Trim(), StringComparison.Ordinal);
        Assert.Single(exited.Stderr.Trim().Split('\n'));
        Assert.DoesNotContain("   at ", exited.Stderr, StringComparison.Ordinal);
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

        Assert.NotNull(sp.GetRequiredService<RecordLifecycle>());
        Assert.NotNull(sp.GetRequiredService<ProjectRegistry>());
        Assert.NotNull(sp.GetRequiredService<KnowledgeGraph>());
    }
}
