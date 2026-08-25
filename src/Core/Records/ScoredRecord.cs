namespace Snail.MCP.Memory.Core.Records;

/// <summary>A record together with the similarity of the query that found it.</summary>
/// <param name="Similarity">Cosine similarity in <c>[0..1]</c>; <c>null</c> when the record came from the text fallback.</param>
public readonly record struct ScoredRecord(MemoryRecord Record, double? Similarity);
