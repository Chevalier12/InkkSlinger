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

    [Fact]
    public void PointerDrag_WhenClamped_RepeatedHoldMoveKeepsResolvedSizesStable()
    {
        var (uiRoot, grid, splitter) = CreateColumnSplitterFixture();

        var start = GetCenter(splitter.LayoutSlot);
        var clamped = new Vector2(start.X + 200f, start.Y);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(clamped, pointerMoved: true));
        RunLayout(uiRoot, 260, 120, 32);

        Assert.Equal(200f, grid.ColumnDefinitions[0].ActualWidth, 0.01f);
        Assert.Equal(0f, grid.ColumnDefinitions[2].ActualWidth, 0.01f);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(clamped, pointerMoved: true));
        RunLayout(uiRoot, 260, 120, 32);

        Assert.Equal(200f, grid.ColumnDefinitions[0].ActualWidth, 0.01f);
        Assert.Equal(0f, grid.ColumnDefinitions[2].ActualWidth, 0.01f);
    }

    [Fact]
    public void PointerDrag_RepeatedHeldMoveAtSamePosition_IsReportedAsNoOpAfterFirstApply()
    {
        var (uiRoot, grid, splitter) = CreateColumnSplitterFixture();

        var start = GetCenter(splitter.LayoutSlot);
        var end = new Vector2(start.X + 24f, start.Y);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, leftPressed: true));

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, pointerMoved: true));
        RunLayout(uiRoot, 260, 120, 32);

        var afterFirstMove = splitter.GetGridSplitterSnapshotForDiagnostics();
        var leftAfterFirstMove = grid.ColumnDefinitions[0].ActualWidth;
        var rightAfterFirstMove = grid.ColumnDefinitions[2].ActualWidth;

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, pointerMoved: true));
        RunLayout(uiRoot, 260, 120, 32);

        var afterSecondMove = splitter.GetGridSplitterSnapshotForDiagnostics();

        Assert.Equal(leftAfterFirstMove, grid.ColumnDefinitions[0].ActualWidth, 0.01f);
        Assert.Equal(rightAfterFirstMove, grid.ColumnDefinitions[2].ActualWidth, 0.01f);
        Assert.Equal(afterFirstMove.PointerMoveApplyCount, afterSecondMove.PointerMoveApplyCount);
        Assert.Equal(afterFirstMove.PointerMoveNoOpDeltaCount + 1, afterSecondMove.PointerMoveNoOpDeltaCount);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, leftReleased: true));
    }

    [Fact]
    public void TelemetrySnapshot_RepeatedHeldMoveAtSamePosition_CountsSecondMoveAsNoOpNotApply()
    {
        _ = GridSplitter.GetTelemetryAndReset();

        var (uiRoot, _, splitter) = CreateColumnSplitterFixture();

        var start = GetCenter(splitter.LayoutSlot);
        var end = new Vector2(start.X + 24f, start.Y);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, leftPressed: true));

    uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, pointerMoved: true));
        RunLayout(uiRoot, 260, 120, 32);

    uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, pointerMoved: true));
        RunLayout(uiRoot, 260, 120, 32);

    uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, leftReleased: true));

        var runtime = splitter.GetGridSplitterSnapshotForDiagnostics();
        var aggregate = GridSplitter.GetTelemetryAndReset();

        Assert.Equal(1L, runtime.PointerMoveApplyCount);
        Assert.Equal(1L, runtime.PointerMoveNoOpDeltaCount);
        Assert.Equal(1L, aggregate.PointerMoveApplyCount);
        Assert.Equal(1L, aggregate.PointerMoveNoOpDeltaCount);
    }

    [Fact]
    public void TelemetrySnapshot_CapturesColumnPointerKeyboardAndHoverPaths()
    {
        _ = GridSplitter.GetTelemetryAndReset();

        var (uiRoot, _, splitter) = CreateColumnSplitterFixture();
        var inside = GetCenter(splitter.LayoutSlot);
        var moved = new Vector2(inside.X + 12f, inside.Y);
        var outside = new Vector2(splitter.LayoutSlot.X + splitter.LayoutSlot.Width + 24f, inside.Y);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(inside, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(outside, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(inside, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(inside, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(moved, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(moved, leftReleased: true));

        uiRoot.SetFocusedElementForTests(splitter);
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Right));
        RunLayout(uiRoot, 260, 120, 32);

        var runtime = splitter.GetGridSplitterSnapshotForDiagnostics();
        Assert.True(runtime.IsMouseOver);
        Assert.False(runtime.IsDragging);
        Assert.Equal(GridResizeDirection.Columns, runtime.EffectiveResizeDirection);
        Assert.Equal(1L, runtime.PointerDownBeginDragSuccessCount);
        Assert.Equal(1L, runtime.PointerMoveApplyCount);
        Assert.Equal(1L, runtime.PointerUpSuccessCount);
        Assert.Equal(3L, runtime.SetMouseOverFromInputChangedCount);
        Assert.Equal(1L, runtime.KeyDownApplyCount);
        Assert.True(runtime.ApplyResizeCallCount >= 2);
        Assert.True(runtime.ResolveColumnSizeCallCount >= 4);
        Assert.True(runtime.SnapCallCount >= 1);

        var aggregate = GridSplitter.GetTelemetryAndReset();
        Assert.True(aggregate.PointerDownCallCount >= 1);
        Assert.True(aggregate.PointerMoveCallCount >= 1);
        Assert.True(aggregate.PointerUpCallCount >= 1);
        Assert.True(aggregate.KeyDownCallCount >= 1);
        Assert.True(aggregate.ApplyResizeCallCount >= 2);
        Assert.True(aggregate.ResolveColumnSizeCallCount >= 4);
        Assert.True(aggregate.SnapCallCount >= 1);

        var cleared = GridSplitter.GetTelemetryAndReset();
        Assert.Equal(0L, cleared.PointerDownCallCount);
        Assert.Equal(0L, cleared.ApplyResizeCallCount);
        Assert.Equal(0L, cleared.ResolveColumnSizeCallCount);
    }

    [Fact]
    public void TelemetrySnapshot_CapturesRowAndRejectedPaths()
    {
        _ = GridSplitter.GetTelemetryAndReset();

        var disabled = new GridSplitter { IsEnabled = false };
        Assert.False(disabled.HandlePointerDownFromInput(Vector2.Zero));

        var (uiRoot, grid, splitter) = CreateRowSplitterFixture();
        splitter.HandlePointerMoveFromInput(Vector2.Zero);
        splitter.HandlePointerUpFromInput();

        uiRoot.SetFocusedElementForTests(splitter);
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Down));
        RunLayout(uiRoot, 120, 260, 24);

        var start = GetCenter(splitter.LayoutSlot);
        var end = new Vector2(start.X, start.Y + 18f);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, pointerMoved: true));
        RunLayout(uiRoot, 120, 260, 32);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, leftReleased: true));

        Assert.Equal(130f, grid.RowDefinitions[0].ActualHeight, 0.01f);
        Assert.Equal(70f, grid.RowDefinitions[2].ActualHeight, 0.01f);

        var runtime = splitter.GetGridSplitterSnapshotForDiagnostics();
        Assert.Equal(GridResizeDirection.Rows, runtime.EffectiveResizeDirection);
        Assert.True(runtime.KeyDownRowsDirectionCount >= 1);
        Assert.True(runtime.BeginDragRowsDirectionCount >= 1);
        Assert.True(runtime.ApplyResizeRowsPathCount >= 2);
        Assert.True(runtime.PointerDownBeginDragSuccessCount >= 1);
        Assert.True(runtime.PointerMoveRejectedNotDraggingCount >= 1);
        Assert.True(runtime.PointerUpRejectedNotDraggingCount >= 1);
        Assert.True(runtime.ResolveRowSizeCallCount >= 4);

        var aggregate = GridSplitter.GetTelemetryAndReset();
        Assert.True(aggregate.PointerDownDisabledRejectCount >= 1);
        Assert.True(aggregate.KeyDownRowsDirectionCount >= 1);
        Assert.True(aggregate.BeginDragRowsDirectionCount >= 1);
        Assert.True(aggregate.ApplyResizeRowsPathCount >= 2);
        Assert.True(aggregate.PointerMoveRejectedNotDraggingCount >= 1);
        Assert.True(aggregate.PointerUpRejectedNotDraggingCount >= 1);
        Assert.True(aggregate.ResolveRowSizeCallCount >= 4);
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

    private static (UiRoot UiRoot, Grid Grid, GridSplitter Splitter) CreateRowSplitterFixture()
    {
        var host = new Canvas
        {
            Width = 120f,
            Height = 260f
        };

        var grid = new Grid
        {
            Width = 60f,
            Height = 205f
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60f) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100f) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5f) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100f) });

        grid.AddChild(new Border());

        var splitter = new GridSplitter
        {
            ResizeDirection = GridResizeDirection.Rows,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            DragIncrement = 1f,
            KeyboardIncrement = 12f
        };
        Grid.SetRow(splitter, 1);
        grid.AddChild(splitter);

        var bottom = new Border();
        Grid.SetRow(bottom, 2);
        grid.AddChild(bottom);

        host.AddChild(grid);
        Canvas.SetLeft(grid, 24f);
        Canvas.SetTop(grid, 20f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 120, 260, 16);
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
