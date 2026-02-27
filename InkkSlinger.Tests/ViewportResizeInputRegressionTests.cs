using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ViewportResizeInputRegressionTests
{
    [Fact]
    public void StationaryClick_AfterViewportResize_ShouldTargetElementUnderCurrentPointer()
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

        var leftClicks = 0;
        var rightClicks = 0;

        var leftButton = new Button { Text = "Left" };
        leftButton.Click += (_, _) => leftClicks++;
        Grid.SetColumn(leftButton, 0);
        root.AddChild(leftButton);

        var rightButton = new Button { Text = "Right" };
        rightButton.Click += (_, _) => rightClicks++;
        Grid.SetColumn(rightButton, 1);
        root.AddChild(rightButton);

        var uiRoot = new UiRoot(root);

        RunLayout(uiRoot, width: 200, height: 120, elapsedMs: 16);
        var pointer = new Vector2(150f, 60f); // Right column at 200px width.
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));

        RunLayout(uiRoot, width: 400, height: 120, elapsedMs: 32);
        // Same pointer is now in the left column at 400px width.
        ClickWithoutPointerMove(uiRoot, pointer);

        Assert.True(
            leftClicks == 1 && rightClicks == 0,
            $"Expected stationary click after resize to invoke left button only. leftClicks={leftClicks}, rightClicks={rightClicks}");
    }

    [Fact]
    public void StationaryClick_AfterViewportResize_ShouldSelectListBoxItemUnderCurrentPointer()
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

        var leftList = new ListBox();
        leftList.Items.Add("Left-Item");
        Grid.SetColumn(leftList, 0);
        root.AddChild(leftList);

        var rightList = new ListBox();
        rightList.Items.Add("Right-Item");
        Grid.SetColumn(rightList, 1);
        root.AddChild(rightList);

        var uiRoot = new UiRoot(root);

        RunLayout(uiRoot, width: 200, height: 180, elapsedMs: 16);
        var pointer = new Vector2(150f, 40f); // Right ListBox at 200px width.
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));

        RunLayout(uiRoot, width: 400, height: 180, elapsedMs: 32);
        // Same pointer is now over the left ListBox at 400px width.
        ClickWithoutPointerMove(uiRoot, pointer);

        Assert.True(
            leftList.SelectedIndex == 0 && rightList.SelectedIndex == -1,
            $"Expected stationary click after resize to select left list item only. leftSelected={leftList.SelectedIndex}, rightSelected={rightList.SelectedIndex}");
    }

    private static void ClickWithoutPointerMove(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true, pointerMoved: false));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true, pointerMoved: false));
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

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }
}
