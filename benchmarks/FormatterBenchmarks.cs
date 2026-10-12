using BenchmarkDotNet.Attributes;
using Snail.MCP.Memory.Core.Formatting;
using Snail.MCP.Memory.Core.Graph;
using Snail.MCP.Memory.Core.Records;

namespace Snail.MCP.Memory.Benchmarks;

/// <summary>The answers a recall, an overview and a graph call build: the Markdown every read tool returns.</summary>
[MemoryDiagnoser]
public class FormatterBenchmarks
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private ScoredRecord[] _scored = [];

    private MemoryRecord[] _records = [];

    private GraphEdge[] _edges = [];

    /// <summary>Twenty scored records for a recall, sixty for an overview across every kind, eighty edges among thirty projects.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _records = [.. Enumerable.Range(0, 60).Select(Record)];
        _scored = [.. _records.Take(20).Select((record, index) => new ScoredRecord(record, 0.9 - index * 0.01))];
        _edges = [.. Enumerable.Range(0, 80).Select(Edge)];
    }

    [Benchmark]
    public string Recall() => MemoryFormatter.RenderRecall("how is the store configured", "project 'Snail.MCP.Memory'", "any", _scored, usedTextFallback: false);

    [Benchmark]
    public string Overview() => MemoryFormatter.RenderOverview("Snail.MCP.Memory", "any", _records);

    [Benchmark]
    public string Graph() => MemoryFormatter.RenderGraph("## Knowledge graph", _edges);

    private static MemoryRecord Record(int index) => new()
    {
        Id = new Guid(index, 0, 0, new byte[8]),
        Project = "Snail.MCP.Memory",
        Kind = RecordKinds.Canonical[index % RecordKinds.Canonical.Count],
        Title = index % 3 == 0 ? "" : $"Record number {index}",
        Content = $"The store keeps vectors in SQLite through sqlite-vec; record {index} explains why the embedder runs in process and what the fallback does when no model was downloaded.\n",
        Created = Start.AddDays(index),
        Updated = Start.AddDays(index * 2),
    };

    private static GraphEdge Edge(int index) =>
        new($"Project{index % 30}", $"Project{(index * 7 + 3) % 30}", LinkTypes.All[index % LinkTypes.All.Count], index % 4 == 0 ? "" : "named in | the manifest", Start.AddHours(index));
}
