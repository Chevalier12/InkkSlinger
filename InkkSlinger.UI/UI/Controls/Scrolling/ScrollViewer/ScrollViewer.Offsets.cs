using System;
using System.Collections.Generic;
using System.Diagnostics;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class ScrollViewer
{
    private bool CoerceOffsetsToCurrentMetrics(bool closeAnchoredPopups)
    {
        var previousHorizontal = HorizontalOffset;
        var previousVertical = VerticalOffset;
        var maxHorizontal = MathF.Max(0f, ExtentWidth - ViewportWidth);
        var maxVertical = MathF.Max(0f, ExtentHeight - ViewportHeight);
        var nextHorizontal = ClampOffsetCandidate(previousHorizontal, maxHorizontal, previousHorizontal);
        var nextVertical = ClampOffsetCandidate(previousVertical, maxVertical, previousVertical);
        if (AreClose(previousHorizontal, nextHorizontal) && AreClose(previousVertical, nextVertical))
        {
            return false;
        }

        WriteOffsetProperties(nextHorizontal, nextVertical);
        if (closeAnchoredPopups)
        {
            Popup.CloseAnchoredPopupsWithin(this);
            _diagPopupCloseCallCount++;
            _runtimePopupCloseCallCount++;
        }

        return true;
    }

    private void SetOffsets(
        float horizontal,
        float vertical,
        ScrollOffsetUpdateSource updateSource = ScrollOffsetUpdateSource.External,
        float? previousHorizontalOverride = null,
        float? previousVerticalOverride = null)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagSetOffsetCalls++;
        _diagInteractionSetOffsetsCallCount++;
        _runtimeSetOffsetsCallCount++;
        RecordSetOffsetSource(updateSource);
        var beforeHorizontal = previousHorizontalOverride ?? HorizontalOffset;
        var beforeVertical = previousVerticalOverride ?? VerticalOffset;
        var previousExtentWidth = ExtentWidth;
        var previousExtentHeight = ExtentHeight;
        var previousViewportWidth = ViewportWidth;
        var previousViewportHeight = ViewportHeight;
        var previousScrollableWidth = ScrollableWidth;
        var previousScrollableHeight = ScrollableHeight;
        var requestedHorizontal = horizontal;
        var requestedVertical = vertical;
        var maxHorizontal = MathF.Max(0f, ExtentWidth - ViewportWidth);
        var maxVertical = MathF.Max(0f, ExtentHeight - ViewportHeight);
        var candidateVirtualizingStackPanel = ContentElement as VirtualizingStackPanel;
        if (TryRefreshVirtualizingMetricsForOffsetCandidate(candidateVirtualizingStackPanel, horizontal, vertical, maxHorizontal, maxVertical))
        {
            maxHorizontal = MathF.Max(0f, ExtentWidth - ViewportWidth);
            maxVertical = MathF.Max(0f, ExtentHeight - ViewportHeight);
        }

        requestedVertical = AlignViewerOwnedWheelOffset(candidateVirtualizingStackPanel, requestedVertical, beforeVertical);

        var nextHorizontal = ClampOffsetCandidate(requestedHorizontal, maxHorizontal, beforeHorizontal);
        var nextVertical = ClampOffsetCandidate(requestedVertical, maxVertical, beforeVertical);
        var horizontalDelta = MathF.Abs(beforeHorizontal - nextHorizontal);
        var verticalDelta = MathF.Abs(beforeVertical - nextVertical);

        _diagHorizontalDelta += horizontalDelta;
        _diagVerticalDelta += verticalDelta;
        _runtimeHorizontalDelta += horizontalDelta;
        _runtimeVerticalDelta += verticalDelta;
        if (horizontalDelta <= 0.001f && verticalDelta <= 0.001f)
        {
            _diagSetOffsetNoOp++;
            _diagInteractionSetOffsetsNoOpCount++;
            _runtimeSetOffsetsNoOpCount++;
            if (updateSource == ScrollOffsetUpdateSource.External)
            {
                UpdateScrollBarValues();
            }

            _diagSetOffsetsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            _runtimeSetOffsetsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;

            return;
        }

        _diagSetOffsetsWorkCount++;
        _runtimeSetOffsetsWorkCount++;
        var canApplyOffsetWithoutDeferredLayout = CanApplyOffsetWithoutDeferredLayout();
        WriteOffsetProperties(nextHorizontal, nextVertical);
        UpdateDerivedScrollState();
        var viewportChangedStartTicks = Stopwatch.GetTimestamp();
        ViewportChanged?.Invoke(this, EventArgs.Empty);
        startTicks += Stopwatch.GetTimestamp() - viewportChangedStartTicks;
        RaiseScrollChangedIfNeeded(
            beforeHorizontal,
            beforeVertical,
            previousExtentWidth,
            previousExtentHeight,
            previousViewportWidth,
            previousViewportHeight,
            previousScrollableWidth,
            previousScrollableHeight);

        ApplyOffsetToContent(
            beforeHorizontal,
            nextHorizontal,
            beforeVertical,
            nextVertical,
            canApplyOffsetWithoutDeferredLayout,
            updateSource);
        SyncScrollBarsAfterOffset(updateSource);
        CloseAnchoredPopupsAfterOffset();
        _diagSetOffsetsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _runtimeSetOffsetsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
    }

    private void RecordSetOffsetSource(ScrollOffsetUpdateSource updateSource)
    {
        switch (updateSource)
        {
            case ScrollOffsetUpdateSource.External:
                _diagSetOffsetsExternalSourceCount++;
                _runtimeSetOffsetsExternalSourceCount++;
                break;
            case ScrollOffsetUpdateSource.HorizontalScrollBar:
                _diagSetOffsetsHorizontalScrollBarSourceCount++;
                _runtimeSetOffsetsHorizontalScrollBarSourceCount++;
                break;
            case ScrollOffsetUpdateSource.VerticalScrollBar:
                _diagSetOffsetsVerticalScrollBarSourceCount++;
                _runtimeSetOffsetsVerticalScrollBarSourceCount++;
                break;
        }
    }

    private bool TryRefreshVirtualizingMetricsForOffsetCandidate(
        VirtualizingStackPanel? virtualizingStackPanel,
        float horizontal,
        float vertical,
        float maxHorizontal,
        float maxVertical)
    {
        if (virtualizingStackPanel == null ||
            (horizontal <= maxHorizontal + 0.001f && vertical <= maxVertical + 0.001f) ||
            !virtualizingStackPanel.TryRefreshForViewerOwnedOffsetCandidate(horizontal, vertical))
        {
            return false;
        }

        SyncViewerOwnedVirtualizingScrollMetrics(virtualizingStackPanel);
        return true;
    }

    private float AlignViewerOwnedWheelOffset(VirtualizingStackPanel? virtualizingStackPanel, float requestedVertical, float beforeVertical)
    {
        if (_inputScrollMutationDepth <= 0 ||
            virtualizingStackPanel == null ||
            AreClose(requestedVertical, beforeVertical) ||
            !virtualizingStackPanel.TryAlignViewerOwnedVerticalWheelOffset(
                requestedVertical,
                ViewportHeight,
                requestedVertical > beforeVertical,
                out var alignedVertical))
        {
            return requestedVertical;
        }

        return alignedVertical;
    }

    private void ApplyOffsetToContent(
        float beforeHorizontal,
        float nextHorizontal,
        float beforeVertical,
        float nextVertical,
        bool canApplyOffsetWithoutDeferredLayout,
        ScrollOffsetUpdateSource updateSource = ScrollOffsetUpdateSource.External)
    {
        if (!canApplyOffsetWithoutDeferredLayout)
        {
            _diagSetOffsetsDeferredLayoutPathCount++;
            _runtimeSetOffsetsDeferredLayoutPathCount++;
            return;
        }

        if (ShouldDeferContentOffsetForThumbDrag(updateSource))
        {
            return;
        }

        var contentBeforeHorizontal = IsDeferredScrollingEnabled ? ContentHorizontalOffset : beforeHorizontal;
        var contentBeforeVertical = IsDeferredScrollingEnabled ? ContentVerticalOffset : beforeVertical;
        if (ContentElement is VirtualizingStackPanel virtualizingStackPanel)
        {
            if (LogicalScrollInfo is { } logicalScrollInfo)
            {
                ApplyLogicalOffset(logicalScrollInfo, nextHorizontal, nextVertical);
            }
            else
            {
                ApplyVirtualizingOffset(virtualizingStackPanel, contentBeforeHorizontal, nextHorizontal, contentBeforeVertical, nextVertical);
            }
        }
        else if (UsesTransformBasedContentScrolling())
        {
            ApplyTransformOffset();
        }
        else
        {
            ApplyManualOffset();
        }
    }

    private void ApplyLogicalOffset(IScrollInfo scrollInfo, float nextHorizontal, float nextVertical)
    {
        scrollInfo.SetHorizontalOffset(nextHorizontal);
        scrollInfo.SetVerticalOffset(nextVertical);
        _ = ApplyScrollMetrics(
            scrollInfo.ExtentWidth,
            scrollInfo.ExtentHeight,
            scrollInfo.ViewportWidth,
            scrollInfo.ViewportHeight,
            publishViewportMetrics: true);
    }

    private void ApplyVirtualizingOffset(
        VirtualizingStackPanel virtualizingStackPanel,
        float beforeHorizontal,
        float nextHorizontal,
        float beforeVertical,
        float nextVertical)
    {
        if (virtualizingStackPanel.TryHandleViewerOwnedOffsetChange(
                beforeHorizontal,
                nextHorizontal,
                beforeVertical,
                nextVertical,
                out var requiresMeasure,
                out var visualOnly))
        {
            ApplyVirtualizingArrangeOnlyOffset(virtualizingStackPanel, nextHorizontal, nextVertical, visualOnly);
            return;
        }

        if (requiresMeasure)
        {
            _diagSetOffsetsVirtualizingMeasureInvalidationPathCount++;
            _runtimeSetOffsetsVirtualizingMeasureInvalidationPathCount++;
            virtualizingStackPanel.InvalidateMeasure();
            return;
        }

        ApplyVirtualizingArrangeOnlyOffset(virtualizingStackPanel, nextHorizontal, nextVertical, visualOnly: false);
    }

    private void ApplyVirtualizingArrangeOnlyOffset(
        VirtualizingStackPanel virtualizingStackPanel,
        float nextHorizontal,
        float nextVertical,
        bool visualOnly)
    {
        SyncViewerOwnedVirtualizingScrollMetrics(virtualizingStackPanel);
        _diagSetOffsetsVirtualizingArrangeOnlyPathCount++;
        _runtimeSetOffsetsVirtualizingArrangeOnlyPathCount++;
        if (visualOnly)
        {
            UiRoot.Current?.NotifyDirectRenderInvalidation(virtualizingStackPanel, requireDeepSync: true);
            return;
        }

        if (!virtualizingStackPanel.TryArrangeForViewerOwnedOffset(nextHorizontal, nextVertical))
        {
            virtualizingStackPanel.InvalidateArrange();
            InvalidateArrange();
        }
    }

    private void ApplyTransformOffset()
    {
        _diagSetOffsetsTransformInvalidationPathCount++;
        _runtimeSetOffsetsTransformInvalidationPathCount++;
        NotifyTransformContentRenderChanged();
    }

    private void NotifyTransformContentRenderChanged()
    {
        if (TryGetContentViewportClipRect(out var contentViewport))
        {
            NotifyTransformContentRenderChanged(contentViewport);
        }
        else if (ContentElement is UIElement contentElement)
        {
            RecordTransformScrollDirtyHintFrame();
            UiRoot.Current?.NotifyDirectRenderInvalidation(contentElement);
        }
    }

    private void NotifyTransformContentRenderChanged(LayoutRect contentViewport)
    {
        RecordTransformScrollDirtyHintFrame();
        if (HasTransformStableLayerContent())
        {
            UiRoot.Current?.NotifyScrollViewportChanged(this, contentViewport);
            return;
        }

        if (ContentElement is UIElement contentElement)
        {
            UiRoot.Current?.NotifyDirectRenderInvalidation(contentElement);
        }
    }

    private void ApplyManualOffset()
    {
        _diagSetOffsetsManualArrangePathCount++;
        _runtimeSetOffsetsManualArrangePathCount++;
        ArrangeContentForCurrentOffsets();
        InvalidateVisual();
    }

    private bool ShouldDeferContentOffsetForThumbDrag(ScrollOffsetUpdateSource updateSource)
    {
        return IsDeferredScrollingEnabled &&
               ((updateSource == ScrollOffsetUpdateSource.HorizontalScrollBar && _horizontalBar.IsThumbDragInProgress) ||
                (updateSource == ScrollOffsetUpdateSource.VerticalScrollBar && _verticalBar.IsThumbDragInProgress));
    }

    private void CommitDeferredContentOffset()
    {
        if (!IsDeferredScrollingEnabled)
        {
            return;
        }

        var beforeHorizontal = ContentHorizontalOffset;
        var beforeVertical = ContentVerticalOffset;
        SetIfChanged(ContentHorizontalOffsetProperty, HorizontalOffset);
        SetIfChanged(ContentVerticalOffsetProperty, VerticalOffset);
        ApplyOffsetToContent(
            beforeHorizontal,
            HorizontalOffset,
            beforeVertical,
            VerticalOffset,
            CanApplyOffsetWithoutDeferredLayout());
    }

    private void OnScrollBarThumbDragCompleted(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        CommitDeferredContentOffset();
    }

    private void SyncScrollBarsAfterOffset(ScrollOffsetUpdateSource updateSource)
    {
        switch (updateSource)
        {
            case ScrollOffsetUpdateSource.External:
                UpdateScrollBarValues();
                break;
            case ScrollOffsetUpdateSource.HorizontalScrollBar:
                UpdateVerticalScrollBarValue();
                break;
            case ScrollOffsetUpdateSource.VerticalScrollBar:
                UpdateHorizontalScrollBarValue();
                break;
        }
    }

    private void CloseAnchoredPopupsAfterOffset()
    {
        Popup.CloseAnchoredPopupsWithin(this);
        _diagPopupCloseCallCount++;
        _runtimePopupCloseCallCount++;
    }

    private bool CanApplyOffsetWithoutDeferredLayout()
    {
        if (NeedsMeasure)
        {
            return false;
        }

        if (!NeedsArrange)
        {
            return true;
        }

        return CanReuseTransformScrolledContentArrangeWhileViewerArrangePending();
    }

    private bool CanReuseTransformScrolledContentArrangeWhileViewerArrangePending()
    {
        if (!UsesTransformBasedContentScrolling() || ContentElement is not FrameworkElement content)
        {
            return false;
        }

        if (content.NeedsMeasure || content.NeedsArrange || _contentViewportRect.Width <= 0f || _contentViewportRect.Height <= 0f)
        {
            return false;
        }

        var arrangedWidth = ResolveContentArrangeWidth(content, _contentViewportRect);
        var arrangedHeight = ResolveContentArrangeHeight(content, _contentViewportRect);
        var arrangeRect = new LayoutRect(
            _contentViewportRect.X,
            _contentViewportRect.Y,
            arrangedWidth,
            arrangedHeight);

        return CanReuseExistingContentArrange(content, arrangeRect);
    }

    private void SyncViewerOwnedVirtualizingScrollMetrics(VirtualizingStackPanel virtualizingStackPanel)
    {
        _ = ApplyScrollMetrics(
            virtualizingStackPanel.ExtentWidth,
            virtualizingStackPanel.ExtentHeight,
            ViewportWidth,
            ViewportHeight,
            publishViewportMetrics: true);
    }

    private void BeginInputScrollMutation()
    {
        _inputScrollMutationDepth++;
    }

    private void EndInputScrollMutation()
    {
        _inputScrollMutationDepth--;
    }

    private void RecordTransformScrollDirtyHintFrame()
    {
        var uiRoot = UiRoot.Current;
        if (uiRoot == null)
        {
            return;
        }

        _lastTransformScrollDirtyHintDrawCount = uiRoot.DrawExecutedFrameCount;
    }

}
