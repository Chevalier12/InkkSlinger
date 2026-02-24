using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ExpanderInputTests
{
    [Fact]
    public void ClickingHeader_ShouldToggleIsExpanded()
    {
        var host = new Canvas
        {
            Width = 460f,
            Height = 260f
        };

        var expander = new Expander
        {
            Width = 360f,
            Height = 200f,
            Header = "Expander Header",
            Content = new Label { Text = "Expander Content" },
            IsExpanded = true
        };
        host.AddChild(expander);
        Canvas.SetLeft(expander, 30f);
        Canvas.SetTop(expander, 24f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);
        var expandedHeight = expander.ActualHeight;
        Assert.True(expandedHeight > 40f);

        var headerPoint = new Vector2(expander.LayoutSlot.X + 8f, expander.LayoutSlot.Y + 8f);
        Click(uiRoot, headerPoint);
        RunLayout(uiRoot);
        Assert.False(expander.IsExpanded);
        Assert.True(expander.ActualHeight < expandedHeight);

        Click(uiRoot, headerPoint);
        RunLayout(uiRoot);
        Assert.True(expander.IsExpanded);
        Assert.True(expander.ActualHeight >= expandedHeight - 0.1f);
    }

    [Fact]
    public void CollapsedExpander_ShouldExcludeContentFromRetainedVisualOrder()
    {
        var host = new Canvas
        {
            Width = 460f,
            Height = 260f
        };

        var content = new Label { Text = "Expander Content" };
        var expander = new Expander
        {
            Width = 360f,
            Height = 200f,
            Header = "Expander Header",
            Content = content,
            IsExpanded = true
        };
        host.AddChild(expander);
        Canvas.SetLeft(expander, 30f);
        Canvas.SetTop(expander, 24f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);
        Assert.Contains(content, uiRoot.GetRetainedVisualOrderForTests());

        var headerPoint = new Vector2(expander.LayoutSlot.X + 8f, expander.LayoutSlot.Y + 8f);
        Click(uiRoot, headerPoint);
        RunLayout(uiRoot);

        Assert.False(expander.IsExpanded);
        Assert.DoesNotContain(content, uiRoot.GetRetainedVisualOrderForTests());
    }

    private static void Click(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));
    }

    private static InputDelta CreatePointerDelta(
        Vector2 pointer,
        bool pointerMoved = false,
        bool leftPressed = false,
        bool leftReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = pointerMoved || leftPressed || leftReleased,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void RunLayout(UiRoot uiRoot)
    {
        uiRoot.Update(
            new GameTime(System.TimeSpan.FromMilliseconds(16), System.TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 460, 260));
    }
}
