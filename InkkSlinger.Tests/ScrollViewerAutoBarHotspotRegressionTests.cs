using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ScrollViewerAutoBarHotspotRegressionTests
{
    [Fact]
    public void VerticalAutoBar_DoesNotRemeasureWhenPreviousVisibleStateBecomesHidden()
    {
        var (uiRoot, viewer) = CreateAutoVerticalViewerFixture();
        RunLayout(uiRoot, 640, 900, 16);

        Assert.True(viewer.ExtentHeight > viewer.ViewportHeight,
            $"Expected the initial layout to require a vertical bar. extent={viewer.ExtentHeight}, viewport={viewer.ViewportHeight}");

        var beforeLargeLayout = viewer.GetScrollViewerSnapshotForDiagnostics();

        viewer.Height = 760f;
        RunLayout(uiRoot, 640, 1000, 32);

        Assert.True(viewer.ExtentHeight <= viewer.ViewportHeight + 0.01f,
            $"Expected the taller layout to fit content without a vertical bar. extent={viewer.ExtentHeight}, viewport={viewer.ViewportHeight}");

        var afterLargeLayout = viewer.GetScrollViewerSnapshotForDiagnostics();
        var deltaIterations = afterLargeLayout.ResolveBarsAndMeasureContentIterationCount - beforeLargeLayout.ResolveBarsAndMeasureContentIterationCount;
        var deltaMeasureContentCalls = afterLargeLayout.MeasureContentCallCount - beforeLargeLayout.MeasureContentCallCount;
        var deltaVerticalFlips = afterLargeLayout.ResolveBarsAndMeasureContentVerticalFlipCount - beforeLargeLayout.ResolveBarsAndMeasureContentVerticalFlipCount;
        var deltaInitialVerticalVisible = afterLargeLayout.ResolveBarsAndMeasureContentInitialVerticalVisibleCount - beforeLargeLayout.ResolveBarsAndMeasureContentInitialVerticalVisibleCount;
        var deltaInitialVerticalHidden = afterLargeLayout.ResolveBarsAndMeasureContentInitialVerticalHiddenCount - beforeLargeLayout.ResolveBarsAndMeasureContentInitialVerticalHiddenCount;
        var deltaResolvedVerticalHidden = afterLargeLayout.ResolveBarsAndMeasureContentResolvedVerticalHiddenCount - beforeLargeLayout.ResolveBarsAndMeasureContentResolvedVerticalHiddenCount;

        Assert.Equal(1, deltaIterations);
        Assert.Equal(1, deltaMeasureContentCalls);
        Assert.Equal(0, deltaVerticalFlips);
        Assert.Equal(0, deltaInitialVerticalVisible);
        Assert.Equal(1, deltaInitialVerticalHidden);
        Assert.Equal(1, deltaResolvedVerticalHidden);
        Assert.Contains("path=remeasure;initial=0,0", afterLargeLayout.ResolveBarsAndMeasureContentLastTrace, StringComparison.Ordinal);
        Assert.Contains("|result=0,0", afterLargeLayout.ResolveBarsAndMeasureContentLastTrace, StringComparison.Ordinal);
    }

    [Fact]
    public void VerticalAutoBar_DoesNotRemeasureWhenPreviousHiddenStateBecomesVisible()
    {
        var (uiRoot, viewer) = CreateAutoVerticalViewerFixture();
        viewer.Height = 760f;
        RunLayout(uiRoot, 640, 1000, 16);

        Assert.True(viewer.ExtentHeight <= viewer.ViewportHeight + 0.01f,
            $"Expected the taller layout to start without a vertical bar. extent={viewer.ExtentHeight}, viewport={viewer.ViewportHeight}");

        var beforeSmallLayout = viewer.GetScrollViewerSnapshotForDiagnostics();

        viewer.Height = 605f;
        RunLayout(uiRoot, 640, 900, 32);

        Assert.True(viewer.ExtentHeight > viewer.ViewportHeight,
            $"Expected the shorter layout to require a vertical bar. extent={viewer.ExtentHeight}, viewport={viewer.ViewportHeight}");

        var afterSmallLayout = viewer.GetScrollViewerSnapshotForDiagnostics();
        var deltaIterations = afterSmallLayout.ResolveBarsAndMeasureContentIterationCount - beforeSmallLayout.ResolveBarsAndMeasureContentIterationCount;
        var deltaMeasureContentCalls = afterSmallLayout.MeasureContentCallCount - beforeSmallLayout.MeasureContentCallCount;
        var deltaVerticalFlips = afterSmallLayout.ResolveBarsAndMeasureContentVerticalFlipCount - beforeSmallLayout.ResolveBarsAndMeasureContentVerticalFlipCount;
        var deltaInitialVerticalVisible = afterSmallLayout.ResolveBarsAndMeasureContentInitialVerticalVisibleCount - beforeSmallLayout.ResolveBarsAndMeasureContentInitialVerticalVisibleCount;
        var deltaInitialVerticalHidden = afterSmallLayout.ResolveBarsAndMeasureContentInitialVerticalHiddenCount - beforeSmallLayout.ResolveBarsAndMeasureContentInitialVerticalHiddenCount;
        var deltaResolvedVerticalVisible = afterSmallLayout.ResolveBarsAndMeasureContentResolvedVerticalVisibleCount - beforeSmallLayout.ResolveBarsAndMeasureContentResolvedVerticalVisibleCount;

        Assert.Equal(1, deltaIterations);
        Assert.Equal(1, deltaMeasureContentCalls);
        Assert.Equal(0, deltaVerticalFlips);
        Assert.Equal(1, deltaInitialVerticalVisible);
        Assert.Equal(0, deltaInitialVerticalHidden);
        Assert.Equal(1, deltaResolvedVerticalVisible);
        Assert.Contains("path=remeasure;initial=0,1", afterSmallLayout.ResolveBarsAndMeasureContentLastTrace, StringComparison.Ordinal);
        Assert.Contains("|result=0,1", afterSmallLayout.ResolveBarsAndMeasureContentLastTrace, StringComparison.Ordinal);
    }

    private static (UiRoot UiRoot, ScrollViewer Viewer) CreateAutoVerticalViewerFixture()
    {
        var root = new Panel();
        var content = new FixedMeasureElement(new Vector2(300f, 700f));
        var viewer = new ScrollViewer
        {
            Width = 320f,
            Height = 605f,
            BorderThickness = 1f,
            ScrollBarThickness = 12f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        root.AddChild(viewer);
        return (new UiRoot(root), viewer);
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }

    private sealed class FixedMeasureElement(Vector2 desiredSize) : FrameworkElement
    {
        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            _ = availableSize;
            return desiredSize;
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            return finalSize;
        }
    }
}