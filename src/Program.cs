using Microsoft.Extensions.Hosting;
using Snail.MCP.Memory.Host.Extensions;

await Host.CreateApplicationBuilder(args)
    .ConfigureMcpServer()
    .Build()
    .RunAsync();
