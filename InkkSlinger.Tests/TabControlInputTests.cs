using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class TabControlInputTests
{
    [Fact]
    public void ClickingTabHeader_ShouldSwitchSelectionAndVisibleContent()
    {
        var host = new Canvas
        {
            Width = 500f,
            Height = 320f
        };

        var firstContent = new Label { Content = "Tab One Content" };
        var secondContent = new Label { Content = "Tab Two Content" };
        var firstTab = new TabItem { Header = "Tab One", Content = firstContent };
        var secondTab = new TabItem { Header = "Tab Two", Content = secondContent };
        var tabControl = new TabControl
        {
            Width = 320f,
            Height = 220f,
            HeaderPadding = new Thickness(0f)
        };
        tabControl.Items.Add(firstTab);
        tabControl.Items.Add(secondTab);
        host.AddChild(tabControl);
        Canvas.SetLeft(tabControl, 30f);
        Canvas.SetTop(tabControl, 20f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        Assert.Equal(0, tabControl.SelectedIndex);
        Assert.True(firstTab.IsSelected);
        Assert.False(secondTab.IsSelected);

        var firstHeaderWidth = MathF.Max(
            36f,
            tabControl.HeaderPadding.Horizontal + UiTextRenderer.MeasureWidth(tabControl, firstTab.Header!, tabControl.FontSize));
        var secondHeaderPoint = new Vector2(tabControl.LayoutSlot.X + firstHeaderWidth + 4f, tabControl.LayoutSlot.Y + 4f);
        Click(uiRoot, secondHeaderPoint);
        RunLayout(uiRoot);

        Assert.Equal(1, tabControl.SelectedIndex);
        Assert.True(secondTab.IsSelected);
        Assert.False(firstTab.IsSelected);
        Assert.Same(secondTab, tabControl.SelectedItem);
    }

    [Fact]
    public void ClickingInsideContentArea_ShouldNotSwitchSelection()
    {
        var host = new Canvas
        {
            Width = 500f,
            Height = 320f
        };

        var tabControl = new TabControl
        {
            Width = 320f,
            Height = 220f,
            HeaderPadding = new Thickness(0f)
        };
        tabControl.Items.Add(new TabItem
        {
            Header = "Tab One",
            Content = new Label { Content = "First" }
        });
        tabControl.Items.Add(new TabItem
        {
            Header = "Tab Two",
            Content = new Label { Content = "Second" }
        });

        host.AddChild(tabControl);
        Canvas.SetLeft(tabControl, 30f);
        Canvas.SetTop(tabControl, 20f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);
        Assert.Equal(0, tabControl.SelectedIndex);

        var contentPoint = new Vector2(tabControl.LayoutSlot.X + 20f, tabControl.LayoutSlot.Y + 80f);
        Click(uiRoot, contentPoint);
        RunLayout(uiRoot);

        Assert.Equal(0, tabControl.SelectedIndex);
    }

    [Fact]
    public void MovingFromSelectedTabContentIntoContentButton_ShouldRetargetHoverAndClickToButton()
    {
        var host = new Canvas
        {
            Width = 500f,
            Height = 320f
        };

        var clickCount = 0;
        var contentButton = new Button
        {
            Content = "Open Diagnostics",
            Width = 140f,
            Height = 36f
        };
        contentButton.Click += (_, _) => clickCount++;

        var contentPanel = new StackPanel
        {
            Margin = new Thickness(24f, 40f, 0f, 0f)
        };
        contentPanel.AddChild(contentButton);

        var firstTab = new TabItem
        {
            Header = "Tab One",
            Content = contentPanel
        };

        var tabControl = new TabControl
        {
            Width = 320f,
            Height = 220f,
            HeaderPadding = new Thickness(0f)
        };
        tabControl.Items.Add(firstTab);
        tabControl.Items.Add(new TabItem
        {
            Header = "Tab Two",
            Content = new Label { Content = "Second" }
        });

        host.AddChild(tabControl);
        Canvas.SetLeft(tabControl, 30f);
        Canvas.SetTop(tabControl, 20f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);
        Assert.True(contentButton.LayoutSlot.Width > 0f);
        Assert.True(contentButton.LayoutSlot.Height > 0f);

        var contentEntryPoint = new Vector2(firstTab.LayoutSlot.X + 8f, firstTab.LayoutSlot.Y + 8f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(contentEntryPoint, pointerMoved: true));
        RunLayout(uiRoot);

        Assert.True(firstTab.IsMouseOver);
        Assert.False(contentButton.IsMouseOver);

        var buttonCenter = new Vector2(
            contentButton.LayoutSlot.X + (contentButton.LayoutSlot.Width * 0.5f),
            contentButton.LayoutSlot.Y + (contentButton.LayoutSlot.Height * 0.5f));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(buttonCenter, pointerMoved: true));
        RunLayout(uiRoot);

        Assert.True(contentButton.IsMouseOver);

        Click(uiRoot, buttonCenter);
        RunLayout(uiRoot);

        Assert.Equal(1, clickCount);
        Assert.Equal(0, tabControl.SelectedIndex);
    }

    [Fact]
    public void ClickingTabHeader_ShouldUpdateSelectedIndexBindingSource_ByDefault()
    {
        var host = new Canvas
        {
            Width = 500f,
            Height = 320f
        };

        var viewModel = new TabSelectionViewModel
        {
            SelectedIndex = 0
        };

        var tabControl = new TabControl
        {
            Width = 320f,
            Height = 220f,
            HeaderPadding = new Thickness(0f)
        };
        tabControl.Items.Add(new TabItem
        {
            Header = "Tab One",
            Content = new Label { Content = "First" }
        });
        tabControl.Items.Add(new TabItem
        {
            Header = "Tab Two",
            Content = new Label { Content = "Second" }
        });
        BindingOperations.SetBinding(
            tabControl,
            Selector.SelectedIndexProperty,
            new Binding
            {
                Source = viewModel,
                Path = nameof(TabSelectionViewModel.SelectedIndex)
            });

        host.AddChild(tabControl);
        Canvas.SetLeft(tabControl, 30f);
        Canvas.SetTop(tabControl, 20f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        Assert.Equal(0, tabControl.SelectedIndex);
        Assert.Equal(0, viewModel.SelectedIndex);

        var firstHeaderWidth = MathF.Max(
            36f,
            tabControl.HeaderPadding.Horizontal + UiTextRenderer.MeasureWidth(tabControl, "Tab One", tabControl.FontSize));
        var secondHeaderPoint = new Vector2(tabControl.LayoutSlot.X + firstHeaderWidth + 4f, tabControl.LayoutSlot.Y + 4f);
        Click(uiRoot, secondHeaderPoint);
        RunLayout(uiRoot);

        Assert.Equal(1, tabControl.SelectedIndex);
        Assert.Equal(1, viewModel.SelectedIndex);
    }

    private sealed class TabSelectionViewModel
    {
        public int SelectedIndex { get; set; }
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

    private static void RunLayout(UiRoot uiRoot)
    {
        uiRoot.RunLayoutForTests(new Viewport(0, 0, 500, 320));
    }
}
