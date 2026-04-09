using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class TextBlockStableTextInvalidationRegressionTests
{
    [Fact]
    public void WrappedTextChange_WithDifferentDesiredSize_StillInvalidatesMeasureUpTree()
    {
        var (uiRoot, viewer, content, target) = CreateWrappedInspectorFixture();
        RunLayout(uiRoot, 640, 480, 16);

        var beforeViewerMeasureWork = viewer.MeasureWorkCount;
        var beforeContentMeasureWork = content.MeasureWorkCount;
        var beforeTargetMeasureWork = target.MeasureWorkCount;
        var beforeViewerMeasureInvalidations = viewer.MeasureInvalidationCount;
        var beforeContentMeasureInvalidations = content.MeasureInvalidationCount;
        var beforeTargetMeasureInvalidations = target.MeasureInvalidationCount;
        var beforeDesired = target.DesiredSize;

        target.Text = "Focus bounds: X=372, Y=549, Right=600, Bottom=689. Desired size follows the live card instead of a placeholder host, and the inspector should expand into an extra wrapped line when the text grows materially longer than the original telemetry sentence.";

        Assert.True(target.MeasureInvalidationCount > beforeTargetMeasureInvalidations);
        Assert.True(content.MeasureInvalidationCount > beforeContentMeasureInvalidations);
        Assert.True(viewer.MeasureInvalidationCount >= beforeViewerMeasureInvalidations);

        RunLayout(uiRoot, 640, 480, 32);

        Assert.True(viewer.MeasureWorkCount >= beforeViewerMeasureWork);
        Assert.True(content.MeasureWorkCount > beforeContentMeasureWork);
        Assert.True(target.MeasureWorkCount > beforeTargetMeasureWork);
        Assert.True(target.DesiredSize.Y > beforeDesired.Y);
    }

    private static (UiRoot UiRoot, ScrollViewer Viewer, StackPanel Content, TextBlock Target) CreateWrappedInspectorFixture()
    {
        var root = new Panel();
        var target = new TextBlock
        {
            Name = "PositionValueText",
            Text = "Focus bounds: X=336, Y=525, Right=564, Bottom=665.",
            TextWrapping = TextWrapping.Wrap
        };

        var content = new StackPanel();
        content.AddChild(new TextBlock
        {
            Text = "Live telemetry",
            TextWrapping = TextWrapping.Wrap
        });
        content.AddChild(target);
        content.AddChild(new TextBlock
        {
            Text = "Focus size: 228 x 140. Desired size follows the live card instead of a placeholder host.",
            TextWrapping = TextWrapping.Wrap
        });

        var viewer = new ScrollViewer
        {
            Width = 281f,
            Height = 160f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };

        root.AddChild(viewer);
        return (new UiRoot(root), viewer, content, target);
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }
}
