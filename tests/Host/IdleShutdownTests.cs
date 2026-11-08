using System.IO.Pipelines;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Memory.Host.Extensions;
using Snail.MCP.Memory.Infrastructure.Configuration;
using Snail.Sdk.Mcp.Hosting;

namespace Snail.MCP.Memory.Tests.Host;

/// A client that died without closing stdin leaves the stdio server waiting for a message that never comes: the server's
/// own composition stops it after the configured quiet, measured by the clock its services hold, so the hour is moved here
/// rather than waited for.
public sealed class IdleShutdownTests : IDisposable
{
    private static readonly TimeSpan Quiet = TimeSpan.FromMinutes(StorageOptions.DefaultIdleTimeoutMinutes);

    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(300);

    private readonly string _dataDirectory = TestFolder.Create();
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-11-08T12:00:00Z"));

    public void Dispose() => Directory.Delete(_dataDirectory, recursive: true);

    [Fact]
    public async Task StdioServer_HearingNothingForTheDefaultHour_Stops()
    {
        using var host = Build();
        var stopping = Stopping(host);
        await host.StartAsync();

        try
        {
            _clock.Advance(Quiet - TimeSpan.FromMinutes(1));
            await Task.Delay(Settle);

            Assert.False(stopping.Task.IsCompleted, "the server stopped before its quiet period was over");

            _clock.Advance(TimeSpan.FromMinutes(1));
            await stopping.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// The composition root as <c>Program</c> builds it, with the client's end of stdio a pipe nobody writes to and the
    /// clock a fake one, registered after the storage's system clock so it is the one the services hand out.
    /// </summary>
    private IHost Build()
    {
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Directory"] = _dataDirectory,
            ["EmbeddingEnabled"] = "false",
            ["AuditIntervalDays"] = "0",
        }).Build();
        var builder = McpHost.CreateBuilder([], settings).ConfigureMcpServer(StorageOptions.From(settings));

        builder.Services.Replace(ServiceDescriptor.Singleton<ITransport>(
            new StreamServerTransport(new Pipe().Reader.AsStream(), new Pipe().Writer.AsStream())));
        builder.Services.AddSingleton<TimeProvider>(_clock);

        return builder.Build();
    }

    private static TaskCompletionSource Stopping(IHost host)
    {
        var stopping = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.Register(() => stopping.TrySetResult());

        return stopping;
    }
}
