namespace Snail.MCP.Memory.Tests.Core;

public class LinkTypesTests
{
    [Theory]
    [InlineData("uses", "uses")]
    [InlineData("Depends-On", "depends_on")]
    [InlineData("design of", "design_of")]
    public void TryNormalize_AcceptsKnownTypes(string raw, string expected)
    {
        Assert.True(LinkTypes.TryNormalize(raw, out var type));
        Assert.Equal(expected, type);
    }

    /// There is no default type anymore: without related_to an empty input names nothing,
    /// and guessing a direction for the caller would store a fact nobody stated.
    [Fact]
    public void TryNormalize_RejectsEmptyInput()
    {
        Assert.False(LinkTypes.TryNormalize("", out _));
    }

    [Fact]
    public void TryNormalize_RejectsInventedType()
    {
        Assert.False(LinkTypes.TryNormalize("inspired_by", out _));
    }

    /// The symmetric types of the document era are gone: similarity is recall's job.
    [Theory]
    [InlineData("related_to")]
    [InlineData("co_access")]
    public void TryNormalize_RejectsTheRetiredSymmetricTypes(string retired)
    {
        Assert.False(LinkTypes.TryNormalize(retired, out _));
    }
}
