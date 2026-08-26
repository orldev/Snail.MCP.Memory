using Snail.MCP.Memory.Infrastructure.Configuration;

namespace Snail.MCP.Memory.Tools;

/// <summary>Description text of every tool and shared parameter.</summary>
/// <remarks>These strings are the prompt: for a model with no other instructions they are the only manual.</remarks>
public static class ToolDescriptions
{
    /// <summary><c>init_project_memory</c>.</summary>
    public const string InitProjectMemory =
        "Initializes memory for a project and returns a briefing from related experience: similar " +
        "projects, their decisions and lessons. Call it when starting work on a project that has no " +
        "memory yet. Pass description — it is what finds the similar projects; pass seedFrom with " +
        "existing project names (joined by '+') to preload their experience explicitly. Without " +
        "projectName the server names the project after its working folder and refuses generic " +
        "folders (src, projects, work, design).";

    /// <summary><c>remember</c>.</summary>
    public const string Remember =
        "Stores one record into long-term memory. Call it by itself whenever something worth keeping " +
        "happens: a decision is made (kind 'decision'), a mistake is understood (kind 'lesson' — " +
        "what happened, why, how to avoid), a stable fact changes (kind 'fact'), the user states a " +
        "preference (kind 'preference'). When the server answers that a very similar record exists, " +
        "do not retry blindly: either supersede it (replaceId) or store anyway (force: true). When " +
        "an existing record turned out wrong or outdated, replace it with replaceId instead of " +
        "adding a second opinion next to it. When the record itself reveals a relation to another " +
        "project, pass links — the record and its edges land in one call. A mentioned project " +
        "with no edge yet comes back as a pending link suggestion: confirm it with link_projects " +
        "or reject it with dismiss_link, do not ignore it.";

    /// <summary><c>recall</c>.</summary>
    public const string Recall =
        "Searches memory by meaning. Call it before proposing an architecture, a stack or a fix — " +
        "past decisions and lessons apply across projects, so leave projectName empty to search the " +
        "whole memory (that is the default that transfers experience); set it to restore the context " +
        "of one project. An empty query returns an overview of the newest records — use that to " +
        "recover after a context reset. If a returned record is visibly wrong or outdated, fix the " +
        "memory immediately: remember with replaceId, or forget. A project-scoped recall also " +
        "lists the project's pending link suggestions until each is confirmed (link_projects) or " +
        "rejected (dismiss_link).";

    /// <summary><c>forget</c>.</summary>
    public const string Forget =
        "Deletes records by id (several ids comma-separated). Deletion is permanent; for bulk " +
        "cleanups confirm the list with the user first — review_memory nominates candidates.";

    /// <summary><c>review_memory</c>.</summary>
    public const string ReviewMemory =
        "Lists deletion candidates: durable records that recall has never returned and that grew " +
        "old, plus tasks untouched for as long. Candidates are nominations, not decisions — confirm " +
        "with the user, then forget the confirmed ids.";

    /// <summary><c>rename_project</c>.</summary>
    public const string RenameProject =
        "Renames a project everywhere: the registry, every record, every graph edge. The old name " +
        "stops existing.";

    /// <summary><c>list_projects</c>.</summary>
    public const string ListProjects =
        "Lists the projects that have memory. Call it before guessing how a project was named.";

    /// <summary><c>link_projects</c>.</summary>
    public const string LinkProjects =
        "Creates or updates a directed, typed edge between two projects. Call it by itself the " +
        "moment a dependency becomes visible: one project uses another, holds its design, documents " +
        "it. Only directed facts belong here — 'these two are similar' is not stored, recall finds " +
        "similarity by meaning. A confirmed edge also settles the pair's pending link suggestion.";

    /// <summary><c>dismiss_link</c>.</summary>
    public const string DismissLink =
        "Rejects a pending link suggestion: the pair stops being proposed, permanently. Use it " +
        "when a mentioned project is not actually related; when the relation is real, confirm it " +
        "with link_projects instead.";

    /// <summary><c>sync_project_links</c>.</summary>
    public const string SyncProjectLinks =
        "Syncs machine-extracted relations into the graph from any source of truth, not only " +
        "code: a project manifest (csproj, package.json), an Obsidian vault's wiki-links, an XML " +
        "registry. The caller parses the source and passes the relations; the server upserts " +
        "them as auto edges (note 'auto: <source>'). Manual edges are never overwritten, an auto " +
        "edge that disappeared from its source is only marked stale — never deleted — and a " +
        "target with no memory is skipped and reported. Idempotent: the same payload twice " +
        "changes nothing.";

    /// <summary><c>unlink_projects</c>.</summary>
    public const string UnlinkProjects =
        "Removes edges between two projects regardless of direction. Empty type removes every edge " +
        "between them.";

    /// <summary><c>get_graph</c>.</summary>
    public const string GetGraph =
        "Renders the directed edges around the selected projects without loading any records. " +
        "Empty projectName renders the whole graph.";

    /// <summary>Shared wording of the projectName parameter.</summary>
    public const string ProjectNameParam =
        "Project name — the identifier the memory was created with. Case-insensitive.";

    /// <summary>Wording of the optional projectName of init.</summary>
    public const string NewProjectNameParam =
        "Project name. Optional: when omitted, the server uses the name of the working folder the " +
        "client was started in, refusing generic folders (src, projects, work, design).";

    /// <summary>Wording of the prefix parameter of list_projects.</summary>
    public const string PrefixParam =
        "Leading characters of the project name, case-insensitive. Empty — every project.";

    /// <summary>Wording of the kind parameter.</summary>
    public const string KindParam =
        "Record kind. Canonical: fact, decision, lesson, preference, reference, note, task, event; " +
        "other tags are allowed. Empty on recall — every kind; empty on remember — 'note'.";

    /// <summary>Wording of the link type parameter.</summary>
    public const string LinkTypeParam =
        "Edge type; all types are directed, from source to target.";

    /// <summary>Wording of the links parameter of remember.</summary>
    public const string LinksParam =
        "Graph edges to create together with the record — the atomic form of link_projects: " +
        "each element links this record's project to its 'to' project. Optional.";
}
