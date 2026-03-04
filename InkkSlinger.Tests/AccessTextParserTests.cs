using Xunit;

namespace InkkSlinger.Tests;

public sealed class AccessTextParserTests
{
    [Fact]
    public void Parse_LeadingMarker_ExtractsKeyAndDisplayText()
    {
        var parsed = AccessTextParser.Parse("_File");

        Assert.Equal("File", parsed.DisplayText);
        Assert.Equal('F', parsed.AccessKey);
        Assert.Equal(0, parsed.AccessKeyDisplayIndex);
    }

    [Fact]
    public void Parse_MiddleMarker_ExtractsCorrectIndex()
    {
        var parsed = AccessTextParser.Parse("E_xit");

        Assert.Equal("Exit", parsed.DisplayText);
        Assert.Equal('X', parsed.AccessKey);
        Assert.Equal(1, parsed.AccessKeyDisplayIndex);
    }

    [Fact]
    public void Parse_EscapedMarker_ProducesLiteralUnderscore()
    {
        var parsed = AccessTextParser.Parse("__Literal");

        Assert.Equal("_Literal", parsed.DisplayText);
        Assert.Null(parsed.AccessKey);
        Assert.Equal(-1, parsed.AccessKeyDisplayIndex);
    }

    [Fact]
    public void Parse_NoMarkers_HasNoAccessKey()
    {
        var parsed = AccessTextParser.Parse("NoMarkers");

        Assert.Equal("NoMarkers", parsed.DisplayText);
        Assert.Null(parsed.AccessKey);
        Assert.Equal(-1, parsed.AccessKeyDisplayIndex);
    }

    [Fact]
    public void Parse_TrailingMarker_IgnoredSafely()
    {
        var parsed = AccessTextParser.Parse("Save_");

        Assert.Equal("Save_", parsed.DisplayText);
        Assert.Null(parsed.AccessKey);
        Assert.Equal(-1, parsed.AccessKeyDisplayIndex);
    }
}
