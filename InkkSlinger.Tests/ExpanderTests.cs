using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public class ExpanderTests
{
    public ExpanderTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void Collapsed_ExcludesContentFromDesiredSize()
    {
        var expander = new Expander
        {
            Header = "Section",
            IsExpanded = false,
            Content = new FixedSizeElement(120f, 80f)
        };

        expander.Measure(new Vector2(300f, 300f));
        var collapsedHeight = expander.DesiredSize.Y;

        expander.IsExpanded = true;
        expander.Measure(new Vector2(300f, 300f));

        Assert.True(expander.DesiredSize.Y > collapsedHeight);
    }

    [Fact]
    public void MouseClickOnHeader_TogglesExpandedState()
    {
        var expander = new TestExpander
        {
            Header = "Toggle",
            IsExpanded = true,
            Content = new FixedSizeElement(80f, 40f),
            Width = 200f,
            Height = 120f
        };

        expander.Measure(new Vector2(200f, 120f));
        expander.Arrange(new LayoutRect(0f, 0f, 200f, 120f));

        expander.SimulateMouseDown(new Vector2(10f, 10f));
        expander.SimulateMouseUp(new Vector2(10f, 10f));
        Assert.False(expander.IsExpanded);

        expander.SimulateMouseDown(new Vector2(10f, 10f));
        expander.SimulateMouseUp(new Vector2(10f, 10f));
        Assert.True(expander.IsExpanded);
    }

    [Fact]
    public void KeyboardSpace_TogglesExpandedState()
    {
        var expander = new TestExpander
        {
            Header = "Toggle",
            IsExpanded = true
        };

        expander.SimulateKeyDown(Keys.Space);
        Assert.False(expander.IsExpanded);
        expander.SimulateKeyDown(Keys.Enter);
        Assert.True(expander.IsExpanded);
    }

    [Fact]
    public void XamlLoader_ParsesExpanderProperties()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Expander Header="Inventory"
                                        IsExpanded="false"
                                        ExpandDirection="Down"
                                        BorderThickness="2"
                                        HeaderPadding="12,8,12,8">
                                <Label Text="Body" />
                              </Expander>
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var expander = Assert.IsType<Expander>(view.Content);
        Assert.Equal("Inventory", expander.Header);
        Assert.False(expander.IsExpanded);
        Assert.Equal(ExpandDirection.Down, expander.ExpandDirection);
        Assert.Equal(2f, expander.BorderThickness, 3);
        Assert.Equal(12f, expander.HeaderPadding.Left, 3);
        Assert.IsType<Label>(expander.Content);
    }

    private sealed class TestExpander : Expander
    {
        public void SimulateMouseDown(Vector2 position)
        {
            OnMouseLeftButtonDown(new RoutedMouseButtonEventArgs(
                UIElement.MouseLeftButtonDownEvent,
                position,
                MouseButton.Left,
                1,
                ModifierKeys.None));
        }

        public void SimulateMouseUp(Vector2 position)
        {
            OnMouseLeftButtonUp(new RoutedMouseButtonEventArgs(
                UIElement.MouseLeftButtonUpEvent,
                position,
                MouseButton.Left,
                1,
                ModifierKeys.None));
        }

        public void SimulateKeyDown(Keys key)
        {
            OnKeyDown(new RoutedKeyEventArgs(
                UIElement.KeyDownEvent,
                key,
                false,
                ModifierKeys.None));
        }
    }

    private sealed class FixedSizeElement : FrameworkElement
    {
        private readonly Vector2 _size;

        public FixedSizeElement(float width, float height)
        {
            _size = new Vector2(width, height);
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            return _size;
        }
    }
}
