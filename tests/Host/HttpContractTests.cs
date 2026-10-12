using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Snail.MCP.Memory.Infrastructure.Configuration;
using Snail.Toolkit.Mcp.Testing;

namespace Snail.MCP.Memory.Tests.Host;

/// The shared server as the machine runs it: one process over HTTP on loopback, several clients at once, each
/// naming its project by the folder it works in, and no browser let through.
#pragma warning disable MCP9005
public sealed class HttpContractTests : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private readonly string _dataDirectory = Directory.CreateTempSubdirectory("snail-http-").FullName;
    private readonly Uri _endpoint = new($"http://127.0.0.1:{FreePort()}/mcp");
    private Process _server = null!;

    public async Task InitializeAsync()
    {
        var (command, arguments) = ServerProgram.Command("Snail.MCP.Memory", "SNAIL_MEMORY_SERVER");
        var start = new ProcessStartInfo(command, arguments)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        start.Environment[EnvironmentSettings.Prefix + "TRANSPORT"] = "Http";
        start.Environment[EnvironmentSettings.Prefix + "URL"] = new Uri(_endpoint, "/").ToString();
        start.Environment[EnvironmentSettings.Prefix + "DIRECTORY"] = _dataDirectory;
        start.Environment[EnvironmentSettings.Prefix + "EMBEDDING_ENABLED"] = "false";
        start.Environment[EnvironmentSettings.Prefix + "AUDIT_INTERVAL_DAYS"] = "0";

        _server = Process.Start(start)!;
        _server.OutputDataReceived += (_, _) => { };
        _server.ErrorDataReceived += (_, _) => { };
        _server.BeginOutputReadLine();
        _server.BeginErrorReadLine();

        using var waiting = new CancellationTokenSource(Timeout);
        using var http = new HttpClient();

        while (true)
        {
            try
            {
                await http.GetAsync(_endpoint, waiting.Token);
                return;
            }
            catch (HttpRequestException) when (!_server.HasExited)
            {
                await Task.Delay(100, waiting.Token);
            }
        }
    }

    public Task DisposeAsync()
    {
        _server.Kill(entireProcessTree: true);
        _server.WaitForExit();
        _server.Dispose();
        Directory.Delete(_dataDirectory, recursive: true);

        return Task.CompletedTask;
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

        Assert.Contains("one server for every session", ServerProgram.Text(read), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitWithoutAName_TakesTheProjectFromTheClientsRoot()
    {
        using var cancellation = new CancellationTokenSource(Timeout);
        var folder = Path.Combine(_dataDirectory, "RootsProbe");
        await using var client = await ConnectAsync(new Uri(folder).AbsoluteUri, cancellation.Token);

        var answer = await client.CallToolAsync("init_project_memory", new Dictionary<string, object?>(), cancellationToken: cancellation.Token);

        Assert.Contains("RootsProbe", ServerProgram.Text(answer), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARequestFromABrowserIsRefused()
    {
        using var http = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent("""{"jsonrpc":"2.0","id":1,"method":"ping"}""", System.Text.Encoding.UTF8, "application/json"),
        };

        request.Headers.Add("Origin", "https://example.com");

        using var response = await http.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private Task<McpClient> ConnectAsync(string? roots, CancellationToken cancellationToken)
    {
        // A session is opened by initialize, which the 2026-07-28 revision no longer sends; clients of today still do.
        var options = new McpClientOptions { ProtocolVersion = "2025-06-18" };

        if (roots is not null)
        {
            options.Capabilities = new ClientCapabilities { Roots = new RootsCapability() };
            options.Handlers = new McpClientHandlers
            {
                RootsHandler = (_, _) => ValueTask.FromResult(new ListRootsResult { Roots = [new Root { Uri = roots }] }),
            };
        }

        return McpClient.CreateAsync(
            new HttpClientTransport(new HttpClientTransportOptions { Endpoint = _endpoint }),
            options,
            cancellationToken: cancellationToken);
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
