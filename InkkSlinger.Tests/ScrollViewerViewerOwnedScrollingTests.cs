using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public class ScrollViewerViewerOwnedScrollingTests
{
    [Fact]
    public void MouseWheel_UsesViewerOwnedOffsets_AndClampsToExtent()
    {
        var root = new Panel();
        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        viewer.Content = CreateTallStackPanel(120);
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 200, 16);

        var maxOffset = MathF.Max(0f, viewer.ExtentHeight - viewer.ViewportHeight);
        Assert.True(maxOffset > 0f);

        var handled = viewer.HandleMouseWheelFromInput(-120);
        Assert.True(handled);
        Assert.True(AreClose(24f, viewer.VerticalOffset));

        for (var i = 0; i < 500; i++)
        {
            viewer.HandleMouseWheelFromInput(-120);
        }

        Assert.True(AreClose(maxOffset, viewer.VerticalOffset));
    }

    [Fact]
    public void RegularContent_ComputesMetrics_AndAppliesTranslatedArrange()
    {
        var root = new Panel();
        var content = CreateTallStackPanel(80);
        ScrollViewer.SetUseTransformContentScrolling(content, false);
        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 300, 220, 16);

        Assert.True(viewer.ViewportHeight > 0f);
        Assert.True(viewer.ExtentHeight > viewer.ViewportHeight);

        viewer.ScrollToVerticalOffset(100_000f);
        RunLayout(uiRoot, 300, 220, 32);

        var maxOffset = MathF.Max(0f, viewer.ExtentHeight - viewer.ViewportHeight);
        Assert.True(AreClose(maxOffset, viewer.VerticalOffset));

        var expectedY = viewer.LayoutSlot.Y + MathF.Max(0f, viewer.BorderThickness) - viewer.VerticalOffset;
        Assert.True(AreClose(expectedY, content.LayoutSlot.Y));
    }

    [Fact]
    public void ViewerOwnedScrolling_HitTest_UsesVisibleChildCoordinates()
    {
        var root = new Panel();
        var content = new StackPanel();
        ScrollViewer.SetUseTransformContentScrolling(content, false);

        var first = new Button { Content = "First", Height = 40f, Margin = new Thickness(0f, 0f, 0f, 8f) };
        var second = new Button { Content = "Second", Height = 40f, Margin = new Thickness(0f, 0f, 0f, 8f) };
        var third = new Button { Content = "Third", Height = 40f };
        content.AddChild(first);
        content.AddChild(second);
        content.AddChild(third);

        var viewer = new ScrollViewer
        {
            Width = 180f,
            Height = 90f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 220, 16);

        viewer.ScrollToVerticalOffset(48f);
        RunLayout(uiRoot, 320, 220, 32);

        var probe = new Vector2(
            second.LayoutSlot.X + (second.LayoutSlot.Width * 0.5f),
            second.LayoutSlot.Y + (second.LayoutSlot.Height * 0.5f));

        var hit = VisualTreeHelper.HitTest(root, probe);

        var hitElement = Assert.IsAssignableFrom<FrameworkElement>(hit);
        Assert.True(IsDescendantOrSelf(second, hitElement));
        Assert.False(first.HitTest(probe));
        Assert.False(third.HitTest(probe));
    }

    [Fact]
    public void VirtualizingStackPanel_ChangesRealizedRange_WhenViewerOffsetChanges()
    {
        var root = new Panel();
        var virtualizingPanel = new VirtualizingStackPanel
        {
            Orientation = Orientation.Vertical,
            IsVirtualizing = true,
            CacheLength = 0.5f,
            CacheLengthUnit = VirtualizationCacheLengthUnit.Page
        };

        for (var i = 0; i < 500; i++)
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

        var initialFirst = virtualizingPanel.FirstRealizedIndex;
        var initialLast = virtualizingPanel.LastRealizedIndex;
        Assert.True(initialFirst >= 0);
        Assert.True(initialLast > initialFirst);

        uiRoot.ResetDirtyStateForTests();
        viewer.ScrollToVerticalOffset(600f);
        RunLayout(uiRoot, 320, 200, 32);
        var runtime = viewer.GetScrollViewerSnapshotForDiagnostics();

        Assert.True(viewer.VerticalOffset > 0f);
        Assert.True(virtualizingPanel.FirstRealizedIndex > initialFirst);
        Assert.True(virtualizingPanel.LastRealizedIndex > initialLast);
        Assert.False(viewer.NeedsMeasure);
        Assert.True(runtime.SetOffsetsVirtualizingArrangeOnlyPathCount > 0);
        Assert.Equal(0, runtime.SetOffsetsVirtualizingMeasureInvalidationPathCount);
    }

    [Fact]
    public void VirtualizingStackPanel_WheelScroll_UsesVirtualizingRefreshPath()
    {
        var root = new Panel();
        var virtualizingPanel = new VirtualizingStackPanel
        {
            Orientation = Orientation.Vertical,
            IsVirtualizing = true,
            CacheLength = 0.5f,
            CacheLengthUnit = VirtualizationCacheLengthUnit.Page
        };

        for (var i = 0; i < 500; i++)
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

        var handled = viewer.HandleMouseWheelFromInput(-120);
        var runtime = viewer.GetScrollViewerSnapshotForDiagnostics();

        Assert.True(handled);
        Assert.True(viewer.VerticalOffset > 0f);
        Assert.True(
            runtime.SetOffsetsVirtualizingMeasureInvalidationPathCount > 0 || runtime.SetOffsetsVirtualizingArrangeOnlyPathCount > 0,
            $"Expected viewer-owned virtualized wheel scrolling to stay on a virtualizing SetOffsets path, but runtime was {runtime}.");
        Assert.True(
            runtime.SetOffsetsTransformInvalidationPathCount == 0 && runtime.SetOffsetsManualArrangePathCount == 0,
            $"Expected viewer-owned virtualized wheel scrolling to avoid transform or manual-arrange fallback paths, but runtime was {runtime}.");
    }

    [Fact]
    public void VirtualizingStackPanel_RearrangesChildren_WhenViewerHorizontalOriginChanges()
    {
        var root = new Panel();
        var virtualizingPanel = new VirtualizingStackPanel
        {
            Orientation = Orientation.Vertical,
            IsVirtualizing = true,
            CacheLength = 0.5f,
            CacheLengthUnit = VirtualizationCacheLengthUnit.Page
        };

        for (var i = 0; i < 40; i++)
        {
            virtualizingPanel.AddChild(new Border { Width = 700f, Height = 24f });
        }

        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = virtualizingPanel
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 200, 16);

        var firstChild = Assert.IsAssignableFrom<FrameworkElement>(virtualizingPanel.Children[0]);
        var childXBefore = firstChild.LayoutSlot.X;
        uiRoot.ResetDirtyStateForTests();

        viewer.ScrollToHorizontalOffset(120f);
        RunLayout(uiRoot, 320, 200, 32);

        Assert.True(viewer.HorizontalOffset > 0f);
        Assert.True(firstChild.LayoutSlot.X < childXBefore);
        Assert.False(viewer.NeedsMeasure);
        Assert.Equal(0, uiRoot.MeasureInvalidationCount);
        Assert.True(uiRoot.ArrangeInvalidationCount > 0);
    }

    [Fact]
    public void PlainStackPanel_ExplicitOptOut_KeepsArrangeOffsetBehavior()
    {
        var root = new Panel();
        var content = CreateTallStackPanel(120);
        ScrollViewer.SetUseTransformContentScrolling(content, false);
        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 200, 16);
        var contentYBefore = content.LayoutSlot.Y;

        var handled = viewer.HandleMouseWheelFromInput(-120);

        Assert.True(handled);
        Assert.True(viewer.VerticalOffset > 0f);
        Assert.True(content.LayoutSlot.Y < contentYBefore);
        Assert.False(content.HasLocalRenderTransform());
    }

    [Fact]
    public void PlainStackPanel_DefaultViewerScrolling_UsesTransformPathWithoutRearrangingContent()
    {
        var root = new Panel();
        var content = CreateTallStackPanel(120);
        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 200, 16);
        var contentArrangeBefore = content.GetStackPanelSnapshotForDiagnostics().ArrangeCallCount;
        var viewerBefore = viewer.GetScrollViewerSnapshotForDiagnostics();

        for (var i = 0; i < 12; i++)
        {
            Assert.True(viewer.HandleMouseWheelFromInput(-120));
        }

        var contentArrangeAfter = content.GetStackPanelSnapshotForDiagnostics().ArrangeCallCount;
        var viewerAfter = viewer.GetScrollViewerSnapshotForDiagnostics();
        var transformPathDelta =
            viewerAfter.SetOffsetsTransformInvalidationPathCount -
            viewerBefore.SetOffsetsTransformInvalidationPathCount;
        var manualPathDelta =
            viewerAfter.SetOffsetsManualArrangePathCount -
            viewerBefore.SetOffsetsManualArrangePathCount;
        var arrangeContentDelta =
            viewerAfter.ArrangeContentForCurrentOffsetsCallCount -
            viewerBefore.ArrangeContentForCurrentOffsetsCallCount;

        Assert.True(viewer.VerticalOffset > 0f);
        Assert.True(content.HasLocalRenderTransform());
        Assert.Equal(12, transformPathDelta);
        Assert.Equal(0, manualPathDelta);
        Assert.Equal(0, arrangeContentDelta);
        Assert.Equal(contentArrangeBefore, contentArrangeAfter);
    }

    [Fact]
    public void AutoBars_RemainVisible_ForOversizedCanvasContent()
    {
        var root = new Panel();
        var canvas = new Canvas
        {
            Width = 720f,
            Height = 380f,
            MinWidth = 720f,
            MinHeight = 380f
        };
        canvas.AddChild(new Border { Width = 120f, Height = 80f });

        var viewer = new ScrollViewer
        {
            Width = 558f,
            Height = 190f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = canvas
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 800, 600, 16);

        Assert.True(viewer.ExtentWidth > viewer.ViewportWidth);
        Assert.True(viewer.ExtentHeight > viewer.ViewportHeight);
        Assert.True(viewer.TryGetContentViewportClipRect(out var viewportClip));
        Assert.True(viewportClip.Width < canvas.DesiredSize.X);
        Assert.True(viewportClip.Height < canvas.DesiredSize.Y);
    }

    [Fact]
    public void TransformDefault_RepeatedOffsetChangesBeforeNextFrame_KeepsViewportDirtyHintActive()
    {
        var root = new Panel();
        var content = CreateTransformCapableTallStackPanel(120);
        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 200, 16);
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        var renderInvalidationsBefore = uiRoot.RenderInvalidationCount;

        var firstHandled = viewer.HandleMouseWheelFromInput(-120);
        var renderInvalidationsAfterFirst = uiRoot.RenderInvalidationCount;
        Assert.True(firstHandled);
        Assert.True(renderInvalidationsAfterFirst > renderInvalidationsBefore);
        Assert.NotEmpty(uiRoot.GetDirtyRegionsSnapshotForTests());

        var offsetAfterFirst = viewer.VerticalOffset;
        var secondHandled = viewer.HandleMouseWheelFromInput(-120);

        Assert.True(secondHandled);
        Assert.True(viewer.VerticalOffset > offsetAfterFirst);
        Assert.True(
            viewer.ShouldUseTransformScrollViewportDirtyHint(),
            "Expected repeated transform-scroll updates before the next draw to keep the viewport dirty hint active.");
        Assert.NotEmpty(uiRoot.GetDirtyRegionsSnapshotForTests());
    }

    [Fact]
    public void TransformDefault_RetainedDrawOrderTracksScrolledContentBeforeResize()
    {
        var root = new Panel();
        var content = CreateTransformCapableTallStackPanel(120);
        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 200, 16);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        for (var i = 0; i < 4; i++)
        {
            Assert.True(viewer.HandleMouseWheelFromInput(-120));
        }

        Assert.True(viewer.VerticalOffset >= 96f - 0.01f);
        Assert.True(viewer.TryGetContentViewportClipRect(out var clip));

        uiRoot.SynchronizeRetainedRenderListForTests();

        var firstChild = Assert.IsType<Border>(content.Children[0]);
        var visibleChild = Assert.IsType<Border>(content.Children[6]);
        var order = uiRoot.GetRetainedDrawOrderForClipForTests(clip);

        Assert.DoesNotContain(firstChild, order);
        Assert.Contains(visibleChild, order);
        RetainedRenderingAssert.AssertRetainedDrawOrderMatchesImmediateTraversal(uiRoot, clip);
    }

    [Fact]
    public void TransformDefault_ExplicitScrollViewportDamage_KeepsRetainedAndImmediateTraversalInParity()
    {
        var root = new Panel();
        var content = CreateTransformCapableTallStackPanel(120);
        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 200, 16);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        for (var i = 0; i < 4; i++)
        {
            Assert.True(viewer.HandleMouseWheelFromInput(-120));
        }

        Assert.True(viewer.TryGetContentViewportClipRect(out var clip));
        var dirtyTrace = uiRoot.GetDirtyBoundsEventTraceForTests();
        var dirtyRegions = uiRoot.GetDirtyRegionsSnapshotForTests();

        Assert.Contains(dirtyTrace, entry => entry.Contains(":scroll-viewport:", System.StringComparison.Ordinal));
        Assert.Contains(dirtyRegions, region => Contains(region, clip));
        Assert.True(uiRoot.GetRetainedRenderControllerTelemetrySnapshotForTests().ScrollViewportDirtyCount >= 1);

        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.Equal("ok", uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests());
        RetainedRenderingAssert.AssertRetainedDrawOrderMatchesImmediateTraversal(uiRoot, clip);
    }

    [Fact]
    public void TransformDefault_ScrolledVisibleDescendantInvalidation_DoesNotHideViewportContent()
    {
        var root = new Panel();
        var content = CreateTallStackPanel(120);
        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 200, 16);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        for (var i = 0; i < 4; i++)
        {
            Assert.True(viewer.HandleMouseWheelFromInput(-120));
        }

        Assert.True(viewer.TryGetContentViewportClipRect(out var clip));
        uiRoot.SynchronizeRetainedRenderListForTests();
        uiRoot.ResetDirtyStateForTests();

        var visibleChild = Assert.IsType<Border>(content.Children[6]);
        visibleChild.InvalidateVisual();
        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.Equal("ok", uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests());
        RetainedRenderingAssert.AssertRetainedDrawOrderMatchesImmediateTraversal(uiRoot, clip);
        Assert.Contains(visibleChild, uiRoot.GetRetainedDrawOrderForClipForTests(clip));
    }

    [Fact]
    public void TransformDefault_DirtyBoundsStayClippedToViewportWhenViewerInvalidatesRender()
    {
        var root = new Panel();
        var content = CreateTallStackPanel(120);
        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 200, 16);
        Assert.True(viewer.TryGetContentViewportClipRect(out var viewportClip));

        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        viewer.InvalidateVisual();

        var invalidation = uiRoot.GetRenderInvalidationDebugSnapshotForTests();
        var dirtyRegions = uiRoot.GetDirtyRegionsSnapshotForTests();
        var dirtyTrace = uiRoot.GetDirtyBoundsEventTraceForTests();

        Assert.Equal(nameof(ScrollViewer), invalidation.EffectiveSourceType);
        Assert.True(invalidation.HasDirtyBounds);
        Assert.False(uiRoot.IsFullDirtyForTests());
        Assert.Contains(dirtyTrace, entry =>
            entry.StartsWith(nameof(ScrollViewer), System.StringComparison.Ordinal) &&
            entry.Contains(":scroll-clip-hint:", System.StringComparison.Ordinal));
        Assert.Contains(dirtyRegions, region =>
            region.X <= viewportClip.X + 0.01f &&
            region.Y <= viewportClip.Y + 0.01f &&
            region.X + region.Width >= viewportClip.X + viewportClip.Width - 0.01f &&
            region.Y + region.Height >= viewportClip.Y + viewportClip.Height - 0.01f);
    }

    [Fact]
    public void TransformDefault_ViewerInvalidation_KeepsRetainedTreeConsistent()
    {
        var root = new Panel();
        var content = CreateTallStackPanel(120);
        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 200, 16);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        viewer.InvalidateVisual();
        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.Equal("ok", uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests());
    }

    [Fact]
    public void TransformDefault_OnUnsupportedContent_KeepsArrangeOffsetBehavior()
    {
        var root = new Panel();
        var host = new Border { Child = CreateTallStackPanel(120) };
        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = host
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 200, 16);
        var hostYBefore = host.LayoutSlot.Y;

        var handled = viewer.HandleMouseWheelFromInput(-120);

        Assert.True(handled);
        Assert.True(viewer.VerticalOffset > 0f);
        Assert.True(host.LayoutSlot.Y < hostYBefore);
        Assert.False(host.HasLocalRenderTransform());
    }

    [Fact]
    public void TransformDefault_ExplicitOptOutAtRuntime_RearrangesContentForCurrentOffset()
    {
        var root = new Panel();
        var content = CreateTallStackPanel(120);
        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 200, 16);

        viewer.HandleMouseWheelFromInput(-120);
        Assert.True(viewer.VerticalOffset > 0f);

        ScrollViewer.SetUseTransformContentScrolling(content, false);
        RunLayout(uiRoot, 320, 200, 32);

        var expectedY = viewer.LayoutSlot.Y + MathF.Max(0f, viewer.BorderThickness) - viewer.VerticalOffset;
        Assert.True(AreClose(expectedY, content.LayoutSlot.Y));
        Assert.False(content.HasLocalRenderTransform());
    }

    private static StackPanel CreateTallStackPanel(int itemCount)
    {
        var panel = new StackPanel();
        for (var i = 0; i < itemCount; i++)
        {
            panel.AddChild(new Border { Height = 20f, Margin = new Thickness(0f, 0f, 0f, 2f) });
        }

        return panel;
    }

    private static TransformCapableStackPanel CreateTransformCapableTallStackPanel(int itemCount)
    {
        var panel = new TransformCapableStackPanel();
        for (var i = 0; i < itemCount; i++)
        {
            panel.AddChild(new Border { Height = 20f, Margin = new Thickness(0f, 0f, 0f, 2f) });
        }

        return panel;
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }

    private static bool AreClose(float left, float right)
    {
        return MathF.Abs(left - right) <= 0.05f;
    }

    private static bool Contains(LayoutRect outer, LayoutRect inner)
    {
        return outer.X <= inner.X + 0.01f &&
               outer.Y <= inner.Y + 0.01f &&
               outer.X + outer.Width >= inner.X + inner.Width - 0.01f &&
               outer.Y + outer.Height >= inner.Y + inner.Height - 0.01f;
    }

    private static bool IsDescendantOrSelf(UIElement ancestor, UIElement candidate)
    {
        for (UIElement? current = candidate; current != null; current = current.VisualParent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class TransformCapableStackPanel : StackPanel, IScrollTransformContent
    {
    }
}
