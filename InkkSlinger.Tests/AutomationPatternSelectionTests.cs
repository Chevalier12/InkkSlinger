using Xunit;

namespace InkkSlinger.Tests;

public sealed class AutomationPatternSelectionTests
{
    [Fact]
    public void ListBoxPeer_ExposesSelectionPattern()
    {
        var host = new Canvas();
        var listBox = new ListBox();
        listBox.Items.Add("One");
        listBox.Items.Add("Two");
        listBox.SelectedIndex = 1;
        host.AddChild(listBox);

        var uiRoot = new UiRoot(host);
        var listPeer = uiRoot.Automation.GetPeer(listBox);
        Assert.NotNull(listPeer);

        Assert.True(listPeer.TryGetPattern(AutomationPatternType.Selection, out var selectionPattern));
        var selectionProvider = Assert.IsAssignableFrom<ISelectionProvider>(selectionPattern);
        var selected = selectionProvider.GetSelection();

        Assert.Single(selected);

        uiRoot.Shutdown();
    }

    [Fact]
    public void ListBoxItemPeer_ExposesSelectionItemPattern()
    {
        var host = new Canvas();
        var listBox = new ListBox();
        listBox.Items.Add("One");
        listBox.Items.Add("Two");
        host.AddChild(listBox);

        var uiRoot = new UiRoot(host);
        listBox.SelectedIndex = 0;
        var firstContainer = listBox.GetItemContainersForPresenter()[0];
        var peer = uiRoot.Automation.GetPeer(firstContainer);
        Assert.NotNull(peer);

        Assert.True(peer.TryGetPattern(AutomationPatternType.SelectionItem, out var selectionItem));
        var selectionItemProvider = Assert.IsAssignableFrom<ISelectionItemProvider>(selectionItem);
        Assert.True(selectionItemProvider.IsSelected);

        uiRoot.Shutdown();
    }

    [Fact]
    public void DataGridPeer_ExtendedSelection_ReportsCanSelectMultiple()
    {
        var host = new Canvas();
        var grid = new DataGrid
        {
            Width = 400f,
            Height = 240f,
            ItemsSource = new[]
            {
                new Row(1, "Alpha"),
                new Row(2, "Bravo")
            },
            SelectionMode = DataGridSelectionMode.Extended
        };
        grid.Columns.Add(new DataGridColumn { Header = "Id", BindingPath = nameof(Row.Id), Width = 120f });
        grid.Columns.Add(new DataGridColumn { Header = "Name", BindingPath = nameof(Row.Name), Width = 120f });
        host.AddChild(grid);

        var uiRoot = new UiRoot(host);
        uiRoot.Update(new Microsoft.Xna.Framework.GameTime(System.TimeSpan.FromMilliseconds(16), System.TimeSpan.FromMilliseconds(16)), new Microsoft.Xna.Framework.Graphics.Viewport(0, 0, 640, 360));
        grid.SelectedIndex = 1;
        var gridPeer = uiRoot.Automation.GetPeer(grid);
        Assert.NotNull(gridPeer);

        Assert.True(gridPeer.TryGetPattern(AutomationPatternType.Selection, out var selectionPattern));
        var selectionProvider = Assert.IsAssignableFrom<ISelectionProvider>(selectionPattern);
        Assert.True(selectionProvider.CanSelectMultiple);
        Assert.Single(selectionProvider.GetSelection());

        uiRoot.Shutdown();
    }

    [Fact]
    public void DataGridRowPeer_ExposesSelectionItemPattern()
    {
        var host = new Canvas();
        var grid = new DataGrid
        {
            Width = 400f,
            Height = 240f,
            ItemsSource = new[]
            {
                new Row(1, "Alpha"),
                new Row(2, "Bravo")
            }
        };
        grid.Columns.Add(new DataGridColumn { Header = "Id", BindingPath = nameof(Row.Id), Width = 120f });
        grid.Columns.Add(new DataGridColumn { Header = "Name", BindingPath = nameof(Row.Name), Width = 120f });
        host.AddChild(grid);

        var uiRoot = new UiRoot(host);
        uiRoot.Update(new Microsoft.Xna.Framework.GameTime(System.TimeSpan.FromMilliseconds(16), System.TimeSpan.FromMilliseconds(16)), new Microsoft.Xna.Framework.Graphics.Viewport(0, 0, 640, 360));
        grid.SelectedIndex = 0;
        var row = Assert.IsType<DataGridRow>(grid.GetItemContainersForPresenter()[0]);
        var peer = uiRoot.Automation.GetPeer(row);
        Assert.NotNull(peer);

        Assert.True(peer.TryGetPattern(AutomationPatternType.SelectionItem, out var selectionItem));
        var selectionItemProvider = Assert.IsAssignableFrom<ISelectionItemProvider>(selectionItem);
        Assert.True(selectionItemProvider.IsSelected);

        uiRoot.Shutdown();
    }

    [Fact]
    public void DataGridPeer_ExposesGridAndTablePatterns()
    {
        var host = new Canvas();
        var grid = new DataGrid
        {
            Width = 400f,
            Height = 240f,
            ItemsSource = new[]
            {
                new Row(1, "Alpha"),
                new Row(2, "Bravo")
            }
        };
        grid.Columns.Add(new DataGridColumn { Header = "Id", BindingPath = nameof(Row.Id), Width = 120f });
        grid.Columns.Add(new DataGridColumn { Header = "Name", BindingPath = nameof(Row.Name), Width = 120f });
        host.AddChild(grid);

        var uiRoot = new UiRoot(host);
        uiRoot.Update(new Microsoft.Xna.Framework.GameTime(System.TimeSpan.FromMilliseconds(16), System.TimeSpan.FromMilliseconds(16)), new Microsoft.Xna.Framework.Graphics.Viewport(0, 0, 640, 360));
        var gridPeer = uiRoot.Automation.GetPeer(grid);
        Assert.NotNull(gridPeer);

        Assert.True(gridPeer.TryGetPattern(AutomationPatternType.Grid, out var gridPattern));
        var gridProvider = Assert.IsAssignableFrom<IGridProvider>(gridPattern);
        Assert.Equal(2, gridProvider.RowCount);
        Assert.Equal(2, gridProvider.ColumnCount);
        Assert.NotNull(gridProvider.GetItem(1, 1));

        Assert.True(gridPeer.TryGetPattern(AutomationPatternType.Table, out var tablePattern));
        var tableProvider = Assert.IsAssignableFrom<ITableProvider>(tablePattern);
        Assert.Equal(RowOrColumnMajor.RowMajor, tableProvider.RowOrColumnMajor);
        Assert.Equal(2, tableProvider.GetColumnHeaders().Count);
        Assert.Equal(2, tableProvider.GetRowHeaders().Count);

        uiRoot.Shutdown();
    }

    [Fact]
    public void DataGridPeer_CellSelection_ReturnsSelectedCellPeers()
    {
        var host = new Canvas();
        var grid = new DataGrid
        {
            Width = 400f,
            Height = 240f,
            SelectionMode = DataGridSelectionMode.Extended,
            SelectionUnit = DataGridSelectionUnit.Cell,
            ItemsSource = new[]
            {
                new Row(1, "Alpha"),
                new Row(2, "Bravo")
            }
        };
        grid.Columns.Add(new DataGridColumn { Header = "Id", BindingPath = nameof(Row.Id), Width = 120f });
        grid.Columns.Add(new DataGridColumn { Header = "Name", BindingPath = nameof(Row.Name), Width = 120f });
        host.AddChild(grid);

        var uiRoot = new UiRoot(host);
        uiRoot.Update(new Microsoft.Xna.Framework.GameTime(System.TimeSpan.FromMilliseconds(16), System.TimeSpan.FromMilliseconds(16)), new Microsoft.Xna.Framework.Graphics.Viewport(0, 0, 640, 360));

        var firstCell = grid.RowsForTesting[0].Cells[0];
        var secondCell = grid.RowsForTesting[1].Cells[1];
        var firstCellCenter = new Microsoft.Xna.Framework.Vector2(firstCell.LayoutSlot.X + (firstCell.LayoutSlot.Width / 2f), firstCell.LayoutSlot.Y + (firstCell.LayoutSlot.Height / 2f));
        var secondCellCenter = new Microsoft.Xna.Framework.Vector2(secondCell.LayoutSlot.X + (secondCell.LayoutSlot.Width / 2f), secondCell.LayoutSlot.Y + (secondCell.LayoutSlot.Height / 2f));
        Assert.True(grid.HandlePointerDownFromInput(firstCell, firstCellCenter, ModifierKeys.None, out _));
        Assert.True(grid.HandlePointerDownFromInput(secondCell, secondCellCenter, ModifierKeys.Control, out _));

        var gridPeer = uiRoot.Automation.GetPeer(grid);
        Assert.NotNull(gridPeer);
        Assert.True(gridPeer.TryGetPattern(AutomationPatternType.Selection, out var selectionPattern));
        var selectionProvider = Assert.IsAssignableFrom<ISelectionProvider>(selectionPattern);
        var selected = selectionProvider.GetSelection();

        Assert.Equal(2, selected.Count);
        Assert.All(selected, peer => Assert.True(peer.TryGetPattern(AutomationPatternType.GridItem, out _)));

        uiRoot.Shutdown();
    }

    [Fact]
    public void DataGridCellPeer_ExposesGridItemAndTableItemPatterns()
    {
        var host = new Canvas();
        var grid = new DataGrid
        {
            Width = 400f,
            Height = 240f,
            SelectionUnit = DataGridSelectionUnit.Cell,
            ItemsSource = new[]
            {
                new Row(1, "Alpha"),
                new Row(2, "Bravo")
            }
        };
        grid.Columns.Add(new DataGridColumn { Header = "Id", BindingPath = nameof(Row.Id), Width = 120f });
        grid.Columns.Add(new DataGridColumn { Header = "Name", BindingPath = nameof(Row.Name), Width = 120f });
        host.AddChild(grid);

        var uiRoot = new UiRoot(host);
        uiRoot.Update(new Microsoft.Xna.Framework.GameTime(System.TimeSpan.FromMilliseconds(16), System.TimeSpan.FromMilliseconds(16)), new Microsoft.Xna.Framework.Graphics.Viewport(0, 0, 640, 360));
        var cellPeer = uiRoot.Automation.GetPeer(grid.RowsForTesting[1].Cells[1]);
        Assert.NotNull(cellPeer);

        Assert.True(cellPeer.TryGetPattern(AutomationPatternType.GridItem, out var gridItemPattern));
        var gridItemProvider = Assert.IsAssignableFrom<IGridItemProvider>(gridItemPattern);
        Assert.Equal(1, gridItemProvider.Row);
        Assert.Equal(1, gridItemProvider.Column);
        Assert.NotNull(gridItemProvider.ContainingGrid);

        Assert.True(cellPeer.TryGetPattern(AutomationPatternType.TableItem, out var tableItemPattern));
        var tableItemProvider = Assert.IsAssignableFrom<ITableItemProvider>(tableItemPattern);
        Assert.Single(tableItemProvider.GetColumnHeaderItems());
        Assert.Single(tableItemProvider.GetRowHeaderItems());

        uiRoot.Shutdown();
    }

    [Fact]
    public void DataGridCellPeer_AddToSelectionAndRemoveFromSelection_ModifyCellSelection()
    {
        var host = new Canvas();
        var grid = new DataGrid
        {
            Width = 400f,
            Height = 240f,
            SelectionMode = DataGridSelectionMode.Extended,
            SelectionUnit = DataGridSelectionUnit.Cell,
            ItemsSource = new[]
            {
                new Row(1, "Alpha"),
                new Row(2, "Bravo")
            }
        };
        grid.Columns.Add(new DataGridColumn { Header = "Id", BindingPath = nameof(Row.Id), Width = 120f });
        grid.Columns.Add(new DataGridColumn { Header = "Name", BindingPath = nameof(Row.Name), Width = 120f });
        host.AddChild(grid);

        var uiRoot = new UiRoot(host);
        uiRoot.Update(new Microsoft.Xna.Framework.GameTime(System.TimeSpan.FromMilliseconds(16), System.TimeSpan.FromMilliseconds(16)), new Microsoft.Xna.Framework.Graphics.Viewport(0, 0, 640, 360));
        var firstCellPeer = uiRoot.Automation.GetPeer(grid.RowsForTesting[0].Cells[0]);
        var secondCellPeer = uiRoot.Automation.GetPeer(grid.RowsForTesting[1].Cells[1]);
        Assert.NotNull(firstCellPeer);
        Assert.NotNull(secondCellPeer);

        Assert.True(firstCellPeer.TryGetPattern(AutomationPatternType.SelectionItem, out var firstSelectionPattern));
        Assert.True(secondCellPeer.TryGetPattern(AutomationPatternType.SelectionItem, out var secondSelectionPattern));
        var firstSelectionItem = Assert.IsAssignableFrom<ISelectionItemProvider>(firstSelectionPattern);
        var secondSelectionItem = Assert.IsAssignableFrom<ISelectionItemProvider>(secondSelectionPattern);

        firstSelectionItem.Select();
        secondSelectionItem.AddToSelection();
        Assert.Equal(2, grid.SelectedCells.Count);

        secondSelectionItem.RemoveFromSelection();
        Assert.Single(grid.SelectedCells);
        Assert.Equal(new DataGridCellInfo(grid.RowsForTesting[0].Item, grid.Columns[0]), grid.SelectedCells[0]);

        uiRoot.Shutdown();
    }

    private sealed record Row(int Id, string Name);
}
