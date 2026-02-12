using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public class ThumbTests
{
    public ThumbTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void Drag_RaisesStartedDeltaAndCompleted()
    {
        var thumb = new TestThumb();
        var started = 0;
        var deltas = 0;
        var completed = 0;
        DragStartedEventArgs? startedArgs = null;
        DragDeltaEventArgs? lastDeltaArgs = null;
        DragCompletedEventArgs? completedArgs = null;

        thumb.DragStarted += (_, args) =>
        {
            started++;
            startedArgs = args;
        };
        thumb.DragDelta += (_, args) =>
        {
            deltas++;
            lastDeltaArgs = args;
        };
        thumb.DragCompleted += (_, args) =>
        {
            completed++;
            completedArgs = args;
        };

        thumb.SimulateMouseDown(new Vector2(10f, 12f));
        thumb.SimulateMouseMove(new Vector2(14f, 18f));
        thumb.SimulateMouseMove(new Vector2(16f, 17f));
        thumb.SimulateMouseUp(new Vector2(16f, 17f));

        Assert.Equal(1, started);
        Assert.Equal(2, deltas);
        Assert.Equal(1, completed);
        Assert.False(thumb.IsDragging);
        Assert.NotNull(startedArgs);
        Assert.NotNull(lastDeltaArgs);
        Assert.NotNull(completedArgs);
        Assert.Equal(10f, startedArgs!.HorizontalOffset, 3);
        Assert.Equal(12f, startedArgs.VerticalOffset, 3);
        Assert.Equal(2f, lastDeltaArgs!.HorizontalChange, 3);
        Assert.Equal(-1f, lastDeltaArgs.VerticalChange, 3);
        Assert.Equal(6f, completedArgs!.HorizontalChange, 3);
        Assert.Equal(5f, completedArgs.VerticalChange, 3);
        Assert.False(completedArgs.Canceled);
    }

    [Fact]
    public void LostCapture_CompletesDragAsCanceled()
    {
        var thumb = new TestThumb();
        DragCompletedEventArgs? completedArgs = null;
        thumb.DragCompleted += (_, args) => completedArgs = args;

        thumb.SimulateMouseDown(new Vector2(20f, 25f));
        thumb.SimulateMouseMove(new Vector2(28f, 29f));
        thumb.ReleaseMouseCapture();

        Assert.NotNull(completedArgs);
        Assert.True(completedArgs!.Canceled);
        Assert.Equal(8f, completedArgs.HorizontalChange, 3);
        Assert.Equal(4f, completedArgs.VerticalChange, 3);
        Assert.False(thumb.IsDragging);
    }

    [Fact]
    public void XamlLoader_ParsesThumbProperties()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Thumb Width="22"
                                     Height="18"
                                     Background="#2A4058"
                                     BorderBrush="#BFD8F2" />
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var thumb = Assert.IsType<Thumb>(view.Content);
        Assert.Equal(22f, thumb.Width, 3);
        Assert.Equal(18f, thumb.Height, 3);
        Assert.Equal(new Color(0x2A, 0x40, 0x58), thumb.Background);
        Assert.Equal(new Color(0xBF, 0xD8, 0xF2), thumb.BorderBrush);
    }

    private sealed class TestThumb : Thumb
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
    }
}
