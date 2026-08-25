namespace Snail.MCP.Memory.Core.Records;

/// <summary>One remembered piece of knowledge: the unit of storage of the whole memory.</summary>
/// <remarks>Access statistics feed <c>review_memory</c> and are updated only by the store.</remarks>
public sealed record MemoryRecord
{
    /// <summary>Stable identity; shown to the model so a record can be replaced or forgotten.</summary>
    public required Guid Id { get; init; }

    /// <summary>Display name of the project the record belongs to.</summary>
    public required string Project { get; init; }

    /// <summary>Normalized kind tag — see <see cref="RecordKinds"/>.</summary>
    public required string Kind { get; init; }

    /// <summary>Short label of the record; may be empty.</summary>
    public string Title { get; init; } = "";

    /// <summary>The knowledge itself, in the language of the conversation it was captured from.</summary>
    public required string Content { get; init; }

    /// <summary>When the record was first written.</summary>
    public DateTimeOffset Created { get; init; }

    /// <summary>When the record was last written (creation or supersession).</summary>
    public DateTimeOffset Updated { get; init; }

    /// <summary>When the record last appeared in a recall result; <c>null</c> — never.</summary>
    public DateTimeOffset? LastAccessed { get; init; }

    /// <summary>How many times the record has appeared in recall results.</summary>
    public long AccessCount { get; init; }
}
