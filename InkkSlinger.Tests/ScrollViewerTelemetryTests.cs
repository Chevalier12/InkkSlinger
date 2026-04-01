using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ScrollViewerTelemetryTests
{
    [Fact]
    public void ScrollViewerTelemetry_CapturesInteractionRuntimeAndAggregateReset()
    {
        _ = ScrollViewer.GetTelemetryAndReset();

        var root = new Canvas();
        var viewer = new ScrollViewer
        {
            Width = 140f,
            Height = 110f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new FixedMeasureElement(new Vector2(420f, 360f))
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 240, 180, 16);

        var horizontalBar = GetPrivateScrollBar(viewer, "_horizontalBar");
        var verticalBar = GetPrivateScrollBar(viewer, "_verticalBar");

        viewer.ScrollToHorizontalOffset(36f);
        viewer.ScrollToVerticalOffset(48f);
        Assert.True(viewer.HandleMouseWheelFromInput(-120));
        Assert.False(viewer.HandleMouseWheelFromInput(0));

        viewer.IsEnabled = false;
        Assert.False(viewer.HandleMouseWheelFromInput(-120));
        viewer.IsEnabled = true;

        horizontalBar.Value = 54f;
        verticalBar.Value = 72f;
        InvokeScrollBarValueChanged(viewer, "OnHorizontalScrollBarValueChanged", horizontalBar);
        InvokeScrollBarValueChanged(viewer, "OnVerticalScrollBarValueChanged", verticalBar);

        SetPrivateField(viewer, "_suppressInternalScrollBarValueChange", true);
        try
        {
            InvokeScrollBarValueChanged(viewer, "OnHorizontalScrollBarValueChanged", horizontalBar);
            InvokeScrollBarValueChanged(viewer, "OnVerticalScrollBarValueChanged", verticalBar);
        }
        finally
        {
            SetPrivateField(viewer, "_suppressInternalScrollBarValueChange", false);
        }

        viewer.InvalidateScrollInfo();
        viewer.ScrollToVerticalOffset(84f);
        RunLayout(uiRoot, 240, 180, 32);

        var runtime = viewer.GetScrollViewerSnapshotForDiagnostics();
        Assert.True(runtime.ScrollToHorizontalOffsetCallCount > 0);
        Assert.True(runtime.ScrollToVerticalOffsetCallCount > 1);
        Assert.True(runtime.InvalidateScrollInfoCallCount > 0);
        Assert.True(runtime.HandleMouseWheelCallCount >= 3);
        Assert.True(runtime.HandleMouseWheelHandledCount > 0);
        Assert.True(runtime.HandleMouseWheelIgnoredZeroDeltaCount > 0);
        Assert.True(runtime.HandleMouseWheelIgnoredDisabledCount > 0);
        Assert.True(runtime.SetOffsetsExternalSourceCount > 0);
        Assert.True(runtime.SetOffsetsHorizontalScrollBarSourceCount > 0);
        Assert.True(runtime.SetOffsetsVerticalScrollBarSourceCount > 0);
        Assert.True(runtime.SetOffsetsWorkCount > 0);
        Assert.True(runtime.SetOffsetsDeferredLayoutPathCount > 0);
        Assert.True(runtime.SetOffsetsManualArrangePathCount > 0);
        Assert.True(runtime.ArrangeContentForCurrentOffsetsCallCount > 0);
        Assert.True(runtime.ArrangeContentOffsetPathCount > 0);
        Assert.True(runtime.UpdateScrollBarValuesCallCount > 0);
        Assert.True(runtime.UpdateHorizontalScrollBarValueCallCount > 0);
        Assert.True(runtime.UpdateVerticalScrollBarValueCallCount > 0);
        Assert.True(runtime.PopupCloseCallCount > 0);
        Assert.True(runtime.HorizontalValueChangedCallCount > 0);
        Assert.True(runtime.VerticalValueChangedCallCount > 0);
        Assert.True(runtime.HorizontalValueChangedSuppressedCount > 0);
        Assert.True(runtime.VerticalValueChangedSuppressedCount > 0);

        var aggregate = ScrollViewer.GetTelemetryAndReset();
        Assert.True(aggregate.ScrollToHorizontalOffsetCallCount > 0);
        Assert.True(aggregate.ScrollToVerticalOffsetCallCount > 1);
        Assert.True(aggregate.HandleMouseWheelCallCount >= 3);
        Assert.True(aggregate.SetOffsetsExternalSourceCount > 0);
        Assert.True(aggregate.SetOffsetsHorizontalScrollBarSourceCount > 0);
        Assert.True(aggregate.SetOffsetsVerticalScrollBarSourceCount > 0);
        Assert.True(aggregate.SetOffsetsDeferredLayoutPathCount > 0);
        Assert.True(aggregate.ArrangeContentForCurrentOffsetsCallCount > 0);
        Assert.True(aggregate.UpdateScrollBarValuesCallCount > 0);
        Assert.True(aggregate.UpdateHorizontalScrollBarValueCallCount > 0);
        Assert.True(aggregate.UpdateVerticalScrollBarValueCallCount > 0);
        Assert.True(aggregate.HorizontalValueChangedCallCount > 0);
        Assert.True(aggregate.VerticalValueChangedCallCount > 0);
        Assert.True(aggregate.HorizontalValueChangedSuppressedCount > 0);
        Assert.True(aggregate.VerticalValueChangedSuppressedCount > 0);

        var cleared = ScrollViewer.GetTelemetryAndReset();
        Assert.Equal(0, cleared.ScrollToHorizontalOffsetCallCount);
        Assert.Equal(0, cleared.SetOffsetsCallCount);
        Assert.Equal(0, cleared.UpdateScrollBarValuesCallCount);
        Assert.Equal(0, cleared.HorizontalValueChangedCallCount);
        Assert.Equal(0, cleared.VerticalValueChangedCallCount);

        uiRoot.Shutdown();
    }

    private static ScrollBar GetPrivateScrollBar(ScrollViewer viewer, string fieldName)
    {
        var field = typeof(ScrollViewer).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<ScrollBar>(field!.GetValue(viewer));
    }

    private static void InvokeScrollBarValueChanged(ScrollViewer viewer, string methodName, ScrollBar scrollBar)
    {
        var method = typeof(ScrollViewer).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(viewer, [scrollBar, new RoutedSimpleEventArgs(ScrollBar.ValueChangedEvent)]);
    }

    private static void SetPrivateField(ScrollViewer viewer, string fieldName, bool value)
    {
        var field = typeof(ScrollViewer).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(viewer, value);
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }

    private sealed class FixedMeasureElement(Vector2 desiredSize) : FrameworkElement
    {
        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            _ = availableSize;
            return desiredSize;
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            return finalSize;
        }
    }
}