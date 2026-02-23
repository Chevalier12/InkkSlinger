using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class ScrollViewerArrangeViewportClampTests
{
    [Fact]
    public void Arrange_WithSmallerViewport_UpdatesViewportMetricsAndBottomClamp()
    {
        var content = new StackPanel();
        for (var i = 0; i < 80; i++)
        {
            content.AddChild(new Border
            {
                Height = 30f
            });
        }

        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };

        // Measure with a larger viewport, then arrange with a smaller one.
        viewer.Measure(new Vector2(500f, 900f));
        viewer.SetLayoutSlot(new LayoutRect(0f, 0f, 500f, 300f));
        viewer.Arrange(new LayoutRect(0f, 0f, 500f, 300f));

        viewer.ScrollToVerticalOffset(100_000f);

        var expectedMax = MathF.Max(0f, viewer.ExtentHeight - viewer.ViewportHeight);
        Assert.True(MathF.Abs(expectedMax - viewer.VerticalOffset) <= 0.01f);
    }
}
