using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Snail.MCP.Memory.Infrastructure.Configuration;
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

    /// <summary>Read in the test's process, whose entry assembly names no server, a refusal names the key of the settings file.</summary>
    [Theory]
    [InlineData("http://0.0.0.0:9230")]
    [InlineData("http://192.168.1.20:9230")]
    public void Read_AnAddressBeyondLoopback_IsRefused(string url)
    {
        var refusal = Assert.Throws<InvalidOperationException>(() => Read(new() { ["Transport"] = "Http", ["Url"] = url }));

        Assert.StartsWith($"Url is '{url}'", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_ListensOnEveryInterface_IsRefused()
    {
        var refusal = Assert.Throws<InvalidOperationException>(() =>
            Read(new() { ["Transport"] = "Http", ["ListensOnEveryInterface"] = "true" }));

        Assert.StartsWith("ListensOnEveryInterface is 'true'", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>The program itself names the variable its operator set, and never starts listening.</summary>
    [Fact]
    public async Task Program_ToldToListenOnEveryInterface_ExitsNamingTheVariable()
    {
        var dataDirectory = TestFolder.Create();
        var (command, arguments) = ServerProgram.Command("Snail.MCP.Memory", "SNAIL_MEMORY_SERVER");
        var start = new ProcessStartInfo { FileName = command, RedirectStandardError = true, RedirectStandardOutput = true };

        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        start.Environment["SNAIL_MCP_MEMORY_DIRECTORY"] = dataDirectory;
        start.Environment["SNAIL_MCP_MEMORY_TRANSPORT"] = "Http";
        start.Environment["SNAIL_MCP_MEMORY_PORT"] = Ports.Free().ToString(System.Globalization.CultureInfo.InvariantCulture);
        start.Environment["SNAIL_MCP_MEMORY_LISTENS_ON_EVERY_INTERFACE"] = "true";
        start.Environment[ServerSettings.FileVariableOf(StorageOptions.ServerName)] = ServerProgram.NoSettingsFile;
        start.Environment["HOME"] = ServerProgram.Home;
        start.Environment["USERPROFILE"] = ServerProgram.Home;

        try
        {
            using var process = Process.Start(start)!;
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var stderr = process.StandardError.ReadToEndAsync(deadline.Token);

            await process.WaitForExitAsync(deadline.Token);

            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains("SNAIL_MCP_MEMORY_LISTENS_ON_EVERY_INTERFACE", await stderr, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    private static McpEndpoint Read(Dictionary<string, string?> settings) =>
        McpEndpoint.Read(
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            StorageOptions.DefaultUrl,
            StorageOptions.EndpointOptions);
}
