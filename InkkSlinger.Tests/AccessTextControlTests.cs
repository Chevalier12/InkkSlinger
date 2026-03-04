using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class AccessTextControlTests
{
    [Fact]
    public void TextMutation_UpdatesParsedFields()
    {
        var accessText = new AccessText
        {
            Text = "_Open"
        };

        Assert.Equal("Open", accessText.DisplayText);
        Assert.Equal('O', accessText.AccessKey);
        Assert.Equal(0, accessText.AccessKeyDisplayIndex);

        accessText.Text = "Sa_ve";

        Assert.Equal("Save", accessText.DisplayText);
        Assert.Equal('V', accessText.AccessKey);
        Assert.Equal(2, accessText.AccessKeyDisplayIndex);
    }

    [Fact]
    public void EmptyText_HasNoAccessKey()
    {
        var accessText = new AccessText
        {
            Text = string.Empty
        };

        Assert.Equal(string.Empty, accessText.DisplayText);
        Assert.Null(accessText.AccessKey);
        Assert.Equal(-1, accessText.AccessKeyDisplayIndex);
    }

    [Fact]
    public void WrappedMeasure_RemainsStableWithAccessMarkers()
    {
        var accessText = new AccessText
        {
            Text = "Sa_ve very long title",
            TextWrapping = TextWrapping.Wrap
        };

        accessText.Measure(new Vector2(64f, 200f));

        Assert.True(accessText.DesiredSize.X > 0f);
        Assert.True(accessText.DesiredSize.Y > 0f);
        Assert.Equal("Save very long title", accessText.DisplayText);
        Assert.Equal('V', accessText.AccessKey);
    }

    [Fact]
    public void TargetName_CanBeAssigned()
    {
        var accessText = new AccessText
        {
            TargetName = "SaveButton"
        };

        Assert.Equal("SaveButton", accessText.TargetName);
    }

    [Fact]
    public void WrappedLayout_LinesDoNotContainLineTerminators()
    {
        var layout = TextLayout.Layout("Save line one\nSave line two", font: null, availableWidth: 48f, TextWrapping.Wrap);
        Assert.NotEmpty(layout.Lines);
        Assert.DoesNotContain(layout.Lines, static line => line.Contains('\n') || line.Contains('\r'));
    }

    [Fact]
    public void MapAccessKeyDisplayIndex_ToWrappedLineAndColumn()
    {
        var lines = new List<string> { "Save", " very", " long" };
        Assert.True(AccessText.TryMapAccessKeyToLineAndColumn(lines, 6, out var lineIndex, out var columnIndex));
        Assert.Equal(1, lineIndex);
        Assert.Equal(2, columnIndex);
    }
}
