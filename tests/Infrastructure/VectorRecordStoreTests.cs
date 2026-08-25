using Snail.MCP.Memory.Tests.TestDoubles;

namespace Snail.MCP.Memory.Tests.Infrastructure;

/// The record store against a real sqlite-vec database file: mapping, filters, vector search,
/// the zero-vector guard, the workspace partition and the lifecycle operations.
public sealed class VectorRecordStoreTests : IDisposable
{
    private const int Dimensions = 16;

    private readonly MemoryHarness _harness = new(Dimensions);
    private readonly IRecordStore _store;

    public VectorRecordStoreTests() => _store = _harness.Records;

    public void Dispose() => _harness.Dispose();

    private static MemoryRecord Record(string project, string kind, string content, string title = "") => new()
    {
        Id = Guid.NewGuid(),
        Project = project,
        Kind = kind,
        Title = title,
        Content = content,
        Created = DateTimeOffset.UtcNow,
        Updated = DateTimeOffset.UtcNow,
    };

    private static ReadOnlyMemory<float> Axis(int index)
    {
        var vector = new float[Dimensions];
        vector[index] = 1f;

        return vector;
    }

    [Fact]
    public async Task Upsert_RoundTripsTheRecord()
    {
        var record = Record("ProjectA", "lesson", "содержимое на русском", "заголовок");

        await _store.UpsertAsync(record, Axis(0));

        var loaded = await _store.GetAsync(record.Id);

        Assert.Equal(record with { Created = loaded!.Created, Updated = loaded.Updated }, loaded);
        Assert.Equal(record.Created.ToUnixTimeSeconds(), loaded.Created.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task Search_FindsTheNearestVectorAndReportsSimilarity()
    {
        var near = Record("ProjectA", "fact", "near");
        var far = Record("ProjectA", "fact", "far");
        await _store.UpsertAsync(near, Axis(0));
        await _store.UpsertAsync(far, Axis(1));

        var found = await _store.SearchAsync(Axis(0), new RecordFilter(), 1);

        Assert.Equal(near.Id, Assert.Single(found).Record.Id);
        Assert.Equal(1.0, found[0].Similarity!.Value, precision: 3);
    }

    [Fact]
    public async Task Search_HonorsProjectAndKindFilters()
    {
        await _store.UpsertAsync(Record("ProjectA", "fact", "a-fact"), Axis(0));
        await _store.UpsertAsync(Record("ProjectB", "fact", "b-fact"), Axis(0));
        await _store.UpsertAsync(Record("ProjectA", "lesson", "a-lesson"), Axis(0));

        var filtered = await _store.SearchAsync(Axis(0), new RecordFilter("projecta", "fact"), 10);

        Assert.Equal("a-fact", Assert.Single(filtered).Record.Content);
    }

    /// A record stored without an embedder carries a zero vector; cosine distance to it is
    /// undefined, so vector search must never surface it — only listing and text search do.
    [Fact]
    public async Task Search_SkipsRecordsStoredWithoutAnEmbedding()
    {
        await _store.UpsertAsync(Record("ProjectA", "fact", "embedded"), Axis(0));
        await _store.UpsertAsync(Record("ProjectA", "fact", "not embedded"), null);

        var found = await _store.SearchAsync(Axis(0), new RecordFilter(), 10);

        Assert.Equal("embedded", Assert.Single(found).Record.Content);
        Assert.Equal(2, (await _store.ListAsync(new RecordFilter(), 10)).Count);
    }

    /// The partition contract: a workspace-scoped store reads only its workspace, the unpartitioned
    /// store reads the whole base, and writes land in the writer's own workspace.
    [Fact]
    public async Task Workspace_PartitionsReadsAndTagsWrites()
    {
        var alpha = _harness.RecordsIn("alpha");
        var beta = _harness.RecordsIn("beta");

        await alpha.UpsertAsync(Record("Shared", "fact", "alpha knowledge"), Axis(0));
        await beta.UpsertAsync(Record("Shared", "fact", "beta knowledge"), Axis(0));

        Assert.Equal("alpha knowledge", Assert.Single(await alpha.ListAsync(new RecordFilter(), 10)).Content);
        Assert.Equal("alpha knowledge", Assert.Single(await alpha.SearchAsync(Axis(0), new RecordFilter(), 10)).Record.Content);
        Assert.Equal(2, (await _store.ListAsync(new RecordFilter(), 10)).Count);
    }

    /// A workspace-scoped store must not read, delete or purge another workspace's records even when
    /// addressed by id.
    [Fact]
    public async Task Workspace_ShieldsForeignRecordsFromDirectAccess()
    {
        var alpha = _harness.RecordsIn("alpha");
        var beta = _harness.RecordsIn("beta");
        var record = Record("Shared", "fact", "alpha knowledge");
        await alpha.UpsertAsync(record, Axis(0));

        Assert.Null(await beta.GetAsync(record.Id));
        Assert.Equal(0, await beta.DeleteAsync([record.Id]));
        Assert.NotNull(await alpha.GetAsync(record.Id));
    }

    /// The text fallback matches Russian case-insensitively — SQL LIKE cannot, which is why the
    /// matching lives in memory.
    [Fact]
    public async Task SearchText_MatchesCaseInsensitivelyBeyondAscii()
    {
        await _store.UpsertAsync(Record("ProjectA", "fact", "Архитектура сервера"), null);

        var found = await _store.SearchTextAsync("архитектура", new RecordFilter(), 10);

        Assert.Single(found);
    }

    [Fact]
    public async Task List_ReturnsNewestFirst()
    {
        var older = Record("ProjectA", "fact", "older") with { Updated = DateTimeOffset.UtcNow.AddDays(-2) };
        var newer = Record("ProjectA", "fact", "newer");
        await _store.UpsertAsync(older, null);
        await _store.UpsertAsync(newer, null);

        var listed = await _store.ListAsync(new RecordFilter(), 10);

        Assert.Equal(["newer", "older"], listed.Select(record => record.Content));
    }

    [Fact]
    public async Task Delete_ReportsOnlyRecordsThatExisted()
    {
        var record = Record("ProjectA", "fact", "gone");
        await _store.UpsertAsync(record, null);

        Assert.Equal(1, await _store.DeleteAsync([record.Id, Guid.NewGuid()]));
        Assert.Null(await _store.GetAsync(record.Id));
    }

    [Fact]
    public async Task MarkAccessed_BumpsTheStatisticsAndKeepsTheVector()
    {
        var record = Record("ProjectA", "fact", "hot");
        await _store.UpsertAsync(record, Axis(0));

        await _store.MarkAccessedAsync([record.Id], DateTimeOffset.UtcNow);
        await _store.MarkAccessedAsync([record.Id], DateTimeOffset.UtcNow);

        var loaded = await _store.GetAsync(record.Id);

        Assert.Equal(2, loaded!.AccessCount);
        Assert.NotNull(loaded.LastAccessed);
        Assert.Single(await _store.SearchAsync(Axis(0), new RecordFilter(), 1));
    }

    [Fact]
    public async Task RenameProject_MovesRecordsUnderTheNewKey()
    {
        await _store.UpsertAsync(Record("OldName", "fact", "content"), Axis(0));

        Assert.Equal(1, await _store.RenameProjectAsync("oldname", "NewName"));

        var moved = Assert.Single(await _store.ListAsync(new RecordFilter("newname"), 10));

        Assert.Equal("NewName", moved.Project);
        Assert.Empty(await _store.ListAsync(new RecordFilter("oldname"), 10));
        Assert.Single(await _store.SearchAsync(Axis(0), new RecordFilter("newname"), 1));
    }

    [Fact]
    public async Task PurgeEvents_RemovesOnlyExpiredEvents()
    {
        var oldEvent = Record("ProjectA", "event", "old event") with { Updated = DateTimeOffset.UtcNow.AddDays(-100) };
        var freshEvent = Record("ProjectA", "event", "fresh event");
        var oldLesson = Record("ProjectA", "lesson", "old lesson") with { Updated = DateTimeOffset.UtcNow.AddDays(-100) };
        await _store.UpsertAsync(oldEvent, null);
        await _store.UpsertAsync(freshEvent, null);
        await _store.UpsertAsync(oldLesson, null);

        Assert.Equal(1, await _store.PurgeEventsBeforeAsync(DateTimeOffset.UtcNow.AddDays(-90)));
        Assert.Null(await _store.GetAsync(oldEvent.Id));
        Assert.NotNull(await _store.GetAsync(freshEvent.Id));
        Assert.NotNull(await _store.GetAsync(oldLesson.Id));
    }

    [Fact]
    public async Task DeleteProject_RemovesEveryRecordOfThatProjectOnly()
    {
        await _store.UpsertAsync(Record("ProjectA", "fact", "a"), null);
        await _store.UpsertAsync(Record("ProjectA", "note", "b"), null);
        await _store.UpsertAsync(Record("ProjectB", "fact", "c"), null);

        Assert.Equal(2, await _store.DeleteProjectAsync("projecta"));
        Assert.Single(await _store.ListAsync(new RecordFilter(), 10));
    }
}
