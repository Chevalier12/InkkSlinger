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
        _ = ScrollViewer.GetInteractionTelemetryAndReset();
        _ = ScrollViewer.GetValueChangedTelemetryAndReset();

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

        var runtimeInteraction = viewer.GetRuntimeInteractionTelemetryForDiagnostics();
        Assert.True(runtimeInteraction.ScrollToHorizontalOffsetCallCount > 0);
        Assert.True(runtimeInteraction.ScrollToVerticalOffsetCallCount > 1);
        Assert.True(runtimeInteraction.InvalidateScrollInfoCallCount > 0);
        Assert.True(runtimeInteraction.HandleMouseWheelCallCount >= 3);
        Assert.True(runtimeInteraction.HandleMouseWheelHandledCount > 0);
        Assert.True(runtimeInteraction.HandleMouseWheelIgnoredZeroDeltaCount > 0);
        Assert.True(runtimeInteraction.HandleMouseWheelIgnoredDisabledCount > 0);
        Assert.True(runtimeInteraction.SetOffsetsExternalSourceCount > 0);
        Assert.True(runtimeInteraction.SetOffsetsHorizontalScrollBarSourceCount > 0);
        Assert.True(runtimeInteraction.SetOffsetsVerticalScrollBarSourceCount > 0);
        Assert.True(runtimeInteraction.SetOffsetsWorkCount > 0);
        Assert.True(runtimeInteraction.SetOffsetsDeferredLayoutPathCount > 0);
        Assert.True(runtimeInteraction.SetOffsetsManualArrangePathCount > 0);
        Assert.True(runtimeInteraction.ArrangeContentForCurrentOffsetsCallCount > 0);
        Assert.True(runtimeInteraction.ArrangeContentOffsetPathCount > 0);
        Assert.True(runtimeInteraction.UpdateScrollBarValuesCallCount > 0);
        Assert.True(runtimeInteraction.UpdateHorizontalScrollBarValueCallCount > 0);
        Assert.True(runtimeInteraction.UpdateVerticalScrollBarValueCallCount > 0);
        Assert.True(runtimeInteraction.PopupCloseCallCount > 0);

        var runtimeValueChanged = viewer.GetRuntimeValueChangedTelemetryForDiagnostics();
        Assert.True(runtimeValueChanged.HorizontalValueChangedCallCount > 0);
        Assert.True(runtimeValueChanged.VerticalValueChangedCallCount > 0);
        Assert.True(runtimeValueChanged.HorizontalValueChangedSuppressedCount > 0);
        Assert.True(runtimeValueChanged.VerticalValueChangedSuppressedCount > 0);

        var aggregateInteraction = ScrollViewer.GetInteractionTelemetryAndReset();
        Assert.True(aggregateInteraction.ScrollToHorizontalOffsetCallCount > 0);
        Assert.True(aggregateInteraction.ScrollToVerticalOffsetCallCount > 1);
        Assert.True(aggregateInteraction.HandleMouseWheelCallCount >= 3);
        Assert.True(aggregateInteraction.SetOffsetsExternalSourceCount > 0);
        Assert.True(aggregateInteraction.SetOffsetsHorizontalScrollBarSourceCount > 0);
        Assert.True(aggregateInteraction.SetOffsetsVerticalScrollBarSourceCount > 0);
        Assert.True(aggregateInteraction.SetOffsetsDeferredLayoutPathCount > 0);
        Assert.True(aggregateInteraction.ArrangeContentForCurrentOffsetsCallCount > 0);
        Assert.True(aggregateInteraction.UpdateScrollBarValuesCallCount > 0);
        Assert.True(aggregateInteraction.UpdateHorizontalScrollBarValueCallCount > 0);
        Assert.True(aggregateInteraction.UpdateVerticalScrollBarValueCallCount > 0);

        var aggregateValueChanged = ScrollViewer.GetValueChangedTelemetryAndReset();
        Assert.True(aggregateValueChanged.HorizontalValueChangedCallCount > 0);
        Assert.True(aggregateValueChanged.VerticalValueChangedCallCount > 0);
        Assert.True(aggregateValueChanged.HorizontalValueChangedSuppressedCount > 0);
        Assert.True(aggregateValueChanged.VerticalValueChangedSuppressedCount > 0);

        var clearedInteraction = ScrollViewer.GetInteractionTelemetryAndReset();
        Assert.Equal(0, clearedInteraction.ScrollToHorizontalOffsetCallCount);
        Assert.Equal(0, clearedInteraction.SetOffsetsCallCount);
        Assert.Equal(0, clearedInteraction.UpdateScrollBarValuesCallCount);

        var clearedValueChanged = ScrollViewer.GetValueChangedTelemetryAndReset();
        Assert.Equal(0, clearedValueChanged.HorizontalValueChangedCallCount);
        Assert.Equal(0, clearedValueChanged.VerticalValueChangedCallCount);

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