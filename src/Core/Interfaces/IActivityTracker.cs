namespace Snail.MCP.Memory.Core.Interfaces;

/// <summary>Tracks the last MCP tool invocation — an "in use" signal for the idle watchdog that never reads stdin.</summary>
public interface IActivityTracker
{
    /// <summary>UTC instant of the most recent <see cref="Touch"/>.</summary>
    DateTime LastActivityUtc { get; }

    /// <summary>Records that a tool was just invoked.</summary>
    void Touch();
}
