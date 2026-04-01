using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ComboBoxPopupEdgeParityTests
{
    [Fact]
    public void OpenDropDown_ShouldCreatePopup_WithDismissOnOutsideClick()
    {
        var (uiRoot, comboBox) = CreateFixture();

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        Assert.True(comboBox.IsDropDownOpen);
        Assert.True(comboBox.IsDropDownPopupOpenForTesting);
    }

    [Fact]
    public void ClickOnComboBox_ShouldOpenDropDown()
    {
        var (uiRoot, comboBox) = CreateFixture();
        Assert.False(comboBox.IsDropDownOpen);

        var clickPoint = new Vector2(
            comboBox.LayoutSlot.X + (comboBox.LayoutSlot.Width / 2f),
            comboBox.LayoutSlot.Y + (comboBox.LayoutSlot.Height / 2f));
        Click(uiRoot, clickPoint);

        Assert.True(comboBox.IsDropDownOpen);
        Assert.True(comboBox.IsDropDownPopupOpenForTesting);
    }

    [Fact]
    public void ClickOnComboBox_WhenOpen_ShouldCloseDropDown()
    {
        var (uiRoot, comboBox) = CreateFixture();
        var clickPoint = new Vector2(
            comboBox.LayoutSlot.X + (comboBox.LayoutSlot.Width / 2f),
            comboBox.LayoutSlot.Y + (comboBox.LayoutSlot.Height / 2f));

        Click(uiRoot, clickPoint);
        Assert.True(comboBox.IsDropDownOpen);

        Click(uiRoot, clickPoint);
        Assert.False(comboBox.IsDropDownOpen);
        Assert.False(comboBox.IsDropDownPopupOpenForTesting);
    }

    [Fact]
    public void OutsideClick_ShouldCloseDropDown_AndSyncIsDropDownOpenFalse()
    {
        var (uiRoot, comboBox) = CreateFixture();
        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);
        Assert.True(comboBox.IsDropDownPopupOpenForTesting);

        Click(uiRoot, new Vector2(6f, 6f));

        Assert.False(comboBox.IsDropDownOpen);
        Assert.False(comboBox.IsDropDownPopupOpenForTesting);
    }

    [Fact]
    public void OutsideClick_AfterClickOpeningDropDown_ShouldCloseDropDown()
    {
        var (uiRoot, comboBox) = CreateFixture();
        var clickPoint = new Vector2(
            comboBox.LayoutSlot.X + (comboBox.LayoutSlot.Width / 2f),
            comboBox.LayoutSlot.Y + (comboBox.LayoutSlot.Height / 2f));

        Click(uiRoot, clickPoint);
        Assert.True(comboBox.IsDropDownOpen);
        Assert.True(comboBox.IsDropDownPopupOpenForTesting);

        Click(uiRoot, new Vector2(6f, 6f));

        Assert.False(comboBox.IsDropDownOpen);
        Assert.False(comboBox.IsDropDownPopupOpenForTesting);
    }

    [Fact]
    public void SelectionFromDropDown_ShouldCloseDropDown_AndPersistSelection()
    {
        var (uiRoot, comboBox) = CreateFixture();
        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        var dropDown = comboBox.DropDownListForTesting;
        Assert.NotNull(dropDown);
        dropDown!.SelectedIndex = 1;

        Assert.Equal(1, comboBox.SelectedIndex);
        Assert.False(comboBox.IsDropDownOpen);
        Assert.False(comboBox.IsDropDownPopupOpenForTesting);
    }

    [Fact]
    public void DropDown_ShouldUseComboBoxItemContainers_AndApplyItemContainerStyle()
    {
        var (uiRoot, comboBox) = CreateFixture();
        var expectedBackground = new Color(0x22, 0x55, 0x99);
        var style = new Style(typeof(ComboBoxItem));
        style.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, expectedBackground));
        comboBox.ItemContainerStyle = style;

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        var dropDown = comboBox.DropDownListForTesting;
        Assert.NotNull(dropDown);

        var hostPanel = FindItemsHostPanel(dropDown!);
        var firstItem = Assert.IsType<ComboBoxItem>(hostPanel.Children[0]);
        Assert.Equal(expectedBackground, firstItem.Background);
    }

    [Fact]
    public void OpenDropDown_WithFewItems_ShouldSizeViewportToContent()
    {
        var (uiRoot, comboBox) = CreateFixture();

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        var dropDown = Assert.IsType<ListBox>(comboBox.DropDownListForTesting);
        var scrollViewer = FindScrollViewer(dropDown);

        Assert.True(scrollViewer.ExtentHeight > 0f, $"Expected dropdown extent height to be positive, got {scrollViewer.ExtentHeight:0.##}.");
        Assert.InRange(
            scrollViewer.ViewportHeight - scrollViewer.ExtentHeight,
            -0.5f,
            6f);
        Assert.True(
            dropDown.LayoutSlot.Height < comboBox.MaxDropDownHeight - 20f,
            $"Expected dropdown with few items to size below the max cap. height={dropDown.LayoutSlot.Height:0.##} max={comboBox.MaxDropDownHeight:0.##} viewport={scrollViewer.ViewportHeight:0.##} extent={scrollViewer.ExtentHeight:0.##}");
    }

    [Fact]
    public void DropDown_InNestedScrollViewerUserControl_ShouldAnchorToComboBoxInsteadOfFlowingAfterSiblingContent()
    {
        var rootView = new UserControl();
        var rootGrid = new Grid();
        rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1f, GridUnitType.Star) });
        rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360f, GridUnitType.Pixel) });
        rootView.Content = rootGrid;

        var filler = new Border();
        Grid.SetColumn(filler, 0);
        rootGrid.AddChild(filler);

        var scrollViewer = new ScrollViewer();
        Grid.SetColumn(scrollViewer, 1);
        rootGrid.AddChild(scrollViewer);

        var sidebar = new StackPanel();
        scrollViewer.Content = sidebar;
        sidebar.AddChild(new Label { Content = "Payload lab" });
        sidebar.AddChild(new Label { Content = "Round-trip full documents or the active selection." });

        var comboBox = new ComboBox
        {
            Width = 320f,
            Height = 40f,
            Margin = new Thickness(0f, 8f, 0f, 0f)
        };
        comboBox.Items.Add("Flow XML");
        comboBox.Items.Add("XAML");
        comboBox.Items.Add("XamlPackage");
        comboBox.Items.Add("Rich Text Format");
        comboBox.Items.Add("Plain Text");
        comboBox.SelectedIndex = 0;
        sidebar.AddChild(comboBox);

        var buttonRow = new WrapPanel { Margin = new Thickness(0f, 8f, 0f, 0f) };
        buttonRow.AddChild(new Button { Content = "Export Doc", Width = 112f, Height = 36f, Margin = new Thickness(0f, 0f, 8f, 8f) });
        buttonRow.AddChild(new Button { Content = "Export Selection", Width = 144f, Height = 36f, Margin = new Thickness(0f, 0f, 8f, 8f) });
        buttonRow.AddChild(new Button { Content = "Load Doc", Width = 112f, Height = 36f, Margin = new Thickness(0f, 0f, 8f, 8f) });
        buttonRow.AddChild(new Button { Content = "Load Selection", Width = 144f, Height = 36f, Margin = new Thickness(0f, 0f, 8f, 8f) });
        sidebar.AddChild(buttonRow);
        sidebar.AddChild(new TextBox { Width = 320f, Height = 220f, Margin = new Thickness(0f, 8f, 0f, 0f) });

        var uiRoot = new UiRoot(rootView);
        RunLayout(uiRoot, 1200, 900);

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot, 1200, 900);

        var dropDown = comboBox.DropDownListForTesting;
        Assert.NotNull(dropDown);
        Assert.InRange(dropDown!.LayoutSlot.X - comboBox.LayoutSlot.X, 0f, 4f);
        Assert.InRange(dropDown.LayoutSlot.Y - (comboBox.LayoutSlot.Y + comboBox.LayoutSlot.Height), 0f, 8f);
    }

    [Fact]
    public void DropDown_InScrolledScrollViewer_ShouldAnchorToRenderedComboBoxBounds()
    {
        var (uiRoot, scrollViewer, comboBox) = CreateScrolledSidebarFixture();

        scrollViewer.ScrollToVerticalOffset(96f);
        RunLayout(uiRoot, 1200, 900);

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot, 1200, 900);

        var dropDown = comboBox.DropDownListForTesting;
        Assert.NotNull(dropDown);

        var renderedComboBoxY = comboBox.LayoutSlot.Y - scrollViewer.VerticalOffset;
        var renderedDropDownY = dropDown!.LayoutSlot.Y;
        Assert.InRange(dropDown.LayoutSlot.X - comboBox.LayoutSlot.X, 0f, 4f);
        Assert.InRange(renderedDropDownY - (renderedComboBoxY + comboBox.LayoutSlot.Height), 0f, 8f);
    }

    [Fact]
    public void ScrollViewerScroll_ShouldCloseOpenComboBoxDropDown()
    {
        var (uiRoot, scrollViewer, comboBox) = CreateScrolledSidebarFixture();

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot, 1200, 900);
        Assert.True(comboBox.IsDropDownPopupOpenForTesting);

        scrollViewer.ScrollToVerticalOffset(64f);
        RunLayout(uiRoot, 1200, 900);

        Assert.False(comboBox.IsDropDownOpen);
        Assert.False(comboBox.IsDropDownPopupOpenForTesting);
    }

    [Fact]
    public void OpenDropDown_ShouldNotReflowSiblingRows_InLocalGrid()
    {
        var root = new Panel
        {
            Width = 840f,
            Height = 520f
        };

        var rightColumn = new Grid();
        rightColumn.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rightColumn.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rightColumn.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.AddChild(rightColumn);

        var comboBox = new ComboBox
        {
            Width = 260f,
            Height = 32f,
            Margin = new Thickness(0f, 8f, 0f, 0f)
        };
        comboBox.Items.Add("Alpha");
        comboBox.Items.Add("Beta");
        comboBox.Items.Add("Gamma");
        comboBox.SelectedIndex = 0;
        Grid.SetRow(comboBox, 0);
        rightColumn.AddChild(comboBox);

        var siblingButton = new Button
        {
            Content = "Open Combo Dropdown",
            Height = 30f,
            Margin = new Thickness(0f, 10f, 0f, 0f)
        };
        Grid.SetRow(siblingButton, 1);
        rightColumn.AddChild(siblingButton);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot);
        var siblingYBefore = siblingButton.LayoutSlot.Y;

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);
        var siblingYAfter = siblingButton.LayoutSlot.Y;

        Assert.True(comboBox.IsDropDownPopupOpenForTesting);
        Assert.Equal(siblingYBefore, siblingYAfter);
    }

    [Fact]
    public void ClickingDropDownItem_OverlappingButton_ShouldNotClickUnderlyingButton()
    {
        var host = new Canvas
        {
            Width = 480f,
            Height = 280f
        };

        var comboBox = new ComboBox
        {
            Width = 220f,
            Height = 32f
        };
        comboBox.Items.Add("Alpha");
        comboBox.Items.Add("Beta");
        comboBox.Items.Add("Gamma");
        comboBox.SelectedIndex = 2;
        host.AddChild(comboBox);
        Canvas.SetLeft(comboBox, 24f);
        Canvas.SetTop(comboBox, 24f);

        var openButtonClicks = 0;
        var openDropDownButton = new Button
        {
            Content = "Open Combo Dropdown",
            Width = 240f,
            Height = 32f
        };
        openDropDownButton.Click += (_, _) =>
        {
            openButtonClicks++;
            comboBox.IsDropDownOpen = true;
        };
        host.AddChild(openDropDownButton);
        Canvas.SetLeft(openDropDownButton, 24f);
        Canvas.SetTop(openDropDownButton, 58f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        // First click opens the dropdown through the underlying button, mirroring the lab interaction.
        Click(uiRoot, new Vector2(openDropDownButton.LayoutSlot.X + 8f, openDropDownButton.LayoutSlot.Y + 8f));
        Assert.Equal(1, openButtonClicks);
        Assert.True(comboBox.IsDropDownOpen);

        RunLayout(uiRoot);

        var dropDown = comboBox.DropDownListForTesting;
        Assert.NotNull(dropDown);

        // Click inside the first row (Alpha). This point intentionally overlaps the button's bounds.
        var clickPoint = new Vector2(dropDown!.LayoutSlot.X + 12f, dropDown.LayoutSlot.Y + 12f);
        Click(uiRoot, clickPoint);

        Assert.Equal(1, openButtonClicks);
        Assert.Equal(0, comboBox.SelectedIndex);
    }

    [Fact]
    public void AfterDropDownCloses_ClickingFormerItemArea_ShouldHitUnderlyingButton()
    {
        var host = new Canvas
        {
            Width = 520f,
            Height = 320f
        };

        var comboBox = new ComboBox
        {
            Width = 220f,
            Height = 32f
        };
        comboBox.Items.Add("Alpha");
        comboBox.Items.Add("Beta");
        comboBox.Items.Add("Gamma");
        comboBox.SelectedIndex = 2;
        host.AddChild(comboBox);
        Canvas.SetLeft(comboBox, 24f);
        Canvas.SetTop(comboBox, 24f);

        var buttonClicks = 0;
        var underneathButton = new Button
        {
            Content = "Underneath",
            Width = 260f,
            Height = 32f
        };
        underneathButton.Click += (_, _) => buttonClicks++;
        host.AddChild(underneathButton);
        Canvas.SetLeft(underneathButton, 24f);
        Canvas.SetTop(underneathButton, 58f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        var dropDown = comboBox.DropDownListForTesting;
        Assert.NotNull(dropDown);
        var firstItemPoint = new Vector2(dropDown!.LayoutSlot.X + 12f, dropDown.LayoutSlot.Y + 12f);

        // Select Alpha (dropdown should close).
        Click(uiRoot, firstItemPoint);
        RunLayout(uiRoot);
        Assert.False(comboBox.IsDropDownOpen);
        Assert.Equal(0, comboBox.SelectedIndex);
        Assert.IsType<Button>(VisualTreeHelper.HitTest(host, firstItemPoint));

        // Clicking the same coordinates again should now hit the underlying button.
        Click(uiRoot, firstItemPoint);

        Assert.Equal(1, buttonClicks);
    }

    private static (UiRoot UiRoot, ComboBox ComboBox) CreateFixture()
    {
        var host = new Canvas
        {
            Width = 420f,
            Height = 260f
        };

        var comboBox = new ComboBox
        {
            Width = 180f,
            Height = 36f
        };
        comboBox.Items.Add("Alpha");
        comboBox.Items.Add("Beta");
        comboBox.Items.Add("Gamma");
        comboBox.SelectedIndex = 0;
        host.AddChild(comboBox);
        Canvas.SetLeft(comboBox, 120f);
        Canvas.SetTop(comboBox, 90f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);
        return (uiRoot, comboBox);
    }

    private static (UiRoot UiRoot, ScrollViewer ScrollViewer, ComboBox ComboBox) CreateScrolledSidebarFixture()
    {
        var rootView = new UserControl();
        var rootGrid = new Grid();
        rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1f, GridUnitType.Star) });
        rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360f, GridUnitType.Pixel) });
        rootView.Content = rootGrid;

        var filler = new Border();
        Grid.SetColumn(filler, 0);
        rootGrid.AddChild(filler);

        var scrollViewer = new ScrollViewer();
        Grid.SetColumn(scrollViewer, 1);
        rootGrid.AddChild(scrollViewer);

        var sidebar = new StackPanel();
        scrollViewer.Content = sidebar;
        sidebar.AddChild(new Border { Height = 140f });
        sidebar.AddChild(new Label { Content = "Payload lab" });
        sidebar.AddChild(new Label { Content = "Round-trip full documents or the active selection." });

        var comboBox = new ComboBox
        {
            Width = 320f,
            Height = 40f,
            Margin = new Thickness(0f, 8f, 0f, 0f)
        };
        comboBox.Items.Add("Flow XML");
        comboBox.Items.Add("XAML");
        comboBox.Items.Add("XamlPackage");
        comboBox.Items.Add("Rich Text Format");
        comboBox.Items.Add("Plain Text");
        comboBox.SelectedIndex = 0;
        sidebar.AddChild(comboBox);

        var buttonRow = new WrapPanel { Margin = new Thickness(0f, 8f, 0f, 0f) };
        buttonRow.AddChild(new Button { Content = "Export Doc", Width = 112f, Height = 36f, Margin = new Thickness(0f, 0f, 8f, 8f) });
        buttonRow.AddChild(new Button { Content = "Export Selection", Width = 144f, Height = 36f, Margin = new Thickness(0f, 0f, 8f, 8f) });
        buttonRow.AddChild(new Button { Content = "Load Doc", Width = 112f, Height = 36f, Margin = new Thickness(0f, 0f, 8f, 8f) });
        buttonRow.AddChild(new Button { Content = "Load Selection", Width = 144f, Height = 36f, Margin = new Thickness(0f, 0f, 8f, 8f) });
        sidebar.AddChild(buttonRow);
        sidebar.AddChild(new TextBox { Width = 320f, Height = 220f, Margin = new Thickness(0f, 8f, 0f, 0f) });
        sidebar.AddChild(new Border { Height = 420f });

        var uiRoot = new UiRoot(rootView);
        RunLayout(uiRoot, 1200, 900);
        return (uiRoot, scrollViewer, comboBox);
    }

    private static Panel FindItemsHostPanel(ListBox listBox)
    {
        foreach (var child in listBox.GetVisualChildren())
        {
            if (child is not ScrollViewer viewer)
            {
                continue;
            }

            foreach (var viewerChild in viewer.GetVisualChildren())
            {
                if (viewerChild is Panel panel)
                {
                    return panel;
                }
            }
        }

        throw new InvalidOperationException("Could not resolve ListBox items host panel.");
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

        throw new InvalidOperationException("Could not resolve ListBox ScrollViewer.");
    }

    private static void Click(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));
    }

    private static InputDelta CreatePointerDelta(Vector2 pointer, bool leftPressed = false, bool leftReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = true,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void RunLayout(UiRoot uiRoot, int width = 420, int height = 260)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, width, height));
    }
}

