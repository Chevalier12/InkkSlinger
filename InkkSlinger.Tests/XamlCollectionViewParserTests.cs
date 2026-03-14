using System;
using System.Collections.ObjectModel;
using Xunit;

namespace InkkSlinger.Tests;

public class XamlCollectionViewParserTests
{
    [Fact]
    public void CollectionViewSource_InResources_CanBeBoundAsItemsSource()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <CollectionViewSource x:Key="RowsView" />
  </UserControl.Resources>
  <Grid>
    <ListBox x:Name="RowsList" ItemsSource="{StaticResource RowsView}" />
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var viewSource = Assert.IsType<CollectionViewSource>(root.Resources["RowsView"]);
        var list = Assert.IsType<ListBox>(root.FindName("RowsList"));

        viewSource.Source = new ObservableCollection<string> { "a", "b", "c" };
        viewSource.Refresh();

        Assert.Equal(3, list.GetItemContainersForPresenter().Count);
    }

    [Fact]
    public void CollectionViewSource_SortAndGroupPropertyElements_Parse()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <CollectionViewSource x:Key="RowsView">
      <CollectionViewSource.SortDescriptions>
        <SortDescription PropertyName="Name" Direction="Descending" />
      </CollectionViewSource.SortDescriptions>
      <CollectionViewSource.GroupDescriptions>
        <PropertyGroupDescription PropertyName="Category" />
      </CollectionViewSource.GroupDescriptions>
    </CollectionViewSource>
  </UserControl.Resources>
  <Grid />
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var viewSource = Assert.IsType<CollectionViewSource>(root.Resources["RowsView"]);

        Assert.Single(viewSource.SortDescriptions);
        Assert.Equal("Name", viewSource.SortDescriptions[0].PropertyName);
        Assert.Single(viewSource.GroupDescriptions);
        Assert.IsType<PropertyGroupDescription>(viewSource.GroupDescriptions[0]);
    }

    [Fact]
    public void ItemsControl_WithItemsSourceAndInlineChildren_Throws()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <CollectionViewSource x:Key="RowsView" />
  </UserControl.Resources>
  <Grid>
    <ListBox ItemsSource="{StaticResource RowsView}">
      <Label Content="child" />
    </ListBox>
  </Grid>
</UserControl>
""";

        var ex = Assert.ThrowsAny<Exception>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("ItemsSource", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
