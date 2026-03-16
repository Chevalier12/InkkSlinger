using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class SelectorSelectionParityTests
{
    [Fact]
    public void SelectedValuePath_ProjectsFromSelectedItem()
    {
        var listBox = new ListBox
        {
            SelectedValuePath = nameof(Row.Id)
        };
        listBox.Items.Add(new Row { Id = 1, Name = "Alpha" });
        listBox.Items.Add(new Row { Id = 2, Name = "Beta" });

        listBox.SelectedIndex = 1;

        Assert.Equal(2, listBox.SelectedValue);
    }

    [Fact]
    public void SelectedValue_SelectsMatchingItem()
    {
        var listBox = new ListBox
        {
            SelectedValuePath = nameof(Row.Id)
        };
        var first = new Row { Id = 1, Name = "Alpha" };
        var second = new Row { Id = 2, Name = "Beta" };
        listBox.Items.Add(first);
        listBox.Items.Add(second);

        listBox.SelectedValue = 2;

        Assert.Same(second, listBox.SelectedItem);
        Assert.Equal(1, listBox.SelectedIndex);
    }

    [Fact]
    public void SelectedItems_ReflectsExtendedSelection()
    {
        var first = new Row { Id = 1, Name = "Alpha" };
        var second = new Row { Id = 2, Name = "Beta" };
        var third = new Row { Id = 3, Name = "Gamma" };
        var listBox = new ListBox
        {
            SelectionMode = SelectionMode.Extended
        };
        listBox.Items.Add(first);
        listBox.Items.Add(second);
        listBox.Items.Add(third);

        listBox.SelectedIndex = 0;
        listBox.SelectedIndex = 2;

        Assert.Single(listBox.SelectedItems);
        Assert.Same(third, listBox.SelectedItems[0]);
    }

    [Fact]
    public void IsSynchronizedWithCurrentItem_PushesSelectionToView()
    {
        var rows = new ObservableCollection<Row>
        {
            new() { Id = 1, Name = "Alpha" },
            new() { Id = 2, Name = "Beta" },
            new() { Id = 3, Name = "Gamma" }
        };
        var source = new CollectionViewSource { Source = rows };
        var listBox = new ListBox
        {
            ItemsSource = source,
            IsSynchronizedWithCurrentItem = true
        };

        listBox.SelectedIndex = 1;

        Assert.Same(rows[1], source.View!.CurrentItem);
        Assert.Equal(1, source.View.CurrentPosition);
    }

    [Fact]
    public void IsSynchronizedWithCurrentItem_PullsSelectionFromView()
    {
        var rows = new ObservableCollection<Row>
        {
            new() { Id = 1, Name = "Alpha" },
            new() { Id = 2, Name = "Beta" },
            new() { Id = 3, Name = "Gamma" }
        };
        var source = new CollectionViewSource { Source = rows };
        var listBox = new ListBox
        {
            ItemsSource = source,
            IsSynchronizedWithCurrentItem = true
        };

        Assert.True(source.View!.MoveCurrentTo(rows[2]));

        Assert.Same(rows[2], listBox.SelectedItem);
        Assert.Equal(2, listBox.SelectedIndex);
    }

    private sealed class Row
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;
    }
}