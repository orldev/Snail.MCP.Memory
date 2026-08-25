using Snail.MCP.Memory.Core.Activity;

namespace Snail.MCP.Memory.Tests.Core;

public class ActivityTrackerTests
{
    [Fact]
    public void LastActivityUtc_IsSetOnConstruction()
    {
        var before = DateTime.UtcNow;
        var tracker = new ActivityTracker();
        var after = DateTime.UtcNow;

        Assert.InRange(tracker.LastActivityUtc, before.AddSeconds(-1), after.AddSeconds(1));
    }

    [Fact]
    public async Task Touch_AdvancesLastActivityUtc()
    {
        var tracker = new ActivityTracker();
        var initial = tracker.LastActivityUtc;

        await Task.Delay(20);
        tracker.Touch();

        Assert.True(tracker.LastActivityUtc > initial);
    }
}
