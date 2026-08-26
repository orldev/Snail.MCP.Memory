using Snail.MCP.Memory.Tests.TestDoubles;

namespace Snail.MCP.Memory.Tests.Infrastructure;

/// The audit lease over a real database: one winner per interval, workspaces independent.
public sealed class EfAuditStoreTests : IDisposable
{
    private static readonly TimeSpan Week = TimeSpan.FromDays(7);

    private readonly MemoryHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task TryClaim_FirstClaimWinsAndHoldsForTheInterval()
    {
        var store = _harness.AuditIn("");

        Assert.True(await store.TryClaimAsync(Week, _harness.Time.GetUtcNow()));
        Assert.False(await store.TryClaimAsync(Week, _harness.Time.GetUtcNow()));

        _harness.Time.Now = _harness.Time.Now.AddDays(3);
        Assert.False(await store.TryClaimAsync(Week, _harness.Time.GetUtcNow()));

        _harness.Time.Now = _harness.Time.Now.AddDays(4);
        Assert.True(await store.TryClaimAsync(Week, _harness.Time.GetUtcNow()));
    }

    /// Two processes over the same database model two agents: after one claims, the other's
    /// identical claim loses.
    [Fact]
    public async Task TryClaim_ASecondProcessOverTheSameDatabaseLoses()
    {
        var first = _harness.AuditIn("team");
        var second = _harness.AuditIn("team");

        Assert.True(await first.TryClaimAsync(Week, _harness.Time.GetUtcNow()));
        Assert.False(await second.TryClaimAsync(Week, _harness.Time.GetUtcNow()));
    }

    [Fact]
    public async Task TryClaim_WorkspacesLeaseIndependently()
    {
        Assert.True(await _harness.AuditIn("left").TryClaimAsync(Week, _harness.Time.GetUtcNow()));
        Assert.True(await _harness.AuditIn("right").TryClaimAsync(Week, _harness.Time.GetUtcNow()));
        Assert.False(await _harness.AuditIn("left").TryClaimAsync(Week, _harness.Time.GetUtcNow()));
    }
}
