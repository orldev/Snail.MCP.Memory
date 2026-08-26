using Microsoft.EntityFrameworkCore;

namespace Snail.MCP.Memory.Infrastructure.Persistence;

/// <summary>Table row of a registered project.</summary>
public sealed class ProjectRow
{
    /// <summary>Partition tag; empty for the unpartitioned base.</summary>
    public string Workspace { get; set; } = "";

    /// <summary>Case-folded project key.</summary>
    public string Key { get; set; } = "";

    /// <summary>Display name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Description given at init.</summary>
    public string Description { get; set; } = "";

    /// <summary>Unix seconds of initialization.</summary>
    public long Created { get; set; }
}

/// <summary>Table row of a directed edge.</summary>
public sealed class EdgeRow
{
    /// <summary>Partition tag; empty for the unpartitioned base.</summary>
    public string Workspace { get; set; } = "";

    /// <summary>Case-folded key of the source project.</summary>
    public string SourceKey { get; set; } = "";

    /// <summary>Case-folded key of the target project.</summary>
    public string TargetKey { get; set; } = "";

    /// <summary>Edge type from <see cref="Core.Graph.LinkTypes"/>.</summary>
    public string Type { get; set; } = "";

    /// <summary>Display name of the source project.</summary>
    public string Source { get; set; } = "";

    /// <summary>Display name of the target project.</summary>
    public string Target { get; set; } = "";

    /// <summary>One-line note.</summary>
    public string Note { get; set; } = "";

    /// <summary>Unix seconds of the last update.</summary>
    public long Updated { get; set; }
}

/// <summary>Table row of an unconfirmed link suggestion.</summary>
public sealed class PendingLinkRow
{
    /// <summary>Partition tag; empty for the unpartitioned base.</summary>
    public string Workspace { get; set; } = "";

    /// <summary>Case-folded key of the project whose record produced the suggestion.</summary>
    public string ProjectKey { get; set; } = "";

    /// <summary>Case-folded key of the mentioned project.</summary>
    public string OtherKey { get; set; } = "";

    /// <summary>Display name of the proposing project.</summary>
    public string Project { get; set; } = "";

    /// <summary>Display name of the mentioned project.</summary>
    public string Other { get; set; } = "";

    /// <summary>One-line handle of the record that triggered the suggestion.</summary>
    public string Evidence { get; set; } = "";

    /// <summary>0 — awaiting resolution; 1 — rejected, never suggested again.</summary>
    public long Dismissed { get; set; }

    /// <summary>Unix seconds of the last update.</summary>
    public long Updated { get; set; }
}

/// <summary>The relational side of the memory database: projects, edges and pending link suggestions.</summary>
/// <remarks>One context, two providers — the stores above never see a dialect; instants are unix seconds so both map them identically.</remarks>
public sealed class MemoryDbContext(DbContextOptions<MemoryDbContext> options) : DbContext(options)
{
    /// <summary>Registered projects.</summary>
    public DbSet<ProjectRow> Projects => Set<ProjectRow>();

    /// <summary>Directed edges.</summary>
    public DbSet<EdgeRow> Edges => Set<EdgeRow>();

    /// <summary>Unconfirmed link suggestions.</summary>
    public DbSet<PendingLinkRow> PendingLinks => Set<PendingLinkRow>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectRow>(project =>
        {
            project.ToTable("projects");
            project.HasKey(row => new { row.Workspace, row.Key });
        });

        modelBuilder.Entity<EdgeRow>(edge =>
        {
            edge.ToTable("edges");
            edge.HasKey(row => new { row.Workspace, row.SourceKey, row.TargetKey, row.Type });
            edge.HasIndex(row => new { row.Workspace, row.TargetKey });
        });

        modelBuilder.Entity<PendingLinkRow>(pending =>
        {
            pending.ToTable("pending_links");
            pending.HasKey(row => new { row.Workspace, row.ProjectKey, row.OtherKey });
            pending.HasIndex(row => new { row.Workspace, row.OtherKey });
        });
    }
}
