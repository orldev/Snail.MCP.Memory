namespace Snail.MCP.Memory.Tests.TestDoubles;

/// <summary>What an application answer refused, held against the kind and the words the tools answer with.</summary>
internal static class Refused
{
    /// <summary>The answer is a failure of <paramref name="type"/> with <paramref name="error"/> and a hint.</summary>
    public static void As<T>(string type, string error, Answer<T> answer)
    {
        Assert.False(answer.IsSuccess, "the answer succeeded where a refusal was expected");
        Assert.Equal(type, answer.Type);
        Assert.Equal(error, answer.Error);
        Assert.False(string.IsNullOrWhiteSpace(answer.Hint), "a refusal without a hint leaves the model nothing to do");
    }
}
