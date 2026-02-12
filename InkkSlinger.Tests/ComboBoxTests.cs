using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public class ComboBoxTests
{
    public ComboBoxTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void ComboBox_KeyboardNavigation_UpdatesSelection_WhenClosed()
    {
        var combo = new TestComboBox();
        combo.Items.Add("A");
        combo.Items.Add("B");
        combo.Items.Add("C");

        combo.FireKeyDown(Keys.Down);
        Assert.Equal(0, combo.SelectedIndex);

        combo.FireKeyDown(Keys.Down);
        Assert.Equal(1, combo.SelectedIndex);

        combo.FireKeyDown(Keys.Up);
        Assert.Equal(0, combo.SelectedIndex);
    }

    [Fact]
    public void ComboBox_DropDownSelection_ClosesPopup_AndCommitsSelection()
    {
        var host = new Panel { Width = 400f, Height = 260f };
        var combo = new TestComboBox { Width = 120f, Height = 30f };
        combo.Items.Add("Alpha");
        combo.Items.Add("Beta");
        combo.Items.Add("Gamma");

        host.AddChild(combo);
        host.Measure(new Vector2(400f, 260f));
        host.Arrange(new LayoutRect(0f, 0f, 400f, 260f));

        combo.IsDropDownOpen = true;
        Assert.True(combo.IsDropDownOpen);

        combo.SelectDropDownIndex(2);

        Assert.Equal(2, combo.SelectedIndex);
        Assert.Equal("Gamma", combo.SelectedItem);
        Assert.False(combo.IsDropDownOpen);
        Assert.False(combo.IsPopupOpen());
    }

    [Fact]
    public void ComboBox_OpeningDropDown_DoesNotClearExistingSelection()
    {
        var host = new Panel { Width = 420f, Height = 280f };
        var combo = new TestComboBox { Width = 120f, Height = 30f };
        combo.Items.Add("One");
        combo.Items.Add("Two");
        combo.Items.Add("Three");
        combo.SelectedIndex = 1;

        host.AddChild(combo);
        host.Measure(new Vector2(420f, 280f));
        host.Arrange(new LayoutRect(0f, 0f, 420f, 280f));

        combo.IsDropDownOpen = true;

        Assert.True(combo.IsDropDownOpen);
        Assert.Equal(1, combo.SelectedIndex);
        Assert.Equal("Two", combo.SelectedItem);
    }

    [Fact]
    public void ComboBox_ClickWhenOpen_TogglesDropDownClosed()
    {
        var host = new Panel { Width = 420f, Height = 280f };
        var combo = new TestComboBox { Width = 140f, Height = 32f };
        combo.Items.Add("One");
        combo.Items.Add("Two");

        host.AddChild(combo);
        host.Measure(new Vector2(420f, 280f));
        host.Arrange(new LayoutRect(0f, 0f, 420f, 280f));

        combo.IsDropDownOpen = true;
        Assert.True(combo.IsPopupOpen());

        var click = new Vector2(combo.LayoutSlot.X + 6f, combo.LayoutSlot.Y + 6f);
        combo.FireLeftDownUp(click);

        Assert.False(combo.IsDropDownOpen);
        Assert.False(combo.IsPopupOpen());
    }

    [Fact]
    public void ComboBox_DismissesDropDown_OnEscapeFromElsewhereViaPopupHostPreview()
    {
        var host = new Panel { Width = 420f, Height = 280f };
        var combo = new TestComboBox { Width = 120f, Height = 30f };
        combo.Items.Add("One");
        combo.Items.Add("Two");

        var focusOther = new TestFocusableElement();
        host.AddChild(combo);
        host.AddChild(focusOther);

        host.Measure(new Vector2(420f, 280f));
        host.Arrange(new LayoutRect(0f, 0f, 420f, 280f));

        combo.IsDropDownOpen = true;
        Assert.True(combo.IsDropDownOpen);

        Assert.True(FocusManager.SetFocusedElement(focusOther));
        focusOther.FireKeyDown(Keys.Escape);

        Assert.False(combo.IsDropDownOpen);
    }

    [Fact]
    public void XamlLoader_ParsesComboBoxItems_AndSelectionChangedHandler()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <ComboBox x:Name="Picker" SelectionChanged="OnChanged">
                                <ComboBoxItem Text="Red" />
                                <ComboBoxItem Text="Green" />
                              </ComboBox>
                            </UserControl>
                            """;

        var codeBehind = new ComboCodeBehind();
        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, codeBehind);

        Assert.NotNull(codeBehind.Picker);
        Assert.Equal(2, codeBehind.Picker!.Items.Count);

        codeBehind.Picker.SelectedIndex = 1;
        Assert.Equal(1, codeBehind.SelectionChangedCount);
    }

    private sealed class TestComboBox : ComboBox
    {
        public bool IsPopupOpen()
        {
            return IsDropDownPopupOpenForTesting;
        }

        public void FireKeyDown(Keys key)
        {
            RaisePreviewKeyDown(key, false, ModifierKeys.None);
            RaiseKeyDown(key, false, ModifierKeys.None);
        }

        public void FireLeftDownUp(Vector2 position)
        {
            RaisePreviewMouseDown(position, MouseButton.Left, 1, ModifierKeys.None);
            RaiseMouseDown(position, MouseButton.Left, 1, ModifierKeys.None);
            RaisePreviewMouseUp(position, MouseButton.Left, 1, ModifierKeys.None);
            RaiseMouseUp(position, MouseButton.Left, 1, ModifierKeys.None);
        }

        public void SelectDropDownIndex(int index)
        {
            var list = DropDownListForTesting;
            Assert.NotNull(list);
            list!.SelectedIndex = index;
        }
    }

    private sealed class TestFocusableElement : Border
    {
        public TestFocusableElement()
        {
            Focusable = true;
            Width = 30f;
            Height = 20f;
        }

        public void FireKeyDown(Keys key)
        {
            RaisePreviewKeyDown(key, false, ModifierKeys.None);
            RaiseKeyDown(key, false, ModifierKeys.None);
        }
    }

    private sealed class ComboCodeBehind
    {
        public ComboBox? Picker { get; set; }

        public int SelectionChangedCount { get; private set; }

        public void OnChanged(object? sender, SelectionChangedEventArgs args)
        {
            SelectionChangedCount++;
        }
    }
}
