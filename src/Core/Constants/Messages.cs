namespace Snail.MCP.Memory.Core.Constants;

/// <summary>Every human-readable string the server returns to the model.</summary>
/// <remarks>English only: responses are read by a model; record content keeps the conversation's language.</remarks>
public static class Messages
{
    /// <summary>Empty project name.</summary>
    public const string ProjectNameEmpty = "Project name is empty.";

    /// <summary>Project already has memory.</summary>
    public static string ProjectAlreadyExists(string project) => $"Memory for project '{project}' already exists.";

    /// <summary>Memory created; the briefing may follow.</summary>
    public static string ProjectCreated(string project) => $"Memory for project '{project}' created.";

    /// <summary>Project not found.</summary>
    public static string ProjectNotFound(string project) => $"Project '{project}' not found.";

    /// <summary>No name was given and the working directory does not suggest one.</summary>
    public static string ProjectNameNotResolved(string folder) =>
        $"Project name is empty and the working directory ('{folder}') is not a project folder. Pass projectName explicitly.";

    /// <summary>Title of the project listing.</summary>
    public const string ProjectListTitle = "# 📚 Projects in memory";

    /// <summary>Sub-title naming the prefix the listing was filtered by.</summary>
    public static string ProjectListFilter(string prefix) => $"_Filtered by prefix `{prefix}`._";

    /// <summary>Nothing is stored yet.</summary>
    public const string ProjectListEmpty = "_Memory is empty. Create a project with init_project_memory._";

    /// <summary>Nothing matches the prefix.</summary>
    public static string ProjectListNoMatch(string prefix) => $"_No projects start with '{prefix}'._";

    /// <summary>Counter line closing the listing.</summary>
    public static string ProjectListCount(int count) => $"Total: {count}.";

    /// <summary>A record was stored.</summary>
    public static string Recorded(string kind, string project, Guid id) =>
        $"Recorded [{kind}] in project '{project}' (id {id}).";

    /// <summary>A record was superseded by a new one.</summary>
    public static string RecordReplaced(Guid oldId, Guid newId) =>
        $"Record {oldId} superseded by {newId}; the old record is deleted.";

    /// <summary>Warning heading when a near-duplicate already exists.</summary>
    public static string DuplicateFound(double similarity) =>
        $"⚠️ Not stored: a very similar record already exists (similarity {similarity:0.00}). " +
        "Pass replaceId with its id to supersede it, or force: true to store both.";

    /// <summary>Record id not found.</summary>
    public static string RecordNotFound(Guid id) => $"Record {id} not found.";

    /// <summary>Malformed record id.</summary>
    public static string RecordIdInvalid(string raw) => $"'{raw}' is not a record id.";

    /// <summary>Records deleted by forget.</summary>
    public static string RecordsForgotten(int count) => $"Records forgotten: {count}.";

    /// <summary>Suffix appended when the lazy purge removed expired events.</summary>
    public static string PurgeNotice(int count) => $"\n🧹 Expired event records purged: {count}.";

    /// <summary>Title of a recall over the whole memory or a project.</summary>
    public static string RecallTitle(string query) => $"# 🔎 Recall: \"{query}\"";

    /// <summary>Title of a project overview (recall without a query).</summary>
    public static string OverviewTitle(string project) => $"# 📖 Overview: {project}";

    /// <summary>Scope line under a recall title.</summary>
    public static string RecallScope(string scope, string kind, int count) =>
        $"_Scope: {scope} · kind: {kind} · {count} record(s)._";

    /// <summary>Scope wording for the whole memory.</summary>
    public const string ScopeWholeMemory = "whole memory";

    /// <summary>Kind wording when no kind filter is applied.</summary>
    public const string AnyKind = "any";

    /// <summary>Nothing was found.</summary>
    public const string NoRecords = "_No records found._";

    /// <summary>Note that vector search was unavailable and text matching was used.</summary>
    public const string TextFallbackNote = "_Embedding model unavailable — plain text matching was used._";

    /// <summary>Closing hint of a recall result.</summary>
    public const string RecallHint = "_forget(id) removes a record; remember(..., replaceId: id) supersedes it._";

    /// <summary>Title of the experience briefing shown after init.</summary>
    public const string BriefingTitle = "## 🧭 Briefing from related experience";

    /// <summary>Sub-heading of the similar projects list.</summary>
    public const string SimilarProjectsTitle = "### Similar projects";

    /// <summary>Sub-heading of the transferred records.</summary>
    public const string ExperienceTitle = "### Decisions and lessons worth knowing";

    /// <summary>How the briefing is obtained when it was skipped.</summary>
    public const string BriefingHint =
        "_Pass description to receive a briefing from similar projects, or seedFrom with project names to preload their experience._";

    /// <summary>A seed project named in seedFrom does not exist.</summary>
    public static string SeedProjectMissing(string project) => $"_Seed project '{project}' not found — skipped._";

    /// <summary>Title of the review listing.</summary>
    public const string ReviewTitle = "# 🧹 Review: deletion candidates";

    /// <summary>Sub-heading of stale durable records.</summary>
    public static string ReviewStaleTitle(int days) => $"### Never recalled and older than {days} days";

    /// <summary>Sub-heading of stale tasks.</summary>
    public static string ReviewTasksTitle(int days) => $"### Tasks untouched for {days}+ days";

    /// <summary>Review found nothing.</summary>
    public const string ReviewEmpty = "_Nothing to clean up._";

    /// <summary>Closing hint of the review listing.</summary>
    public const string ReviewHint =
        "_These are candidates, not decisions: confirm each with the user, then forget(\"id1,id2,...\")._";

    /// <summary>Project renamed.</summary>
    public static string ProjectRenamed(string oldName, string newName, int records, int edges) =>
        $"Project '{oldName}' renamed to '{newName}': {records} record(s) and {edges} edge(s) moved.";

    /// <summary>The new name is already taken.</summary>
    public static string RenameTargetTaken(string newName) => $"Project '{newName}' already exists — rename refused.";

    /// <summary>Edge created.</summary>
    public static string LinkCreated(string from, string type, string to) => $"Link created: {from} --{type}--> {to}";

    /// <summary>Edge updated.</summary>
    public static string LinkUpdated(string from, string type, string to) => $"Link updated: {from} --{type}--> {to}";

    /// <summary>No edges between the projects.</summary>
    public static string NoLinksBetween(string from, string to) => $"No links between {from} and {to}.";

    /// <summary>Edges removed.</summary>
    public static string LinksRemoved(int count, string from, string to) => $"Links removed: {count} (between {from} and {to}).";

    /// <summary>Both projects are required.</summary>
    public const string BothProjectsRequired = "Both projects are required.";

    /// <summary>A project cannot be linked to itself.</summary>
    public const string SelfLinkForbidden = "A project cannot be linked to itself.";

    /// <summary>Unknown edge type with the list of allowed ones.</summary>
    public static string UnknownLinkType(string type, string allowed) => $"Unknown link type '{type}'. Allowed: {allowed}.";

    /// <summary>A pending suggestion settled by a confirmed edge.</summary>
    public static string PendingResolved(int count) => $"\n✅ Pending link suggestion resolved ({count}).";

    /// <summary>A suggested pair was rejected.</summary>
    public static string LinkDismissed(string a, string b) =>
        $"Link suggestion between '{a}' and '{b}' dismissed; the pair will not be suggested again.";

    /// <summary>Sub-heading of the pending link suggestions queue.</summary>
    public const string PendingTitle = "### ⏳ Pending link suggestions";

    /// <summary>Closing hint of the pending queue.</summary>
    public const string PendingHint =
        "_Confirm with link_projects (pick the type and direction) or reject with dismiss_link — " +
        "a suggestion returns every session until resolved._";

    /// <summary>Text for an empty graph.</summary>
    public const string GraphEmpty = "_The graph is empty. Link projects with the link_projects tool._";

    /// <summary>Title of the whole-graph rendering.</summary>
    public const string WholeGraphTitle = "# 🕸 Knowledge graph (whole)";

    /// <summary>Title of a graph rendering around roots.</summary>
    public static string GraphTitle(string roots, int depth) => $"# 🕸 Knowledge graph: {roots} (depth {depth})";

    /// <summary>Edge/project counters line.</summary>
    public static string GraphStats(int edges, int projects) => $"Edges: {edges}, project nodes: {projects}.";

    /// <summary>Header line of the edge table (columns).</summary>
    public const string GraphTableHeader = "| From | Relation | To | Note |";

    /// <summary>Separator line of the edge table.</summary>
    public const string GraphTableSeparator = "|------|----------|----|------|";
}
