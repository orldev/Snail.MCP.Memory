using Microsoft.Extensions.Hosting;
using Snail.MCP.Memory.Host.Extensions;
using Snail.MCP.Memory.Host.Hosting;
using Snail.MCP.Memory.Infrastructure.Configuration;
using Snail.Sdk.Mcp.Hosting;

var settings = ServerSettings.Read(StorageOptions.ServerName);
var options = StorageOptions.From(settings);
var endpoint = McpEndpoint.Read(settings, StorageOptions.DefaultUrl, StorageOptions.EndpointOptions);

if (endpoint.Transport is McpTransport.Http)
{
    await HttpServerHost.RunAsync(args, endpoint, settings, options);

    return;
}

await McpHost.CreateBuilder(args, settings)
    .ConfigureMcpServer(options)
    .Build()
    .RunAsync();
