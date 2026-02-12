using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public class DataGridTests
{
    public DataGridTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void DataGrid_GeneratesRowsAndCells_FromColumns()
    {
        var grid = CreateSampleGrid();

        grid.Measure(new Vector2(600f, 300f));
        grid.Arrange(new LayoutRect(0f, 0f, 600f, 300f));

        Assert.Equal(3, grid.RowCount);

        var firstRow = grid.GetRow(0);
        Assert.Equal(3, firstRow.Cells.Count);
        Assert.Equal("Pencil", firstRow.Cells[0].Value?.ToString());
        Assert.Equal("Tools", firstRow.Cells[1].Value?.ToString());
    }

    [Fact]
    public void DataGrid_CellSelection_TracksRowAndColumn()
    {
        var grid = CreateSampleGrid();
        grid.SelectionUnit = DataGridSelectionUnit.Cell;

        grid.Measure(new Vector2(600f, 300f));
        grid.Arrange(new LayoutRect(0f, 0f, 600f, 300f));

        var row = grid.GetRow(1);
        grid.NotifyRowPressed(row, 2);

        Assert.Equal(1, grid.SelectedRowIndex);
        Assert.Equal(2, grid.SelectedColumnIndex);
        Assert.True(row.Cells[2].IsSelected);
    }

    [Fact]
    public void DataGrid_KeyboardNavigation_MovesSelection()
    {
        var grid = CreateSampleGrid();
        grid.SelectionUnit = DataGridSelectionUnit.Cell;

        grid.Measure(new Vector2(600f, 300f));
        grid.Arrange(new LayoutRect(0f, 0f, 600f, 300f));

        grid.NotifyRowPressed(grid.GetRow(0), 0);
        grid.FireKey(Keys.Down);
        grid.FireKey(Keys.Right);

        Assert.Equal(1, grid.SelectedRowIndex);
        Assert.Equal(1, grid.SelectedColumnIndex);
    }

    [Fact]
    public void DataGrid_UsesVirtualization_ForLargeRowSets()
    {
        RunVirtualizationScenario(2000);
    }

    [Fact(Skip = "Manual stress test: can destabilize local testhost on some machines.")]
    public void DataGrid_UsesVirtualization_ForTenThousandRows_ManualStress()
    {
        RunVirtualizationScenario(10000);
    }

    [Fact]
    public void XamlLoader_ParsesDataGridColumns()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <DataGrid x:Name="Grid" Width="400" Height="220">
                                <DataGrid.Columns>
                                  <DataGridColumn Header="Name" BindingPath="Name" Width="150" />
                                  <DataGridColumn Header="Category" BindingPath="Category" Width="140" />
                                </DataGrid.Columns>
                              </DataGrid>
                            </UserControl>
                            """;

        var codeBehind = new GridCodeBehind();
        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, codeBehind);

        Assert.NotNull(codeBehind.Grid);
        Assert.Equal(2, codeBehind.Grid!.Columns.Count);
        Assert.Equal("Name", codeBehind.Grid.Columns[0].Header);
        Assert.Equal("Category", codeBehind.Grid.Columns[1].BindingPath);
    }

    [Fact]
    public void DataGrid_UsesClipRect_MatchingLayoutSlot()
    {
        var grid = CreateSampleGrid();

        grid.Measure(new Vector2(600f, 300f));
        grid.Arrange(new LayoutRect(10f, 20f, 420f, 160f));

        var hasClip = grid.TryGetClipRectForTesting(out var clipRect);
        Assert.True(hasClip);
        Assert.Equal(grid.LayoutSlot.X, clipRect.X, 3);
        Assert.Equal(grid.LayoutSlot.Y, clipRect.Y, 3);
        Assert.Equal(grid.LayoutSlot.Width, clipRect.Width, 3);
        Assert.Equal(grid.LayoutSlot.Height, clipRect.Height, 3);
    }

    private static TestDataGrid CreateSampleGrid()
    {
        var grid = new TestDataGrid
        {
            Width = 600f,
            Height = 300f
        };

        grid.Columns.Add(new DataGridColumn { Header = "Name", BindingPath = "Name", Width = 180f });
        grid.Columns.Add(new DataGridColumn { Header = "Category", BindingPath = "Category", Width = 180f });
        grid.Columns.Add(new DataGridColumn { Header = "Qty", BindingPath = "Quantity", Width = 90f });

        grid.Items.Add(new RowModel { Name = "Pencil", Category = "Tools", Quantity = 4, Index = 0 });
        grid.Items.Add(new RowModel { Name = "Brush", Category = "Paint", Quantity = 12, Index = 1 });
        grid.Items.Add(new RowModel { Name = "Layer", Category = "Panel", Quantity = 1, Index = 2 });

        return grid;
    }

    private static void RunVirtualizationScenario(int rowCount)
    {
        var grid = new TestDataGrid
        {
            Width = 640f,
            Height = 280f,
            EnableRowVirtualization = true
        };

        grid.Columns.Add(new DataGridColumn { Header = "Index", BindingPath = "Index", Width = 100f });
        var bulkRows = new RowModel[rowCount];
        for (var i = 0; i < bulkRows.Length; i++)
        {
            bulkRows[i] = new RowModel { Name = "Item", Category = "Bulk", Quantity = i, Index = i };
        }

        grid.AddItems(bulkRows);

        grid.Measure(new Vector2(640f, 280f));
        grid.Arrange(new LayoutRect(0f, 0f, 640f, 280f));

        Assert.True(grid.RealizedRowCountForTesting > 0);
        Assert.True(grid.RealizedRowCountForTesting < rowCount);
    }

    private sealed class TestDataGrid : DataGrid
    {
        public int RowCount => ItemContainers.Count;

        public DataGridRow GetRow(int index)
        {
            return (DataGridRow)ItemContainers[index];
        }

        public void FireKey(Keys key)
        {
            RaisePreviewKeyDown(key, false, ModifierKeys.None);
            RaiseKeyDown(key, false, ModifierKeys.None);
        }

        public bool TryGetClipRectForTesting(out LayoutRect clipRect)
        {
            return TryGetClipRect(out clipRect);
        }
    }

    private sealed class GridCodeBehind
    {
        public DataGrid? Grid { get; set; }
    }

    private sealed class RowModel
    {
        public string Name { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public int Index { get; set; }
    }
}
