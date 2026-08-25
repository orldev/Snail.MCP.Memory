using CommunityToolkit.VectorData.PgVector;
using CommunityToolkit.VectorData.SqliteVec;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using Snail.MCP.Memory.Application;
using Snail.MCP.Memory.Infrastructure.Configuration;
using Snail.MCP.Memory.Infrastructure.Embeddings;
using Snail.MCP.Memory.Infrastructure.Persistence;
using Snail.MCP.Memory.Infrastructure.Stores;

namespace Snail.MCP.Memory.Host.Extensions;

/// <summary>Storage composition: which provider stands behind the store interfaces is decided here from <see cref="StorageOptions"/>.</summary>
/// <remarks>The stores are provider-neutral, so a provider is nothing but the two registrations in its <c>Add{Provider}Storage</c>.</remarks>
public static class StorageServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the embedder, the stores of the selected provider and the application services.</summary>
        public IServiceCollection AddStorage(StorageOptions options)
        {
            services.AddSingleton(options);
            services.AddSingleton(TimeProvider.System);

            return services
                .AddEmbedder(options)
                .AddApplicationServices(options)
                .AddStores(options)
                .AddProvider(options);
        }

        /// <summary>In-process ONNX, or the null embedder when embeddings are switched off.</summary>
        private IServiceCollection AddEmbedder(StorageOptions options)
        {
            services.AddSingleton<IEmbedder>(sp => options.ResolveEmbeddingEnabled()
                ? new OnnxEmbedder(options, sp.GetRequiredService<ILogger<OnnxEmbedder>>())
                : new NullEmbedder(options.ResolveEmbeddingDimensions()));

            return services;
        }

        /// <summary>The provider-independent services behind the tools.</summary>
        private IServiceCollection AddApplicationServices(StorageOptions options)
        {
            services.AddSingleton(sp => new MemoryService(
                sp.GetRequiredService<IRecordStore>(), sp.GetRequiredService<IProjectStore>(),
                sp.GetRequiredService<IEmbedder>(), sp.GetRequiredService<TimeProvider>(),
                options.ResolveDuplicateThreshold(), options.ResolveSimilarityFloor()));

            services.AddSingleton(sp => new ProjectService(
                sp.GetRequiredService<IProjectStore>(), sp.GetRequiredService<IRecordStore>(),
                sp.GetRequiredService<IGraphStore>(), sp.GetRequiredService<IEmbedder>(),
                sp.GetRequiredService<TimeProvider>(), options.ResolveSimilarityFloor()));

            services.AddSingleton(sp => new GraphService(
                sp.GetRequiredService<IGraphStore>(), sp.GetRequiredService<IProjectStore>(),
                sp.GetRequiredService<TimeProvider>()));

            return services;
        }

        /// <summary>The workspace-partitioned stores over whichever provider is registered.</summary>
        private IServiceCollection AddStores(StorageOptions options)
        {
            services.AddSingleton(sp => new MemoryDb(sp.GetRequiredService<IDbContextFactory<MemoryDbContext>>()));

            services.AddSingleton<IProjectStore>(sp =>
                new EfProjectStore(sp.GetRequiredService<MemoryDb>(), options.ResolveWorkspace()));

            services.AddSingleton<IGraphStore>(sp =>
                new EfGraphStore(sp.GetRequiredService<MemoryDb>(), options.ResolveWorkspace()));

            services.AddSingleton<IRecordStore>(sp => new VectorRecordStore(
                sp.GetRequiredService<VectorStoreCollection<string, RecordEntity>>(),
                options.ResolveEmbeddingDimensions(), options.ResolveWorkspace()));

            return services;
        }

        private IServiceCollection AddProvider(StorageOptions options) => options.Provider switch
        {
            Provider.Sqlite => services.AddSqliteStorage(options),
            Provider.Postgres => services.AddPostgresStorage(options),
            _ => throw new InvalidOperationException(
                $"Unknown storage provider '{options.Provider}'. Allowed: {string.Join(", ", Enum.GetNames<Provider>())}.")
        };

        /// <summary>SQLite with sqlite-vec: records, projects and edges in one database file.</summary>
        /// <remarks>The database directory is created eagerly — neither EF Core nor the vector provider creates a missing parent.</remarks>
        public IServiceCollection AddSqliteStorage(StorageOptions options)
        {
            var connectionString = options.ResolveConnectionString();
            var dataSource = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString).DataSource;

            if (dataSource != ":memory:" &&
                Path.GetDirectoryName(Path.GetFullPath(dataSource)) is { Length: > 0 } directory)
                Directory.CreateDirectory(directory);

            services.AddPooledDbContextFactory<MemoryDbContext>(builder => builder.UseSqlite(connectionString));

            services.AddSingleton<VectorStoreCollection<string, RecordEntity>>(_ =>
                new SqliteCollection<string, RecordEntity>(connectionString, VectorRecordStore.CollectionName,
                    new SqliteCollectionOptions { Definition = RecordEntity.Definition(options.ResolveEmbeddingDimensions()) }));

            return services;
        }

        /// <summary>PostgreSQL with pgvector: the server-side twin of the SQLite setup.</summary>
        public IServiceCollection AddPostgresStorage(StorageOptions options)
        {
            var connectionString = options.ResolveConnectionString();

            services.AddPooledDbContextFactory<MemoryDbContext>(builder => builder.UseNpgsql(connectionString));

            services.AddSingleton<VectorStoreCollection<string, RecordEntity>>(_ =>
                new PostgresCollection<string, RecordEntity>(connectionString, VectorRecordStore.CollectionName,
                    new PostgresCollectionOptions { Definition = RecordEntity.Definition(options.ResolveEmbeddingDimensions()) }));

            return services;
        }
    }
}
