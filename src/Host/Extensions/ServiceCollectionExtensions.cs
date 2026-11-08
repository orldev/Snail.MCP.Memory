using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Snail.MCP.Memory.Host.Hosting;
using Snail.MCP.Memory.Infrastructure.Configuration;
using Snail.Sdk.Mcp.Hosting;
using Snail.Sdk.Mcp.Hosting.Answers;
using Snail.Sdk.Mcp.Http;

namespace Snail.MCP.Memory.Host.Extensions;

/// <summary>Registration of host-level services.</summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the background audit unless it is switched off — see <see cref="AuditWorker"/>.</summary>
        public IServiceCollection AddAuditWorker(StorageOptions options)
        {
            if (!options.IsAuditEnabled())
                return services;

            services.AddHostedService(sp => new AuditWorker(
                sp.GetRequiredService<IAuditStore>(),
                sp.GetRequiredService<Application.MentionAudit>(),
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<ILogger<AuditWorker>>(),
                options.ResolveAuditInterval(),
                AuditWorker.DefaultCheckPeriod));

            return services;
        }

        /// <summary>
        /// Driving adapter: the tools are delivered to the model over stdio. The memory rules travel in the tool
        /// descriptions and in the repository's <c>server-instructions.md</c>, which the user wires in themselves.
        /// </summary>
        public IServiceCollection AddMcpTransport()
        {
            services.AddMemoryServer().WithStdioServerTransport();

            return services;
        }

        /// <summary>The same server over Streamable HTTP, one session per client; see <see cref="HttpServerHost"/>.</summary>
        /// <remarks>A client that opens with initialize keeps a session: the server can then ask it for its roots, which is how
        /// <c>init_project_memory</c> finds the project of a client it did not start in.</remarks>
        public IServiceCollection AddMcpHttpTransport()
        {
            services.AddMemoryServer().WithLocalHttpTransport();

            return services;
        }

        /// <summary>The server with every tool of the assembly, a tool that throws answered in the envelope as every answer is.</summary>
        private IMcpServerBuilder AddMemoryServer() =>
            services
                .AddMcpServer(server => server.ServerInfo = ServerIdentity.Of(typeof(ServiceCollectionExtensions).Assembly))
                .ExplainFailures()
                .WithToolsFromAssembly();
    }
}
