using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class MouseEnterLeavePhase3Tests
{
    [Fact]
    public void HoverBoundaryCrossing_RaisesMouseEnterAndMouseLeaveOnce()
    {
        var root = new Canvas();
        var button = new Button
        {
            Width = 100f,
            Height = 40f
        };
        Canvas.SetLeft(button, 20f);
        Canvas.SetTop(button, 20f);
        root.AddChild(button);

        var enterCount = 0;
        var leaveCount = 0;
        button.AddHandler<MouseRoutedEventArgs>(UIElement.MouseEnterEvent, (_, _) => enterCount++);
        button.AddHandler<MouseRoutedEventArgs>(UIElement.MouseLeaveEvent, (_, _) => leaveCount++);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, width: 220, height: 140, elapsedMs: 16);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(4f, 4f), pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(30f, 30f), pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(35f, 32f), pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(180f, 120f), pointerMoved: true));

        Assert.Equal(1, enterCount);
        Assert.Equal(1, leaveCount);
    }

    [Fact]
    public void StationaryPointerWithoutHoverChange_DoesNotRaiseEnterLeaveAgain()
    {
        var root = new Canvas();
        var button = new Button
        {
            Width = 100f,
            Height = 40f
        };
        Canvas.SetLeft(button, 20f);
        Canvas.SetTop(button, 20f);
        root.AddChild(button);

        var enterCount = 0;
        var leaveCount = 0;
        button.AddHandler<MouseRoutedEventArgs>(UIElement.MouseEnterEvent, (_, _) => enterCount++);
        button.AddHandler<MouseRoutedEventArgs>(UIElement.MouseLeaveEvent, (_, _) => leaveCount++);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, width: 220, height: 140, elapsedMs: 16);

        var inside = new Vector2(30f, 30f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(inside, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(inside, pointerMoved: false));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(inside, pointerMoved: false));

        Assert.Equal(1, enterCount);
        Assert.Equal(0, leaveCount);
    }

    [Fact]
    public void EventTrigger_MouseEnterAndMouseLeave_ResolvesAndExecutesActions()
    {
        var root = new Canvas();
        var button = new Button
        {
            Width = 100f,
            Height = 40f
        };
        Canvas.SetLeft(button, 20f);
        Canvas.SetTop(button, 20f);
        root.AddChild(button);

        var enterAction = new CountingTriggerAction();
        var leaveAction = new CountingTriggerAction();
        var enterTrigger = new EventTrigger { RoutedEvent = "MouseEnter" };
        enterTrigger.Actions.Add(enterAction);
        enterTrigger.Attach(button, static () => { });

        var leaveTrigger = new EventTrigger { RoutedEvent = "MouseLeave" };
        leaveTrigger.Actions.Add(leaveAction);
        leaveTrigger.Attach(button, static () => { });

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, width: 220, height: 140, elapsedMs: 16);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(30f, 30f), pointerMoved: true));
        Assert.Equal(1, enterAction.InvokeCount);
        Assert.Equal(0, leaveAction.InvokeCount);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(180f, 120f), pointerMoved: true));
        Assert.Equal(1, enterAction.InvokeCount);
        Assert.Equal(1, leaveAction.InvokeCount);
    }

    [Fact]
    public void HoveringChild_SetsIsMouseOverOnAncestorRoute()
    {
        var hoverBackground = new Color(0x24, 0x27, 0x2C);
        var rowStyle = new Style(typeof(Border));
        rowStyle.Setters.Add(new Setter(Border.BackgroundProperty, Color.Transparent));
        var hoverTrigger = new Trigger(UIElement.IsMouseOverProperty, true);
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hoverBackground));
        rowStyle.Triggers.Add(hoverTrigger);

        var root = new Canvas();
        var row = new Border
        {
            Width = 180f,
            Height = 64f,
            Style = rowStyle,
            Child = new Button
            {
                Width = 140f,
                Height = 40f,
                Content = "Project"
            }
        };

        Canvas.SetLeft(row, 20f);
        Canvas.SetTop(row, 20f);
        root.AddChild(row);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, width: 260, height: 140, elapsedMs: 16);

        var hoverPoint = new Vector2(row.LayoutSlot.X + 40f, row.LayoutSlot.Y + 24f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(hoverPoint, pointerMoved: true));

        Assert.True(row.IsMouseOver);
        Assert.True(((Button)row.Child!).IsMouseOver);
        Assert.Equal(hoverBackground, Assert.IsType<SolidColorBrush>(row.Background).Color);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(240f, 120f), pointerMoved: true));

        Assert.False(row.IsMouseOver);
        Assert.False(((Button)row.Child!).IsMouseOver);
        Assert.Equal(Color.Transparent, Assert.IsType<SolidColorBrush>(row.Background).Color);
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

    private sealed class CountingTriggerAction : TriggerAction
    {
        public int InvokeCount { get; private set; }

        public override void Invoke(DependencyObject target)
        {
            InvokeCount++;
        }
    }
}
