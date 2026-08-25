namespace Snail.MCP.Memory.Core.Lifecycle;

/// <summary>How long ephemeral records live and when a durable record counts as stale.</summary>
/// <param name="EventTtl">Age above which an <c>event</c> record is purged on the next write.</param>
/// <param name="StaleAfter">Age above which a never-recalled durable record is only <em>nominated</em> for review — durable knowledge is never deleted silently.</param>
public sealed record RetentionPolicy(TimeSpan EventTtl, TimeSpan StaleAfter)
{
    /// <summary>90 days for events, 180 days before a durable record is nominated for review.</summary>
    public static readonly RetentionPolicy Default = new(TimeSpan.FromDays(90), TimeSpan.FromDays(180));
}
