namespace Snail.MCP.Memory.Core.Activity;

/// <summary>Default <see cref="IActivityTracker"/>: remembers the instant of the last tool call.</summary>
/// <remarks>Stored as ticks because <see cref="Interlocked"/> swaps primitives atomically, not <see cref="DateTime"/>.</remarks>
public sealed class ActivityTracker : IActivityTracker
{
    private readonly TimeProvider _timeProvider;
    private long _lastActivityTicks;

    /// <summary>Creates a tracker whose initial activity instant is "now".</summary>
    public ActivityTracker(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _lastActivityTicks = _timeProvider.GetUtcNow().UtcTicks;
    }

    /// <inheritdoc />
    public DateTime LastActivityUtc => new(Interlocked.Read(ref _lastActivityTicks), DateTimeKind.Utc);

    /// <inheritdoc />
    public void Touch() => Interlocked.Exchange(ref _lastActivityTicks, _timeProvider.GetUtcNow().UtcTicks);
}
