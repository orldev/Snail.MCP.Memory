using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Snail.MCP.Memory.Infrastructure.Configuration;

namespace Snail.MCP.Memory.Infrastructure.Embeddings;

/// <summary>In-process <see cref="IEmbedder"/>: a multilingual sentence transformer run through ONNX Runtime.</summary>
/// <remarks>
/// <para>
/// The files are downloaded once, before the first embedding. The model is loaded for the first embedding and released
/// after an idle period without one, then loaded again for the next — see <see cref="OnDemandModel{TModel}"/>.
/// </para>
/// <para>
/// Failure is a state: any failure is logged once and the embedder answers <c>null</c> forever after — knowledge is
/// never refused over a failed download. e5 needs <c>query:</c>/<c>passage:</c> prefixes.
/// </para>
/// </remarks>
public sealed class OnnxEmbedder : IEmbedder, IDisposable
{
    private const string QueryPrefix = "query: ";
    private const string PassagePrefix = "passage: ";

    private readonly string _modelPath;
    private readonly string _tokenizerPath;
    private readonly string _modelUrl;
    private readonly string _tokenizerUrl;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _downloadLock = new(1, 1);
    private readonly OnDemandModel<OnnxModel> _model;

    private volatile bool _failed;
    private volatile bool _hasFiles;

    /// <summary>Creates the embedder over the given files, releasing the model after <paramref name="idlePeriod"/> without use.</summary>
    /// <param name="modelPath">Where the ONNX model lives, or is downloaded to.</param>
    /// <param name="tokenizerPath">Where the SentencePiece tokenizer lives, or is downloaded to.</param>
    /// <param name="modelUrl">Where the model is downloaded from when it is missing.</param>
    /// <param name="tokenizerUrl">Where the tokenizer is downloaded from when it is missing.</param>
    /// <param name="dimensions">The length of the vectors the model answers.</param>
    /// <param name="idlePeriod">How long the model may go unused before it is released; zero keeps it loaded for good.</param>
    /// <param name="time">The clock the idle period is measured by.</param>
    /// <param name="logger">Where the one failure, the loads and the releases are told; nowhere when omitted.</param>
    public OnnxEmbedder(string modelPath, string tokenizerPath, string modelUrl, string tokenizerUrl,
        int dimensions, TimeSpan idlePeriod, TimeProvider time, ILogger? logger = null)
    {
        _modelPath = modelPath;
        _tokenizerPath = tokenizerPath;
        _modelUrl = modelUrl;
        _tokenizerUrl = tokenizerUrl;
        _logger = logger ?? NullLogger.Instance;
        _model = new OnDemandModel<OnnxModel>(LoadAsync, idlePeriod, time, _logger);
        Dimensions = dimensions;
    }

    /// <summary>Creates the embedder from server options, releasing the model after their idle period on the system clock.</summary>
    public OnnxEmbedder(StorageOptions options, ILogger<OnnxEmbedder>? logger = null, TimeProvider? time = null)
        : this(options.ResolveEmbeddingModelPath(), options.ResolveEmbeddingTokenizerPath(),
            options.ResolveEmbeddingModelUrl(), options.ResolveEmbeddingTokenizerUrl(),
            options.ResolveEmbeddingDimensions(), options.ResolveEmbeddingIdlePeriod(), time ?? TimeProvider.System, logger)
    {
    }

    /// <inheritdoc />
    public int Dimensions { get; }

    /// <summary>Whether the model is in memory now.</summary>
    public bool IsLoaded => _model.IsLoaded;

    /// <summary>When the last embedding ended; <c>null</c> before the first one.</summary>
    public DateTimeOffset? LastUsed => _model.LastUsed;

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<float>?> TryEmbedAsync(string text, EmbeddingPurpose purpose, CancellationToken cancellationToken = default)
    {
        if (_failed)
            return null;

        if (!await TryEnsureFilesAsync(cancellationToken))
            return null;

        try
        {
            var prefixed = (purpose == EmbeddingPurpose.Query ? QueryPrefix : PassagePrefix) + text;

            return await _model.UseAsync(model => Task.Run(() => model.Embed(prefixed), cancellationToken), cancellationToken);
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
        _model.Dispose();
        _downloadLock.Dispose();
    }

    private async Task<bool> TryEnsureFilesAsync(CancellationToken cancellationToken)
    {
        if (_hasFiles)
            return true;

        await _downloadLock.WaitAsync(cancellationToken);

        try
        {
            if (_hasFiles)
                return true;

            if (_failed)
                return false;

            await ModelDownloader.EnsureAsync(_tokenizerUrl, _tokenizerPath, _logger, cancellationToken);
            await ModelDownloader.EnsureAsync(_modelUrl, _modelPath, _logger, cancellationToken);

            _hasFiles = true;

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
            _downloadLock.Release();
        }
    }

    private Task<OnnxModel> LoadAsync()
    {
        try
        {
            return Task.FromResult(OnnxModel.Load(_modelPath, _tokenizerPath, Dimensions));
        }
        catch (Exception exception)
        {
            Fail(exception, "Embedding model could not be initialized; recall falls back to text matching.");

            throw;
        }
    }

    private void Fail(Exception exception, string message)
    {
        if (!_failed)
            Log.EmbedderFailed(_logger, exception, message);

        _failed = true;
    }
}
