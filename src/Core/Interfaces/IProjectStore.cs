namespace Snail.MCP.Memory.Core.Interfaces;

/// <summary>The registry of projects that have memory.</summary>
/// <remarks>Lookup is by the case-folded key; the store keeps the display name.</remarks>
public interface IProjectStore
{
    /// <summary>The project with this key, or <c>null</c>.</summary>
    Task<ProjectInfo?> GetAsync(string projectKey, CancellationToken cancellationToken = default);

    /// <summary>Registers a project; returns <c>false</c> when the key is already taken.</summary>
    Task<bool> CreateAsync(ProjectInfo project, CancellationToken cancellationToken = default);

    /// <summary>Every registered project, unordered; filtering and ordering live in the identity.</summary>
    Task<IReadOnlyList<ProjectInfo>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Renames a project; <c>false</c> when the old key is missing or the new one is taken.</summary>
    Task<bool> RenameAsync(string oldKey, string newName, CancellationToken cancellationToken = default);

    /// <summary>Removes a project from the registry; returns whether it existed.</summary>
    Task<bool> DeleteAsync(string projectKey, CancellationToken cancellationToken = default);
}
