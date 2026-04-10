using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ListBoxItemTemplateParityTests
{
    [Fact]
    public void ItemTemplate_KeepsGeneratedListBoxItemContainers()
    {
        var listBox = new ListBox
        {
            Width = 280f,
            Height = 180f,
            ItemTemplate = new DataTemplate((item, _) =>
            {
                var text = new TextBlock();
                text.Text = $"templated:{item}";
                return text;
            })
        };
        listBox.Items.Add("Alpha");
        listBox.Items.Add("Beta");

        var uiRoot = BuildUiRootWithSingleChild(listBox, 420, 260);
        RunLayout(uiRoot, 420, 260);

        var hostPanel = FindItemsHostPanel(listBox);
        var firstItem = Assert.IsType<ListBoxItem>(hostPanel.Children[0]);
        Assert.Equal("Alpha", firstItem.Content);

        var textBlock = FindDescendant<TextBlock>(firstItem);
        Assert.Equal("templated:Alpha", textBlock.Text);
    }

    [Fact]
    public void ItemTemplate_DoesNotReplaceExplicitListBoxItemContainers()
    {
        var explicitContainer = new ListBoxItem
        {
            Content = "Explicit"
        };

        var listBox = new ListBox
        {
            Width = 280f,
            Height = 180f,
            ItemTemplate = new DataTemplate((item, _) =>
            {
                var text = new TextBlock();
                text.Text = $"templated:{item}";
                return text;
            })
        };
        listBox.Items.Add(explicitContainer);

        var uiRoot = BuildUiRootWithSingleChild(listBox, 420, 260);
        RunLayout(uiRoot, 420, 260);

        var hostPanel = FindItemsHostPanel(listBox);
        var hostedContainer = Assert.IsType<ListBoxItem>(hostPanel.Children[0]);
        Assert.Same(explicitContainer, hostedContainer);
        Assert.Equal("Explicit", hostedContainer.Content);
    }

    [Fact]
    public void ListView_ItemTemplate_KeepsGeneratedListViewItemContainers()
    {
        var listView = new ListView
        {
            Width = 280f,
            Height = 180f,
            ItemTemplate = new DataTemplate((item, _) =>
            {
                var text = new TextBlock();
                text.Text = $"templated:{item}";
                return text;
            })
        };
        listView.Items.Add("Alpha");

        var uiRoot = BuildUiRootWithSingleChild(listView, 420, 260);
        RunLayout(uiRoot, 420, 260);

        var hostPanel = FindItemsHostPanel(listView);
        var firstItem = Assert.IsType<ListViewItem>(hostPanel.Children[0]);
        Assert.Equal("Alpha", firstItem.Content);
    }

    private static UiRoot BuildUiRootWithSingleChild(UIElement child, int width, int height)
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, width, height));
        child.SetLayoutSlot(new LayoutRect(20f, 20f, width - 40f, height - 40f));
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

    private static TElement FindDescendant<TElement>(UIElement root)
        where TElement : UIElement
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (child is TElement match)
            {
                return match;
            }

            var descendant = FindDescendantOrDefault<TElement>(child);
            if (descendant != null)
            {
                return descendant;
            }
        }

        throw new InvalidOperationException($"Expected descendant of type '{typeof(TElement).Name}'.");
    }

    private static TElement? FindDescendantOrDefault<TElement>(UIElement root)
        where TElement : UIElement
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (child is TElement match)
            {
                return match;
            }

            var descendant = FindDescendantOrDefault<TElement>(child);
            if (descendant != null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, width, height));
    }
}