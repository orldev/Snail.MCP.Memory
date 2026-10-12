using Microsoft.Data.Sqlite;

namespace Snail.MCP.Memory.Tests.TestDoubles;

/// <summary>Lets go of one test database's pooled connections, so that its folder can be deleted.</summary>
/// <remarks>
/// Never <c>SqliteConnection.ClearAllPools</c>: test classes run in parallel, and clearing every pool of the process
/// disposes the handles another class is taking out of its own pool at that moment. That was the flaky test — a store
/// or service test failing at random with an ObjectDisposedException of <c>SQLitePCL.sqlite3</c> inside
/// <c>SqliteConnection.Open</c>, a few runs in thirty under load.
/// </remarks>
internal static class SqlitePools
{
    /// <summary>Clears the pool of this connection string only.</summary>
    public static void Release(string database)
    {
        using var connection = new SqliteConnection(database);

        SqliteConnection.ClearPool(connection);
    }
}
