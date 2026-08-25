namespace Snail.MCP.Memory.Tests.TestDoubles;

/// <summary>
/// Deterministic embedder for tests: identical texts get identical vectors, distinct texts get
/// (near-)orthogonal ones, and a <c>null</c> factory result simulates an unavailable model.
/// </summary>
public sealed class FakeEmbedder(Func<string, float[]?>? factory = null, int dimensions = 16) : IEmbedder
{
    private readonly Dictionary<string, int> _axes = [];

    /// <summary>An embedder that always answers "unavailable".</summary>
    public static FakeEmbedder Unavailable => new(_ => null);

    public int Dimensions => dimensions;

    /// <summary>Texts the embedder was asked to embed, prefixes included.</summary>
    public List<(string Text, EmbeddingPurpose Purpose)> Calls { get; } = [];

    public ValueTask<ReadOnlyMemory<float>?> TryEmbedAsync(string text, EmbeddingPurpose purpose, CancellationToken cancellationToken = default)
    {
        Calls.Add((text, purpose));

        var vector = factory is null ? AxisVector(text) : factory(text);

        return ValueTask.FromResult(AsNullable(vector));
    }

    /// <summary>
    /// Explicit branches instead of a conditional expression: <c>cond ? null : floatArray</c> has the
    /// natural type <c>float[]</c>, whose user-defined conversion turns the null branch into an
    /// <em>empty</em> memory rather than a null nullable.
    /// </summary>
    private static ReadOnlyMemory<float>? AsNullable(float[]? vector)
    {
        if (vector is null)
            return null;

        return new ReadOnlyMemory<float>(vector);
    }

    /// <summary>
    /// A unit vector on one axis: the same text always gets the same axis, each new distinct text
    /// the next free one — assignment instead of hashing, so a collision can never fake similarity.
    /// </summary>
    private float[] AxisVector(string text)
    {
        lock (_axes)
        {
            if (!_axes.TryGetValue(text, out var axis))
            {
                axis = _axes.Count % dimensions;
                _axes[text] = axis;
            }

            var vector = new float[dimensions];
            vector[axis] = 1f;

            return vector;
        }
    }
}
