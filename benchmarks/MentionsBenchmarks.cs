using BenchmarkDotNet.Attributes;
using Snail.MCP.Memory.Core.Graph;

namespace Snail.MCP.Memory.Benchmarks;

/// <summary>The literal scan every remember runs: the record's text against the name of every known project.</summary>
[MemoryDiagnoser]
public class MentionsBenchmarks
{
    private const string Text =
        "Decided to keep the vector store in SQLite for the local server and PostgreSQL with pgvector for the shared one. " +
        "Snail.MCP.Memory.Tests exercise both through the same stores; the CodeContext index and the Roslyn server are " +
        "unrelated. The audit worker proposes links when a record quotes Snail.MCP.Plans or snail.toolkit.web by name. " +
        "Other notes: dotnet-tools, the launchd job, a model downloaded once into the data directory, and nothing else.";

    private static readonly string[] Projects =
    [
        .. Enumerable.Range(0, 36).Select(index => $"Snail.Toolkit.Library{index}"),
        "Snail.MCP.Memory", "Snail.MCP.Plans", "Snail.Toolkit.Web", "CodeContext",
    ];

    [Benchmark]
    public int AllProjects()
    {
        var mentioned = 0;

        foreach (var project in Projects)
        {
            if (ProjectMentions.Mentions(Text, project))
                mentioned++;
        }

        return mentioned;
    }
}
