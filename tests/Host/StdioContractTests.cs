using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using ModelContextProtocol.Server;
using Snail.MCP.Memory.Infrastructure.Configuration;
using Snail.MCP.Memory.Tools;
using Snail.Sdk.Mcp.Hosting;

namespace Snail.MCP.Memory.Tests.Host;

/// The stdio contract of the real server process: stdout carries JSON-RPC and nothing else, the
/// composition root boots, configuration reaches the store, and every tool of the assembly
/// reaches the model. Embeddings are switched off through the environment — the contract must
/// hold on a machine that never downloads a model, and that degraded mode is itself part of the
/// contract.
public class StdioContractTests
{
    private const string ProtocolVersion = "2025-06-18";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private static readonly string Prefix = ServerSettings.PrefixOf(StorageOptions.ServerName);

    private static readonly string ConfigVariable = ServerSettings.FileVariableOf(StorageOptions.ServerName);

    [Fact]
    public async Task ServerAnswersTheHandshakeAndKeepsStdoutCleanOfEverythingButJsonRpc()
    {
        using var session = await ServerSession.StartAsync();

        var initialize = await session.CallAsync(1, "initialize", new
        {
            protocolVersion = ProtocolVersion,
            capabilities = new { },
            clientInfo = new { name = "stdio-contract", version = "1.0" },
        });

        var serverInfo = initialize.GetProperty("result").GetProperty("serverInfo");

        Assert.Equal("s3l-mcp-memory", serverInfo.GetProperty("name").GetString());
        Assert.False(string.IsNullOrWhiteSpace(serverInfo.GetProperty("version").GetString()));

        await session.NotifyAsync("notifications/initialized");

        var listed = await session.CallAsync(2, "tools/list", new { });
        var names = listed.GetProperty("result").GetProperty("tools")
            .EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(ToolNamesOfAssembly(), names);

        await session.CallAsync(3, "tools/call", new
        {
            name = "init_project_memory",
            arguments = new { projectName = "StdioProbe", description = "written by the contract test" },
        });

        await session.CallAsync(4, "tools/call", new
        {
            name = "remember",
            arguments = new { projectName = "StdioProbe", content = "the stack is .NET", kind = "fact" },
        });

        var read = await session.CallAsync(5, "tools/call", new
        {
            name = "recall",
            arguments = new { projectName = "StdioProbe" },
        });

        var overview = Envelope(read);
        var record = overview.GetProperty("data").GetProperty("records")[0];

        Assert.True(overview.GetProperty("ok").GetBoolean(), $"recall failed: {overview}\nstderr:\n{session.StderrText}");
        Assert.Equal("fact", record.GetProperty("kind").GetString());
        Assert.Equal("the stack is .NET", record.GetProperty("content").GetString());

        var unknown = await session.CallAsync(8, "tools/call", new
        {
            name = "recall",
            arguments = new { projectName = "NoSuchProject" },
        });
        var refusal = Envelope(unknown);

        Assert.True(unknown.GetProperty("result").GetProperty("isError").GetBoolean(), "a refusal must be flagged isError");
        Assert.False(refusal.GetProperty("ok").GetBoolean());
        Assert.Equal("notFound", refusal.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(refusal.GetProperty("hint").GetString()));

        await session.CallAsync(6, "tools/call", new
        {
            name = "init_project_memory",
            arguments = new { projectName = "StdioProbeDocs" },
        });

        var linked = await session.CallAsync(7, "tools/call", new
        {
            name = "link_projects",
            arguments = new { from = "StdioProbeDocs", to = "StdioProbe", type = "documents" },
        });

        Assert.False(linked.GetProperty("result").TryGetProperty("isError", out var isError) && isError.GetBoolean(),
            "link_projects rejected a canonical wire value of the type enum");

        var exit = await session.ShutdownAsync();

        Assert.Equal(0, exit);
        Assert.All(session.StdoutLines, line => Assert.Equal("2.0", session.ParseOrFail(line).GetProperty("jsonrpc").GetString()));
        Assert.True(session.HasDataDirectoryContent, "the configured data directory stayed empty — configuration did not reach the store");
    }

    /// <summary>The envelope a tool answers with: the text of its first content block, parsed.</summary>
    private static JsonElement Envelope(JsonElement response) =>
        JsonDocument.Parse(response.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!).RootElement;

    /// <summary>Tools are discovered by the SDK through attributes, so a misconfigured attribute is invisible to reflection-only guards.</summary>
    private static HashSet<string> ToolNamesOfAssembly()
    {
        var toolTypes = typeof(ToolDescriptions).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null);
        var toolMethods = toolTypes
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null);

        return toolMethods.Select(method => McpServerTool.Create(method).ProtocolTool.Name).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// The guard rules of every Snail server, on the list a model receives: names in snake case and listed once, the tool
    /// and every parameter described, read-only or destructive stated, every allowed value named, English.
    /// </summary>
    [Fact]
    public async Task Tools_AsListed_KeepTheGuardRules()
    {
        var dataDirectory = TestFolder.Create();

        try
        {
            var environment = new Dictionary<string, string?>
            {
                [Prefix + "DIRECTORY"] = dataDirectory,
                [Prefix + "EMBEDDING_ENABLED"] = "false",
                [ConfigVariable] = ServerProgram.NoSettingsFile,
            };

            await using var client = await ServerProgram.StartAsync("Snail.MCP.Memory", "SNAIL_MEMORY_SERVER", environment);

            var listed = await client.ListToolsAsync();

            Assert.Empty(ToolGuards.Problems(listed.Select(tool => tool.ProtocolTool)));
        }
        finally
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    /// <summary>The server started as a separate process and spoken to over stdin/stdout, exactly as an MCP client does.</summary>
    private sealed class ServerSession : IDisposable
    {
        private readonly Process _process;
        private readonly string _dataDirectory;
        private readonly List<string> _stderr = [];

        private ServerSession(Process process, string dataDirectory)
        {
            _process = process;
            _dataDirectory = dataDirectory;
        }

        public List<string> StdoutLines { get; } = [];

        public bool HasDataDirectoryContent =>
            Directory.Exists(_dataDirectory) && Directory.EnumerateFileSystemEntries(_dataDirectory).Any();

        public string StderrText => Stderr();

        public static async Task<ServerSession> StartAsync()
        {
            var dataDirectory = Path.Combine(TestFolder.Create(), "data");
            var (command, arguments) = ServerProgram.Command("Snail.MCP.Memory", "SNAIL_MEMORY_SERVER");
            var start = new ProcessStartInfo
            {
                FileName = command,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            foreach (var argument in arguments)
                start.ArgumentList.Add(argument);
            start.Environment[Prefix + "DIRECTORY"] = dataDirectory;
            start.Environment[Prefix + "EMBEDDING_ENABLED"] = "false";
            start.Environment[ConfigVariable] = ServerProgram.NoSettingsFile;
            start.Environment["HOME"] = ServerProgram.Home;
            start.Environment["USERPROFILE"] = ServerProgram.Home;

            var session = new ServerSession(Process.Start(start)!, dataDirectory);
            session.DrainStderr();

            return await Task.FromResult(session);
        }

        public async Task<JsonElement> CallAsync(int id, string method, object parameters)
        {
            await NotifyAsync(method, parameters, id);

            return await ReadAsync();
        }

        public async Task NotifyAsync(string method, object? parameters = null, int? id = null)
        {
            var envelope = new Dictionary<string, object> { ["jsonrpc"] = "2.0", ["method"] = method };

            if (id is { } value) envelope["id"] = value;
            if (parameters is not null) envelope["params"] = parameters;

            await _process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(envelope));
            await _process.StandardInput.FlushAsync();
        }

        public async Task<int> ShutdownAsync()
        {
            _process.StandardInput.Close();

            using var deadline = new CancellationTokenSource(Timeout);

            while (await _process.StandardOutput.ReadLineAsync(deadline.Token) is { } line)
            {
                if (line.Length > 0) StdoutLines.Add(line);
            }

            await _process.WaitForExitAsync(deadline.Token);

            return _process.ExitCode;
        }

        /// <summary>stdio carries JSON-RPC only: a single stray Console.Write breaks every MCP client, so the line itself is reported.</summary>
        public JsonElement ParseOrFail(string line)
        {
            try
            {
                return JsonDocument.Parse(line).RootElement;
            }
            catch (JsonException)
            {
                Assert.Fail($"stdout carries a line that is not JSON-RPC: {line}\nstderr:\n{Stderr()}");

                throw;
            }
        }

        private string Stderr()
        {
            lock (_stderr) return string.Join('\n', _stderr);
        }

        private async Task<JsonElement> ReadAsync()
        {
            using var deadline = new CancellationTokenSource(Timeout);

            while (await _process.StandardOutput.ReadLineAsync(deadline.Token) is { } line)
            {
                if (line.Length == 0) continue;

                StdoutLines.Add(line);

                var message = ParseOrFail(line);

                if (message.TryGetProperty("result", out _) || message.TryGetProperty("error", out _))
                    return message;
            }

            throw new InvalidOperationException($"the server closed stdout without answering. stderr:\n{Stderr()}");
        }

        private void DrainStderr() => _ = Task.Run(async () =>
        {
            while (await _process.StandardError.ReadLineAsync() is { } line)
            {
                lock (_stderr) _stderr.Add(line);
            }
        });

        public void Dispose()
        {
            if (!_process.HasExited) _process.Kill(entireProcessTree: true);

            _process.Dispose();

            if (Directory.Exists(_dataDirectory)) Directory.Delete(_dataDirectory, recursive: true);
        }
    }
}
