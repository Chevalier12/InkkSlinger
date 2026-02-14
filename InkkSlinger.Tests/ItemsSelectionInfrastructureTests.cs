using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public class ItemsSelectionInfrastructureTests
{
    public ItemsSelectionInfrastructureTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void SelectionModel_SingleSelect_RaisesAddedAndRemoved()
    {
        var model = new SelectionModel();
        model.ReplaceItems(new object[] { "A", "B", "C" });

        SelectionModelChangedEventArgs? last = null;
        model.Changed += (_, args) => last = args;

        model.SelectIndex(1);
        Assert.NotNull(last);
        Assert.Equal(new[] { 1 }, last!.AddedIndices);
        Assert.Equal("B", model.SelectedItem);

        model.SelectIndex(2);
        Assert.NotNull(last);
        Assert.Equal(new[] { 1 }, last!.RemovedIndices);
        Assert.Equal(new[] { 2 }, last.AddedIndices);
        Assert.Equal("C", model.SelectedItem);
    }

    [Fact]
    public void ItemsControl_UsesContainerHooks_ForNonVisualItems()
    {
        var itemsControl = new TestItemsControl();
        itemsControl.Items.Add("One");
        itemsControl.Items.Add("Two");

        itemsControl.Measure(new Vector2(200f, 100f));
        itemsControl.Arrange(new LayoutRect(0f, 0f, 200f, 100f));

        Assert.True(itemsControl.PreparedCount >= 2);
        Assert.True(itemsControl.VisualCount >= 2);
    }

    [Fact]
    public void ListBox_SelectsContainer_OnPreviewMouseDown()
    {
        var listBox = new ListBox
        {
            Width = 220f,
            Height = 120f
        };

        var first = new TestListBoxItem();
        first.Content = new Label { Text = "First" };
        var second = new TestListBoxItem();
        second.Content = new Label { Text = "Second" };

        listBox.Items.Add(first);
        listBox.Items.Add(second);

        listBox.Measure(new Vector2(220f, 120f));
        listBox.Arrange(new LayoutRect(0f, 0f, 220f, 120f));

        var click = new Vector2(second.LayoutSlot.X + 4f, second.LayoutSlot.Y + 4f);
        second.FireLeftDown(click);

        Assert.Equal(1, listBox.SelectedIndex);
        Assert.Same(second, listBox.SelectedItem);
        Assert.True(second.IsSelected);
        Assert.False(first.IsSelected);
    }

    [Fact]
    public void XamlLoader_Parses_ListBox_AndSelectionChangedHandler()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <ListBox x:Name="Picker" SelectionChanged="OnChanged">
                                <ListBoxItem>
                                  <Label Text="A" />
                                </ListBoxItem>
                                <ListBoxItem>
                                  <Label Text="B" />
                                </ListBoxItem>
                              </ListBox>
                            </UserControl>
                            """;

        var codeBehind = new ListBoxCodeBehind();
        var root = new UserControl();
        XamlLoader.LoadIntoFromString(root, xaml, codeBehind);

        Assert.NotNull(codeBehind.Picker);
        codeBehind.Picker!.SelectedIndex = 1;

        Assert.Equal(1, codeBehind.ChangeCount);
        Assert.Equal(2, codeBehind.Picker.Items.Count);
    }

    [Fact]
    public void SelectionModel_MultipleMode_Supports_Toggle_And_Range()
    {
        var model = new SelectionModel
        {
            Mode = SelectionMode.Multiple
        };
        model.ReplaceItems(new object[] { "A", "B", "C", "D" });

        model.ToggleIndex(1);
        Assert.Equal(new[] { 1 }, model.SelectedIndices);

        model.SelectRange(1, 3, clearExisting: false);
        Assert.Equal(new[] { 1, 2, 3 }, model.SelectedIndices);

        model.SelectRange(0, 1, clearExisting: true);
        Assert.Equal(new[] { 0, 1 }, model.SelectedIndices);
    }

    [Fact]
    public void ListBox_MultiSelect_Mouse_CtrlAndShift_SelectExpectedRanges()
    {
        var listBox = CreateTestListBoxWithItems(4, out var containers);
        listBox.SelectionMode = SelectionMode.Multiple;

        var p1 = new Vector2(containers[1].LayoutSlot.X + 4f, containers[1].LayoutSlot.Y + 4f);
        var p3 = new Vector2(containers[3].LayoutSlot.X + 4f, containers[3].LayoutSlot.Y + 4f);

        containers[1].FireLeftDown(p1);
        Assert.Equal(new[] { 1 }, listBox.SelectedIndices);

        containers[3].FireLeftDown(p3, ModifierKeys.Control);
        Assert.Equal(new[] { 1, 3 }, listBox.SelectedIndices);

        containers[1].FireLeftDown(p1, ModifierKeys.Shift);
        Assert.Equal(new[] { 1, 2, 3 }, listBox.SelectedIndices);
    }

    [Fact]
    public void ListBox_MultiSelect_Keyboard_Shift_Extends_From_Anchor()
    {
        var listBox = CreateTestListBoxWithItems(4, out _);
        listBox.SelectionMode = SelectionMode.Multiple;

        listBox.SelectedIndex = 1;
        listBox.FireKeyDown(Keys.Down, ModifierKeys.Shift);

        Assert.Equal(new[] { 1, 2 }, listBox.SelectedIndices);

        listBox.FireKeyDown(Keys.End, ModifierKeys.Shift);
        Assert.Equal(new[] { 1, 2, 3 }, listBox.SelectedIndices);
    }

    [Fact]
    public void ListBox_MultiSelect_PlainClick_Replaces_And_ClickSelected_Deselects()
    {
        var listBox = CreateTestListBoxWithItems(4, out var containers);
        listBox.SelectionMode = SelectionMode.Multiple;

        var p1 = new Vector2(containers[1].LayoutSlot.X + 4f, containers[1].LayoutSlot.Y + 4f);
        var p3 = new Vector2(containers[3].LayoutSlot.X + 4f, containers[3].LayoutSlot.Y + 4f);

        containers[1].FireLeftDown(p1);
        Assert.Equal(new[] { 1 }, listBox.SelectedIndices);

        containers[3].FireLeftDown(p3);
        Assert.Equal(new[] { 3 }, listBox.SelectedIndices);

        containers[3].FireLeftDown(p3);
        Assert.Empty(listBox.SelectedIndices);
    }

    [Fact]
    public void ListBox_ClickBottomVisibleItem_AfterScroll_UpdatesSelection()
    {
        var root = new Panel
        {
            Width = 320f,
            Height = 260f
        };

        var listBox = new ListBox
        {
            Width = 240f,
            Height = 180f
        };

        var items = new List<TestListBoxItem>();
        for (var i = 0; i < 36; i++)
        {
            var item = new TestListBoxItem
            {
                Content = new Label { Text = $"Item {i}" }
            };
            items.Add(item);
            listBox.Items.Add(item);
        }

        root.AddChild(listBox);
        root.Measure(new Vector2(320f, 260f));
        root.Arrange(new LayoutRect(0f, 0f, 320f, 260f));

        var internalViewer = FindInnerScrollViewer(listBox);
        Assert.NotNull(internalViewer);
        internalViewer!.ScrollToVerticalOffset(internalViewer.ScrollableHeight);

        // Re-arrange after scrolling so hit targets stay deterministic for the test.
        root.Measure(new Vector2(320f, 260f));
        root.Arrange(new LayoutRect(0f, 0f, 320f, 260f));

        var clickPoint = new Vector2(
            listBox.LayoutSlot.X + 14f,
            listBox.LayoutSlot.Y + listBox.LayoutSlot.Height - 8f);

        var hit = VisualTreeHelper.HitTest(root, clickPoint);
        Assert.IsType<TestListBoxItem>(hit);

        var clickedItem = (TestListBoxItem)hit!;
        Assert.False(clickedItem.IsSelected);

        var gameTime = new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
        InputManager.ResetForTests();
        InputManager.UpdateForTesting(root, gameTime, clickPoint);
        InputManager.UpdateForTesting(root, gameTime, clickPoint, leftButton: ButtonState.Pressed);
        InputManager.UpdateForTesting(root, gameTime, clickPoint, leftButton: ButtonState.Released);

        Assert.Same(clickedItem, listBox.SelectedItem);
        Assert.True(clickedItem.IsSelected);
    }

    private static TestListBox CreateTestListBoxWithItems(int count, out List<TestListBoxItem> containers)
    {
        var listBox = new TestListBox
        {
            Width = 220f,
            Height = 160f
        };

        containers = new List<TestListBoxItem>(count);
        for (var i = 0; i < count; i++)
        {
            var item = new TestListBoxItem
            {
                Content = new Label { Text = $"Item {i}" }
            };
            containers.Add(item);
            listBox.Items.Add(item);
        }

        listBox.Measure(new Vector2(220f, 160f));
        listBox.Arrange(new LayoutRect(0f, 0f, 220f, 160f));
        return listBox;
    }

    private static ScrollViewer? FindInnerScrollViewer(ListBox listBox)
    {
        foreach (var child in listBox.GetVisualChildren())
        {
            if (child is ScrollViewer viewer)
            {
                return viewer;
            }
        }

        return null;
    }

    private sealed class TestItemsControl : ItemsControl
    {
        public int PreparedCount { get; private set; }

        public int VisualCount
        {
            get
            {
                var count = 0;
                foreach (var _ in GetVisualChildren())
                {
                    count++;
                }

                return count;
            }
        }

        protected override UIElement CreateContainerForItemOverride(object item)
        {
            return new Label { Text = item.ToString() ?? string.Empty };
        }

        protected override void PrepareContainerForItemOverride(UIElement element, object item, int index)
        {
            base.PrepareContainerForItemOverride(element, item, index);
            PreparedCount++;
        }
    }

    private sealed class TestListBoxItem : ListBoxItem
    {
        public void FireLeftDown(Vector2 position, ModifierKeys modifiers = ModifierKeys.None)
        {
            RaisePreviewMouseDown(position, MouseButton.Left, 1, modifiers);
            RaiseMouseDown(position, MouseButton.Left, 1, modifiers);
        }
    }

    private sealed class TestListBox : ListBox
    {
        public void FireKeyDown(Keys key, ModifierKeys modifiers = ModifierKeys.None)
        {
            RaisePreviewKeyDown(key, false, modifiers);
            RaiseKeyDown(key, false, modifiers);
        }
    }

    private sealed class ListBoxCodeBehind
    {
        public ListBox? Picker { get; set; }

        public int ChangeCount { get; private set; }

        public void OnChanged(object? sender, SelectionChangedEventArgs args)
        {
            ChangeCount++;
        }
    }
}
