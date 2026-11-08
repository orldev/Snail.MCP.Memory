namespace Snail.MCP.Memory.Core.Answers;

/// <summary>How a recall found its records.</summary>
public enum RecallSearch
{
    /// <summary>No query: the newest records.</summary>
    Overview,

    /// <summary>By meaning, through the embedding model.</summary>
    Meaning,

    /// <summary>By plain text, since the embedding model is unavailable.</summary>
    Text,
}
