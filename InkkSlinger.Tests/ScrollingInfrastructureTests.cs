using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class ScrollingInfrastructureTests
{
    public ScrollingInfrastructureTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void ScrollViewer_ComputesExtentViewport_AndCoercesOffsets()
    {
        var viewer = new ScrollViewer
        {
            Width = 140f,
            Height = 90f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
            Content = new FixedSizeElement(340f, 260f)
        };

        viewer.Measure(new Vector2(140f, 90f));
        viewer.Arrange(new LayoutRect(0f, 0f, 140f, 90f));

        Assert.True(viewer.ExtentWidth >= 340f);
        Assert.True(viewer.ExtentHeight >= 260f);
        Assert.True(viewer.ScrollableWidth > 0f);
        Assert.True(viewer.ScrollableHeight > 0f);

        viewer.ScrollToHorizontalOffset(9999f);
        viewer.ScrollToVerticalOffset(9999f);

        Assert.Equal(viewer.ScrollableWidth, viewer.HorizontalOffset, 3);
        Assert.Equal(viewer.ScrollableHeight, viewer.VerticalOffset, 3);
    }

    [Fact]
    public void ScrollViewer_MouseWheel_UpdatesVerticalOffset()
    {
        var viewer = new TestScrollViewer
        {
            Width = 200f,
            Height = 110f,
            LineScrollAmount = 20f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            Content = new FixedSizeElement(160f, 520f)
        };

        viewer.Measure(new Vector2(200f, 110f));
        viewer.Arrange(new LayoutRect(0f, 0f, 200f, 110f));

        var start = viewer.VerticalOffset;
        viewer.FireMouseWheel(new Vector2(10f, 10f), -120);

        Assert.True(viewer.VerticalOffset > start);
    }

    [Fact]
    public void ScrollBar_DragThumb_AndWheel_StayInRange()
    {
        var bar = new TestScrollBar
        {
            Orientation = Orientation.Vertical,
            Minimum = 0f,
            Maximum = 100f,
            ViewportSize = 25f,
            Value = 0f,
            SmallChange = 5f
        };

        bar.Measure(new Vector2(18f, 200f));
        bar.Arrange(new LayoutRect(0f, 0f, 18f, 200f));

        bar.FireLeftDown(new Vector2(8f, 8f));
        bar.FireMove(new Vector2(8f, 150f));
        bar.FireLeftUp(new Vector2(8f, 150f));

        Assert.True(bar.Value > 0f);

        var afterDrag = bar.Value;
        bar.FireWheel(new Vector2(8f, 100f), -120);

        Assert.True(bar.Value > afterDrag);
        Assert.InRange(bar.Value, bar.Minimum, bar.Maximum);
    }

    [Fact]
    public void XamlLoader_ParsesScrollViewer_AndScrollBarAttributes()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <StackPanel>
                                <ScrollBar x:Name="VerticalBar"
                                           Orientation="Vertical"
                                           Minimum="0"
                                           Maximum="120"
                                           Value="15"
                                           ViewportSize="30" />
                                <ScrollViewer x:Name="Viewer"
                                              HorizontalScrollBarVisibility="Auto"
                                              VerticalScrollBarVisibility="Visible"
                                              LineScrollAmount="24">
                                  <Panel Width="420" Height="280" />
                                </ScrollViewer>
                              </StackPanel>
                            </UserControl>
                            """;

        var codeBehind = new ScrollViewCodeBehind();
        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, codeBehind);

        Assert.NotNull(codeBehind.VerticalBar);
        Assert.Equal(Orientation.Vertical, codeBehind.VerticalBar!.Orientation);
        Assert.Equal(15f, codeBehind.VerticalBar.Value, 3);

        Assert.NotNull(codeBehind.Viewer);
        Assert.Equal(ScrollBarVisibility.Visible, codeBehind.Viewer!.VerticalScrollBarVisibility);
        Assert.Equal(24f, codeBehind.Viewer.LineScrollAmount, 3);
    }

    private sealed class TestScrollViewer : ScrollViewer
    {
        public void FireMouseWheel(Vector2 position, int delta)
        {
            RaisePreviewMouseWheel(position, delta, ModifierKeys.None);
            RaiseMouseWheel(position, delta, ModifierKeys.None);
        }
    }

    private sealed class TestScrollBar : ScrollBar
    {
        public void FireLeftDown(Vector2 position)
        {
            RaisePreviewMouseDown(position, MouseButton.Left, 1, ModifierKeys.None);
            RaiseMouseDown(position, MouseButton.Left, 1, ModifierKeys.None);
        }

        public void FireMove(Vector2 position)
        {
            RaisePreviewMouseMove(position, ModifierKeys.None);
            RaiseMouseMove(position, ModifierKeys.None);
        }

        public void FireLeftUp(Vector2 position)
        {
            RaisePreviewMouseUp(position, MouseButton.Left, 1, ModifierKeys.None);
            RaiseMouseUp(position, MouseButton.Left, 1, ModifierKeys.None);
        }

        public void FireWheel(Vector2 position, int delta)
        {
            RaisePreviewMouseWheel(position, delta, ModifierKeys.None);
            RaiseMouseWheel(position, delta, ModifierKeys.None);
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

    private sealed class ScrollViewCodeBehind
    {
        public ScrollBar? VerticalBar { get; set; }

        public ScrollViewer? Viewer { get; set; }
    }
}
