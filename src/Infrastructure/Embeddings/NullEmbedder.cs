namespace Snail.MCP.Memory.Infrastructure.Embeddings;

/// <summary>The embedder used when embeddings are switched off: always answers "unavailable".</summary>
/// <remarks>Keeps the rest of the application from ever branching on configuration.</remarks>
public sealed class NullEmbedder(int dimensions) : IEmbedder
{
    /// <inheritdoc />
    public int Dimensions => dimensions;

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<float>?> TryEmbedAsync(string text, EmbeddingPurpose purpose, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<ReadOnlyMemory<float>?>(null);
}
