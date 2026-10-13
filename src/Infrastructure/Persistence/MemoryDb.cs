using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Snail.MCP.Memory.Infrastructure.Persistence;

/// <summary>Hands out <see cref="MemoryDbContext"/> instances with the schema guaranteed to exist.</summary>
/// <remarks>Not <c>EnsureCreated</c>: it skips creation whenever the vector provider made its tables first, so the relational tables are created explicitly: EF's wholesale create in a database without tables, <c>IF NOT EXISTS</c> DDL in one that has some, and a normal start runs no command that fails.</remarks>
public sealed class MemoryDb(IDbContextFactory<MemoryDbContext> factory) : IDisposable
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _initialized;

    /// <summary>Creates a context; the first call creates the schema.</summary>
    public async Task<MemoryDbContext> CreateAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
            await InitializeAsync(cancellationToken);

        return await factory.CreateDbContextAsync(cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose() => _initLock.Dispose();

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _initLock.WaitAsync(cancellationToken);

        try
        {
            if (_initialized)
                return;

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var creator = context.GetService<IRelationalDatabaseCreator>();

            if (!await creator.ExistsAsync(cancellationToken))
                await creator.CreateAsync(cancellationToken);

            if (await creator.HasTablesAsync(cancellationToken))
                await UpgradeAsync(context, cancellationToken);
            else
                await CreateTablesAsync(creator, context, cancellationToken);

            if (!await TablesExistAsync(context, cancellationToken))
                throw new InvalidOperationException("The memory tables are missing after the schema was created and upgraded.");

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>DDL of every table, for a database that already holds tables: Memory's own of any release, the vector collection's, or a shared server's unrelated ones.</summary>
    /// <remarks>
    /// EF Core has no per-table create, and its wholesale one throws on the first table that exists, logging the failed command
    /// on every start; <c>IF NOT EXISTS</c> adds what is missing and passes over the rest without an error. Quoted identifiers
    /// and TEXT/BIGINT keep the statements valid for both SQLite and PostgreSQL; names and keys follow what EF creates.
    /// </remarks>
    private static readonly string[] UpgradeDdl =
    [
        """
        CREATE TABLE IF NOT EXISTS projects (
            "Workspace" TEXT NOT NULL,
            "Key" TEXT NOT NULL,
            "Name" TEXT NOT NULL,
            "Description" TEXT NOT NULL,
            "Created" BIGINT NOT NULL,
            CONSTRAINT "PK_projects" PRIMARY KEY ("Workspace", "Key")
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS edges (
            "Workspace" TEXT NOT NULL,
            "SourceKey" TEXT NOT NULL,
            "TargetKey" TEXT NOT NULL,
            "Type" TEXT NOT NULL,
            "Source" TEXT NOT NULL,
            "Target" TEXT NOT NULL,
            "Note" TEXT NOT NULL,
            "Updated" BIGINT NOT NULL,
            CONSTRAINT "PK_edges" PRIMARY KEY ("Workspace", "SourceKey", "TargetKey", "Type")
        )
        """,
        """CREATE INDEX IF NOT EXISTS "IX_edges_Workspace_TargetKey" ON edges ("Workspace", "TargetKey")""",
        """
        CREATE TABLE IF NOT EXISTS pending_links (
            "Workspace" TEXT NOT NULL,
            "ProjectKey" TEXT NOT NULL,
            "OtherKey" TEXT NOT NULL,
            "Project" TEXT NOT NULL,
            "Other" TEXT NOT NULL,
            "Evidence" TEXT NOT NULL,
            "Dismissed" BIGINT NOT NULL,
            "Updated" BIGINT NOT NULL,
            CONSTRAINT "PK_pending_links" PRIMARY KEY ("Workspace", "ProjectKey", "OtherKey")
        )
        """,
        """CREATE INDEX IF NOT EXISTS "IX_pending_links_Workspace_OtherKey" ON pending_links ("Workspace", "OtherKey")""",
        """
        CREATE TABLE IF NOT EXISTS audit_state (
            "Workspace" TEXT NOT NULL,
            "LastRun" BIGINT NOT NULL,
            CONSTRAINT "PK_audit_state" PRIMARY KEY ("Workspace")
        )
        """,
    ];

    /// <summary>EF's wholesale create, for a database that holds no table at all.</summary>
    /// <remarks>A concurrent start can create the tables between the check and this call: its "already exists" leaves only the upgrade to run, and a failure the upgrade does not mend is rethrown.</remarks>
    private static async Task CreateTablesAsync(IRelationalDatabaseCreator creator, MemoryDbContext context, CancellationToken cancellationToken)
    {
        try
        {
            await creator.CreateTablesAsync(cancellationToken);
        }
        catch (Exception)
        {
            await UpgradeAsync(context, cancellationToken);

            if (!await TablesExistAsync(context, cancellationToken))
                throw;
        }
    }

    private static async Task UpgradeAsync(MemoryDbContext context, CancellationToken cancellationToken)
    {
        foreach (var statement in UpgradeDdl)
            await context.Database.ExecuteSqlRawAsync(statement, cancellationToken);
    }

    private static async Task<bool> TablesExistAsync(MemoryDbContext context, CancellationToken cancellationToken)
    {
        try
        {
            _ = await context.Projects.AnyAsync(cancellationToken);
            _ = await context.Edges.AnyAsync(cancellationToken);
            _ = await context.PendingLinks.AnyAsync(cancellationToken);
            _ = await context.AuditState.AnyAsync(cancellationToken);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
