using Snail.MCP.Memory.Tests.TestDoubles;

namespace Snail.MCP.Memory.Tests.Application;

/// The audit backfills the pending queue from records the write-time scan could not cover —
/// mentions that predate the mentioned project — and repeats without duplicating.
public sealed class MentionAuditTests : IDisposable
{
    private readonly MemoryHarness _harness = new();
    private readonly RecordLifecycle _memory;
    private readonly MentionAudit _audit;

    public MentionAuditTests()
    {
        var graph = new KnowledgeGraph(_harness.Graph, _harness.Projects, _harness.Pending, _harness.Time);
        _memory = new RecordLifecycle(_harness.Records, _harness.Projects, _harness.Embedder, graph, _harness.Time, 0.95, 0.5);
        _audit = new MentionAudit(_harness.Records, _harness.Projects, _harness.Graph, _harness.Pending, _harness.Time);
    }

    public void Dispose() => _harness.Dispose();

    /// The record naming ProjectB was written while ProjectB had no memory, so the write-time
    /// scan saw nothing; the audit is what catches up.
    [Fact]
    public async Task Run_QueuesAMentionThatPredatesTheMentionedProject()
    {
        await _memory.RememberAsync("ProjectA", "shares the parser with ProjectB");
        Assert.Empty(await _harness.Pending.ListAsync());

        await _memory.RememberAsync("ProjectB", "the other project");

        Assert.Equal(1, await _audit.RunAsync());

        var link = Assert.Single(await _harness.Pending.ListAsync());

        Assert.Equal("ProjectA", link.Project);
        Assert.Equal("ProjectB", link.Other);
    }

    [Fact]
    public async Task Run_SkipsLinkedPairsAndRepeatsWithoutDuplicating()
    {
        await _memory.RememberAsync("ProjectA", "built on ProjectB and ProjectC");
        await _memory.RememberAsync("ProjectB", "the linked one");
        await _memory.RememberAsync("ProjectC", "the unlinked one");
        await _harness.Graph.TryInsertAsync(
            new GraphEdge("ProjectA", "ProjectB", LinkTypes.DependsOn, "", _harness.Time.GetUtcNow()));

        Assert.Equal(1, await _audit.RunAsync());
        Assert.Equal("ProjectC", Assert.Single(await _harness.Pending.ListAsync()).Other);
        Assert.Equal(0, await _audit.RunAsync());
    }

    [Fact]
    public async Task Run_RespectsADismissedPair()
    {
        await _memory.RememberAsync("ProjectA", "namesake of ProjectB");
        await _memory.RememberAsync("ProjectB", "the other project");
        await _harness.Pending.DismissAsync("ProjectA", "ProjectB", _harness.Time.GetUtcNow());

        Assert.Equal(0, await _audit.RunAsync());
        Assert.Empty(await _harness.Pending.ListAsync());
    }
}
