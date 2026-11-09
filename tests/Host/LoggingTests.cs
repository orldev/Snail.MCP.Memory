using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Snail.MCP.Memory.Host.Extensions;
using Snail.MCP.Memory.Infrastructure.Configuration;
using Snail.Sdk.Mcp.Hosting;

namespace Snail.MCP.Memory.Tests.Host;

/// The shared server's log is one file launchd appends to for months: what it holds is what someone reads after a failure.
public sealed class LoggingTests : IDisposable
{
    private const string Commands = "Microsoft.EntityFrameworkCore.Database.Command";

    private readonly string _dataDirectory = TestFolder.Create();

    public void Dispose() => Directory.Delete(_dataDirectory, recursive: true);

    [Fact]
    public void Server_LeavesEntityFrameworksSqlCommandsOutAndKeepsItsWarnings()
    {
        using var host = Build([]);
        var commands = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(Commands);

        Assert.False(commands.IsEnabled(LogLevel.Information), "every SQL command would be written to the log");
        Assert.True(commands.IsEnabled(LogLevel.Warning));
    }

    [Fact]
    public void Server_WritesTheSqlCommandsOnceTheSettingsAskForThem()
    {
        using var host = Build(new() { [$"Logging:LogLevel:{Commands}"] = "Information" });
        var commands = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(Commands);

        Assert.True(commands.IsEnabled(LogLevel.Information));
    }

    private IHost Build(Dictionary<string, string?> levels)
    {
        var values = new Dictionary<string, string?>(levels)
        {
            ["Directory"] = _dataDirectory,
            ["EmbeddingEnabled"] = "false",
            ["AuditIntervalDays"] = "0",
        };
        var settings = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        return McpHost.CreateBuilder([], settings).ConfigureMcpServer(StorageOptions.From(settings)).Build();
    }
}
