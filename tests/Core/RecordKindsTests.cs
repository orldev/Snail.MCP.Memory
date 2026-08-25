namespace Snail.MCP.Memory.Tests.Core;

public class RecordKindsTests
{
    [Theory]
    [InlineData("Lesson", "lesson")]
    [InlineData(" design token ", "design_token")]
    [InlineData("code-review", "code_review")]
    [InlineData("", "note")]
    [InlineData(null, "note")]
    public void Normalize_LowercasesAndDefaultsToNote(string? raw, string expected) =>
        Assert.Equal(expected, RecordKinds.Normalize(raw));

    /// Only events expire by age; every other kind — canonical or invented — lives until
    /// superseded or forgotten, because silently losing durable knowledge is the one failure
    /// a memory must never have.
    [Fact]
    public void OnlyEventsAreEphemeral()
    {
        Assert.True(RecordKinds.IsEphemeral(RecordKinds.Event));

        foreach (var kind in RecordKinds.Canonical.Where(kind => kind != RecordKinds.Event))
            Assert.False(RecordKinds.IsEphemeral(kind));

        Assert.False(RecordKinds.IsEphemeral("invented_kind"));
    }

    [Fact]
    public void TransferableKinds_AreASubsetOfTheCanonicalCore()
    {
        foreach (var kind in RecordKinds.Transferable)
            Assert.Contains(kind, RecordKinds.Canonical);
    }
}
