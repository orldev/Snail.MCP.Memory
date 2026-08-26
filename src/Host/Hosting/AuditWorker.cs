using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Snail.MCP.Memory.Application;

namespace Snail.MCP.Memory.Host.Hosting;

/// <summary>
/// Runs the memory audit behind a database lease. Server processes are many and short-lived
/// (one per connected agent, idle-shutdown), so no process owns a schedule: every process
/// checks periodically, and only the one that atomically claims the elapsed interval performs
/// the run — however many agents share the database, the audit happens once per interval.
/// </summary>
public sealed class AuditWorker(
    IAuditStore state,
    AuditService audit,
    TimeProvider timeProvider,
    ILogger<AuditWorker> logger,
    TimeSpan interval,
    TimeSpan checkPeriod) : BackgroundService
{
    /// <summary>How often a process checks whether the interval elapsed.</summary>
    public static readonly TimeSpan DefaultCheckPeriod = TimeSpan.FromHours(1);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(checkPeriod, timeProvider);

        try
        {
            do
            {
                await TryRunAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task TryRunAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (!await state.TryClaimAsync(interval, timeProvider.GetUtcNow(), stoppingToken))
                return;

            var queued = await audit.RunAsync(stoppingToken);
            logger.LogInformation("Memory audit queued {Count} link suggestion(s).", queued);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Memory audit run failed.");
        }
    }
}
