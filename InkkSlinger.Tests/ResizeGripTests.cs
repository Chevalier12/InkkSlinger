using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public class ResizeGripTests
{
    public ResizeGripTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void Drag_WithExplicitTarget_ResizesWithinBounds()
    {
        var target = new Border
        {
            Width = 100f,
            Height = 80f,
            MinWidth = 90f,
            MinHeight = 70f,
            MaxWidth = 120f,
            MaxHeight = 100f
        };

        var grip = new TestResizeGrip
        {
            Target = target,
            ResizeIncrement = 1f
        };

        grip.SimulateMouseDown(new Vector2(10f, 10f));
        grip.SimulateMouseMove(new Vector2(40f, 40f));
        grip.SimulateMouseUp(new Vector2(40f, 40f));

        Assert.Equal(120f, target.Width, 3);
        Assert.Equal(100f, target.Height, 3);
        Assert.False(grip.IsDragging);
    }

    [Fact]
    public void Keyboard_ResizesTarget_ByArrowKeys()
    {
        var target = new Border
        {
            Width = 100f,
            Height = 80f,
            MinWidth = 60f,
            MinHeight = 60f
        };

        var grip = new TestResizeGrip
        {
            Target = target
        };

        grip.SimulateKeyDown(Keys.Right);
        grip.SimulateKeyDown(Keys.Down);
        grip.SimulateKeyDown(Keys.Left);
        grip.SimulateKeyDown(Keys.Up);

        Assert.Equal(100f, target.Width, 3);
        Assert.Equal(80f, target.Height, 3);
    }

    [Fact]
    public void Drag_WithoutExplicitTarget_ResizesAncestorPopup()
    {
        var popup = new Popup
        {
            Width = 200f,
            Height = 150f
        };
        var grip = new TestResizeGrip();
        popup.Content = grip;

        grip.SimulateMouseDown(new Vector2(5f, 5f));
        grip.SimulateMouseMove(new Vector2(25f, 35f));
        grip.SimulateMouseUp(new Vector2(25f, 35f));

        Assert.Equal(220f, popup.Width, 3);
        Assert.Equal(180f, popup.Height, 3);
    }

    [Fact]
    public void XamlLoader_ParsesResizeGripProperties()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <ResizeGrip GripSize="20"
                                          ResizeIncrement="4"
                                          Background="#112233"
                                          Foreground="#AABBCC" />
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var grip = Assert.IsType<ResizeGrip>(view.Content);
        Assert.Equal(20f, grip.GripSize, 3);
        Assert.Equal(4f, grip.ResizeIncrement, 3);
        Assert.Equal(new Color(0x11, 0x22, 0x33), grip.Background);
        Assert.Equal(new Color(0xAA, 0xBB, 0xCC), grip.Foreground);
    }

    private sealed class TestResizeGrip : ResizeGrip
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
