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

    [Fact]
    public void PointerClick_UsesPreciseHitTest_AfterHoverReuse()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 400f, 200f));
        var left = new Button();
        left.SetLayoutSlot(new LayoutRect(20f, 20f, 120f, 40f));
        var right = new Button();
        right.SetLayoutSlot(new LayoutRect(220f, 20f, 120f, 40f));
        root.AddChild(left);
        root.AddChild(right);

        var leftClicks = 0;
        var rightClicks = 0;
        left.Click += (_, _) => leftClicks++;
        right.Click += (_, _) => rightClicks++;

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: new Vector2(30f, 30f)));
        Assert.Equal(1, uiRoot.GetInputMetricsSnapshot().HitTestCount);

        // Move to the right button with max-savings hover reuse: no new hit-test.
        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: new Vector2(230f, 30f)));
        Assert.Equal(0, uiRoot.GetInputMetricsSnapshot().HitTestCount);

        // Click transition must force precise targeting.
        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, position: new Vector2(230f, 30f), leftPressed: true));
        Assert.Equal(1, uiRoot.GetInputMetricsSnapshot().HitTestCount);

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, position: new Vector2(230f, 30f), leftReleased: true));
        Assert.Equal(0, uiRoot.GetInputMetricsSnapshot().HitTestCount);

        Assert.Equal(0, leftClicks);
        Assert.Equal(1, rightClicks);
    }

    [Fact]
    public void ListHover_ManyItems_StaysLowHitTestRate()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 500f, 600f));
        var listBox = new ListBox();
        listBox.SetLayoutSlot(new LayoutRect(10f, 10f, 320f, 500f));
        for (var i = 0; i < 1000; i++)
        {
            listBox.Items.Add($"Item {i}");
        }

        root.AddChild(listBox);
        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: new Vector2(40f, 40f)));
        Assert.Equal(1, uiRoot.GetInputMetricsSnapshot().HitTestCount);

        for (var i = 0; i < 25; i++)
        {
            uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: new Vector2(40f, 40f + i)));
            Assert.Equal(0, uiRoot.GetInputMetricsSnapshot().HitTestCount);
        }
    }

    private static InputDelta CreateDelta(
        bool pointerMoved,
        Vector2 position,
        int wheelDelta = 0,
        bool leftPressed = false,
        bool leftReleased = false)
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
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }
}
