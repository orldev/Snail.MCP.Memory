using Snail.Sdk.Mcp.Hosting.Answers;

namespace Snail.MCP.Memory.Core.Answers;

/// <summary>A stored record and what came with it.</summary>
/// <param name="Record">The record as stored.</param>
/// <param name="Replaced">The id of the record it superseded, which is deleted; null when it superseded none.</param>
/// <param name="Links">Each edge asked for in <c>links</c>, made or refused; the record is stored either way.</param>
/// <param name="Proposed">Link suggestions the record's mentions queued.</param>
/// <param name="PurgedEvents">Expired <c>event</c> records the write purged.</param>
public sealed record Remembered(
    MemoryRecord Record,
    Guid? Replaced,
    IReadOnlyList<Answer<Linked>> Links,
    IReadOnlyList<PendingLink> Proposed,
    int PurgedEvents);
