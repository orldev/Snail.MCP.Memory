using Microsoft.Extensions.DependencyInjection;
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
    MentionAudit audit,
    TimeProvider timeProvider,
    ILogger<AuditWorker> logger,
    TimeSpan interval,
    TimeSpan checkPeriod) : BackgroundService
{
    private volatile bool _isRunning;

    /// <summary>How often a process checks whether the interval elapsed.</summary>
    public static readonly TimeSpan DefaultCheckPeriod = TimeSpan.FromHours(1);

    /// <summary>Whether a run is being claimed or carried out right now.</summary>
    public bool IsRunning => _isRunning;

    /// <summary>Whether the audit among these services is at work, for the idle shutdown to wait for.</summary>
    /// <remarks>
    /// The lease is claimed before the run: a server stopped between the two has spent the interval and audited
    /// nothing, and the next audit would come only after another interval, days later.
    /// </remarks>
    public static bool IsAuditing(IServiceProvider services) => services.GetService<AuditWorker>()?.IsRunning ?? false;

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
        _isRunning = true;

        try
        {
            if (!await state.TryClaimAsync(interval, timeProvider.GetUtcNow(), stoppingToken))
                return;

            var queued = await audit.RunAsync(stoppingToken);
            Log.AuditQueued(logger, queued);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Log.AuditFailed(logger, exception);
        }
        finally
        {
            _isRunning = false;
        }
    }
}
