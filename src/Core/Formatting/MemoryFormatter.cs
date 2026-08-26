using Snail.MCP.Memory.Core.Constants;

namespace Snail.MCP.Memory.Core.Formatting;

/// <summary>Renders store results into the Markdown the model receives — the only place response layout exists.</summary>
/// <remarks>Record ids are printed in full: they are the handles <c>forget</c> and <c>replaceId</c> take back.</remarks>
public static class MemoryFormatter
{
    /// <summary>The one date format of the whole application.</summary>
    public const string DateFormat = "yyyy-MM-dd";

    /// <summary>The project listing: name, description, creation date.</summary>
    public static string RenderProjectList(IReadOnlyList<ProjectInfo> projects, string prefix)
    {
        var builder = new StringBuilder();
        builder.AppendLine(Messages.ProjectListTitle);

        if (!string.IsNullOrWhiteSpace(prefix))
            builder.AppendLine(Messages.ProjectListFilter(prefix.Trim()));

        builder.AppendLine();

        if (projects.Count == 0)
        {
            builder.AppendLine(string.IsNullOrWhiteSpace(prefix)
                ? Messages.ProjectListEmpty
                : Messages.ProjectListNoMatch(prefix.Trim()));

            return builder.ToString().TrimEnd();
        }

        foreach (var project in projects)
        {
            builder.Append("- **").Append(project.Name).Append("** · ").Append(Date(project.Created));

            if (!string.IsNullOrWhiteSpace(project.Description))
                builder.Append(" — ").Append(project.Description);

            builder.AppendLine();
        }

        builder.AppendLine();
        builder.AppendLine(Messages.ProjectListCount(projects.Count));

        return builder.ToString().TrimEnd();
    }

    /// <summary>A recall result: title, scope line, record blocks, closing hint.</summary>
    public static string RenderRecall(
        string query, string scope, string kind, IReadOnlyList<ScoredRecord> records, bool usedTextFallback)
    {
        var builder = new StringBuilder();
        builder.AppendLine(Messages.RecallTitle(query));
        builder.AppendLine(Messages.RecallScope(scope, kind, records.Count));

        if (usedTextFallback)
            builder.AppendLine(Messages.TextFallbackNote);

        builder.AppendLine();

        if (records.Count == 0)
        {
            builder.AppendLine(Messages.NoRecords);

            return builder.ToString().TrimEnd();
        }

        foreach (var scored in records)
            AppendRecord(builder, scored.Record, scored.Similarity);

        builder.AppendLine(Messages.RecallHint);

        return builder.ToString().TrimEnd();
    }

    /// <summary>A project overview: newest records grouped by kind, canonical kinds first.</summary>
    public static string RenderOverview(string project, string kind, IReadOnlyList<MemoryRecord> records)
    {
        var builder = new StringBuilder();
        builder.AppendLine(Messages.OverviewTitle(project));
        builder.AppendLine(Messages.RecallScope(Scope(project), kind, records.Count));
        builder.AppendLine();

        if (records.Count == 0)
        {
            builder.AppendLine(Messages.NoRecords);

            return builder.ToString().TrimEnd();
        }

        foreach (var record in records.OrderBy(r => KindRank(r.Kind)).ThenByDescending(r => r.Updated))
            AppendRecord(builder, record, similarity: null);

        builder.AppendLine(Messages.RecallHint);

        return builder.ToString().TrimEnd();
    }

    /// <summary>The experience briefing of an init response; empty inputs render nothing but the hint.</summary>
    public static string RenderBriefing(
        IReadOnlyList<(string Project, double Similarity)> similarProjects,
        IReadOnlyList<ScoredRecord> experience,
        IReadOnlyList<string> notes)
    {
        if (similarProjects.Count == 0 && experience.Count == 0 && notes.Count == 0)
            return Messages.BriefingHint;

        var builder = new StringBuilder();
        builder.AppendLine(Messages.BriefingTitle);
        builder.AppendLine();

        foreach (var note in notes)
            builder.AppendLine(note);

        if (similarProjects.Count > 0)
        {
            builder.AppendLine(Messages.SimilarProjectsTitle);

            foreach (var (project, similarity) in similarProjects)
                builder.Append("- **").Append(project).Append("** · similarity ")
                    .AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0:0.00}", similarity)
                    .AppendLine();

            builder.AppendLine();
        }

        if (experience.Count > 0)
        {
            builder.AppendLine(Messages.ExperienceTitle);
            builder.AppendLine();

            foreach (var scored in experience)
                AppendRecord(builder, scored.Record, scored.Similarity);
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>The review listing: stale durable records and stale tasks as one-line candidates.</summary>
    public static string RenderReview(
        IReadOnlyList<MemoryRecord> staleDurable, IReadOnlyList<MemoryRecord> staleTasks, int staleDays)
    {
        var builder = new StringBuilder();
        builder.AppendLine(Messages.ReviewTitle);
        builder.AppendLine();

        if (staleDurable.Count == 0 && staleTasks.Count == 0)
        {
            builder.AppendLine(Messages.ReviewEmpty);

            return builder.ToString().TrimEnd();
        }

        AppendCandidates(builder, Messages.ReviewStaleTitle(staleDays), staleDurable);
        AppendCandidates(builder, Messages.ReviewTasksTitle(staleDays), staleTasks);

        builder.AppendLine(Messages.ReviewHint);

        return builder.ToString().TrimEnd();
    }

    /// <summary>The edge table around the given title; an empty edge list renders the empty-graph hint.</summary>
    public static string RenderGraph(string title, IReadOnlyList<GraphEdge> edges)
    {
        var builder = new StringBuilder();
        builder.AppendLine(title);
        builder.AppendLine();

        if (edges.Count == 0)
        {
            builder.AppendLine(Messages.GraphEmpty);

            return builder.ToString().TrimEnd();
        }

        var projects = edges.SelectMany(edge => new[] { edge.Source, edge.Target })
            .Distinct(ProjectIdentity.Comparer).Count();

        builder.AppendLine(Messages.GraphStats(edges.Count, projects));
        builder.AppendLine();
        builder.AppendLine(Messages.GraphTableHeader);
        builder.AppendLine(Messages.GraphTableSeparator);

        foreach (var edge in edges.OrderBy(e => e.Source, ProjectIdentity.Comparer).ThenBy(e => e.Type))
            builder.Append("| ").Append(edge.Source)
                .Append(" | ").Append(LinkTypes.Describe(edge.Type))
                .Append(" | ").Append(edge.Target)
                .Append(" | ").Append(Cell(edge.Note))
                .AppendLine(" |");

        return builder.ToString().TrimEnd();
    }

    /// <summary>The pending link suggestions queue: one line per pair, closed by the resolution hint.</summary>
    public static string RenderPending(IReadOnlyList<PendingLink> pending)
    {
        var builder = new StringBuilder();
        builder.AppendLine(Messages.PendingTitle);

        foreach (var link in pending)
        {
            builder.Append("- **").Append(link.Project).Append("** ↔ **").Append(link.Other).Append("**");

            if (!string.IsNullOrWhiteSpace(link.Evidence))
                builder.Append(" — ").Append(link.Evidence);

            builder.Append(" · noticed ").Append(Date(link.Updated)).AppendLine();
        }

        builder.AppendLine();
        builder.Append(Messages.PendingHint);

        return builder.ToString();
    }

    /// <summary>One record as a Markdown block with its id handle.</summary>
    public static void AppendRecord(StringBuilder builder, MemoryRecord record, double? similarity)
    {
        builder.Append("### [").Append(record.Kind).Append(']');

        if (!string.IsNullOrWhiteSpace(record.Title))
            builder.Append(' ').Append(record.Title);

        builder.Append(" — ").Append(record.Project).Append(" · ").Append(Date(record.Updated));

        if (similarity is { } value)
            builder.Append(" · similarity ")
                .AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0:0.00}", value);

        builder.AppendLine();
        builder.Append("id: `").Append(record.Id).AppendLine("`");
        builder.AppendLine(record.Content.Trim());
        builder.AppendLine();
    }

    /// <summary>Scope wording of a recall: a project name or the whole memory.</summary>
    public static string Scope(string? project) =>
        string.IsNullOrWhiteSpace(project) ? Messages.ScopeWholeMemory : $"project '{project.Trim()}'";

    /// <summary>Kind wording of a recall: the kind or "any".</summary>
    public static string KindLabel(string? kind) => string.IsNullOrWhiteSpace(kind) ? Messages.AnyKind : kind;

    private static void AppendCandidates(StringBuilder builder, string title, IReadOnlyList<MemoryRecord> records)
    {
        if (records.Count == 0)
            return;

        builder.AppendLine(title);

        foreach (var record in records)
            builder.Append("- [").Append(record.Kind).Append("] ")
                .Append(Label(record))
                .Append(" — ").Append(record.Project)
                .Append(" · created ").Append(Date(record.Created))
                .Append(" · id `").Append(record.Id).AppendLine("`");

        builder.AppendLine();
    }

    /// <summary>A one-line handle of a record: its title, or a snippet of the content.</summary>
    internal static string Label(MemoryRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.Title))
            return record.Title;

        var snippet = record.Content.Trim().ReplaceLineEndings(" ");

        return snippet.Length <= 80 ? snippet : $"{snippet[..80]}…";
    }

    private static int KindRank(string kind)
    {
        var index = RecordKinds.Canonical.ToList().IndexOf(kind);

        return index < 0 ? RecordKinds.Canonical.Count : index;
    }

    private static string Date(DateTimeOffset instant) => instant.ToString(DateFormat);

    private static string Cell(string value) => value.Replace("|", "\\|").Replace("\n", " ");
}
