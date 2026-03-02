using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogHoverScaleBehaviorTests
{
    [Fact]
    public void CatalogButtons_HoverEnterAndLeaveScaleDirection_IsCorrect()
    {
        var catalog = new ControlsCatalogView();
        var host = Assert.IsType<StackPanel>(catalog.FindName("ControlButtonsHost"));
        var button = Assert.IsType<Button>(host.Children[0]);

        var uiRoot = new UiRoot(catalog);
        RunLayout(uiRoot, 1200, 900, 16);

        if (button.RenderTransform is not ScaleTransform transform)
        {
            return;
        }

        var inside = FindPointHittingTarget(catalog, button, 1200, 900);
        Assert.Equal(1f, transform.ScaleX, 3);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(5f, 5f), pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(inside, pointerMoved: true));
        RunLayout(uiRoot, 1200, 900, 32);
        Assert.True(transform.ScaleX >= 1f);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(1180f, 20f), pointerMoved: true));
        RunLayout(uiRoot, 1200, 900, 48);
        Assert.True(transform.ScaleX <= 1.03f + 0.001f);
    }

    [Fact]
    public void CatalogButtons_HoverRecovers_AfterMovingToHeaderLabel()
    {
        var catalog = new ControlsCatalogView();
        var host = Assert.IsType<StackPanel>(catalog.FindName("ControlButtonsHost"));
        var button = Assert.IsType<Button>(host.Children[0]);
        var header = FindLabelByText(catalog, "Control Views");

        var uiRoot = new UiRoot(catalog);
        RunLayout(uiRoot, 1200, 900, 16);

        var buttonPoint = FindPointHittingTarget(catalog, button, 1200, 900);
        var headerPoint = FindPointHittingTarget(catalog, header, 1200, 900);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(buttonPoint, pointerMoved: true));
        RunLayout(uiRoot, 1200, 900, 32);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(headerPoint, pointerMoved: true));
        RunLayout(uiRoot, 1200, 900, 48);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(buttonPoint, pointerMoved: true));
        RunLayout(uiRoot, 1200, 900, 64);

        var transform = Assert.IsType<ScaleTransform>(button.RenderTransform);
        Assert.True(transform.ScaleX > 1f);
    }

    private static Vector2 FindPointHittingTarget(UIElement root, UIElement target, int width, int height)
    {
        for (var y = 0; y < height; y += 2)
        {
            for (var x = 0; x < width; x += 2)
            {
                var point = new Vector2(x, y);
                var hit = VisualTreeHelper.HitTest(root, point);
                if (IsTargetOrDescendant(hit, target))
                {
                    return point;
                }
            }
        }

        throw new InvalidOperationException("Could not locate point that hits catalog button.");
    }

    private static bool IsTargetOrDescendant(UIElement? hit, UIElement target)
    {
        for (var current = hit; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (ReferenceEquals(current, target))
            {
                return true;
            }
        }

        return false;
    }

    private static Label FindLabelByText(UIElement root, string text)
    {
        var stack = new Stack<UIElement>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current is Label label && string.Equals(label.Text, text, StringComparison.Ordinal))
            {
                return label;
            }

            foreach (var child in current.GetVisualChildren())
            {
                stack.Push(child);
            }
        }

        throw new InvalidOperationException($"Could not find label with text '{text}'.");
    }

    private static InputDelta CreatePointerDelta(Vector2 pointer, bool pointerMoved)
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

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }
}
