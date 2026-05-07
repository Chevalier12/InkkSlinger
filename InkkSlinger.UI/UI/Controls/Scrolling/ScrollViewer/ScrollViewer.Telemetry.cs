using System;
using System.Collections.Generic;
using System.Diagnostics;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class ScrollViewer
{
    internal new static ScrollViewerTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return CreateAggregateTelemetrySnapshot();
    }

    internal new static ScrollViewerTelemetrySnapshot GetTelemetryAndReset()
    {
        var snapshot = CreateAggregateTelemetrySnapshot();
        _diagWheelEvents = 0;
        _diagWheelHandled = 0;
        _diagSetOffsetCalls = 0;
        _diagSetOffsetNoOp = 0;
        _diagHorizontalDelta = 0f;
        _diagVerticalDelta = 0f;
        _diagScrollToHorizontalOffsetCallCount = 0;
        _diagScrollToVerticalOffsetCallCount = 0;
        _diagInvalidateScrollInfoCallCount = 0;
        _diagHandleMouseWheelCallCount = 0;
        _diagHandleMouseWheelElapsedTicks = 0L;
        _diagHandleMouseWheelHandledCount = 0;
        _diagHandleMouseWheelIgnoredDisabledCount = 0;
        _diagHandleMouseWheelIgnoredZeroDeltaCount = 0;
        _diagInteractionSetOffsetsCallCount = 0;
        _diagInteractionSetOffsetsNoOpCount = 0;
        _diagSetOffsetsElapsedTicks = 0L;
        _diagSetOffsetsExternalSourceCount = 0;
        _diagSetOffsetsHorizontalScrollBarSourceCount = 0;
        _diagSetOffsetsVerticalScrollBarSourceCount = 0;
        _diagSetOffsetsWorkCount = 0;
        _diagSetOffsetsDeferredLayoutPathCount = 0;
        _diagSetOffsetsVirtualizingMeasureInvalidationPathCount = 0;
        _diagSetOffsetsVirtualizingArrangeOnlyPathCount = 0;
        _diagSetOffsetsTransformInvalidationPathCount = 0;
        _diagSetOffsetsManualArrangePathCount = 0;
        _diagPopupCloseCallCount = 0;
        _diagArrangeContentForCurrentOffsetsCallCount = 0;
        _diagArrangeContentForCurrentOffsetsElapsedTicks = 0L;
        _diagArrangeContentSkippedNoContentCount = 0;
        _diagArrangeContentSkippedZeroViewportCount = 0;
        _diagArrangeContentTransformPathCount = 0;
        _diagArrangeContentOffsetPathCount = 0;
        _diagUpdateScrollBarValuesCallCount = 0;
        _diagUpdateScrollBarValuesElapsedTicks = 0L;
        _diagUpdateHorizontalScrollBarValueCallCount = 0;
        _diagUpdateHorizontalScrollBarValueElapsedTicks = 0L;
        _diagUpdateVerticalScrollBarValueCallCount = 0;
        _diagUpdateVerticalScrollBarValueElapsedTicks = 0L;
        _diagHorizontalValueChangedCallCount = 0;
        _diagHorizontalValueChangedElapsedTicks = 0L;
        _diagHorizontalValueChangedSetOffsetsElapsedTicks = 0L;
        _diagHorizontalValueChangedSuppressedCount = 0;
        _diagVerticalValueChangedCallCount = 0;
        _diagVerticalValueChangedElapsedTicks = 0L;
        _diagVerticalValueChangedSetOffsetsElapsedTicks = 0L;
        _diagVerticalValueChangedSuppressedCount = 0;
        _diagMeasureOverrideCallCount = 0;
        _diagMeasureOverrideElapsedTicks = 0L;
        _diagArrangeOverrideCallCount = 0;
        _diagArrangeOverrideElapsedTicks = 0L;
        _diagResolveBarsAndMeasureContentCallCount = 0;
        _diagResolveBarsAndMeasureContentElapsedTicks = 0L;
        _diagResolveBarsAndMeasureContentIterationCount = 0;
        _diagResolveBarsAndMeasureContentHorizontalFlipCount = 0;
        _diagResolveBarsAndMeasureContentVerticalFlipCount = 0;
        _diagResolveBarsAndMeasureContentSingleMeasurePathCount = 0;
        _diagResolveBarsAndMeasureContentRemeasurePathCount = 0;
        _diagResolveBarsAndMeasureContentFallbackCount = 0;
        _diagResolveBarsAndMeasureContentInitialHorizontalVisibleCount = 0;
        _diagResolveBarsAndMeasureContentInitialHorizontalHiddenCount = 0;
        _diagResolveBarsAndMeasureContentInitialVerticalVisibleCount = 0;
        _diagResolveBarsAndMeasureContentInitialVerticalHiddenCount = 0;
        _diagResolveBarsAndMeasureContentResolvedHorizontalVisibleCount = 0;
        _diagResolveBarsAndMeasureContentResolvedHorizontalHiddenCount = 0;
        _diagResolveBarsAndMeasureContentResolvedVerticalVisibleCount = 0;
        _diagResolveBarsAndMeasureContentResolvedVerticalHiddenCount = 0;
        _diagResolveBarsForArrangeCallCount = 0;
        _diagResolveBarsForArrangeElapsedTicks = 0L;
        _diagResolveBarsForArrangeIterationCount = 0;
        _diagResolveBarsForArrangeHorizontalFlipCount = 0;
        _diagResolveBarsForArrangeVerticalFlipCount = 0;
        _diagMeasureContentCallCount = 0;
        _diagMeasureContentElapsedTicks = 0L;
        _diagUpdateScrollBarsCallCount = 0;
        _diagUpdateScrollBarsElapsedTicks = 0L;
        return snapshot;
    }

    internal ScrollViewerRuntimeDiagnosticsSnapshot GetScrollViewerSnapshotForDiagnostics()
    {
        return new ScrollViewerRuntimeDiagnosticsSnapshot(
            _showHorizontalBar,
            _showVerticalBar,
            _hasPreviousScrollBarResolution,
            _previousShowHorizontalScrollBar,
            _previousShowVerticalScrollBar,
            _suppressInternalScrollBarValueChange,
            _inputScrollMutationDepth,
            _contentViewportRect.X,
            _contentViewportRect.Y,
            _contentViewportRect.Width,
            _contentViewportRect.Height,
            _runtimeResolveBarsAndMeasureContentLastTrace,
            _runtimeResolveBarsAndMeasureContentHottestTrace,
            TicksToMilliseconds(_runtimeResolveBarsAndMeasureContentHottestTicks),
            _runtimeWheelEvents,
            _runtimeWheelHandled,
            _runtimeSetOffsetsCallCount,
            _runtimeSetOffsetsNoOpCount,
            _runtimeHorizontalDelta,
            _runtimeVerticalDelta,
            _runtimeScrollToHorizontalOffsetCallCount,
            _runtimeScrollToVerticalOffsetCallCount,
            _runtimeInvalidateScrollInfoCallCount,
            _runtimeHandleMouseWheelCallCount,
            TicksToMilliseconds(_runtimeHandleMouseWheelElapsedTicks),
            _runtimeHandleMouseWheelHandledCount,
            _runtimeHandleMouseWheelIgnoredDisabledCount,
            _runtimeHandleMouseWheelIgnoredZeroDeltaCount,
            TicksToMilliseconds(_runtimeSetOffsetsElapsedTicks),
            _runtimeSetOffsetsExternalSourceCount,
            _runtimeSetOffsetsHorizontalScrollBarSourceCount,
            _runtimeSetOffsetsVerticalScrollBarSourceCount,
            _runtimeSetOffsetsWorkCount,
            _runtimeSetOffsetsDeferredLayoutPathCount,
            _runtimeSetOffsetsVirtualizingMeasureInvalidationPathCount,
            _runtimeSetOffsetsVirtualizingArrangeOnlyPathCount,
            _runtimeSetOffsetsTransformInvalidationPathCount,
            _runtimeSetOffsetsManualArrangePathCount,
            _runtimePopupCloseCallCount,
            _runtimeArrangeContentForCurrentOffsetsCallCount,
            TicksToMilliseconds(_runtimeArrangeContentForCurrentOffsetsElapsedTicks),
            _runtimeArrangeContentSkippedNoContentCount,
            _runtimeArrangeContentSkippedZeroViewportCount,
            _runtimeArrangeContentTransformPathCount,
            _runtimeArrangeContentOffsetPathCount,
            _runtimeUpdateScrollBarValuesCallCount,
            TicksToMilliseconds(_runtimeUpdateScrollBarValuesElapsedTicks),
            _runtimeUpdateHorizontalScrollBarValueCallCount,
            TicksToMilliseconds(_runtimeUpdateHorizontalScrollBarValueElapsedTicks),
            _runtimeUpdateVerticalScrollBarValueCallCount,
            TicksToMilliseconds(_runtimeUpdateVerticalScrollBarValueElapsedTicks),
            _runtimeHorizontalValueChangedCallCount,
            TicksToMilliseconds(_runtimeHorizontalValueChangedElapsedTicks),
            TicksToMilliseconds(_runtimeHorizontalValueChangedSetOffsetsElapsedTicks),
            _runtimeHorizontalValueChangedSuppressedCount,
            _runtimeVerticalValueChangedCallCount,
            TicksToMilliseconds(_runtimeVerticalValueChangedElapsedTicks),
            TicksToMilliseconds(_runtimeVerticalValueChangedSetOffsetsElapsedTicks),
            _runtimeVerticalValueChangedSuppressedCount,
            _runtimeMeasureOverrideCallCount,
            TicksToMilliseconds(_runtimeMeasureOverrideElapsedTicks),
            _runtimeArrangeOverrideCallCount,
            TicksToMilliseconds(_runtimeArrangeOverrideElapsedTicks),
            _runtimeResolveBarsAndMeasureContentCallCount,
            TicksToMilliseconds(_runtimeResolveBarsAndMeasureContentElapsedTicks),
            _runtimeResolveBarsAndMeasureContentIterationCount,
            _runtimeResolveBarsAndMeasureContentHorizontalFlipCount,
            _runtimeResolveBarsAndMeasureContentVerticalFlipCount,
            _runtimeResolveBarsAndMeasureContentSingleMeasurePathCount,
            _runtimeResolveBarsAndMeasureContentRemeasurePathCount,
            _runtimeResolveBarsAndMeasureContentFallbackCount,
            _runtimeResolveBarsAndMeasureContentInitialHorizontalVisibleCount,
            _runtimeResolveBarsAndMeasureContentInitialHorizontalHiddenCount,
            _runtimeResolveBarsAndMeasureContentInitialVerticalVisibleCount,
            _runtimeResolveBarsAndMeasureContentInitialVerticalHiddenCount,
            _runtimeResolveBarsAndMeasureContentResolvedHorizontalVisibleCount,
            _runtimeResolveBarsAndMeasureContentResolvedHorizontalHiddenCount,
            _runtimeResolveBarsAndMeasureContentResolvedVerticalVisibleCount,
            _runtimeResolveBarsAndMeasureContentResolvedVerticalHiddenCount,
            _runtimeResolveBarsForArrangeCallCount,
            TicksToMilliseconds(_runtimeResolveBarsForArrangeElapsedTicks),
            _runtimeResolveBarsForArrangeIterationCount,
            _runtimeResolveBarsForArrangeHorizontalFlipCount,
            _runtimeResolveBarsForArrangeVerticalFlipCount,
            _runtimeMeasureContentCallCount,
            TicksToMilliseconds(_runtimeMeasureContentElapsedTicks),
            _runtimeUpdateScrollBarsCallCount,
            TicksToMilliseconds(_runtimeUpdateScrollBarsElapsedTicks));
    }

    private static ScrollViewerTelemetrySnapshot CreateAggregateTelemetrySnapshot()
    {
        return new ScrollViewerTelemetrySnapshot(
            _diagWheelEvents,
            _diagWheelHandled,
            _diagSetOffsetCalls,
            _diagSetOffsetNoOp,
            _diagHorizontalDelta,
            _diagVerticalDelta,
            _diagScrollToHorizontalOffsetCallCount,
            _diagScrollToVerticalOffsetCallCount,
            _diagInvalidateScrollInfoCallCount,
            _diagHandleMouseWheelCallCount,
            TicksToMilliseconds(_diagHandleMouseWheelElapsedTicks),
            _diagHandleMouseWheelHandledCount,
            _diagHandleMouseWheelIgnoredDisabledCount,
            _diagHandleMouseWheelIgnoredZeroDeltaCount,
            TicksToMilliseconds(_diagSetOffsetsElapsedTicks),
            _diagSetOffsetsExternalSourceCount,
            _diagSetOffsetsHorizontalScrollBarSourceCount,
            _diagSetOffsetsVerticalScrollBarSourceCount,
            _diagSetOffsetsWorkCount,
            _diagSetOffsetsDeferredLayoutPathCount,
            _diagSetOffsetsVirtualizingMeasureInvalidationPathCount,
            _diagSetOffsetsVirtualizingArrangeOnlyPathCount,
            _diagSetOffsetsTransformInvalidationPathCount,
            _diagSetOffsetsManualArrangePathCount,
            _diagPopupCloseCallCount,
            _diagArrangeContentForCurrentOffsetsCallCount,
            TicksToMilliseconds(_diagArrangeContentForCurrentOffsetsElapsedTicks),
            _diagArrangeContentSkippedNoContentCount,
            _diagArrangeContentSkippedZeroViewportCount,
            _diagArrangeContentTransformPathCount,
            _diagArrangeContentOffsetPathCount,
            _diagUpdateScrollBarValuesCallCount,
            TicksToMilliseconds(_diagUpdateScrollBarValuesElapsedTicks),
            _diagUpdateHorizontalScrollBarValueCallCount,
            TicksToMilliseconds(_diagUpdateHorizontalScrollBarValueElapsedTicks),
            _diagUpdateVerticalScrollBarValueCallCount,
            TicksToMilliseconds(_diagUpdateVerticalScrollBarValueElapsedTicks),
            _diagHorizontalValueChangedCallCount,
            TicksToMilliseconds(_diagHorizontalValueChangedElapsedTicks),
            TicksToMilliseconds(_diagHorizontalValueChangedSetOffsetsElapsedTicks),
            _diagHorizontalValueChangedSuppressedCount,
            _diagVerticalValueChangedCallCount,
            TicksToMilliseconds(_diagVerticalValueChangedElapsedTicks),
            TicksToMilliseconds(_diagVerticalValueChangedSetOffsetsElapsedTicks),
            _diagVerticalValueChangedSuppressedCount,
            _diagMeasureOverrideCallCount,
            TicksToMilliseconds(_diagMeasureOverrideElapsedTicks),
            _diagArrangeOverrideCallCount,
            TicksToMilliseconds(_diagArrangeOverrideElapsedTicks),
            _diagResolveBarsAndMeasureContentCallCount,
            TicksToMilliseconds(_diagResolveBarsAndMeasureContentElapsedTicks),
            _diagResolveBarsAndMeasureContentIterationCount,
            _diagResolveBarsAndMeasureContentHorizontalFlipCount,
            _diagResolveBarsAndMeasureContentVerticalFlipCount,
            _diagResolveBarsAndMeasureContentSingleMeasurePathCount,
            _diagResolveBarsAndMeasureContentRemeasurePathCount,
            _diagResolveBarsAndMeasureContentFallbackCount,
            _diagResolveBarsAndMeasureContentInitialHorizontalVisibleCount,
            _diagResolveBarsAndMeasureContentInitialHorizontalHiddenCount,
            _diagResolveBarsAndMeasureContentInitialVerticalVisibleCount,
            _diagResolveBarsAndMeasureContentInitialVerticalHiddenCount,
            _diagResolveBarsAndMeasureContentResolvedHorizontalVisibleCount,
            _diagResolveBarsAndMeasureContentResolvedHorizontalHiddenCount,
            _diagResolveBarsAndMeasureContentResolvedVerticalVisibleCount,
            _diagResolveBarsAndMeasureContentResolvedVerticalHiddenCount,
            _diagResolveBarsForArrangeCallCount,
            TicksToMilliseconds(_diagResolveBarsForArrangeElapsedTicks),
            _diagResolveBarsForArrangeIterationCount,
            _diagResolveBarsForArrangeHorizontalFlipCount,
            _diagResolveBarsForArrangeVerticalFlipCount,
            _diagMeasureContentCallCount,
            TicksToMilliseconds(_diagMeasureContentElapsedTicks),
            _diagUpdateScrollBarsCallCount,
            TicksToMilliseconds(_diagUpdateScrollBarsElapsedTicks));
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }

}
