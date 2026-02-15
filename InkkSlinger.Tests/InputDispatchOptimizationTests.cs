using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public class InputDispatchOptimizationTests
{
    [Fact]
    public void PointerMove_ReusesHoveredTarget_WithoutRepeatedHitTests()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 300f, 200f));
        var button = new Button();
        button.SetLayoutSlot(new LayoutRect(20f, 20f, 120f, 40f));
        root.AddChild(button);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: new Vector2(30f, 30f)));
        var first = uiRoot.GetInputMetricsSnapshot();
        Assert.Equal(1, first.HitTestCount);

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: new Vector2(31f, 31f)));
        var second = uiRoot.GetInputMetricsSnapshot();
        Assert.Equal(0, second.HitTestCount);
    }

    [Fact]
    public void MouseWheel_WithHoveredTarget_AvoidsHitTesting()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 400f, 300f));
        var scrollViewer = new ScrollViewer
        {
            Content = new StackPanel()
        };
        scrollViewer.SetLayoutSlot(new LayoutRect(10f, 10f, 200f, 120f));
        root.AddChild(scrollViewer);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: new Vector2(30f, 30f)));
        var move = uiRoot.GetInputMetricsSnapshot();
        Assert.Equal(1, move.HitTestCount);

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, wheelDelta: 120, position: new Vector2(30f, 30f)));
        var wheel = uiRoot.GetInputMetricsSnapshot();
        Assert.Equal(0, wheel.HitTestCount);
        Assert.True(wheel.PointerEventCount > 0);
    }

    private static InputDelta CreateDelta(bool pointerMoved, Vector2 position, int wheelDelta = 0)
    {
        var previous = new InputSnapshot(default(KeyboardState), default(MouseState), position);
        var current = new InputSnapshot(default(KeyboardState), default(MouseState), position);
        return new InputDelta
        {
            Previous = previous,
            Current = current,
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = pointerMoved,
            WheelDelta = wheelDelta,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }
}
