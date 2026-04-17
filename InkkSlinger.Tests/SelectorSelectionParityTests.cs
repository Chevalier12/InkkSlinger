using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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

    [Fact]
    public void MouseLeftButtonDown_SelectsClickedListBoxItem()
    {
        var listBox = new ListBox();
        listBox.Items.Add("Alpha");
        listBox.Items.Add("Beta");

        var uiRoot = BuildUiRootWithSingleChild(listBox);
        RunLayout(uiRoot);

        var hostPanel = FindItemsHostPanel(listBox);
        var secondItem = Assert.IsType<ListBoxItem>(hostPanel.Children[1]);
        var point = GetCenter(secondItem.LayoutSlot);

        RaiseMouseLeftButtonRoute(secondItem, point);

        Assert.Equal(1, listBox.SelectedIndex);
        Assert.True(secondItem.IsSelected);
    }

    [Fact]
    public void EmbeddedColorPicker_PreviewHandling_BlocksListBoxSelection()
    {
        var picker = new ColorPicker
        {
            Width = 180f,
            Height = 140f
        };
        var listBox = new ListBox();
        listBox.Items.Add("Alpha");
        listBox.Items.Add(picker);
        listBox.SelectedIndex = 0;

        var uiRoot = BuildUiRootWithSingleChild(listBox);
        RunLayout(uiRoot);

        var hostPanel = FindItemsHostPanel(listBox);
        var secondItem = Assert.IsType<ListBoxItem>(hostPanel.Children[1]);
        var point = GetCenter(picker.LayoutSlot);

        RaiseMouseLeftButtonRoute(picker, point);

        Assert.True(point.X >= picker.LayoutSlot.X && point.Y >= picker.LayoutSlot.Y);
        Assert.Equal(0, listBox.SelectedIndex);
        Assert.False(secondItem.IsSelected);
    }

    [Fact]
    public void EmbeddedColorSpectrum_PreviewHandling_BlocksListBoxSelection()
    {
        var spectrum = new ColorSpectrum
        {
            Width = 180f,
            Height = 24f,
            Orientation = Orientation.Horizontal,
            Mode = ColorSpectrumMode.Hue
        };
        var listBox = new ListBox();
        listBox.Items.Add("Alpha");
        listBox.Items.Add(spectrum);
        listBox.SelectedIndex = 0;

        var uiRoot = BuildUiRootWithSingleChild(listBox);
        RunLayout(uiRoot);

        var hostPanel = FindItemsHostPanel(listBox);
        var secondItem = Assert.IsType<ListBoxItem>(hostPanel.Children[1]);
        var point = GetCenter(spectrum.LayoutSlot);

        RaiseMouseLeftButtonRoute(spectrum, point);

        Assert.Equal(0, listBox.SelectedIndex);
        Assert.False(secondItem.IsSelected);
    }

    private sealed class Row
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;
    }

    private static UiRoot BuildUiRootWithSingleChild(UIElement child)
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 480f, 360f));
        child.SetLayoutSlot(new LayoutRect(20f, 20f, 260f, 280f));
        root.AddChild(child);
        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        return uiRoot;
    }

    private static Panel FindItemsHostPanel(ListBox listBox)
    {
        foreach (var child in listBox.GetVisualChildren())
        {
            if (child is not ScrollViewer scrollViewer)
            {
                continue;
            }

            foreach (var scrollChild in scrollViewer.GetVisualChildren())
            {
                if (scrollChild is Panel panel)
                {
                    return panel;
                }
            }
        }

        throw new InvalidOperationException("Expected ListBox to expose a ScrollViewer panel host.");
    }

    private static void RunLayout(UiRoot uiRoot)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 480, 360));
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));
    }

    private static void RaiseMouseLeftButtonRoute(UIElement target, Vector2 point)
    {
        var args = new MouseRoutedEventArgs(UIElement.PreviewMouseDownEvent, point, MouseButton.Left);
        target.RaiseRoutedEventInternal(UIElement.PreviewMouseDownEvent, args);
        target.RaiseRoutedEventInternal(UIElement.PreviewMouseLeftButtonDownEvent, args);
        target.RaiseRoutedEventInternal(UIElement.MouseDownEvent, args);
        target.RaiseRoutedEventInternal(UIElement.MouseLeftButtonDownEvent, args);
    }
}