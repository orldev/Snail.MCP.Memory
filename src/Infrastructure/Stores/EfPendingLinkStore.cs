using Microsoft.EntityFrameworkCore;
using Snail.MCP.Memory.Infrastructure.Persistence;

namespace Snail.MCP.Memory.Infrastructure.Stores;

/// <summary><see cref="IPendingLinkStore"/> over the <c>pending_links</c> table.</summary>
/// <remarks>A pair is stored as proposed but treated as undirected: every lookup checks both orders.</remarks>
public sealed class EfPendingLinkStore(MemoryDb database, string workspace) : IPendingLinkStore
{
    /// <inheritdoc />
    public async Task<bool> ProposeAsync(PendingLink link, CancellationToken cancellationToken = default)
    {
        var projectKey = ProjectIdentity.Key(link.Project);
        var otherKey = ProjectIdentity.Key(link.Other);

        await using var context = await database.CreateAsync(cancellationToken);

        if (await PairOf(context, projectKey, otherKey).AnyAsync(cancellationToken))
            return false;

        context.PendingLinks.Add(new PendingLinkRow
        {
            Workspace = workspace,
            ProjectKey = projectKey,
            OtherKey = otherKey,
            Project = link.Project,
            Other = link.Other,
            Evidence = link.Evidence,
            Dismissed = 0,
            Updated = link.Updated.ToUnixTimeSeconds(),
        });

        await context.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PendingLink>> ListAsync(string projectKey = "", CancellationToken cancellationToken = default)
    {
        await using var context = await database.CreateAsync(cancellationToken);

        var rows = await Scoped(context)
            .Where(row => row.Dismissed == 0)
            .Where(row => projectKey == "" || row.ProjectKey == projectKey || row.OtherKey == projectKey)
            .OrderBy(row => row.Updated)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row =>
            new PendingLink(row.Project, row.Other, row.Evidence, DateTimeOffset.FromUnixTimeSeconds(row.Updated)))];
    }

    /// <inheritdoc />
    public async Task<int> ResolveAsync(string aKey, string bKey, CancellationToken cancellationToken = default)
    {
        await using var context = await database.CreateAsync(cancellationToken);

        return await PairOf(context, aKey, bKey).ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DismissAsync(string project, string other, DateTimeOffset when, CancellationToken cancellationToken = default)
    {
        var projectKey = ProjectIdentity.Key(project);
        var otherKey = ProjectIdentity.Key(other);

        await using var context = await database.CreateAsync(cancellationToken);

        var existing = await PairOf(context, projectKey, otherKey).FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            context.PendingLinks.Add(new PendingLinkRow
            {
                Workspace = workspace,
                ProjectKey = projectKey,
                OtherKey = otherKey,
                Project = project,
                Other = other,
                Evidence = "",
                Dismissed = 1,
                Updated = when.ToUnixTimeSeconds(),
            });
        }
        else
        {
            existing.Dismissed = 1;
            existing.Updated = when.ToUnixTimeSeconds();
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> RenameProjectAsync(string oldKey, string newName, CancellationToken cancellationToken = default)
    {
        var newKey = ProjectIdentity.Key(newName);
        var displayName = ProjectIdentity.Normalize(newName);

        await using var context = await database.CreateAsync(cancellationToken);

        var changed = await Scoped(context)
            .Where(row => row.ProjectKey == oldKey)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.ProjectKey, newKey)
                .SetProperty(row => row.Project, displayName), cancellationToken);

        changed += await Scoped(context)
            .Where(row => row.OtherKey == oldKey)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.OtherKey, newKey)
                .SetProperty(row => row.Other, displayName), cancellationToken);

        return changed;
    }

    private IQueryable<PendingLinkRow> Scoped(MemoryDbContext context) =>
        workspace.Length == 0 ? context.PendingLinks : context.PendingLinks.Where(row => row.Workspace == workspace);

    private IQueryable<PendingLinkRow> PairOf(MemoryDbContext context, string aKey, string bKey) =>
        Scoped(context).Where(row => (row.ProjectKey == aKey && row.OtherKey == bKey) ||
                                     (row.ProjectKey == bKey && row.OtherKey == aKey));
}
