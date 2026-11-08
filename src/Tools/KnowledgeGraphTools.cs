using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Memory.Application;
using Snail.Sdk.Mcp.Hosting.Answers;

namespace Snail.MCP.Memory.Tools;

/// <summary>MCP tools for the directed knowledge graph.</summary>
/// <remarks>Nodes are whole projects; symmetric similarity is recall's business, not the graph's.</remarks>
[McpServerToolType]
public static class KnowledgeGraphTools
{
    /// <summary>Creates or updates a directed, typed edge between two projects.</summary>
    [McpServerTool(Name = "link_projects", ReadOnly = false, Destructive = false), Description(ToolDescriptions.LinkProjects)]
    public static async Task<CallToolResult> LinkProjectsAsync(
        KnowledgeGraph service,
        CancellationToken cancellationToken,
        [Description("Source project of the directed edge.")] string from,
        [Description("Target project of the directed edge.")] string to,
        [Description(ToolDescriptions.LinkTypeParam)] LinkType type,
        [Description("What exactly connects them — one line. Optional.")] string note = "") =>
        ToolAnswer.From(await service.LinkAsync(from, to, type.Wire(), note, cancellationToken), AnswerFields.Of);

    /// <summary>Rejects a pending link suggestion so the pair is never proposed again.</summary>
    [McpServerTool(Name = "dismiss_link", ReadOnly = false, Destructive = false), Description(ToolDescriptions.DismissLink)]
    public static async Task<CallToolResult> DismissLinkAsync(
        KnowledgeGraph service,
        CancellationToken cancellationToken,
        [Description("First project of the suggested pair.")] string from,
        [Description("Second project of the suggested pair.")] string to) =>
        ToolAnswer.From(await service.DismissAsync(from, to, cancellationToken), AnswerFields.Of);

    /// <summary>Removes edges between two projects regardless of direction.</summary>
    [McpServerTool(Name = "unlink_projects", ReadOnly = false, Destructive = true), Description(ToolDescriptions.UnlinkProjects)]
    public static async Task<CallToolResult> UnlinkProjectsAsync(
        KnowledgeGraph service,
        CancellationToken cancellationToken,
        [Description("First project.")] string from,
        [Description("Second project.")] string to,
        [Description("Edge type. Empty — remove every edge between these projects.")] string type = "") =>
        ToolAnswer.From(await service.UnlinkAsync(from, to, type, cancellationToken), AnswerFields.Of);

    /// <summary>Upserts machine-extracted relations as auto edges.</summary>
    [McpServerTool(Name = "sync_project_links", ReadOnly = false, Destructive = false), Description(ToolDescriptions.SyncProjectLinks)]
    public static async Task<CallToolResult> SyncProjectLinksAsync(
        KnowledgeGraph service,
        CancellationToken cancellationToken,
        [Description(ToolDescriptions.ProjectNameParam)] string projectName,
        [Description("The relations extracted from the source, each directed from this project to its 'to'.")] RecordLink[] links,
        [Description("What was parsed to get them — csproj, package.json, obsidian, xml, … — stamped into the auto note.")] string source) =>
        ToolAnswer.From(await service.SyncAsync(projectName, links, source, cancellationToken), AnswerFields.Of);

    /// <summary>The edges around the selected projects, without loading records.</summary>
    [McpServerTool(Name = "get_graph", ReadOnly = true), Description(ToolDescriptions.GetGraph)]
    public static async Task<CallToolResult> GetGraphAsync(
        KnowledgeGraph service,
        CancellationToken cancellationToken,
        [Description("Project name, or several joined by '+'. Empty — the whole graph.")] string projectName = "",
        [Description("How many edge hops to expand (1-5).")] int depth = 1) =>
        ToolAnswer.Ok(AnswerFields.Of(await service.ReadAsync(projectName, depth, cancellationToken)));
}
