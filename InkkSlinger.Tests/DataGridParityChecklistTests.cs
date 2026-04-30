using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
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
    public void RowSelection_SyncsSelectorSelectedItemSurface()
    {
        var (grid, uiRoot) = CreateGridHost();
        RunLayout(uiRoot);

        Click(uiRoot, GetCenter(grid.RowsForTesting[1].Cells[1].LayoutSlot));

        Assert.Equal(1, grid.SelectedIndex);
        var selectedItem = Assert.IsType<Row>(grid.SelectedItem);
        Assert.Equal(2, selectedItem.Id);
        Assert.Single(grid.SelectedItems);
        Assert.Same(grid.SelectedItem, grid.SelectedItems[0]);
    }

    [Fact]
    public void ProgrammaticSelectorSelection_UpdatesDataGridSelectionState()
    {
        var (grid, uiRoot) = CreateGridHost();
        RunLayout(uiRoot);

        grid.SelectedIndex = 2;

        Assert.Equal(2, grid.SelectedRowIndex);
        Assert.Equal(2, grid.CurrentRowIndexForTesting);
        Assert.Equal(0, grid.CurrentColumnIndexForTesting);
        Assert.IsType<Row>(grid.SelectedItem);
        Assert.Equal(grid.SelectedItem, grid.CurrentItem);
        Assert.Same(grid.Columns[0], grid.CurrentColumn);
    }

    [Fact]
    public void DataGridSelectionMode_MapsToSelectorSelectionMode()
    {
        var grid = new DataGrid();

        grid.SelectionMode = DataGridSelectionMode.Extended;

        Assert.Equal(DataGridSelectionMode.Extended, grid.SelectionMode);
        Assert.Equal(SelectionMode.Extended, ((Selector)grid).SelectionMode);
    }

    [Fact]
    public void ExtendedSelection_CtrlClick_AddsMultipleRows()
    {
        var (grid, uiRoot) = CreateGridHost();
        grid.SelectionMode = DataGridSelectionMode.Extended;
        RunLayout(uiRoot);

        Click(uiRoot, GetCenter(grid.RowsForTesting[0].Cells[0].LayoutSlot));
        Click(uiRoot, GetCenter(grid.RowsForTesting[2].Cells[0].LayoutSlot), ModifierKeys.Control);

        Assert.Equal(2, grid.SelectedItems.Count);
        Assert.True(grid.RowsForTesting[0].IsSelected);
        Assert.True(grid.RowsForTesting[2].IsSelected);
        Assert.Equal(2, grid.CurrentRowIndexForTesting);
    }

    [Fact]
    public void ExtendedSelection_ShiftClick_ExtendsRangeFromAnchor()
    {
        var (grid, uiRoot) = CreateGridHost();
        grid.SelectionMode = DataGridSelectionMode.Extended;
        RunLayout(uiRoot);

        Click(uiRoot, GetCenter(grid.RowsForTesting[0].Cells[0].LayoutSlot));
        Click(uiRoot, GetCenter(grid.RowsForTesting[2].Cells[0].LayoutSlot), ModifierKeys.Shift);

        Assert.Equal(3, grid.SelectedItems.Count);
        Assert.True(grid.RowsForTesting[0].IsSelected);
        Assert.True(grid.RowsForTesting[1].IsSelected);
        Assert.True(grid.RowsForTesting[2].IsSelected);
    }

    [Fact]
    public void ExtendedSelection_ShiftDown_ExtendsRowSelectionFromCurrentAnchor()
    {
        var (grid, uiRoot) = CreateGridHost();
        grid.SelectionMode = DataGridSelectionMode.Extended;
        RunLayout(uiRoot);

        Click(uiRoot, GetCenter(grid.RowsForTesting[0].Cells[0].LayoutSlot));
        PressKey(uiRoot, Keys.Down, ModifierKeys.Shift);

        Assert.Equal(2, grid.SelectedItems.Count);
        Assert.True(grid.RowsForTesting[0].IsSelected);
        Assert.True(grid.RowsForTesting[1].IsSelected);
        Assert.Equal(1, grid.CurrentRowIndexForTesting);
    }

    [Fact]
    public void CellSelection_CtrlA_SelectsAllCells()
    {
        var (grid, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        grid.SelectionMode = DataGridSelectionMode.Extended;
        RunLayout(uiRoot);

        Click(uiRoot, GetCenter(grid.RowsForTesting[0].Cells[0].LayoutSlot));
        PressKey(uiRoot, Keys.A, ModifierKeys.Control);

        Assert.Equal(6, grid.SelectedCells.Count);
        Assert.Empty(grid.SelectedItems);
        Assert.Equal(-1, grid.SelectedIndex);
        Assert.All(grid.RowsForTesting, row => Assert.All(row.Cells, cell => Assert.True(cell.IsSelected)));
    }

    [Fact]
    public void SelectAllCellsAndUnselectAllCells_UpdateCellSelectionApiSurface()
    {
        var (grid, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        grid.SelectionMode = DataGridSelectionMode.Extended;
        RunLayout(uiRoot);

        grid.SelectAllCells();

        Assert.Equal(6, grid.SelectedCells.Count);
        Assert.Empty(grid.SelectedItems);
        Assert.Equal(-1, grid.SelectedIndex);

        grid.UnselectAllCells();

        Assert.Empty(grid.SelectedCells);
        Assert.Empty(grid.SelectedItems);
        Assert.Equal(-1, grid.SelectedIndex);
        Assert.Equal(-1, grid.SelectedRowIndex);
    }

    [Fact]
    public void CellSelection_CtrlShiftClickAfterSelectAllKeepsAllCellsSelected()
    {
        var (grid, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        grid.SelectionMode = DataGridSelectionMode.Extended;
        RunLayout(uiRoot);

        grid.SelectAllCells();
        Click(uiRoot, GetCenter(grid.RowsForTesting[1].Cells[1].LayoutSlot), ModifierKeys.Control | ModifierKeys.Shift);

        Assert.Equal(6, grid.SelectedCells.Count);
        Assert.All(grid.RowsForTesting, row => Assert.All(row.Cells, cell => Assert.True(cell.IsSelected)));
    }

    [Fact]
    public void InheritedSelectAll_SelectsRowsInCellSelectionMode()
    {
        var (grid, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        grid.SelectionMode = DataGridSelectionMode.Extended;
        RunLayout(uiRoot);

        grid.SelectAll();

        Assert.Equal(3, grid.SelectedItems.Count);
        Assert.Equal(6, grid.SelectedCells.Count);
        Assert.Equal(0, grid.SelectedIndex);

        grid.UnselectAll();

        Assert.Empty(grid.SelectedItems);
        Assert.Empty(grid.SelectedCells);
        Assert.Equal(-1, grid.SelectedIndex);
    }

    [Fact]
    public void InheritedSelectAll_WhenSelectionModeIsSingle_Throws()
    {
        var grid = new DataGrid();

        var exception = Assert.Throws<InvalidOperationException>(() => grid.SelectAll());

        Assert.Equal("SelectAll requires multi-selection mode.", exception.Message);
    }

    [Fact]
    public void FullRowSelection_CurrentCellAndSelectedCellsExposeRowAndClickedColumn()
    {
        var (grid, uiRoot) = CreateGridHost();
        RunLayout(uiRoot);

        Click(uiRoot, GetCenter(grid.RowsForTesting[1].Cells[1].LayoutSlot));

        Assert.True(grid.CurrentCell.IsValid);
        Assert.Same(grid.CurrentItem, grid.CurrentCell.Item);
        Assert.Same(grid.Columns[1], grid.CurrentCell.Column);
        Assert.Equal(2, grid.SelectedCells.Count);
        Assert.All(grid.SelectedCells, cell => Assert.Same(grid.CurrentItem, cell.Item));
        Assert.Contains(grid.SelectedCells, cell => ReferenceEquals(cell.Column, grid.Columns[0]));
        Assert.Contains(grid.SelectedCells, cell => ReferenceEquals(cell.Column, grid.Columns[1]));
    }

    [Fact]
    public void CellSelection_CurrentCellAndSelectedCellsExposeOnlyActiveCell()
    {
        var (grid, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        RunLayout(uiRoot);

        Click(uiRoot, GetCenter(grid.RowsForTesting[2].Cells[1].LayoutSlot));

        Assert.True(grid.CurrentCell.IsValid);
        Assert.Same(grid.CurrentItem, grid.CurrentCell.Item);
        Assert.Same(grid.Columns[1], grid.CurrentCell.Column);
        Assert.Empty(grid.SelectedItems);
        Assert.Equal(-1, grid.SelectedIndex);
        var selectedCell = Assert.Single(grid.SelectedCells);
        Assert.Equal(grid.CurrentCell, selectedCell);
    }

    [Fact]
    public void FullRowSelection_SortKeepsSelectedItemAndCurrentCellOnSameDataItem()
    {
        var (grid, uiRoot) = CreateGridHost();
        RunLayout(uiRoot);

        var selectedCell = grid.RowsForTesting[0].Cells[1];
        Click(uiRoot, GetCenter(selectedCell.LayoutSlot));
        var currentCell = grid.CurrentCell;
        var selectedItem = grid.SelectedItem;

        grid.ColumnHeadersForTesting[1].RaiseRoutedEventInternal(Button.ClickEvent, new RoutedSimpleEventArgs(Button.ClickEvent));
        grid.ColumnHeadersForTesting[1].RaiseRoutedEventInternal(Button.ClickEvent, new RoutedSimpleEventArgs(Button.ClickEvent));

        Assert.Same(selectedItem, grid.SelectedItem);
        Assert.Equal(2, grid.SelectedIndex);
        Assert.Equal(2, grid.CurrentRowIndexForTesting);
        Assert.Equal(1, grid.CurrentColumnIndexForTesting);
        Assert.Equal(currentCell, grid.CurrentCell);
    }

    [Fact]
    public void CellSelection_SortKeepsCurrentCellAndSelectedCellsOnSameDataItem()
    {
        var (grid, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        RunLayout(uiRoot);

        var selectedCell = grid.RowsForTesting[0].Cells[1];
        Click(uiRoot, GetCenter(selectedCell.LayoutSlot));
        var currentCell = grid.CurrentCell;

        grid.ColumnHeadersForTesting[1].RaiseRoutedEventInternal(Button.ClickEvent, new RoutedSimpleEventArgs(Button.ClickEvent));
        grid.ColumnHeadersForTesting[1].RaiseRoutedEventInternal(Button.ClickEvent, new RoutedSimpleEventArgs(Button.ClickEvent));

        Assert.True(grid.CurrentCell.IsValid);
        Assert.Equal(2, grid.CurrentRowIndexForTesting);
        Assert.Equal(1, grid.CurrentColumnIndexForTesting);
        Assert.Equal(grid.CurrentCell, Assert.Single(grid.SelectedCells));
        Assert.Equal(currentCell, grid.CurrentCell);
    }

    [Fact]
    public void CellSelection_CtrlClick_AddsMultipleCellsWithoutSelectingRows()
    {
        var (grid, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        grid.SelectionMode = DataGridSelectionMode.Extended;
        RunLayout(uiRoot);

        Click(uiRoot, GetCenter(grid.RowsForTesting[0].Cells[0].LayoutSlot));
        Click(uiRoot, GetCenter(grid.RowsForTesting[1].Cells[1].LayoutSlot), ModifierKeys.Control);

        Assert.Equal(2, grid.SelectedCells.Count);
        Assert.Empty(grid.SelectedItems);
        Assert.Equal(-1, grid.SelectedIndex);
        Assert.Contains(grid.SelectedCells, cell => ReferenceEquals(cell.Item, grid.RowsForTesting[0].Item) && ReferenceEquals(cell.Column, grid.Columns[0]));
        Assert.Contains(grid.SelectedCells, cell => ReferenceEquals(cell.Item, grid.RowsForTesting[1].Item) && ReferenceEquals(cell.Column, grid.Columns[1]));
    }

    [Fact]
    public void CellSelection_ShiftClick_SelectsRectangularRange()
    {
        var (grid, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        grid.SelectionMode = DataGridSelectionMode.Extended;
        RunLayout(uiRoot);

        Click(uiRoot, GetCenter(grid.RowsForTesting[0].Cells[0].LayoutSlot));
        Click(uiRoot, GetCenter(grid.RowsForTesting[1].Cells[1].LayoutSlot), ModifierKeys.Shift);

        Assert.Equal(4, grid.SelectedCells.Count);
        Assert.Empty(grid.SelectedItems);
        Assert.All(grid.RowsForTesting.Take(2), row => Assert.All(row.Cells, cell => Assert.True(cell.IsSelected)));
    }

    [Fact]
    public void ExtendedFullRowSelection_SelectedCellsSpanAllSelectedRows()
    {
        var (grid, uiRoot) = CreateGridHost();
        grid.SelectionMode = DataGridSelectionMode.Extended;
        RunLayout(uiRoot);

        Click(uiRoot, GetCenter(grid.RowsForTesting[0].Cells[0].LayoutSlot));
        Click(uiRoot, GetCenter(grid.RowsForTesting[2].Cells[0].LayoutSlot), ModifierKeys.Control);

        Assert.Equal(4, grid.SelectedCells.Count);
        Assert.Equal(2, grid.SelectedCells.Count(cell => ReferenceEquals(cell.Item, grid.RowsForTesting[0].Item)));
        Assert.Equal(2, grid.SelectedCells.Count(cell => ReferenceEquals(cell.Item, grid.RowsForTesting[2].Item)));
    }

    [Fact]
    public void CurrentCellChanged_RaisesWhenCurrentCellMoves()
    {
        var (grid, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        RunLayout(uiRoot);

        var changeCount = 0;
        grid.CurrentCellChanged += (_, _) => changeCount++;

        Click(uiRoot, GetCenter(grid.RowsForTesting[0].Cells[1].LayoutSlot));
        Click(uiRoot, GetCenter(grid.RowsForTesting[0].Cells[0].LayoutSlot));

        Assert.Equal(2, changeCount);
        Assert.Same(grid.Columns[0], grid.CurrentCell.Column);
    }

    [Fact]
    public void SelectedCellsChanged_RaisesAddedAndRemovedCellsForCellSelection()
    {
        var (grid, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        RunLayout(uiRoot);

        SelectedCellsChangedEventArgs? firstArgs = null;
        SelectedCellsChangedEventArgs? secondArgs = null;
        grid.SelectedCellsChanged += (_, args) =>
        {
            if (firstArgs == null)
            {
                firstArgs = args;
            }
            else
            {
                secondArgs = args;
            }
        };

        Click(uiRoot, GetCenter(grid.RowsForTesting[0].Cells[0].LayoutSlot));
        Click(uiRoot, GetCenter(grid.RowsForTesting[0].Cells[1].LayoutSlot));

        Assert.NotNull(firstArgs);
        Assert.Empty(firstArgs!.RemovedCells);
        Assert.Single(firstArgs.AddedCells);
        Assert.Same(grid.RowsForTesting[0].Item, firstArgs.AddedCells[0].Item);
        Assert.Same(grid.Columns[0], firstArgs.AddedCells[0].Column);

        Assert.NotNull(secondArgs);
        Assert.Single(secondArgs!.RemovedCells);
        Assert.Single(secondArgs.AddedCells);
        Assert.Same(grid.Columns[0], secondArgs.RemovedCells[0].Column);
        Assert.Same(grid.Columns[1], secondArgs.AddedCells[0].Column);
    }

    [Fact]
    public void SettingCurrentCell_UpdatesFullRowSelectionAndCurrentColumn()
    {
        var (grid, uiRoot) = CreateGridHost();
        RunLayout(uiRoot);

        var targetCell = new DataGridCellInfo(grid.RowsForTesting[2].Item, grid.Columns[1]);

        grid.CurrentCell = targetCell;

        Assert.Equal(2, grid.SelectedRowIndex);
        Assert.Equal(-1, grid.SelectedColumnIndex);
        Assert.Equal(2, grid.CurrentRowIndexForTesting);
        Assert.Equal(1, grid.CurrentColumnIndexForTesting);
        Assert.Equal(targetCell, grid.CurrentCell);
    }

    [Fact]
    public void SettingCurrentCell_UpdatesCellSelectionProjection()
    {
        var (grid, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        RunLayout(uiRoot);

        var targetCell = new DataGridCellInfo(grid.RowsForTesting[1].Item, grid.Columns[1]);

        grid.CurrentCell = targetCell;

        Assert.Equal(1, grid.SelectedRowIndex);
        Assert.Equal(1, grid.SelectedColumnIndex);
        Assert.Empty(grid.SelectedItems);
        Assert.Equal(-1, grid.SelectedIndex);
        Assert.Equal(targetCell, grid.CurrentCell);
        Assert.Equal(targetCell, Assert.Single(grid.SelectedCells));
    }

    [Fact]
    public void CommitEdit_CellUnitWithoutExit_KeepsEditorActiveAndUpdatesSource()
    {
        var (grid, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        var source = (ObservableCollection<Row>)grid.ItemsSource!;
        RunLayout(uiRoot);

        var cell = grid.RowsForTesting[0].Cells[0];
        Click(uiRoot, GetCenter(cell.LayoutSlot));
        PressKey(uiRoot, Keys.F2);

        var editor = Assert.IsType<TextBox>(cell.EditingElement);
        editor.Text = "42";

        Assert.True(grid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: false));

        Assert.Same(editor, cell.EditingElement);
        Assert.Equal(42, source[0].Id);
        Assert.Equal(0, grid.EditingRowIndexForTesting);
        Assert.Equal(0, grid.EditingColumnIndexForTesting);
    }

    [Fact]
    public void CancelEdit_RowUnit_CancelsActiveEditor()
    {
        var (grid, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        RunLayout(uiRoot);

        var cell = grid.RowsForTesting[0].Cells[0];
        Click(uiRoot, GetCenter(cell.LayoutSlot));
        Assert.True(grid.BeginEdit((RoutedEventArgs?)null));

        var editor = Assert.IsType<TextBox>(cell.EditingElement);
        editor.Text = "77";

        Assert.True(grid.CancelEdit(DataGridEditingUnit.Row));
        Assert.Null(cell.EditingElement);
        Assert.Equal("1", cell.Content?.ToString());
    }

    [Fact]
    public void Copy_FullRowSelection_WritesTabSeparatedRows()
    {
        TextClipboard.ResetForTests();
        var (grid, uiRoot) = CreateGridHost();
        RunLayout(uiRoot);

        Click(uiRoot, GetCenter(grid.RowsForTesting[1].Cells[0].LayoutSlot));

        grid.Copy();

        Assert.True(TextClipboard.TryGetText(out var copied));
        Assert.Equal("2\tBravo", copied);
    }

    [Fact]
    public void Copy_IncludeHeaderMode_WritesHeaderRow()
    {
        TextClipboard.ResetForTests();
        var (grid, uiRoot) = CreateGridHost();
        grid.ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader;
        RunLayout(uiRoot);

        Click(uiRoot, GetCenter(grid.RowsForTesting[0].Cells[0].LayoutSlot));

        grid.Copy();

        Assert.True(TextClipboard.TryGetText(out var copied));
        Assert.Equal($"Id\tName{Environment.NewLine}1\tAlpha", copied);
    }

    [Fact]
    public void CtrlC_CellSelection_WritesActiveCellOnly()
    {
        TextClipboard.ResetForTests();
        var (grid, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        RunLayout(uiRoot);

        Click(uiRoot, GetCenter(grid.RowsForTesting[2].Cells[1].LayoutSlot));
        PressKey(uiRoot, Keys.C, ModifierKeys.Control);

        Assert.True(TextClipboard.TryGetText(out var copied));
        Assert.Equal("Charlie", copied);
    }

    [Fact]
    public void Copy_CellSelectionRectangle_WritesTabSeparatedRectangle()
    {
        TextClipboard.ResetForTests();
        var (grid, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        grid.SelectionMode = DataGridSelectionMode.Extended;
        RunLayout(uiRoot);

        Click(uiRoot, GetCenter(grid.RowsForTesting[0].Cells[0].LayoutSlot));
        Click(uiRoot, GetCenter(grid.RowsForTesting[1].Cells[1].LayoutSlot), ModifierKeys.Shift);

        grid.Copy();

        Assert.True(TextClipboard.TryGetText(out var copied));
        Assert.Equal($"1\tAlpha{Environment.NewLine}2\tBravo", copied);
    }

    [Fact]
    public void Copy_NoneMode_DoesNotWriteClipboard()
    {
        TextClipboard.ResetForTests();
        var (grid, uiRoot) = CreateGridHost();
        grid.ClipboardCopyMode = DataGridClipboardCopyMode.None;
        RunLayout(uiRoot);

        Click(uiRoot, GetCenter(grid.RowsForTesting[0].Cells[0].LayoutSlot));

        grid.Copy();

        Assert.False(TextClipboard.TryGetText(out _));
    }

    [Fact]
    public void ScrollIntoView_ItemAndColumn_UpdatesViewportOffsets()
    {
        var (grid, uiRoot) = CreateGridHost(width: 260f, height: 180f, itemCount: 40);
        grid.Columns.Add(new DataGridColumn { Header = "City", BindingPath = nameof(Row.City), Width = 120f });
        RunLayout(uiRoot, width: 320, height: 240);

        var source = Assert.IsType<ObservableCollection<Row>>(grid.ItemsSource);
        var targetItem = source[39];
        var targetColumn = grid.Columns[2];

        grid.ScrollIntoView(targetItem, targetColumn);
        RunLayout(uiRoot, width: 320, height: 240);

        Assert.True(grid.ScrollViewerForTesting.VerticalOffset > 0f);
        Assert.True(grid.ScrollViewerForTesting.HorizontalOffset > 0f);
    }

    [Fact]
    public void ExtendedSelection_CurrentRowPushesCollectionViewCurrentItem()
    {
        var rows = new ObservableCollection<Row>
        {
            new(1, "Alpha", "Rome"),
            new(2, "Bravo", "Paris"),
            new(3, "Charlie", "Berlin")
        };
        var source = new CollectionViewSource { Source = rows };
        var grid = new DataGrid
        {
            ItemsSource = source,
            Width = 500f,
            Height = 260f,
            SelectionMode = DataGridSelectionMode.Extended,
            IsSynchronizedWithCurrentItem = true
        };
        grid.Columns.Add(new DataGridColumn { Header = "Id", BindingPath = nameof(Row.Id), Width = 120f });
        grid.Columns.Add(new DataGridColumn { Header = "Name", BindingPath = nameof(Row.Name), Width = 120f });

        var host = new Canvas { Width = 540f, Height = 320f };
        host.AddChild(grid);
        Canvas.SetLeft(grid, 10f);
        Canvas.SetTop(grid, 10f);
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        Click(uiRoot, GetCenter(grid.RowsForTesting[0].Cells[0].LayoutSlot));
        Click(uiRoot, GetCenter(grid.RowsForTesting[2].Cells[0].LayoutSlot), ModifierKeys.Control);

        Assert.Same(rows[2], source.View!.CurrentItem);
        Assert.Equal(2, source.View.CurrentPosition);
    }

    [Fact]
    public void ExtendedSelection_CurrentItemPullFromViewUpdatesCurrentRow()
    {
        var rows = new ObservableCollection<Row>
        {
            new(1, "Alpha", "Rome"),
            new(2, "Bravo", "Paris"),
            new(3, "Charlie", "Berlin")
        };
        var source = new CollectionViewSource { Source = rows };
        var grid = new DataGrid
        {
            ItemsSource = source,
            Width = 500f,
            Height = 260f,
            SelectionMode = DataGridSelectionMode.Extended,
            IsSynchronizedWithCurrentItem = true
        };
        grid.Columns.Add(new DataGridColumn { Header = "Id", BindingPath = nameof(Row.Id), Width = 120f });
        grid.Columns.Add(new DataGridColumn { Header = "Name", BindingPath = nameof(Row.Name), Width = 120f });

        var host = new Canvas { Width = 540f, Height = 320f };
        host.AddChild(grid);
        Canvas.SetLeft(grid, 10f);
        Canvas.SetTop(grid, 10f);
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        Click(uiRoot, GetCenter(grid.RowsForTesting[0].Cells[0].LayoutSlot));
        Click(uiRoot, GetCenter(grid.RowsForTesting[2].Cells[0].LayoutSlot), ModifierKeys.Control);

        Assert.True(source.View!.MoveCurrentTo(rows[1]));

        Assert.Equal(1, grid.CurrentRowIndexForTesting);
        Assert.Same(rows[1], grid.CurrentItem);
        Assert.Equal(2, grid.SelectedItems.Count);
    }

    [Fact]
    public void CellOrRowHeader_RowHeaderClick_SelectsWholeRow()
    {
        var (grid, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.CellOrRowHeader);
        RunLayout(uiRoot);

        Click(uiRoot, GetCenter(grid.RowsForTesting[1].RowHeaderForTesting.LayoutSlot));

        Assert.Equal(1, grid.SelectedRowIndex);
        Assert.True(grid.RowsForTesting[1].IsSelected || grid.RowsForTesting[1].Cells.Any(static cell => cell.IsSelected));
    }

    [Fact]
    public void CellOrRowHeader_CellClick_ClearsRowSelectionAndKeepsOnlyCellSelection()
    {
        var (grid, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.CellOrRowHeader);
        RunLayout(uiRoot);

        Click(uiRoot, GetCenter(grid.RowsForTesting[1].RowHeaderForTesting.LayoutSlot));
        Click(uiRoot, GetCenter(grid.RowsForTesting[2].Cells[1].LayoutSlot));

        Assert.Empty(grid.SelectedItems);
        Assert.Equal(-1, grid.SelectedIndex);
        Assert.False(grid.RowsForTesting[1].IsSelected);
        Assert.Equal(new DataGridCellInfo(grid.RowsForTesting[2].Item, grid.Columns[1]), Assert.Single(grid.SelectedCells));
    }

    [Fact]
    public void CellSelection_RowHeaderClick_DoesNotSelectFirstCell()
    {
        var (grid, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        RunLayout(uiRoot);

        Click(uiRoot, GetCenter(grid.RowsForTesting[1].RowHeaderForTesting.LayoutSlot));

        Assert.True(grid.SelectedRowIndex is -1 or 1);
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
        grid.RowDetailsTemplate = new DataTemplate(static item => new Label { Content = $"details:{item}" });
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

        Assert.Equal("Name", grid.ColumnHeadersForTesting[0].GetContentText());
        Assert.Equal("Alpha", grid.RowsForTesting[0].Cells[0].Content?.ToString());
    }

    [Fact]
    public void HeaderReorder_ReusesExistingVisibleCellsWithoutRowsHostMeasureInvalidation()
    {
        var (grid, uiRoot) = CreateGridHost();
        grid.CanUserReorderColumns = true;
        RunLayout(uiRoot);

        var row = grid.RowsForTesting[0];
        var originalFirstCell = row.Cells[0];
        var originalSecondCell = row.Cells[1];
        var rowsHostMeasureInvalidationsBefore = grid.RowsHostForTesting.MeasureInvalidationCount;

        var secondHeader = grid.ColumnHeadersForTesting[1];
        var firstHeader = grid.ColumnHeadersForTesting[0];
        var dragStart = GetCenter(secondHeader.LayoutSlot);
        var dragTarget = GetCenter(firstHeader.LayoutSlot);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(dragStart, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(dragStart, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(dragStart.X - 60f, dragStart.Y), pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(dragTarget, leftReleased: true));
        RunLayout(uiRoot);

        Assert.Same(originalSecondCell, row.Cells[0]);
        Assert.Same(originalFirstCell, row.Cells[1]);
        Assert.Equal(rowsHostMeasureInvalidationsBefore, grid.RowsHostForTesting.MeasureInvalidationCount);
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
        var rowHeaderX = row.RowHeaderForTesting.LayoutSlot.X;
        var frozenX = row.Cells[0].LayoutSlot.X;
        var scrollingX = row.Cells[1].LayoutSlot.X;

        System.Diagnostics.Debug.WriteLine($"DEBUG PRE-SCROLL: row.LayoutSlot={row.LayoutSlot}, rowHeaderX={rowHeaderX}, frozenX={frozenX}, scrollingX={scrollingX}, header[0].X={grid.ColumnHeadersForTesting[0].LayoutSlot.X}, header[1].X={grid.ColumnHeadersForTesting[1].LayoutSlot.X}");
        System.Diagnostics.Debug.WriteLine($"DEBUG PRE-SCROLL: VSP LayoutSlot={grid.RowsHostForTesting.LayoutSlot}, ScrollViewer.HorizontalOffset={grid.ScrollViewerForTesting.HorizontalOffset}");

        grid.ScrollViewerForTesting.ScrollToHorizontalOffset(60f);
        RunLayout(uiRoot, width: 260, height: 300);

        System.Diagnostics.Debug.WriteLine($"DEBUG POST-SCROLL: row.LayoutSlot={row.LayoutSlot}, rowHeader.X={row.RowHeaderForTesting.LayoutSlot.X}, cell[0].X={row.Cells[0].LayoutSlot.X}, cell[1].X={row.Cells[1].LayoutSlot.X}");
        System.Diagnostics.Debug.WriteLine($"DEBUG POST-SCROLL: header[0].X={grid.ColumnHeadersForTesting[0].LayoutSlot.X}, header[1].X={grid.ColumnHeadersForTesting[1].LayoutSlot.X}");
        System.Diagnostics.Debug.WriteLine($"DEBUG POST-SCROLL: VSP LayoutSlot={grid.RowsHostForTesting.LayoutSlot}, ScrollViewer.HorizontalOffset={grid.ScrollViewerForTesting.HorizontalOffset}");

        Assert.Equal(rowHeaderX, row.RowHeaderForTesting.LayoutSlot.X);
        Assert.Equal(frozenX, row.Cells[0].LayoutSlot.X);
        Assert.True(row.Cells[1].LayoutSlot.X < scrollingX);
    }

    [Fact]
    public void HorizontalScroll_RepositionsHeadersWithoutGridLayoutInvalidation()
    {
        var (grid, uiRoot) = CreateGridHost(width: 260f);
        grid.Columns.Clear();
        grid.Columns.Add(new DataGridColumn { Header = "Id", BindingPath = nameof(Row.Id), Width = 120f });
        grid.Columns.Add(new DataGridColumn { Header = "Name", BindingPath = nameof(Row.Name), Width = 120f });
        grid.Columns.Add(new DataGridColumn { Header = "City", BindingPath = nameof(Row.City), Width = 120f });
        grid.FrozenColumnCount = 1;
        RunLayout(uiRoot, width: 260, height: 300);

        var frozenHeaderX = grid.ColumnHeadersForTesting[0].LayoutSlot.X;
        var scrollingHeaderX = grid.ColumnHeadersForTesting[1].LayoutSlot.X;
        var gridMeasureInvalidationsBefore = grid.MeasureInvalidationCount;
        var gridArrangeInvalidationsBefore = grid.ArrangeInvalidationCount;
        var rootMeasureInvalidationsBefore = uiRoot.MeasureInvalidationCount;
        var rootArrangeInvalidationsBefore = uiRoot.ArrangeInvalidationCount;

        uiRoot.ResetDirtyStateForTests();
        grid.ScrollViewerForTesting.ScrollToHorizontalOffset(60f);
        RunLayout(uiRoot, width: 260, height: 300);

        Assert.Equal(frozenHeaderX, grid.ColumnHeadersForTesting[0].LayoutSlot.X);
        Assert.True(grid.ColumnHeadersForTesting[1].LayoutSlot.X < scrollingHeaderX);
        Assert.Equal(gridMeasureInvalidationsBefore, grid.MeasureInvalidationCount);
        Assert.True(grid.ArrangeInvalidationCount > gridArrangeInvalidationsBefore);
        Assert.Equal(rootMeasureInvalidationsBefore, uiRoot.MeasureInvalidationCount);
        Assert.True(uiRoot.ArrangeInvalidationCount > rootArrangeInvalidationsBefore);
    }

    [Fact]
    public void HorizontalScrollBarThumbDrag_KeepsFrozenLanesAligned()
    {
        var (grid, uiRoot) = CreateGridHost(width: 260f, itemCount: 12);
        grid.Columns.Clear();
        grid.Columns.Add(new DataGridColumn { Header = "Ticket", BindingPath = nameof(Row.Id), Width = 96f });
        grid.Columns.Add(new DataGridColumn { Header = "Name", BindingPath = nameof(Row.Name), Width = 160f });
        grid.Columns.Add(new DataGridColumn { Header = "Team", BindingPath = nameof(Row.City), Width = 150f });
        grid.Columns.Add(new DataGridColumn { Header = "Priority", BindingPath = nameof(Row.Name), Width = 110f });
        grid.FrozenColumnCount = 1;
        RunLayout(uiRoot, width: 260, height: 300);

        var row = grid.RowsForTesting[0];
        var rowHeaderX = row.RowHeaderForTesting.LayoutSlot.X;
        var frozenHeaderX = grid.ColumnHeadersForTesting[0].LayoutSlot.X;
        var frozenCellX = row.Cells[0].LayoutSlot.X;

        var horizontalBar = GetPrivateScrollBar(grid.ScrollViewerForTesting, "_horizontalBar");
        var thumbCenter = GetCenter(horizontalBar.GetThumbRectForInput());
        var rightPointer = new Vector2(horizontalBar.LayoutSlot.X + horizontalBar.LayoutSlot.Width - 2f, thumbCenter.Y);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(thumbCenter, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(thumbCenter, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(rightPointer, pointerMoved: true));
        RunLayout(uiRoot, width: 260, height: 300);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(rightPointer, leftReleased: true));
        RunLayout(uiRoot, width: 260, height: 300);

        Assert.True(grid.ScrollViewerForTesting.HorizontalOffset > 0f);
        Assert.Equal(rowHeaderX, row.RowHeaderForTesting.LayoutSlot.X);
        Assert.Equal(frozenHeaderX, grid.ColumnHeadersForTesting[0].LayoutSlot.X);
        Assert.Equal(frozenCellX, row.Cells[0].LayoutSlot.X);
        Assert.Equal(grid.ColumnHeadersForTesting[1].LayoutSlot.X, row.Cells[1].LayoutSlot.X);
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

    private static void Click(UiRoot uiRoot, Vector2 pointer, ModifierKeys modifiers = ModifierKeys.None)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, modifiers: modifiers, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, modifiers: modifiers, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, modifiers: modifiers, leftReleased: true));
    }

    private static void PressKey(UiRoot uiRoot, Keys key, ModifierKeys modifiers = ModifierKeys.None)
    {
        var previousKeyboard = CreateKeyboardState(modifiers);
        var currentKeyboard = CreateKeyboardState(modifiers, key);
        uiRoot.RunInputDeltaForTests(new InputDelta
        {
            Previous = new InputSnapshot(previousKeyboard, default, Vector2.Zero),
            Current = new InputSnapshot(currentKeyboard, default, Vector2.Zero),
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

    private static InputDelta CreatePointerDelta(Vector2 pointer, ModifierKeys modifiers = ModifierKeys.None, bool pointerMoved = false, bool leftPressed = false, bool leftReleased = false)
    {
        var keyboard = CreateKeyboardState(modifiers);
        return new InputDelta
        {
            Previous = new InputSnapshot(keyboard, default, pointer),
            Current = new InputSnapshot(keyboard, default, pointer),
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

    private static KeyboardState CreateKeyboardState(ModifierKeys modifiers, params Keys[] additionalKeys)
    {
        var pressedKeys = new List<Keys>(additionalKeys.Length + 2);
        if ((modifiers & ModifierKeys.Control) != 0)
        {
            pressedKeys.Add(Keys.LeftControl);
        }

        if ((modifiers & ModifierKeys.Shift) != 0)
        {
            pressedKeys.Add(Keys.LeftShift);
        }

        for (var i = 0; i < additionalKeys.Length; i++)
        {
            pressedKeys.Add(additionalKeys[i]);
        }

        return new KeyboardState(pressedKeys.ToArray());
    }

    private static Vector2 GetCenter(LayoutRect rect) => new(rect.X + (rect.Width * 0.5f), rect.Y + (rect.Height * 0.5f));

    private static ScrollBar GetPrivateScrollBar(ScrollViewer viewer, string fieldName)
    {
        var field = typeof(ScrollViewer).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<ScrollBar>(field!.GetValue(viewer));
    }

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
