using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public class SliderTests
{
    public SliderTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void MouseMoveToPoint_UpdatesValue()
    {
        var slider = new TestSlider
        {
            Orientation = Orientation.Horizontal,
            Minimum = 0f,
            Maximum = 100f,
            Value = 0f,
            Width = 200f,
            Height = 24f,
            IsMoveToPointEnabled = true
        };

        slider.Measure(new Vector2(200f, 24f));
        slider.Arrange(new LayoutRect(0f, 0f, 200f, 24f));

        slider.SimulateMouseDown(new Vector2(150f, 12f));
        slider.SimulateMouseUp(new Vector2(150f, 12f));

        Assert.True(slider.Value > 70f);
        Assert.True(slider.Value < 90f);
    }

    [Fact]
    public void DragThumb_ChangesValue()
    {
        var slider = new TestSlider
        {
            Orientation = Orientation.Horizontal,
            Minimum = 0f,
            Maximum = 100f,
            Value = 0f,
            Width = 200f,
            Height = 24f
        };

        slider.Measure(new Vector2(200f, 24f));
        slider.Arrange(new LayoutRect(0f, 0f, 200f, 24f));

        slider.SimulateMouseDown(new Vector2(7f, 12f));
        slider.SimulateMouseMove(new Vector2(107f, 12f));
        slider.SimulateMouseUp(new Vector2(107f, 12f));

        Assert.True(slider.Value > 40f);
        Assert.True(slider.Value < 70f);
    }

    [Fact]
    public void SnapToTick_QuantizesValue()
    {
        var slider = new Slider
        {
            Minimum = 0f,
            Maximum = 10f,
            TickFrequency = 2f,
            IsSnapToTickEnabled = true
        };

        slider.Value = 3.1f;
        Assert.Equal(4f, slider.Value, 3);

        slider.Value = 8.9f;
        Assert.Equal(8f, slider.Value, 3);
    }

    [Fact]
    public void VerticalKeyboard_UpIncreases_DownDecreases()
    {
        var slider = new TestSlider
        {
            Orientation = Orientation.Vertical,
            Minimum = 0f,
            Maximum = 100f,
            Value = 50f,
            SmallChange = 5f
        };

        slider.SimulateKeyDown(Keys.Up);
        Assert.Equal(55f, slider.Value, 3);
        slider.SimulateKeyDown(Keys.Down);
        Assert.Equal(50f, slider.Value, 3);
    }

    [Fact]
    public void XamlLoader_ParsesSliderProperties()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Slider Orientation="Vertical"
                                      Minimum="0"
                                      Maximum="20"
                                      Value="10"
                                      IsSnapToTickEnabled="true"
                                      TickFrequency="5"
                                      IsMoveToPointEnabled="true"
                                      TrackThickness="3"
                                      ThumbSize="16" />
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var slider = Assert.IsType<Slider>(view.Content);
        Assert.Equal(Orientation.Vertical, slider.Orientation);
        Assert.Equal(0f, slider.Minimum, 3);
        Assert.Equal(20f, slider.Maximum, 3);
        Assert.Equal(10f, slider.Value, 3);
        Assert.True(slider.IsSnapToTickEnabled);
        Assert.Equal(5f, slider.TickFrequency, 3);
        Assert.True(slider.IsMoveToPointEnabled);
        Assert.Equal(3f, slider.TrackThickness, 3);
        Assert.Equal(16f, slider.ThumbSize, 3);
    }

    private sealed class TestSlider : Slider
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

        public void SimulateMouseMove(Vector2 position)
        {
            OnMouseMove(new RoutedMouseEventArgs(
                UIElement.MouseMoveEvent,
                position,
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
}
