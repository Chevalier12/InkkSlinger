using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public class ListBoxSelectionScrollIntoViewTests
{
    [Fact]
    public void SelectingLastItem_ScrollsSoItemIsFullyVisible()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 500f, 700f));

        var listBox = new ListBox();
        listBox.SetLayoutSlot(new LayoutRect(20f, 20f, 360f, 620f));
        for (var i = 0; i < 80; i++)
        {
            listBox.Items.Add($"Item {i + 1:000}");
        }

        root.AddChild(listBox);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        RunLayout(uiRoot, 500, 700, 16);

        listBox.SelectedIndex = 79;
        RunLayout(uiRoot, 500, 700, 32);

        var scrollViewer = FindScrollViewer(listBox);
        var hostPanel = FindHostPanel(scrollViewer);
        var selectedContainer = Assert.IsType<ListBoxItem>(hostPanel.Children[79]);

        var itemTop = selectedContainer.LayoutSlot.Y - hostPanel.LayoutSlot.Y;
        var itemBottom = itemTop + selectedContainer.LayoutSlot.Height;
        var viewportTop = scrollViewer.VerticalOffset;
        var viewportBottom = viewportTop + scrollViewer.ViewportHeight;

        Assert.True(itemTop >= viewportTop - 0.01f);
        Assert.True(itemBottom <= viewportBottom + 0.01f);
    }

    private static ScrollViewer FindScrollViewer(ListBox listBox)
    {
        foreach (var child in listBox.GetVisualChildren())
        {
            if (child is ScrollViewer viewer)
            {
                return viewer;
            }
        }

        throw new InvalidOperationException("Expected ListBox to expose a ScrollViewer visual child.");
    }

    private static Panel FindHostPanel(ScrollViewer scrollViewer)
    {
        foreach (var child in scrollViewer.GetVisualChildren())
        {
            if (child is Panel panel)
            {
                return panel;
            }
        }

        throw new InvalidOperationException("Expected ScrollViewer to expose a panel host.");
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {            uiRoot.Update(
                new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
                new Viewport(0, 0, width, height));
    }
}

