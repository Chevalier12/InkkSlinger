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
        Assert.Equal("101", firstRow.Cells[0].Value?.ToString());
        Assert.Equal("Alpha", firstRow.Cells[1].Value?.ToString());
        Assert.Equal("Navigation", firstRow.Cells[2].Value?.ToString());
        Assert.Equal("High", firstRow.Cells[3].Value?.ToString());
        Assert.Equal("Active", firstRow.Cells[4].Value?.ToString());

        var lastRow = rows.Last();
        Assert.Equal("142", lastRow.Cells[0].Value?.ToString());
        Assert.Equal("Lima", lastRow.Cells[1].Value?.ToString());
        Assert.Equal("Diagnostics", lastRow.Cells[2].Value?.ToString());
        Assert.Equal("Planned", lastRow.Cells[4].Value?.ToString());
        Assert.DoesNotContain(
            uiRoot.GetRetainedVisualOrderForTests(),
            static visual => visual is Label label &&
                             string.Equals(label.GetContentText(), "InkkSlinger.ControlDemoSupport+DemoRow", StringComparison.Ordinal));
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

        var nameHeader = FindFirstVisualChild<DataGridColumnHeader>(dataGrid, static header => header.GetContentText() == "Name");
        var ticketHeader = FindFirstVisualChild<DataGridColumnHeader>(dataGrid, static header => header.GetContentText() == "Ticket");
        Assert.NotNull(nameHeader);
        Assert.NotNull(ticketHeader);

        Click(uiRoot, GetCenter(nameHeader!.LayoutSlot));
        RunLayout(uiRoot, 900, 500);
        Assert.Equal(DataGridSortDirection.Ascending, nameHeader.SortDirection);
        Assert.Equal("Alpha", dataGrid.RowsForTesting[0].Cells[1].Value?.ToString());

        Click(uiRoot, GetCenter(ticketHeader!.LayoutSlot));
        RunLayout(uiRoot, 900, 500);
        Assert.Equal(DataGridSortDirection.None, nameHeader.SortDirection);
        Assert.Equal(DataGridSortDirection.Ascending, ticketHeader.SortDirection);
        Assert.Equal("101", dataGrid.RowsForTesting[0].Cells[0].Value?.ToString());

        Click(uiRoot, GetCenter(ticketHeader.LayoutSlot));
        RunLayout(uiRoot, 900, 500);
        Assert.Equal(DataGridSortDirection.Descending, ticketHeader.SortDirection);
        Assert.Equal("142", dataGrid.RowsForTesting[0].Cells[0].Value?.ToString());
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

        var nameHeader = FindFirstVisualChild<DataGridColumnHeader>(dataGrid, static header => header.GetContentText() == "Name");
        Assert.NotNull(nameHeader);

        Click(uiRoot, GetCenter(nameHeader!.LayoutSlot));
        RunLayout(uiRoot, 900, 500);
        Click(uiRoot, GetCenter(nameHeader.LayoutSlot));
        RunLayout(uiRoot, 900, 500);

        Assert.Equal(DataGridSortDirection.Descending, nameHeader.SortDirection);

        var topVisibleRow = dataGrid.RowsForTesting
            .OrderBy(static row => row.LayoutSlot.Y)
            .First();

        Assert.Equal("142", topVisibleRow.Cells[0].Value?.ToString());
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

        var nameHeader = FindFirstVisualChild<DataGridColumnHeader>(dataGrid, static header => header.GetContentText() == "Name");
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

        var nameHeader = FindFirstVisualChild<DataGridColumnHeader>(dataGrid!, static header => header.GetContentText() == "Name");
        Assert.NotNull(nameHeader);

        Click(uiRoot, GetCenter(nameHeader!.LayoutSlot));
        RunLayout(uiRoot, 1200, 520);

        var firstRow = dataGrid.RowsForTesting[0];
        var firstCell = firstRow.Cells[0];
        var contentHeight = firstRow.LayoutSlot.Height;

        Assert.Equal(contentHeight, firstCell.LayoutSlot.Height, 0.5f);
    }

    [Fact]
    public void DataGridView_UsesRicherInteractiveConfigurationThanRawSample()
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
    Assert.Null(FindFirstVisualChild<ContentControl>(view, static contentControl => string.Equals(contentControl.Name, "DemoHost", StringComparison.Ordinal)));

        Assert.Equal(DataGridHeadersVisibility.All, dataGrid!.HeadersVisibility);
        Assert.Equal(DataGridSelectionMode.Extended, dataGrid.SelectionMode);
        Assert.Equal(DataGridSelectionUnit.FullRow, dataGrid.SelectionUnit);
        Assert.Equal(DataGridClipboardCopyMode.IncludeHeader, dataGrid.ClipboardCopyMode);
        Assert.True(dataGrid.CanUserReorderColumns);
        Assert.Equal(1, dataGrid.FrozenColumnCount);

        Click(uiRoot, GetCenter(dataGrid.RowsForTesting[0].RowHeaderForTesting.LayoutSlot));
        RunLayout(uiRoot, 1200, 520);

        Assert.True(dataGrid.RowsForTesting[0].DetailsPresenterForTesting.IsVisibleDetails);
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

    [Fact]
    public void DataGridView_DraggingHorizontalScrollBar_KeepsFrozenLanesAligned()
    {
        var view = new DataGridView
        {
            Width = 780f,
            Height = 520f
        };

        var host = new Canvas
        {
            Width = 780f,
            Height = 520f
        };
        host.AddChild(view);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 780, 520);

        var dataGrid = FindFirstVisualChild<DataGrid>(view);
        Assert.NotNull(dataGrid);

        var scrollViewer = dataGrid!.ScrollViewerForTesting;
        Assert.True(scrollViewer.ExtentWidth > scrollViewer.ViewportWidth);

        var row = dataGrid.RowsForTesting[0];
        var rowHeaderX = row.RowHeaderForTesting.LayoutSlot.X;
        var frozenHeaderX = dataGrid.ColumnHeadersForTesting[0].LayoutSlot.X;
        var frozenCellX = row.Cells[0].LayoutSlot.X;

        var horizontalBar = GetPrivateScrollBar(scrollViewer, "_horizontalBar");
        var thumb = FindFirstVisualChild<Thumb>(horizontalBar);
        Assert.NotNull(thumb);

        var thumbCenter = GetCenter(thumb!.LayoutSlot);
        Assert.True(
            thumbCenter.X >= horizontalBar.LayoutSlot.X &&
            thumbCenter.X <= horizontalBar.LayoutSlot.X + horizontalBar.LayoutSlot.Width &&
            thumbCenter.Y >= horizontalBar.LayoutSlot.Y &&
            thumbCenter.Y <= horizontalBar.LayoutSlot.Y + horizontalBar.LayoutSlot.Height,
            $"Expected horizontal thumb center to stay within the horizontal scrollbar bounds. Thumb={thumb.LayoutSlot}, CachedThumb={horizontalBar.GetThumbRectForInput()}, ScrollBar={horizontalBar.LayoutSlot}.");
        var rightPointer = new Vector2(horizontalBar.LayoutSlot.X + horizontalBar.LayoutSlot.Width - 2f, thumbCenter.Y);

        Assert.True(thumb.HandlePointerDownFromInput(thumbCenter));
        Assert.True(thumb.HandlePointerMoveFromInput(rightPointer));
        RunLayout(uiRoot, 780, 520);
        Assert.True(thumb.HandlePointerUpFromInput());
        RunLayout(uiRoot, 780, 520);

        Assert.True(scrollViewer.HorizontalOffset > 0f);
        Assert.Equal(rowHeaderX, row.RowHeaderForTesting.LayoutSlot.X);
        Assert.Equal(frozenHeaderX, dataGrid.ColumnHeadersForTesting[0].LayoutSlot.X);
        Assert.Equal(frozenCellX, row.Cells[0].LayoutSlot.X);
        Assert.Equal(dataGrid.ColumnHeadersForTesting[1].LayoutSlot.X, row.Cells[1].LayoutSlot.X);
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
