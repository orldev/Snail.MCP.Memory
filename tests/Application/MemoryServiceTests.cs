using Snail.MCP.Memory.Tests.TestDoubles;

namespace Snail.MCP.Memory.Tests.Application;

/// The record lifecycle end to end over a real database: supersession, duplicate refusal,
/// fallback recall, access marking, review nominations.
public sealed class MemoryServiceTests : IDisposable
{
    private readonly MemoryHarness _harness = new();
    private readonly MemoryService _service;

    public MemoryServiceTests()
    {
        _service = new MemoryService(
            _harness.Records, _harness.Projects, _harness.Embedder,
            new GraphService(_harness.Graph, _harness.Projects, _harness.Pending, _harness.Time), _harness.Time,
            duplicateThreshold: 0.95, similarityFloor: 0.5);
    }

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Remember_StoresTheRecordAndRegistersTheProjectOnTheFly()
    {
        var answer = await _service.RememberAsync("ProjectA", "the stack is now SQLite", kind: "fact");

        Assert.StartsWith("Recorded [fact] in project 'ProjectA'", answer);
        Assert.NotNull(await _harness.Projects.GetAsync("projecta"));
        Assert.Single(await _harness.Records.ListAsync(new RecordFilter("projecta"), 10));
    }

    /// The same knowledge twice is a duplicate: the second remember is refused with the
    /// existing record quoted, and nothing is written.
    [Fact]
    public async Task Remember_RefusesANearDuplicateAndQuotesTheExistingRecord()
    {
        await _service.RememberAsync("ProjectA", "the very same knowledge");

        var answer = await _service.RememberAsync("ProjectA", "the very same knowledge");

        Assert.Contains("Not stored", answer);
        Assert.Contains("replaceId", answer);
        Assert.Single(await _harness.Records.ListAsync(new RecordFilter("projecta"), 10));
    }

    [Fact]
    public async Task Remember_ForceStoresDespiteTheDuplicate()
    {
        await _service.RememberAsync("ProjectA", "the very same knowledge");
        await _service.RememberAsync("ProjectA", "the very same knowledge", force: true);

        Assert.Equal(2, (await _harness.Records.ListAsync(new RecordFilter("projecta"), 10)).Count);
    }

    /// Supersession replaces content but keeps the creation date: the record's age is the
    /// knowledge's age, not the last rewording's.
    [Fact]
    public async Task Remember_ReplaceSupersedesTheOldRecordKeepingItsCreationDate()
    {
        await _service.RememberAsync("ProjectA", "old knowledge");
        var old = Assert.Single(await _harness.Records.ListAsync(new RecordFilter("projecta"), 10));

        _harness.Time.Now = _harness.Time.Now.AddDays(10);
        var answer = await _service.RememberAsync("ProjectA", "new knowledge", replaceIdRaw: old.Id.ToString());

        Assert.Contains("superseded", answer);

        var current = Assert.Single(await _harness.Records.ListAsync(new RecordFilter("projecta"), 10));

        Assert.Equal("new knowledge", current.Content);
        Assert.NotEqual(old.Id, current.Id);
        Assert.Equal(old.Created, current.Created);
        Assert.NotEqual(old.Updated, current.Updated);
    }

    [Fact]
    public async Task Remember_RejectsAMalformedReplaceId()
    {
        var answer = await _service.RememberAsync("ProjectA", "knowledge", replaceIdRaw: "not-a-guid");

        Assert.Equal(Messages.RecordIdInvalid("not-a-guid"), answer);
    }

    [Fact]
    public async Task Recall_FindsByMeaningAcrossProjectsByDefault()
    {
        await _service.RememberAsync("ProjectA", "a lesson about LiteDB", kind: "lesson");
        await _service.RememberAsync("ProjectB", "another topic");

        var answer = await _service.RecallAsync("a lesson about LiteDB");

        Assert.Contains("[lesson]", answer);
        Assert.Contains("ProjectA", answer);
        Assert.Contains(Messages.ScopeWholeMemory, answer);
    }

    /// FakeEmbedder gives the same text the same vector; the query prefix parity between
    /// remember and recall is what this asserts end to end.
    [Fact]
    public async Task Recall_WithProjectFilterSeesOnlyThatProject()
    {
        await _service.RememberAsync("ProjectA", "shared knowledge");
        await _service.RememberAsync("ProjectB", "shared knowledge", force: true);

        var answer = await _service.RecallAsync("shared knowledge", projectName: "ProjectB");

        Assert.Contains("ProjectB", answer);
        Assert.DoesNotContain("— ProjectA", answer);
    }

    [Fact]
    public async Task Recall_UnknownProjectAnswersNotFound()
    {
        Assert.Equal(Messages.ProjectNotFound("Ghost"), await _service.RecallAsync("q", "Ghost"));
    }

    [Fact]
    public async Task Recall_EmptyQueryRendersTheOverview()
    {
        await _service.RememberAsync("ProjectA", "knowledge", kind: "fact");

        var answer = await _service.RecallAsync(projectName: "ProjectA");

        Assert.StartsWith("# 📖 Overview: ProjectA", answer);
        Assert.Contains("[fact]", answer);
    }

    [Fact]
    public async Task Recall_FallsBackToTextSearchWithoutAnEmbedder()
    {
        Assert.Null(await FakeEmbedder.Unavailable.TryEmbedAsync("probe", EmbeddingPurpose.Passage));

        var service = new MemoryService(
            _harness.Records, _harness.Projects, FakeEmbedder.Unavailable,
            new GraphService(_harness.Graph, _harness.Projects, _harness.Pending, _harness.Time), _harness.Time, 0.95, 0.5);
        await service.RememberAsync("ProjectA", "Архитектура сервера");

        var answer = await service.RecallAsync("архитектура");

        Assert.Contains(Messages.TextFallbackNote, answer);
        Assert.Contains("Архитектура сервера", answer);
    }

    [Fact]
    public async Task Recall_MarksReturnedRecordsAsAccessed()
    {
        await _service.RememberAsync("ProjectA", "knowledge");
        await _service.RecallAsync("knowledge");

        var record = Assert.Single(await _harness.Records.ListAsync(new RecordFilter("projecta"), 10));

        Assert.Equal(1, record.AccessCount);
        Assert.NotNull(record.LastAccessed);
    }

    [Fact]
    public async Task Forget_DeletesSeveralCommaSeparatedIds()
    {
        await _service.RememberAsync("ProjectA", "the first");
        await _service.RememberAsync("ProjectA", "the second");
        var ids = (await _harness.Records.ListAsync(new RecordFilter("projecta"), 10)).Select(r => r.Id).ToList();

        var answer = await _service.ForgetAsync(string.Join(", ", ids));

        Assert.Equal(Messages.RecordsForgotten(2), answer);
        Assert.Empty(await _harness.Records.ListAsync(new RecordFilter("projecta"), 10));
    }

    [Fact]
    public async Task Forget_RejectsAMalformedIdWithoutDeletingAnything()
    {
        await _service.RememberAsync("ProjectA", "knowledge");
        var id = (await _harness.Records.ListAsync(new RecordFilter("projecta"), 10))[0].Id;

        var answer = await _service.ForgetAsync($"{id}, oops");

        Assert.Equal(Messages.RecordIdInvalid("oops"), answer);
        Assert.Single(await _harness.Records.ListAsync(new RecordFilter("projecta"), 10));
    }

    /// The lazy purge rides on remember: an expired event disappears with a notice, durable
    /// records of the same age stay.
    [Fact]
    public async Task Remember_PurgesExpiredEventsAndSaysSo()
    {
        await _service.RememberAsync("ProjectA", "something happened", kind: "event");
        await _service.RememberAsync("ProjectA", "an everlasting lesson", kind: "lesson");

        _harness.Time.Now = _harness.Time.Now.AddDays(100);
        var answer = await _service.RememberAsync("ProjectA", "new knowledge");

        Assert.Contains(Messages.PurgeNotice(1).Trim(), answer);

        var kinds = (await _harness.Records.ListAsync(new RecordFilter("projecta"), 10)).Select(r => r.Kind);

        Assert.DoesNotContain("event", kinds);
        Assert.Contains("lesson", kinds);
    }

    /// links makes the record and its edges one call — the two-step "stored the decision,
    /// forgot link_projects" mistake stops existing.
    [Fact]
    public async Task Remember_WithLinksStoresTheRecordAndTheEdgesAtomically()
    {
        await _service.RememberAsync("ProjectB", "the dependency");

        var answer = await _service.RememberAsync("ProjectA", "we build on it",
            links: [new RecordLink("ProjectB", LinkType.DependsOn, "runtime")]);

        Assert.Contains(Messages.LinkCreated("ProjectA", LinkTypes.DependsOn, "ProjectB"), answer);
        Assert.Single(await _harness.Graph.GetAllAsync());
        Assert.Empty(await _harness.Pending.ListAsync());
    }

    /// A refused duplicate stores nothing — including the requested edges.
    [Fact]
    public async Task Remember_DuplicateRefusalCreatesNoLinks()
    {
        await _service.RememberAsync("ProjectB", "other knowledge");
        await _service.RememberAsync("ProjectA", "the very same knowledge");

        var answer = await _service.RememberAsync("ProjectA", "the very same knowledge",
            links: [new RecordLink("ProjectB", LinkType.Uses)]);

        Assert.Contains("Not stored", answer);
        Assert.Empty(await _harness.Graph.GetAllAsync());
    }

    /// A record that names a known project without an edge queues a pending suggestion; the
    /// suggestion then returns with every project-scoped recall until resolved.
    [Fact]
    public async Task Remember_QueuesAPendingSuggestionForAMentionedUnlinkedProject()
    {
        await _service.RememberAsync("ProjectB", "the other project");

        var answer = await _service.RememberAsync("ProjectA", "shares the parser with ProjectB");

        Assert.Contains(Messages.PendingTitle, answer);
        Assert.Contains("ProjectB", answer);

        var overview = await _service.RecallAsync(projectName: "ProjectA");
        Assert.Contains(Messages.PendingTitle, overview);

        var queried = await _service.RecallAsync("parser", projectName: "ProjectA");
        Assert.Contains(Messages.PendingTitle, queried);
    }

    [Fact]
    public async Task Remember_DoesNotReproposeAResolvedOrDismissedPair()
    {
        await _service.RememberAsync("ProjectB", "the other project");
        await _service.RememberAsync("ProjectA", "shares the parser with ProjectB");
        await _harness.Pending.DismissAsync("ProjectA", "ProjectB", _harness.Time.GetUtcNow());

        var answer = await _service.RememberAsync("ProjectA", "again about ProjectB", force: true);

        Assert.DoesNotContain(Messages.PendingTitle, answer);
        Assert.Empty(await _harness.Pending.ListAsync());
    }

    /// Review nominates but never deletes: durable records that recall has never returned and
    /// that grew old, plus long-untouched tasks.
    [Fact]
    public async Task Review_NominatesStaleDurableRecordsAndStaleTasks()
    {
        await _service.RememberAsync("ProjectA", "forgotten knowledge", kind: "fact");
        await _service.RememberAsync("ProjectA", "an old task", kind: "task");
        await _service.RememberAsync("ProjectA", "recalled knowledge", kind: "fact", force: true);

        _harness.Time.Now = _harness.Time.Now.AddDays(200);
        await _service.RecallAsync("recalled knowledge");

        var answer = await _service.ReviewAsync("ProjectA");

        Assert.Contains("forgotten knowledge", answer);
        Assert.Contains("an old task", answer);
        Assert.DoesNotContain("recalled knowledge", answer);
        Assert.Equal(3, (await _harness.Records.ListAsync(new RecordFilter("projecta"), 10)).Count);
    }
}
