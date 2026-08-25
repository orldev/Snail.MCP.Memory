using Microsoft.Extensions.Logging;

namespace Snail.MCP.Memory.Infrastructure.Embeddings;

/// <summary>Downloads a model file to its configured path on first use.</summary>
/// <remarks>Goes through a temp file and an atomic rename: a crash must not leave a half model behind.</remarks>
internal static class ModelDownloader
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(30) };

    /// <summary>Downloads <paramref name="url"/> into <paramref name="path"/> unless the file already exists.</summary>
    public static async Task EnsureAsync(string url, string path, ILogger logger, CancellationToken cancellationToken)
    {
        if (File.Exists(path))
            return;

        if (Path.GetDirectoryName(Path.GetFullPath(path)) is { Length: > 0 } directory)
            Directory.CreateDirectory(directory);

        logger.LogInformation("Downloading embedding asset {Url} into {Path}...", url, path);

        var temporaryPath = $"{path}.tmp";

        try
        {
            using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using (var target = File.Create(temporaryPath))
                await response.Content.CopyToAsync(target, cancellationToken);

            File.Move(temporaryPath, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            throw;
        }

        logger.LogInformation("Downloaded {Path} ({Size} MB).", path, new FileInfo(path).Length / (1024 * 1024));
    }
}
