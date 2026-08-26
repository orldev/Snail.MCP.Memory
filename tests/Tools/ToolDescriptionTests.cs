using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Server;
using Snail.MCP.Memory.Tools;

namespace Snail.MCP.Memory.Tests.Tools;

/// A user who installs the package from NuGet gets no prompt file and no slash commands: the tool
/// schema is the whole instruction. So the descriptions quote things that live elsewhere in the
/// code — the kind vocabulary, the refused folders — and a quote drifts silently. These tests
/// fail when it does.
public sealed class ToolDescriptionTests
{
    private static readonly Type[] ToolTypes = [typeof(ProjectMemoryTools), typeof(KnowledgeGraphTools)];

    [Fact]
    public void KindParam_NamesEveryCanonicalKind()
    {
        foreach (var kind in RecordKinds.Canonical)
            Assert.Contains(kind, ToolDescriptions.KindParam);
    }

    /// The description promises the model that projectName can be omitted. If the parameter
    /// ever loses its default, that promise turns into a schema error on every call.
    [Fact]
    public void InitProjectMemory_LeavesProjectNameOptionalAsTheDescriptionPromises()
    {
        var projectName = typeof(ProjectMemoryTools)
            .GetMethod(nameof(ProjectMemoryTools.InitProjectMemory))!
            .GetParameters().Single(p => p.Name == "projectName");

        Assert.True(projectName.IsOptional);
        Assert.Contains("when omitted", ToolDescriptions.NewProjectNameParam);
    }

    /// Listing the whole memory is the common call and the description says so ("Empty — every
    /// project"); a required prefix would turn every such call into a schema error.
    [Fact]
    public void ListProjects_LeavesThePrefixOptionalAsTheDescriptionPromises()
    {
        var prefix = typeof(ProjectMemoryTools)
            .GetMethod(nameof(ProjectMemoryTools.ListProjects))!
            .GetParameters().Single(p => p.Name == "prefix");

        Assert.True(prefix.IsOptional);
        Assert.Equal("", prefix.DefaultValue);
        Assert.Contains("Empty — every project", ToolDescriptions.PrefixParam);
    }

    /// Recall's whole point is the cross-project default; a required query or projectName would
    /// break both the overview call and the experience-transfer call.
    [Fact]
    public void Recall_LeavesQueryAndProjectNameOptional()
    {
        var parameters = typeof(ProjectMemoryTools)
            .GetMethod(nameof(ProjectMemoryTools.Recall))!
            .GetParameters();

        Assert.True(parameters.Single(p => p.Name == "query").IsOptional);
        Assert.True(parameters.Single(p => p.Name == "projectName").IsOptional);
    }

    /// The folders named as examples of a refused container have to be folders the resolver
    /// really refuses — otherwise the model is told to pass a name where none is needed.
    [Theory]
    [InlineData("design")]
    [InlineData("projects")]
    [InlineData("work")]
    [InlineData("src")]
    public void NewProjectNameParam_QuotesFoldersTheResolverActuallyRefuses(string folder)
    {
        Assert.Contains(folder, ToolDescriptions.NewProjectNameParam);
        Assert.True(DefaultProjectName.IsGeneric(folder), $"'{folder}' is quoted as generic but is not.");
    }

    [Fact]
    public void EveryToolAndEveryModelFacingArgument_CarriesADescription()
    {
        foreach (var method in ToolTypes.SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                     .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null))
        {
            Assert.False(string.IsNullOrWhiteSpace(method.GetCustomAttribute<DescriptionAttribute>()?.Description),
                $"{method.Name} has no tool description.");

            foreach (var parameter in method.GetParameters().Where(IsModelFacing))
                Assert.False(string.IsNullOrWhiteSpace(parameter.GetCustomAttribute<DescriptionAttribute>()?.Description),
                    $"{method.Name}.{parameter.Name} has no description.");
        }
    }

    /// The atomic form must stay optional — a required links array would break every plain
    /// remember call — and the queue's own hint has to name the only two ways out of it.
    [Fact]
    public void Remember_LeavesLinksOptionalAndThePendingHintNamesBothResolutions()
    {
        var links = typeof(ProjectMemoryTools)
            .GetMethod(nameof(ProjectMemoryTools.Remember))!
            .GetParameters().Single(p => p.Name == "links");

        Assert.True(links.IsOptional);
        Assert.Contains("link_projects", Messages.PendingHint);
        Assert.Contains("dismiss_link", Messages.PendingHint);
    }

    /// Services and the clock are injected by the host and never reach the schema; the rest
    /// are arguments the model has to fill in blind.
    private static bool IsModelFacing(ParameterInfo parameter) =>
        parameter.ParameterType == typeof(string) ||
        parameter.ParameterType == typeof(bool) ||
        parameter.ParameterType == typeof(int) ||
        parameter.ParameterType.IsEnum ||
        parameter.ParameterType.IsArray;
}
