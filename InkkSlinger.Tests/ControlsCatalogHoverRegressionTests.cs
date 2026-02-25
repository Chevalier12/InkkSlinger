using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogHoverRegressionTests
{
    [Fact]
    public void HoveringFromViewerGutterIntoButton_ShouldActivateButtonHover()
    {
        var view = new ControlsCatalogView();
        var uiRoot = new UiRoot(view);
        RunLayout(uiRoot, 1280, 820, 16);

        var viewer = FindFirstVisualChild<ScrollViewer>(view);
        Assert.NotNull(viewer);

        var host = Assert.IsType<StackPanel>(view.FindName("ControlButtonsHost"));
        var button = host.Children.OfType<Button>().First();

        var verticalBar = viewer!.GetVisualChildren()
            .OfType<ScrollBar>()
            .FirstOrDefault(static bar => bar.Orientation == Orientation.Vertical && bar.IsVisible);
        Assert.NotNull(verticalBar);

        var gutterPoint = new Vector2(
            verticalBar!.LayoutSlot.X - 0.25f,
            button.LayoutSlot.Y + (button.LayoutSlot.Height * 0.5f));
        MovePointer(uiRoot, gutterPoint);

        Assert.False(button.IsMouseOver);

        var buttonPoint = new Vector2(
            button.LayoutSlot.X + (button.LayoutSlot.Width * 0.5f),
            button.LayoutSlot.Y + (button.LayoutSlot.Height * 0.5f));
        MovePointer(uiRoot, buttonPoint);

        Assert.True(
            button.IsMouseOver,
            $"Expected button hover to recover after moving from viewer gutter. button={button.Text}, gutter=({gutterPoint.X:0.###},{gutterPoint.Y:0.###}), buttonPoint=({buttonPoint.X:0.###},{buttonPoint.Y:0.###})");
    }

    private static void MovePointer(UiRoot uiRoot, Vector2 point)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(point, pointerMoved: true));
    }

    private static InputDelta CreatePointerDelta(
        Vector2 pointer,
        bool pointerMoved = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = pointerMoved,
            WheelDelta = 0,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
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
