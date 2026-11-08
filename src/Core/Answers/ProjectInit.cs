namespace Snail.MCP.Memory.Core.Answers;

/// <summary>What <c>init_project_memory</c> did: the project, whether its memory is new, the briefing, the open suggestions.</summary>
/// <param name="Project">The project's display name as the registry holds it.</param>
/// <param name="IsCreated">Whether this call created the memory; false when it existed already.</param>
/// <param name="Briefing">The experience of related projects.</param>
/// <param name="Pending">The project's link suggestions still waiting for a decision.</param>
public sealed record ProjectInit(string Project, bool IsCreated, Briefing Briefing, IReadOnlyList<PendingLink> Pending);
