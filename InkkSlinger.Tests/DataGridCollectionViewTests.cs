using System.Collections.ObjectModel;
using System.Reflection;
using Xunit;

namespace InkkSlinger.Tests;

public class DataGridCollectionViewTests
{
    [Fact]
    public void DataGrid_HeaderSortUpdatesCollectionViewSortDescriptions()
    {
        var source = new ObservableCollection<Row>
        {
            new(2, "B"),
            new(1, "A")
        };

        var grid = new DataGrid
        {
            ItemsSource = source
        };
        grid.Columns.Add(new DataGridColumn
        {
            Header = "Id",
            BindingPath = nameof(Row.Id)
        });

        grid.Measure(new Microsoft.Xna.Framework.Vector2(600, 400));
        grid.Arrange(new LayoutRect(0, 0, 600, 400));

        var headersField = typeof(DataGrid).GetField("_columnHeaders", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(headersField);
        var headers = Assert.IsType<System.Collections.Generic.List<DataGridColumnHeader>>(headersField!.GetValue(grid));
        Assert.NotEmpty(headers);

        var invoke = typeof(DataGrid).GetMethod("OnColumnHeaderClick", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(invoke);
        invoke!.Invoke(grid, [headers[0], new RoutedSimpleEventArgs(Button.ClickEvent)]);

        var view = Assert.IsAssignableFrom<ICollectionView>(typeof(ItemsControl)
            .GetProperty("ItemsSourceView", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(grid));
        Assert.NotEmpty(view.SortDescriptions);
        Assert.Equal(nameof(Row.Id), view.SortDescriptions[0].PropertyName);
    }

    private sealed record Row(int Id, string Name);
}
