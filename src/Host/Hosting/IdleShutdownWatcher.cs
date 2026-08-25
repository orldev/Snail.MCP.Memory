using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Snail.MCP.Memory.Host.Hosting;

/// <summary>Stops an orphaned process when no MCP tool has been called for longer than the idle timeout.</summary>
/// <remarks>Never touches stdin — the SDK transport owns it, and a second reader would race for protocol bytes.</remarks>
/// <param name="idleTimeout">Idle period after which the host stops; 1 hour when omitted.</param>
/// <param name="pollInterval">How often activity is checked; 5 minutes when omitted.</param>
public sealed class IdleShutdownWatcher(
    IActivityTracker activity,
    IHostApplicationLifetime appLifetime,
    ILogger<IdleShutdownWatcher> logger,
    TimeProvider? timeProvider = null,
    TimeSpan? idleTimeout = null,
    TimeSpan? pollInterval = null)
    : BackgroundService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly TimeSpan _idleTimeout = idleTimeout ?? TimeSpan.FromHours(1);
    private readonly TimeSpan _pollInterval = pollInterval ?? TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_pollInterval, _timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var idleFor = _timeProvider.GetUtcNow().UtcDateTime - activity.LastActivityUtc;
                if (idleFor < _idleTimeout) continue;

                logger.LogWarning(
                    "No MCP tool has been called for {IdleFor}. The process looks orphaned — shutting the server down...",
                    idleFor);
                appLifetime.StopApplication();
                return;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
