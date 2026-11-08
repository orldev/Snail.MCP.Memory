using System.Text.Json.Nodes;
using Snail.MCP.Memory.Core.Constants;
using Snail.Sdk.Mcp.Hosting.Answers;

namespace Snail.MCP.Memory.Tools;

/// <summary>The fields of every answer's <c>data</c>, one overload per answer of the application.</summary>
/// <remarks>
/// <para>
/// Names are camelCase; a field without a value — an empty title, a similarity a text match has none of, an empty
/// list of suggestions — is left out rather than written empty, so a record reads as what it holds. Dates are days,
/// <c>yyyy-MM-dd</c>: the memory reasons about the age of knowledge, never its hour. Record ids are written in full:
/// they are what <c>forget</c> and <c>replaceId</c> take back.
/// </para>
/// <para>An answer that found nothing carries a <c>note</c> saying which nothing it is.</para>
/// </remarks>
public static class AnswerFields
{
    /// <summary><c>init_project_memory</c>: the project, whether its memory is new, the briefing, the open suggestions.</summary>
    public static JsonObject Of(ProjectInit init)
    {
        var data = new JsonObject
        {
            ["project"] = init.Project,
            ["created"] = init.IsCreated,
            ["briefing"] = Of(init.Briefing),
        };

        AddPending(data, init.Pending);

        return data;
    }

    /// <summary>The briefing of a new project: similar projects, transferable records, and notes on what was not found.</summary>
    public static JsonObject Of(Briefing briefing)
    {
        var data = new JsonObject();

        if (briefing.SimilarProjects.Count > 0)
            data["similarProjects"] = Array(briefing.SimilarProjects, similar => new JsonObject
            {
                ["project"] = similar.Project,
                ["similarity"] = Similarity(similar.Similarity),
            });

        if (briefing.Experience.Count > 0)
            data["experience"] = Array(briefing.Experience, Of);

        var notes = briefing.MissingSeeds.Select(Messages.SeedProjectMissing).ToList();

        if (!briefing.WasAsked)
            notes.Add(Messages.BriefingHint);
        else if (briefing.SimilarProjects.Count == 0 && briefing.Experience.Count == 0)
            notes.Add(Messages.BriefingEmpty);

        if (notes.Count > 0)
            data["notes"] = Array(notes, note => JsonValue.Create(note));

        return data;
    }

    /// <summary><c>list_projects</c>: the projects, their count, and a note when there are none.</summary>
    public static JsonObject Of(ProjectListing listing)
    {
        var data = new JsonObject();

        if (listing.Prefix.Length > 0)
            data["prefix"] = listing.Prefix;

        data["projects"] = Array(listing.Projects, project =>
        {
            var fields = new JsonObject { ["name"] = project.Name };

            if (!string.IsNullOrWhiteSpace(project.Description))
                fields["description"] = project.Description;

            fields["created"] = Day.Of(project.Created);

            return fields;
        });
        data["count"] = listing.Projects.Count;

        if (listing.Projects.Count == 0)
            data["note"] = listing.Prefix.Length == 0 ? Messages.ProjectListEmpty : Messages.ProjectListNoMatch(listing.Prefix);

        return data;
    }

    /// <summary><c>remember</c>: the id and kind of the stored record, what it superseded, its edges, the suggestions it queued.</summary>
    public static JsonObject Of(Remembered remembered)
    {
        var data = new JsonObject
        {
            ["id"] = remembered.Record.Id.ToString(),
            ["kind"] = remembered.Record.Kind,
            ["project"] = remembered.Record.Project,
        };

        if (remembered.Replaced is { } replaced)
            data["replaced"] = replaced.ToString();

        if (remembered.Links.Count > 0)
            data["links"] = Array(remembered.Links, Of);

        AddPending(data, remembered.Proposed);

        if (remembered.PurgedEvents > 0)
            data["purgedEvents"] = remembered.PurgedEvents;

        return data;
    }

    /// <summary>One edge asked for with a record: the edge written, or why it was not, with the record stored all the same.</summary>
    public static JsonObject Of(Answer<Linked> link)
    {
        if (link.TryGet(out var linked))
            return Of(linked);

        var refusal = new JsonObject { ["error"] = link.Error };

        if (link.Hint is { } hint)
            refusal["hint"] = hint;

        if (link.Type is { } type)
            refusal["type"] = type;

        return refusal;
    }

    /// <summary><c>recall</c>: what was searched, how, the records found, the project's open suggestions.</summary>
    public static JsonObject Of(Recall recall)
    {
        var data = new JsonObject();

        if (recall.Project.Length > 0)
            data["project"] = recall.Project;

        if (recall.Query.Length > 0)
            data["query"] = recall.Query;

        if (recall.Kind.Length > 0)
            data["kind"] = recall.Kind;

        data["search"] = ToolAnswer.Word(recall.Search, WordCase.Lower);
        data["records"] = Array(recall.Records, Of);

        if (recall.Search is RecallSearch.Text)
            data["note"] = Messages.TextFallbackNote;
        else if (recall.Records.Count == 0)
            data["note"] = Messages.NoRecords;

        AddPending(data, recall.Pending);

        return data;
    }

    /// <summary>One record as a recall, a briefing or a refused duplicate show it, with the similarity that found it.</summary>
    public static JsonObject Of(ScoredRecord scored)
    {
        var record = scored.Record;
        var data = new JsonObject
        {
            ["id"] = record.Id.ToString(),
            ["kind"] = record.Kind,
        };

        if (!string.IsNullOrWhiteSpace(record.Title))
            data["title"] = record.Title;

        data["project"] = record.Project;
        data["content"] = record.Content.Trim();
        data["updated"] = Day.Of(record.Updated);

        if (scored.Similarity is { } similarity)
            data["similarity"] = Similarity(similarity);

        return data;
    }

    /// <summary><c>forget</c>: how many records were deleted.</summary>
    public static JsonObject Of(Forgotten forgotten) => new() { ["forgotten"] = forgotten.Count };

    /// <summary><c>review_memory</c>: stale durable records and stale tasks as one-line candidates.</summary>
    public static JsonObject Of(Review review)
    {
        var data = new JsonObject();

        if (review.Project.Length > 0)
            data["project"] = review.Project;

        data["staleAfterDays"] = review.StaleAfterDays;
        data["stale"] = Array(review.Stale, Candidate);
        data["tasks"] = Array(review.Tasks, Candidate);

        if (review.Stale.Count == 0 && review.Tasks.Count == 0)
            data["note"] = Messages.ReviewEmpty;

        return data;
    }

    /// <summary><c>rename_project</c>: the two names and what moved.</summary>
    public static JsonObject Of(Renamed renamed) => new()
    {
        ["from"] = renamed.From,
        ["to"] = renamed.To,
        ["records"] = renamed.Records,
        ["edges"] = renamed.Edges,
    };

    /// <summary><c>link_projects</c>: the edge, whether it is new, the suggestions it settled.</summary>
    public static JsonObject Of(Linked linked)
    {
        var data = new JsonObject
        {
            ["from"] = linked.From,
            ["type"] = linked.Type,
            ["to"] = linked.To,
            ["created"] = linked.IsCreated,
        };

        if (linked.PendingResolved > 0)
            data["pendingResolved"] = linked.PendingResolved;

        return data;
    }

    /// <summary><c>unlink_projects</c>: the pair, the type removed, how many edges went.</summary>
    public static JsonObject Of(Unlinked unlinked)
    {
        var data = new JsonObject
        {
            ["from"] = unlinked.From,
            ["to"] = unlinked.To,
        };

        if (unlinked.Type.Length > 0)
            data["type"] = unlinked.Type;

        data["removed"] = unlinked.Removed;

        if (unlinked.Removed == 0)
            data["note"] = Messages.NoLinksBetween(unlinked.From, unlinked.To);

        return data;
    }

    /// <summary><c>dismiss_link</c>: the pair that will not be suggested again.</summary>
    public static JsonObject Of(Dismissed dismissed) => new()
    {
        ["from"] = dismissed.From,
        ["to"] = dismissed.To,
        ["dismissed"] = true,
    };

    /// <summary><c>sync_project_links</c>: the counts of a sync and the targets it skipped.</summary>
    public static JsonObject Of(Synced synced)
    {
        var data = new JsonObject
        {
            ["project"] = synced.Project,
            ["source"] = synced.Source,
            ["created"] = synced.Created,
            ["refreshed"] = synced.Refreshed,
            ["kept"] = synced.Kept,
            ["stale"] = synced.Stale,
        };

        if (synced.Skipped.Count > 0)
            data["skipped"] = Array(synced.Skipped, name => JsonValue.Create(name));

        return data;
    }

    /// <summary><c>get_graph</c>: the edges, how many projects they join, and a note when there are none.</summary>
    public static JsonObject Of(GraphView view)
    {
        var data = new JsonObject();

        if (view.Roots.Count > 0)
        {
            data["roots"] = Array(view.Roots, root => JsonValue.Create(root));
            data["depth"] = view.Depth;
        }

        var projects = view.Edges
            .SelectMany(edge => new[] { edge.Source, edge.Target })
            .Distinct(ProjectIdentity.Comparer)
            .Count();

        data["projects"] = projects;
        data["edges"] = Array(view.Edges, edge =>
        {
            var fields = new JsonObject
            {
                ["from"] = edge.Source,
                ["type"] = edge.Type,
                ["to"] = edge.Target,
            };

            if (!string.IsNullOrWhiteSpace(edge.Note))
                fields["note"] = edge.Note;

            return fields;
        });

        if (view.Edges.Count == 0)
            data["note"] = view.Roots.Count == 0 ? Messages.GraphEmpty : Messages.GraphEmptyAround(string.Join(" + ", view.Roots));

        return data;
    }

    private static void AddPending(JsonObject data, IReadOnlyList<PendingLink> pending)
    {
        if (pending.Count == 0)
            return;

        data["pending"] = Array(pending, link =>
        {
            var fields = new JsonObject
            {
                ["project"] = link.Project,
                ["other"] = link.Other,
            };

            if (!string.IsNullOrWhiteSpace(link.Evidence))
                fields["evidence"] = link.Evidence;

            fields["noticed"] = Day.Of(link.Updated);

            return fields;
        });
    }

    private static JsonObject Candidate(MemoryRecord record) => new()
    {
        ["id"] = record.Id.ToString(),
        ["kind"] = record.Kind,
        ["label"] = record.Label,
        ["project"] = record.Project,
        ["created"] = Day.Of(record.Created),
        ["updated"] = Day.Of(record.Updated),
    };

    private static double Similarity(double value) => Math.Round(value, 2);

    private static JsonArray Array<T>(IEnumerable<T> items, Func<T, JsonNode?> map) => [.. items.Select(map)];
}
