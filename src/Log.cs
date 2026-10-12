using Microsoft.Extensions.Logging;

namespace Snail.MCP.Memory;

/// <summary>What the server says about the background audit, the idle watch and the embedding model.</summary>
/// <remarks>
/// Generated rather than written through the logging extensions, which box and format their arguments before the level is
/// even checked. The messages are the ones the server has always written.
/// </remarks>
internal static partial class Log
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Memory audit queued {Count} link suggestion(s).")]
    internal static partial void AuditQueued(ILogger logger, int count);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Memory audit run failed.")]
    internal static partial void AuditFailed(ILogger logger, Exception failure);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "No MCP tool has been called for {IdleFor}. The process looks orphaned — shutting the server down...")]
    internal static partial void Orphaned(ILogger logger, TimeSpan idleFor);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Downloading embedding asset {Url} into {Path}...")]
    internal static partial void Downloading(ILogger logger, string url, string path);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Downloaded {Path} ({Size} MB).")]
    internal static partial void Downloaded(ILogger logger, string path, long size);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "{Message}")]
    internal static partial void EmbedderFailed(ILogger logger, Exception failure, string message);
}
