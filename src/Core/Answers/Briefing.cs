namespace Snail.MCP.Memory.Core.Answers;

/// <summary>The experience a new project starts with: similar projects and the decisions and lessons worth knowing.</summary>
/// <param name="WasAsked">Whether a description or seed projects were given; without either nothing is looked for.</param>
/// <param name="SimilarProjects">Projects whose records are nearest to the description, nearest first.</param>
/// <param name="Experience">Transferable records of similar and seed projects, each once.</param>
/// <param name="MissingSeeds">Seed projects named in <c>seedFrom</c> that have no memory.</param>
public sealed record Briefing(
    bool WasAsked,
    IReadOnlyList<SimilarProject> SimilarProjects,
    IReadOnlyList<ScoredRecord> Experience,
    IReadOnlyList<string> MissingSeeds);
