using Microsoft.Extensions.Hosting;
using Snail.MCP.Memory.Host.Extensions;
using Snail.MCP.Memory.Infrastructure.Configuration;
using Snail.Toolkit.Mcp.Http;

namespace Snail.MCP.Memory.Host.Hosting;

/// <summary>One server for every session of the machine, on a loopback address, kept running by the system.</summary>
/// <remarks>
/// <para>
/// Over stdio each session starts a server of its own: the model, the database connection and the runtime once per
/// session, ~115 MB each, all writing one database. Over HTTP there is one of each. The idle watchdog is left out —
/// it ends a server its one client forgot, and this one has no single client; launchd keeps it alive instead.
/// </para>
/// <para>The loopback address and the refusal of browsers are Snail.Toolkit.Mcp.Http's, see <see cref="McpHttpHost"/>.</para>
/// <para>Kept apart from <c>Program</c> so that a stdio server never loads ASP.NET Core.</para>
/// </remarks>
public static class HttpServerHost
{
    public static Task RunAsync(string[] args, StorageOptions options)
    {
        var builder = McpHttpHost.CreateBuilder(args, options.ResolveUrl());

        builder.Services
            .AddStorage(options)
            .AddAuditWorker(options)
            .AddMcpHttpTransport();

        var app = builder.Build();

        app.MapLocalMcp();

        return app.RunAsync();
    }
}
