using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public class ToggleControlsTests
{
    public ToggleControlsTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void ToggleButton_MouseClick_Toggles_RaisesEvents_AndExecutesCommand()
    {
        var toggle = new TestToggleButton();
        toggle.Measure(new Vector2(100f, 40f));
        toggle.Arrange(new LayoutRect(0f, 0f, 100f, 40f));

        var checkedCount = 0;
        var uncheckedCount = 0;
        var commandCount = 0;

        toggle.Checked += (_, _) => checkedCount++;
        toggle.Unchecked += (_, _) => uncheckedCount++;
        toggle.Command = new RelayCommand(_ => commandCount++);

        toggle.FireMouseDownAndUp(new Vector2(10f, 10f));
        Assert.True(toggle.IsChecked == true);
        Assert.Equal(1, checkedCount);
        Assert.Equal(0, uncheckedCount);

        toggle.FireMouseDownAndUp(new Vector2(10f, 10f));
        Assert.True(toggle.IsChecked == false);
        Assert.Equal(1, checkedCount);
        Assert.Equal(1, uncheckedCount);
        Assert.Equal(2, commandCount);
    }

    [Fact]
    public void ToggleButton_KeyActivation_Toggles_ForEnter_AndSpace()
    {
        var toggle = new TestToggleButton();

        toggle.FireKeyDown(Keys.Enter);
        Assert.True(toggle.IsChecked == true);

        toggle.FireKeyDown(Keys.Space);
        Assert.True(toggle.IsChecked == false);
    }

    [Fact]
    public void ToggleButton_ThreeState_Cycles_Through_Indeterminate()
    {
        var toggle = new TestToggleButton
        {
            IsThreeState = true
        };
        toggle.Measure(new Vector2(100f, 40f));
        toggle.Arrange(new LayoutRect(0f, 0f, 100f, 40f));

        var indeterminateCount = 0;
        toggle.Indeterminate += (_, _) => indeterminateCount++;

        toggle.FireMouseDownAndUp(new Vector2(10f, 10f));
        Assert.True(toggle.IsChecked == true);

        toggle.FireMouseDownAndUp(new Vector2(10f, 10f));
        Assert.Null(toggle.IsChecked);
        Assert.Equal(1, indeterminateCount);

        toggle.FireMouseDownAndUp(new Vector2(10f, 10f));
        Assert.True(toggle.IsChecked == false);
    }

    [Fact]
    public void RadioButton_GroupName_Unchecks_OtherMembers()
    {
        var host = new StackPanel();
        var first = new RadioButton { GroupName = "Quality", Text = "Low" };
        var second = new RadioButton { GroupName = "Quality", Text = "High" };
        var third = new RadioButton { GroupName = "Other", Text = "Other" };

        host.AddChild(first);
        host.AddChild(second);
        host.AddChild(third);

        first.IsChecked = true;
        Assert.True(first.IsChecked == true);

        second.IsChecked = true;
        Assert.True(first.IsChecked == false);
        Assert.True(second.IsChecked == true);
        Assert.True(third.IsChecked == false);
    }

    [Fact]
    public void RadioButton_WithoutGroupName_UsesLocalParentScope()
    {
        var root = new StackPanel();
        var leftGroup = new StackPanel();
        var rightGroup = new StackPanel();

        var left = new RadioButton { Text = "Left" };
        var right = new RadioButton { Text = "Right" };

        leftGroup.AddChild(left);
        rightGroup.AddChild(right);
        root.AddChild(leftGroup);
        root.AddChild(rightGroup);

        left.IsChecked = true;
        right.IsChecked = true;

        Assert.True(left.IsChecked == true);
        Assert.True(right.IsChecked == true);
    }

    [Fact]
    public void XamlLoader_Wires_CheckBox_Checked_And_Unchecked_Handlers()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <CheckBox x:Name="FeatureToggle"
                                        Text="Feature"
                                        IsChecked="true"
                                        Checked="OnChecked"
                                        Unchecked="OnUnchecked" />
                            </UserControl>
                            """;

        var codeBehind = new ToggleControlCodeBehind();
        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, codeBehind);

        var checkBox = Assert.IsType<CheckBox>(view.Content);
        Assert.Same(checkBox, codeBehind.FeatureToggle);
        Assert.True(checkBox.IsChecked == true);

        checkBox.IsChecked = false;
        checkBox.IsChecked = true;

        Assert.Equal(1, codeBehind.CheckedCount);
        Assert.Equal(1, codeBehind.UncheckedCount);
    }

    private sealed class TestToggleButton : ToggleButton
    {
        public void FireMouseDownAndUp(Vector2 position)
        {
            RaisePreviewMouseDown(position, MouseButton.Left, 1, ModifierKeys.None);
            RaiseMouseDown(position, MouseButton.Left, 1, ModifierKeys.None);
            RaisePreviewMouseUp(position, MouseButton.Left, 1, ModifierKeys.None);
            RaiseMouseUp(position, MouseButton.Left, 1, ModifierKeys.None);
        }

        public void FireKeyDown(Keys key)
        {
            RaisePreviewKeyDown(key, false, ModifierKeys.None);
            RaiseKeyDown(key, false, ModifierKeys.None);
        }
    }

    private sealed class ToggleControlCodeBehind
    {
        public CheckBox? FeatureToggle { get; set; }

        public int CheckedCount { get; private set; }

        public int UncheckedCount { get; private set; }

        public void OnChecked(object? sender, RoutedSimpleEventArgs args)
        {
            CheckedCount++;
        }

        public void OnUnchecked(object? sender, RoutedSimpleEventArgs args)
        {
            UncheckedCount++;
        }
    }
}
