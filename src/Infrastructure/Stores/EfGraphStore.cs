using Microsoft.EntityFrameworkCore;
using Snail.MCP.Memory.Infrastructure.Persistence;

namespace Snail.MCP.Memory.Infrastructure.Stores;

/// <summary><see cref="IGraphStore"/> over the <c>edges</c> table.</summary>
/// <remarks>Reads honor the workspace partition (empty — the whole base), writes always tag it.</remarks>
public sealed class EfGraphStore(MemoryDb database, string workspace) : IGraphStore
{
    /// <inheritdoc />
    public async Task<bool> UpsertAsync(GraphEdge edge, CancellationToken cancellationToken = default)
    {
        var sourceKey = ProjectIdentity.Key(edge.Source);
        var targetKey = ProjectIdentity.Key(edge.Target);

        await using var context = await database.CreateAsync(cancellationToken);

        var existing = await context.Edges.FirstOrDefaultAsync(row =>
            row.Workspace == workspace && row.SourceKey == sourceKey && row.TargetKey == targetKey && row.Type == edge.Type,
            cancellationToken);

        if (existing is null)
        {
            context.Edges.Add(new EdgeRow
            {
                Workspace = workspace,
                SourceKey = sourceKey,
                TargetKey = targetKey,
                Type = edge.Type,
                Source = edge.Source,
                Target = edge.Target,
                Note = edge.Note,
                Updated = edge.Updated.ToUnixTimeSeconds(),
            });
        }
        else
        {
            existing.Source = edge.Source;
            existing.Target = edge.Target;
            existing.Note = edge.Note;
            existing.Updated = edge.Updated.ToUnixTimeSeconds();
        }

        await context.SaveChangesAsync(cancellationToken);

        return existing is null;
    }

    /// <inheritdoc />
    public async Task<int> RemoveAsync(string sourceKey, string targetKey, string type, CancellationToken cancellationToken = default)
    {
        await using var context = await database.CreateAsync(cancellationToken);

        return await Scoped(context)
            .Where(row => (row.SourceKey == sourceKey && row.TargetKey == targetKey) ||
                          (row.SourceKey == targetKey && row.TargetKey == sourceKey))
            .Where(row => type == "" || row.Type == type)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GraphEdge>> GetForProjectsAsync(IReadOnlyCollection<string> projectKeys, CancellationToken cancellationToken = default)
    {
        if (projectKeys.Count == 0)
            return [];

        await using var context = await database.CreateAsync(cancellationToken);

        var rows = await Scoped(context)
            .Where(row => projectKeys.Contains(row.SourceKey) || projectKeys.Contains(row.TargetKey))
            .ToListAsync(cancellationToken);

        return [.. rows.Select(ToEdge)];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GraphEdge>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await database.CreateAsync(cancellationToken);

        var rows = await Scoped(context).ToListAsync(cancellationToken);

        return [.. rows.Select(ToEdge)];
    }

    /// <inheritdoc />
    public async Task<int> RenameProjectAsync(string oldKey, string newName, CancellationToken cancellationToken = default)
    {
        var newKey = ProjectIdentity.Key(newName);
        var displayName = ProjectIdentity.Normalize(newName);

        await using var context = await database.CreateAsync(cancellationToken);

        var changed = await Scoped(context)
            .Where(row => row.SourceKey == oldKey)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.SourceKey, newKey)
                .SetProperty(row => row.Source, displayName), cancellationToken);

        changed += await Scoped(context)
            .Where(row => row.TargetKey == oldKey)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.TargetKey, newKey)
                .SetProperty(row => row.Target, displayName), cancellationToken);

        return changed;
    }

    private IQueryable<EdgeRow> Scoped(MemoryDbContext context) =>
        workspace.Length == 0 ? context.Edges : context.Edges.Where(row => row.Workspace == workspace);

    private static GraphEdge ToEdge(EdgeRow row) =>
        new(row.Source, row.Target, row.Type, row.Note, DateTimeOffset.FromUnixTimeSeconds(row.Updated));
}
