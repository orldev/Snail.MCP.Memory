using Microsoft.Extensions.Configuration;
using Snail.MCP.Memory.Host.Extensions;
using Snail.MCP.Memory.Infrastructure.Configuration;
using Snail.Sdk.Mcp.Hosting;
using Snail.Sdk.Mcp.Http;

namespace Snail.MCP.Memory.Host.Hosting;

/// <summary>One server for every session of the machine, on a loopback address whatever the settings say, kept running by the system.</summary>
/// <remarks>
/// <para>
/// Over stdio each session starts a server of its own: the model, the database connection and the runtime once per
/// session, ~115 MB each, all writing one database. Over HTTP there is one of each. The idle shutdown is left out —
/// it ends a server its one client forgot, and this one has no single client; launchd keeps it alive instead.
/// </para>
/// <para>
/// The address — <c>Transport</c>, <c>Url</c> or <c>Port</c>, on loopback alone (<see cref="StorageOptions.EndpointOptions"/>) — is
/// read by <see cref="McpEndpoint.Read(IConfiguration, Uri, McpEndpointOptions)"/>; the host and the refusal of browsers
/// are Snail.Sdk.Mcp.Http's, see <see cref="McpHttpHost"/>.
/// </para>
/// <para>Kept apart from <c>Program</c> so that a stdio server never loads ASP.NET Core.</para>
/// </remarks>
public static class HttpServerHost
{
    /// <summary>Builds the HTTP server at the endpoint the settings ask for and runs it until it is stopped.</summary>
    public static Task RunAsync(string[] args, McpEndpoint endpoint, IConfiguration settings, StorageOptions options)
    {
        var builder = McpHttpHost.CreateBuilder(args, endpoint, settings);

        builder.Services
            .AddStorage(options)
            .AddAuditWorker(options)
            .AddMcpHttpTransport();

        var app = builder.Build();

        app.MapLocalMcp();

        return app.RunAsync();
    }
}
