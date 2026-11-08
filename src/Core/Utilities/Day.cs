using System.Globalization;

namespace Snail.MCP.Memory.Core.Utilities;

/// <summary>The one way the memory writes a date: the day, invariant.</summary>
public static class Day
{
    /// <summary>The day of <paramref name="at"/>, <c>yyyy-MM-dd</c>.</summary>
    public static string Of(DateTimeOffset at) => at.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
