using Microsoft.EntityFrameworkCore;
using Snail.MCP.Memory.Infrastructure.Persistence;

namespace Snail.MCP.Memory.Infrastructure.Stores;

/// <summary><see cref="IProjectStore"/> over the <c>projects</c> table.</summary>
/// <remarks>Reads honor the workspace partition (empty — the whole base), writes always tag it.</remarks>
public sealed class EfProjectStore(MemoryDb database, string workspace) : IProjectStore
{
    /// <inheritdoc />
    public async Task<ProjectInfo?> GetAsync(string projectKey, CancellationToken cancellationToken = default)
    {
        await using var context = await database.CreateAsync(cancellationToken);

        var row = await Scoped(context).FirstOrDefaultAsync(project => project.Key == projectKey, cancellationToken);

        return row is null ? null : ToInfo(row);
    }

    /// <inheritdoc />
    public async Task<bool> CreateAsync(ProjectInfo project, CancellationToken cancellationToken = default)
    {
        var key = ProjectIdentity.Key(project.Name);

        await using var context = await database.CreateAsync(cancellationToken);

        if (await Scoped(context).AnyAsync(row => row.Key == key, cancellationToken))
            return false;

        context.Projects.Add(new ProjectRow
        {
            Workspace = workspace,
            Key = key,
            Name = project.Name,
            Description = project.Description,
            Created = project.Created.ToUnixTimeSeconds(),
        });

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

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await database.CreateAsync(cancellationToken);

        return await Scoped(context).Select(row => ToInfo(row)).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> RenameAsync(string oldKey, string newName, CancellationToken cancellationToken = default)
    {
        var newKey = ProjectIdentity.Key(newName);
        var displayName = ProjectIdentity.Normalize(newName);

        await using var context = await database.CreateAsync(cancellationToken);

        var row = await Scoped(context).FirstOrDefaultAsync(project => project.Key == oldKey, cancellationToken);

        if (row is null)
            return false;

        if (newKey != oldKey &&
            await context.Projects.AnyAsync(other => other.Workspace == row.Workspace && other.Key == newKey, cancellationToken))
            return false;

        return await context.Projects
            .Where(project => project.Workspace == row.Workspace && project.Key == oldKey)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(project => project.Key, newKey)
                .SetProperty(project => project.Name, displayName), cancellationToken) > 0;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string projectKey, CancellationToken cancellationToken = default)
    {
        await using var context = await database.CreateAsync(cancellationToken);

        return await Scoped(context)
            .Where(project => project.Key == projectKey)
            .ExecuteDeleteAsync(cancellationToken) > 0;
    }

    private IQueryable<ProjectRow> Scoped(MemoryDbContext context) =>
        workspace.Length == 0 ? context.Projects : context.Projects.Where(project => project.Workspace == workspace);

    private static ProjectInfo ToInfo(ProjectRow row) =>
        new(row.Name, row.Description, DateTimeOffset.FromUnixTimeSeconds(row.Created));
}
