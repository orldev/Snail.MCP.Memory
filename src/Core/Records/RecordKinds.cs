namespace Snail.MCP.Memory.Core.Records;

/// <summary>The kind vocabulary: canonical tags with lifecycle semantics, open to extension.</summary>
/// <remarks>An unknown kind is treated as durable — only <see cref="Event"/> expires by age.</remarks>
public static class RecordKinds
{
    /// <summary>Current state of things: stack, environment, how something works today.</summary>
    public const string Fact = "fact";

    /// <summary>An accepted decision with its reason and the rejected alternatives.</summary>
    public const string Decision = "decision";

    /// <summary>A mistake or pitfall with its cause and how to avoid it — the main transferable asset.</summary>
    public const string Lesson = "lesson";

    /// <summary>How the user likes things done; not tied to any single project.</summary>
    public const string Preference = "preference";

    /// <summary>A pointer to an external resource: URL, dashboard, ticket.</summary>
    public const string Reference = "reference";

    /// <summary>Anything that fits nowhere else; the default.</summary>
    public const string Note = "note";

    /// <summary>A work item, open or done; stale ones surface in review instead of expiring.</summary>
    public const string Task = "task";

    /// <summary>Something that happened; the only kind that expires by age.</summary>
    public const string Event = "event";

    /// <summary>The canonical core, in rendering order.</summary>
    public static readonly IReadOnlyList<string> Canonical =
        [Fact, Decision, Lesson, Preference, Reference, Note, Task, Event];

    /// <summary>Kinds whose records carry experience worth transferring into a new project's briefing.</summary>
    public static readonly IReadOnlyList<string> Transferable = [Lesson, Decision, Preference, Fact];

    /// <summary>Lower-cases and trims a raw kind; spaces and dashes become underscores; empty — <see cref="Note"/>.</summary>
    public static string Normalize(string? raw)
    {
        var kind = (raw ?? "").Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');

        return string.IsNullOrEmpty(kind) ? Note : kind;
    }

    /// <summary>Whether records of this kind expire by age (see <see cref="Lifecycle.RetentionPolicy"/>).</summary>
    public static bool IsEphemeral(string kind) => kind == Event;
}
