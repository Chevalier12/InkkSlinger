using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class DataGridParityChecklistTests
{
    [Fact]
    public void FullRowSelection_ClickAndTab_KeepsCurrentCellIndependentFromSelection()
    {
        var (grid, uiRoot) = CreateGridHost();
        RunLayout(uiRoot);

        var secondRowSecondCell = grid.RowsForTesting[1].Cells[1];
        Click(uiRoot, GetCenter(secondRowSecondCell.LayoutSlot));

        Assert.Equal(1, grid.SelectedRowIndex);
        Assert.Equal(-1, grid.SelectedColumnIndex);
        Assert.Equal(1, grid.CurrentRowIndexForTesting);
        Assert.Equal(1, grid.CurrentColumnIndexForTesting);

        PressKey(uiRoot, Keys.Tab);

        Assert.Equal(2, grid.SelectedRowIndex);
        Assert.Equal(-1, grid.SelectedColumnIndex);
        Assert.Equal(2, grid.CurrentRowIndexForTesting);
        Assert.Equal(0, grid.CurrentColumnIndexForTesting);
    }

    [Fact]
    public void F2EditAndEnter_CommitsCurrentCellValue()
    {
        var (grid, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        RunLayout(uiRoot);

        var firstCell = grid.RowsForTesting[0].Cells[0];
        Click(uiRoot, GetCenter(firstCell.LayoutSlot));
        PressKey(uiRoot, Keys.F2);

        Assert.Equal(0, grid.EditingRowIndexForTesting);
        Assert.Equal(0, grid.EditingColumnIndexForTesting);

        var editor = Assert.IsType<TextBox>(firstCell.EditingElement);
        editor.Text = "42";
        PressKey(uiRoot, Keys.Enter);

        Assert.Null(firstCell.EditingElement);
        Assert.Equal("42", firstCell.Content?.ToString());
        Assert.Equal("42", firstCell.Value?.ToString());
    }

    [Fact]
    public void EscapeWhileEditing_RestoresOriginalValue()
    {
        var (grid, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        RunLayout(uiRoot);

        var firstCell = grid.RowsForTesting[0].Cells[0];
        Click(uiRoot, GetCenter(firstCell.LayoutSlot));
        PressKey(uiRoot, Keys.F2);

        var editor = Assert.IsType<TextBox>(firstCell.EditingElement);
        editor.Text = "99";
        PressKey(uiRoot, Keys.Escape);

        Assert.Null(firstCell.EditingElement);
        Assert.Equal("1", firstCell.Content?.ToString());
    }

    [Fact]
    public void VisibleWhenSelected_RowDetailsFollowSelection()
    {
        var (grid, uiRoot) = CreateGridHost();
        grid.RowDetailsTemplate = new DataTemplate(static item => new Label { Text = $"details:{item}" });
        grid.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.VisibleWhenSelected;
        RunLayout(uiRoot);

        Assert.False(grid.RowsForTesting[0].DetailsPresenterForTesting.IsVisibleDetails);
        Click(uiRoot, GetCenter(grid.RowsForTesting[1].Cells[0].LayoutSlot));

        Assert.False(grid.RowsForTesting[0].DetailsPresenterForTesting.IsVisibleDetails);
        Assert.True(grid.RowsForTesting[1].DetailsPresenterForTesting.IsVisibleDetails);
    }

    [Fact]
    public void HeaderResizeAndReorder_UpdateProjectedColumns()
    {
        var (grid, uiRoot) = CreateGridHost();
        grid.CanUserReorderColumns = true;
        RunLayout(uiRoot);

        var firstHeader = grid.ColumnHeadersForTesting[0];
        var secondHeader = grid.ColumnHeadersForTesting[1];
        var originalWidth = grid.DisplayColumnsForTesting[0].Width;

        var resizeStart = new Vector2(firstHeader.LayoutSlot.X + firstHeader.LayoutSlot.Width - 1f, firstHeader.LayoutSlot.Y + (firstHeader.LayoutSlot.Height / 2f));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(resizeStart, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(resizeStart, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(resizeStart.X + 40f, resizeStart.Y), pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(resizeStart.X + 40f, resizeStart.Y), leftReleased: true));
        RunLayout(uiRoot);

        Assert.True(grid.DisplayColumnsForTesting[0].Width > originalWidth);

        var dragStart = GetCenter(secondHeader.LayoutSlot);
        var dragTarget = GetCenter(firstHeader.LayoutSlot);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(dragStart, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(dragStart, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(dragStart.X - 60f, dragStart.Y), pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(dragTarget, leftReleased: true));
        RunLayout(uiRoot);

        Assert.Equal("Name", grid.ColumnHeadersForTesting[0].Text);
        Assert.Equal("Alpha", grid.RowsForTesting[0].Cells[0].Content?.ToString());
    }

    [Fact]
    public void FrozenColumns_StayStationaryDuringHorizontalScroll()
    {
        var (grid, uiRoot) = CreateGridHost(width: 260f);
        grid.Columns.Clear();
        grid.Columns.Add(new DataGridColumn { Header = "Id", BindingPath = nameof(Row.Id), Width = 120f });
        grid.Columns.Add(new DataGridColumn { Header = "Name", BindingPath = nameof(Row.Name), Width = 120f });
        grid.Columns.Add(new DataGridColumn { Header = "City", BindingPath = nameof(Row.City), Width = 120f });
        grid.FrozenColumnCount = 1;
        RunLayout(uiRoot, width: 260, height: 300);

        var row = grid.RowsForTesting[0];
        var frozenX = row.Cells[0].LayoutSlot.X;
        var scrollingX = row.Cells[1].LayoutSlot.X;

        grid.ScrollViewerForTesting.ScrollToHorizontalOffset(60f);
        RunLayout(uiRoot, width: 260, height: 300);

        Assert.Equal(frozenX, row.Cells[0].LayoutSlot.X);
        Assert.True(row.Cells[1].LayoutSlot.X < scrollingX);
    }

    [Fact]
    public void RepeatedPointerPressOnCurrentCell_BeginsEdit()
    {
        var (grid, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        RunLayout(uiRoot);

        var cell = grid.RowsForTesting[0].Cells[0];
        var point = GetCenter(cell.LayoutSlot);
        Click(uiRoot, point);
        Click(uiRoot, point);

        Assert.NotNull(cell.EditingElement);
        Assert.Equal(0, grid.EditingRowIndexForTesting);
        Assert.Equal(0, grid.EditingColumnIndexForTesting);
    }

    [Fact]
    public void InvalidTextCommit_PreservesEditModeAndOriginalValue()
    {
        var (grid, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        RunLayout(uiRoot);

        var cell = grid.RowsForTesting[0].Cells[0];
        Click(uiRoot, GetCenter(cell.LayoutSlot));
        PressKey(uiRoot, Keys.F2);
        var editor = Assert.IsType<TextBox>(cell.EditingElement);
        editor.Text = "abc";

        PressKey(uiRoot, Keys.Enter);

        Assert.Same(editor, cell.EditingElement);
        Assert.Equal("1", cell.Content?.ToString());
        Assert.True(Validation.GetHasError(editor));
    }

    [Fact]
    public void BindingGroupFailure_RollsBackEditedSourceValue()
    {
        var (grid, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        var source = (ObservableCollection<Row>)grid.ItemsSource!;
        grid.BindingGroup = new BindingGroup();
        grid.BindingGroup.ValidationRules.Add(new AlwaysInvalidRule());
        RunLayout(uiRoot);

        var cell = grid.RowsForTesting[0].Cells[0];
        Click(uiRoot, GetCenter(cell.LayoutSlot));
        PressKey(uiRoot, Keys.F2);
        var editor = Assert.IsType<TextBox>(cell.EditingElement);
        editor.Text = "42";

        PressKey(uiRoot, Keys.Enter);

        Assert.Same(editor, cell.EditingElement);
        Assert.Equal(1, source[0].Id);
        Assert.Equal("1", cell.Content?.ToString());
    }

    [Fact]
    public void TextInput_StartsEdit_OnlyAppendsCharacterOnce()
    {
        var (grid, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        var source = (ObservableCollection<Row>)grid.ItemsSource!;
        source[0].Name = string.Empty;
        RunLayout(uiRoot);

        var cell = grid.RowsForTesting[0].Cells[1];
        Click(uiRoot, GetCenter(cell.LayoutSlot));

        TypeCharacter(uiRoot, '7');

        var editor = Assert.IsType<TextBox>(cell.EditingElement);
        Assert.Equal("7", editor.Text);
    }

    [Fact]
    public void ClickingInsideInlineEditor_KeepsEditSessionActiveAndGridFocused()
    {
        var (grid, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        RunLayout(uiRoot);

        var cell = grid.RowsForTesting[0].Cells[0];
        Click(uiRoot, GetCenter(cell.LayoutSlot));
        PressKey(uiRoot, Keys.F2);
        RunLayout(uiRoot);

        var editor = Assert.IsType<TextBox>(cell.EditingElement);
        Click(uiRoot, GetCenter(editor.LayoutSlot));

        Assert.Same(editor, cell.EditingElement);
        Assert.Same(grid, FocusManager.GetFocusedElement());
    }

    [Fact]
    public void BeginningEdit_KeepsInlineEditorFontMetricsAlignedWithCell()
    {
        var (grid, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        grid.FontSize = 11f;
        RunLayout(uiRoot);

        var cell = grid.RowsForTesting[0].Cells[1];
        Click(uiRoot, GetCenter(cell.LayoutSlot));
        PressKey(uiRoot, Keys.F2);

        var editor = Assert.IsType<TextBox>(cell.EditingElement);
        Assert.Equal(cell.FontSize, editor.FontSize);
        Assert.Equal(cell.FontFamily, editor.FontFamily);
        Assert.Equal(cell.FontWeight, editor.FontWeight);
        Assert.Equal(cell.FontStyle, editor.FontStyle);
    }

    [Fact]
    public void DisablingRowVirtualizationAfterLayout_RealizesAllRows()
    {
        var (grid, uiRoot) = CreateGridHost(itemCount: 40, height: 80f);
        RunLayout(uiRoot, height: 160);

        Assert.True(grid.RealizedRowCountForTesting < 40);

        grid.EnableRowVirtualization = false;
        RunLayout(uiRoot, height: 160);

        Assert.Equal(40, grid.RealizedRowCountForTesting);
    }

    [Fact]
    public void ClickingVisibleCellInScrolledGrid_DoesNotChangeVerticalOffset()
    {
        var (grid, uiRoot) = CreateGridHost(itemCount: 40, height: 140f);
        RunLayout(uiRoot, height: 220);

        grid.ScrollViewerForTesting.ScrollToVerticalOffset(200f);
        RunLayout(uiRoot, height: 220);

        var offsetBeforeClick = grid.ScrollViewerForTesting.VerticalOffset;
        var clickedRow = grid.RowsForTesting[10];
        Click(uiRoot, GetCenter(clickedRow.Cells[0].LayoutSlot));
        RunLayout(uiRoot, height: 220);

        Assert.True(
            MathF.Abs(grid.ScrollViewerForTesting.VerticalOffset - offsetBeforeClick) <= 0.01f,
            $"Expected click on a visible cell to preserve vertical offset. before={offsetBeforeClick:0.###}, after={grid.ScrollViewerForTesting.VerticalOffset:0.###}");
    }

    [Fact]
    public void ArrowNavigation_BackToTop_AdjustsVerticalOffsetContinuously()
    {
        var (grid, uiRoot) = CreateGridHost(itemCount: 40, height: 140f);
        RunLayout(uiRoot, height: 220);

        Click(uiRoot, GetCenter(grid.RowsForTesting[0].Cells[0].LayoutSlot));

        for (var i = 0; i < 39; i++)
        {
            PressKey(uiRoot, Keys.Down);
            RunLayout(uiRoot, height: 220);
        }

        Assert.Equal(39, grid.CurrentRowIndexForTesting);
        Assert.True(grid.ScrollViewerForTesting.VerticalOffset > 0f);

        var previousOffset = grid.ScrollViewerForTesting.VerticalOffset;
        var observedUpwardScrollAdjustment = false;
        for (var i = 0; i < 39; i++)
        {
            PressKey(uiRoot, Keys.Up);
            RunLayout(uiRoot, height: 220);

            var currentOffset = grid.ScrollViewerForTesting.VerticalOffset;
            if (currentOffset < previousOffset - 0.01f)
            {
                observedUpwardScrollAdjustment = true;
            }

            previousOffset = currentOffset;
        }

        Assert.True(observedUpwardScrollAdjustment, "Expected at least one upward navigation step to reduce the vertical offset before reaching the first few rows.");
        Assert.Equal(0, grid.CurrentRowIndexForTesting);
        Assert.True(grid.ScrollViewerForTesting.VerticalOffset <= 0.01f, $"Expected to return to the top after navigating upward. actual={grid.ScrollViewerForTesting.VerticalOffset:0.###}");
    }

    private static (DataGrid Grid, UiRoot UiRoot) CreateGridHost(
        DataGridSelectionUnit selectionUnit = DataGridSelectionUnit.FullRow,
        float width = 500f,
        float height = 260f,
        int itemCount = 3)
    {
        var source = new ObservableCollection<Row>();
        var defaultRows = new[]
        {
            new Row(1, "Alpha", "Rome"),
            new Row(2, "Bravo", "Paris"),
            new Row(3, "Charlie", "Berlin")
        };
        for (var i = 0; i < itemCount; i++)
        {
            source.Add(i < defaultRows.Length
                ? defaultRows[i]
                : new Row(i + 1, $"Name{i + 1}", $"City{i + 1}"));
        }

        var grid = new DataGrid
        {
            ItemsSource = source,
            Width = width,
            Height = height,
            SelectionUnit = selectionUnit
        };
        grid.Columns.Add(new DataGridColumn { Header = "Id", BindingPath = nameof(Row.Id), Width = 120f });
        grid.Columns.Add(new DataGridColumn { Header = "Name", BindingPath = nameof(Row.Name), Width = 120f });

        var host = new Canvas { Width = width + 40f, Height = 320f };
        host.AddChild(grid);
        Canvas.SetLeft(grid, 10f);
        Canvas.SetTop(grid, 10f);

        return (grid, new UiRoot(host));
    }

    private static void RunLayout(UiRoot uiRoot, int width = 640, int height = 360)
    {
        uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)), new Viewport(0, 0, width, height));
    }

    private static void Click(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));
    }

    private static void PressKey(UiRoot uiRoot, Keys key)
    {
        uiRoot.RunInputDeltaForTests(new InputDelta
        {
            Previous = new InputSnapshot(default, default, Vector2.Zero),
            Current = new InputSnapshot(default, default, Vector2.Zero),
            PressedKeys = [key],
            ReleasedKeys = [],
            TextInput = [],
            PointerMoved = false,
            WheelDelta = 0,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        });
    }

    private static void TypeCharacter(UiRoot uiRoot, char character)
    {
        uiRoot.RunInputDeltaForTests(new InputDelta
        {
            Previous = new InputSnapshot(default, default, Vector2.Zero),
            Current = new InputSnapshot(default, default, Vector2.Zero),
            PressedKeys = [],
            ReleasedKeys = [],
            TextInput = [character],
            PointerMoved = false,
            WheelDelta = 0,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        });
    }

    private static InputDelta CreatePointerDelta(Vector2 pointer, bool pointerMoved = false, bool leftPressed = false, bool leftReleased = false)
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

    private static Vector2 GetCenter(LayoutRect rect) => new(rect.X + (rect.Width * 0.5f), rect.Y + (rect.Height * 0.5f));

    private sealed class AlwaysInvalidRule : ValidationRule
    {
        public override ValidationResult Validate(object? value, System.Globalization.CultureInfo cultureInfo)
        {
            _ = value;
            _ = cultureInfo;
            return new ValidationResult(false, "Invalid");
        }
    }

    private sealed class Row
    {
        public Row(int id, string name, string city)
        {
            Id = id;
            Name = name;
            City = city;
        }

        public int Id { get; set; }

        public string Name { get; set; }

        public string City { get; set; }
    }
}
