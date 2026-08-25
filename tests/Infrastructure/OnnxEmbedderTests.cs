using Snail.MCP.Memory.Infrastructure.Configuration;
using Snail.MCP.Memory.Infrastructure.Embeddings;

namespace Snail.MCP.Memory.Tests.Infrastructure;

/// The embedder's failure contract and, when the real model is on disk, its quality.
public sealed class OnnxEmbedderTests
{
    /// <summary>A port nothing listens on: the download must fail fast, not hang.</summary>
    private const string UnreachableUrl = "http://127.0.0.1:1/model.onnx";

    /// Failure is a state, not an exception: a machine that cannot download the model still
    /// stores knowledge, and the second call must not retry the download.
    [Fact]
    public async Task TryEmbed_AnswersNullWhenTheModelCannotBeObtained()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"smonnx-{Guid.NewGuid()}");
        using var embedder = new OnnxEmbedder(
            Path.Combine(missing, "model.onnx"), Path.Combine(missing, "tokenizer.model"),
            UnreachableUrl, UnreachableUrl, StorageOptions.DefaultEmbeddingDimensions);

        Assert.Null(await embedder.TryEmbedAsync("text", EmbeddingPurpose.Passage));
        Assert.Null(await embedder.TryEmbedAsync("text", EmbeddingPurpose.Query));

        if (Directory.Exists(missing)) Directory.Delete(missing, recursive: true);
    }

    /// Runs only when the real model already lives in the default location — a CI machine
    /// without it skips the body rather than downloading 123 MB. Checks the whole pipeline:
    /// tokenizer offsets, pooling, normalization and that similarity actually orders meaning,
    /// across languages.
    [Fact]
    public async Task RealModel_EmbedsMeaningfullyWhenPresent()
    {
        var options = new StorageOptions();

        if (!File.Exists(options.ResolveEmbeddingModelPath()) || !File.Exists(options.ResolveEmbeddingTokenizerPath()))
            return;

        using var embedder = new OnnxEmbedder(options);

        var cat = await embedder.TryEmbedAsync("кошка спит на диване", EmbeddingPurpose.Passage);
        var feline = await embedder.TryEmbedAsync("a cat is sleeping on the couch", EmbeddingPurpose.Query);
        var invoice = await embedder.TryEmbedAsync("квартальный финансовый отчёт компании", EmbeddingPurpose.Passage);

        Assert.NotNull(cat);
        Assert.Equal(options.ResolveEmbeddingDimensions(), cat!.Value.Length);
        Assert.Equal(1.0, Norm(cat.Value), precision: 3);
        Assert.True(Cosine(feline!.Value, cat.Value) > Cosine(feline.Value, invoice!.Value),
            "a cross-language paraphrase scored below an unrelated text — the tokenizer offsets are probably wrong");
    }

    private static double Norm(ReadOnlyMemory<float> vector) =>
        Math.Sqrt(vector.ToArray().Sum(component => (double)component * component));

    private static double Cosine(ReadOnlyMemory<float> a, ReadOnlyMemory<float> b) =>
        a.ToArray().Zip(b.ToArray(), (x, y) => (double)x * y).Sum();
}
