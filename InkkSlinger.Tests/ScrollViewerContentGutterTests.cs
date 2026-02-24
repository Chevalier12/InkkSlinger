using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ScrollViewerContentGutterTests
{
    [Fact]
    public void VerticalScrollbar_ShouldReserveContentGutter()
    {
        var host = new StackPanel();
        for (var i = 0; i < 120; i++)
        {
            host.AddChild(new Button
            {
                Text = $"Button {i}",
                Margin = new Thickness(0f, 0f, 0f, 4f)
            });
        }

        var viewer = new ScrollViewer
        {
            Height = 640f,
            Content = host
        };

        var root = new Border
        {
            Padding = new Thickness(8f),
            Child = viewer
        };

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 280, 820, 16);

        var verticalBar = viewer.GetVisualChildren()
            .OfType<ScrollBar>()
            .FirstOrDefault(static b => b.Orientation == Orientation.Vertical && b.IsVisible);

        Assert.NotNull(verticalBar);

        var maxRight = host.Children
            .OfType<FrameworkElement>()
            .Where(static element => element.LayoutSlot.Height > 0f)
            .Max(static element => element.LayoutSlot.X + element.LayoutSlot.Width);

        Assert.True(
            maxRight <= verticalBar!.LayoutSlot.X - 1f,
            $"Expected content gutter before vertical bar. maxRight={maxRight:0.###}, barLeft={verticalBar.LayoutSlot.X:0.###}, viewport={viewer.ViewportWidth:0.###}");
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }
}
