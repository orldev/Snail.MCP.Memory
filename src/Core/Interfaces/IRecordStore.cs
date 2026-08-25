namespace Snail.MCP.Memory.Core.Interfaces;

/// <summary>Persistence of memory records with vector and text search.</summary>
/// <remarks>A record and its embedding are written together or not at all; filters take pre-normalized values.</remarks>
public interface IRecordStore
{
    /// <summary>Inserts or replaces a record; <paramref name="embedding"/> is <c>null</c> when no embedder is available.</summary>
    Task UpsertAsync(MemoryRecord record, ReadOnlyMemory<float>? embedding, CancellationToken cancellationToken = default);

    /// <summary>The record with this id, or <c>null</c>.</summary>
    Task<MemoryRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deletes the given records; returns how many existed.</summary>
    Task<int> DeleteAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>Deletes every record of a project; returns how many there were.</summary>
    Task<int> DeleteProjectAsync(string projectKey, CancellationToken cancellationToken = default);

    /// <summary>Newest records first (by <see cref="MemoryRecord.Updated"/>), narrowed by the filter.</summary>
    Task<IReadOnlyList<MemoryRecord>> ListAsync(RecordFilter filter, int limit, CancellationToken cancellationToken = default);

    /// <summary>Nearest records by cosine similarity, narrowed by the filter.</summary>
    Task<IReadOnlyList<ScoredRecord>> SearchAsync(ReadOnlyMemory<float> vector, RecordFilter filter, int limit, CancellationToken cancellationToken = default);

    /// <summary>Substring fallback over title and content, newest first — used when no embedder is available.</summary>
    Task<IReadOnlyList<MemoryRecord>> SearchTextAsync(string query, RecordFilter filter, int limit, CancellationToken cancellationToken = default);

    /// <summary>Bumps the access statistics of records recall has just returned.</summary>
    Task MarkAccessedAsync(IReadOnlyCollection<Guid> ids, DateTimeOffset when, CancellationToken cancellationToken = default);

    /// <summary>Moves every record of a project under a new name; returns how many moved.</summary>
    Task<int> RenameProjectAsync(string oldKey, string newName, CancellationToken cancellationToken = default);

    /// <summary>Purges ephemeral records older than the cutoff; returns how many were purged.</summary>
    Task<int> PurgeEventsBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default);
}
