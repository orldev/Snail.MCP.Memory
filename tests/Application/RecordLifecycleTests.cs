using Snail.MCP.Memory.Tests.TestDoubles;

namespace Snail.MCP.Memory.Tests.Application;

/// The record lifecycle end to end over a real database: supersession, duplicate refusal,
/// fallback recall, access marking, review nominations.
public sealed class RecordLifecycleTests : IDisposable
{
    private readonly MemoryHarness _harness = new();
    private readonly RecordLifecycle _service;

    public RecordLifecycleTests()
    {
        _service = new RecordLifecycle(
            _harness.Records, _harness.Projects, _harness.Embedder,
            new KnowledgeGraph(_harness.Graph, _harness.Projects, _harness.Pending, _harness.Time), _harness.Time,
            duplicateThreshold: 0.95, similarityFloor: 0.5);
    }

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Remember_StoresTheRecordAndRegistersTheProjectOnTheFly()
    {
        var answer = await _service.RememberAsync("ProjectA", "the stack is now SQLite", kind: "fact");

        Assert.True(answer.TryGet(out var remembered), answer.Error);
        Assert.Equal(("fact", "ProjectA"), (remembered.Record.Kind, remembered.Record.Project));
        Assert.Null(remembered.Replaced);
        Assert.NotNull(await _harness.Projects.GetAsync("projecta"));
        Assert.Single(await _harness.Records.ListAsync(new RecordFilter("projecta"), 10));
    }

    /// The same knowledge twice is a duplicate: the second remember is refused with the
    /// existing record as the refusal's details, and nothing is written.
    [Fact]
    public async Task Remember_RefusesANearDuplicateAndQuotesTheExistingRecord()
    {
        var first = await _service.RememberAsync("ProjectA", "the very same knowledge");

        var answer = await _service.RememberAsync("ProjectA", "the very same knowledge");

        Assert.Equal(Refusals.Duplicate, answer.Type);
        Assert.StartsWith("Not stored", answer.Error, StringComparison.Ordinal);
        Assert.Contains("replaceId", answer.Hint, StringComparison.Ordinal);
        Assert.True(answer.TryGetDetails<ScoredRecord>(out var duplicate));
        Assert.Equal(first.Value!.Record.Id, duplicate.Record.Id);
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

        _harness.Time.Advance(TimeSpan.FromDays(10));
        var answer = await _service.RememberAsync("ProjectA", "new knowledge", replaceIdRaw: old.Id.ToString());

        Assert.Equal(old.Id, answer.Value!.Replaced);

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

        Refused.As(Refusals.InvalidId, Messages.RecordIdInvalid("not-a-guid"), answer);
    }

    [Fact]
    public async Task Recall_FindsByMeaningAcrossProjectsByDefault()
    {
        await _service.RememberAsync("ProjectA", "a lesson about LiteDB", kind: "lesson");
        await _service.RememberAsync("ProjectB", "another topic");

        var recall = (await _service.RecallAsync("a lesson about LiteDB")).Value!;
        var found = Assert.Single(recall.Records);

        Assert.Equal(("lesson", "ProjectA"), (found.Record.Kind, found.Record.Project));
        Assert.Equal("", recall.Project);
        Assert.Equal(RecallSearch.Meaning, recall.Search);
    }

    /// FakeEmbedder gives the same text the same vector; the query prefix parity between
    /// remember and recall is what this asserts end to end.
    [Fact]
    public async Task Recall_WithProjectFilterSeesOnlyThatProject()
    {
        await _service.RememberAsync("ProjectA", "shared knowledge");
        await _service.RememberAsync("ProjectB", "shared knowledge", force: true);

        var recall = (await _service.RecallAsync("shared knowledge", projectName: "ProjectB")).Value!;

        Assert.All(recall.Records, scored => Assert.Equal("ProjectB", scored.Record.Project));
        Assert.NotEmpty(recall.Records);
    }

    [Fact]
    public async Task Recall_UnknownProjectAnswersNotFound()
    {
        Refused.As(Refusals.NotFound, Messages.ProjectNotFound("Ghost"), await _service.RecallAsync("q", "Ghost"));
    }

    [Fact]
    public async Task Recall_EmptyQueryAnswersTheOverviewCanonicalKindsFirst()
    {
        await _service.RememberAsync("ProjectA", "a custom tag", kind: "idea");
        await _service.RememberAsync("ProjectA", "knowledge", kind: "fact");

        var recall = (await _service.RecallAsync(projectName: "ProjectA")).Value!;

        Assert.Equal(RecallSearch.Overview, recall.Search);
        Assert.Equal(["fact", "idea"], recall.Records.Select(scored => scored.Record.Kind));
    }

    [Fact]
    public async Task Recall_FallsBackToTextSearchWithoutAnEmbedder()
    {
        Assert.Null(await FakeEmbedder.Unavailable.TryEmbedAsync("probe", EmbeddingPurpose.Passage));

        var service = new RecordLifecycle(
            _harness.Records, _harness.Projects, FakeEmbedder.Unavailable,
            new KnowledgeGraph(_harness.Graph, _harness.Projects, _harness.Pending, _harness.Time), _harness.Time, 0.95, 0.5);
        await service.RememberAsync("ProjectA", "Архитектура сервера");

        var recall = (await service.RecallAsync("архитектура")).Value!;

        Assert.Equal(RecallSearch.Text, recall.Search);
        Assert.Equal("Архитектура сервера", Assert.Single(recall.Records).Record.Content);
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

        Assert.Equal(new Forgotten(2), answer.Value);
        Assert.Empty(await _harness.Records.ListAsync(new RecordFilter("projecta"), 10));
    }

    [Fact]
    public async Task Forget_RejectsAMalformedIdWithoutDeletingAnything()
    {
        await _service.RememberAsync("ProjectA", "knowledge");
        var id = (await _harness.Records.ListAsync(new RecordFilter("projecta"), 10))[0].Id;

        var answer = await _service.ForgetAsync($"{id}, oops");

        Refused.As(Refusals.InvalidId, Messages.RecordIdInvalid("oops"), answer);
        Assert.Single(await _harness.Records.ListAsync(new RecordFilter("projecta"), 10));
    }

    /// The lazy purge rides on remember: an expired event disappears and is counted, durable
    /// records of the same age stay.
    [Fact]
    public async Task Remember_PurgesExpiredEventsAndSaysSo()
    {
        await _service.RememberAsync("ProjectA", "something happened", kind: "event");
        await _service.RememberAsync("ProjectA", "an everlasting lesson", kind: "lesson");

        _harness.Time.Advance(TimeSpan.FromDays(100));
        var answer = await _service.RememberAsync("ProjectA", "new knowledge");

        Assert.Equal(1, answer.Value!.PurgedEvents);

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

        Assert.Equal(new Linked("ProjectA", LinkTypes.DependsOn, "ProjectB", IsCreated: true, 0), Assert.Single(answer.Value!.Links).Value);
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

        Assert.Equal(Refusals.Duplicate, answer.Type);
        Assert.Empty(await _harness.Graph.GetAllAsync());
    }

    /// A record that names a known project without an edge queues a pending suggestion; the
    /// suggestion then returns with every project-scoped recall until resolved.
    [Fact]
    public async Task Remember_QueuesAPendingSuggestionForAMentionedUnlinkedProject()
    {
        await _service.RememberAsync("ProjectB", "the other project");

        var answer = await _service.RememberAsync("ProjectA", "shares the parser with ProjectB");

        Assert.Equal("ProjectB", Assert.Single(answer.Value!.Proposed).Other);

        var overview = await _service.RecallAsync(projectName: "ProjectA");
        Assert.Single(overview.Value!.Pending);

        var queried = await _service.RecallAsync("parser", projectName: "ProjectA");
        Assert.Single(queried.Value!.Pending);
    }

    [Fact]
    public async Task Remember_DoesNotReproposeAResolvedOrDismissedPair()
    {
        await _service.RememberAsync("ProjectB", "the other project");
        await _service.RememberAsync("ProjectA", "shares the parser with ProjectB");
        await _harness.Pending.DismissAsync("ProjectA", "ProjectB", _harness.Time.GetUtcNow());

        var answer = await _service.RememberAsync("ProjectA", "again about ProjectB", force: true);

        Assert.Empty(answer.Value!.Proposed);
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

        _harness.Time.Advance(TimeSpan.FromDays(200));
        await _service.RecallAsync("recalled knowledge");

        var review = await _service.ReviewAsync("ProjectA");

        Assert.Equal("forgotten knowledge", Assert.Single(review.Stale).Content);
        Assert.Equal("an old task", Assert.Single(review.Tasks).Content);
        Assert.Equal(3, (await _harness.Records.ListAsync(new RecordFilter("projecta"), 10)).Count);
    }
}
