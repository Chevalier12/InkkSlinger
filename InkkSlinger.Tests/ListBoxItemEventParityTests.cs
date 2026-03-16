using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ListBoxItemEventParityTests
{
    [Fact]
    public void SelectionChanges_RaiseSelectedAndUnselectedEventsThatBubble()
    {
        var listBox = new ListBox();
        listBox.Items.Add("Alpha");
        listBox.Items.Add("Beta");

        var uiRoot = BuildUiRootWithSingleChild(listBox);
        RunLayout(uiRoot);

        var hostPanel = FindItemsHostPanel(listBox);
        var firstItem = Assert.IsType<ListBoxItem>(hostPanel.Children[0]);

        var selectedCount = 0;
        var unselectedCount = 0;
        object? selectedOriginalSource = null;
        object? unselectedOriginalSource = null;

        firstItem.Selected += (_, _) => selectedCount++;
        firstItem.Unselected += (_, _) => unselectedCount++;
        listBox.AddHandler<RoutedSimpleEventArgs>(ListBoxItem.SelectedEvent, (_, args) => selectedOriginalSource = args.OriginalSource);
        listBox.AddHandler<RoutedSimpleEventArgs>(ListBoxItem.UnselectedEvent, (_, args) => unselectedOriginalSource = args.OriginalSource);

        listBox.SelectedIndex = 0;
        listBox.SelectedIndex = -1;

        Assert.Equal(1, selectedCount);
        Assert.Equal(1, unselectedCount);
        Assert.Same(firstItem, selectedOriginalSource);
        Assert.Same(firstItem, unselectedOriginalSource);
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
}