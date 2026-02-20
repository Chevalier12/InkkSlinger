using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class PopupEdgeParityTests
{
    [Fact]
    public void Show_WhenAlreadyOpen_ShouldActivateAndStaySingleHostChild()
    {
        var host = CreateHostCanvas();
        var popup = CreatePopup(dismissOnOutsideClick: true, canClose: true);

        popup.Show(host);
        popup.Show(host);

        Assert.True(popup.IsOpen);
        Assert.Equal(1, CountChildrenOfType<Popup>(host));
    }

    [Fact]
    public void Close_ShouldRemoveFromHost_AndRaiseClosedOnce()
    {
        var host = CreateHostCanvas();
        var popup = CreatePopup(dismissOnOutsideClick: true, canClose: true);
        var closeCount = 0;
        popup.Closed += (_, _) => closeCount++;

        popup.Show(host);
        popup.Close();
        popup.Close();

        Assert.False(popup.IsOpen);
        Assert.Equal(0, CountChildrenOfType<Popup>(host));
        Assert.Equal(1, closeCount);
    }

    [Fact]
    public void DismissOnOutsideClick_True_ShouldClose_OnExternalPointerDown()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var target = new TextBox();
        host.AddChild(target);
        Canvas.SetLeft(target, 8f);
        Canvas.SetTop(target, 8f);
        target.Width = 120f;
        target.Height = 40f;

        var popup = CreatePopup(dismissOnOutsideClick: true, canClose: true);
        popup.Left = 260f;
        popup.Top = 120f;
        popup.Show(host);
        RunLayout(uiRoot);

        Click(uiRoot, new Vector2(20f, 20f));

        Assert.False(popup.IsOpen);
    }

    [Fact]
    public void DismissOnOutsideClick_False_ShouldStayOpen_OnExternalPointerDown()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var target = new TextBox();
        host.AddChild(target);
        Canvas.SetLeft(target, 8f);
        Canvas.SetTop(target, 8f);
        target.Width = 120f;
        target.Height = 40f;

        var popup = CreatePopup(dismissOnOutsideClick: false, canClose: true);
        popup.Left = 260f;
        popup.Top = 120f;
        popup.Show(host);
        RunLayout(uiRoot);

        Click(uiRoot, new Vector2(20f, 20f));

        Assert.True(popup.IsOpen);
    }

    [Fact]
    public void Esc_WhenCanClose_ShouldClose()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var popup = CreatePopup(dismissOnOutsideClick: false, canClose: true);
        popup.Show(host);
        RunLayout(uiRoot);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Escape));

        Assert.False(popup.IsOpen);
    }

    [Fact]
    public void Esc_WhenCanCloseFalse_ShouldRemainOpen()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var popup = CreatePopup(dismissOnOutsideClick: false, canClose: false);
        popup.Show(host);
        RunLayout(uiRoot);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Escape));

        Assert.True(popup.IsOpen);
    }

    [Fact]
    public void CloseButton_WhenCanClose_ShouldClose_OnClick()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var popup = CreatePopup(dismissOnOutsideClick: false, canClose: true);
        popup.Left = 120f;
        popup.Top = 80f;
        popup.Width = 220f;
        popup.Height = 120f;
        popup.Show(host);
        RunLayout(uiRoot);

        var clickPoint = new Vector2(
            popup.LayoutSlot.X + popup.LayoutSlot.Width - 10f,
            popup.LayoutSlot.Y + 10f);
        Click(uiRoot, clickPoint);

        Assert.False(popup.IsOpen);
    }

    [Fact]
    public void Placement_BottomRightCenterAbsolute_ShouldClampWithinHostBounds()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var target = new Border { Width = 80f, Height = 30f };
        host.AddChild(target);
        Canvas.SetLeft(target, 340f);
        Canvas.SetTop(target, 180f);

        var popup = CreatePopup(dismissOnOutsideClick: false, canClose: true);
        popup.Width = 180f;
        popup.Height = 90f;
        popup.PlacementTarget = target;
        popup.Show(host);

        popup.PlacementMode = PopupPlacementMode.Bottom;
        RunLayout(uiRoot);
        Assert.InRange(popup.Left, 0f, 400f - 180f);
        Assert.InRange(popup.Top, 0f, 240f - 90f);

        popup.PlacementMode = PopupPlacementMode.Right;
        RunLayout(uiRoot);
        Assert.InRange(popup.Left, 0f, 400f - 180f);
        Assert.InRange(popup.Top, 0f, 240f - 90f);

        popup.PlacementMode = PopupPlacementMode.Center;
        RunLayout(uiRoot);
        Assert.InRange(popup.Left, 0f, 400f - 180f);
        Assert.InRange(popup.Top, 0f, 240f - 90f);

        popup.PlacementMode = PopupPlacementMode.Absolute;
        popup.Left = 999f;
        popup.Top = 999f;
        RunLayout(uiRoot);
        Assert.InRange(popup.Left, 0f, 400f - 180f);
        Assert.InRange(popup.Top, 0f, 240f - 90f);
    }

    [Fact]
    public void HostLayoutUpdated_ShouldRecomputePlacement_WithoutDrift()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var target = new Border { Width = 120f, Height = 40f };
        host.AddChild(target);
        Canvas.SetLeft(target, 40f);
        Canvas.SetTop(target, 40f);

        var popup = CreatePopup(dismissOnOutsideClick: false, canClose: true);
        popup.Width = 160f;
        popup.Height = 90f;
        popup.PlacementTarget = target;
        popup.PlacementMode = PopupPlacementMode.Bottom;
        popup.Show(host);

        RunLayout(uiRoot);
        var left1 = popup.Left;
        var top1 = popup.Top;

        RunLayout(uiRoot);
        var left2 = popup.Left;
        var top2 = popup.Top;

        Assert.Equal(left1, left2);
        Assert.Equal(top1, top2);
    }

    [Fact]
    public void DragTitleBar_WhenCanDragMove_ShouldUpdatePosition()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var anchor = new Border { Width = 120f, Height = 36f };
        host.AddChild(anchor);
        Canvas.SetLeft(anchor, 120f);
        Canvas.SetTop(anchor, 36f);

        var popup = CreatePopup(dismissOnOutsideClick: false, canClose: true);
        popup.Width = 180f;
        popup.Height = 100f;
        popup.CanDragMove = true;
        popup.PlacementTarget = anchor;
        popup.PlacementMode = PopupPlacementMode.Bottom;
        popup.Show(host);
        RunLayout(uiRoot);

        var startLeft = popup.Left;
        var startTop = popup.Top;
        var downPoint = new Vector2(popup.LayoutSlot.X + 20f, popup.LayoutSlot.Y + 12f);
        var movePoint = new Vector2(downPoint.X + 70f, downPoint.Y + 46f);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(downPoint, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(movePoint));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(movePoint, leftReleased: true));

        Assert.True(popup.Left > startLeft + 20f, $"Left did not move enough. start={startLeft}, current={popup.Left}");
        Assert.True(popup.Top > startTop + 20f, $"Top did not move enough. start={startTop}, current={popup.Top}");
    }

    private static Popup CreatePopup(bool dismissOnOutsideClick, bool canClose)
    {
        return new Popup
        {
            Width = 160f,
            Height = 90f,
            CanClose = canClose,
            DismissOnOutsideClick = dismissOnOutsideClick,
            Content = new Label { Text = "popup" }
        };
    }

    private static Canvas CreateHostCanvas()
    {
        var host = new Canvas
        {
            Width = 400f,
            Height = 240f
        };
        return host;
    }

    private static (UiRoot UiRoot, Canvas Host) CreateUiRootWithHost()
    {
        var host = CreateHostCanvas();
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);
        return (uiRoot, host);
    }

    private static int CountChildrenOfType<T>(Panel panel)
        where T : UIElement
    {
        var count = 0;
        foreach (var child in panel.Children)
        {
            if (child is T)
            {
                count++;
            }
        }

        return count;
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

    private static void RunLayout(UiRoot uiRoot)
    {
        const string inputPipelineVariable = "INKKSLINGER_ENABLE_INPUT_PIPELINE";
        var previous = Environment.GetEnvironmentVariable(inputPipelineVariable);
        Environment.SetEnvironmentVariable(inputPipelineVariable, "0");
        try
        {
            uiRoot.Update(
                new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
                new Viewport(0, 0, 400, 240));
        }
        finally
        {
            Environment.SetEnvironmentVariable(inputPipelineVariable, previous);
        }
    }
}
