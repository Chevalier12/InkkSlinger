using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class ScrollViewerParityTests
{
    public ScrollViewerParityTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void ScrollViewer_AutoVisibility_CascadesBetweenAxes()
    {
        var viewer = new ScrollViewer
        {
            Width = 120f,
            Height = 120f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new FixedSizeElement(200f, 200f)
        };

        viewer.Measure(new Vector2(120f, 120f));
        viewer.Arrange(new LayoutRect(0f, 0f, 120f, 120f));

        Assert.Equal(ScrollBarVisibility.Visible, viewer.ComputedHorizontalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Visible, viewer.ComputedVerticalScrollBarVisibility);
    }

    [Fact]
    public void ScrollViewer_Hidden_DoesNotDisableScrolling()
    {
        var viewer = new ScrollViewer
        {
            Width = 160f,
            Height = 100f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            Content = new FixedSizeElement(120f, 400f)
        };

        viewer.Measure(new Vector2(160f, 100f));
        viewer.Arrange(new LayoutRect(0f, 0f, 160f, 100f));
        var start = viewer.VerticalOffset;
        viewer.LineDown();

        Assert.True(viewer.VerticalOffset > start);
        Assert.Equal(ScrollBarVisibility.Hidden, viewer.ComputedVerticalScrollBarVisibility);
    }

    [Fact]
    public void ScrollViewer_Disabled_AxisDoesNotScroll()
    {
        var viewer = new ScrollViewer
        {
            Width = 160f,
            Height = 100f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new FixedSizeElement(120f, 400f)
        };

        viewer.Measure(new Vector2(160f, 100f));
        viewer.Arrange(new LayoutRect(0f, 0f, 160f, 100f));
        var start = viewer.VerticalOffset;
        viewer.LineDown();

        Assert.Equal(start, viewer.VerticalOffset, 3);
        Assert.Equal(ScrollBarVisibility.Disabled, viewer.ComputedVerticalScrollBarVisibility);
    }

    [Fact]
    public void ScrollViewer_RaisesScrollChanged_OnlyOnChanges()
    {
        var viewer = new ScrollViewer
        {
            Width = 120f,
            Height = 80f,
            Content = new FixedSizeElement(120f, 400f)
        };

        var count = 0;
        viewer.ScrollChanged += (_, _) => count++;
        viewer.Measure(new Vector2(120f, 80f));
        viewer.Arrange(new LayoutRect(0f, 0f, 120f, 80f));
        var baseline = count;

        viewer.ScrollToVerticalOffset(40f);
        Assert.True(count > baseline);

        var afterMove = count;
        viewer.ScrollToVerticalOffset(40f);
        Assert.Equal(afterMove, count);
    }

    [Fact]
    public void ScrollViewer_CanContentScroll_UsesIScrollInfo()
    {
        var panel = new VirtualizingStackPanel
        {
            Orientation = Orientation.Vertical,
            IsVirtualizing = true
        };
        for (var i = 0; i < 500; i++)
        {
            panel.AddChild(new FixedSizeElement(120f, 20f));
        }

        var viewer = new ScrollViewer
        {
            Width = 120f,
            Height = 100f,
            CanContentScroll = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            Content = panel
        };

        viewer.Measure(new Vector2(120f, 100f));
        viewer.Arrange(new LayoutRect(0f, 0f, 120f, 100f));
        viewer.ScrollToVerticalOffset(260f);
        viewer.Measure(new Vector2(120f, 100f));
        viewer.Arrange(new LayoutRect(0f, 0f, 120f, 100f));

        Assert.True(panel.FirstRealizedIndex > 0);
        Assert.Equal(viewer.VerticalOffset, panel.VerticalOffset, 3);
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
