// Global usings of the test project (threshold: 5+ files). Xunit used to be wired in through
// <Using Include="Xunit"/> in the csproj — moved here so that every global using is visible in
// one place instead of drifting between .cs and .csproj.
global using Xunit;
global using Snail.MCP.Memory.Core.Constants;
global using Snail.MCP.Memory.Core.Graph;
global using Snail.MCP.Memory.Core.Interfaces;
global using Snail.MCP.Memory.Core.Records;
global using Snail.MCP.Memory.Core.Utilities;
global using Snail.MCP.Memory.Application;
global using Snail.MCP.Memory.Infrastructure.Stores;
