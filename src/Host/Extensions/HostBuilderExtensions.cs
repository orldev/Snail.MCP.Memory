using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Snail.MCP.Memory.Infrastructure.Configuration;

namespace Snail.MCP.Memory.Host.Extensions;

/// <summary>Composition root of the MCP server.</summary>
public static class HostBuilderExtensions
{
    extension(HostApplicationBuilder builder)
    {
        /// <summary>
        /// Wires logging to stderr, storage from <see cref="StorageOptions.Load"/>, the idle
        /// watchdog and the MCP server over stdio with every tool in the assembly.
        /// </summary>
        public HostApplicationBuilder ConfigureMcpServer()
        {
            builder.Logging.AddMcpStdioLogging();

            var options = StorageOptions.Load();

            builder.Services
                .AddStorage(options)
                .AddIdleShutdownWatcher(options)
                .AddAuditWorker(options)
                .AddMcpTransport();

            return builder;
        }
    }
}
