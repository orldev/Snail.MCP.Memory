using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Snail.Sdk.Mcp.Hosting;

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

/// <summary>Server configuration: flat keys from the settings file or <c>SNAIL_MCP_MEMORY_*</c> variables, bound by <see cref="From"/>.</summary>
/// <remarks>
/// Where clients reach the server — <c>Transport</c>, <c>Url</c>, <c>Port</c> — is not here: the same settings are read for it by
/// <see cref="McpEndpoint.Read(IConfiguration, Uri, McpEndpointOptions)"/>, with <see cref="DefaultUrl"/> as the address and
/// <see cref="EndpointOptions"/> holding it to loopback.
/// </remarks>
public sealed class StorageOptions
{
    /// <summary>The server's short name, which names its settings file and its variables.</summary>
    public const string ServerName = "memory";

    /// <summary>Where the HTTP server listens unless <c>Url</c> (<c>SNAIL_MCP_MEMORY_URL</c>) or <c>Port</c> says otherwise.</summary>
    public static readonly Uri DefaultUrl = new("http://127.0.0.1:9230");

    /// <summary>How the endpoint is read: held to loopback wherever the server runs, its refusals naming the variables by <see cref="ServerName"/>.</summary>
    /// <remarks>
    /// The endpoint has no key and the memory holds everything a user ever told an agent, so no setting may serve it to
    /// the network: <c>ListensOnEveryInterface</c>, a URL at <c>0.0.0.0</c> or beyond loopback, and a start inside a
    /// container are refused at the start, naming the variable, where they used to open every interface.
    /// </remarks>
    public static readonly McpEndpointOptions EndpointOptions = new() { Reach = McpReach.LoopbackOnly, Server = ServerName };

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

    /// <summary>Days between background audit runs.</summary>
    public const int DefaultAuditIntervalDays = 7;

    /// <summary>Minutes without an embedding before the model is released from memory.</summary>
    /// <remarks>
    /// Released, the model gives back ~150 MB of the shared server's footprint, and the next embedding loads it again in
    /// 0.4 s, 1.6 s for a process's first load (2026-10-13). Hooks call the server at every session's start and stop, so
    /// half an hour without an embedding is a night, a weekend or a lunch, not a pause between two questions.
    /// </remarks>
    public const int DefaultEmbeddingIdleMinutes = 30;

    /// <summary>Which backend stores the memory. Default: <see cref="Provider.Sqlite"/>.</summary>
    public Provider Provider { get; set; } = Provider.Sqlite;

    /// <summary>Data directory. Empty — <c>~/.s3l-mcp-memory</c> (<see cref="ServerFolders.DataDirectory(string)"/>).</summary>
    public string? Directory { get; set; }

    /// <summary>SQLite: a file path or connection string, empty — <c>{Directory}/memory.db</c>; PostgreSQL: the full connection string, required.</summary>
    [SuppressMessage("Snail", "S3L0007", Justification = "A settings key the owner's files and SNAIL_MCP_MEMORY_CONNECTION_STRING bind by this name.")]
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

    /// <summary>Minutes without an embedding before the model is released. Empty — <see cref="DefaultEmbeddingIdleMinutes"/>; 0 — never.</summary>
    public int? EmbeddingIdleMinutes { get; set; }

    /// <summary>Duplicate similarity threshold of remember. Empty — <see cref="DefaultDuplicateThreshold"/>.</summary>
    public double? DuplicateThreshold { get; set; }

    /// <summary>Minimum similarity of a search hit. Empty — <see cref="DefaultSimilarityFloor"/>.</summary>
    public double? SimilarityFloor { get; set; }

    /// <summary>Minutes of idleness before the server shuts down. Empty — <see cref="DefaultIdleTimeoutMinutes"/>.</summary>
    public int? IdleTimeoutMinutes { get; set; }

    /// <summary>Days between background audit runs. Empty — <see cref="DefaultAuditIntervalDays"/>; 0 — the audit is off.</summary>
    public int? AuditIntervalDays { get; set; }

    /// <summary>Binds the options from the server's settings as <see cref="ServerSettings.Read(string)"/> read them: the settings file, then the variables, which win.</summary>
    /// <remarks>
    /// Bound before any host exists, so a value its property's type cannot hold — <c>SNAIL_MCP_MEMORY_IDLE_TIMEOUT_MINUTES=abc</c>
    /// — is refused through <see cref="ServerSettings.Bind(string, IConfiguration, Action{IConfiguration})"/> as a
    /// <see cref="StartRefusedException"/> naming the variable, where the binder's bare exception crashed the program with a trace.
    /// </remarks>
    /// <exception cref="StartRefusedException">A setting holds a value its property's type cannot hold.</exception>
    public static StorageOptions From(IConfiguration settings)
    {
        StorageOptions? bound = null;

        ServerSettings.Bind(ServerName, settings, read => bound = read.Get<StorageOptions>());

        return bound ?? new StorageOptions();
    }

    /// <summary>Reads options from <paramref name="file"/>, when there is one, and the server's variables among <paramref name="variables"/>.</summary>
    internal static StorageOptions Load(string? file, IDictionary variables) =>
        From(new ConfigurationBuilder()
            .AddServerSettings(ServerName, file, variables)
            .Build());

    /// <summary>Configured directory, absolute with a leading <c>~</c> expanded, or the default one.</summary>
    public string ResolveDirectory() => ServerFolders.FullPath(Directory, ServerFolders.DataDirectory(ServerName));

    /// <summary>Full connection string: SQLite wraps a bare path into <c>Data Source=</c>; PostgreSQL requires the configured string.</summary>
    /// <remarks>
    /// Resolved while the services are registered, before any host exists: a bare exception there ended the program
    /// with a trace and exit code 134, where the operator had only left a setting out.
    /// </remarks>
    /// <exception cref="StartRefusedException">The provider is PostgreSQL and no connection string is set.</exception>
    public string ResolveConnectionString()
    {
        if (Provider == Provider.Postgres)
            return string.IsNullOrWhiteSpace(ConnectionString)
                ? throw new StartRefusedException(
                    $"{ServerSettings.NameOfSetting(ServerName, nameof(ConnectionString))} is empty; Provider=Postgres needs one, such as 'Host=...;Database=...;Username=...;Password=...'.")
                : ConnectionString.Trim();

        if (string.IsNullOrWhiteSpace(ConnectionString))
            return $"Data Source={Path.Combine(ResolveDirectory(), DefaultDatabaseFileName)}";

        var value = ConnectionString.Trim();

        return value.Contains('=', StringComparison.Ordinal) ? value : $"Data Source={ServerFolders.FullPath(value)}";
    }

    /// <summary>Configured partition tag, case-folded; empty — the whole shared base.</summary>
    public string ResolveWorkspace() => (Workspace ?? "").Trim().ToLowerInvariant();

    /// <summary>Whether embeddings are computed; on unless switched off.</summary>
    public bool IsEmbeddingEnabled() => EmbeddingEnabled ?? true;

    /// <summary>Configured model path or the default file in the models directory.</summary>
    public string ResolveEmbeddingModelPath() =>
        string.IsNullOrWhiteSpace(EmbeddingModelPath)
            ? Path.Combine(ResolveDirectory(), ModelsDirectoryName, "model.onnx")
            : ServerFolders.FullPath(EmbeddingModelPath);

    /// <summary>Configured tokenizer path or the default file in the models directory.</summary>
    public string ResolveEmbeddingTokenizerPath() =>
        string.IsNullOrWhiteSpace(EmbeddingTokenizerPath)
            ? Path.Combine(ResolveDirectory(), ModelsDirectoryName, "sentencepiece.bpe.model")
            : ServerFolders.FullPath(EmbeddingTokenizerPath);

    /// <summary>Configured model URL or the default one.</summary>
    public string ResolveEmbeddingModelUrl() =>
        string.IsNullOrWhiteSpace(EmbeddingModelUrl) ? DefaultEmbeddingModelUrl : EmbeddingModelUrl.Trim();

    /// <summary>Configured tokenizer URL or the default one.</summary>
    public string ResolveEmbeddingTokenizerUrl() =>
        string.IsNullOrWhiteSpace(EmbeddingTokenizerUrl) ? DefaultEmbeddingTokenizerUrl : EmbeddingTokenizerUrl.Trim();

    /// <summary>Configured vector length or the default one.</summary>
    public int ResolveEmbeddingDimensions() =>
        EmbeddingDimensions is > 0 ? EmbeddingDimensions.Value : DefaultEmbeddingDimensions;

    /// <summary>How long the model may go unused before it is released: the configured minutes, zero for never, the default half hour otherwise.</summary>
    public TimeSpan ResolveEmbeddingIdlePeriod() => EmbeddingIdleMinutes switch
    {
        0 => TimeSpan.Zero,
        > 0 => TimeSpan.FromMinutes(EmbeddingIdleMinutes.Value),
        _ => TimeSpan.FromMinutes(DefaultEmbeddingIdleMinutes)
    };

    /// <summary>Configured duplicate threshold or the default one.</summary>
    public double ResolveDuplicateThreshold() =>
        DuplicateThreshold is > 0 and <= 1 ? DuplicateThreshold.Value : DefaultDuplicateThreshold;

    /// <summary>Configured similarity floor or the default one.</summary>
    public double ResolveSimilarityFloor() =>
        SimilarityFloor is >= 0 and <= 1 ? SimilarityFloor.Value : DefaultSimilarityFloor;

    /// <summary>Configured idle timeout or the default hour; the shutdown cannot be turned off, so zero and below are the default too.</summary>
    public TimeSpan ResolveIdleTimeout() =>
        TimeSpan.FromMinutes(IdleTimeoutMinutes is > 0 ? IdleTimeoutMinutes.Value : DefaultIdleTimeoutMinutes);

    /// <summary>Whether the background audit runs at all; an explicit 0 switches it off.</summary>
    public bool IsAuditEnabled() => AuditIntervalDays is not 0;

    /// <summary>Configured audit interval or the default week.</summary>
    public TimeSpan ResolveAuditInterval() =>
        TimeSpan.FromDays(AuditIntervalDays is > 0 ? AuditIntervalDays.Value : DefaultAuditIntervalDays);
}
