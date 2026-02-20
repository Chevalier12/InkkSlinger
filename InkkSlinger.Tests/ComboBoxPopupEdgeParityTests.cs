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
            Text = "Open Combo Dropdown",
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
            Text = "Open Combo Dropdown",
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
            Text = "Underneath",
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

    private static void RunLayout(UiRoot uiRoot)
    {
        const string inputPipelineVariable = "INKKSLINGER_ENABLE_INPUT_PIPELINE";
        var previous = Environment.GetEnvironmentVariable(inputPipelineVariable);
        Environment.SetEnvironmentVariable(inputPipelineVariable, "0");
        try
        {
            uiRoot.Update(
                new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
                new Viewport(0, 0, 420, 260));
        }
        finally
        {
            Environment.SetEnvironmentVariable(inputPipelineVariable, previous);
        }
    }
}
