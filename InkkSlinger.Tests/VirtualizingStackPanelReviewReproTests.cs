using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public class VirtualizingStackPanelReviewReproTests
{
    [Fact]
    public void MakeVisible_WithAncestorScrollViewer_ScrollsViewerViewport()
    {
        var root = new Panel();
        var virtualizingPanel = new VirtualizingStackPanel
        {
            Orientation = Orientation.Vertical,
            IsVirtualizing = true,
            CacheLength = 0.5f,
            CacheLengthUnit = VirtualizationCacheLengthUnit.Page
        };

        for (var i = 0; i < 120; i++)
        {
            virtualizingPanel.AddChild(new Border { Height = 24f });
        }

        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = virtualizingPanel
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 200, 16);

        var target = Assert.IsType<Border>(virtualizingPanel.Children[40]);
        var itemBounds = new LayoutRect(0f, 40f * 24f, 10f, 24f);

        virtualizingPanel.MakeVisible(target, itemBounds);

        Assert.True(viewer.VerticalOffset > 0f);
        Assert.Equal(0f, virtualizingPanel.VerticalOffset);
    }

    [Fact]
    public void GetLogicalChildren_KeepsUnrealizedChildrenInLogicalTree()
    {
        var root = new Panel();
        var virtualizingPanel = new VirtualizingStackPanel
        {
            Orientation = Orientation.Vertical,
            IsVirtualizing = true,
            CacheLength = 0f,
            CacheLengthUnit = VirtualizationCacheLengthUnit.Page
        };

        for (var i = 0; i < 100; i++)
        {
            virtualizingPanel.AddChild(new Border { Height = 24f });
        }

        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = virtualizingPanel
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 200, 16);

        Assert.True(virtualizingPanel.RealizedChildrenCount < virtualizingPanel.Children.Count);

        var logicalChildren = virtualizingPanel.GetLogicalChildren().ToList();

        Assert.Equal(virtualizingPanel.Children.Count, logicalChildren.Count);
        Assert.Contains(virtualizingPanel.Children[50], logicalChildren);
        Assert.Same(virtualizingPanel, virtualizingPanel.Children[50].LogicalParent);
    }

    [Fact]
    public void ViewerOwnedScroll_WhenRealizedRangeChanges_UpdatesRetainedVisualChildren()
    {
        var root = new Panel();
        var virtualizingPanel = new VirtualizingStackPanel
        {
            Orientation = Orientation.Vertical,
            IsVirtualizing = true,
            CacheLength = 0f,
            CacheLengthUnit = VirtualizationCacheLengthUnit.Page
        };

        for (var i = 0; i < 120; i++)
        {
            virtualizingPanel.AddChild(new Border { Height = 24f });
        }

        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = virtualizingPanel
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 200, 16);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        var initialFirst = virtualizingPanel.FirstRealizedIndex;
        var initialFirstChild = virtualizingPanel.Children[initialFirst];

        viewer.ScrollToVerticalOffset(600f);
        RunLayout(uiRoot, 320, 200, 32);
        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.True(virtualizingPanel.FirstRealizedIndex > initialFirst);
        Assert.DoesNotContain(initialFirstChild, uiRoot.GetRetainedVisualOrderForTests());
        Assert.Contains(virtualizingPanel.Children[virtualizingPanel.FirstRealizedIndex], uiRoot.GetRetainedVisualOrderForTests());
    }

    [Fact]
    public void ViewerOwnedScroll_BeforeLayout_WhenRealizedRangeChanges_UpdatesRetainedVisualChildren()
    {
        var root = new Panel();
        var virtualizingPanel = new VirtualizingStackPanel
        {
            Orientation = Orientation.Vertical,
            IsVirtualizing = true,
            CacheLength = 0f,
            CacheLengthUnit = VirtualizationCacheLengthUnit.Page
        };

        for (var i = 0; i < 120; i++)
        {
            virtualizingPanel.AddChild(new Border { Height = 24f });
        }

        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = virtualizingPanel
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 200, 16);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        var initialFirst = virtualizingPanel.FirstRealizedIndex;
        var initialFirstChild = virtualizingPanel.Children[initialFirst];

        viewer.ScrollToVerticalOffset(600f);
        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.True(virtualizingPanel.FirstRealizedIndex > initialFirst);
        Assert.DoesNotContain(initialFirstChild, uiRoot.GetRetainedVisualOrderForTests());
        Assert.Contains(virtualizingPanel.Children[virtualizingPanel.FirstRealizedIndex], uiRoot.GetRetainedVisualOrderForTests());
    }

    [Fact]
    public void DemoWorkbenchScroll_WhenRealizedRangeChanges_DrawsNewlyRealizedCardContent()
    {
        var applicationResources = SnapshotApplicationResources();
        try
        {
            TestApplicationResources.LoadDemoAppResources();

            var view = new VirtualizingStackPanelView();
            var viewer = Assert.IsType<ScrollViewer>(view.FindName("WorkbenchScrollViewer"));
            var virtualizingPanel = Assert.IsType<VirtualizingStackPanel>(view.FindName("WorkbenchPanel"));
            var uiRoot = new UiRoot(view);

            RunLayout(uiRoot, 1900, 950, 16);
            uiRoot.RebuildRenderListForTests();
            uiRoot.ResetDirtyStateForTests();
            view.ClearRenderInvalidationRecursive();
            uiRoot.CompleteDrawStateForTests();

            var initialFirst = virtualizingPanel.FirstRealizedIndex;
            var initialFirstChild = virtualizingPanel.Children[initialFirst];

            viewer.ScrollToVerticalOffset(1400f);
            RunLayout(uiRoot, 1900, 950, 32);
            uiRoot.SynchronizeRetainedRenderListForTests();

            Assert.True(virtualizingPanel.FirstRealizedIndex > initialFirst);
            Assert.DoesNotContain(initialFirstChild, uiRoot.GetRetainedVisualOrderForTests());
            Assert.Contains(
                uiRoot.GetRetainedVisualOrderForTests(),
                visual => visual is TextBlock textBlock &&
                          string.Equals(textBlock.Text, $"Workbench card {virtualizingPanel.FirstRealizedIndex:000}", StringComparison.Ordinal));
        }
        finally
        {
            TestApplicationResources.Restore(applicationResources);
        }
    }

    [Fact]
    public void DemoWorkbenchScroll_WithinInitialPageCache_DrawsPartiallyVisibleCardDescendants()
    {
        var applicationResources = SnapshotApplicationResources();
        try
        {
            TestApplicationResources.LoadDemoAppResources();

            var view = new VirtualizingStackPanelView();
            var viewer = Assert.IsType<ScrollViewer>(view.FindName("WorkbenchScrollViewer"));
            var virtualizingPanel = Assert.IsType<VirtualizingStackPanel>(view.FindName("WorkbenchPanel"));
            var uiRoot = new UiRoot(view);

            RunLayout(uiRoot, 1900, 950, 16);
            uiRoot.RebuildRenderListForTests();
            uiRoot.ResetDirtyStateForTests();
            view.ClearRenderInvalidationRecursive();
            uiRoot.CompleteDrawStateForTests();

            viewer.ScrollToVerticalOffset(144f);
            RunLayout(uiRoot, 1900, 950, 32);
            var dirtySummary = uiRoot.GetDirtyRegionSummaryForTests(limit: 12);

            Assert.Equal(0, virtualizingPanel.FirstRealizedIndex);
            Assert.True(virtualizingPanel.LastRealizedIndex >= 3);
            Assert.True(
                virtualizingPanel.LayoutSlot.Y >= viewer.LayoutSlot.Y - 0.01f,
                $"Expected viewer-owned virtualization to keep the panel clip anchored to the viewport. panel={FormatRect(virtualizingPanel.LayoutSlot)} viewer={FormatRect(viewer.LayoutSlot)}");
            Assert.True(
                uiRoot.IsFullDirtyForTests() ||
                uiRoot.GetDirtyRegionsSnapshotForTests().Any(region => Contains(region, viewer.LayoutSlot)),
                $"Expected scroll to dirty the full workbench viewport. dirty={dirtySummary} viewer={FormatRect(viewer.LayoutSlot)}");

            var drawOrder = uiRoot.GetRetainedDrawOrderForClipForTests(viewer.LayoutSlot);
            var title = Assert.IsType<TextBlock>(Assert.Single(
                drawOrder,
                visual => visual is TextBlock textBlock &&
                          string.Equals(textBlock.Text, "Workbench card 003", StringComparison.Ordinal)));
            Assert.True(
                Intersects(title.LayoutSlot, viewer.LayoutSlot),
                $"Expected card 003 title to be inside the workbench viewport. title={FormatRect(title.LayoutSlot)} viewer={FormatRect(viewer.LayoutSlot)}");
        }
        finally
        {
            TestApplicationResources.Restore(applicationResources);
        }
    }

    [Fact]
    public void DemoWorkbenchScroll_RepeatedOffsetsBeforeNextFrame_DirtiesWorkbenchViewport()
    {
        var applicationResources = SnapshotApplicationResources();
        try
        {
            TestApplicationResources.LoadDemoAppResources();

            var view = new VirtualizingStackPanelView();
            var viewer = Assert.IsType<ScrollViewer>(view.FindName("WorkbenchScrollViewer"));
            var uiRoot = new UiRoot(view);

            RunLayout(uiRoot, 1900, 950, 16);
            uiRoot.RebuildRenderListForTests();
            uiRoot.ResetDirtyStateForTests();
            view.ClearRenderInvalidationRecursive();
            uiRoot.CompleteDrawStateForTests();

            viewer.ScrollToVerticalOffset(144f);
            viewer.ScrollToVerticalOffset(456f);

            Assert.True(
                uiRoot.IsFullDirtyForTests() ||
                uiRoot.GetDirtyRegionsSnapshotForTests().Any(region => Contains(region, viewer.LayoutSlot)),
                $"Expected repeated virtualized scrolling before the next frame to dirty the workbench viewport. dirty={uiRoot.GetDirtyRegionSummaryForTests(limit: 12)} viewer={FormatRect(viewer.LayoutSlot)}");
            Assert.True(uiRoot.ShouldDrawThisFrame(
                new GameTime(TimeSpan.FromMilliseconds(32), TimeSpan.FromMilliseconds(16)),
                new Viewport(0, 0, 1900, 950)));
        }
        finally
        {
            TestApplicationResources.Restore(applicationResources);
        }
    }

    [Fact]
    public void DemoWorkbenchScroll_WhenRangeAdvances_RemovesInitialCardsFromDrawTraversal()
    {
        var applicationResources = SnapshotApplicationResources();
        try
        {
            TestApplicationResources.LoadDemoAppResources();

            var view = new VirtualizingStackPanelView();
            var viewer = Assert.IsType<ScrollViewer>(view.FindName("WorkbenchScrollViewer"));
            var virtualizingPanel = Assert.IsType<VirtualizingStackPanel>(view.FindName("WorkbenchPanel"));
            var uiRoot = new UiRoot(view);

            RunLayout(uiRoot, 1900, 950, 16);
            uiRoot.RebuildRenderListForTests();
            uiRoot.ResetDirtyStateForTests();
            view.ClearRenderInvalidationRecursive();
            uiRoot.CompleteDrawStateForTests();

            viewer.ScrollToVerticalOffset(648f);
            RunLayout(uiRoot, 1900, 950, 32);
            uiRoot.SynchronizeRetainedRenderListForTests();

            Assert.True(virtualizingPanel.FirstRealizedIndex >= 2);
            var drawOrder = uiRoot.GetRetainedDrawOrderForClipForTests(viewer.LayoutSlot);
            Assert.DoesNotContain(
                drawOrder,
                visual => visual is TextBlock textBlock &&
                          string.Equals(textBlock.Text, "Workbench card 000", StringComparison.Ordinal));
            var visibleCardTitles = string.Join(
                " | ",
                drawOrder
                    .OfType<TextBlock>()
                    .Where(static textBlock => textBlock.Text.StartsWith("Workbench card ", StringComparison.Ordinal))
                    .Select(static textBlock => $"{textBlock.Text}@{FormatRect(textBlock.LayoutSlot)}"));
            Assert.True(
                drawOrder.Any(visual => visual is TextBlock textBlock &&
                                        string.Equals(textBlock.Text, "Workbench card 005", StringComparison.Ordinal)),
                $"visibleCardTitles={visibleCardTitles}; range={virtualizingPanel.FirstRealizedIndex}..{virtualizingPanel.LastRealizedIndex}; viewer={FormatRect(viewer.LayoutSlot)} panel={FormatRect(virtualizingPanel.LayoutSlot)}");
        }
        finally
        {
            TestApplicationResources.Restore(applicationResources);
        }
    }

    private static Dictionary<object, object> SnapshotApplicationResources()
    {
        return UiApplication.Current.Resources.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value);
    }

    private static bool Contains(LayoutRect outer, LayoutRect inner)
    {
        return outer.X <= inner.X + 0.01f &&
               outer.Y <= inner.Y + 0.01f &&
               outer.X + outer.Width >= inner.X + inner.Width - 0.01f &&
               outer.Y + outer.Height >= inner.Y + inner.Height - 0.01f;
    }

    private static bool Intersects(LayoutRect left, LayoutRect right)
    {
        return left.X < right.X + right.Width &&
               left.X + left.Width > right.X &&
               left.Y < right.Y + right.Height &&
               left.Y + left.Height > right.Y;
    }

    private static string FormatRect(LayoutRect rect)
    {
        return $"{rect.X:0.###},{rect.Y:0.###},{rect.Width:0.###},{rect.Height:0.###}";
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }
}
