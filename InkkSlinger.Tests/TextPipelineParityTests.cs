using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public class TextPipelineParityTests
{
    [Fact]
    public void TextBox_LayoutCache_ReportsHitsAndMisses()
    {
        var textBox = new TextBox
        {
            Text = "alpha\nbeta\ngamma"
        };

        textBox.PrimeLayoutCacheForTests(220f);
        textBox.PrimeLayoutCacheForTests(220f);
        var firstSnapshot = textBox.GetPerformanceSnapshot();

        Assert.True(firstSnapshot.LayoutCacheMissCount >= 1);
        Assert.True(firstSnapshot.LayoutCacheHitCount >= 1);

        textBox.TextWrapping = TextWrapping.NoWrap;
        textBox.PrimeLayoutCacheForTests(220f);
        var secondSnapshot = textBox.GetPerformanceSnapshot();

        Assert.True(secondSnapshot.LayoutCacheMissCount > firstSnapshot.LayoutCacheMissCount);
    }

    [Fact]
    public void TextBlock_LayoutCache_InvalidatesOnWidthAndTextChanges()
    {
        var textBlock = new TextBlock
        {
            Text = "one two three four five six",
            TextWrapping = TextWrapping.Wrap
        };

        textBlock.PrimeLayoutCacheForTests(180f);
        textBlock.PrimeLayoutCacheForTests(180f);
        var firstSnapshot = textBlock.GetPerformanceSnapshot();

        Assert.True(firstSnapshot.LayoutCacheMissCount >= 1);
        Assert.True(firstSnapshot.LayoutCacheHitCount >= 1);

        textBlock.PrimeLayoutCacheForTests(120f);
        var widthChangedSnapshot = textBlock.GetPerformanceSnapshot();
        Assert.True(widthChangedSnapshot.LayoutCacheMissCount > firstSnapshot.LayoutCacheMissCount);

        textBlock.Text = "changed text content";
        textBlock.PrimeLayoutCacheForTests(120f);
        var textChangedSnapshot = textBlock.GetPerformanceSnapshot();
        Assert.True(textChangedSnapshot.LayoutCacheMissCount > widthChangedSnapshot.LayoutCacheMissCount);

        textBlock.FontSize = 22f;
        textBlock.PrimeLayoutCacheForTests(120f);
        var fontSizeChangedSnapshot = textBlock.GetPerformanceSnapshot();
        Assert.True(fontSizeChangedSnapshot.LayoutCacheMissCount > textChangedSnapshot.LayoutCacheMissCount);

        textBlock.TextWrapping = TextWrapping.NoWrap;
        textBlock.PrimeLayoutCacheForTests(float.PositiveInfinity);
        var wrappingChangedSnapshot = textBlock.GetPerformanceSnapshot();
        Assert.True(wrappingChangedSnapshot.LayoutCacheMissCount > fontSizeChangedSnapshot.LayoutCacheMissCount);
    }

    [Fact]
    public void TextBlock_Measure_UsesMaxWidthConstrainedWrapWidth()
    {
        var textBlock = new TextBlock
        {
            Text = "Activity loaded welcome preset hyperlink none spell check remains disabled without an attached engine",
            TextWrapping = TextWrapping.Wrap
        };

        var intrinsicWidth = UiTextRenderer.MeasureWidth(textBlock, textBlock.Text, textBlock.FontSize);
        textBlock.MaxWidth = intrinsicWidth * 0.55f;

        var unconstrainedLayout = TextLayout.LayoutForElement(textBlock.Text, textBlock, textBlock.FontSize, intrinsicWidth + 10f, TextWrapping.Wrap);
        var constrainedLayout = TextLayout.LayoutForElement(textBlock.Text, textBlock, textBlock.FontSize, textBlock.MaxWidth, TextWrapping.Wrap);

        Assert.Single(unconstrainedLayout.Lines);
        Assert.True(constrainedLayout.Lines.Count > 1);

        textBlock.Measure(new Vector2(intrinsicWidth + 10f, float.PositiveInfinity));

        Assert.True(textBlock.DesiredSize.X <= textBlock.MaxWidth + 0.01f);
        Assert.Equal(constrainedLayout.Size.Y, textBlock.DesiredSize.Y, 3);
    }

    [Fact]
    public void CaretBlink_Invalidation_UsesTightDirtyRegion()
    {
        Dispatcher.ResetForTests();
        AnimationManager.Current.ResetForTests();

        var root = new Panel();
        var textBox = new TextBox
        {
            Text = "caret"
        };
        root.AddChild(textBox);
        textBox.SetValue(TextBox.IsFocusedProperty, true);

        var uiRoot = new UiRoot(root);
        var viewport = new Viewport(0, 0, 640, 360);

        uiRoot.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)), viewport);
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();

        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(600), TimeSpan.FromMilliseconds(600)),
            viewport);

        var dirtyRegions = uiRoot.GetDirtyRegionsSnapshotForTests();
        Assert.Single(dirtyRegions);
        Assert.False(uiRoot.IsFullDirtyForTests());
        Assert.True(dirtyRegions[0].Width <= 12f);
        Assert.True(dirtyRegions[0].Height <= 32f);
    }

    [Fact]
    public void WrappedTextInput_BatchRenderingUsesNewlineAwareGlyphPositions()
    {
        var textBox = new TextBox
        {
            Text = "TextBox sampleadsfadsfadsfadsfadsfadsa",
            TextWrapping = TextWrapping.Wrap,
            Width = 160f,
            Height = 220f
        };

        var wrappedLayout = TextLayout.LayoutForElement(textBox.Text, textBox, textBox.FontSize, 120f, TextWrapping.Wrap);
        Assert.True(wrappedLayout.Lines.Count > 1);

        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < wrappedLayout.Lines.Count; i++)
        {
            var line = wrappedLayout.Lines[i];
            builder.Append(line);

            if (i < wrappedLayout.Lines.Count - 1)
            {
                builder.Append('\n');
            }
        }

        var positions = UiTextRenderer.GetGlyphDrawPositionsForTests(
            UiTextRenderer.ResolveTypography(textBox, textBox.FontSize),
            builder.ToString());

        Assert.NotEmpty(positions);
        var firstLineY = positions[0].Y;
        Assert.Contains(positions, position => position.Y > firstLineY);
    }

    [Fact]
    public void TextLayout_GlobalMetrics_TrackCacheHitsMissesAndBuildModes()
    {
        TextLayout.ResetMetricsForTests();
        var typography = new UiTypography("Segoe UI", 12f, "Normal", "Normal");

        _ = TextLayout.Layout("alpha beta", typography, 200f, TextWrapping.NoWrap);
        _ = TextLayout.Layout("alpha beta", typography, 200f, TextWrapping.NoWrap);
        _ = TextLayout.Layout("alpha beta gamma delta", typography, 40f, TextWrapping.Wrap);

        var snapshot = TextLayout.GetMetricsSnapshot();

        Assert.Equal(3, snapshot.LayoutRequestCount);
        Assert.Equal(1, snapshot.CacheHitCount);
        Assert.Equal(2, snapshot.CacheMissCount);
        Assert.Equal(2, snapshot.BuildCount);
        Assert.Equal(1, snapshot.NoWrapBuildCount);
        Assert.Equal(1, snapshot.WrappedBuildCount);
        Assert.True(snapshot.TotalMeasuredTextLength > 0);
        Assert.True(snapshot.TotalProducedLineCount >= 2);
        Assert.True(snapshot.CacheEntryCount >= 2);
    }
}
