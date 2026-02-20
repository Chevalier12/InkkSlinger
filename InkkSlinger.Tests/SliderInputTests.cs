using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class SliderInputTests
{
    [Fact]
    public void DraggingThumb_ShouldUpdateValue()
    {
        var host = new Canvas
        {
            Width = 460f,
            Height = 220f
        };

        var slider = new Slider
        {
            Width = 240f,
            Height = 28f,
            Minimum = -200f,
            Maximum = 200f,
            Value = 0f
        };
        host.AddChild(slider);
        Canvas.SetLeft(slider, 40f);
        Canvas.SetTop(slider, 80f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        var y = slider.LayoutSlot.Y + (slider.LayoutSlot.Height / 2f);
        var downPoint = new Vector2(slider.LayoutSlot.X + (slider.LayoutSlot.Width / 2f), y);
        var movePoint = new Vector2(slider.LayoutSlot.X + slider.LayoutSlot.Width - 8f, y);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(downPoint, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(movePoint, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(movePoint, leftReleased: true));

        Assert.True(slider.Value > 100f);
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
        const string inputPipelineVariable = "INKKSLINGER_ENABLE_INPUT_PIPELINE";
        var previous = System.Environment.GetEnvironmentVariable(inputPipelineVariable);
        System.Environment.SetEnvironmentVariable(inputPipelineVariable, "0");
        try
        {
            uiRoot.Update(
                new GameTime(System.TimeSpan.FromMilliseconds(16), System.TimeSpan.FromMilliseconds(16)),
                new Viewport(0, 0, 460, 220));
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(inputPipelineVariable, previous);
        }
    }
}
