using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ContextMenuEdgeParityTests
{
    [Fact]
    public void OpenAt_ShouldSetAbsolutePosition_AndIsOpenTrue()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var menu = new ContextMenu();

        menu.OpenAt(host, left: 123f, top: 67f);
        RunLayout(uiRoot);

        Assert.True(menu.IsOpen);
        Assert.Equal(PopupPlacementMode.Absolute, menu.PlacementMode);
        Assert.Equal(123f, menu.Left);
        Assert.Equal(67f, menu.Top);
    }

    [Fact]
    public void OutsideClick_WhenStaysOpenFalse_ShouldClose()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var anchor = new TextBox { Width = 120f, Height = 40f };
        host.AddChild(anchor);
        Canvas.SetLeft(anchor, 8f);
        Canvas.SetTop(anchor, 8f);

        var menu = new ContextMenu
        {
            StaysOpen = false
        };
        menu.Items.Add(new Label { Text = "item" });
        menu.OpenAt(host, left: 220f, top: 120f, placementTarget: anchor);
        RunLayout(uiRoot);

        Click(uiRoot, new Vector2(20f, 20f));

        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void OutsideClick_WhenStaysOpenTrue_ShouldRemainOpen()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var anchor = new TextBox { Width = 120f, Height = 40f };
        host.AddChild(anchor);
        Canvas.SetLeft(anchor, 8f);
        Canvas.SetTop(anchor, 8f);

        var menu = new ContextMenu
        {
            StaysOpen = true
        };
        menu.Items.Add(new Label { Text = "item" });
        menu.OpenAt(host, left: 220f, top: 120f, placementTarget: anchor);
        RunLayout(uiRoot);

        Click(uiRoot, new Vector2(20f, 20f));

        Assert.True(menu.IsOpen);
    }

    [Fact]
    public void Esc_WhenOpen_ShouldClose()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var menu = new ContextMenu
        {
            StaysOpen = true
        };
        menu.Items.Add(new Label { Text = "item" });
        menu.OpenAt(host, left: 160f, top: 110f);
        RunLayout(uiRoot);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Escape));

        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void OpenAt_InPanelHost_ShouldNotStretchToHostHeight()
    {
        var host = new Panel
        {
            Width = 640f,
            Height = 420f
        };
        var menu = new ContextMenu
        {
            StaysOpen = false
        };
        menu.Items.Add(new Label { Text = "item A" });
        menu.Items.Add(new Label { Text = "item B" });
        menu.Items.Add(new Label { Text = "item C" });

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 640, 420);

        menu.OpenAt(host, left: 140f, top: 90f);
        RunLayout(uiRoot, 640, 420);

        Assert.True(menu.IsOpen);
        Assert.True(float.IsFinite(menu.Height), $"Height should be finite but was {menu.Height}");
        Assert.True(menu.Height < host.LayoutSlot.Height * 0.75f, $"Height property too large: menu.Height={menu.Height}, host={host.LayoutSlot.Height}");
        Assert.True(menu.LayoutSlot.Height < host.LayoutSlot.Height * 0.75f);
    }

    private static (UiRoot UiRoot, Canvas Host) CreateUiRootWithHost()
    {
        var host = new Canvas
        {
            Width = 400f,
            Height = 240f
        };
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);
        return (uiRoot, host);
    }

    private static void Click(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));
    }

    private static InputDelta CreateKeyDownDelta(Keys key, KeyboardState? keyboard = null)
    {
        var state = keyboard ?? default;
        var pointer = new Vector2(12f, 12f);
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(state, default, pointer),
            PressedKeys = new List<Keys> { key },
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = false,
            WheelDelta = 0,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static InputDelta CreatePointerDelta(Vector2 pointer, bool leftPressed = false, bool leftReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = true,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void RunLayout(UiRoot uiRoot, int width = 400, int height = 240)
    {
        const string inputPipelineVariable = "INKKSLINGER_ENABLE_INPUT_PIPELINE";
        var previous = Environment.GetEnvironmentVariable(inputPipelineVariable);
        Environment.SetEnvironmentVariable(inputPipelineVariable, "0");
        try
        {
            uiRoot.Update(
                new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
                new Viewport(0, 0, width, height));
        }
        finally
        {
            Environment.SetEnvironmentVariable(inputPipelineVariable, previous);
        }
    }
}
