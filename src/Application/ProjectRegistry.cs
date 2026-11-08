using Snail.MCP.Memory.Core.Constants;
using Snail.Sdk.Mcp.Hosting.Answers;

namespace Snail.MCP.Memory.Application;

/// <summary>Project registry operations: init with the experience briefing, listing, renaming.</summary>
/// <remarks>Init embeds the description and asks the whole memory "where have I seen this before?"; <c>seedFrom</c> pulls named donors instead.</remarks>
public sealed class ProjectRegistry(
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

    /// <summary>Registers a project and answers whether it is new, the experience briefing and its open link suggestions.</summary>
    public async Task<Answer<ProjectInit>> InitAsync(
        string projectName, string description = "", string seedFrom = "", CancellationToken cancellationToken = default)
    {
        var project = ProjectIdentity.Normalize(projectName);

        if (project.Length == 0)
            return Answer.Failed(Messages.ProjectNameEmpty, Messages.ProjectNameEmptyHint, Refusals.EmptyName);

        var created = await projects.TryCreateAsync(
            new ProjectInfo(project, description.Trim(), timeProvider.GetUtcNow()), cancellationToken);
        var registered = created
            ? project
            : (await projects.GetAsync(ProjectIdentity.Key(project), cancellationToken))?.Name ?? project;
        var briefing = await BriefingAsync(project, description, seedFrom, cancellationToken);
        var queue = await pending.ListAsync(ProjectIdentity.Key(project), cancellationToken);

        return Answer.Ok(new ProjectInit(registered, created, briefing, queue));
    }

    /// <summary>The project listing, filtered by prefix and ordered case-insensitively.</summary>
    public async Task<ProjectListing> ListAsync(string prefix = "", CancellationToken cancellationToken = default)
    {
        var all = await projects.ListAsync(cancellationToken);

        var listed = all
            .Where(project => ProjectIdentity.StartsWith(project.Name, prefix))
            .OrderBy(project => project.Name, ProjectIdentity.Comparer)
            .ToList();

        return new ProjectListing(prefix.Trim(), listed);
    }

    /// <summary>Renames a project everywhere: the registry, every record, every edge.</summary>
    public async Task<Answer<Renamed>> RenameAsync(string oldName, string newName, CancellationToken cancellationToken = default)
    {
        var oldProject = ProjectIdentity.Normalize(oldName);
        var newProject = ProjectIdentity.Normalize(newName);

        if (oldProject.Length == 0 || newProject.Length == 0)
            return Answer.Failed(Messages.ProjectNameEmpty, Messages.ProjectNameEmptyHint, Refusals.EmptyName);

        var oldKey = ProjectIdentity.Key(oldProject);
        var newKey = ProjectIdentity.Key(newProject);

        if (await projects.GetAsync(oldKey, cancellationToken) is null)
            return Answer.Failed(Messages.ProjectNotFound(oldProject), Messages.ProjectNotFoundHint, Refusals.NotFound);

        if (oldKey != newKey && await projects.GetAsync(newKey, cancellationToken) is { } taken)
            return Answer.Failed(Messages.RenameTargetTaken(taken.Name), Messages.RenameTargetTakenHint, Refusals.NameTaken);

        await projects.TryRenameAsync(oldKey, newProject, cancellationToken);
        var movedRecords = await records.RenameProjectAsync(oldKey, newProject, cancellationToken);
        var movedEdges = await graph.RenameProjectAsync(oldKey, newProject, cancellationToken);
        await pending.RenameProjectAsync(oldKey, newProject, cancellationToken);

        return Answer.Ok(new Renamed(oldProject, newProject, movedRecords, movedEdges));
    }

    private async Task<Briefing> BriefingAsync(
        string project, string description, string seedFrom, CancellationToken cancellationToken)
    {
        var projectKey = ProjectIdentity.Key(project);
        var missing = new List<string>();
        var experience = new List<ScoredRecord>();
        var similar = new List<SimilarProject>();

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
                .Select(group => new SimilarProject(group.Key, group.Max(scored => scored.Similarity ?? 0)))
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
                missing.Add(seed);
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

        var wasAsked = !string.IsNullOrWhiteSpace(description) || Seeds(seedFrom).Any();

        return new Briefing(wasAsked, similar, distinct, missing);
    }

    private static IEnumerable<string> Seeds(string seedFrom) =>
        seedFrom.Split(['+', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
