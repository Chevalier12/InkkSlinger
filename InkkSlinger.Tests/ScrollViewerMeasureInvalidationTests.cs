using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ScrollViewerMeasureInvalidationTests
{
    [Fact]
    public void DescendantMeasureChange_ThatKeepsViewerDesiredSizeStable_CurrentlyRebubblesParentMeasure()
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

        Assert.True(host.NeedsMeasure);
        Assert.True(viewer.NeedsMeasure);

        RunLayout(uiRoot, 640, 480, 32);

        Assert.True(host.MeasureOverrideCount > hostMeasureBefore);
        Assert.True(viewer.MeasureOverrideCount > viewerMeasureBefore);
    }

    [Fact]
    public void DescendantMeasureChange_WithStableViewportAndTransformScrolling_RerunsViewerMeasureAndArrangeOverrides()
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
        var contentArrangeBefore = content.ArrangeOverrideCount;

        content.SetDesiredHeight(650f);

        Assert.True(host.NeedsMeasure);
        Assert.True(viewer.NeedsMeasure);

        RunLayout(uiRoot, 640, 480, 32);

        Assert.True(viewer.MeasureOverrideCount > viewerMeasureBefore);
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

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
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
}