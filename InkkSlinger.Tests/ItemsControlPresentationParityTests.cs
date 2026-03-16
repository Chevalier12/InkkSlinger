using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ItemsControlPresentationParityTests
{
    [Fact]
    public void DisplayMemberPath_UsesProjectedPropertyText()
    {
        var listBox = new ListBox
        {
            DisplayMemberPath = nameof(Row.Name)
        };
        listBox.Items.Add(new Row("Alpha", "Art"));

        var uiRoot = BuildUiRootWithSingleChild(listBox);
        RunLayout(uiRoot);

        var hostPanel = FindItemsHostPanel(listBox);
        var item = Assert.IsType<ListBoxItem>(hostPanel.Children[0]);
        var label = Assert.IsType<Label>(Assert.Single(item.GetVisualChildren()));
        Assert.Equal("Alpha", Label.ExtractAutomationText(label.Content));
    }

    [Fact]
    public void ItemStringFormat_FormatsDisplayText()
    {
        var listBox = new ListBox
        {
            DisplayMemberPath = nameof(Row.Name),
            ItemStringFormat = "[{0}]"
        };
        listBox.Items.Add(new Row("Alpha", "Art"));

        var uiRoot = BuildUiRootWithSingleChild(listBox);
        RunLayout(uiRoot);

        var hostPanel = FindItemsHostPanel(listBox);
        var item = Assert.IsType<ListBoxItem>(hostPanel.Children[0]);
        var label = Assert.IsType<Label>(Assert.Single(item.GetVisualChildren()));
        Assert.Equal("[Alpha]", Label.ExtractAutomationText(label.Content));
    }

    [Fact]
    public void ItemContainerStyleSelector_AppliesPerItemContainerStyle()
    {
        var listBox = new ListBox();
        listBox.Items.Add(new Row("Alpha", "Art"));
        listBox.Items.Add(new Row("Beta", "UI"));
        listBox.ItemContainerStyleSelector = new CategoryStyleSelector();

        var uiRoot = BuildUiRootWithSingleChild(listBox);
        RunLayout(uiRoot);

        var hostPanel = FindItemsHostPanel(listBox);
        var first = Assert.IsType<ListBoxItem>(hostPanel.Children[0]);
        var second = Assert.IsType<ListBoxItem>(hostPanel.Children[1]);
        Assert.Equal(new Color(0x44, 0x66, 0x88), first.Background);
        Assert.Equal(new Color(0x88, 0x44, 0x44), second.Background);
    }

    [Fact]
    public void ItemsPanel_UsesCustomPanelHost()
    {
        var listBox = new ListBox
        {
            ItemsPanel = new ItemsPanelTemplate(static () => new TestItemsPanel())
        };
        listBox.Items.Add(new Row("Alpha", "Art"));

        var uiRoot = BuildUiRootWithSingleChild(listBox);
        RunLayout(uiRoot);

        var hostPanel = FindItemsHostPanel(listBox);
        Assert.IsType<TestItemsPanel>(hostPanel);
    }

    [Fact]
    public void TemplatePartScrollViewer_HostsItemsInsideTemplatedScrollViewer()
    {
        var listBox = new ListBox
        {
            DisplayMemberPath = nameof(Row.Name),
            Template = new ControlTemplate(static _ => new ScrollViewer
            {
                Name = "PART_ScrollViewer",
                BorderThickness = 0f,
                Background = Color.Transparent
            })
        };
        listBox.Items.Add(new Row("Alpha", "Art"));

        var uiRoot = BuildUiRootWithSingleChild(listBox);
        RunLayout(uiRoot);

        var visualChildren = listBox.GetVisualChildren().ToList();
        var scrollViewer = Assert.IsType<ScrollViewer>(Assert.Single(visualChildren));
        var hostPanel = FindItemsHostPanel(listBox);
        Assert.Same(hostPanel, scrollViewer.Content);

        var item = Assert.IsType<ListBoxItem>(hostPanel.Children[0]);
        var label = Assert.IsType<Label>(Assert.Single(item.GetVisualChildren()));
        Assert.Equal("Alpha", Label.ExtractAutomationText(label.Content));
    }

    [Fact]
    public void ListBox_GroupStyle_WithGroupedView_ProjectsGroupContainers()
    {
        var rows = new ObservableCollection<Row>
        {
            new("Alpha", "Art"),
            new("Beta", "UI"),
            new("Gamma", "Art")
        };
        var viewSource = new CollectionViewSource
        {
            Source = rows
        };
        viewSource.GroupDescriptions.Add(new PropertyGroupDescription { PropertyName = nameof(Row.Category) });
        viewSource.Refresh();

        var listBox = new ListBox
        {
            ItemsSource = viewSource
        };
        listBox.GroupStyle.Add(new GroupStyle());

        Assert.NotEmpty(listBox.GetItemContainersForPresenter());
        Assert.IsType<GroupItem>(listBox.GetItemContainersForPresenter()[0]);
    }

    private static UiRoot BuildUiRootWithSingleChild(UIElement child)
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 480f, 360f));
        child.SetLayoutSlot(new LayoutRect(20f, 20f, 240f, 260f));
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

    private sealed record Row(string Name, string Category);

    private sealed class TestItemsPanel : StackPanel
    {
    }

    private sealed class CategoryStyleSelector : StyleSelector
    {
        private readonly Style _artStyle;
        private readonly Style _uiStyle;

        public CategoryStyleSelector()
        {
            _artStyle = new Style(typeof(ListBoxItem));
            _artStyle.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new Color(0x44, 0x66, 0x88)));

            _uiStyle = new Style(typeof(ListBoxItem));
            _uiStyle.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new Color(0x88, 0x44, 0x44)));
        }

        public override Style? SelectStyle(object? item, DependencyObject container)
        {
            _ = container;
            return item is Row row && string.Equals(row.Category, "Art", StringComparison.Ordinal)
                ? _artStyle
                : _uiStyle;
        }
    }
}