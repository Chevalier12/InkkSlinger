using System.Collections.ObjectModel;
using System.ComponentModel;
using Xunit;

namespace InkkSlinger.Tests;

public class CollectionViewCoreTests
{
    [Fact]
    public void CollectionView_AppliesFilterSortGroup_AndMaintainsCurrent()
    {
        var source = new ObservableCollection<Row>
        {
            new(1, "B", "Art"),
            new(2, "A", "UI"),
            new(3, "C", "Art")
        };

        var view = new ListCollectionView(source);
        view.Filter = item => item is Row row && row.Id != 2;
        view.SortDescriptions.Add(new SortDescription(nameof(Row.Name), ListSortDirection.Ascending));
        view.GroupDescriptions.Add(new PropertyGroupDescription { PropertyName = nameof(Row.Category) });
        view.Refresh();

        Assert.True(view.MoveCurrentToFirst());
        Assert.Equal("B", Assert.IsType<Row>(view.CurrentItem).Name);
        Assert.Single(view.Groups);
        Assert.Equal("Art", view.Groups[0].Name);
        Assert.Equal(2, view.Groups[0].ItemCount);
    }

    [Fact]
    public void CollectionView_RefreshesWhenSourceChanges()
    {
        var source = new ObservableCollection<Row>
        {
            new(1, "A", "One")
        };
        var view = new ListCollectionView(source);

        source.Add(new Row(2, "B", "Two"));

        var count = 0;
        foreach (var _ in view)
        {
            count++;
        }

        Assert.Equal(2, count);
    }

    private sealed record Row(int Id, string Name, string Category);
}
