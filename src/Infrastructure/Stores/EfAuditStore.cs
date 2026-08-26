using Microsoft.EntityFrameworkCore;
using Snail.MCP.Memory.Infrastructure.Persistence;

namespace Snail.MCP.Memory.Infrastructure.Stores;

/// <summary><see cref="IAuditStore"/> over the <c>audit_state</c> table.</summary>
/// <remarks>The claim is one conditional UPDATE — the database serializes concurrent claimants, no in-process locking needed.</remarks>
public sealed class EfAuditStore(MemoryDb database, string workspace) : IAuditStore
{
    /// <inheritdoc />
    public async Task<bool> TryClaimAsync(TimeSpan interval, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var cutoff = (now - interval).ToUnixTimeSeconds();

        await using var context = await database.CreateAsync(cancellationToken);

        var claimed = await context.AuditState
            .Where(row => row.Workspace == workspace && row.LastRun <= cutoff)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.LastRun, now.ToUnixTimeSeconds()), cancellationToken);

        if (claimed > 0)
            return true;

        if (await context.AuditState.AnyAsync(row => row.Workspace == workspace, cancellationToken))
            return false;

        context.AuditState.Add(new AuditStateRow { Workspace = workspace, LastRun = now.ToUnixTimeSeconds() });

        try
        {
            await context.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }
}
