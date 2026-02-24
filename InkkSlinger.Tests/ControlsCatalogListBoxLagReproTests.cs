using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogListBoxLagReproTests
{
    [Fact]
    public void CatalogFlow_ClickListBoxItem_ShouldSelectOnFirstClick()
    {
        var root = BuildCatalogShell(out var listBoxButton, out var previewHost);
        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 1400, 900, 16);

        // Step 1: navigate to the ListBox pane from the left control list.
        var buttonPoint = GetCenter(listBoxButton.LayoutSlot);
        Click(uiRoot, buttonPoint);
        RunLayout(uiRoot, 1400, 900, 32);

        var listBox = Assert.IsType<ListBox>(previewHost.Content);
        RunLayout(uiRoot, 1400, 900, 48);

        // Step 2: move pointer from the left pane to the right pane then click list item.
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(buttonPoint.X, buttonPoint.Y), pointerMoved: true));
        var itemPoint = GetFirstVisibleItemCenter(listBox);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(itemPoint, pointerMoved: true));
        Click(uiRoot, itemPoint);

        // Repro expectation for lag: if this fails in CI/manual, we can confirm delayed selection.
        Assert.Equal(0, listBox.SelectedIndex);
    }

    [Fact]
    public void CatalogFlow_ImmediateItemClickAfterPaneSwitch_ShouldStillSelectImmediately()
    {
        var root = BuildCatalogShell(out var listBoxButton, out var previewHost);
        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 1400, 900, 16);

        var buttonPoint = GetCenter(listBoxButton.LayoutSlot);
        Click(uiRoot, buttonPoint);

        // No additional layout/update phase here on purpose:
        // this reproduces rapid manual interaction where user switches pane and clicks item immediately.
        RunLayout(uiRoot, 1400, 900, 32);
        var listBox = Assert.IsType<ListBox>(previewHost.Content);
        var itemPoint = GetFirstVisibleItemCenter(listBox);
        Click(uiRoot, itemPoint);

        Assert.Equal(0, listBox.SelectedIndex);
    }

    private static Grid BuildCatalogShell(out Button listBoxButton, out ContentControl previewHost)
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280f) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

        var leftButtons = new StackPanel();
        for (var i = 0; i < 6; i++)
        {
            leftButtons.AddChild(new Button { Text = $"Control {i:00}", Margin = new Thickness(0f, 0f, 0f, 4f) });
        }

        listBoxButton = new Button { Text = "ListBox", Margin = new Thickness(0f, 0f, 0f, 4f) };
        leftButtons.AddChild(listBoxButton);
        for (var i = 6; i < 90; i++)
        {
            leftButtons.AddChild(new Button { Text = $"Control {i:00}", Margin = new Thickness(0f, 0f, 0f, 4f) });
        }

        var leftScroll = new ScrollViewer
        {
            Content = leftButtons,
            Height = 640f
        };

        var leftBorder = new Border
        {
            Padding = new Thickness(8f),
            Child = leftScroll
        };
        Grid.SetColumn(leftBorder, 0);
        root.AddChild(leftBorder);

        var previewHostLocal = new ContentControl();
        var rightBorder = new Border
        {
            Padding = new Thickness(8f),
            Child = previewHostLocal
        };
        Grid.SetColumn(rightBorder, 1);
        root.AddChild(rightBorder);

        previewHostLocal.Content = new Label { Text = "No selection yet" };
        listBoxButton.Click += (_, _) =>
        {
            var listBox = new ListBox();
            listBox.Items.Add("Alpha");
            listBox.Items.Add("Beta");
            listBox.Items.Add("Gamma");
            previewHostLocal.Content = listBox;
        };

        previewHost = previewHostLocal;
        return root;
    }

    private static Vector2 GetFirstVisibleItemCenter(ListBox listBox)
    {
        foreach (var visualChild in listBox.GetVisualChildren())
        {
            if (visualChild is not ScrollViewer scrollViewer)
            {
                continue;
            }

            foreach (var child in scrollViewer.GetVisualChildren())
            {
                if (child is not Panel panel || panel.Children.Count == 0)
                {
                    continue;
                }

                for (var i = 0; i < panel.Children.Count; i++)
                {
                    if (panel.Children[i] is FrameworkElement item && item.LayoutSlot.Height > 0f)
                    {
                        return GetCenter(item.LayoutSlot);
                    }
                }
            }
        }

        throw new InvalidOperationException("Could not resolve a visible ListBox item click point.");
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width * 0.5f), rect.Y + (rect.Height * 0.5f));
    }

    private static void Click(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));
    }

    private static InputDelta CreatePointerDelta(
        Vector2 pointer,
        bool pointerMoved = false,
        bool leftPressed = false,
        bool leftReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = pointerMoved || leftPressed || leftReleased,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }
}
