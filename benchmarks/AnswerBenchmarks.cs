using BenchmarkDotNet.Attributes;
using ModelContextProtocol.Protocol;
using Snail.MCP.Memory.Core.Answers;
using Snail.MCP.Memory.Core.Graph;
using Snail.MCP.Memory.Core.Records;
using Snail.MCP.Memory.Tools;
using Snail.Sdk.Mcp.Hosting.Answers;

namespace Snail.MCP.Memory.Benchmarks;

/// <summary>The answers a recall, an overview and a graph call build: the fields of <see cref="AnswerFields"/> written as the envelope.</summary>
[MemoryDiagnoser]
public class AnswerBenchmarks
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
    public CallToolResult Recall() =>
        ToolAnswer.Ok(AnswerFields.Of(new Recall("Snail.MCP.Memory", "how is the store configured", "", RecallSearch.Meaning, _scored, [])));

    [Benchmark]
    public CallToolResult Overview() =>
        ToolAnswer.Ok(AnswerFields.Of(new Recall("Snail.MCP.Memory", "", "", RecallSearch.Overview, [.. _records.Select(record => new ScoredRecord(record, null))], [])));

    [Benchmark]
    public CallToolResult Graph() => ToolAnswer.Ok(AnswerFields.Of(new GraphView([], 0, _edges)));

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
