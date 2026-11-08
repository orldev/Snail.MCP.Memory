using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Memory.Application;
using Snail.MCP.Memory.Core.Constants;
using Snail.Sdk.Mcp.Hosting;
using Snail.Sdk.Mcp.Hosting.Answers;

namespace Snail.MCP.Memory.Tools;

/// <summary>MCP tools of the record memory.</summary>
/// <remarks>
/// Service parameters come from DI and never reach the JSON schema; the description text lives in <see cref="ToolDescriptions"/>.
/// Every tool answers in the envelope of <see cref="ToolAnswer"/>, its data mapped by <see cref="AnswerFields"/>.
/// </remarks>
[McpServerToolType]
public static class ProjectMemoryTools
{
    /// <summary>Initializes a project's memory and returns the experience briefing.</summary>
    [McpServerTool(Name = "init_project_memory", ReadOnly = false, Destructive = false), Description(ToolDescriptions.InitProjectMemory)]
    public static async Task<CallToolResult> InitProjectMemoryAsync(
        ProjectRegistry service,
        McpServer server,
        CancellationToken cancellationToken,
        [Description(ToolDescriptions.NewProjectNameParam)] string projectName = "",
        [Description("Short description of what the project will be — it is embedded and finds similar projects. Optional.")] string description = "",
        [Description("Existing project names to preload experience from, joined by '+'. Optional.")] string seedFrom = "")
    {
        if (!string.IsNullOrWhiteSpace(projectName))
            return ToolAnswer.From(await service.InitAsync(projectName, description, seedFrom, cancellationToken), AnswerFields.Of);

        var workingDirectory = await ClientFolder.FindAsync(server, cancellationToken) ?? Directory.GetCurrentDirectory();
        var fromFolder = DefaultProjectName.FromDirectory(workingDirectory, ServerFolders.Home);

        if (string.IsNullOrEmpty(fromFolder))
            return ToolAnswer.Failed(Messages.ProjectNameNotResolved(workingDirectory), Messages.ProjectNameNotResolvedHint, Refusals.UnresolvedName);

        return ToolAnswer.From(await service.InitAsync(fromFolder, description, seedFrom, cancellationToken), AnswerFields.Of);
    }

    /// <summary>Lists the stored projects, optionally filtered by a name prefix.</summary>
    [McpServerTool(Name = "list_projects", ReadOnly = true), Description(ToolDescriptions.ListProjects)]
    public static async Task<CallToolResult> ListProjectsAsync(
        ProjectRegistry service,
        CancellationToken cancellationToken,
        [Description(ToolDescriptions.PrefixParam)] string prefix = "") =>
        ToolAnswer.Ok(AnswerFields.Of(await service.ListAsync(prefix, cancellationToken)));

    /// <summary>Stores one record, with duplicate detection, supersession and atomic edges.</summary>
    [McpServerTool(Name = "remember", ReadOnly = false, Destructive = true), Description(ToolDescriptions.Remember)]
    public static async Task<CallToolResult> RememberAsync(
        RecordLifecycle service,
        CancellationToken cancellationToken,
        [Description(ToolDescriptions.ProjectNameParam)] string projectName,
        [Description("The knowledge itself, in the language of the conversation.")] string content,
        [Description(ToolDescriptions.KindParam)] string kind = "",
        [Description("Short label of the record. Optional.")] string title = "",
        [Description("Id of the record this one supersedes; the old record is deleted. Optional.")] string replaceId = "",
        [Description("Store even when a very similar record exists.")] bool force = false,
        [Description(ToolDescriptions.LinksParam)] RecordLink[]? links = null) =>
        ToolAnswer.From(
            await service.RememberAsync(projectName, content, kind, title, replaceId, force, links, AnswerFields.Of, cancellationToken),
            AnswerFields.Of);

    /// <summary>Searches memory by meaning; empty query answers an overview.</summary>
    [McpServerTool(Name = "recall", ReadOnly = false, Destructive = false), Description(ToolDescriptions.Recall)]
    public static async Task<CallToolResult> RecallAsync(
        RecordLifecycle service,
        CancellationToken cancellationToken,
        [Description("What to look for, in natural language. Empty — an overview of the newest records.")] string query = "",
        [Description(ToolDescriptions.ProjectNameParam + " Empty — the whole memory (transfers experience across projects).")] string projectName = "",
        [Description(ToolDescriptions.KindParam)] string kind = "",
        [Description("How many records to return (1-50).")] int limit = 8) =>
        ToolAnswer.From(await service.RecallAsync(query, projectName, kind, limit, cancellationToken), AnswerFields.Of);

    /// <summary>Deletes records by id.</summary>
    [McpServerTool(Name = "forget", ReadOnly = false, Destructive = true), Description(ToolDescriptions.Forget)]
    public static async Task<CallToolResult> ForgetAsync(
        RecordLifecycle service,
        CancellationToken cancellationToken,
        [Description("Record id, or several ids comma-separated.")] string ids) =>
        ToolAnswer.From(await service.ForgetAsync(ids, cancellationToken), AnswerFields.Of);

    /// <summary>Nominates stale records for deletion.</summary>
    [McpServerTool(Name = "review_memory", ReadOnly = true), Description(ToolDescriptions.ReviewMemory)]
    public static async Task<CallToolResult> ReviewMemoryAsync(
        RecordLifecycle service,
        CancellationToken cancellationToken,
        [Description(ToolDescriptions.ProjectNameParam + " Empty — the whole memory.")] string projectName = "") =>
        ToolAnswer.Ok(AnswerFields.Of(await service.ReviewAsync(projectName, cancellationToken)));

    /// <summary>Renames a project across records, edges and the registry.</summary>
    [McpServerTool(Name = "rename_project", ReadOnly = false, Destructive = true), Description(ToolDescriptions.RenameProject)]
    public static async Task<CallToolResult> RenameProjectAsync(
        ProjectRegistry service,
        CancellationToken cancellationToken,
        [Description("Current name of the project.")] string oldName,
        [Description("New name of the project.")] string newName) =>
        ToolAnswer.From(await service.RenameAsync(oldName, newName, cancellationToken), AnswerFields.Of);
}
