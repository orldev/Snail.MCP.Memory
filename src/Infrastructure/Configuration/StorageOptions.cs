using Microsoft.Extensions.Configuration;

namespace Snail.MCP.Memory.Infrastructure.Configuration;

/// <summary>
/// Storage backend. A new provider is a new value here plus its own
/// <c>services.Add{Provider}Storage(...)</c> in Host; the core and the tools stay unchanged.
/// </summary>
public enum Provider
{
    /// <summary>SQLite with the sqlite-vec extension, single file: <c>{Directory}/memory.db</c>.</summary>
    Sqlite,

    /// <summary>PostgreSQL with the pgvector extension; <see cref="StorageOptions.ConnectionString"/> is required.</summary>
    Postgres
}

/// <summary>Server configuration: flat keys from a JSON config file or <c>SNAIL_MCP_MEMORY_*</c> variables; <see cref="Load"/> is the single reader.</summary>
public sealed class StorageOptions
{
    /// <summary>Database file name inside the data directory.</summary>
    public const string DefaultDatabaseFileName = "memory.db";

    /// <summary>Models sub-directory inside the data directory.</summary>
    public const string ModelsDirectoryName = "models";

    /// <summary>Default embedding model: multilingual-e5-small, ONNX, int8-quantized (~118 MB).</summary>
    public const string DefaultEmbeddingModelUrl =
        "https://huggingface.co/Xenova/multilingual-e5-small/resolve/main/onnx/model_quantized.onnx";

    /// <summary>Default tokenizer: the SentencePiece model of multilingual-e5-small (~5 MB).</summary>
    public const string DefaultEmbeddingTokenizerUrl =
        "https://huggingface.co/intfloat/multilingual-e5-small/resolve/main/sentencepiece.bpe.model";

    /// <summary>Vector length of the default model.</summary>
    public const int DefaultEmbeddingDimensions = 384;

    /// <summary>Similarity above which <c>remember</c> refuses to insert; high because e5 similarities are compressed upwards.</summary>
    public const double DefaultDuplicateThreshold = 0.95;

    /// <summary>Similarity below which a search hit is dropped; tuned to e5, whose unrelated pairs sit around 0.7.</summary>
    public const double DefaultSimilarityFloor = 0.75;

    /// <summary>Minutes without a tool call before the idle watchdog stops the process.</summary>
    public const int DefaultIdleTimeoutMinutes = 60;

    /// <summary>Which backend stores the memory. Default: <see cref="Provider.Sqlite"/>.</summary>
    public Provider Provider { get; set; } = Provider.Sqlite;

    /// <summary>Data directory. Empty — <see cref="ServerPaths.DefaultDataDirectory"/>.</summary>
    public string? Directory { get; set; }

    /// <summary>SQLite: a file path or connection string, empty — <c>{Directory}/memory.db</c>; PostgreSQL: the full connection string, required.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Partition of a shared database: writes are tagged with it, reads narrowed to it; empty — the whole base.</summary>
    public string? Workspace { get; set; }

    /// <summary>Whether embeddings are computed at all. Empty — <c>true</c>.</summary>
    public bool? EmbeddingEnabled { get; set; }

    /// <summary>Path of the ONNX model file. Empty — <c>{Directory}/models/model.onnx</c>, downloaded on first use.</summary>
    public string? EmbeddingModelPath { get; set; }

    /// <summary>Path of the SentencePiece tokenizer file. Empty — <c>{Directory}/models/sentencepiece.bpe.model</c>.</summary>
    public string? EmbeddingTokenizerPath { get; set; }

    /// <summary>Where the model is downloaded from when missing. Empty — <see cref="DefaultEmbeddingModelUrl"/>.</summary>
    public string? EmbeddingModelUrl { get; set; }

    /// <summary>Where the tokenizer is downloaded from when missing. Empty — <see cref="DefaultEmbeddingTokenizerUrl"/>.</summary>
    public string? EmbeddingTokenizerUrl { get; set; }

    /// <summary>Vector length of the configured model. Empty — <see cref="DefaultEmbeddingDimensions"/>.</summary>
    public int? EmbeddingDimensions { get; set; }

    /// <summary>Duplicate similarity threshold of remember. Empty — <see cref="DefaultDuplicateThreshold"/>.</summary>
    public double? DuplicateThreshold { get; set; }

    /// <summary>Minimum similarity of a search hit. Empty — <see cref="DefaultSimilarityFloor"/>.</summary>
    public double? SimilarityFloor { get; set; }

    /// <summary>Minutes of idleness before the server shuts down. Empty — <see cref="DefaultIdleTimeoutMinutes"/>.</summary>
    public int? IdleTimeoutMinutes { get; set; }

    /// <summary>Reads options from the config file and the environment; the environment wins.</summary>
    public static StorageOptions Load()
    {
        var builder = new ConfigurationBuilder();

        if (ServerPaths.FindConfigFile() is { } configFile)
            builder.AddJsonFile(Path.GetFullPath(configFile), optional: true, reloadOnChange: false);

        return builder.AddEnvironment().Build().Get<StorageOptions>() ?? new StorageOptions();
    }

    /// <summary>Configured directory or the default one, with a leading <c>~</c> expanded.</summary>
    public string ResolveDirectory() => ServerPaths.ExpandHome(Directory);

    /// <summary>Full connection string: SQLite wraps a bare path into <c>Data Source=</c>; PostgreSQL requires the configured string.</summary>
    public string ResolveConnectionString()
    {
        if (Provider == Provider.Postgres)
            return string.IsNullOrWhiteSpace(ConnectionString)
                ? throw new InvalidOperationException(
                    "Provider=Postgres requires ConnectionString (e.g. 'Host=...;Database=...;Username=...;Password=...').")
                : ConnectionString.Trim();

        if (string.IsNullOrWhiteSpace(ConnectionString))
            return $"Data Source={Path.Combine(ResolveDirectory(), DefaultDatabaseFileName)}";

        var value = ConnectionString.Trim();

        return value.Contains('=') ? value : $"Data Source={ServerPaths.ExpandHome(value)}";
    }

    /// <summary>Configured partition tag, case-folded; empty — the whole shared base.</summary>
    public string ResolveWorkspace() => (Workspace ?? "").Trim().ToLowerInvariant();

    /// <summary>Whether embeddings are computed; on unless switched off.</summary>
    public bool ResolveEmbeddingEnabled() => EmbeddingEnabled ?? true;

    /// <summary>Configured model path or the default file in the models directory.</summary>
    public string ResolveEmbeddingModelPath() =>
        string.IsNullOrWhiteSpace(EmbeddingModelPath)
            ? Path.Combine(ResolveDirectory(), ModelsDirectoryName, "model.onnx")
            : ServerPaths.ExpandHome(EmbeddingModelPath);

    /// <summary>Configured tokenizer path or the default file in the models directory.</summary>
    public string ResolveEmbeddingTokenizerPath() =>
        string.IsNullOrWhiteSpace(EmbeddingTokenizerPath)
            ? Path.Combine(ResolveDirectory(), ModelsDirectoryName, "sentencepiece.bpe.model")
            : ServerPaths.ExpandHome(EmbeddingTokenizerPath);

    /// <summary>Configured model URL or the default one.</summary>
    public string ResolveEmbeddingModelUrl() =>
        string.IsNullOrWhiteSpace(EmbeddingModelUrl) ? DefaultEmbeddingModelUrl : EmbeddingModelUrl.Trim();

    /// <summary>Configured tokenizer URL or the default one.</summary>
    public string ResolveEmbeddingTokenizerUrl() =>
        string.IsNullOrWhiteSpace(EmbeddingTokenizerUrl) ? DefaultEmbeddingTokenizerUrl : EmbeddingTokenizerUrl.Trim();

    /// <summary>Configured vector length or the default one.</summary>
    public int ResolveEmbeddingDimensions() =>
        EmbeddingDimensions is > 0 ? EmbeddingDimensions.Value : DefaultEmbeddingDimensions;

    /// <summary>Configured duplicate threshold or the default one.</summary>
    public double ResolveDuplicateThreshold() =>
        DuplicateThreshold is > 0 and <= 1 ? DuplicateThreshold.Value : DefaultDuplicateThreshold;

    /// <summary>Configured similarity floor or the default one.</summary>
    public double ResolveSimilarityFloor() =>
        SimilarityFloor is >= 0 and <= 1 ? SimilarityFloor.Value : DefaultSimilarityFloor;

    /// <summary>Configured idle timeout or the default hour.</summary>
    public TimeSpan ResolveIdleTimeout() =>
        TimeSpan.FromMinutes(IdleTimeoutMinutes is > 0 ? IdleTimeoutMinutes.Value : DefaultIdleTimeoutMinutes);

}
