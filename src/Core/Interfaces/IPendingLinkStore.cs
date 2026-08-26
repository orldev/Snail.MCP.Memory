namespace Snail.MCP.Memory.Core.Interfaces;

/// <summary>Persistence of unconfirmed link suggestions between projects.</summary>
/// <remarks>A pair is undirected; a dismissed pair is kept so it is never suggested again.</remarks>
public interface IPendingLinkStore
{
    /// <summary>Stores a suggestion unless the pair is already suggested or dismissed; returns whether it was stored.</summary>
    Task<bool> ProposeAsync(PendingLink link, CancellationToken cancellationToken = default);

    /// <summary>Open suggestions touching the project, oldest first; empty key — every project.</summary>
    Task<IReadOnlyList<PendingLink>> ListAsync(string projectKey = "", CancellationToken cancellationToken = default);

    /// <summary>Removes the pair's suggestion in any state — an edge between the two settles the question. Returns the count.</summary>
    Task<int> ResolveAsync(string aKey, string bKey, CancellationToken cancellationToken = default);

    /// <summary>Marks the pair as rejected so it is never suggested again.</summary>
    Task DismissAsync(string project, string other, DateTimeOffset when, CancellationToken cancellationToken = default);

    /// <summary>Rewrites a project name on every suggestion touching it; returns how many changed.</summary>
    Task<int> RenameProjectAsync(string oldKey, string newName, CancellationToken cancellationToken = default);
}
