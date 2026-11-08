using Microsoft.Extensions.Hosting;
using Snail.MCP.Memory.Host.Extensions;
using Snail.MCP.Memory.Host.Hosting;
using Snail.MCP.Memory.Infrastructure.Configuration;
using Snail.Sdk.Mcp.Hosting;

try
{
    var settings = ServerSettings.Read(StorageOptions.ServerName);
    var options = StorageOptions.From(settings);
    var endpoint = McpEndpoint.Read(settings, StorageOptions.DefaultUrl, StorageOptions.EndpointOptions);

    if (endpoint.Transport is McpTransport.Http)
    {
        await HttpServerHost.RunAsync(args, endpoint, settings, options);

        return 0;
    }

    await McpHost.CreateBuilder(args, settings)
        .ConfigureMcpServer(options)
        .Build()
        .RunAsync();

    return 0;
}
catch (StartRefusedException refused)
{
    await Console.Error.WriteLineAsync(refused.Message);

    return 1;
}
