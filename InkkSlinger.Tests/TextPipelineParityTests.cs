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
}
