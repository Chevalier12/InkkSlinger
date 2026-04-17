using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ScrollViewerContentArrangeReuseTests
{
    [Fact]
    public void HorizontalDisabled_ReusableContent_PreservesPreviousArrangeWidthWhenViewportShrinks()
    {
        var content = new ReusableFixedMeasureElement(new Vector2(300f, 700f));
        var viewer = CreateViewer(content);

        MeasureAndArrange(viewer, 320f, 180f);

        var initialArrangeWidth = content.LastArrangeRect.Width;
        var initialArrangeCount = content.ArrangeOverrideCount;

        MeasureAndArrange(viewer, 280f, 180f);

        Assert.True(initialArrangeWidth > viewer.ViewportWidth + 0.01f,
            $"Expected the preserved width to exceed the shrunken viewport. arranged={initialArrangeWidth}, viewport={viewer.ViewportWidth}");
        Assert.True(MathF.Abs(content.LastArrangeRect.Width - initialArrangeWidth) <= 0.01f,
            $"Expected the reusable content to keep its previous arranged width. initial={initialArrangeWidth}, actual={content.LastArrangeRect.Width}");
        Assert.Equal(initialArrangeCount, content.ArrangeOverrideCount);
    }

    [Fact]
    public void HorizontalDisabled_NonReusableContent_RearrangesWhenViewportShrinks()
    {
        var content = new FixedMeasureElement(new Vector2(300f, 700f));
        var viewer = CreateViewer(content);

        MeasureAndArrange(viewer, 320f, 180f);

        var initialArrangeWidth = content.LastArrangeRect.Width;
        var initialArrangeCount = content.ArrangeOverrideCount;

        MeasureAndArrange(viewer, 280f, 180f);

        Assert.True(content.LastArrangeRect.Width < initialArrangeWidth - 0.01f,
            $"Expected non-reusable content to receive a narrower arrange width. initial={initialArrangeWidth}, actual={content.LastArrangeRect.Width}");
        Assert.True(content.ArrangeOverrideCount > initialArrangeCount);
    }

    [Fact]
    public void HorizontalDisabled_ReusableContent_ExpandsAgainWhenViewportOutgrowsPreservedWidth()
    {
        var content = new ReusableFixedMeasureElement(new Vector2(300f, 700f));
        var viewer = CreateViewer(content);

        MeasureAndArrange(viewer, 320f, 180f);
        MeasureAndArrange(viewer, 280f, 180f);

        var preservedWidth = content.LastArrangeRect.Width;
        var preservedArrangeCount = content.ArrangeOverrideCount;

        MeasureAndArrange(viewer, 360f, 180f);

        Assert.True(viewer.ViewportWidth > preservedWidth + 0.01f,
            $"Expected the grown viewport to exceed the preserved width. viewport={viewer.ViewportWidth}, preserved={preservedWidth}");
        Assert.True(content.LastArrangeRect.Width > preservedWidth + 0.01f,
            $"Expected the content to expand once the viewport exceeds the preserved width. actual={content.LastArrangeRect.Width}, preserved={preservedWidth}");
        Assert.True(content.ArrangeOverrideCount > preservedArrangeCount);
    }

    [Fact]
    public void ViewerOwnedScrolling_ContentPanelRearrangesWhenOnlyOriginChanges()
    {
        var content = new CountingPanel();
        content.AddChild(new FixedMeasureElement(new Vector2(80f, 40f)));
        content.AddChild(new FixedMeasureElement(new Vector2(120f, 50f)));
        var viewer = CreateViewer(content);

        MeasureAndArrange(viewer, 320f, 180f, 0f, 0f);

        var contentArrangeCount = content.ArrangeOverrideCount;
        var firstChild = (FixedMeasureElement)content.Children[0];
        var secondChild = (FixedMeasureElement)content.Children[1];
        var firstChildArrangeCount = firstChild.ArrangeOverrideCount;
        var secondChildArrangeCount = secondChild.ArrangeOverrideCount;
        var initialContentRect = content.LayoutSlot;
        var initialFirstChildRect = firstChild.LayoutSlot;
        var initialSecondChildRect = secondChild.LayoutSlot;

        MeasureAndArrange(viewer, 320f, 180f, 48f, 0f);

        Assert.True(content.ArrangeOverrideCount > contentArrangeCount);
        Assert.True(firstChild.ArrangeOverrideCount > firstChildArrangeCount);
        Assert.True(secondChild.ArrangeOverrideCount > secondChildArrangeCount);
        Assert.True(MathF.Abs(content.LayoutSlot.X - (initialContentRect.X + 48f)) <= 0.01f);
        Assert.True(MathF.Abs(firstChild.LayoutSlot.X - (initialFirstChildRect.X + 48f)) <= 0.01f);
        Assert.True(MathF.Abs(secondChild.LayoutSlot.X - (initialSecondChildRect.X + 48f)) <= 0.01f);
    }

    private static ScrollViewer CreateViewer(FrameworkElement content)
    {
        return new ScrollViewer
        {
            BorderThickness = 1f,
            ScrollBarThickness = 12f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
    }

    private static void MeasureAndArrange(ScrollViewer viewer, float width, float height, float x = 0f, float y = 0f)
    {
        viewer.Measure(new Vector2(width, height));
        viewer.SetLayoutSlot(new LayoutRect(x, y, width, height));
        viewer.Arrange(new LayoutRect(x, y, width, height));
    }

    private class FixedMeasureElement(Vector2 desiredSize) : FrameworkElement
    {
        public int ArrangeOverrideCount { get; private set; }

        public LayoutRect LastArrangeRect { get; private set; }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            _ = availableSize;
            return desiredSize;
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            ArrangeOverrideCount++;
            LastArrangeRect = LayoutSlot;
            return finalSize;
        }
    }

    private sealed class ReusableFixedMeasureElement(Vector2 desiredSize) : FixedMeasureElement(desiredSize)
    {
        protected override bool CanReuseMeasureForAvailableSizeChange(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
        {
            _ = previousAvailableSize;
            _ = nextAvailableSize;
            return true;
        }
    }

    private sealed class CountingPanel : Panel
    {
        public int ArrangeOverrideCount { get; private set; }

        public LayoutRect LastArrangeRect { get; private set; }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            var desired = Vector2.Zero;
            foreach (var child in Children)
            {
                if (child is not FrameworkElement frameworkChild)
                {
                    continue;
                }

                frameworkChild.Measure(availableSize);
                desired.X = MathF.Max(desired.X, frameworkChild.DesiredSize.X);
                desired.Y += frameworkChild.DesiredSize.Y;
            }

            return desired;
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            ArrangeOverrideCount++;
            LastArrangeRect = LayoutSlot;

            var currentY = LayoutSlot.Y;
            foreach (var child in Children)
            {
                if (child is not FrameworkElement frameworkChild)
                {
                    continue;
                }

                var height = frameworkChild.DesiredSize.Y;
                frameworkChild.Arrange(new LayoutRect(LayoutSlot.X, currentY, finalSize.X, height));
                currentY += height;
            }

            return finalSize;
        }
    }
}