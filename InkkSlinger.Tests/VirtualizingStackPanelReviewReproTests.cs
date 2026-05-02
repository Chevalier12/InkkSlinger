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
    public void MakeVisible_WithAncestorScrollViewer_UsesTargetLocalRectangleForUnrealizedChild()
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

        var target = Assert.IsType<Border>(virtualizingPanel.Children[40]);
        Assert.DoesNotContain(target, virtualizingPanel.GetVisualChildren());

        virtualizingPanel.MakeVisible(target, new LayoutRect(0f, 0f, 10f, 24f));
        RunLayout(uiRoot, 320, 200, 32);

        Assert.True(viewer.VerticalOffset > 0f);
        Assert.Contains(target, virtualizingPanel.GetVisualChildren());
        Assert.True(
            Intersects(target.LayoutSlot, viewer.LayoutSlot),
            $"Expected target to be visible. target={FormatRect(target.LayoutSlot)} viewer={FormatRect(viewer.LayoutSlot)}");
    }

    [Fact]
    public void MakeVisible_WithAncestorScrollViewer_UsesTargetLocalRectangleForRealizedScrolledChild()
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
        viewer.ScrollToVerticalOffset(600f);
        RunLayout(uiRoot, 320, 200, 32);

        var target = Assert.IsType<Border>(virtualizingPanel.Children[26]);
        Assert.Contains(target, virtualizingPanel.GetVisualChildren());

        virtualizingPanel.MakeVisible(target, new LayoutRect(0f, 0f, 10f, 24f));

        Assert.Equal(600f, viewer.VerticalOffset, 2);
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
    public void ViewerOwnedScroll_AfterChildRemovalBeforeLayout_DoesNotUseStaleSizeCaches()
    {
        var root = new Panel();
        var virtualizingPanel = new VirtualizingStackPanel
        {
            Orientation = Orientation.Vertical,
            IsVirtualizing = true,
            CacheLength = 0f,
            CacheLengthUnit = VirtualizationCacheLengthUnit.Page
        };

        for (var i = 0; i < 80; i++)
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

        Assert.True(virtualizingPanel.RemoveChildAt(2));
        viewer.ScrollToVerticalOffset(300f);
        RunLayout(uiRoot, 320, 200, 32);

        Assert.True(viewer.VerticalOffset > 0f);
        Assert.True(virtualizingPanel.FirstRealizedIndex >= 0);
    }

    [Fact]
    public void ViewerOwnedScroll_ArrangeOnlyPath_DeepSyncsRetainedChildBounds()
    {
        var root = new Panel();
        var virtualizingPanel = new VirtualizingStackPanel
        {
            Orientation = Orientation.Vertical,
            IsVirtualizing = true,
            CacheLength = 1f,
            CacheLengthUnit = VirtualizationCacheLengthUnit.Page
        };

        for (var i = 0; i < 80; i++)
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
        uiRoot.SynchronizeRetainedRenderListForTests();

        viewer.ScrollToVerticalOffset(240f);
        RunLayout(uiRoot, 320, 200, 32);
        uiRoot.SynchronizeRetainedRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        var beforeFirst = virtualizingPanel.FirstRealizedIndex;
        viewer.ScrollToVerticalOffset(120f);
        RunLayout(uiRoot, 320, 200, 48);

        Assert.Equal(beforeFirst, virtualizingPanel.FirstRealizedIndex);

        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.True(
            uiRoot.GetPerformanceTelemetrySnapshotForTests().RetainedForceDeepSyncCount > 0,
            "Expected viewer-owned virtualized scrolling to force a retained deep sync for the realized row window.");
        Assert.Equal("ok", uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests());
    }

    [Fact]
    public void NestedVirtualizingStackPanel_DoesNotTreatAncestorScrollViewerAsOffsetOwner()
    {
        var root = new Panel();
        var virtualizingPanel = new VirtualizingStackPanel
        {
            Orientation = Orientation.Vertical,
            IsVirtualizing = true,
            CacheLength = 0f,
            CacheLengthUnit = VirtualizationCacheLengthUnit.Page
        };

        for (var i = 0; i < 80; i++)
        {
            virtualizingPanel.AddChild(new Border { Height = 24f });
        }

        var host = new Border { Child = virtualizingPanel };
        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = host
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 200, 16);

        var firstChild = Assert.IsType<Border>(virtualizingPanel.Children[0]);
        viewer.ScrollToVerticalOffset(96f);
        RunLayout(uiRoot, 320, 200, 32);

        Assert.Equal(0, virtualizingPanel.FirstRealizedIndex);
        Assert.Equal(virtualizingPanel.LayoutSlot.Y, firstChild.LayoutSlot.Y, 2);
    }

    [Fact]
    public void Measure_SameRealizedRangeWithNewCrossAxisConstraint_RemeasuresChildren()
    {
        var panel = new VirtualizingStackPanel
        {
            Orientation = Orientation.Vertical,
            IsVirtualizing = true,
            CacheLength = 0f,
            CacheLengthUnit = VirtualizationCacheLengthUnit.Page
        };
        var child = new WidthDependentHeightElement();
        panel.AddChild(child);

        panel.Measure(new Vector2(300f, 120f));
        panel.Arrange(new LayoutRect(0f, 0f, 300f, 120f));
        var wideHeight = child.DesiredSize.Y;

        panel.Measure(new Vector2(120f, 120f));
        panel.Arrange(new LayoutRect(0f, 0f, 120f, 120f));

        Assert.True(child.MeasureOverrideCount >= 2);
        Assert.True(child.DesiredSize.Y > wideHeight);
        Assert.Equal(child.DesiredSize.Y, panel.ExtentHeight, 2);
    }

    [Fact]
    public void GetVisualChildren_WhenVirtualized_OrdersRealizedChildrenByZIndex()
    {
        var panel = new VirtualizingStackPanel
        {
            Orientation = Orientation.Vertical,
            IsVirtualizing = true,
            CacheLength = 0f,
            CacheLengthUnit = VirtualizationCacheLengthUnit.Page
        };
        var lower = new Border { Name = "lower", Height = 40f };
        var upper = new Border { Name = "upper", Height = 40f };
        Panel.SetZIndex(lower, 10);
        Panel.SetZIndex(upper, 0);
        panel.AddChild(lower);
        panel.AddChild(upper);

        panel.Measure(new Vector2(200f, 80f));
        panel.Arrange(new LayoutRect(0f, 0f, 200f, 80f));

        Assert.Equal(new[] { "upper", "lower" }, panel.GetVisualChildren().OfType<Border>().Select(static child => child.Name));
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
    public void ViewerOwnedScroll_WhenRealizedRangeChanges_DoesNotReportRootVisualStructureChange()
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
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = virtualizingPanel
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 200, 16);

        var initialFirst = virtualizingPanel.FirstRealizedIndex;
        var initialStructureChanges = uiRoot.VisualStructureChangeCount;

        viewer.ScrollToVerticalOffset(600f);
        RunLayout(uiRoot, 320, 200, 32);

        Assert.True(viewer.VerticalOffset > 0f);
        Assert.True(virtualizingPanel.FirstRealizedIndex > initialFirst);
        Assert.Equal(initialStructureChanges, uiRoot.VisualStructureChangeCount);
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

    private sealed class WidthDependentHeightElement : FrameworkElement
    {
        public int MeasureOverrideCount { get; private set; }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            MeasureOverrideCount++;
            var width = float.IsPositiveInfinity(availableSize.X) ? 300f : MathF.Max(1f, availableSize.X);
            var height = width >= 200f ? 24f : 72f;
            return new Vector2(width, height);
        }
    }
}
