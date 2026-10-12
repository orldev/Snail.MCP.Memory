using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Snail.MCP.Memory.Host.Extensions;
using Snail.MCP.Memory.Infrastructure.Configuration;
using Snail.Toolkit.Mcp.Hosting;

namespace Snail.MCP.Memory.Host.Hosting;

/// <summary>One server for every session of the machine, on a loopback address, kept running by the system.</summary>
/// <remarks>
/// <para>
/// Over stdio each session starts a server of its own: the model, the database connection and the runtime once per
/// session, ~115 MB each, all writing one database. Over HTTP there is one of each. The idle watchdog is left out —
/// it ends a server its one client forgot, and this one has no single client; launchd keeps it alive instead.
/// </para>
/// <para>
/// The endpoint has no key. It listens on loopback only, and a request that carries an <c>Origin</c> is refused: a
/// page in a browser always sends one, an agent's client never does, so no web page can reach the memory, not even
/// through a name rebound to 127.0.0.1.
/// </para>
/// <para>Kept apart from <c>Program</c> so that a stdio server never loads ASP.NET Core.</para>
/// </remarks>
public static class HttpServerHost
{
    /// <summary>Where clients connect.</summary>
    public const string McpPath = "/mcp";

    public static Task RunAsync(string[] args, StorageOptions options)
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { Args = args });

        builder.Logging.UseStderr();
        builder.WebHost.UseKestrelCore().UseUrls(options.ResolveUrl().ToString());
        builder.Services.AddRoutingCore();
        builder.Services
            .AddStorage(options)
            .AddAuditWorker(options)
            .AddMcpHttpTransport();

        var app = builder.Build();

        app.Use(RefuseBrowsers);
        app.MapMcp(McpPath);

        return app.RunAsync();
    }

    private static Task RefuseBrowsers(HttpContext context, RequestDelegate next)
    {
        if (!context.Request.Headers.Origin.Any())
            return next(context);

        context.Response.StatusCode = StatusCodes.Status403Forbidden;

        return Task.CompletedTask;
    }
}
