using Microsoft.Extensions.Hosting;
using Snail.MCP.Memory.Host.Hosting;
using Snail.MCP.Memory.Infrastructure.Configuration;
using Snail.Sdk.Mcp.Hosting;

namespace Snail.MCP.Memory.Host.Extensions;

/// <summary>Composition root of the MCP server.</summary>
public static class HostBuilderExtensions
{
    extension(HostApplicationBuilder builder)
    {
        /// <summary>
        /// Wires storage from <see cref="StorageOptions"/>, the idle shutdown and the MCP server over stdio with every
        /// tool in the assembly; logging to stderr is the host's (<see cref="McpHost.CreateBuilder(string[], Microsoft.Extensions.Configuration.IConfiguration)"/>).
        /// </summary>
        /// <remarks>
        /// The idle shutdown is Snail.Sdk.Mcp.Hosting's: a client that died without closing stdin leaves no process behind once
        /// the server has heard no MCP message for <see cref="StorageOptions.ResolveIdleTimeout"/>, which never turns it off,
        /// and no audit is at work (<see cref="AuditWorker.IsAuditing"/>).
        /// </remarks>
        public HostApplicationBuilder ConfigureMcpServer(StorageOptions options)
        {
            builder.Services
                .AddStorage(options)
                .AddIdleShutdown(options.ResolveIdleTimeout(), AuditWorker.IsAuditing)
                .AddAuditWorker(options)
                .AddMcpTransport();

            return builder;
        }
    }
}
