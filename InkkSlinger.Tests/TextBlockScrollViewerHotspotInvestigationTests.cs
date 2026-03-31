using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class TextBlockScrollViewerHotspotInvestigationTests
{
    [Fact]
    public void ShortAutoScrollViewer_NarrowsWrappedInspectorText()
    {
        var large = MeasureInspectorTextWidth(viewerHeight: 700f);
        var small = MeasureInspectorTextWidth(viewerHeight: 180f);

        Assert.True(small.LayoutWidth < large.LayoutWidth,
            $"Expected short auto-scroll viewer to reserve scrollbar width for wrapped text. large={large.LayoutWidth}, small={small.LayoutWidth}");
        Assert.True(large.LayoutWidth - small.LayoutWidth >= 10f,
            $"Expected scrollbar reservation to reduce wrapped text width by roughly a scrollbar gutter. large={large.LayoutWidth}, small={small.LayoutWidth}");
    }

    [Fact]
    public void WrappedStaticText_ReusesLayout_WhenWidthAlternatesBetweenInspectorWidths()
    {
        var large = MeasureInspectorTextWidth(viewerHeight: 700f);
        var small = MeasureInspectorTextWidth(viewerHeight: 180f);

        var stable = RunWidthChurnVariant(large.LayoutWidth, large.LayoutWidth, frames: 120);
        var churn = RunWidthChurnVariant(large.LayoutWidth, small.LayoutWidth, frames: 120);

        Assert.True(stable.ResolveLayoutCacheMissCount <= 2,
            $"Expected stable width to keep wrapped layout cached. actual={stable.ResolveLayoutCacheMissCount}");
        Assert.True(churn.ResolveLayoutCacheMissCount <= 3,
            $"Expected alternating inspector widths to reuse cached wrapped layouts after the first widths are seen. actual={churn.ResolveLayoutCacheMissCount}");
        Assert.True(churn.ResolveLayoutCacheHitCount >= 100,
            $"Expected alternating inspector widths to hit the wrapped layout cache for most passes. actual={churn.ResolveLayoutCacheHitCount}");
        Assert.True(churn.ResolveLayoutCacheMissCount <= stable.ResolveLayoutCacheMissCount + 1,
            $"Expected width churn misses to stay near the stable-width baseline. stable={stable.ResolveLayoutCacheMissCount}, churn={churn.ResolveLayoutCacheMissCount}");
        Assert.Equal(1, churn.TextPropertyChangeCount);
    }

    private static InspectorWidthResult MeasureInspectorTextWidth(float viewerHeight)
    {
        var root = new Panel();
        var outer = new Border
        {
            Width = 320f,
            Height = viewerHeight,
            Padding = new Thickness(0f)
        };
        root.AddChild(outer);

        var staticText = new TextBlock
        {
            Text = "Left/Top mode keeps the focus card measured from the canvas origin while the legend, chip, and inspector continue to exercise mixed edge combinations.",
            TextWrapping = TextWrapping.Wrap
        };

        var content = new StackPanel();
        content.AddChild(CreateSection("Live telemetry", new TextBlock
        {
            Text = "Focus bounds: X=42, Y=17, Right=270, Bottom=158, mixed anchors enabled, guide layer visible, viewport constrained to the inspector rail.",
            TextWrapping = TextWrapping.Wrap
        }));
        content.AddChild(CreateSection("Stage metrics", new TextBlock
        {
            Text = "Stage metrics: canvas focus card, inspector rail, mixed anchors, and drag telemetry.",
            TextWrapping = TextWrapping.Wrap
        }, staticText));

        var viewer = new ScrollViewer
        {
            Width = 320f,
            Height = viewerHeight,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        outer.Child = viewer;

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 640, Math.Max(400, (int)MathF.Ceiling(viewerHeight) + 100), 16);

        return new InspectorWidthResult(staticText.ActualWidth);
    }

    private static TextBlockRuntimeDiagnosticsSnapshot RunWidthChurnVariant(float wideWidth, float narrowWidth, int frames)
    {
        var text = new TextBlock
        {
            Text = "Left/Top mode keeps the focus card measured from the canvas origin while the legend, chip, and inspector continue to exercise mixed edge combinations.",
            TextWrapping = TextWrapping.Wrap
        };

        MeasureAndArrange(text, wideWidth);

        for (var i = 0; i < frames; i++)
        {
            MeasureAndArrange(text, i % 2 == 0 ? wideWidth : narrowWidth);
        }

        return text.GetRuntimeDiagnosticsForTests();
    }

    private static void MeasureAndArrange(TextBlock text, float width)
    {
        text.Measure(new Vector2(width, 1000f));
        text.Arrange(new LayoutRect(0f, 0f, width, 1000f));
    }

    private static Border CreateSection(string headerText, params TextBlock[] bodies)
    {
        var section = new StackPanel();
        section.AddChild(new TextBlock { Text = headerText });
        foreach (var body in bodies)
        {
            section.AddChild(body);
        }

        return new Border
        {
            Padding = new Thickness(13f, 13f, 13f, 13f),
            Margin = new Thickness(0f, 0f, 0f, 10f),
            Child = section
        };
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }

    private readonly record struct InspectorWidthResult(float LayoutWidth);
}