using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using Snail.MCP.Memory.Infrastructure.Configuration;

namespace Snail.MCP.Memory.Infrastructure.Embeddings;

/// <summary>In-process <see cref="IEmbedder"/>: a multilingual sentence transformer run through ONNX Runtime.</summary>
/// <remarks>
/// Initialization is lazy and failure is a state: any failure is logged once and the embedder
/// answers <c>null</c> forever after — knowledge is never refused over a failed download.
/// XLM-RoBERTa quirks handled here: SentencePiece ids carry the fairseq offset, and e5 needs
/// <c>query:</c>/<c>passage:</c> prefixes.
/// </remarks>
public sealed class OnnxEmbedder : IEmbedder, IDisposable
{
    private const string QueryPrefix = "query: ";
    private const string PassagePrefix = "passage: ";

    /// <summary>Model input limit is 512; two special tokens are added around the pieces.</summary>
    private const int MaxPieceCount = 510;

    private const long BosId = 0;
    private const long EosId = 2;
    private const long UnknownId = 3;
    private const long FairseqOffset = 1;

    private readonly string _modelPath;
    private readonly string _tokenizerPath;
    private readonly string _modelUrl;
    private readonly string _tokenizerUrl;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private InferenceSession? _session;
    private Tokenizer? _tokenizer;
    private bool _sessionWantsTokenTypes;
    private volatile bool _failed;
    private volatile bool _ready;

    /// <summary>Creates the embedder from server options.</summary>
    public OnnxEmbedder(StorageOptions options, ILogger<OnnxEmbedder>? logger = null)
        : this(options.ResolveEmbeddingModelPath(), options.ResolveEmbeddingTokenizerPath(),
            options.ResolveEmbeddingModelUrl(), options.ResolveEmbeddingTokenizerUrl(),
            options.ResolveEmbeddingDimensions(), logger)
    {
    }

    /// <summary>Creates the embedder from explicit paths — the constructor tests use.</summary>
    public OnnxEmbedder(string modelPath, string tokenizerPath, string modelUrl, string tokenizerUrl,
        int dimensions, ILogger? logger = null)
    {
        _modelPath = modelPath;
        _tokenizerPath = tokenizerPath;
        _modelUrl = modelUrl;
        _tokenizerUrl = tokenizerUrl;
        Dimensions = dimensions;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public int Dimensions { get; }

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<float>?> TryEmbedAsync(string text, EmbeddingPurpose purpose, CancellationToken cancellationToken = default)
    {
        if (_failed)
            return null;

        if (!await EnsureReadyAsync(cancellationToken))
            return null;

        try
        {
            var prefixed = (purpose == EmbeddingPurpose.Query ? QueryPrefix : PassagePrefix) + text;

            return await Task.Run(() => Embed(prefixed), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Fail(exception, "Embedding failed; recall falls back to text matching.");

            return null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _session?.Dispose();
        _initLock.Dispose();
    }

    private async Task<bool> EnsureReadyAsync(CancellationToken cancellationToken)
    {
        if (_ready)
            return true;

        await _initLock.WaitAsync(cancellationToken);

        try
        {
            if (_ready)
                return true;

            if (_failed)
                return false;

            await ModelDownloader.EnsureAsync(_tokenizerUrl, _tokenizerPath, _logger, cancellationToken);
            await ModelDownloader.EnsureAsync(_modelUrl, _modelPath, _logger, cancellationToken);

            await using (var tokenizerStream = File.OpenRead(_tokenizerPath))
                _tokenizer = SentencePieceTokenizer.Create(tokenizerStream,
                    addBeginningOfSentence: false, addEndOfSentence: false);

            _session = new InferenceSession(_modelPath);
            _sessionWantsTokenTypes = _session.InputMetadata.ContainsKey("token_type_ids");

            _ready = true;

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Fail(exception, "Embedding model could not be initialized; recall falls back to text matching.");

            return false;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private float[] Embed(string text)
    {
        var ids = TokenIds(text);
        var length = ids.Length;

        var ones = new long[length];
        Array.Fill(ones, 1L);

        var inputIds = new DenseTensor<long>(ids, new[] { 1, length });
        var attentionMask = new DenseTensor<long>(ones, new[] { 1, length });

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask)
        };

        if (_sessionWantsTokenTypes)
            inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(new long[length], new[] { 1, length })));

        using var results = _session!.Run(inputs);
        var hidden = results[0].AsTensor<float>();

        return Normalize(MeanPool(hidden, length));
    }

    /// <summary>SentencePiece ids shifted into the fairseq vocabulary, wrapped in BOS/EOS.</summary>
    private long[] TokenIds(string text)
    {
        var pieces = _tokenizer!.EncodeToIds(text);
        var count = Math.Min(pieces.Count, MaxPieceCount);

        var ids = new long[count + 2];
        ids[0] = BosId;

        for (var index = 0; index < count; index++)
            ids[index + 1] = pieces[index] == 0 ? UnknownId : pieces[index] + FairseqOffset;

        ids[count + 1] = EosId;

        return ids;
    }

    private float[] MeanPool(Tensor<float> hidden, int length)
    {
        var pooled = new float[Dimensions];

        for (var token = 0; token < length; token++)
            for (var component = 0; component < Dimensions; component++)
                pooled[component] += hidden[0, token, component];

        for (var component = 0; component < Dimensions; component++)
            pooled[component] /= length;

        return pooled;
    }

    private static float[] Normalize(float[] vector)
    {
        var norm = MathF.Sqrt(vector.Sum(component => component * component));

        if (norm > 0)
            for (var component = 0; component < vector.Length; component++)
                vector[component] /= norm;

        return vector;
    }

    private void Fail(Exception exception, string message)
    {
        if (!_failed)
            _logger.LogWarning(exception, "{Message}", message);

        _failed = true;
    }
}
