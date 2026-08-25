using System.Text.Json.Serialization;

namespace Snail.MCP.Memory.Core.Graph;

/// <summary>The edge vocabulary as a schema-level contract: a JSON string enum rejects invented types before they reach the server.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LinkType>))]
public enum LinkType
{
    /// <summary>Source uses target.</summary>
    [JsonStringEnumMemberName(LinkTypes.Uses)] Uses,

    /// <summary>Source depends on target.</summary>
    [JsonStringEnumMemberName(LinkTypes.DependsOn)] DependsOn,

    /// <summary>Source is the design of target.</summary>
    [JsonStringEnumMemberName(LinkTypes.DesignOf)] DesignOf,

    /// <summary>Source is part of target.</summary>
    [JsonStringEnumMemberName(LinkTypes.PartOf)] PartOf,

    /// <summary>Source implements target.</summary>
    [JsonStringEnumMemberName(LinkTypes.Implements)] Implements,

    /// <summary>Source documents target.</summary>
    [JsonStringEnumMemberName(LinkTypes.Documents)] Documents
}

/// <summary>Mapping from the schema enum back to the wire vocabulary of <see cref="LinkTypes"/>.</summary>
public static class LinkTypeExtensions
{
    /// <summary>The stored type string of the edge.</summary>
    public static string Wire(this LinkType type) => type switch
    {
        LinkType.Uses => LinkTypes.Uses,
        LinkType.DependsOn => LinkTypes.DependsOn,
        LinkType.DesignOf => LinkTypes.DesignOf,
        LinkType.PartOf => LinkTypes.PartOf,
        LinkType.Implements => LinkTypes.Implements,
        LinkType.Documents => LinkTypes.Documents,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
