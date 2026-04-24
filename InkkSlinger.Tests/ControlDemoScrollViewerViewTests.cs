using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlDemoScrollViewerViewTests
{
    [Fact]
    public void ScrollBarThicknessSlider_ShouldResizeWorkbenchScrollBarVisuals()
    {
        var backup = CaptureApplicationResources();
        try
        {
            TestApplicationResources.LoadDemoAppResources();

            var view = new ScrollViewerView
            {
                Width = 1180f,
                Height = 820f
            };
            var uiRoot = new UiRoot(view);
            RunLayout(uiRoot, 1180, 820, 16);

            try
            {
                var viewer = Assert.IsType<ScrollViewer>(view.FindName("WorkbenchScrollViewer"));
                var thicknessSlider = Assert.IsType<Slider>(view.FindName("ScrollBarThicknessSlider"));
                var verticalBar = GetPrivateScrollBar(viewer, "_verticalBar");
                var horizontalBar = GetPrivateScrollBar(viewer, "_horizontalBar");

                var initialVerticalWidth = verticalBar.LayoutSlot.Width;
                var initialHorizontalHeight = horizontalBar.LayoutSlot.Height;

                thicknessSlider.Value = 26f;
                RunLayout(uiRoot, 1180, 820, 32);

                Assert.Equal(26f, viewer.ScrollBarThickness);
                Assert.True(
                    verticalBar.LayoutSlot.Width > initialVerticalWidth + 4f,
                    $"Expected the vertical scrollbar visual to resize after the demo slider changed ScrollBarThickness. before={initialVerticalWidth:0.###}, after={verticalBar.LayoutSlot.Width:0.###}.");
                Assert.True(
                    horizontalBar.LayoutSlot.Height > initialHorizontalHeight + 4f,
                    $"Expected the horizontal scrollbar visual to resize after the demo slider changed ScrollBarThickness. before={initialHorizontalHeight:0.###}, after={horizontalBar.LayoutSlot.Height:0.###}.");
            }
            finally
            {
                uiRoot.Shutdown();
            }
        }
        finally
        {
            TestApplicationResources.Restore(backup);
        }
    }

    private static Dictionary<object, object> CaptureApplicationResources()
    {
        return new Dictionary<object, object>(UiApplication.Current.Resources);
    }

    private static ScrollBar GetPrivateScrollBar(ScrollViewer viewer, string fieldName)
    {
        var field = typeof(ScrollViewer).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<ScrollBar>(field!.GetValue(viewer));
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }
}
