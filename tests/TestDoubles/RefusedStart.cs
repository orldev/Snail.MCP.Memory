using System.Diagnostics;
using Snail.MCP.Memory.Infrastructure.Configuration;
using Snail.Sdk.Mcp.Hosting;

namespace Snail.MCP.Memory.Tests.TestDoubles;

/// <summary>How the server's program ended: its exit code and everything it wrote to stderr.</summary>
/// <param name="Code">The exit code.</param>
/// <param name="Stderr">What it wrote to stderr, trimmed.</param>
internal readonly record struct Exited(int Code, string Stderr);

/// <summary>The server's program run for a start it should refuse, and read once it ends.</summary>
/// <remarks>
/// <c>ServerProgram.StartAsync</c> opens an MCP session, and a program that refuses at once may close its stdin before
/// the session's first write: the client then throws an <see cref="IOException"/>, "MCP server process exited
/// unexpectedly", in place of <c>ServerExitedException</c>. Against the publication, which starts faster than the build
/// beside the tests, that was the answer on mini on 09.11.2026. A refused start needs no session: run, wait, read.
/// </remarks>
internal static class RefusedStart
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(60);

    /// <summary>Runs the build beside the tests, or the one SNAIL_MEMORY_SERVER names, with <paramref name="environment"/> and a home and data folder of its own.</summary>
    public static async Task<Exited> RunAsync(IReadOnlyDictionary<string, string?> environment)
    {
        var dataDirectory = TestFolder.Create();
        var (command, arguments) = ServerProgram.Command("Snail.MCP.Memory", "SNAIL_MEMORY_SERVER");
        var start = new ProcessStartInfo
        {
            FileName = command,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        start.Environment["SNAIL_MCP_MEMORY_DIRECTORY"] = dataDirectory;
        start.Environment[ServerSettings.FileVariableOf(StorageOptions.ServerName)] = ServerProgram.NoSettingsFile;
        start.Environment["HOME"] = ServerProgram.Home;
        start.Environment["USERPROFILE"] = ServerProgram.Home;

        foreach (var (name, value) in environment)
            start.Environment[name] = value;

        try
        {
            using var process = Process.Start(start)!;
            using var deadline = new CancellationTokenSource(Deadline);
            var stderr = process.StandardError.ReadToEndAsync(deadline.Token);

            await process.WaitForExitAsync(deadline.Token);

            return new Exited(process.ExitCode, (await stderr).Trim());
        }
        finally
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }
}
