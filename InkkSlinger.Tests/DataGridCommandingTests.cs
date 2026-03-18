using System.Collections.ObjectModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class DataGridCommandingTests
{
    [Fact]
    public void CommandCanExecute_ReflectsSelectionEditingAndDeletePermissions()
    {
        var (grid, _, uiRoot) = CreateGridHost();
        grid.SelectionMode = DataGridSelectionMode.Extended;
        RunLayout(uiRoot);

        grid.CurrentCell = new DataGridCellInfo(grid.RowsForTesting[0].Item, grid.Columns[0]);

        Assert.True(CommandManager.CanExecute(DataGrid.BeginEditCommand, null, grid));
        Assert.False(CommandManager.CanExecute(DataGrid.CommitEditCommand, null, grid));
        Assert.False(CommandManager.CanExecute(DataGrid.CancelEditCommand, null, grid));
        Assert.True(CommandManager.CanExecute(EditingCommands.Copy, null, grid));
        Assert.True(CommandManager.CanExecute(DataGrid.SelectAllCommand, null, grid));
        Assert.True(CommandManager.CanExecute(DataGrid.DeleteCommand, null, grid));

        CommandManager.Execute(DataGrid.BeginEditCommand, null, grid);

        Assert.True(CommandManager.CanExecute(DataGrid.CommitEditCommand, null, grid));
        Assert.True(CommandManager.CanExecute(DataGrid.CancelEditCommand, null, grid));
        Assert.False(CommandManager.CanExecute(DataGrid.DeleteCommand, null, grid));

        grid.CanUserDeleteRows = false;
        Assert.False(CommandManager.CanExecute(DataGrid.DeleteCommand, null, grid));

        uiRoot.Shutdown();
    }

    [Fact]
    public void BeginAndCommitEditCommands_ExecuteThroughCommandManager()
    {
        var (grid, source, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        RunLayout(uiRoot);

        grid.CurrentCell = new DataGridCellInfo(source[0], grid.Columns[0]);

        CommandManager.Execute(DataGrid.BeginEditCommand, null, grid);
        var editor = Assert.IsType<TextBox>(grid.RowsForTesting[0].Cells[0].EditingElement);
        editor.Text = "41";

        CommandManager.Execute(DataGrid.CommitEditCommand, null, grid);

        Assert.Null(grid.RowsForTesting[0].Cells[0].EditingElement);
        Assert.Equal(41, source[0].Id);

        uiRoot.Shutdown();
    }

    [Fact]
    public void SelectAllAndCopyCommands_ExecuteThroughCommandManager()
    {
        TextClipboard.ResetForTests();
        var (grid, _, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        grid.SelectionMode = DataGridSelectionMode.Extended;
        RunLayout(uiRoot);

        grid.CurrentCell = new DataGridCellInfo(grid.RowsForTesting[0].Item, grid.Columns[0]);

        CommandManager.Execute(DataGrid.SelectAllCommand, null, grid);
        CommandManager.Execute(EditingCommands.Copy, null, grid);

        Assert.Equal(6, grid.SelectedCells.Count);
        Assert.True(TextClipboard.TryGetText(out var copied));
        Assert.Equal($"1\tAlpha{System.Environment.NewLine}2\tBravo{System.Environment.NewLine}3\tCharlie", copied);

        uiRoot.Shutdown();
    }

    [Fact]
    public void DeleteCommand_RemovesSelectedRowsFromMutableSource()
    {
        var (grid, source, uiRoot) = CreateGridHost();
        RunLayout(uiRoot);

        grid.CurrentCell = new DataGridCellInfo(source[1], grid.Columns[0]);

        CommandManager.Execute(DataGrid.DeleteCommand, null, grid);

        Assert.Equal(2, source.Count);
        Assert.DoesNotContain(source, row => row.Id == 2);

        uiRoot.Shutdown();
    }

    [Fact]
    public void KeyGestures_ExecuteThroughDataGridCommandBindings()
    {
        TextClipboard.ResetForTests();
        var (grid, _, uiRoot) = CreateGridHost(selectionUnit: DataGridSelectionUnit.Cell);
        grid.SelectionMode = DataGridSelectionMode.Extended;
        RunLayout(uiRoot);

        grid.CurrentCell = new DataGridCellInfo(grid.RowsForTesting[0].Item, grid.Columns[0]);
        grid.SetFocusedFromInput(true);

        var selectAllHandled = grid.HandleKeyDownFromInput(Keys.A, ModifierKeys.Control);
        var copyHandled = grid.HandleKeyDownFromInput(Keys.C, ModifierKeys.Control);

        Assert.True(selectAllHandled);
        Assert.True(copyHandled);
        Assert.Equal(6, grid.SelectedCells.Count);
        Assert.True(TextClipboard.TryGetText(out var copied));
        Assert.Equal($"1\tAlpha{System.Environment.NewLine}2\tBravo{System.Environment.NewLine}3\tCharlie", copied);

        uiRoot.Shutdown();
    }

    private static (DataGrid Grid, ObservableCollection<Row> Source, UiRoot UiRoot) CreateGridHost(DataGridSelectionUnit selectionUnit = DataGridSelectionUnit.FullRow)
    {
        var source = new ObservableCollection<Row>
        {
            new(1, "Alpha"),
            new(2, "Bravo"),
            new(3, "Charlie")
        };

        var grid = new DataGrid
        {
            Width = 400f,
            Height = 240f,
            ItemsSource = source,
            SelectionUnit = selectionUnit
        };
        grid.Columns.Add(new DataGridColumn { Header = "Id", BindingPath = nameof(Row.Id), Width = 120f });
        grid.Columns.Add(new DataGridColumn { Header = "Name", BindingPath = nameof(Row.Name), Width = 120f });

        var host = new Canvas();
        host.AddChild(grid);
        var uiRoot = new UiRoot(host);
        return (grid, source, uiRoot);
    }

    private static void RunLayout(UiRoot uiRoot, int width = 640, int height = 360)
    {
        uiRoot.Update(new GameTime(System.TimeSpan.FromMilliseconds(16), System.TimeSpan.FromMilliseconds(16)), new Viewport(0, 0, width, height));
    }

    private sealed record Row(int Id, string Name);
}