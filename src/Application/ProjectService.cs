using Snail.MCP.Memory.Core.Constants;
using Snail.MCP.Memory.Core.Formatting;

namespace Snail.MCP.Memory.Application;

/// <summary>Project registry operations: init with the experience briefing, listing, renaming.</summary>
/// <remarks>Init embeds the description and asks the whole memory "where have I seen this before?"; <c>seedFrom</c> pulls named donors instead.</remarks>
public sealed class ProjectService(
    IProjectStore projects,
    IRecordStore records,
    IGraphStore graph,
    IPendingLinkStore pending,
    IEmbedder embedder,
    TimeProvider timeProvider,
    double similarityFloor = 0)
{
    /// <summary>How many similar projects a briefing names.</summary>
    internal const int SimilarProjectLimit = 3;

    /// <summary>How many transferable records a briefing quotes.</summary>
    internal const int ExperienceLimit = 6;

    /// <summary>How many records are pulled from each seed project.</summary>
    internal const int SeedLimitPerProject = 5;

    /// <summary>Registers a project and returns the creation message plus the experience briefing.</summary>
    public async Task<string> InitAsync(
        string projectName, string description = "", string seedFrom = "", CancellationToken cancellationToken = default)
    {
        var project = ProjectIdentity.Normalize(projectName);

        if (project.Length == 0)
            return Messages.ProjectNameEmpty;

        var created = await projects.CreateAsync(
            new ProjectInfo(project, description.Trim(), timeProvider.GetUtcNow()), cancellationToken);

        var headline = created
            ? Messages.ProjectCreated(project)
            : Messages.ProjectAlreadyExists(
                (await projects.GetAsync(ProjectIdentity.Key(project), cancellationToken))?.Name ?? project);
        var briefing = await BriefingAsync(project, description, seedFrom, cancellationToken);
        var queue = await pending.ListAsync(ProjectIdentity.Key(project), cancellationToken);

        return queue.Count == 0
            ? $"{headline}\n\n{briefing}"
            : $"{headline}\n\n{briefing}\n\n{MemoryFormatter.RenderPending(queue)}";
    }

    /// <summary>The project listing, filtered by prefix and ordered case-insensitively.</summary>
    public async Task<string> ListAsync(string prefix = "", CancellationToken cancellationToken = default)
    {
        var all = await projects.ListAsync(cancellationToken);

        var listed = all
            .Where(project => ProjectIdentity.StartsWith(project.Name, prefix))
            .OrderBy(project => project.Name, ProjectIdentity.Comparer)
            .ToList();

        return MemoryFormatter.RenderProjectList(listed, prefix);
    }

    /// <summary>Renames a project everywhere: the registry, every record, every edge.</summary>
    public async Task<string> RenameAsync(string oldName, string newName, CancellationToken cancellationToken = default)
    {
        var oldProject = ProjectIdentity.Normalize(oldName);
        var newProject = ProjectIdentity.Normalize(newName);

        if (oldProject.Length == 0 || newProject.Length == 0)
            return Messages.ProjectNameEmpty;

        var oldKey = ProjectIdentity.Key(oldProject);
        var newKey = ProjectIdentity.Key(newProject);

        if (await projects.GetAsync(oldKey, cancellationToken) is null)
            return Messages.ProjectNotFound(oldProject);

        if (oldKey != newKey && await projects.GetAsync(newKey, cancellationToken) is { } taken)
            return Messages.RenameTargetTaken(taken.Name);

        await projects.RenameAsync(oldKey, newProject, cancellationToken);
        var movedRecords = await records.RenameProjectAsync(oldKey, newProject, cancellationToken);
        var movedEdges = await graph.RenameProjectAsync(oldKey, newProject, cancellationToken);
        await pending.RenameProjectAsync(oldKey, newProject, cancellationToken);

        return Messages.ProjectRenamed(oldProject, newProject, movedRecords, movedEdges);
    }

    private async Task<string> BriefingAsync(
        string project, string description, string seedFrom, CancellationToken cancellationToken)
    {
        var projectKey = ProjectIdentity.Key(project);
        var notes = new List<string>();
        var experience = new List<ScoredRecord>();
        var similar = new List<(string Project, double Similarity)>();

        if (!string.IsNullOrWhiteSpace(description) &&
            await embedder.TryEmbedAsync(description.Trim(), EmbeddingPurpose.Query, cancellationToken) is { } vector)
        {
            var found = await records.SearchAsync(vector, new RecordFilter(), SimilarProjectLimit * 8, cancellationToken);

            var foreign = found
                .Where(scored => scored.Similarity >= similarityFloor)
                .Where(scored => ProjectIdentity.Key(scored.Record.Project) != projectKey)
                .ToList();

            similar = [.. foreign
                .GroupBy(scored => scored.Record.Project, ProjectIdentity.Comparer)
                .Select(group => (Project: group.Key, Similarity: group.Max(scored => scored.Similarity ?? 0)))
                .OrderByDescending(candidate => candidate.Similarity)
                .Take(SimilarProjectLimit)];

            experience.AddRange(foreign
                .Where(scored => RecordKinds.Transferable.Contains(scored.Record.Kind))
                .Take(ExperienceLimit));
        }

        foreach (var seed in Seeds(seedFrom))
        {
            if (ProjectIdentity.Key(seed) == projectKey)
                continue;

            if (await projects.GetAsync(ProjectIdentity.Key(seed), cancellationToken) is null)
            {
                notes.Add(Messages.SeedProjectMissing(seed));
                continue;
            }

            var seeded = await records.ListAsync(new RecordFilter(ProjectIdentity.Key(seed)), 100, cancellationToken);

            experience.AddRange(seeded
                .Where(record => RecordKinds.Transferable.Contains(record.Kind))
                .Take(SeedLimitPerProject)
                .Select(record => new ScoredRecord(record, null)));
        }

        var distinct = experience
            .GroupBy(scored => scored.Record.Id)
            .Select(group => group.First())
            .ToList();

        return MemoryFormatter.RenderBriefing(similar, distinct, notes);
    }

    private static IEnumerable<string> Seeds(string seedFrom) =>
        seedFrom.Split(['+', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
