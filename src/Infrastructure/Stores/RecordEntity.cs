using Microsoft.Extensions.VectorData;

namespace Snail.MCP.Memory.Infrastructure.Stores;

/// <summary>Storage shape of a <see cref="MemoryRecord"/> in the vector collection.</summary>
/// <remarks>
/// A record without an embedder stores a zero vector, and cosine distance to it is undefined —
/// vector search always filters to <c>HasEmbedding == 1</c>. The schema is defined at runtime
/// (<see cref="Definition"/>) so the vector length can come from configuration.
/// </remarks>
public sealed class RecordEntity
{
    /// <summary>Record id, <see cref="Guid"/> in its canonical string form.</summary>
    public string Id { get; set; } = "";

    /// <summary>Partition tag; empty for the unpartitioned base.</summary>
    public string Workspace { get; set; } = "";

    /// <summary>Case-folded project key the filters match on.</summary>
    public string ProjectKey { get; set; } = "";

    /// <summary>Display name of the project.</summary>
    public string Project { get; set; } = "";

    /// <summary>Normalized kind tag.</summary>
    public string Kind { get; set; } = "";

    /// <summary>Short label; may be empty.</summary>
    public string Title { get; set; } = "";

    /// <summary>The knowledge itself.</summary>
    public string Content { get; set; } = "";

    /// <summary>Unix seconds of the first write.</summary>
    public long Created { get; set; }

    /// <summary>Unix seconds of the last write.</summary>
    public long Updated { get; set; }

    /// <summary>Unix seconds of the last recall hit; 0 — never.</summary>
    public long LastAccessed { get; set; }

    /// <summary>How many times recall returned the record.</summary>
    public long AccessCount { get; set; }

    /// <summary>1 when <see cref="Embedding"/> is a real vector, 0 when it is the zero placeholder.</summary>
    public long HasEmbedding { get; set; }

    /// <summary>The embedding, or a zero vector when none was available.</summary>
    public ReadOnlyMemory<float> Embedding { get; set; }

    /// <summary>The runtime schema of the collection for the given vector length.</summary>
    public static VectorStoreCollectionDefinition Definition(int dimensions) => new()
    {
        Properties =
        [
            new VectorStoreKeyProperty(nameof(Id), typeof(string)),
            new VectorStoreDataProperty(nameof(Workspace), typeof(string)) { IsIndexed = true },
            new VectorStoreDataProperty(nameof(ProjectKey), typeof(string)) { IsIndexed = true },
            new VectorStoreDataProperty(nameof(Project), typeof(string)),
            new VectorStoreDataProperty(nameof(Kind), typeof(string)) { IsIndexed = true },
            new VectorStoreDataProperty(nameof(Title), typeof(string)),
            new VectorStoreDataProperty(nameof(Content), typeof(string)),
            new VectorStoreDataProperty(nameof(Created), typeof(long)),
            new VectorStoreDataProperty(nameof(Updated), typeof(long)) { IsIndexed = true },
            new VectorStoreDataProperty(nameof(LastAccessed), typeof(long)),
            new VectorStoreDataProperty(nameof(AccessCount), typeof(long)),
            new VectorStoreDataProperty(nameof(HasEmbedding), typeof(long)) { IsIndexed = true },
            new VectorStoreVectorProperty(nameof(Embedding), typeof(ReadOnlyMemory<float>), dimensions)
            {
                DistanceFunction = DistanceFunction.CosineDistance
            }
        ]
    };

    /// <summary>Maps a domain record and its embedding onto the storage shape.</summary>
    public static RecordEntity From(MemoryRecord record, ReadOnlyMemory<float>? embedding, int dimensions, string workspace) => new()
    {
        Id = record.Id.ToString("D"),
        Workspace = workspace,
        ProjectKey = ProjectIdentity.Key(record.Project),
        Project = record.Project,
        Kind = record.Kind,
        Title = record.Title,
        Content = record.Content,
        Created = record.Created.ToUnixTimeSeconds(),
        Updated = record.Updated.ToUnixTimeSeconds(),
        LastAccessed = record.LastAccessed?.ToUnixTimeSeconds() ?? 0,
        AccessCount = record.AccessCount,
        HasEmbedding = embedding is null ? 0 : 1,
        Embedding = embedding ?? new float[dimensions]
    };

    /// <summary>Maps the storage shape back onto the domain record.</summary>
    public MemoryRecord ToRecord() => new()
    {
        Id = Guid.Parse(Id),
        Project = Project,
        Kind = Kind,
        Title = Title,
        Content = Content,
        Created = DateTimeOffset.FromUnixTimeSeconds(Created),
        Updated = DateTimeOffset.FromUnixTimeSeconds(Updated),
        LastAccessed = LastAccessed > 0 ? DateTimeOffset.FromUnixTimeSeconds(LastAccessed) : null,
        AccessCount = AccessCount
    };
}
