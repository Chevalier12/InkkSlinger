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
            LineScrollAmount = 30f,
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
        Assert.True(AreClose(30f, viewer.VerticalOffset));

        for (var i = 0; i < 500; i++)
        {
            viewer.HandleMouseWheelFromInput(-120);
        }

        Assert.True(AreClose(maxOffset, viewer.VerticalOffset));
    }

    [Fact]
    public void MouseWheel_UpdatesContentWithoutRootLayoutInvalidation()
    {
        var root = new Panel();
        var content = CreateTallStackPanel(120);
        ScrollViewer.SetUseTransformContentScrolling(content, false);
        var viewer = new ScrollViewer
        {
            LineScrollAmount = 30f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 200, 16);
        var contentYBefore = content.LayoutSlot.Y;
        var arrangeInvalidationsBefore = uiRoot.ArrangeInvalidationCount;
        var measureInvalidationsBefore = uiRoot.MeasureInvalidationCount;

        var handled = viewer.HandleMouseWheelFromInput(-120);

        Assert.True(handled);
        Assert.True(viewer.VerticalOffset > 0f);
        Assert.True(content.LayoutSlot.Y < contentYBefore);
        Assert.Equal(arrangeInvalidationsBefore, uiRoot.ArrangeInvalidationCount);
        Assert.Equal(measureInvalidationsBefore, uiRoot.MeasureInvalidationCount);
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

        viewer.ScrollToVerticalOffset(600f);
        RunLayout(uiRoot, 320, 200, 32);

        Assert.True(viewer.VerticalOffset > 0f);
        Assert.True(virtualizingPanel.FirstRealizedIndex > initialFirst);
        Assert.True(virtualizingPanel.LastRealizedIndex > initialLast);
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

        viewer.ScrollToHorizontalOffset(120f);
        RunLayout(uiRoot, 320, 200, 32);

        Assert.True(viewer.HorizontalOffset > 0f);
        Assert.True(firstChild.LayoutSlot.X < childXBefore);
    }

    [Fact]
    public void PlainStackPanel_Default_UsesTransformScrollingPath()
    {
        var root = new Panel();
        var content = CreateTallStackPanel(120);
        var viewer = new ScrollViewer
        {
            LineScrollAmount = 30f,
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
        Assert.True(AreClose(contentYBefore, content.LayoutSlot.Y));
        Assert.True(content.HasLocalRenderTransform());
    }

    [Fact]
    public void PlainStackPanel_ExplicitOptOut_KeepsArrangeOffsetBehavior()
    {
        var root = new Panel();
        var content = CreateTallStackPanel(120);
        ScrollViewer.SetUseTransformContentScrolling(content, false);
        var viewer = new ScrollViewer
        {
            LineScrollAmount = 30f,
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
    public void TransformDefault_SupportsHorizontalScrolling()
    {
        var root = new Panel();
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        for (var i = 0; i < 12; i++)
        {
            content.AddChild(new Border { Width = 120f, Height = 40f, Margin = new Thickness(0f, 0f, 4f, 0f) });
        }

        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = content
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 200, 16);
        Assert.True(viewer.ExtentWidth > viewer.ViewportWidth);

        var contentXBefore = content.LayoutSlot.X;
        viewer.ScrollToHorizontalOffset(200f);

        Assert.True(viewer.HorizontalOffset > 0f);
        Assert.True(AreClose(contentXBefore, content.LayoutSlot.X));
        Assert.True(content.HasLocalRenderTransform());
    }

    [Fact]
    public void TransformDefault_DoesNotTriggerRootLayoutInvalidation_OnOffsetOnlyChanges()
    {
        var root = new Panel();
        var content = CreateTallStackPanel(120);
        var viewer = new ScrollViewer
        {
            LineScrollAmount = 30f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 200, 16);
        var contentYBefore = content.LayoutSlot.Y;
        var arrangeInvalidationsBefore = uiRoot.ArrangeInvalidationCount;
        var measureInvalidationsBefore = uiRoot.MeasureInvalidationCount;

        var handled = viewer.HandleMouseWheelFromInput(-120);

        Assert.True(handled);
        Assert.True(viewer.VerticalOffset > 0f);
        Assert.True(AreClose(contentYBefore, content.LayoutSlot.Y));
        Assert.Equal(arrangeInvalidationsBefore, uiRoot.ArrangeInvalidationCount);
        Assert.Equal(measureInvalidationsBefore, uiRoot.MeasureInvalidationCount);
    }

    [Fact]
    public void TransformDefault_ClampsOffsetsToExtent()
    {
        var root = new Panel();
        var content = CreateTallStackPanel(80);
        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 300, 220, 16);

        viewer.ScrollToVerticalOffset(100_000f);
        RunLayout(uiRoot, 300, 220, 32);

        var maxOffset = MathF.Max(0f, viewer.ExtentHeight - viewer.ViewportHeight);
        Assert.True(AreClose(maxOffset, viewer.VerticalOffset));
        Assert.True(content.HasLocalRenderTransform());
    }

    [Fact]
    public void TransformDefault_OnUnsupportedContent_KeepsArrangeOffsetBehavior()
    {
        var root = new Panel();
        var host = new Border { Child = CreateTallStackPanel(120) };
        var viewer = new ScrollViewer
        {
            LineScrollAmount = 30f,
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
            LineScrollAmount = 30f,
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

    [Fact]
    public void TransformDefault_AppliesOnlyToDirectContentHostPanel()
    {
        var root = new Panel();
        var outer = new StackPanel();
        var inner = new StackPanel();
        for (var i = 0; i < 120; i++)
        {
            inner.AddChild(new Border { Height = 20f, Margin = new Thickness(0f, 0f, 0f, 2f) });
        }

        outer.AddChild(inner);
        var viewer = new ScrollViewer
        {
            LineScrollAmount = 30f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = outer
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 200, 16);
        viewer.HandleMouseWheelFromInput(-120);

        Assert.True(outer.HasLocalRenderTransform());
        Assert.False(inner.HasLocalRenderTransform());
    }

    [Fact]
    public void TransformDefault_DescendantOptOut_DoesNotDisableHostTransform()
    {
        var root = new Panel();
        var outer = new StackPanel();
        var inner = new StackPanel();
        ScrollViewer.SetUseTransformContentScrolling(inner, false);

        for (var i = 0; i < 120; i++)
        {
            inner.AddChild(new Border { Height = 20f, Margin = new Thickness(0f, 0f, 0f, 2f) });
        }

        outer.AddChild(inner);
        var viewer = new ScrollViewer
        {
            LineScrollAmount = 30f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = outer
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 200, 16);
        viewer.HandleMouseWheelFromInput(-120);

        Assert.True(outer.HasLocalRenderTransform());
        Assert.False(inner.HasLocalRenderTransform());
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
}
