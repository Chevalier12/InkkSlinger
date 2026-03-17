using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class GridSplitterInputTests
{
    [Fact]
    public void PointerDrag_ResizesAdjacentColumns_AndReleasesPointerCapture()
    {
        var (uiRoot, grid, splitter) = CreateColumnSplitterFixture();

        var start = GetCenter(splitter.LayoutSlot);
        var end = new Vector2(start.X + 24f, start.Y);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, leftPressed: true));

        Assert.Same(splitter, FocusManager.GetCapturedPointerElement());
        Assert.Same(splitter, FocusManager.GetFocusedElement());
        Assert.True(splitter.IsDragging);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, pointerMoved: true));
        RunLayout(uiRoot, 260, 120, 32);

        Assert.Equal(124f, grid.ColumnDefinitions[0].ActualWidth, 0.01f);
        Assert.Equal(76f, grid.ColumnDefinitions[2].ActualWidth, 0.01f);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, leftReleased: true));

        Assert.False(splitter.IsDragging);
        Assert.Null(FocusManager.GetCapturedPointerElement());
    }

    [Fact]
    public void PointerMove_TogglesGridSplitterHoverState()
    {
        var (uiRoot, _, splitter) = CreateColumnSplitterFixture();

        var inside = GetCenter(splitter.LayoutSlot);
        var outside = new Vector2(splitter.LayoutSlot.X + splitter.LayoutSlot.Width + 24f, inside.Y);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(inside, pointerMoved: true));
        Assert.True(splitter.IsMouseOver);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(outside, pointerMoved: true));
        Assert.False(splitter.IsMouseOver);
    }

    [Fact]
    public void KeyboardArrowInput_UsesKeyboardIncrement()
    {
        var (uiRoot, grid, splitter) = CreateColumnSplitterFixture();
        splitter.KeyboardIncrement = 10f;

        uiRoot.SetFocusedElementForTests(splitter);
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Right));
        RunLayout(uiRoot, 260, 120, 32);

        Assert.Equal(110f, grid.ColumnDefinitions[0].ActualWidth, 0.01f);
        Assert.Equal(90f, grid.ColumnDefinitions[2].ActualWidth, 0.01f);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Left));
        RunLayout(uiRoot, 260, 120, 48);

        Assert.Equal(100f, grid.ColumnDefinitions[0].ActualWidth, 0.01f);
        Assert.Equal(100f, grid.ColumnDefinitions[2].ActualWidth, 0.01f);
    }

    private static (UiRoot UiRoot, Grid Grid, GridSplitter Splitter) CreateColumnSplitterFixture()
    {
        var host = new Canvas
        {
            Width = 260f,
            Height = 120f
        };

        var grid = new Grid
        {
            Width = 205f,
            Height = 60f
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100f) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5f) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100f) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60f) });

        grid.AddChild(new Border());

        var splitter = new GridSplitter
        {
            ResizeDirection = GridResizeDirection.Columns,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            DragIncrement = 1f
        };
        Grid.SetColumn(splitter, 1);
        grid.AddChild(splitter);

        var right = new Border();
        Grid.SetColumn(right, 2);
        grid.AddChild(right);

        host.AddChild(grid);
        Canvas.SetLeft(grid, 20f);
        Canvas.SetTop(grid, 24f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 260, 120, 16);
        return (uiRoot, grid, splitter);
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));
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

    private static InputDelta CreateKeyDownDelta(Keys key)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(new KeyboardState(), default, Vector2.Zero),
            Current = new InputSnapshot(new KeyboardState(key), default, Vector2.Zero),
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

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }
}