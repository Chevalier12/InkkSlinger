using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class BorderViewLayoutTests
{
    [Fact]
    public void Border_RemeasureWithChangedChildDesiredSize_DoesNotInvalidateItselfDuringMeasure()
    {
        var child = new WidthConstrainedMeasureElement(80f, 24f);
        var border = new Border
        {
            Child = child
        };

        border.Measure(new Vector2(120f, 120f));

        Assert.False(border.NeedsMeasure);
        Assert.Equal(new Vector2(80f, 24f), border.DesiredSize);

        border.Measure(new Vector2(50f, 120f));

        Assert.False(border.NeedsMeasure);
        Assert.Equal(new Vector2(50f, 24f), border.DesiredSize);
        Assert.Equal(2, child.MeasureOverrideCount);
    }

    [Fact]
    public void Border_ChildRemeasureDuringArrange_DoesNotInvalidateBorderMeasure()
    {
        var child = new WidthConstrainedMeasureElement(80f, 24f);
        var border = new Border
        {
            Child = child
        };

        border.Measure(new Vector2(120f, 120f));
        border.Arrange(new LayoutRect(0f, 0f, 50f, 120f));

        Assert.False(border.NeedsMeasure);
        Assert.False(border.NeedsArrange);
        Assert.Equal(new Vector2(50f, 24f), child.DesiredSize);
        Assert.Equal(2, child.MeasureOverrideCount);
    }

    [Fact]
    public void NestedGridBorder_ViewportShrink_StabilizesWithoutRepeatedArrangeLoop()
    {
        var leaf = new WidthConstrainedMeasureElement(80f, 24f);
        var innerBorder = new CountingBorder
        {
            Child = leaf
        };

        var innerGrid = new Grid();
        innerGrid.AddChild(innerBorder);

        var outerBorder = new CountingBorder
        {
            Child = innerGrid
        };

        var rootGrid = new Grid();
        rootGrid.AddChild(outerBorder);

        var host = new Panel();
        host.AddChild(rootGrid);

        var uiRoot = new UiRoot(host);

        RunLayout(uiRoot, 120, 120, 16);

        Assert.False(innerBorder.NeedsArrange);
        Assert.False(outerBorder.NeedsArrange);
        Assert.False(rootGrid.NeedsArrange);

        RunLayout(uiRoot, 50, 120, 32);

        Assert.False(innerBorder.NeedsMeasure);
        Assert.False(innerBorder.NeedsArrange);
        Assert.False(outerBorder.NeedsMeasure);
        Assert.False(outerBorder.NeedsArrange);
        Assert.False(rootGrid.NeedsMeasure);
        Assert.False(rootGrid.NeedsArrange);
        Assert.Equal(new Vector2(50f, 24f), leaf.DesiredSize);

        var innerBorderArrangeCount = innerBorder.ArrangeOverrideCount;
        var outerBorderArrangeCount = outerBorder.ArrangeOverrideCount;
        var rootGridArrangeCount = rootGrid.ArrangeCallCount;

        RunLayout(uiRoot, 50, 120, 48);

        Assert.Equal(innerBorderArrangeCount, innerBorder.ArrangeOverrideCount);
        Assert.Equal(outerBorderArrangeCount, outerBorder.ArrangeOverrideCount);
        Assert.Equal(rootGridArrangeCount, rootGrid.ArrangeCallCount);
    }

    private static UIElement? FindDescendant(UIElement root, Predicate<UIElement> match)
    {
        if (match(root))
        {
            return root;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindDescendant(child, match);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs = 16)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }

    private sealed class WidthConstrainedMeasureElement(float desiredWidth, float desiredHeight) : FrameworkElement
    {
        public int MeasureOverrideCount { get; private set; }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            MeasureOverrideCount++;
            var width = float.IsFinite(availableSize.X)
                ? MathF.Min(desiredWidth, MathF.Max(0f, availableSize.X))
                : desiredWidth;

            return new Vector2(width, desiredHeight);
        }
    }

    private sealed class CountingBorder : Border
    {
        public int ArrangeOverrideCount { get; private set; }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            ArrangeOverrideCount++;
            return base.ArrangeOverride(finalSize);
        }
    }
}
