using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ListBoxKeyboardParityTests
{
    [Fact]
    public void ArrowDown_WhenFocused_MovesSingleSelection()
    {
        var (uiRoot, listBox) = CreateFixture();
        listBox.SelectedIndex = 0;
        uiRoot.SetFocusedElementForTests(listBox);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Down));

        Assert.Equal(1, listBox.SelectedIndex);
    }

    [Fact]
    public void ShiftArrowDown_ExtendsSelectionInExtendedMode()
    {
        var (uiRoot, listBox) = CreateFixture(selectionMode: SelectionMode.Extended);
        listBox.SelectedIndex = 0;
        uiRoot.SetFocusedElementForTests(listBox);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Down, heldModifiers: [Keys.LeftShift]));
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Down, heldModifiers: [Keys.LeftShift]));

        Assert.Equal(new[] { 0, 1, 2 }, listBox.SelectedIndices);
    }

    [Fact]
    public void CtrlA_SelectsAll_InExtendedMode()
    {
        var (uiRoot, listBox) = CreateFixture(selectionMode: SelectionMode.Extended, itemCount: 5);
        uiRoot.SetFocusedElementForTests(listBox);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.A, heldModifiers: [Keys.LeftControl]));

        Assert.Equal(5, listBox.SelectedItems.Count);
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, listBox.SelectedIndices);
    }

    [Fact]
    public void ShiftClick_SelectsRange_FromAnchor()
    {
        var (uiRoot, listBox) = CreateFixture(selectionMode: SelectionMode.Extended);
        var hostPanel = FindItemsHostPanel(listBox);
        var firstPointer = GetCenter(hostPanel.Children[0]);
        var thirdPointer = GetCenter(hostPanel.Children[2]);

        Click(uiRoot, firstPointer);
        Click(uiRoot, thirdPointer, heldModifiers: [Keys.LeftShift]);

        Assert.Equal(new[] { 0, 1, 2 }, listBox.SelectedIndices);
    }

    [Fact]
    public void CtrlClick_TogglesItem_InExtendedMode()
    {
        var (uiRoot, listBox) = CreateFixture(selectionMode: SelectionMode.Extended);
        var hostPanel = FindItemsHostPanel(listBox);
        var firstPointer = GetCenter(hostPanel.Children[0]);
        var secondPointer = GetCenter(hostPanel.Children[1]);

        Click(uiRoot, firstPointer);
        Click(uiRoot, secondPointer, heldModifiers: [Keys.LeftControl]);
        Click(uiRoot, firstPointer, heldModifiers: [Keys.LeftControl]);

        Assert.Equal(new[] { 1 }, listBox.SelectedIndices);
    }

    private static (UiRoot UiRoot, ListBox ListBox) CreateFixture(SelectionMode selectionMode = SelectionMode.Single, int itemCount = 6)
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 480f, 360f));

        var listBox = new ListBox
        {
            SelectionMode = selectionMode
        };
        listBox.SetLayoutSlot(new LayoutRect(20f, 20f, 240f, 260f));
        for (var i = 0; i < itemCount; i++)
        {
            listBox.Items.Add($"Item {i}");
        }

        root.AddChild(listBox);
        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        RunLayout(uiRoot);
        return (uiRoot, listBox);
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

    private static void Click(UiRoot uiRoot, Vector2 pointer, Keys[]? heldModifiers = null)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, heldModifiers, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, heldModifiers, leftReleased: true));
    }

    private static Vector2 GetCenter(UIElement element)
    {
        return new Vector2(
            element.LayoutSlot.X + (element.LayoutSlot.Width * 0.5f),
            element.LayoutSlot.Y + (element.LayoutSlot.Height * 0.5f));
    }

    private static InputDelta CreateKeyDownDelta(Keys key, Keys[]? heldModifiers = null)
    {
        var pointer = new Vector2(16f, 16f);
        var modifiers = heldModifiers ?? [];
        return new InputDelta
        {
            Previous = new InputSnapshot(new KeyboardState(), default, pointer),
            Current = new InputSnapshot(new KeyboardState(modifiers), default, pointer),
            PressedKeys = new List<Keys> { key },
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = false,
            WheelDelta = 0,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static InputDelta CreatePointerDelta(Vector2 pointer, Keys[]? heldModifiers, bool leftPressed = false, bool leftReleased = false)
    {
        var modifiers = heldModifiers ?? [];
        return new InputDelta
        {
            Previous = new InputSnapshot(new KeyboardState(modifiers), default, pointer),
            Current = new InputSnapshot(new KeyboardState(modifiers), default, pointer),
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
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 480, 360));
    }
}