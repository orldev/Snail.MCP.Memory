using System.ComponentModel;
using ModelContextProtocol.Server;
using Snail.MCP.Memory.Application;
using Snail.MCP.Memory.Core.Constants;
using Snail.Sdk.Mcp.Hosting;

namespace Snail.MCP.Memory.Tools;

/// <summary>MCP tools of the record memory.</summary>
/// <remarks>Service parameters come from DI and never reach the JSON schema; the description text lives in <see cref="ToolDescriptions"/>.</remarks>
[McpServerToolType]
public static class ProjectMemoryTools
{
    /// <summary>Initializes a project's memory and returns the experience briefing.</summary>
    [McpServerTool(Name = "init_project_memory", ReadOnly = false, Destructive = false), Description(ToolDescriptions.InitProjectMemory)]
    public static async Task<string> InitProjectMemoryAsync(
        ProjectRegistry service,
        McpServer server,
        CancellationToken cancellationToken,
        [Description(ToolDescriptions.NewProjectNameParam)] string projectName = "",
        [Description("Short description of what the project will be — it is embedded and finds similar projects. Optional.")] string description = "",
        [Description("Existing project names to preload experience from, joined by '+'. Optional.")] string seedFrom = "")
    {
        if (!string.IsNullOrWhiteSpace(projectName))
            return await service.InitAsync(projectName, description, seedFrom);

        var workingDirectory = await ClientFolder.FindAsync(server, cancellationToken) ?? Directory.GetCurrentDirectory();
        var fromFolder = DefaultProjectName.FromDirectory(workingDirectory, ServerFolders.Home);

        return string.IsNullOrEmpty(fromFolder)
            ? Messages.ProjectNameNotResolved(workingDirectory)
            : await service.InitAsync(fromFolder, description, seedFrom);
    }

    /// <summary>Lists the stored projects, optionally filtered by a name prefix.</summary>
    [McpServerTool(ReadOnly = true), Description(ToolDescriptions.ListProjects)]
    public static Task<string> ListProjects(
        ProjectRegistry service,
        [Description(ToolDescriptions.PrefixParam)] string prefix = "") =>
        service.ListAsync(prefix);

    /// <summary>Stores one record, with duplicate detection, supersession and atomic edges.</summary>
    [McpServerTool(ReadOnly = false, Destructive = true), Description(ToolDescriptions.Remember)]
    public static Task<string> Remember(
        RecordLifecycle service,
        [Description(ToolDescriptions.ProjectNameParam)] string projectName,
        [Description("The knowledge itself, in the language of the conversation.")] string content,
        [Description(ToolDescriptions.KindParam)] string kind = "",
        [Description("Short label of the record. Optional.")] string title = "",
        [Description("Id of the record this one supersedes; the old record is deleted. Optional.")] string replaceId = "",
        [Description("Store even when a very similar record exists.")] bool force = false,
        [Description(ToolDescriptions.LinksParam)] RecordLink[]? links = null) =>
        service.RememberAsync(projectName, content, kind, title, replaceId, force, links);

    /// <summary>Searches memory by meaning; empty query renders an overview.</summary>
    [McpServerTool(ReadOnly = false, Destructive = false), Description(ToolDescriptions.Recall)]
    public static Task<string> Recall(
        RecordLifecycle service,
        [Description("What to look for, in natural language. Empty — an overview of the newest records.")] string query = "",
        [Description(ToolDescriptions.ProjectNameParam + " Empty — the whole memory (transfers experience across projects).")] string projectName = "",
        [Description(ToolDescriptions.KindParam)] string kind = "",
        [Description("How many records to return (1-50).")] int limit = 8) =>
        service.RecallAsync(query, projectName, kind, limit);

    /// <summary>Deletes records by id.</summary>
    [McpServerTool(ReadOnly = false, Destructive = true), Description(ToolDescriptions.Forget)]
    public static Task<string> Forget(
        RecordLifecycle service,
        [Description("Record id, or several ids comma-separated.")] string ids) =>
        service.ForgetAsync(ids);

    /// <summary>Nominates stale records for deletion.</summary>
    [McpServerTool(ReadOnly = true), Description(ToolDescriptions.ReviewMemory)]
    public static Task<string> ReviewMemory(
        RecordLifecycle service,
        [Description(ToolDescriptions.ProjectNameParam + " Empty — the whole memory.")] string projectName = "") =>
        service.ReviewAsync(projectName);

    /// <summary>Renames a project across records, edges and the registry.</summary>
    [McpServerTool(ReadOnly = false, Destructive = true), Description(ToolDescriptions.RenameProject)]
    public static Task<string> RenameProject(
        ProjectRegistry service,
        [Description("Current name of the project.")] string oldName,
        [Description("New name of the project.")] string newName) =>
        service.RenameAsync(oldName, newName);
}
