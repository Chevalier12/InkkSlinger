using System.Linq;
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlDemoDataGridSampleTests
{
    [Fact]
    public void DataGridSample_ShouldRealizeRowsWithVisibleCellValues()
    {
        var element = ControlDemoSupport.BuildSampleElement("DataGrid");
        var dataGrid = Assert.IsType<DataGrid>(element);

        var host = new Canvas
        {
            Width = 900f,
            Height = 500f
        };
        host.AddChild(dataGrid);
        Canvas.SetLeft(dataGrid, 20f);
        Canvas.SetTop(dataGrid, 20f);

        var uiRoot = new UiRoot(host);
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 900, 500));

        var rows = dataGrid.RowsForTesting;
        Assert.NotEmpty(rows);
        Assert.NotNull(FindFirstVisualChild<DataGridRow>(dataGrid));
        Assert.NotNull(FindFirstVisualChild<DataGridCell>(dataGrid));
        Assert.Contains(uiRoot.GetRetainedVisualOrderForTests(), static element => element is DataGridRow);
        Assert.Contains(uiRoot.GetRetainedVisualOrderForTests(), static element => element is DataGridCell);

        var firstRow = rows[0];
        Assert.Equal("1", firstRow.Cells[0].Value?.ToString());
        Assert.Equal("Alpha", firstRow.Cells[1].Value?.ToString());

        var lastRow = rows.Last();
        Assert.Equal("12", lastRow.Cells[0].Value?.ToString());
        Assert.Equal("Lima", lastRow.Cells[1].Value?.ToString());
        Assert.DoesNotContain(
            uiRoot.GetRetainedVisualOrderForTests(),
            static visual => visual is Label label &&
                             string.Equals(label.Text, "InkkSlinger.ControlDemoSupport+DemoRow", StringComparison.Ordinal));
    }

    [Fact]
    public void DataGridSample_ShouldPreseedChromePropertiesBeforeAttach()
    {
        var element = ControlDemoSupport.BuildSampleElement("DataGrid");
        var dataGrid = Assert.IsType<DataGrid>(element);

        Assert.Equal(1f, dataGrid.BorderThickness);
        Assert.Equal(DependencyPropertyValueSource.Local, dataGrid.GetValueSource(DataGrid.BorderThicknessProperty));
        Assert.Equal(DataGridHeadersVisibility.Column, dataGrid.HeadersVisibility);
        Assert.Equal(DependencyPropertyValueSource.Local, dataGrid.GetValueSource(DataGrid.HeadersVisibilityProperty));
    }

    [Fact]
    public void DataGridSample_ClickingColumnHeader_ShouldSortRows()
    {
        var element = ControlDemoSupport.BuildSampleElement("DataGrid");
        var dataGrid = Assert.IsType<DataGrid>(element);

        var host = new Canvas
        {
            Width = 900f,
            Height = 500f
        };
        host.AddChild(dataGrid);
        Canvas.SetLeft(dataGrid, 20f);
        Canvas.SetTop(dataGrid, 20f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 900, 500);

        var idHeader = FindFirstVisualChild<DataGridColumnHeader>(dataGrid, static header => header.Text == "Id");
        Assert.NotNull(idHeader);
        var clickCount = 0;
        idHeader!.Click += (_, _) => clickCount++;
        var headerCenter = GetCenter(idHeader!.LayoutSlot);
        var hit = VisualTreeHelper.HitTest(host, headerCenter);
        Assert.True(
            ReferenceEquals(hit, idHeader) ||
            FindAncestor<DataGridColumnHeader>(hit) == idHeader,
            $"Expected header hit target, got {hit?.GetType().Name ?? "null"}.");

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(headerCenter, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(headerCenter, leftPressed: true));
        Assert.Same(idHeader, FocusManager.GetCapturedPointerElement());
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(headerCenter, leftReleased: true));
        RunLayout(uiRoot, 900, 500);
        Assert.Equal(1, clickCount);
        Assert.Equal(DataGridSortDirection.Ascending, idHeader.SortDirection);
        Click(uiRoot, headerCenter);
        RunLayout(uiRoot, 900, 500);
        Assert.Equal(DataGridSortDirection.Descending, idHeader.SortDirection);

        var rows = dataGrid.RowsForTesting;
        Assert.NotEmpty(rows);
        Assert.Equal("12", rows[0].Cells[0].Value?.ToString());
        Assert.Equal("Lima", rows[0].Cells[1].Value?.ToString());
        Assert.Equal("1", rows[^1].Cells[0].Value?.ToString());
    }

    [Fact]
    public void DataGridSample_ClickingDifferentHeaders_ShouldReplacePreviousSort()
    {
        var element = ControlDemoSupport.BuildSampleElement("DataGrid");
        var dataGrid = Assert.IsType<DataGrid>(element);

        var host = new Canvas
        {
            Width = 900f,
            Height = 500f
        };
        host.AddChild(dataGrid);
        Canvas.SetLeft(dataGrid, 20f);
        Canvas.SetTop(dataGrid, 20f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 900, 500);

        var nameHeader = FindFirstVisualChild<DataGridColumnHeader>(dataGrid, static header => header.Text == "Name");
        var idHeader = FindFirstVisualChild<DataGridColumnHeader>(dataGrid, static header => header.Text == "Id");
        Assert.NotNull(nameHeader);
        Assert.NotNull(idHeader);

        Click(uiRoot, GetCenter(nameHeader!.LayoutSlot));
        RunLayout(uiRoot, 900, 500);
        Assert.Equal(DataGridSortDirection.Ascending, nameHeader.SortDirection);
        Assert.Equal("Alpha", dataGrid.RowsForTesting[0].Cells[1].Value?.ToString());

        Click(uiRoot, GetCenter(idHeader!.LayoutSlot));
        RunLayout(uiRoot, 900, 500);
        Assert.Equal(DataGridSortDirection.None, nameHeader.SortDirection);
        Assert.Equal(DataGridSortDirection.Ascending, idHeader.SortDirection);
        Assert.Equal("1", dataGrid.RowsForTesting[0].Cells[0].Value?.ToString());

        Click(uiRoot, GetCenter(idHeader.LayoutSlot));
        RunLayout(uiRoot, 900, 500);
        Assert.Equal(DataGridSortDirection.Descending, idHeader.SortDirection);
        Assert.Equal("12", dataGrid.RowsForTesting[0].Cells[0].Value?.ToString());
        Assert.Equal("Lima", dataGrid.RowsForTesting[0].Cells[1].Value?.ToString());
    }

    [Fact]
    public void DataGridSample_NameDescendingSort_RepositionsTopVisibleRowWithoutScroll()
    {
        var element = ControlDemoSupport.BuildSampleElement("DataGrid");
        var dataGrid = Assert.IsType<DataGrid>(element);

        var host = new Canvas
        {
            Width = 900f,
            Height = 500f
        };
        host.AddChild(dataGrid);
        Canvas.SetLeft(dataGrid, 20f);
        Canvas.SetTop(dataGrid, 20f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 900, 500);

        var nameHeader = FindFirstVisualChild<DataGridColumnHeader>(dataGrid, static header => header.Text == "Name");
        Assert.NotNull(nameHeader);

        Click(uiRoot, GetCenter(nameHeader!.LayoutSlot));
        RunLayout(uiRoot, 900, 500);
        Click(uiRoot, GetCenter(nameHeader.LayoutSlot));
        RunLayout(uiRoot, 900, 500);

        Assert.Equal(DataGridSortDirection.Descending, nameHeader.SortDirection);

        var topVisibleRow = dataGrid.RowsForTesting
            .OrderBy(static row => row.LayoutSlot.Y)
            .First();

        Assert.Equal("12", topVisibleRow.Cells[0].Value?.ToString());
        Assert.Equal("Lima", topVisibleRow.Cells[1].Value?.ToString());
    }

    [Fact]
    public void DataGridView_FirstHeaderSort_KeepsDemoGridViewportWidthStable()
    {
        var view = new DataGridView();
        view.Width = 1200f;
        view.Height = 520f;

        var host = new Canvas
        {
            Width = 1200f,
            Height = 520f
        };
        host.AddChild(view);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 1200, 520);

        var dataGrid = FindFirstVisualChild<DataGrid>(view);
        Assert.NotNull(dataGrid);

        var viewportWidthBeforeSort = dataGrid!.ScrollViewerForTesting.ViewportWidth;
        var gridWidthBeforeSort = dataGrid.ActualWidth;

        var nameHeader = FindFirstVisualChild<DataGridColumnHeader>(dataGrid, static header => header.Text == "Name");
        Assert.NotNull(nameHeader);

        Click(uiRoot, GetCenter(nameHeader!.LayoutSlot));
        RunLayout(uiRoot, 1200, 520);

        Assert.Equal(DataGridSortDirection.Ascending, nameHeader.SortDirection);
        Assert.Equal("Alpha", dataGrid.RowsForTesting[0].Cells[1].Value?.ToString());
        Assert.True(dataGrid.ScrollViewerForTesting.ViewportWidth >= viewportWidthBeforeSort - 0.5f);
        Assert.True(dataGrid.ActualWidth >= gridWidthBeforeSort - 0.5f);
    }

    [Fact]
    public void DataGridView_FirstHeaderSort_KeepsCellHeightAlignedWithRowHeight()
    {
        var view = new DataGridView();
        view.Width = 1200f;
        view.Height = 520f;

        var host = new Canvas
        {
            Width = 1200f,
            Height = 520f
        };
        host.AddChild(view);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 1200, 520);

        var dataGrid = FindFirstVisualChild<DataGrid>(view);
        Assert.NotNull(dataGrid);

        var nameHeader = FindFirstVisualChild<DataGridColumnHeader>(dataGrid!, static header => header.Text == "Name");
        Assert.NotNull(nameHeader);

        Click(uiRoot, GetCenter(nameHeader!.LayoutSlot));
        RunLayout(uiRoot, 1200, 520);

        var firstRow = dataGrid.RowsForTesting[0];
        var firstCell = firstRow.Cells[0];
        var contentHeight = firstRow.LayoutSlot.Height;

        Assert.Equal(contentHeight, firstCell.LayoutSlot.Height, 0.5f);
    }

    [Fact]
    public void DataGridSample_ScrollingToBottom_ShouldNotLeaveBlankSpaceAfterLastRow()
    {
        var element = ControlDemoSupport.BuildSampleElement("DataGrid");
        var dataGrid = Assert.IsType<DataGrid>(element);

        var host = new Canvas
        {
            Width = 900f,
            Height = 500f
        };
        host.AddChild(dataGrid);
        Canvas.SetLeft(dataGrid, 20f);
        Canvas.SetTop(dataGrid, 20f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 900, 500);

        var scrollViewer = dataGrid.ScrollViewerForTesting;
        scrollViewer.ScrollToVerticalOffset(10000f);
        RunLayout(uiRoot, 900, 500);

        var rows = dataGrid.RowsForTesting;
        Assert.NotEmpty(rows);
        var lastRow = rows[^1];
        var viewportBottom = scrollViewer.LayoutSlot.Y + scrollViewer.ViewportHeight;
        var lastRowBottom = lastRow.LayoutSlot.Y + lastRow.LayoutSlot.Height;
        var maxVerticalOffset = MathF.Max(0f, scrollViewer.ExtentHeight - scrollViewer.ViewportHeight);

        Assert.True(
            MathF.Abs(scrollViewer.VerticalOffset - maxVerticalOffset) <= 0.5f,
            $"Expected bottom-clamped vertical offset. Offset={scrollViewer.VerticalOffset}, Max={maxVerticalOffset}, Extent={scrollViewer.ExtentHeight}, Viewport={scrollViewer.ViewportHeight}.");
        Assert.True(
            MathF.Abs(lastRowBottom - viewportBottom) <= 0.5f,
            $"Expected last row to align with viewport bottom. LastRowBottom={lastRowBottom}, ViewportBottom={viewportBottom}, Offset={scrollViewer.VerticalOffset}, Extent={scrollViewer.ExtentHeight}, Viewport={scrollViewer.ViewportHeight}.");
    }

    [Fact]
    public void DataGridView_ScrollingToBottom_ShouldNotLeaveBlankSpaceAfterLastRow()
    {
        var view = new DataGridView
        {
            Width = 1200f,
            Height = 520f
        };

        var host = new Canvas
        {
            Width = 1200f,
            Height = 520f
        };
        host.AddChild(view);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 1200, 520);

        var dataGrid = FindFirstVisualChild<DataGrid>(view);
        Assert.NotNull(dataGrid);

        var scrollViewer = dataGrid!.ScrollViewerForTesting;
        scrollViewer.ScrollToVerticalOffset(10000f);
        RunLayout(uiRoot, 1200, 520);

        var lastRow = dataGrid.RowsForTesting[^1];
        var viewportBottom = scrollViewer.LayoutSlot.Y + scrollViewer.ViewportHeight;
        var lastRowBottom = lastRow.LayoutSlot.Y + lastRow.LayoutSlot.Height;
        var maxVerticalOffset = MathF.Max(0f, scrollViewer.ExtentHeight - scrollViewer.ViewportHeight);

        Assert.True(
            MathF.Abs(scrollViewer.VerticalOffset - maxVerticalOffset) <= 0.5f,
            $"Expected bottom-clamped vertical offset. Offset={scrollViewer.VerticalOffset}, Max={maxVerticalOffset}, Extent={scrollViewer.ExtentHeight}, Viewport={scrollViewer.ViewportHeight}.");
        Assert.True(
            MathF.Abs(lastRowBottom - viewportBottom) <= 0.5f,
            $"Expected last row to align with viewport bottom. LastRowBottom={lastRowBottom}, ViewportBottom={viewportBottom}, Offset={scrollViewer.VerticalOffset}, Extent={scrollViewer.ExtentHeight}, Viewport={scrollViewer.ViewportHeight}.");
    }

    [Fact]
    public void DataGridView_WheelScrollingToBottom_ShouldNotLeaveBlankSpaceAfterLastRow()
    {
        var view = new DataGridView
        {
            Width = 1200f,
            Height = 520f
        };

        var host = new Canvas
        {
            Width = 1200f,
            Height = 520f
        };
        host.AddChild(view);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 1200, 520);

        var dataGrid = FindFirstVisualChild<DataGrid>(view);
        Assert.NotNull(dataGrid);

        var scrollViewer = dataGrid!.ScrollViewerForTesting;
        var pointer = new Vector2(
            scrollViewer.LayoutSlot.X + (scrollViewer.LayoutSlot.Width * 0.5f),
            scrollViewer.LayoutSlot.Y + (scrollViewer.LayoutSlot.Height * 0.5f));

        for (var i = 0; i < 40; i++)
        {
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: i == 0, wheelDelta: -120));
            RunLayout(uiRoot, 1200, 520);
        }

        var lastRow = dataGrid.RowsForTesting[^1];
        var viewportBottom = scrollViewer.LayoutSlot.Y + scrollViewer.ViewportHeight;
        var lastRowBottom = lastRow.LayoutSlot.Y + lastRow.LayoutSlot.Height;
        var maxVerticalOffset = MathF.Max(0f, scrollViewer.ExtentHeight - scrollViewer.ViewportHeight);

        Assert.True(
            MathF.Abs(scrollViewer.VerticalOffset - maxVerticalOffset) <= 0.5f,
            $"Expected bottom-clamped vertical offset after wheel scroll. Offset={scrollViewer.VerticalOffset}, Max={maxVerticalOffset}, Extent={scrollViewer.ExtentHeight}, Viewport={scrollViewer.ViewportHeight}.");
        Assert.True(
            MathF.Abs(lastRowBottom - viewportBottom) <= 0.5f,
            $"Expected last row to align with viewport bottom after wheel scroll. LastRowBottom={lastRowBottom}, ViewportBottom={viewportBottom}, Offset={scrollViewer.VerticalOffset}, Extent={scrollViewer.ExtentHeight}, Viewport={scrollViewer.ViewportHeight}.");
    }

    [Fact]
    public void DataGridView_DraggingVerticalScrollBarToBottom_ShouldNotLeaveBlankSpaceAfterLastRow()
    {
        var view = new DataGridView
        {
            Width = 1200f,
            Height = 520f
        };

        var host = new Canvas
        {
            Width = 1200f,
            Height = 520f
        };
        host.AddChild(view);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 1200, 520);

        var dataGrid = FindFirstVisualChild<DataGrid>(view);
        Assert.NotNull(dataGrid);

        var scrollViewer = dataGrid!.ScrollViewerForTesting;
        var verticalBar = GetPrivateScrollBar(scrollViewer, "_verticalBar");
        var thumb = verticalBar.GetThumbRectForInput();
        var thumbCenter = GetCenter(thumb);
        var bottomPointer = new Vector2(
            thumbCenter.X,
            verticalBar.LayoutSlot.Y + verticalBar.LayoutSlot.Height - 2f);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(thumbCenter, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(thumbCenter, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(bottomPointer, pointerMoved: true));
        RunLayout(uiRoot, 1200, 520);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(bottomPointer, leftReleased: true));
        RunLayout(uiRoot, 1200, 520);

        var lastRow = dataGrid.RowsForTesting[^1];
        var viewportBottom = scrollViewer.LayoutSlot.Y + scrollViewer.ViewportHeight;
        var lastRowBottom = lastRow.LayoutSlot.Y + lastRow.LayoutSlot.Height;
        var maxVerticalOffset = MathF.Max(0f, scrollViewer.ExtentHeight - scrollViewer.ViewportHeight);

        Assert.True(
            MathF.Abs(scrollViewer.VerticalOffset - maxVerticalOffset) <= 0.5f,
            $"Expected bottom-clamped vertical offset after scrollbar drag. Offset={scrollViewer.VerticalOffset}, Max={maxVerticalOffset}, Extent={scrollViewer.ExtentHeight}, Viewport={scrollViewer.ViewportHeight}.");
        Assert.True(
            MathF.Abs(lastRowBottom - viewportBottom) <= 0.5f,
            $"Expected last row to align with viewport bottom after scrollbar drag. LastRowBottom={lastRowBottom}, ViewportBottom={viewportBottom}, Offset={scrollViewer.VerticalOffset}, Extent={scrollViewer.ExtentHeight}, Viewport={scrollViewer.ViewportHeight}.");
    }

    private static TElement? FindFirstVisualChild<TElement>(UIElement root)
        where TElement : UIElement
    {
        return FindFirstVisualChild<TElement>(root, static _ => true);
    }

    private static TElement? FindFirstVisualChild<TElement>(UIElement root, Func<TElement, bool> predicate)
        where TElement : UIElement
    {
        if (root is TElement match && predicate(match))
        {
            return match;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindFirstVisualChild(child, predicate);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, width, height));
    }

    private static void Click(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));
    }

    private static InputDelta CreatePointerDelta(
        Vector2 pointer,
        bool pointerMoved = false,
        int wheelDelta = 0,
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
            WheelDelta = wheelDelta,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width * 0.5f), rect.Y + (rect.Height * 0.5f));
    }

    private static TElement? FindAncestor<TElement>(UIElement? start)
        where TElement : UIElement
    {
        for (var current = start; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is TElement match)
            {
                return match;
            }
        }

        return null;
    }

    private static ScrollBar GetPrivateScrollBar(ScrollViewer viewer, string fieldName)
    {
        var field = typeof(ScrollViewer).GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<ScrollBar>(field!.GetValue(viewer));
    }
}
