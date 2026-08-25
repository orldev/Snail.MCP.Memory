using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Snail.MCP.Memory.Infrastructure.Persistence;

/// <summary>Hands out <see cref="MemoryDbContext"/> instances with the schema guaranteed to exist.</summary>
/// <remarks>Not <c>EnsureCreated</c>: it skips creation whenever the vector provider made its tables first, so the relational tables are created explicitly and "already exists" counts as success.</remarks>
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

            try
            {
                await creator.CreateTablesAsync(cancellationToken);
            }
            catch (Exception)
            {
                if (!await TablesExistAsync(context, cancellationToken))
                    throw;
            }

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static async Task<bool> TablesExistAsync(MemoryDbContext context, CancellationToken cancellationToken)
    {
        try
        {
            _ = await context.Projects.AnyAsync(cancellationToken);
            _ = await context.Edges.AnyAsync(cancellationToken);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
