using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Snail.MCP.Memory.Tools;

#pragma warning disable MCP9005

/// <summary>The folder the client works in, as the client says it.</summary>
/// <remarks>
/// Over stdio the server starts in the client's folder, so its own working directory was the answer. A server shared
/// over HTTP started in the folder of whoever launched it, which is nobody's project: the client's first file root is.
/// Roots are deprecated from the 2026-07-28 revision of the protocol (SEP-2577) and reach the server only within a
/// session a client opened with initialize, as clients of the 2025 revisions do; a client without them gets the
/// server's folder, and with it the message asking for <c>projectName</c>.
/// </remarks>
internal static class ClientFolder
{
    /// <summary>How long a client that lists roots gets to answer before the server falls back to its own folder.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    /// <summary>The path of the client's first <c>file:</c> root, or null when it lists none or does not answer.</summary>
    public static async Task<string?> FindAsync(McpServer server, CancellationToken cancellationToken)
    {
        if (server.ClientCapabilities?.Roots is null)
            return null;

        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        patience.CancelAfter(Patience);

        try
        {
            var listed = await server.RequestRootsAsync(new ListRootsRequestParams(), patience.Token);

            return listed.Roots
                .Select(root => Uri.TryCreate(root.Uri, UriKind.Absolute, out var uri) && uri.IsFile ? uri.LocalPath : null)
                .FirstOrDefault(path => path is not null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (McpException)
        {
            return null;
        }
    }
}
