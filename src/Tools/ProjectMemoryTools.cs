using System.ComponentModel;
using ModelContextProtocol.Server;
using Snail.MCP.Memory.Application;
using Snail.MCP.Memory.Core.Constants;
using Snail.MCP.Memory.Infrastructure.Configuration;

namespace Snail.MCP.Memory.Tools;

/// <summary>MCP tools of the record memory.</summary>
/// <remarks>Service parameters come from DI and never reach the JSON schema; the description text lives in <see cref="ToolDescriptions"/>.</remarks>
[McpServerToolType]
public static class ProjectMemoryTools
{
    /// <summary>Initializes a project's memory and returns the experience briefing.</summary>
    [McpServerTool, Description(ToolDescriptions.InitProjectMemory)]
    public static Task<string> InitProjectMemory(
        ProjectService service,
        [Description(ToolDescriptions.NewProjectNameParam)] string projectName = "",
        [Description("Short description of what the project will be — it is embedded and finds similar projects. Optional.")] string description = "",
        [Description("Existing project names to preload experience from, joined by '+'. Optional.")] string seedFrom = "")
    {
        if (!string.IsNullOrWhiteSpace(projectName))
            return service.InitAsync(projectName, description, seedFrom);

        var workingDirectory = Directory.GetCurrentDirectory();
        var fromFolder = DefaultProjectName.FromDirectory(workingDirectory, ServerPaths.Home);

        return string.IsNullOrEmpty(fromFolder)
            ? Task.FromResult(Messages.ProjectNameNotResolved(workingDirectory))
            : service.InitAsync(fromFolder, description, seedFrom);
    }

    /// <summary>Lists the stored projects, optionally filtered by a name prefix.</summary>
    [McpServerTool, Description(ToolDescriptions.ListProjects)]
    public static Task<string> ListProjects(
        ProjectService service,
        [Description(ToolDescriptions.PrefixParam)] string prefix = "") =>
        service.ListAsync(prefix);

    /// <summary>Stores one record, with duplicate detection and supersession.</summary>
    [McpServerTool, Description(ToolDescriptions.Remember)]
    public static Task<string> Remember(
        MemoryService service,
        [Description(ToolDescriptions.ProjectNameParam)] string projectName,
        [Description("The knowledge itself, in the language of the conversation.")] string content,
        [Description(ToolDescriptions.KindParam)] string kind = "",
        [Description("Short label of the record. Optional.")] string title = "",
        [Description("Id of the record this one supersedes; the old record is deleted. Optional.")] string replaceId = "",
        [Description("Store even when a very similar record exists.")] bool force = false) =>
        service.RememberAsync(projectName, content, kind, title, replaceId, force);

    /// <summary>Searches memory by meaning; empty query renders an overview.</summary>
    [McpServerTool, Description(ToolDescriptions.Recall)]
    public static Task<string> Recall(
        MemoryService service,
        [Description("What to look for, in natural language. Empty — an overview of the newest records.")] string query = "",
        [Description(ToolDescriptions.ProjectNameParam + " Empty — the whole memory (transfers experience across projects).")] string projectName = "",
        [Description(ToolDescriptions.KindParam)] string kind = "",
        [Description("How many records to return (1-50).")] int limit = 8) =>
        service.RecallAsync(query, projectName, kind, limit);

    /// <summary>Deletes records by id.</summary>
    [McpServerTool, Description(ToolDescriptions.Forget)]
    public static Task<string> Forget(
        MemoryService service,
        [Description("Record id, or several ids comma-separated.")] string ids) =>
        service.ForgetAsync(ids);

    /// <summary>Nominates stale records for deletion.</summary>
    [McpServerTool, Description(ToolDescriptions.ReviewMemory)]
    public static Task<string> ReviewMemory(
        MemoryService service,
        [Description(ToolDescriptions.ProjectNameParam + " Empty — the whole memory.")] string projectName = "") =>
        service.ReviewAsync(projectName);

    /// <summary>Renames a project across records, edges and the registry.</summary>
    [McpServerTool, Description(ToolDescriptions.RenameProject)]
    public static Task<string> RenameProject(
        ProjectService service,
        [Description("Current name of the project.")] string oldName,
        [Description("New name of the project.")] string newName) =>
        service.RenameAsync(oldName, newName);
}
