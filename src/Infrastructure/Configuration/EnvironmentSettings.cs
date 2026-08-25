using System.Collections;
using Microsoft.Extensions.Configuration;

namespace Snail.MCP.Memory.Infrastructure.Configuration;

/// <summary>
/// Maps <c>SNAIL_MCP_MEMORY_SECTION__FIELD_NAME</c> variables onto configuration keys
/// <c>Section:FieldName</c>.
/// </summary>
/// <remarks>
/// Only prefixed variables are read: a bare name like <c>Directory</c> collides with whatever
/// else the client process exports.
/// </remarks>
public static class EnvironmentSettings
{
    /// <summary>Variable name prefix of this server.</summary>
    public const string Prefix = "SNAIL_MCP_MEMORY_";

    /// <summary>Variable pointing at an explicit config file.</summary>
    public const string ConfigPathVariable = Prefix + "CONFIG";

    /// <summary>Adds the prefixed variables of the current process as a configuration source.</summary>
    public static IConfigurationBuilder AddEnvironment(this IConfigurationBuilder builder) =>
        builder.AddInMemoryCollection(Read(Environment.GetEnvironmentVariables()));

    internal static IEnumerable<KeyValuePair<string, string?>> Read(IDictionary variables)
    {
        foreach (DictionaryEntry entry in variables)
        {
            if (entry.Key is not string name || !name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (entry.Value is not string value || string.IsNullOrWhiteSpace(value)) continue;
            if (name.Equals(ConfigPathVariable, StringComparison.OrdinalIgnoreCase)) continue;

            yield return new KeyValuePair<string, string?>(ToConfigurationKey(name[Prefix.Length..]), value.Trim());
        }
    }

    /// <summary><c>SIZE_LIMIT_KILOBYTES</c> → <c>SizeLimitKilobytes</c>, <c>A__B</c> → <c>A:B</c>; binding is case-insensitive.</summary>
    internal static string ToConfigurationKey(string variable) =>
        string.Join(':', variable.Split("__").Select(segment => segment.Replace("_", "")));
}
