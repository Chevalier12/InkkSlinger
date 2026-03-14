using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class CollectionViewParityLayoutReproTests
{
    [Fact]
    public void SourceEditsActionRow_DoesNotStretchButtonsToContainerHeight_InVerticalStackPanel()
    {
        var stack = new StackPanel();

        // Mirrors the "Add Item / Remove Current" row shape in CollectionViewParityDemoView.
        var actionRow = new Grid();
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8f, GridUnitType.Pixel) });
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

        var addButton = new Button { Text = "Add Item" };
        Grid.SetColumn(addButton, 0);
        actionRow.AddChild(addButton);

        var removeButton = new Button { Text = "Remove Current" };
        Grid.SetColumn(removeButton, 2);
        actionRow.AddChild(removeButton);

        stack.AddChild(actionRow);

        var available = new Vector2(340f, 600f);
        stack.Measure(available);
        stack.Arrange(new LayoutRect(0f, 0f, available.X, available.Y));

        Assert.True(actionRow.ActualHeight < 100f, $"Expected action row to size to content height, got {actionRow.ActualHeight:0.##}.");
        Assert.True(addButton.ActualHeight < 100f, $"Expected Add Item button to size to content height, got {addButton.ActualHeight:0.##}.");
        Assert.True(removeButton.ActualHeight < 100f, $"Expected Remove Current button to size to content height, got {removeButton.ActualHeight:0.##}.");
    }

    [Fact]
    public void ListBoxInVerticalStackPanel_RemainsFiniteAndVisible_WhenMeasuredWithInfiniteStackAxis()
    {
        var stack = new StackPanel();
        stack.AddChild(new Label { Content = "ListBox" });

        var listBox = new ListBox();
        listBox.Items.Add("One");
        listBox.Items.Add("Two");
        listBox.Items.Add("Three");
        stack.AddChild(listBox);

        var available = new Vector2(640f, 420f);
        stack.Measure(available);
        stack.Arrange(new LayoutRect(0f, 0f, available.X, available.Y));

        Assert.True(listBox.ActualHeight > 10f, $"Expected listbox to remain visible, got height {listBox.ActualHeight:0.##}.");
        Assert.True(float.IsFinite(listBox.ActualHeight), $"Expected finite listbox height, got {listBox.ActualHeight}.");
        Assert.True(float.IsFinite(stack.DesiredSize.Y), $"Expected finite stack desired height, got {stack.DesiredSize.Y}.");
    }
}
