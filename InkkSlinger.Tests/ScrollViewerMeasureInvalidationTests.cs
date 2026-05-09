using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ScrollViewerMeasureInvalidationTests
{
    [Fact]
    public void DescendantMeasureChange_ThatKeepsViewerDesiredSizeStable_DoesNotRebubbleParentMeasure()
    {
        var content = new DynamicDesiredSizeElement(260f, 600f);
        var viewer = new CountingScrollViewer
        {
            Width = 320f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };

        var host = new CountingBorder
        {
            Child = viewer
        };

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 640, 480, 16);

        var hostMeasureBefore = host.MeasureOverrideCount;
        var viewerMeasureBefore = viewer.MeasureOverrideCount;

        content.SetDesiredHeight(650f);

        Assert.False(host.NeedsMeasure);
        Assert.False(viewer.NeedsMeasure);

        RunLayout(uiRoot, 640, 480, 32);

        Assert.Equal(hostMeasureBefore, host.MeasureOverrideCount);
        Assert.Equal(viewerMeasureBefore, viewer.MeasureOverrideCount);
    }

    [Fact]
    public void DescendantMeasureChange_WithStableViewportAndTransformScrolling_RearrangesContentWithoutParentMeasure()
    {
        var content = new DynamicDesiredSizeElement(260f, 600f);
        var viewer = new CountingScrollViewer
        {
            Width = 320f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };

        var host = new CountingBorder
        {
            Child = viewer
        };

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 640, 480, 16);

        var viewerArrangeBefore = viewer.ArrangeOverrideCount;
        var viewerMeasureBefore = viewer.MeasureOverrideCount;
        var hostMeasureBefore = host.MeasureOverrideCount;
        var contentArrangeBefore = content.ArrangeOverrideCount;

        content.SetDesiredHeight(650f);

        Assert.False(host.NeedsMeasure);
        Assert.False(viewer.NeedsMeasure);

        RunLayout(uiRoot, 640, 480, 32);

        Assert.Equal(hostMeasureBefore, host.MeasureOverrideCount);
        Assert.Equal(viewerMeasureBefore, viewer.MeasureOverrideCount);
        Assert.True(viewer.ArrangeOverrideCount > viewerArrangeBefore);
        Assert.True(content.ArrangeOverrideCount > contentArrangeBefore);
    }

    [Fact]
    public void DescendantMeasureChange_ThatChangesViewerDesiredSize_StillRebubblesParentMeasure()
    {
        var content = new DynamicDesiredSizeElement(260f, 600f);
        var viewer = new CountingScrollViewer
        {
            Width = 320f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };

        var host = new CountingBorder
        {
            Child = viewer
        };

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 640, 480, 16);

        var hostMeasureBefore = host.MeasureOverrideCount;

        content.SetDesiredHeight(150f);

        Assert.True(host.NeedsMeasure);
        Assert.True(viewer.NeedsMeasure);

        RunLayout(uiRoot, 640, 480, 32);

        Assert.True(host.MeasureOverrideCount > hostMeasureBefore);
    }

    [Fact]
    public void AutoBothAxes_UnchangedInfiniteContentConstraint_ReusesCurrentExtentWithoutContentReuseProof()
    {
        var content = new ReuseProofCountingElement(720f, 640f);
        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };

        MeasureAndArrange(viewer, 320f, 220f);
        content.ResetCounters();

        viewer.InvalidateMeasure();
        viewer.Measure(new Vector2(280f, 180f));

        Assert.Equal(0, content.CanReuseMeasureForAvailableSizeChangeCount);
        Assert.Equal(0, content.MeasureOverrideCount);
        Assert.Equal(720f, viewer.ExtentWidth, 0.01f);
        Assert.Equal(640f, viewer.ExtentHeight, 0.01f);

        viewer.SetLayoutSlot(new LayoutRect(0f, 0f, 280f, 180f));
        viewer.Arrange(new LayoutRect(0f, 0f, 280f, 180f));
    }

    [Fact]
    public void LogicalScrollInfoChange_WhenAutoBarsCannotChange_DoesNotRebubbleParentMeasure()
    {
        var content = new LogicalScrollInfoElement(300f, 900f, 300f, 220f);
        var viewer = new CountingScrollViewer
        {
            Width = 320f,
            Height = 240f,
            CanContentScroll = true,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            Content = content
        };

        var host = new CountingBorder { Child = viewer };
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 640, 480, 16);

        var hostMeasureBefore = host.MeasureOverrideCount;
        var viewerMeasureBefore = viewer.MeasureOverrideCount;

        content.SetExtentHeight(930f);

        Assert.Equal(930f, viewer.ExtentHeight, 0.01f);
        Assert.False(host.NeedsMeasure);
        Assert.False(viewer.NeedsMeasure);
        Assert.Equal(hostMeasureBefore, host.MeasureOverrideCount);
        Assert.Equal(viewerMeasureBefore, viewer.MeasureOverrideCount);
    }

    [Fact]
    public void LogicalScrollInfoChange_WhenAutoBarThresholdChanges_InvalidatesViewerMeasure()
    {
        var content = new LogicalScrollInfoElement(300f, 120f, 300f, 220f);
        var viewer = new CountingScrollViewer
        {
            Width = 320f,
            Height = 240f,
            CanContentScroll = true,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };

        var host = new CountingBorder { Child = viewer };
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 640, 480, 16);

        Assert.Equal(Visibility.Collapsed, viewer.ComputedVerticalScrollBarVisibility);

        content.SetExtentHeight(900f);

        Assert.True(viewer.NeedsMeasure);
        Assert.True(host.NeedsMeasure);
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }

    private static void MeasureAndArrange(ScrollViewer viewer, float width, float height)
    {
        viewer.Measure(new Vector2(width, height));
        viewer.SetLayoutSlot(new LayoutRect(0f, 0f, width, height));
        viewer.Arrange(new LayoutRect(0f, 0f, width, height));
    }

    private sealed class CountingScrollViewer : ScrollViewer
    {
        public int MeasureOverrideCount { get; private set; }
        public int ArrangeOverrideCount { get; private set; }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            MeasureOverrideCount++;
            return base.MeasureOverride(availableSize);
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            ArrangeOverrideCount++;
            return base.ArrangeOverride(finalSize);
        }
    }

    private sealed class CountingBorder : Border
    {
        public int MeasureOverrideCount { get; private set; }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            MeasureOverrideCount++;
            return base.MeasureOverride(availableSize);
        }
    }

    private sealed class DynamicDesiredSizeElement : FrameworkElement
    {
        private float _desiredWidth;
        private float _desiredHeight;

        public int ArrangeOverrideCount { get; private set; }

        public DynamicDesiredSizeElement(float desiredWidth, float desiredHeight)
        {
            _desiredWidth = desiredWidth;
            _desiredHeight = desiredHeight;
        }

        public void SetDesiredHeight(float desiredHeight)
        {
            if (MathF.Abs(_desiredHeight - desiredHeight) <= 0.01f)
            {
                return;
            }

            _desiredHeight = desiredHeight;
            InvalidateMeasure();
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            var width = float.IsFinite(availableSize.X)
                ? MathF.Min(_desiredWidth, MathF.Max(0f, availableSize.X))
                : _desiredWidth;
            return new Vector2(width, _desiredHeight);
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            ArrangeOverrideCount++;
            return finalSize;
        }
    }

    private sealed class ReuseProofCountingElement : FrameworkElement
    {
        private readonly Vector2 _desiredSize;

        public ReuseProofCountingElement(float desiredWidth, float desiredHeight)
        {
            _desiredSize = new Vector2(desiredWidth, desiredHeight);
        }

        public int MeasureOverrideCount { get; private set; }

        public int CanReuseMeasureForAvailableSizeChangeCount { get; private set; }

        public void ResetCounters()
        {
            MeasureOverrideCount = 0;
            CanReuseMeasureForAvailableSizeChangeCount = 0;
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            _ = availableSize;
            MeasureOverrideCount++;
            return _desiredSize;
        }

        protected override bool CanReuseMeasureForAvailableSizeChange(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
        {
            _ = previousAvailableSize;
            _ = nextAvailableSize;
            CanReuseMeasureForAvailableSizeChangeCount++;
            return false;
        }
    }

    private sealed class LogicalScrollInfoElement : FrameworkElement, IScrollInfo
    {
        public LogicalScrollInfoElement(float extentWidth, float extentHeight, float viewportWidth, float viewportHeight)
        {
            ExtentWidth = extentWidth;
            ExtentHeight = extentHeight;
            ViewportWidth = viewportWidth;
            ViewportHeight = viewportHeight;
        }

        public ScrollViewer? ScrollOwner { get; set; }

        public float ExtentWidth { get; private set; }

        public float ExtentHeight { get; private set; }

        public float ViewportWidth { get; private set; }

        public float ViewportHeight { get; private set; }

        public float HorizontalOffset { get; private set; }

        public float VerticalOffset { get; private set; }

        public void SetExtentHeight(float extentHeight)
        {
            ExtentHeight = extentHeight;
            ScrollOwner?.InvalidateScrollInfo();
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            if (float.IsFinite(availableSize.X))
            {
                ViewportWidth = MathF.Max(0f, availableSize.X);
            }

            if (float.IsFinite(availableSize.Y))
            {
                ViewportHeight = MathF.Max(0f, availableSize.Y);
            }

            return new Vector2(
                MathF.Min(ExtentWidth, ViewportWidth),
                MathF.Min(ExtentHeight, ViewportHeight));
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            ViewportWidth = finalSize.X;
            ViewportHeight = finalSize.Y;
            return finalSize;
        }

        public void LineUp() { }
        public void LineDown() { }
        public void LineLeft() { }
        public void LineRight() { }
        public void PageUp() { }
        public void PageDown() { }
        public void PageLeft() { }
        public void PageRight() { }
        public void MouseWheelUp() { }
        public void MouseWheelDown() { }
        public void MouseWheelLeft() { }
        public void MouseWheelRight() { }
        public void SetHorizontalOffset(float offset) => HorizontalOffset = offset;
        public void SetVerticalOffset(float offset) => VerticalOffset = offset;
        public LayoutRect MakeVisible(UIElement visual, LayoutRect rectangle) => rectangle;
    }
}
