using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class VirtualizingStackPanelTests
{
    public VirtualizingStackPanelTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void VirtualizingStackPanel_RealizesVisibleRange_InScrollViewer()
    {
        var panel = new VirtualizingStackPanel
        {
            Orientation = Orientation.Vertical,
            IsVirtualizing = true,
            CacheLength = 0.5f,
            CacheLengthUnit = VirtualizationCacheLengthUnit.Page
        };

        for (var i = 0; i < 1000; i++)
        {
            panel.AddChild(new FixedSizeElement(220f, 20f));
        }

        var viewer = new ScrollViewer
        {
            Width = 220f,
            Height = 140f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = panel
        };

        viewer.Measure(new Vector2(220f, 140f));
        viewer.Arrange(new LayoutRect(0f, 0f, 220f, 140f));

        Assert.True(panel.RealizedChildrenCount > 0);
        Assert.True(panel.RealizedChildrenCount < panel.Children.Count);
        Assert.Equal(0, panel.FirstRealizedIndex);
        Assert.True(CountChildren(panel.GetVisualChildren()) == panel.RealizedChildrenCount);
    }

    [Fact]
    public void VirtualizingStackPanel_ScrollOffset_AdvancesRealizedWindow()
    {
        var panel = new VirtualizingStackPanel
        {
            Orientation = Orientation.Vertical,
            IsVirtualizing = true,
            CacheLength = 0.25f,
            CacheLengthUnit = VirtualizationCacheLengthUnit.Page
        };

        for (var i = 0; i < 1000; i++)
        {
            panel.AddChild(new FixedSizeElement(220f, 20f));
        }

        var viewer = new ScrollViewer
        {
            Width = 220f,
            Height = 140f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = panel
        };

        viewer.Measure(new Vector2(220f, 140f));
        viewer.Arrange(new LayoutRect(0f, 0f, 220f, 140f));

        viewer.ScrollToVerticalOffset(700f);
        viewer.Measure(new Vector2(220f, 140f));
        viewer.Arrange(new LayoutRect(0f, 0f, 220f, 140f));

        Assert.True(panel.FirstRealizedIndex > 0);
        Assert.True(panel.LastRealizedIndex >= panel.FirstRealizedIndex);
        Assert.True(panel.RealizedChildrenCount < panel.Children.Count);
    }

    [Fact]
    public void VirtualizingStackPanel_DisabledVirtualization_RealizesAllChildren()
    {
        var panel = new VirtualizingStackPanel
        {
            Orientation = Orientation.Vertical,
            IsVirtualizing = false
        };

        for (var i = 0; i < 180; i++)
        {
            panel.AddChild(new FixedSizeElement(220f, 20f));
        }

        var viewer = new ScrollViewer
        {
            Width = 220f,
            Height = 140f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = panel
        };

        viewer.Measure(new Vector2(220f, 140f));
        viewer.Arrange(new LayoutRect(0f, 0f, 220f, 140f));

        Assert.Equal(panel.Children.Count, panel.RealizedChildrenCount);
        Assert.Equal(0, panel.FirstRealizedIndex);
        Assert.Equal(panel.Children.Count - 1, panel.LastRealizedIndex);
        Assert.Equal(panel.Children.Count, CountChildren(panel.GetVisualChildren()));
    }

    [Fact]
    public void XamlLoader_ParsesVirtualizingStackPanel_AndAttachedProperties()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <ScrollViewer>
                                <VirtualizingStackPanel x:Name="Host"
                                                        Orientation="Vertical"
                                                        VirtualizingStackPanel.IsVirtualizing="True"
                                                        VirtualizingStackPanel.VirtualizationMode="Recycling"
                                                        VirtualizingStackPanel.CacheLength="2"
                                                        VirtualizingStackPanel.CacheLengthUnit="Item" />
                              </ScrollViewer>
                            </UserControl>
                            """;

        var codeBehind = new VirtualizingCodeBehind();
        var root = new UserControl();
        XamlLoader.LoadIntoFromString(root, xaml, codeBehind);

        Assert.NotNull(codeBehind.Host);
        Assert.Equal(Orientation.Vertical, codeBehind.Host!.Orientation);
        Assert.True(codeBehind.Host.IsVirtualizing);
        Assert.Equal(VirtualizationMode.Recycling, codeBehind.Host.VirtualizationMode);
        Assert.Equal(2f, codeBehind.Host.CacheLength, 3);
        Assert.Equal(VirtualizationCacheLengthUnit.Item, codeBehind.Host.CacheLengthUnit);
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

    private sealed class VirtualizingCodeBehind
    {
        public VirtualizingStackPanel? Host { get; set; }
    }

    private static int CountChildren(System.Collections.Generic.IEnumerable<UIElement> children)
    {
        var count = 0;
        foreach (var _ in children)
        {
            count++;
        }

        return count;
    }
}
