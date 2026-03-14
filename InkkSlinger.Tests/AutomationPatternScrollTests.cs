using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class AutomationPatternScrollTests
{
    [Fact]
    public void ScrollViewerPeer_ExposesScrollPattern_AndSetScrollPercentMutatesOffsets()
    {
        var stack = new StackPanel();
        for (var i = 0; i < 40; i++)
        {
            stack.AddChild(new Label { Content = $"Row {i}" });
        }

        var viewer = new ScrollViewer
        {
            Content = stack,
            Width = 120f,
            Height = 100f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var host = new Canvas();
        host.AddChild(viewer);
        var uiRoot = new UiRoot(host);

        viewer.Measure(new Vector2(120f, 100f));
        viewer.Arrange(new LayoutRect(0f, 0f, 120f, 100f));

        var peer = uiRoot.Automation.GetPeer(viewer);
        Assert.NotNull(peer);
        Assert.True(peer.TryGetPattern(AutomationPatternType.Scroll, out var pattern));

        var scroll = Assert.IsAssignableFrom<IScrollProvider>(pattern);
        scroll.SetScrollPercent(0f, 75f);

        Assert.True(viewer.VerticalOffset > 0f);

        uiRoot.Shutdown();
    }
}
