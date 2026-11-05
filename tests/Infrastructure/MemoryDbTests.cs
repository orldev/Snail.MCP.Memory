using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Snail.MCP.Memory.Infrastructure.Persistence;
using Snail.MCP.Memory.Tests.TestDoubles;

namespace Snail.MCP.Memory.Tests.Infrastructure;

/// Schema initialization over real SQLite files: a fresh database is created, an older one is
/// upgraded, and a start against any database that is fine prints no failed command — the
/// installed server used to log one on every start, because EF's wholesale create was tried first.
public sealed class MemoryDbTests : IDisposable
{
    private static readonly string[] MemoryTables = ["audit_state", "edges", "pending_links", "projects"];

    private readonly DirectoryInfo _tempDir = new DirectoryInfo(TestFolder.Create());
    private readonly List<string> _connections = [];
    private readonly List<string> _errors = [];

    public void Dispose()
    {
        foreach (var connection in _connections)
            SqlitePools.Release(connection);

        _tempDir.Delete(recursive: true);
    }

    [Fact]
    public async Task FreshDatabase_IsCreatedWithEveryTable()
    {
        var connection = Connection("memory.db");

        await StartAsync(connection);

        Assert.Equal(MemoryTables, await TablesOfAsync(connection));
        Assert.Empty(_errors);
    }

    [Fact]
    public async Task UpToDateDatabase_StartsWithoutAFailedCommand()
    {
        var connection = Connection("memory.db");

        await StartAsync(connection);
        await StartAsync(connection);

        Assert.Equal(MemoryTables, await TablesOfAsync(connection));
        Assert.Empty(_errors);
    }

    /// A database of the first release holds projects and edges only, as EF created them then; the
    /// start adds the pending and audit tables and keeps the rows.
    [Fact]
    public async Task OlderDatabase_GetsThePendingAndAuditTablesWithoutAFailedCommand()
    {
        var connection = Connection("memory.db");

        await ExecuteAsync(connection,
            """
            CREATE TABLE "edges" ("Workspace" TEXT NOT NULL, "SourceKey" TEXT NOT NULL, "TargetKey" TEXT NOT NULL,
                "Type" TEXT NOT NULL, "Source" TEXT NOT NULL, "Target" TEXT NOT NULL, "Note" TEXT NOT NULL,
                "Updated" INTEGER NOT NULL,
                CONSTRAINT "PK_edges" PRIMARY KEY ("Workspace", "SourceKey", "TargetKey", "Type"));
            CREATE TABLE "projects" ("Workspace" TEXT NOT NULL, "Key" TEXT NOT NULL, "Name" TEXT NOT NULL,
                "Description" TEXT NOT NULL, "Created" INTEGER NOT NULL,
                CONSTRAINT "PK_projects" PRIMARY KEY ("Workspace", "Key"));
            CREATE INDEX "IX_edges_Workspace_TargetKey" ON "edges" ("Workspace", "TargetKey");
            INSERT INTO "projects" VALUES ('', 'kept', 'Kept', 'written before the upgrade', 0);
            """);

        using var database = Open(connection);
        var now = DateTimeOffset.Parse("2026-08-25T12:00:00Z");

        Assert.Equal("Kept", Assert.Single(await new EfProjectStore(database, "").ListAsync()).Name);
        Assert.True(await new EfPendingLinkStore(database, "").TryProposeAsync(new PendingLink("A", "B", "seen", now)));
        Assert.True(await new EfAuditStore(database, "").TryClaimAsync(TimeSpan.FromDays(7), now));
        Assert.Equal(MemoryTables, await TablesOfAsync(connection));
        Assert.Empty(_errors);
    }

    /// A database that already holds somebody else's tables — the vector collection here, unrelated
    /// tables on a shared PostgreSQL — gets Memory's tables, shaped as EF creates them in an empty one.
    [Fact]
    public async Task DatabaseWithForeignTablesOnly_GetsTheTablesTheModelDefines()
    {
        var shared = Connection("shared.db");
        var fresh = Connection("fresh.db");

        await ExecuteAsync(shared, """CREATE TABLE "memories" ("Key" TEXT NOT NULL PRIMARY KEY, "Content" TEXT)""");

        await StartAsync(shared);
        await StartAsync(fresh);

        Assert.Equal(MemoryTables, await TablesOfAsync(shared));
        Assert.Equal(await SchemaOfAsync(fresh), await SchemaOfAsync(shared));
        Assert.Empty(_errors);
    }

    /// The quiet start must not hide a real failure: tables that cannot be created stop the start.
    [Fact]
    public async Task DatabaseThatCannotTakeTheTables_FailsTheStart()
    {
        var path = Path.Combine(_tempDir.FullName, "readonly.db");

        await ExecuteAsync(Connection("readonly.db"), """CREATE TABLE "memories" ("Key" TEXT NOT NULL PRIMARY KEY)""");

        using var database = Open(Track($"Data Source={path};Mode=ReadOnly"));

        await Assert.ThrowsAnyAsync<Exception>(() => database.CreateAsync());
    }

    private string Connection(string file) => Track($"Data Source={Path.Combine(_tempDir.FullName, file)}");

    private string Track(string connection)
    {
        _connections.Add(connection);

        return connection;
    }

    /// The memory database over <paramref name="connection"/>, with every EF error captured — the
    /// failed-command line of the installed server's log is one of them.
    private MemoryDb Open(string connection)
    {
        var options = new DbContextOptionsBuilder<MemoryDbContext>()
            .UseSqlite(connection)
            .LogTo(_errors.Add, LogLevel.Error)
            .Options;

        return new MemoryDb(new Factory(options));
    }

    private async Task StartAsync(string connection)
    {
        using var database = Open(connection);

        await using var context = await database.CreateAsync();
    }

    private static async Task ExecuteAsync(string connection, string sql)
    {
        await using var sqlite = new SqliteConnection(connection);
        await sqlite.OpenAsync();
        await using var command = sqlite.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static Task<List<string>> TablesOfAsync(string connection) => QueryAsync(connection,
        """
        SELECT name FROM sqlite_master
        WHERE type = 'table' AND name IN ('audit_state', 'edges', 'pending_links', 'projects')
        ORDER BY name
        """);

    /// Columns with their affinity, nullability and key position, and the named indexes — what the
    /// stores rely on, leaving out the declared type names, which differ between EF's DDL and the
    /// provider-neutral one.
    private static Task<List<string>> SchemaOfAsync(string connection) => QueryAsync(connection,
        """
        SELECT m.name || '.' || c.name
            || ' ' || CASE WHEN upper(c.type) LIKE '%INT%' THEN 'INTEGER' ELSE upper(c.type) END
            || ' notnull=' || c."notnull" || ' pk=' || c.pk
        FROM sqlite_master AS m JOIN pragma_table_info(m.name) AS c
        WHERE m.type = 'table' AND m.name IN ('audit_state', 'edges', 'pending_links', 'projects')
        UNION ALL
        SELECT 'index ' || name || ' on ' || tbl_name FROM sqlite_master
        WHERE type = 'index' AND name NOT LIKE 'sqlite_%'
        ORDER BY 1
        """);

    private static async Task<List<string>> QueryAsync(string connection, string sql)
    {
        await using var sqlite = new SqliteConnection(connection);
        await sqlite.OpenAsync();
        await using var command = sqlite.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();

        while (await reader.ReadAsync())
            rows.Add(reader.GetString(0));

        return rows;
    }

    private sealed class Factory(DbContextOptions<MemoryDbContext> options) : IDbContextFactory<MemoryDbContext>
    {
        public MemoryDbContext CreateDbContext() => new(options);
    }
}
