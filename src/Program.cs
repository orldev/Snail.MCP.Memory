using Microsoft.Extensions.Hosting;
using Snail.MCP.Memory.Host.Extensions;
using Snail.MCP.Memory.Host.Hosting;
using Snail.MCP.Memory.Infrastructure.Configuration;
using Snail.Toolkit.Mcp.Hosting;

var options = StorageOptions.Load();

if (options.Transport is Transport.Http)
{
    await HttpServerHost.RunAsync(args, options);

    return;
}

await McpHost.CreateBuilder(args)
    .ConfigureMcpServer(options)
    .Build()
    .RunAsync();
