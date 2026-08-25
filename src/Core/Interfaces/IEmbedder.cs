namespace Snail.MCP.Memory.Core.Interfaces;

/// <summary>What a text is embedded for; asymmetric models prefix the two differently.</summary>
public enum EmbeddingPurpose
{
    /// <summary>A search query.</summary>
    Query,

    /// <summary>A stored passage.</summary>
    Passage
}

/// <summary>Turns text into a normalized embedding vector — or admits it cannot.</summary>
/// <remarks>The <c>null</c> result is contract, not error: memory keeps working on a machine without a model.</remarks>
public interface IEmbedder
{
    /// <summary>Vector length this embedder produces.</summary>
    int Dimensions { get; }

    /// <summary>The embedding of <paramref name="text"/>, or <c>null</c> when embedding is unavailable.</summary>
    ValueTask<ReadOnlyMemory<float>?> TryEmbedAsync(string text, EmbeddingPurpose purpose, CancellationToken cancellationToken = default);
}
