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
}
