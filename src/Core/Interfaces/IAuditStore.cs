namespace Snail.MCP.Memory.Core.Interfaces;

/// <summary>Coordination of the background audit across server processes sharing one database.</summary>
/// <remarks>The lease lives in the database because processes are many and short-lived; a workspace audits itself.</remarks>
public interface IAuditStore
{
    /// <summary>Atomically claims the next audit run when <paramref name="interval"/> has elapsed since the last one; only one process wins.</summary>
    Task<bool> TryClaimAsync(TimeSpan interval, DateTimeOffset now, CancellationToken cancellationToken = default);
}
