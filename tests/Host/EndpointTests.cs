using Microsoft.Extensions.Configuration;
using Snail.MCP.Memory.Infrastructure.Configuration;
using Snail.MCP.Memory.Tests.TestDoubles;
using Snail.Sdk.Mcp.Hosting;

namespace Snail.MCP.Memory.Tests.Host;

/// The endpoint has no key and the memory holds everything a user told an agent: the HTTP server listens on loopback
/// whatever the settings say, and a setting that would serve it to the network stops the start, naming itself.
public sealed class EndpointTests
{
    [Fact]
    public void Read_HttpWithNoAddress_ListensOnTheLoopbackDefault()
    {
        var endpoint = Read(new() { ["Transport"] = "Http" });

        Assert.Equal(McpTransport.Http, endpoint.Transport);
        Assert.Equal(StorageOptions.DefaultUrl, endpoint.Url);
        Assert.False(endpoint.ListensOnEveryInterface);
    }

    [Fact]
    public void Read_NeitherTransportNorPort_IsStdio()
    {
        Assert.Equal(McpTransport.Stdio, Read([]).Transport);
    }

    /// <summary>Read in the test's process, whose entry assembly is the test runner, a refusal still names the variable: the options name the server.</summary>
    [Theory]
    [InlineData("http://0.0.0.0:9230")]
    [InlineData("http://192.168.1.20:9230")]
    public void Read_AnAddressBeyondLoopback_IsRefused(string url)
    {
        var refusal = Assert.Throws<StartRefusedException>(() => Read(new() { ["Transport"] = "Http", ["Url"] = url }));

        Assert.StartsWith($"SNAIL_MCP_MEMORY_URL (or Url in the settings file) is '{url}'", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_ListensOnEveryInterface_IsRefused()
    {
        var refusal = Assert.Throws<StartRefusedException>(() =>
            Read(new() { ["Transport"] = "Http", ["ListensOnEveryInterface"] = "true" }));

        Assert.StartsWith(
            "SNAIL_MCP_MEMORY_LISTENS_ON_EVERY_INTERFACE (or ListensOnEveryInterface in the settings file) is 'true'",
            refusal.Message,
            StringComparison.Ordinal);
    }

    /// <summary>The program itself names the variable its operator set in one line, exits with 1 rather than crashing, and never starts listening.</summary>
    [Fact]
    public async Task Program_ToldToListenOnEveryInterface_ExitsNamingTheVariable()
    {
        var exited = await RefusedStart.RunAsync(new Dictionary<string, string?>
        {
            ["SNAIL_MCP_MEMORY_TRANSPORT"] = "Http",
            ["SNAIL_MCP_MEMORY_PORT"] = Ports.Free().ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["SNAIL_MCP_MEMORY_LISTENS_ON_EVERY_INTERFACE"] = "true",
        });

        Assert.Equal(1, exited.Code);
        Assert.Contains("SNAIL_MCP_MEMORY_LISTENS_ON_EVERY_INTERFACE", exited.Stderr, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", exited.Stderr, StringComparison.Ordinal);
    }

    private static McpEndpoint Read(Dictionary<string, string?> settings) =>
        McpEndpoint.Read(
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            StorageOptions.DefaultUrl,
            StorageOptions.EndpointOptions);
}
