using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Snail.MCP.Memory.Core.Activity;
using Snail.MCP.Memory.Host.Hosting;
using Snail.MCP.Memory.Tests.TestDoubles;

namespace Snail.MCP.Memory.Tests.Host;

/// The idle decision runs on a hand-moved clock: polling happens in real time, but whether the
/// server counts as idle depends only on the fake instants — a loaded test machine cannot turn
/// "active" into "idle" or the other way around.
public class IdleShutdownWatcherTests
{
    private sealed class FakeHostApplicationLifetime : IHostApplicationLifetime
    {
        private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool StopApplicationCalled { get; private set; }
        public Task StoppedTask => _stopped.Task;

        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
            StopApplicationCalled = true;
            _stopped.TrySetResult();
        }
    }

    [Fact]
    public async Task StopsApplication_AfterIdleTimeoutElapsesWithoutActivity()
    {
        var time = new FixedTimeProvider(DateTimeOffset.Parse("2026-08-25T12:00:00Z"));
        var activity = new ActivityTracker(time);
        var lifetime = new FakeHostApplicationLifetime();
        var watcher = new IdleShutdownWatcher(
            activity, lifetime, NullLogger<IdleShutdownWatcher>.Instance,
            timeProvider: time,
            idleTimeout: TimeSpan.FromMinutes(60),
            pollInterval: TimeSpan.FromMilliseconds(20));

        await watcher.StartAsync(CancellationToken.None);

        time.Now = time.Now.AddMinutes(61);

        var completed = await Task.WhenAny(lifetime.StoppedTask, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.Same(lifetime.StoppedTask, completed);
        Assert.True(lifetime.StopApplicationCalled);

        await watcher.StopAsync(CancellationToken.None);
    }

    /// The clock never advances, so the server can never look idle — no matter how slowly the
    /// polls themselves run.
    [Fact]
    public async Task DoesNotStopApplication_WhileActivityKeepsBeingTouched()
    {
        var time = new FixedTimeProvider(DateTimeOffset.Parse("2026-08-25T12:00:00Z"));
        var activity = new ActivityTracker(time);
        var lifetime = new FakeHostApplicationLifetime();
        var watcher = new IdleShutdownWatcher(
            activity, lifetime, NullLogger<IdleShutdownWatcher>.Instance,
            timeProvider: time,
            idleTimeout: TimeSpan.FromMinutes(60),
            pollInterval: TimeSpan.FromMilliseconds(20));

        await watcher.StartAsync(CancellationToken.None);

        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(30);
            time.Now = time.Now.AddMinutes(59);
            activity.Touch();
        }

        Assert.False(lifetime.StopApplicationCalled);

        await watcher.StopAsync(CancellationToken.None);
    }
}
