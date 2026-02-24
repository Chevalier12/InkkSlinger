using System;
using System.Collections.ObjectModel;
using Xunit;

namespace InkkSlinger.Tests;

public class ItemsSourceParityTests
{
    [Fact]
    public void ItemsControl_WhenItemsSourceSet_ItemsMutationThrows()
    {
        var listBox = new ListBox
        {
            ItemsSource = new ObservableCollection<string> { "one", "two" }
        };

        var ex = Assert.Throws<InvalidOperationException>(() => listBox.Items.Add("three"));
        Assert.Contains("ItemsSource", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ItemsControl_WhenItemsSourceCleared_ItemsMutationAllowed()
    {
        var listBox = new ListBox
        {
            ItemsSource = new ObservableCollection<string> { "one", "two" }
        };

        listBox.ItemsSource = null;
        listBox.Items.Add("local");

        Assert.Single(listBox.Items);
    }

    [Fact]
    public void GroupStyle_WithGroupedView_ProjectsGroupContainers()
    {
        var source = new ObservableCollection<Row>
        {
            new("A", "Art"),
            new("B", "UI"),
            new("C", "Art")
        };

        var viewSource = new CollectionViewSource
        {
            Source = source
        };
        viewSource.GroupDescriptions.Add(new PropertyGroupDescription { PropertyName = nameof(Row.Category) });
        viewSource.Refresh();

        var itemsControl = new ItemsControl
        {
            ItemsSource = viewSource
        };
        itemsControl.GroupStyle.Add(new GroupStyle());

        Assert.NotEmpty(itemsControl.GetItemContainersForPresenter());
        Assert.IsType<GroupItem>(itemsControl.GetItemContainersForPresenter()[0]);
    }

    private sealed record Row(string Name, string Category);
}
