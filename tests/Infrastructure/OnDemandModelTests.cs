using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Snail.MCP.Memory.Infrastructure.Embeddings;

namespace Snail.MCP.Memory.Tests.Infrastructure;

/// The model's life on a hand-moved clock: the release timer fires only when the test advances it, so whether the model
/// is in memory depends on the fake instants alone, never on how fast the machine runs the test.
public sealed class OnDemandModelTests
{
    private static readonly TimeSpan IdlePeriod = TimeSpan.FromMinutes(30);

    private readonly FakeTimeProvider _time = new();
    private readonly List<FakeModel> _loaded = [];

    /// Five calls that arrive before the model is in memory wait for one load rather than paying for five.
    [Fact]
    public async Task ConcurrentFirstUses_ShareOneLoad()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var model = Holder(IdlePeriod, async () =>
        {
            await gate.Task;

            return Load();
        });

        var uses = Enumerable.Range(0, 5).Select(_ => model.UseAsync(Answer, CancellationToken.None)).ToList();
        gate.SetResult();
        await Task.WhenAll(uses);

        Assert.Single(_loaded);
        Assert.True(model.IsLoaded);
    }

    [Fact]
    public async Task UnusedModel_IsReleasedAfterTheIdlePeriod()
    {
        using var model = Holder(IdlePeriod);

        await model.UseAsync(Answer, CancellationToken.None);
        _time.Advance(IdlePeriod - TimeSpan.FromSeconds(1));

        Assert.True(model.IsLoaded);

        _time.Advance(TimeSpan.FromSeconds(1));

        Assert.False(model.IsLoaded);
        Assert.True(_loaded[0].IsDisposed);
    }

    [Fact]
    public async Task UseAfterRelease_LoadsTheModelAgain()
    {
        using var model = Holder(IdlePeriod);

        await model.UseAsync(Answer, CancellationToken.None);
        _time.Advance(IdlePeriod);
        var answer = await model.UseAsync(Answer, CancellationToken.None);

        Assert.Equal(2, answer);
        Assert.Equal(2, _loaded.Count);
        Assert.True(model.IsLoaded);
    }

    /// A use within the period starts it over: the model goes a whole period after the last use, not after the load.
    [Fact]
    public async Task UseWithinThePeriod_PostponesTheRelease()
    {
        using var model = Holder(IdlePeriod);

        await model.UseAsync(Answer, CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(20));
        await model.UseAsync(Answer, CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(20));

        Assert.True(model.IsLoaded);

        _time.Advance(TimeSpan.FromMinutes(10));

        Assert.False(model.IsLoaded);
        Assert.Single(_loaded);
    }

    /// A call longer than the period holds the model however long it runs; the period starts when it ends.
    [Fact]
    public async Task ModelInUse_IsNeverReleased()
    {
        var running = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var model = Holder(IdlePeriod);

        var use = model.UseAsync(_ => running.Task, CancellationToken.None);
        await WaitUntilAsync(() => model.IsLoaded);
        _time.Advance(TimeSpan.FromHours(2));

        Assert.True(model.IsLoaded);
        Assert.False(_loaded[0].IsDisposed);

        running.SetResult(1);
        await use;
        _time.Advance(IdlePeriod);

        Assert.False(model.IsLoaded);
    }

    [Fact]
    public async Task ZeroPeriod_KeepsTheModelForGood()
    {
        using var model = Holder(TimeSpan.Zero);

        await model.UseAsync(Answer, CancellationToken.None);
        _time.Advance(TimeSpan.FromDays(30));

        Assert.True(model.IsLoaded);
        Assert.Single(_loaded);
    }

    /// A failed load is not remembered by the holder: the next call tries again.
    [Fact]
    public async Task FailedLoad_IsTriedAgainOnTheNextUse()
    {
        var attempts = 0;
        using var model = Holder(IdlePeriod, () => ++attempts == 1
            ? Task.FromException<FakeModel>(new IOException("model file is locked"))
            : Task.FromResult(Load()));

        await Assert.ThrowsAsync<IOException>(() => model.UseAsync(Answer, CancellationToken.None));
        await model.UseAsync(Answer, CancellationToken.None);

        Assert.Equal(2, attempts);
        Assert.True(model.IsLoaded);
    }

    [Fact]
    public async Task LastUsed_IsWhenTheLastUseEnded()
    {
        using var model = Holder(IdlePeriod);

        Assert.Null(model.LastUsed);

        _time.Advance(TimeSpan.FromMinutes(5));
        await model.UseAsync(Answer, CancellationToken.None);

        Assert.Equal(_time.GetUtcNow(), model.LastUsed);
    }

    [Fact]
    public async Task Dispose_ReleasesTheLoadedModel()
    {
        var model = Holder(IdlePeriod);

        await model.UseAsync(Answer, CancellationToken.None);
        model.Dispose();

        Assert.True(_loaded[0].IsDisposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => model.UseAsync(Answer, CancellationToken.None));
    }

    private OnDemandModel<FakeModel> Holder(TimeSpan idlePeriod, Func<Task<FakeModel>>? load = null) =>
        new(load ?? (() => Task.FromResult(Load())), idlePeriod, _time, NullLogger.Instance);

    private FakeModel Load()
    {
        var model = new FakeModel(_loaded.Count + 1);

        lock (_loaded)
            _loaded.Add(model);

        return model;
    }

    private static Task<int> Answer(FakeModel model) => Task.FromResult(model.Generation);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(5);

        Assert.True(condition(), "the condition did not come true within 5 s");
    }

    private sealed class FakeModel(int generation) : IDisposable
    {
        public int Generation => generation;

        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }
}
