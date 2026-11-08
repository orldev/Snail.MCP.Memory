namespace Snail.MCP.Memory.Core.Answers;

/// <summary>Deletion candidates: nominations for the user to confirm, never decisions.</summary>
/// <param name="Project">The project reviewed; empty for the whole memory.</param>
/// <param name="StaleAfterDays">The age after which a record counts as stale.</param>
/// <param name="Stale">Durable records recall never returned that grew older than that.</param>
/// <param name="Tasks">Tasks untouched for as long.</param>
public sealed record Review(string Project, int StaleAfterDays, IReadOnlyList<MemoryRecord> Stale, IReadOnlyList<MemoryRecord> Tasks);
