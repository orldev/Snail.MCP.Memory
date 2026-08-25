namespace Snail.MCP.Memory.Core.Utilities;

/// <summary>Derives a project name from the directory the client was started in.</summary>
/// <remarks>A generic container folder is refused: memory named <c>src</c> would be shared by everything underneath it.</remarks>
internal static class DefaultProjectName
{
    /// <summary>Folder names that describe a container, not a project; matched case-insensitively.</summary>
    public static readonly IReadOnlySet<string> GenericFolders = new HashSet<string>(ProjectIdentity.Comparer)
    {
        "app", "apps", "bin", "build", "code", "dev", "desktop", "documents", "downloads",
        "git", "github", "home", "lib", "main", "master", "obj", "out", "project", "projects",
        "repo", "repos", "sandbox", "source", "sources", "src", "temp", "test", "tests", "tmp",
        "trunk", "users", "work", "workspace", "www", "design", "designs"
    };

    /// <summary>Whether the folder name describes a container rather than a project.</summary>
    public static bool IsGeneric(string folderName) => GenericFolders.Contains(folderName);

    /// <summary>Last path segment, or empty when the path is missing, a root, the user profile, or a generic container.</summary>
    public static string FromDirectory(string? directory, string? home = null)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return "";

        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory.Trim()));

        if (!string.IsNullOrWhiteSpace(home) &&
            ProjectIdentity.Equal(full, Path.TrimEndingDirectorySeparator(Path.GetFullPath(home))))
            return "";

        var folder = Path.GetFileName(full);

        return string.IsNullOrEmpty(folder) || IsGeneric(folder) ? "" : folder;
    }
}
