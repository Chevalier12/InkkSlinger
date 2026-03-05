using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ToolTipParityTests
{
    [Fact]
    public void Hover_DelaysThenOpensToolTip()
    {
        var (uiRoot, host, target) = CreateFixture();
        var toolTip = new ToolTip();
        ToolTipService.SetToolTip(target, toolTip);
        ToolTipService.SetInitialShowDelay(target, 200);

        var pointer = GetCenter(target);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: false), elapsedMs: 199);
        Assert.False(toolTip.IsOpen);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: false), elapsedMs: 1);
        Assert.True(toolTip.IsOpen);
    }

    [Fact]
    public void Hover_SwitchBetweenTargetsWithinBetweenShowDelay_ReopensImmediately()
    {
        var (uiRoot, host, first) = CreateFixture();
        var second = new Button { Width = 140f, Height = 36f, Text = "Second" };
        host.AddChild(second);
        Canvas.SetLeft(second, 220f);
        Canvas.SetTop(second, 40f);
        RunLayout(uiRoot);

        var firstTip = new ToolTip();
        var secondTip = new ToolTip();
        ToolTipService.SetToolTip(first, firstTip);
        ToolTipService.SetToolTip(second, secondTip);

        var firstPointer = GetCenter(first);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(firstPointer, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(firstPointer, pointerMoved: false), elapsedMs: 500);
        Assert.True(firstTip.IsOpen);

        var secondPointer = GetCenter(second);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(secondPointer, pointerMoved: true));
        Assert.False(firstTip.IsOpen);
        Assert.True(secondTip.IsOpen);
    }

    [Fact]
    public void ShowDuration_AutoClosesOpenToolTip()
    {
        var (uiRoot, _, target) = CreateFixture();
        var toolTip = new ToolTip();
        ToolTipService.SetToolTip(target, toolTip);
        ToolTipService.SetShowDuration(target, 120);

        var pointer = GetCenter(target);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: false), elapsedMs: 500);
        Assert.True(toolTip.IsOpen);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: false), elapsedMs: 120);
        Assert.False(toolTip.IsOpen);
    }

    [Fact]
    public void ToolTip_ClosesOnLeaveDisableAndDetach()
    {
        var (uiRoot, host, target) = CreateFixture();
        var toolTip = new ToolTip();
        ToolTipService.SetToolTip(target, toolTip);

        var pointer = GetCenter(target);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: false), elapsedMs: 500);
        Assert.True(toolTip.IsOpen);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(8f, 8f), pointerMoved: true));
        Assert.False(toolTip.IsOpen);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: false), elapsedMs: 500);
        Assert.True(toolTip.IsOpen);

        target.IsEnabled = false;
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: false), elapsedMs: 16);
        Assert.False(toolTip.IsOpen);

        target.IsEnabled = true;
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: false), elapsedMs: 500);
        Assert.True(toolTip.IsOpen);

        host.RemoveChild(target);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: false), elapsedMs: 16);
        Assert.False(toolTip.IsOpen);
    }

    [Fact]
    public void ToolTip_DoesNotCapturePointerOrStealFocus()
    {
        FocusManager.ClearFocus();
        FocusManager.ClearPointerCapture();
        var host = new Canvas
        {
            Width = 480f,
            Height = 300f
        };

        var focusBox = new TextBox
        {
            Width = 180f,
            Height = 36f
        };
        host.AddChild(focusBox);
        Canvas.SetLeft(focusBox, 20f);
        Canvas.SetTop(focusBox, 20f);

        var hoverButton = new Button
        {
            Width = 180f,
            Height = 36f,
            Text = "Hover me"
        };
        host.AddChild(hoverButton);
        Canvas.SetLeft(hoverButton, 20f);
        Canvas.SetTop(hoverButton, 90f);

        var tip = new ToolTip();
        ToolTipService.SetToolTip(hoverButton, tip);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        Click(uiRoot, GetCenter(focusBox));
        Assert.Same(focusBox, FocusManager.GetFocusedElement());

        var hoverPointer = GetCenter(hoverButton);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(hoverPointer, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(hoverPointer, pointerMoved: false), elapsedMs: 500);

        Assert.True(tip.IsOpen);
        Assert.Same(focusBox, FocusManager.GetFocusedElement());
        Assert.Null(FocusManager.GetCapturedPointerElement());
    }

    [Fact]
    public void ExternallyClosedActiveToolTip_ClearsStateAndCanReopen()
    {
        var (uiRoot, _, target) = CreateFixture();
        var toolTip = new ToolTip();
        ToolTipService.SetToolTip(target, toolTip);
        ToolTipService.SetInitialShowDelay(target, 200);

        var pointer = GetCenter(target);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: false), elapsedMs: 200);
        Assert.True(toolTip.IsOpen);

        toolTip.Close();
        Assert.False(toolTip.IsOpen);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: false), elapsedMs: 0);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: false), elapsedMs: 200);
        Assert.True(toolTip.IsOpen);
    }

    [Fact]
    public void ToolTipService_WhenToolTipValueIsUIElement_ThrowsClearDiagnostic()
    {
        var (uiRoot, _, target) = CreateFixture();
        ToolTipService.SetToolTip(target, new Border { Width = 12f, Height = 12f });

        var pointer = GetCenter(target);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        var exception = Assert.Throws<InvalidOperationException>(() =>
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: false), elapsedMs: 500));

        Assert.Contains("does not support UIElement values", exception.Message);
        Assert.Contains("Use a ToolTip instance instead", exception.Message);
    }

    private static (UiRoot UiRoot, Canvas Host, Button Target) CreateFixture()
    {
        FocusManager.ClearFocus();
        FocusManager.ClearPointerCapture();
        var host = new Canvas
        {
            Width = 480f,
            Height = 300f
        };

        var button = new Button
        {
            Width = 140f,
            Height = 36f,
            Text = "Hover"
        };
        host.AddChild(button);
        Canvas.SetLeft(button, 40f);
        Canvas.SetTop(button, 40f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);
        return (uiRoot, host, button);
    }

    private static Vector2 GetCenter(UIElement element)
    {
        return new Vector2(
            element.LayoutSlot.X + (element.LayoutSlot.Width * 0.5f),
            element.LayoutSlot.Y + (element.LayoutSlot.Height * 0.5f));
    }

    private static void Click(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));
    }

    private static InputDelta CreatePointerDelta(
        Vector2 pointer,
        bool leftPressed = false,
        bool leftReleased = false,
        bool pointerMoved = true)
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
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 480, 300));
    }
}
