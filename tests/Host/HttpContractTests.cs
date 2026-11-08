using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Snail.MCP.Memory.Infrastructure.Configuration;
using Snail.Sdk.Mcp.Hosting;

namespace Snail.MCP.Memory.Tests.Host;

/// The shared server as the machine runs it: one process over HTTP on loopback, several clients at once, each
/// naming its project by the folder it works in, and no browser let through.
#pragma warning disable MCP9005
public sealed class HttpContractTests : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private static readonly string Prefix = ServerSettings.PrefixOf(StorageOptions.ServerName);

    private readonly string _dataDirectory = TestFolder.Create();
    private HttpServerProgram _server = null!;

    /// <summary>
    /// The build beside the tests, or the one SNAIL_MEMORY_SERVER names, told HTTP and its address by HttpServerProgram,
    /// with its own home and data folder, no model and no audit; ready once its MCP endpoint answers, since it maps no /healthz.
    /// </summary>
    public async Task InitializeAsync()
    {
        var environment = new Dictionary<string, string?>
        {
            [Prefix + "DIRECTORY"] = _dataDirectory,
            [Prefix + "EMBEDDING_ENABLED"] = "false",
            [Prefix + "AUDIT_INTERVAL_DAYS"] = "0",
        };

        _server = await HttpServerProgram.StartAsync("Snail.MCP.Memory", "SNAIL_MEMORY_SERVER", environment, readiness: null);
    }

    public async Task DisposeAsync()
    {
        await _server.DisposeAsync();
        Directory.Delete(_dataDirectory, recursive: true);
    }

    [Fact]
    public async Task TwoClientsShareOneMemory()
    {
        using var cancellation = new CancellationTokenSource(Timeout);
        await using var writer = await ConnectAsync(roots: null, cancellation.Token);
        await using var reader = await ConnectAsync(roots: null, cancellation.Token);

        await writer.CallToolAsync("init_project_memory", new Dictionary<string, object?> { ["projectName"] = "HttpProbe" }, cancellationToken: cancellation.Token);
        await writer.CallToolAsync(
            "remember",
            new Dictionary<string, object?> { ["projectName"] = "HttpProbe", ["content"] = "one server for every session", ["kind"] = "fact" },
            cancellationToken: cancellation.Token);

        var read = await reader.CallToolAsync("recall", new Dictionary<string, object?> { ["projectName"] = "HttpProbe" }, cancellationToken: cancellation.Token);

        Assert.True(read.IsOk(), read.Text());
        Assert.Equal("one server for every session", read.Data()!["records"]![0]!["content"]!.GetValue<string>());
    }

    [Fact]
    public async Task InitWithoutAName_TakesTheProjectFromTheClientsRoot()
    {
        using var cancellation = new CancellationTokenSource(Timeout);
        var folder = Path.Combine(_dataDirectory, "RootsProbe");
        await using var client = await ConnectAsync(new Uri(folder).AbsoluteUri, cancellation.Token);

        var answer = await client.CallToolAsync("init_project_memory", new Dictionary<string, object?>(), cancellationToken: cancellation.Token);

        Assert.True(answer.IsOk(), answer.Text());
        Assert.Equal("RootsProbe", answer.Data()!["project"]!.GetValue<string>());
    }

    /// <summary>A refusal is the failure envelope, flagged an error, with the kind a program branches on and what to do.</summary>
    [Fact]
    public async Task ARefusal_IsTheFailureEnvelope()
    {
        using var cancellation = new CancellationTokenSource(Timeout);
        await using var client = await ConnectAsync(roots: null, cancellation.Token);

        var answer = await client.CallToolAsync("forget", new Dictionary<string, object?> { ["ids"] = "not-an-id" }, cancellationToken: cancellation.Token);

        Assert.True(answer.IsError);
        Assert.False(answer.IsOk());
        Assert.Equal("invalidId", answer.Type());
        Assert.NotNull(answer.Hint());
    }

    /// <summary>A page in a browser always sends Origin and an agent's client never does: no web page may reach the server.</summary>
    [Fact]
    public async Task ARequestFromABrowserIsRefused() =>
        Assert.Empty(await HttpGuards.ProblemsAsync(_server.Endpoint));

    /// <summary>A client of the shared server, offering <paramref name="roots"/> as its folder when given.</summary>
    /// <remarks>
    /// Speaks the 2025-06-18 revision: a session is opened by initialize, which the 2026-07-28 revision no longer sends;
    /// clients of today still do.
    /// </remarks>
    private Task<McpClient> ConnectAsync(string? roots, CancellationToken cancellationToken)
    {
        var options = new McpClientOptions { ProtocolVersion = "2025-06-18" };

        if (roots is not null)
        {
            options.Capabilities = new ClientCapabilities { Roots = new RootsCapability() };
            options.Handlers = new McpClientHandlers
            {
                RootsHandler = (_, _) => ValueTask.FromResult(new ListRootsResult { Roots = [new Root { Uri = roots }] }),
            };
        }

        return _server.SessionAsync(options: options, cancellationToken: cancellationToken);
    }
}
