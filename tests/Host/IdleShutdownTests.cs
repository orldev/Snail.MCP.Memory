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
    /// The audit claims its lease before it runs: stopped in between, the server would have spent the interval and audited
    /// nothing. Its work counts as the server's, and the quiet period starts again when it ends.
    /// </summary>
    [Fact]
    public async Task StdioServer_WhileTheAuditIsAtWork_WaitsForItToEnd()
    {
        var audit = new HeldAudit();
        using var host = Build(audit);
        var stopping = Stopping(host);
        await host.StartAsync();

        try
        {
            await audit.Claimed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            _clock.Advance(Quiet + TimeSpan.FromMinutes(1));
            await Task.Delay(Settle);

            Assert.False(stopping.Task.IsCompleted, "the server stopped while its audit was at work");

            audit.Release.TrySetResult();
            await Task.Delay(Settle);
            await AdvanceUntilAsync(stopping.Task, Quiet * 4);

            Assert.True(stopping.Task.IsCompleted, "the server kept running after its audit ended and the quiet period passed");
        }
        finally
        {
            audit.Release.TrySetResult();
            await host.StopAsync();
        }
    }

    /// <summary>
    /// The composition root as <c>Program</c> builds it, with the client's end of stdio a pipe nobody writes to and the
    /// clock a fake one, registered after the storage's system clock so it is the one the services hand out; with an
    /// audit store given, the audit is on and claims its lease there.
    /// </summary>
    private IHost Build(IAuditStore? audit = null)
    {
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Directory"] = _dataDirectory,
            ["EmbeddingEnabled"] = "false",
            ["AuditIntervalDays"] = audit is null ? "0" : "1",
        }).Build();
        var builder = McpHost.CreateBuilder([], settings).ConfigureMcpServer(StorageOptions.From(settings));

        builder.Services.Replace(ServiceDescriptor.Singleton<ITransport>(
            new StreamServerTransport(new Pipe().Reader.AsStream(), new Pipe().Writer.AsStream())));
        builder.Services.AddSingleton<TimeProvider>(_clock);

        if (audit is not null)
            builder.Services.Replace(ServiceDescriptor.Singleton(audit));

        return builder.Build();
    }

    /// <summary>Moves the clock half an hour at a time until <paramref name="done"/> completes or <paramref name="limit"/> has passed.</summary>
    /// <remarks>
    /// The audit checks its lease every hour and counts as work while it does; a single jump past such a check can land
    /// the shutdown's tick inside that instant and restart the quiet. Steps let the next tick come after it.
    /// </remarks>
    private async Task AdvanceUntilAsync(Task done, TimeSpan limit)
    {
        var step = TimeSpan.FromMinutes(30);

        for (var moved = TimeSpan.Zero; moved < limit && !done.IsCompleted; moved += step)
        {
            _clock.Advance(step);
            await Task.Delay(Settle);
        }
    }

    private static TaskCompletionSource Stopping(IHost host)
    {
        var stopping = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.Register(() => stopping.TrySetResult());

        return stopping;
    }

    /// <summary>A lease the audit waits on until the test lets it go, then never grants: the audit is at work exactly as long as the test says.</summary>
    private sealed class HeldAudit : IAuditStore
    {
        public TaskCompletionSource Claimed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<bool> TryClaimAsync(TimeSpan interval, DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            Claimed.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);

            return false;
        }
    }
}
