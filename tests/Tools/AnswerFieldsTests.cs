using Snail.MCP.Memory.Tests.TestDoubles;
using Snail.MCP.Memory.Tools;

namespace Snail.MCP.Memory.Tests.Tools;

/// The fields of data are what a model and the mem0 commands read: a record carries its id in full, leaves out what it
/// does not hold, and an answer that found nothing says which nothing it is.
public sealed class AnswerFieldsTests : IDisposable
{
    private readonly MemoryHarness _harness = new();
    private readonly RecordLifecycle _memory;
    private readonly ProjectRegistry _registry;

    public AnswerFieldsTests()
    {
        _memory = new RecordLifecycle(
            _harness.Records, _harness.Projects, _harness.Embedder,
            new KnowledgeGraph(_harness.Graph, _harness.Projects, _harness.Pending, _harness.Time), _harness.Time,
            duplicateThreshold: 0.95, similarityFloor: 0.5);
        _registry = new ProjectRegistry(
            _harness.Projects, _harness.Records, _harness.Graph, _harness.Pending, _harness.Embedder, _harness.Time);
    }

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void Record_WithoutATitle_LeavesTheTitleOutAndRoundsTheSimilarity()
    {
        var record = new MemoryRecord
        {
            Id = Guid.Parse("4abbb314-08df-42f3-b3c5-e1ac580aa601"),
            Project = "ProjectA",
            Kind = "fact",
            Content = " the stack is .NET \n",
            Updated = DateTimeOffset.Parse("2026-11-08T23:30:00Z"),
        };

        var fields = AnswerFields.Of(new ScoredRecord(record, 0.87654));

        Assert.Equal(
            """{"id":"4abbb314-08df-42f3-b3c5-e1ac580aa601","kind":"fact","project":"ProjectA","content":"the stack is .NET","updated":"2026-11-08","similarity":0.88}""",
            fields.ToJsonString());
    }

    [Fact]
    public async Task Recall_FindingNothing_CarriesANote()
    {
        var answer = await ProjectMemoryTools.RecallAsync(_memory, CancellationToken.None, query: "anything");

        Assert.True(answer.IsOk(), answer.Text());
        Assert.Empty(answer.Data()!["records"]!.AsArray());
        Assert.Equal(Messages.NoRecords, answer.Data()!["note"]!.GetValue<string>());
    }

    /// The refusal names the record that stood in the way, so the model can pass its id as replaceId without a recall.
    [Fact]
    public async Task Remember_ANearDuplicate_IsRefusedWithTheExistingRecordAsData()
    {
        var first = await ProjectMemoryTools.RememberAsync(_memory, CancellationToken.None, "ProjectA", "the very same knowledge");
        var second = await ProjectMemoryTools.RememberAsync(_memory, CancellationToken.None, "ProjectA", "the very same knowledge");

        Assert.True(second.IsError);
        Assert.Equal(Refusals.Duplicate, second.Type());
        Assert.Equal(first.Data()!["id"]!.GetValue<string>(), second.Data()!["id"]!.GetValue<string>());
        Assert.Equal("the very same knowledge", second.Data()!["content"]!.GetValue<string>());
    }

    [Fact]
    public async Task ListProjects_OfAnEmptyMemory_SaysHowToStart()
    {
        var answer = await ProjectMemoryTools.ListProjectsAsync(_registry, CancellationToken.None);

        Assert.Equal(0, answer.Data()!["count"]!.GetValue<int>());
        Assert.Equal(Messages.ProjectListEmpty, answer.Data()!["note"]!.GetValue<string>());
    }

    /// An edge refused inside remember does not fail the record, which is stored: the link says why it was not made.
    [Fact]
    public async Task Remember_ALinkToAProjectWithoutMemory_StoresTheRecordAndNamesTheRefusal()
    {
        var answer = await ProjectMemoryTools.RememberAsync(
            _memory, CancellationToken.None, "ProjectA", "we build on it", links: [new RecordLink("Ghost", LinkType.Uses)]);

        var link = answer.Data()!["links"]![0]!;

        Assert.True(answer.IsOk(), answer.Text());
        Assert.Equal(Refusals.NotFound, link["type"]!.GetValue<string>());
        Assert.Equal(Messages.ProjectNotFound("Ghost"), link["error"]!.GetValue<string>());
    }
}
