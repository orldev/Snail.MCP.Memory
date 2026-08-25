using System.ComponentModel;
using ModelContextProtocol.Server;
using Snail.MCP.Memory.Application;

namespace Snail.MCP.Memory.Tools;

/// <summary>MCP tools for the directed knowledge graph.</summary>
/// <remarks>Nodes are whole projects; symmetric similarity is recall's business, not the graph's.</remarks>
[McpServerToolType]
public static class KnowledgeGraphTools
{
    /// <summary>Creates or updates a directed, typed edge between two projects.</summary>
    [McpServerTool, Description(ToolDescriptions.LinkProjects)]
    public static Task<string> LinkProjects(
        GraphService service,
        [Description("Source project of the directed edge.")] string from,
        [Description("Target project of the directed edge.")] string to,
        [Description(ToolDescriptions.LinkTypeParam)] LinkType type,
        [Description("What exactly connects them — one line. Optional.")] string note = "") =>
        service.LinkAsync(from, to, type.Wire(), note);

    /// <summary>Removes edges between two projects regardless of direction.</summary>
    [McpServerTool, Description(ToolDescriptions.UnlinkProjects)]
    public static Task<string> UnlinkProjects(
        GraphService service,
        [Description("First project.")] string from,
        [Description("Second project.")] string to,
        [Description("Edge type. Empty — remove every edge between these projects.")] string type = "") =>
        service.UnlinkAsync(from, to, type);

    /// <summary>Renders the edges around the selected projects without loading records.</summary>
    [McpServerTool, Description(ToolDescriptions.GetGraph)]
    public static Task<string> GetGraph(
        GraphService service,
        [Description("Project name, or several joined by '+'. Empty — the whole graph.")] string projectName = "",
        [Description("How many edge hops to expand (1-5).")] int depth = 1) =>
        service.RenderAsync(projectName, depth);
}
