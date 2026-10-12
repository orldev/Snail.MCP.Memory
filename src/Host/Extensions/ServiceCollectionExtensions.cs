using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Memory.Core.Activity;
using Snail.MCP.Memory.Host.Hosting;
using Snail.MCP.Memory.Infrastructure.Configuration;

namespace Snail.MCP.Memory.Host.Extensions;

/// <summary>Registration of host-level services.</summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the idle watchdog with the configured timeout — see <see cref="IdleShutdownWatcher"/>.</summary>
        public IServiceCollection AddIdleShutdownWatcher(StorageOptions options)
        {
            services.AddSingleton<IActivityTracker>(sp => new ActivityTracker(sp.GetRequiredService<TimeProvider>()));

            services.AddHostedService(sp => new IdleShutdownWatcher(
                sp.GetRequiredService<IActivityTracker>(),
                sp.GetRequiredService<IHostApplicationLifetime>(),
                sp.GetRequiredService<ILogger<IdleShutdownWatcher>>(),
                sp.GetRequiredService<TimeProvider>(),
                options.ResolveIdleTimeout()));

            return services;
        }

        /// <summary>Registers the background audit unless it is switched off — see <see cref="AuditWorker"/>.</summary>
        public IServiceCollection AddAuditWorker(StorageOptions options)
        {
            if (!options.ResolveAuditEnabled())
                return services;

            services.AddHostedService(sp => new AuditWorker(
                sp.GetRequiredService<IAuditStore>(),
                sp.GetRequiredService<Application.AuditService>(),
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<ILogger<AuditWorker>>(),
                options.ResolveAuditInterval(),
                AuditWorker.DefaultCheckPeriod));

            return services;
        }

        /// <summary>
        /// Driving adapter: the tools are delivered to the model over stdio; every tool call
        /// marks the server as active. The memory rules travel in the tool descriptions and in
        /// the repository's <c>server-instructions.md</c>, which the user wires in themselves.
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
            services.AddMemoryServer().WithHttpTransport(options =>
                options.SessionMode = HttpServerSessionMode.StatefulForInitializeClients);

            return services;
        }

        private IMcpServerBuilder AddMemoryServer() =>
            services
                .AddMcpServer()
                .WithToolsFromAssembly()
                .WithRequestFilters(filters => filters
                    .AddCallToolFilter(TouchActivity));
    }

    private static McpRequestHandler<TParams, TResult> TouchActivity<TParams, TResult>(
        McpRequestHandler<TParams, TResult> next) =>
        (context, cancellationToken) =>
        {
            context.Services?.GetService<IActivityTracker>()?.Touch();

            return next(context, cancellationToken);
        };
}
