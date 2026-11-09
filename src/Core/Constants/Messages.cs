namespace Snail.MCP.Memory.Core.Constants;

/// <summary>Every human-readable string the server answers the model with: the errors and hints of failures, the notes of empty answers.</summary>
/// <remarks>
/// English only: answers are read by a model; record content keeps the conversation's language. Hints are lowercase and
/// end without a stop, as the hints of Snail.Sdk beside them do.
/// </remarks>
public static class Messages
{
    /// <summary>Empty project name.</summary>
    public const string ProjectNameEmpty = "Project name is empty.";

    /// <summary>What to do about an empty project name.</summary>
    public const string ProjectNameEmptyHint = "name the project as list_projects lists it";

    /// <summary>Project not found.</summary>
    public static string ProjectNotFound(string project) => $"Project '{project}' not found.";

    /// <summary>What to do about a project that is not in memory.</summary>
    public const string ProjectNotFoundHint = "list_projects names the projects in memory; init_project_memory creates one";

    /// <summary>No name was given and the working directory does not suggest one.</summary>
    public static string ProjectNameNotResolved(string folder) =>
        $"Project name is empty and the working directory ('{folder}') is not a project folder.";

    /// <summary>What to do when the folder names no project.</summary>
    public const string ProjectNameNotResolvedHint = "pass projectName explicitly";

    /// <summary>A record was refused as a near-duplicate.</summary>
    public static string DuplicateFound(double similarity) =>
        FormattableString.Invariant($"Not stored: a very similar record already exists (similarity {similarity:0.00}).");

    /// <summary>What to do about a near-duplicate, whose record is the failure's data.</summary>
    public const string DuplicateFoundHint = "pass replaceId with its id to supersede it, or force: true to store both";

    /// <summary>Record id not found.</summary>
    public static string RecordNotFound(Guid id) => $"Record {id} not found.";

    /// <summary>What to do about a record id that names no record.</summary>
    public const string RecordNotFoundHint = "recall finds the record and its current id";

    /// <summary>Malformed record id.</summary>
    public static string RecordIdInvalid(string raw) => $"'{raw}' is not a record id.";

    /// <summary>What to do about a malformed record id.</summary>
    public const string RecordIdInvalidHint = "pass the id as recall or review_memory answer it";

    /// <summary>The new name is already taken.</summary>
    public static string RenameTargetTaken(string newName) => $"Project '{newName}' already exists — rename refused.";

    /// <summary>What to do about a name another project has.</summary>
    public const string RenameTargetTakenHint = "choose a name no project has";

    /// <summary>An unconfirmed forget_project: nothing is gone yet.</summary>
    public static string ProjectForgetUnconfirmed(string project) =>
        $"nothing is deleted yet: show the user these counts, and call forget_project for '{project}' with confirm: true only once they agree";

    /// <summary>Both projects are required.</summary>
    public const string BothProjectsRequired = "Both projects are required.";

    /// <summary>What to do about a link with an empty end.</summary>
    public const string BothProjectsRequiredHint = "pass both from and to";

    /// <summary>A project cannot be linked to itself.</summary>
    public const string SelfLinkForbidden = "A project cannot be linked to itself.";

    /// <summary>What to do about a link from a project to itself.</summary>
    public const string SelfLinkForbiddenHint = "pass two different projects";

    /// <summary>Unknown edge type.</summary>
    public static string UnknownLinkType(string type) => $"Unknown link type '{type}'.";

    /// <summary>What to do about an unknown edge type: the allowed list.</summary>
    public static string UnknownLinkTypeHint(string allowed) => $"one of: {allowed}; empty removes every type";

    /// <summary>Nothing is stored yet.</summary>
    public const string ProjectListEmpty = "memory is empty: init_project_memory creates a project";

    /// <summary>Nothing matches the prefix.</summary>
    public static string ProjectListNoMatch(string prefix) => $"no project starts with '{prefix}'";

    /// <summary>A recall or an overview found nothing.</summary>
    public const string NoRecords = "no records found";

    /// <summary>Vector search was unavailable and text matching was used.</summary>
    public const string TextFallbackNote = "the embedding model is unavailable: plain text matching was used";

    /// <summary>The briefing was not asked for.</summary>
    public const string BriefingHint =
        "pass description to receive a briefing from similar projects, or seedFrom with project names to preload their experience";

    /// <summary>The briefing was asked for and found nothing.</summary>
    public const string BriefingEmpty = "no similar project and no transferable record was found";

    /// <summary>A seed project named in seedFrom does not exist.</summary>
    public static string SeedProjectMissing(string project) => $"seed project '{project}' not found — skipped";

    /// <summary>Review found nothing.</summary>
    public const string ReviewEmpty = "nothing to clean up";

    /// <summary>No edges between the projects.</summary>
    public static string NoLinksBetween(string from, string to) => $"no links between {from} and {to}";

    /// <summary>An empty graph.</summary>
    public const string GraphEmpty = "the graph is empty: link_projects links two projects";

    /// <summary>No edges around the projects asked for.</summary>
    public static string GraphEmptyAround(string roots) => $"no edges around {roots}";

    /// <summary>Prefix that marks an edge as machine-written; manual notes never start with it.</summary>
    public const string AutoNotePrefix = "auto: ";

    /// <summary>The note of a freshly synced auto edge.</summary>
    public static string AutoNote(string source) => AutoNotePrefix + source;

    /// <summary>Marker of a synced edge that disappeared from its source.</summary>
    public const string StaleMarker = " · stale ";

    /// <summary>The stale marker with the date the disappearance was noticed.</summary>
    public static string StaleSuffix(string date) => StaleMarker + date;
}
