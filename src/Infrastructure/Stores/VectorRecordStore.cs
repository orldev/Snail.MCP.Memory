using Microsoft.Extensions.VectorData;
using System.Linq.Expressions;

namespace Snail.MCP.Memory.Infrastructure.Stores;

/// <summary><see cref="IRecordStore"/> over any <c>Microsoft.Extensions.VectorData</c> collection — sqlite-vec locally, pgvector on a server.</summary>
/// <remarks>
/// Filters are a closed set of cases because providers translate only plain member comparisons.
/// The text fallback matches in memory: SQL <c>LIKE</c> is case-sensitive for non-ASCII.
/// Reads honor the workspace partition (empty — the whole base), writes always tag it.
/// </remarks>
public sealed class VectorRecordStore(
    VectorStoreCollection<string, RecordEntity> collection,
    int dimensions,
    string workspace) : IRecordStore, IDisposable
{
    /// <summary>Collection (table) name of the records.</summary>
    public const string CollectionName = "memories";

    /// <summary>How many candidates the in-memory text fallback scans.</summary>
    internal const int TextScanLimit = 1000;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _initialized;

    /// <inheritdoc />
    public async Task UpsertAsync(MemoryRecord record, ReadOnlyMemory<float>? embedding, CancellationToken cancellationToken = default)
    {
        await EnsureCreatedAsync(cancellationToken);
        await collection.UpsertAsync(RecordEntity.From(record, embedding, dimensions, workspace), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MemoryRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureCreatedAsync(cancellationToken);

        var entity = await collection.GetAsync(id.ToString("D"), cancellationToken: cancellationToken);

        return entity is null || !InScope(entity) ? null : entity.ToRecord();
    }

    /// <inheritdoc />
    public async Task<int> DeleteAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        await EnsureCreatedAsync(cancellationToken);

        var existing = 0;

        foreach (var id in ids)
        {
            var key = id.ToString("D");
            var entity = await collection.GetAsync(key, cancellationToken: cancellationToken);

            if (entity is null || !InScope(entity))
                continue;

            await collection.DeleteAsync(key, cancellationToken);
            existing++;
        }

        return existing;
    }

    /// <inheritdoc />
    public async Task<int> DeleteProjectAsync(string projectKey, CancellationToken cancellationToken = default)
    {
        await EnsureCreatedAsync(cancellationToken);

        var keys = new List<string>();

        await foreach (var entity in collection.GetAsync(
                           ListFilter(new RecordFilter(projectKey)), int.MaxValue, cancellationToken: cancellationToken))
            keys.Add(entity.Id);

        if (keys.Count > 0)
            await collection.DeleteAsync(keys, cancellationToken);

        return keys.Count;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MemoryRecord>> ListAsync(RecordFilter filter, int limit, CancellationToken cancellationToken = default)
    {
        await EnsureCreatedAsync(cancellationToken);

        var records = new List<MemoryRecord>();

        await foreach (var entity in collection.GetAsync(ListFilter(filter), limit,
                           new FilteredRecordRetrievalOptions<RecordEntity> { OrderBy = o => o.Descending(r => r.Updated) },
                           cancellationToken))
            records.Add(entity.ToRecord());

        return records;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScoredRecord>> SearchAsync(ReadOnlyMemory<float> vector, RecordFilter filter, int limit, CancellationToken cancellationToken = default)
    {
        await EnsureCreatedAsync(cancellationToken);

        var results = new List<ScoredRecord>();

        await foreach (var result in collection.SearchAsync(vector, limit,
                           new VectorSearchOptions<RecordEntity> { Filter = SearchFilter(filter) }, cancellationToken))
            results.Add(new ScoredRecord(result.Record.ToRecord(), Similarity(result.Score)));

        return results;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MemoryRecord>> SearchTextAsync(string query, RecordFilter filter, int limit, CancellationToken cancellationToken = default)
    {
        var needle = query.Trim();
        var candidates = await ListAsync(filter, TextScanLimit, cancellationToken);

        return [.. candidates
            .Where(record =>
                record.Content.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                record.Title.Contains(needle, StringComparison.OrdinalIgnoreCase))
            .Take(limit)];
    }

    /// <inheritdoc />
    public async Task MarkAccessedAsync(IReadOnlyCollection<Guid> ids, DateTimeOffset when, CancellationToken cancellationToken = default)
    {
        await EnsureCreatedAsync(cancellationToken);

        var entities = new List<RecordEntity>();

        await foreach (var entity in collection.GetAsync(ids.Select(id => id.ToString("D")),
                           new RecordRetrievalOptions { IncludeVectors = true }, cancellationToken))
        {
            entity.LastAccessed = when.ToUnixTimeSeconds();
            entity.AccessCount++;
            entities.Add(entity);
        }

        if (entities.Count > 0)
            await collection.UpsertAsync(entities, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> RenameProjectAsync(string oldKey, string newName, CancellationToken cancellationToken = default)
    {
        await EnsureCreatedAsync(cancellationToken);

        var newKey = ProjectIdentity.Key(newName);
        var moved = new List<RecordEntity>();

        await foreach (var entity in collection.GetAsync(ListFilter(new RecordFilter(oldKey)), int.MaxValue,
                           new FilteredRecordRetrievalOptions<RecordEntity> { IncludeVectors = true }, cancellationToken))
        {
            entity.ProjectKey = newKey;
            entity.Project = ProjectIdentity.Normalize(newName);
            moved.Add(entity);
        }

        if (moved.Count > 0)
            await collection.UpsertAsync(moved, cancellationToken);

        return moved.Count;
    }

    /// <inheritdoc />
    public async Task<int> PurgeEventsBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
    {
        await EnsureCreatedAsync(cancellationToken);

        var cutoffSeconds = cutoff.ToUnixTimeSeconds();
        var keys = new List<string>();

        await foreach (var entity in collection.GetAsync(ListFilter(new RecordFilter(Kind: RecordKinds.Event)),
                           int.MaxValue, cancellationToken: cancellationToken))
            if (entity.Updated < cutoffSeconds)
                keys.Add(entity.Id);

        if (keys.Count > 0)
            await collection.DeleteAsync(keys, cancellationToken);

        return keys.Count;
    }

    /// <inheritdoc />
    public void Dispose() => _initLock.Dispose();

    private async Task EnsureCreatedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
            return;

        await _initLock.WaitAsync(cancellationToken);

        try
        {
            if (_initialized)
                return;

            await collection.EnsureCollectionExistsAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private bool InScope(RecordEntity entity) => workspace.Length == 0 || entity.Workspace == workspace;

    /// <summary>The closed filter cases; the no-filter case still needs a translatable expression, hence <c>Updated >= 0</c>.</summary>
    private Expression<Func<RecordEntity, bool>> ListFilter(RecordFilter filter)
    {
        var (project, kind) = (filter.ProjectKey, filter.Kind);

        return (workspace.Length > 0, filter.HasProject, filter.HasKind) switch
        {
            (true, true, true) => r => r.Workspace == workspace && r.ProjectKey == project && r.Kind == kind,
            (true, true, false) => r => r.Workspace == workspace && r.ProjectKey == project,
            (true, false, true) => r => r.Workspace == workspace && r.Kind == kind,
            (true, false, false) => r => r.Workspace == workspace,
            (false, true, true) => r => r.ProjectKey == project && r.Kind == kind,
            (false, true, false) => r => r.ProjectKey == project,
            (false, false, true) => r => r.Kind == kind,
            (false, false, false) => r => r.Updated >= 0
        };
    }

    /// <summary>The same cases with the zero-vector guard vector search always needs.</summary>
    private Expression<Func<RecordEntity, bool>> SearchFilter(RecordFilter filter)
    {
        var (project, kind) = (filter.ProjectKey, filter.Kind);

        return (workspace.Length > 0, filter.HasProject, filter.HasKind) switch
        {
            (true, true, true) => r => r.HasEmbedding == 1 && r.Workspace == workspace && r.ProjectKey == project && r.Kind == kind,
            (true, true, false) => r => r.HasEmbedding == 1 && r.Workspace == workspace && r.ProjectKey == project,
            (true, false, true) => r => r.HasEmbedding == 1 && r.Workspace == workspace && r.Kind == kind,
            (true, false, false) => r => r.HasEmbedding == 1 && r.Workspace == workspace,
            (false, true, true) => r => r.HasEmbedding == 1 && r.ProjectKey == project && r.Kind == kind,
            (false, true, false) => r => r.HasEmbedding == 1 && r.ProjectKey == project,
            (false, false, true) => r => r.HasEmbedding == 1 && r.Kind == kind,
            (false, false, false) => r => r.HasEmbedding == 1
        };
    }

    private static double? Similarity(double? cosineDistance) =>
        cosineDistance is { } distance ? Math.Clamp(1 - distance, 0, 1) : null;
}
