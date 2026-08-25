using System.Text.Json;
using ModelContextProtocol.Server;
using Snail.MCP.Memory.Tools;

namespace Snail.MCP.Memory.Tests.Tools;

/// The closed vocabularies are not only described in prose — where the parameter type allows it
/// they are part of the JSON schema itself, so a client rejects an invented value before the
/// server ever sees it. These tests pin the schema the model actually receives.
public sealed class ToolSchemaTests
{
    [Fact]
    public void LinkProjects_TypeIsAStringEnumOfEveryAllowedType()
    {
        var schema = InputSchemaOf(nameof(KnowledgeGraphTools.LinkProjects));
        var type = schema.GetProperty("properties").GetProperty("type");
        var values = type.GetProperty("enum").EnumerateArray().Select(v => v.GetString()).ToHashSet();

        Assert.Equal(LinkTypes.All.ToHashSet(), values!);
    }

    /// The symmetric types are retired: an enum that named them would invite the model to
    /// store similarity the vectors already derive.
    [Theory]
    [InlineData("related_to")]
    [InlineData("co_access")]
    public void LinkProjects_EnumDoesNotAcceptTheRetiredTypes(string retired)
    {
        var type = InputSchemaOf(nameof(KnowledgeGraphTools.LinkProjects))
            .GetProperty("properties").GetProperty("type");

        Assert.DoesNotContain(retired, type.GetProperty("enum").EnumerateArray().Select(v => v.GetString()));
    }

    /// The wire mapping is the single road from the schema enum to the stored vocabulary;
    /// a new enum member without a wire name would only surface at runtime.
    [Fact]
    public void EveryLinkTypeMapsOntoTheAllowedVocabulary()
    {
        var wires = Enum.GetValues<LinkType>().Select(t => t.Wire()).ToList();

        Assert.Equal(LinkTypes.All, wires);
    }

    private static JsonElement InputSchemaOf(string method) =>
        McpServerTool.Create(typeof(KnowledgeGraphTools).GetMethod(method)!).ProtocolTool.InputSchema;
}
