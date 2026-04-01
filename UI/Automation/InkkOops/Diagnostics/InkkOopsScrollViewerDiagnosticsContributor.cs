using System;
using System.Collections.Generic;

namespace InkkSlinger;

public sealed class InkkOopsScrollViewerDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 50;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        if (element is not ScrollViewer scrollViewer)
        {
            return;
        }

        builder.Add("type", scrollViewer.GetType().Name);
        builder.Add("slot", FormatRect(scrollViewer.LayoutSlot));
        builder.Add("desired", $"{scrollViewer.DesiredSize.X:0.##},{scrollViewer.DesiredSize.Y:0.##}");
        builder.Add("renderSize", $"{scrollViewer.RenderSize.X:0.##},{scrollViewer.RenderSize.Y:0.##}");
        builder.Add("actual", $"{scrollViewer.ActualWidth:0.##},{scrollViewer.ActualHeight:0.##}");
        builder.Add("previousAvailable", $"{scrollViewer.PreviousAvailableSizeForTests.X:0.##},{scrollViewer.PreviousAvailableSizeForTests.Y:0.##}");
        builder.Add("measureCalls", scrollViewer.MeasureCallCount);
        builder.Add("measureWork", scrollViewer.MeasureWorkCount);
        builder.Add("arrangeCalls", scrollViewer.ArrangeCallCount);
        builder.Add("arrangeWork", scrollViewer.ArrangeWorkCount);
        builder.Add("measureMs", FormatMilliseconds(scrollViewer.MeasureElapsedTicksForTests));
        builder.Add("measureExclusiveMs", FormatMilliseconds(scrollViewer.MeasureExclusiveElapsedTicksForTests));
        builder.Add("arrangeMs", FormatMilliseconds(scrollViewer.ArrangeElapsedTicksForTests));
        builder.Add("measureValid", scrollViewer.IsMeasureValidForTests);
        builder.Add("arrangeValid", scrollViewer.IsArrangeValidForTests);
        builder.Add("visible", scrollViewer.Visibility == Visibility.Visible);
        builder.Add("enabled", scrollViewer.IsEnabled);
        builder.Add("focusable", scrollViewer.Focusable);
        builder.Add("loaded", scrollViewer.IsLoaded);
        builder.Add("opacity", $"{scrollViewer.Opacity:0.###}");
        builder.Add("snapsToDevicePixels", scrollViewer.SnapsToDevicePixels);
        builder.Add("useLayoutRounding", scrollViewer.UseLayoutRounding);
        builder.Add("clipToBounds", scrollViewer.ClipToBounds);
        builder.Add("margin", FormatThickness(scrollViewer.Margin));
        builder.Add("width", FormatFloat(scrollViewer.Width));
        builder.Add("height", FormatFloat(scrollViewer.Height));
        builder.Add("min", $"{scrollViewer.MinWidth:0.##},{scrollViewer.MinHeight:0.##}");
        builder.Add("max", $"{scrollViewer.MaxWidth:0.##},{scrollViewer.MaxHeight:0.##}");
        builder.Add("background", FormatColor(scrollViewer.Background));
        builder.Add("borderBrush", FormatColor(scrollViewer.BorderBrush));
        builder.Add("borderThickness", FormatFloat(scrollViewer.BorderThickness));
        builder.Add("horizontalScrollBarVisibility", scrollViewer.HorizontalScrollBarVisibility);
        builder.Add("verticalScrollBarVisibility", scrollViewer.VerticalScrollBarVisibility);
        builder.Add("horizontalOffset", $"{scrollViewer.HorizontalOffset:0.###}");
        builder.Add("verticalOffset", $"{scrollViewer.VerticalOffset:0.###}");
        builder.Add("extent", $"{scrollViewer.ExtentWidth:0.##},{scrollViewer.ExtentHeight:0.##}");
        builder.Add("viewport", $"{scrollViewer.ViewportWidth:0.##},{scrollViewer.ViewportHeight:0.##}");
        builder.Add("scrollable", $"{MathF.Max(0f, scrollViewer.ExtentWidth - scrollViewer.ViewportWidth):0.##},{MathF.Max(0f, scrollViewer.ExtentHeight - scrollViewer.ViewportHeight):0.##}");
        builder.Add("viewportFill", $"{SafeRatio(scrollViewer.ViewportWidth, scrollViewer.ExtentWidth):0.###},{SafeRatio(scrollViewer.ViewportHeight, scrollViewer.ExtentHeight):0.###}");
        builder.Add("horizontalBarVisible", IsHorizontalBarVisible(scrollViewer));
        builder.Add("verticalBarVisible", IsVerticalBarVisible(scrollViewer));
        builder.Add("scrollBarThickness", $"{scrollViewer.ScrollBarThickness:0.##}");
        builder.Add("lineScrollAmount", $"{scrollViewer.LineScrollAmount:0.##}");

        var runtimeLayout = scrollViewer.GetRuntimeLayoutTelemetryForDiagnostics();
        builder.Add("runtimeMeasureOverrideCalls", runtimeLayout.MeasureOverrideCallCount);
        builder.Add("runtimeMeasureOverrideMs", $"{runtimeLayout.MeasureOverrideMilliseconds:0.###}");
        builder.Add("runtimeArrangeOverrideCalls", runtimeLayout.ArrangeOverrideCallCount);
        builder.Add("runtimeArrangeOverrideMs", $"{runtimeLayout.ArrangeOverrideMilliseconds:0.###}");
        builder.Add("runtimeResolveBarsMeasureCalls", runtimeLayout.ResolveBarsAndMeasureContentCallCount);
        builder.Add("runtimeResolveBarsMeasureMs", $"{runtimeLayout.ResolveBarsAndMeasureContentMilliseconds:0.###}");
        builder.Add("runtimeResolveBarsMeasureIterations", runtimeLayout.ResolveBarsAndMeasureContentIterationCount);
        builder.Add("runtimeResolveBarsMeasureHorizontalFlips", runtimeLayout.ResolveBarsAndMeasureContentHorizontalFlipCount);
        builder.Add("runtimeResolveBarsMeasureVerticalFlips", runtimeLayout.ResolveBarsAndMeasureContentVerticalFlipCount);
        builder.Add("runtimeResolveBarsMeasureSinglePathCalls", runtimeLayout.ResolveBarsAndMeasureContentSingleMeasurePathCount);
        builder.Add("runtimeResolveBarsMeasureRemeasurePathCalls", runtimeLayout.ResolveBarsAndMeasureContentRemeasurePathCount);
        builder.Add("runtimeResolveBarsMeasureFallbacks", runtimeLayout.ResolveBarsAndMeasureContentFallbackCount);
        builder.Add("runtimeResolveBarsMeasureInitialHorizontalVisible", runtimeLayout.ResolveBarsAndMeasureContentInitialHorizontalVisibleCount);
        builder.Add("runtimeResolveBarsMeasureInitialHorizontalHidden", runtimeLayout.ResolveBarsAndMeasureContentInitialHorizontalHiddenCount);
        builder.Add("runtimeResolveBarsMeasureInitialVerticalVisible", runtimeLayout.ResolveBarsAndMeasureContentInitialVerticalVisibleCount);
        builder.Add("runtimeResolveBarsMeasureInitialVerticalHidden", runtimeLayout.ResolveBarsAndMeasureContentInitialVerticalHiddenCount);
        builder.Add("runtimeResolveBarsMeasureResolvedHorizontalVisible", runtimeLayout.ResolveBarsAndMeasureContentResolvedHorizontalVisibleCount);
        builder.Add("runtimeResolveBarsMeasureResolvedHorizontalHidden", runtimeLayout.ResolveBarsAndMeasureContentResolvedHorizontalHiddenCount);
        builder.Add("runtimeResolveBarsMeasureResolvedVerticalVisible", runtimeLayout.ResolveBarsAndMeasureContentResolvedVerticalVisibleCount);
        builder.Add("runtimeResolveBarsMeasureResolvedVerticalHidden", runtimeLayout.ResolveBarsAndMeasureContentResolvedVerticalHiddenCount);
        builder.Add("runtimeResolveBarsMeasureLastTrace", runtimeLayout.ResolveBarsAndMeasureContentLastTrace);
        builder.Add("runtimeResolveBarsMeasureHottestTrace", runtimeLayout.ResolveBarsAndMeasureContentHottestTrace);
        builder.Add("runtimeResolveBarsArrangeCalls", runtimeLayout.ResolveBarsForArrangeCallCount);
        builder.Add("runtimeResolveBarsArrangeMs", $"{runtimeLayout.ResolveBarsForArrangeMilliseconds:0.###}");
        builder.Add("runtimeResolveBarsArrangeIterations", runtimeLayout.ResolveBarsForArrangeIterationCount);
        builder.Add("runtimeResolveBarsArrangeHorizontalFlips", runtimeLayout.ResolveBarsForArrangeHorizontalFlipCount);
        builder.Add("runtimeResolveBarsArrangeVerticalFlips", runtimeLayout.ResolveBarsForArrangeVerticalFlipCount);
        builder.Add("runtimeMeasureContentCalls", runtimeLayout.MeasureContentCallCount);
        builder.Add("runtimeMeasureContentMs", $"{runtimeLayout.MeasureContentMilliseconds:0.###}");
        builder.Add("runtimeUpdateScrollBarsCalls", runtimeLayout.UpdateScrollBarsCallCount);
        builder.Add("runtimeUpdateScrollBarsMs", $"{runtimeLayout.UpdateScrollBarsMilliseconds:0.###}");

        var runtimeInteraction = scrollViewer.GetRuntimeInteractionTelemetryForDiagnostics();
        builder.Add("runtimeScrollToHorizontalCalls", runtimeInteraction.ScrollToHorizontalOffsetCallCount);
        builder.Add("runtimeScrollToVerticalCalls", runtimeInteraction.ScrollToVerticalOffsetCallCount);
        builder.Add("runtimeInvalidateScrollInfoCalls", runtimeInteraction.InvalidateScrollInfoCallCount);
        builder.Add("runtimeMouseWheelCalls", runtimeInteraction.HandleMouseWheelCallCount);
        builder.Add("runtimeMouseWheelMs", $"{runtimeInteraction.HandleMouseWheelMilliseconds:0.###}");
        builder.Add("runtimeMouseWheelHandled", runtimeInteraction.HandleMouseWheelHandledCount);
        builder.Add("runtimeMouseWheelIgnoredDisabled", runtimeInteraction.HandleMouseWheelIgnoredDisabledCount);
        builder.Add("runtimeMouseWheelIgnoredZeroDelta", runtimeInteraction.HandleMouseWheelIgnoredZeroDeltaCount);
        builder.Add("runtimeSetOffsetsCalls", runtimeInteraction.SetOffsetsCallCount);
        builder.Add("runtimeSetOffsetsMs", $"{runtimeInteraction.SetOffsetsMilliseconds:0.###}");
        builder.Add("runtimeSetOffsetsExternal", runtimeInteraction.SetOffsetsExternalSourceCount);
        builder.Add("runtimeSetOffsetsHorizontalBar", runtimeInteraction.SetOffsetsHorizontalScrollBarSourceCount);
        builder.Add("runtimeSetOffsetsVerticalBar", runtimeInteraction.SetOffsetsVerticalScrollBarSourceCount);
        builder.Add("runtimeSetOffsetsWork", runtimeInteraction.SetOffsetsWorkCount);
        builder.Add("runtimeSetOffsetsNoOp", runtimeInteraction.SetOffsetsNoOpCount);
        builder.Add("runtimeSetOffsetsDeferredLayout", runtimeInteraction.SetOffsetsDeferredLayoutPathCount);
        builder.Add("runtimeSetOffsetsVirtualizingMeasureInvalidation", runtimeInteraction.SetOffsetsVirtualizingMeasureInvalidationPathCount);
        builder.Add("runtimeSetOffsetsVirtualizingArrangeOnly", runtimeInteraction.SetOffsetsVirtualizingArrangeOnlyPathCount);
        builder.Add("runtimeSetOffsetsTransformInvalidation", runtimeInteraction.SetOffsetsTransformInvalidationPathCount);
        builder.Add("runtimeSetOffsetsManualArrange", runtimeInteraction.SetOffsetsManualArrangePathCount);
        builder.Add("runtimePopupCloseCalls", runtimeInteraction.PopupCloseCallCount);
        builder.Add("runtimeArrangeContentCalls", runtimeInteraction.ArrangeContentForCurrentOffsetsCallCount);
        builder.Add("runtimeArrangeContentMs", $"{runtimeInteraction.ArrangeContentForCurrentOffsetsMilliseconds:0.###}");
        builder.Add("runtimeArrangeContentSkippedNoContent", runtimeInteraction.ArrangeContentSkippedNoContentCount);
        builder.Add("runtimeArrangeContentSkippedZeroViewport", runtimeInteraction.ArrangeContentSkippedZeroViewportCount);
        builder.Add("runtimeArrangeContentTransformPath", runtimeInteraction.ArrangeContentTransformPathCount);
        builder.Add("runtimeArrangeContentOffsetPath", runtimeInteraction.ArrangeContentOffsetPathCount);
        builder.Add("runtimeUpdateScrollBarValuesCalls", runtimeInteraction.UpdateScrollBarValuesCallCount);
        builder.Add("runtimeUpdateScrollBarValuesMs", $"{runtimeInteraction.UpdateScrollBarValuesMilliseconds:0.###}");
        builder.Add("runtimeUpdateHorizontalScrollBarValueCalls", runtimeInteraction.UpdateHorizontalScrollBarValueCallCount);
        builder.Add("runtimeUpdateHorizontalScrollBarValueMs", $"{runtimeInteraction.UpdateHorizontalScrollBarValueMilliseconds:0.###}");
        builder.Add("runtimeUpdateVerticalScrollBarValueCalls", runtimeInteraction.UpdateVerticalScrollBarValueCallCount);
        builder.Add("runtimeUpdateVerticalScrollBarValueMs", $"{runtimeInteraction.UpdateVerticalScrollBarValueMilliseconds:0.###}");

        var runtimeValueChanged = scrollViewer.GetRuntimeValueChangedTelemetryForDiagnostics();
        builder.Add("runtimeHorizontalValueChangedCalls", runtimeValueChanged.HorizontalValueChangedCallCount);
        builder.Add("runtimeHorizontalValueChangedMs", $"{runtimeValueChanged.HorizontalValueChangedMilliseconds:0.###}");
        builder.Add("runtimeHorizontalValueChangedSetOffsetsMs", $"{runtimeValueChanged.HorizontalValueChangedSetOffsetsMilliseconds:0.###}");
        builder.Add("runtimeHorizontalValueChangedSuppressed", runtimeValueChanged.HorizontalValueChangedSuppressedCount);
        builder.Add("runtimeVerticalValueChangedCalls", runtimeValueChanged.VerticalValueChangedCallCount);
        builder.Add("runtimeVerticalValueChangedMs", $"{runtimeValueChanged.VerticalValueChangedMilliseconds:0.###}");
        builder.Add("runtimeVerticalValueChangedSetOffsetsMs", $"{runtimeValueChanged.VerticalValueChangedSetOffsetsMilliseconds:0.###}");
        builder.Add("runtimeVerticalValueChangedSuppressed", runtimeValueChanged.VerticalValueChangedSuppressedCount);

        if (scrollViewer.TryGetContentViewportClipRect(out var clipRect))
        {
            builder.Add("contentViewport", $"{clipRect.X:0.##},{clipRect.Y:0.##},{clipRect.Width:0.##},{clipRect.Height:0.##}");
        }

        var contentElement = scrollViewer.Content as UIElement;
        if (contentElement == null)
        {
            foreach (var child in scrollViewer.GetVisualChildren())
            {
                if (child is ScrollBar)
                {
                    continue;
                }

                contentElement = child;
                break;
            }
        }

        if (contentElement != null)
        {
            builder.Add("contentType", contentElement.GetType().Name);
            builder.Add("transformContentScrolling", ScrollViewer.GetUseTransformContentScrolling(contentElement));
            builder.Add("contentSummary", DescribeElement(contentElement));

            if (contentElement is FrameworkElement frameworkContent)
            {
                builder.Add("contentSlot", FormatRect(frameworkContent.LayoutSlot));
                builder.Add("contentDesired", $"{frameworkContent.DesiredSize.X:0.##},{frameworkContent.DesiredSize.Y:0.##}");
                builder.Add("contentActual", $"{frameworkContent.ActualWidth:0.##},{frameworkContent.ActualHeight:0.##}");
                builder.Add("contentPreviousAvailable", $"{frameworkContent.PreviousAvailableSizeForTests.X:0.##},{frameworkContent.PreviousAvailableSizeForTests.Y:0.##}");
                builder.Add("contentMeasureCalls", frameworkContent.MeasureCallCount);
                builder.Add("contentMeasureWork", frameworkContent.MeasureWorkCount);
                builder.Add("contentArrangeCalls", frameworkContent.ArrangeCallCount);
                builder.Add("contentArrangeWork", frameworkContent.ArrangeWorkCount);
                builder.Add("contentMeasureMs", FormatMilliseconds(frameworkContent.MeasureElapsedTicksForTests));
                builder.Add("contentMeasureExclusiveMs", FormatMilliseconds(frameworkContent.MeasureExclusiveElapsedTicksForTests));
                builder.Add("contentArrangeMs", FormatMilliseconds(frameworkContent.ArrangeElapsedTicksForTests));
                builder.Add("contentMeasureValid", frameworkContent.IsMeasureValidForTests);
                builder.Add("contentArrangeValid", frameworkContent.IsArrangeValidForTests);
            }
        }

        AppendScrollBarDetails(builder, scrollViewer, Orientation.Horizontal, "hbar");
        AppendScrollBarDetails(builder, scrollViewer, Orientation.Vertical, "vbar");
    }

    private static void AppendScrollBarDetails(InkkOopsElementDiagnosticsBuilder builder, ScrollViewer scrollViewer, Orientation orientation, string prefix)
    {
        var scrollBar = FindScrollBar(scrollViewer, orientation);
        if (scrollBar == null)
        {
            builder.Add(prefix + "_present", false);
            return;
        }

        builder.Add(prefix + "_present", true);
        builder.Add(prefix + "_slot", FormatRect(scrollBar.LayoutSlot));
        builder.Add(prefix + "_desired", $"{scrollBar.DesiredSize.X:0.##},{scrollBar.DesiredSize.Y:0.##}");
        builder.Add(prefix + "_actual", $"{scrollBar.ActualWidth:0.##},{scrollBar.ActualHeight:0.##}");
        builder.Add(prefix + "_orientation", scrollBar.Orientation);
        builder.Add(prefix + "_min", $"{scrollBar.Minimum:0.###}");
        builder.Add(prefix + "_max", $"{scrollBar.Maximum:0.###}");
        builder.Add(prefix + "_value", $"{scrollBar.Value:0.###}");
        builder.Add(prefix + "_viewportSize", $"{scrollBar.ViewportSize:0.###}");
        builder.Add(prefix + "_smallChange", $"{scrollBar.SmallChange:0.###}");
        builder.Add(prefix + "_largeChange", $"{scrollBar.LargeChange:0.###}");
        builder.Add(prefix + "_focusable", scrollBar.Focusable);
        builder.Add(prefix + "_enabled", scrollBar.IsEnabled);
        builder.Add(prefix + "_background", FormatColor(scrollBar.Background));
        builder.Add(prefix + "_foreground", FormatColor(scrollBar.Foreground));
        builder.Add(prefix + "_borderBrush", FormatColor(scrollBar.BorderBrush));
        builder.Add(prefix + "_borderThickness", FormatThickness(scrollBar.BorderThickness));
        builder.Add(prefix + "_measureCalls", scrollBar.MeasureCallCount);
        builder.Add(prefix + "_measureWork", scrollBar.MeasureWorkCount);
        builder.Add(prefix + "_arrangeCalls", scrollBar.ArrangeCallCount);
        builder.Add(prefix + "_arrangeWork", scrollBar.ArrangeWorkCount);
        builder.Add(prefix + "_measureMs", FormatMilliseconds(scrollBar.MeasureElapsedTicksForTests));
        builder.Add(prefix + "_arrangeMs", FormatMilliseconds(scrollBar.ArrangeElapsedTicksForTests));

        var track = FindDescendant<Track>(scrollBar);
        if (track == null)
        {
            builder.Add(prefix + "_trackPresent", false);
            return;
        }

        builder.Add(prefix + "_trackPresent", true);
        builder.Add(prefix + "_trackSlot", FormatRect(track.LayoutSlot));
        builder.Add(prefix + "_trackRect", FormatRect(track.GetTrackRect()));
        builder.Add(prefix + "_thumbRect", FormatRect(track.GetThumbRect()));
        builder.Add(prefix + "_trackOrientation", track.Orientation);
        builder.Add(prefix + "_trackMin", $"{track.Minimum:0.###}");
        builder.Add(prefix + "_trackMax", $"{track.Maximum:0.###}");
        builder.Add(prefix + "_trackValue", $"{track.Value:0.###}");
        builder.Add(prefix + "_trackViewportSize", $"{track.ViewportSize:0.###}");
        builder.Add(prefix + "_trackViewportSizedThumb", track.IsViewportSizedThumb);
        builder.Add(prefix + "_trackDirectionReversed", track.IsDirectionReversed);
        builder.Add(prefix + "_trackThumbLength", $"{track.ThumbLength:0.###}");
        builder.Add(prefix + "_trackThumbMinLength", $"{track.ThumbMinLength:0.###}");
        builder.Add(prefix + "_trackThickness", $"{track.TrackThickness:0.###}");
        builder.Add(prefix + "_trackBorderBrush", FormatColor(track.BorderBrush));
        builder.Add(prefix + "_trackBorderThickness", FormatThickness(track.BorderThickness));
        builder.Add(prefix + "_trackMeasureCalls", track.MeasureCallCount);
        builder.Add(prefix + "_trackMeasureWork", track.MeasureWorkCount);
        builder.Add(prefix + "_trackArrangeCalls", track.ArrangeCallCount);
        builder.Add(prefix + "_trackArrangeWork", track.ArrangeWorkCount);
        builder.Add(prefix + "_trackMeasureMs", FormatMilliseconds(track.MeasureElapsedTicksForTests));
        builder.Add(prefix + "_trackArrangeMs", FormatMilliseconds(track.ArrangeElapsedTicksForTests));
        builder.Add(prefix + "_trackParts", BuildTrackPartsSummary(track));

        var thumb = FindDescendant<Thumb>(track);
        if (thumb == null)
        {
            builder.Add(prefix + "_thumbPresent", false);
            return;
        }

        builder.Add(prefix + "_thumbPresent", true);
        builder.Add(prefix + "_thumbSlot", FormatRect(thumb.LayoutSlot));
        builder.Add(prefix + "_thumbDesired", $"{thumb.DesiredSize.X:0.##},{thumb.DesiredSize.Y:0.##}");
        builder.Add(prefix + "_thumbActual", $"{thumb.ActualWidth:0.##},{thumb.ActualHeight:0.##}");
        builder.Add(prefix + "_thumbDragging", thumb.IsDragging);
        builder.Add(prefix + "_thumbMouseOver", thumb.IsMouseOver);
        builder.Add(prefix + "_thumbFocusable", thumb.Focusable);
        builder.Add(prefix + "_thumbEnabled", thumb.IsEnabled);
        builder.Add(prefix + "_thumbBackground", FormatColor(thumb.Background));
        builder.Add(prefix + "_thumbBorderBrush", FormatColor(thumb.BorderBrush));
        builder.Add(prefix + "_thumbMeasureCalls", thumb.MeasureCallCount);
        builder.Add(prefix + "_thumbMeasureWork", thumb.MeasureWorkCount);
        builder.Add(prefix + "_thumbArrangeCalls", thumb.ArrangeCallCount);
        builder.Add(prefix + "_thumbArrangeWork", thumb.ArrangeWorkCount);
    }

    private static ScrollBar? FindScrollBar(ScrollViewer scrollViewer, Orientation orientation)
    {
        foreach (var child in scrollViewer.GetVisualChildren())
        {
            if (child is ScrollBar scrollBar && scrollBar.Orientation == orientation)
            {
                return scrollBar;
            }
        }

        return null;
    }

    private static T? FindDescendant<T>(UIElement root) where T : UIElement
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (child is T typed)
            {
                return typed;
            }

            var nested = FindDescendant<T>(child);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static string BuildTrackPartsSummary(Track track)
    {
        var parts = new List<string>();
        foreach (var child in track.GetVisualChildren())
        {
            var role = Track.GetPartRole(child);
            if (child is FrameworkElement frameworkChild)
            {
                parts.Add($"{DescribeElement(child)}:{role}@{FormatRect(frameworkChild.LayoutSlot)}");
            }
            else
            {
                parts.Add($"{DescribeElement(child)}:{role}");
            }
        }

        return parts.Count == 0 ? "none" : string.Join(" | ", parts);
    }

    private static bool IsHorizontalBarVisible(ScrollViewer scrollViewer)
    {
        return scrollViewer.HorizontalScrollBarVisibility switch
        {
            ScrollBarVisibility.Visible => true,
            ScrollBarVisibility.Auto => scrollViewer.ExtentWidth > scrollViewer.ViewportWidth + 0.01f,
            _ => false
        };
    }

    private static bool IsVerticalBarVisible(ScrollViewer scrollViewer)
    {
        return scrollViewer.VerticalScrollBarVisibility switch
        {
            ScrollBarVisibility.Visible => true,
            ScrollBarVisibility.Auto => scrollViewer.ExtentHeight > scrollViewer.ViewportHeight + 0.01f,
            _ => false
        };
    }

    private static float SafeRatio(float numerator, float denominator)
    {
        return denominator <= 0.01f ? 0f : numerator / denominator;
    }

    private static string DescribeElement(UIElement element)
    {
        return element is FrameworkElement frameworkElement && !string.IsNullOrWhiteSpace(frameworkElement.Name)
            ? $"{element.GetType().Name}#{frameworkElement.Name}"
            : element.GetType().Name;
    }

    private static string FormatRect(LayoutRect rect)
    {
        return $"{rect.X:0.##},{rect.Y:0.##},{rect.Width:0.##},{rect.Height:0.##}";
    }

    private static string FormatThickness(Thickness thickness)
    {
        return $"{thickness.Left:0.##},{thickness.Top:0.##},{thickness.Right:0.##},{thickness.Bottom:0.##}";
    }

    private static string FormatColor(Microsoft.Xna.Framework.Color color)
    {
        return $"{color.R},{color.G},{color.B},{color.A}";
    }

    private static string FormatMilliseconds(long ticks)
    {
        return ((double)ticks * 1000d / System.Diagnostics.Stopwatch.Frequency).ToString("0.###");
    }

    private static string FormatFloat(float value)
    {
        if (float.IsNaN(value))
        {
            return "NaN";
        }

        if (float.IsPositiveInfinity(value))
        {
            return "+Infinity";
        }

        if (float.IsNegativeInfinity(value))
        {
            return "-Infinity";
        }

        return value.ToString("0.##");
    }
}