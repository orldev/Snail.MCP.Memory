using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Snail.MCP.Memory.Core.Graph;

/// <summary>An edge requested together with a record — the atomic companion of <c>link_projects</c>.</summary>
/// <remarks>The source of the edge is the record's own project; only the target travels here.</remarks>
public sealed record RecordLink(
    [property: JsonPropertyName("to"), Description("Target project of the edge; the source is the record's project.")]
    string To,
    [property: JsonPropertyName("type"), Description("Edge type; directed from the record's project to 'to'.")]
    LinkType Type,
    [property: JsonPropertyName("note"), Description("What exactly connects them — one line. Optional.")]
    string Note = "");
