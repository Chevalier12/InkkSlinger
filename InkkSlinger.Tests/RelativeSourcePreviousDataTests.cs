using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class RelativeSourcePreviousDataTests
{
    [Fact]
    public void PreviousData_UsesProjectedCollectionViewOrder()
    {
        var rows = new ObservableCollection<Row>
        {
            new() { Name = "Alpha", SortKey = 1 },
            new() { Name = "Bravo", SortKey = 2 },
            new() { Name = "Charlie", SortKey = 3 }
        };

        var view = new CollectionViewSource
        {
            Source = rows
        };
        view.SortDescriptions.Add(new SortDescription(nameof(Row.SortKey), ListSortDirection.Descending));

        var itemsControl = new ProbeItemsControl
        {
            ItemsSource = view,
            ItemTemplate = new DataTemplate((_, _) =>
            {
                var text = new TextBlock();
                BindingOperations.SetBinding(
                    text,
                    TextBlock.TextProperty,
                    new Binding
                    {
                        Path = nameof(Row.Name),
                        RelativeSourceMode = RelativeSourceMode.PreviousData,
                        FallbackValue = "<none>"
                    });
                return text;
            })
        };

        var first = GetText(itemsControl, 0);
        var second = GetText(itemsControl, 1);
        var third = GetText(itemsControl, 2);

        Assert.Equal("<none>", first);
        Assert.Equal("Charlie", second);
        Assert.Equal("Bravo", third);
    }

    [Fact]
    public void PreviousData_OutsideItemsContext_UsesFallback()
    {
        var probe = new TextBlock();
        BindingOperations.SetBinding(
            probe,
            TextBlock.TextProperty,
            new Binding
            {
                Path = nameof(Row.Name),
                RelativeSourceMode = RelativeSourceMode.PreviousData,
                FallbackValue = "fallback"
            });

        Assert.Equal("fallback", probe.Text);
    }

    [Fact]
    public void PreviousData_ReevaluatesAfterInsert()
    {
        var itemsControl = CreateDirectItemsProbe();
        itemsControl.Items.Add(new Row { Name = "A" });
        itemsControl.Items.Add(new Row { Name = "B" });
        itemsControl.Items.Add(new Row { Name = "C" });

        itemsControl.Items.Insert(1, new Row { Name = "X" });

        Assert.Equal("<none>", GetText(itemsControl, 0));
        Assert.Equal("A", GetText(itemsControl, 1));
        Assert.Equal("X", GetText(itemsControl, 2));
        Assert.Equal("B", GetText(itemsControl, 3));
    }

    [Fact]
    public void PreviousData_ReevaluatesAfterMove()
    {
        var itemsControl = CreateDirectItemsProbe();
        itemsControl.Items.Add(new Row { Name = "A" });
        itemsControl.Items.Add(new Row { Name = "B" });
        itemsControl.Items.Add(new Row { Name = "C" });

        itemsControl.Items.Move(2, 0);

        Assert.Equal("<none>", GetText(itemsControl, 0));
        Assert.Equal("C", GetText(itemsControl, 1));
        Assert.Equal("A", GetText(itemsControl, 2));
    }

    private static ProbeItemsControl CreateDirectItemsProbe()
    {
        return new ProbeItemsControl
        {
            ItemTemplate = new DataTemplate((_, _) =>
            {
                var text = new TextBlock();
                BindingOperations.SetBinding(
                    text,
                    TextBlock.TextProperty,
                    new Binding
                    {
                        Path = nameof(Row.Name),
                        RelativeSourceMode = RelativeSourceMode.PreviousData,
                        FallbackValue = "<none>"
                    });
                return text;
            })
        };
    }

    private static string GetText(ProbeItemsControl itemsControl, int index)
    {
        var text = Assert.IsType<TextBlock>(itemsControl.RealizedContainers[index]);
        return text.Text;
    }

    private sealed class ProbeItemsControl : ItemsControl
    {
        public IReadOnlyList<UIElement> RealizedContainers => ItemContainers;
    }

    private sealed class Row
    {
        public string Name { get; init; } = string.Empty;

        public int SortKey { get; init; }
    }
}
