using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogLayoutTests
{
    [Fact]
    public void ControlsCatalog_LeftButtons_ShouldNotTouchVerticalScrollbarBand()
    {
        var view = new ControlsCatalogView();
        var uiRoot = new UiRoot(view);
        RunLayout(uiRoot, 1280, 820, 16);

        var buttonsHost = Assert.IsType<StackPanel>(view.FindName("ControlButtonsHost"));
        var viewer = FindFirstVisualChild<ScrollViewer>(view);
        Assert.NotNull(viewer);

        var verticalBar = viewer!.GetVisualChildren()
            .OfType<ScrollBar>()
            .FirstOrDefault(static bar => bar.Orientation == Orientation.Vertical && bar.IsVisible);
        Assert.NotNull(verticalBar);

        var maxRight = buttonsHost.Children
            .OfType<FrameworkElement>()
            .Where(static element => element.LayoutSlot.Height > 0f)
            .Max(static element => element.LayoutSlot.X + element.LayoutSlot.Width);

        Assert.True(
            maxRight <= verticalBar!.LayoutSlot.X - 1f,
            $"Catalog buttons still touch scrollbar band. maxRight={maxRight:0.###}, barLeft={verticalBar.LayoutSlot.X:0.###}");
    }

    [Fact]
    public void ControlsCatalog_LeftButtons_RightBorderPixels_ShouldNotEnterVerticalScrollbarPixels()
    {
        var viewportSizes = new (int Width, int Height)[]
        {
            (720, 620),
            (800, 620),
            (960, 720),
            (1024, 768),
            (1111, 777),
            (1280, 820),
            (1365, 768),
            (1600, 900),
            (1919, 1079),
            (1920, 1080)
        };

        foreach (var (width, height) in viewportSizes)
        {
            var view = new ControlsCatalogView();
            var uiRoot = new UiRoot(view);
            RunLayout(uiRoot, width, height, 16);

            var buttonsHost = Assert.IsType<StackPanel>(view.FindName("ControlButtonsHost"));
            var viewer = FindFirstVisualChild<ScrollViewer>(view);
            Assert.NotNull(viewer);

            var verticalBar = viewer!.GetVisualChildren()
                .OfType<ScrollBar>()
                .FirstOrDefault(static bar => bar.Orientation == Orientation.Vertical && bar.IsVisible);
            Assert.NotNull(verticalBar);

            var barPixelLeft = PixelRound(verticalBar!.LayoutSlot.X);
            foreach (var button in buttonsHost.Children.OfType<Button>().Where(static b => b.LayoutSlot.Height > 0f))
            {
                var rightBorderRectX = button.LayoutSlot.X + button.LayoutSlot.Width - button.BorderThickness;
                var rightBorderPixelX = PixelRound(rightBorderRectX);
                var rightBorderPixelWidth = PixelRound(button.BorderThickness);
                var rightBorderPixelRight = rightBorderPixelX + rightBorderPixelWidth;

                Assert.True(
                    rightBorderPixelRight <= barPixelLeft,
                    $"Button right border overlaps scrollbar pixels. viewport={width}x{height}, button={button.GetContentText()}, borderRight={rightBorderPixelRight}, barLeft={barPixelLeft}, slotX={button.LayoutSlot.X:0.###}, slotW={button.LayoutSlot.Width:0.###}");
            }
        }
    }

    private static TElement? FindFirstVisualChild<TElement>(UIElement root)
        where TElement : UIElement
    {
        if (root is TElement match)
        {
            return match;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindFirstVisualChild<TElement>(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }

    private static int PixelRound(float value)
    {
        return (int)MathF.Round(value);
    }
}
