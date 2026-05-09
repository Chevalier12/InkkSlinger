using System;
using System.Collections.Generic;
using System.Diagnostics;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class ScrollViewer
{
    protected override bool TryHandleMeasureInvalidation(UIElement origin, UIElement? source, string reason)
    {
        _ = source;
        _ = reason;

        if (ReferenceEquals(origin, this) ||
            !origin.AllowsAncestorMeasureInvalidationReconciliation ||
            _isReconcilingDescendantMeasureInvalidation ||
            NeedsMeasure ||
            ContentElement is not FrameworkElement content ||
            !IsContentDescendant(origin))
        {
            return false;
        }

        var availableSize = PreviousAvailableSizeForTests;
        if (float.IsNaN(availableSize.X) || float.IsNaN(availableSize.Y))
        {
            return false;
        }

        var margin = Margin;
        var innerAvailable = new Vector2(
            MathF.Max(0f, availableSize.X - margin.Horizontal),
            MathF.Max(0f, availableSize.Y - margin.Vertical));
        var border = MathF.Max(0f, BorderThickness);
        var horizontalBarThickness = ResolveHorizontalBarThicknessForLayout();
        var verticalBarThickness = ResolveVerticalBarThicknessForLayout();
        var contentBounds = new LayoutRect(
            LayoutSlot.X + border,
            LayoutSlot.Y + border,
            MathF.Max(0f, innerAvailable.X - (border * 2f)),
            MathF.Max(0f, innerAvailable.Y - (border * 2f)));

        _isReconcilingDescendantMeasureInvalidation = true;
        try
        {
            var decision = ResolveBarsAndMeasureContent(contentBounds);
            var canPreserveArrangeRepairPath = CanPreserveArrangeRepairPath(decision.ShowHorizontalBar, decision.ShowVerticalBar, decision.ViewportRect);
            var desiredViewportWidth = float.IsFinite(contentBounds.Width)
                ? MathF.Min(decision.ExtentWidth, decision.ViewportRect.Width)
                : decision.ExtentWidth;
            var desiredViewportHeight = float.IsFinite(contentBounds.Height)
                ? MathF.Min(decision.ExtentHeight, decision.ViewportRect.Height)
                : decision.ExtentHeight;
            var desiredWidth = desiredViewportWidth + (border * 2f) + GetVerticalBarReservation(decision.ShowVerticalBar, verticalBarThickness);
            var desiredHeight = desiredViewportHeight + (border * 2f) + GetHorizontalBarReservation(decision.ShowHorizontalBar, horizontalBarThickness);
            var measuredWidth = float.IsNaN(Width) ? desiredWidth : Width;
            var measuredHeight = float.IsNaN(Height) ? desiredHeight : Height;
            measuredWidth = Math.Clamp(measuredWidth, MinWidth, MaxWidth);
            measuredHeight = Math.Clamp(measuredHeight, MinHeight, MaxHeight);
            var reconciledDesired = new Vector2(
                MathF.Max(0f, measuredWidth + margin.Horizontal),
                MathF.Max(0f, measuredHeight + margin.Vertical));
            if (!AreFloatsClose(reconciledDesired.X, DesiredSize.X) ||
                !AreFloatsClose(reconciledDesired.Y, DesiredSize.Y))
            {
                return false;
            }

            ApplyScrollMetrics(decision.ExtentWidth, decision.ExtentHeight, decision.ViewportWidth, decision.ViewportHeight, publishViewportMetrics: false);
            _showHorizontalBar = decision.ShowHorizontalBar;
            _showVerticalBar = decision.ShowVerticalBar;
            SyncInternalScrollBarParents();
            CacheResolvedScrollBarVisibility(decision.ShowHorizontalBar, decision.ShowVerticalBar);
            UpdateDerivedScrollState();
            MarkMeasureValidAfterLocalReconciliation();

            if (canPreserveArrangeRepairPath)
            {
                _contentViewportRect = decision.ViewportRect;
                SetOffsets(HorizontalOffset, VerticalOffset);
                UpdateScrollBars();
            }
            else
            {
                InvalidateArrangeForDirectLayoutOnly();
            }

            return true;
        }
        finally
        {
            _isReconcilingDescendantMeasureInvalidation = false;
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        ExecuteQueuedScrollCommandsForLayout();
        var startTicks = Stopwatch.GetTimestamp();
        var border = MathF.Max(0f, BorderThickness);
        var horizontalBarThickness = ResolveHorizontalBarThicknessForLayout();
        var verticalBarThickness = ResolveVerticalBarThicknessForLayout();
        var contentBounds = new LayoutRect(
            LayoutSlot.X + border,
            LayoutSlot.Y + border,
            MathF.Max(0f, availableSize.X - (border * 2f)),
            MathF.Max(0f, availableSize.Y - (border * 2f)));

        var decision = ResolveBarsAndMeasureContent(contentBounds);
        ApplyScrollMetrics(decision.ExtentWidth, decision.ExtentHeight, decision.ViewportWidth, decision.ViewportHeight, publishViewportMetrics: false);
        _showHorizontalBar = decision.ShowHorizontalBar;
        _showVerticalBar = decision.ShowVerticalBar;
        SyncInternalScrollBarParents();
        CacheResolvedScrollBarVisibility(decision.ShowHorizontalBar, decision.ShowVerticalBar);
        UpdateDerivedScrollState();
        _contentViewportRect = decision.ViewportRect;
        UpdateScrollBars();

        var desiredViewportWidth = float.IsFinite(contentBounds.Width)
            ? MathF.Min(decision.ExtentWidth, decision.ViewportRect.Width)
            : decision.ExtentWidth;
        var desiredViewportHeight = float.IsFinite(contentBounds.Height)
            ? MathF.Min(decision.ExtentHeight, decision.ViewportRect.Height)
            : decision.ExtentHeight;

        var desiredWidth = desiredViewportWidth + (border * 2f) + GetVerticalBarReservation(_showVerticalBar, verticalBarThickness);
        var desiredHeight = desiredViewportHeight + (border * 2f) + GetHorizontalBarReservation(_showHorizontalBar, horizontalBarThickness);
        _diagMeasureOverrideCallCount++;
        _diagMeasureOverrideElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _runtimeMeasureOverrideCallCount++;
        _runtimeMeasureOverrideElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        return new Vector2(MathF.Max(0f, desiredWidth), MathF.Max(0f, desiredHeight));
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        ExecuteQueuedScrollCommandsForLayout();
        var startTicks = Stopwatch.GetTimestamp();
        var previousViewportRect = _contentViewportRect;
        var previousShowHorizontalBar = _showHorizontalBar;
        var previousShowVerticalBar = _showVerticalBar;
        var border = MathF.Max(0f, BorderThickness);
        var horizontalBarThickness = ResolveHorizontalBarThicknessForLayout();
        var verticalBarThickness = ResolveVerticalBarThicknessForLayout();
        SyncInternalScrollBarLayoutDimensions(horizontalBarThickness, verticalBarThickness);
        var fullRect = new LayoutRect(LayoutSlot.X + border, LayoutSlot.Y + border, MathF.Max(0f, finalSize.X - (border * 2f)), MathF.Max(0f, finalSize.Y - (border * 2f)));
        var decision = ResolveBarsForArrange(fullRect);
        var viewportMetricsChanged = ApplyScrollMetrics(decision.ExtentWidth, decision.ExtentHeight, decision.ViewportWidth, decision.ViewportHeight, publishViewportMetrics: true);
        _showHorizontalBar = decision.ShowHorizontalBar;
        _showVerticalBar = decision.ShowVerticalBar;
        SyncInternalScrollBarParents();
        CacheResolvedScrollBarVisibility(decision.ShowHorizontalBar, decision.ShowVerticalBar);
        UpdateDerivedScrollState();
        _contentViewportRect = decision.ViewportRect;
        var scrollBarVisibilityChanged =
            previousShowHorizontalBar != _showHorizontalBar ||
            previousShowVerticalBar != _showVerticalBar;
        var clipStateChanged =
            scrollBarVisibilityChanged ||
            !AreLayoutRectsClose(previousViewportRect, _contentViewportRect);
        var offsetsChanged = CoerceOffsetsToCurrentMetrics(closeAnchoredPopups: true);
        ArrangeContentForCurrentOffsets(previousViewportRect);

        if (clipStateChanged)
        {
            if (scrollBarVisibilityChanged)
            {
                InvalidateVisual();
            }
            else
            {
                UiRoot.Current?.NotifyDirectRenderInvalidation(this, RenderInvalidationKind.Clip);
            }
        }

        if (_showHorizontalBar)
        {
            _horizontalBar.Arrange(new LayoutRect(
                fullRect.X,
                fullRect.Y + fullRect.Height - horizontalBarThickness,
                MathF.Max(0f, fullRect.Width - GetVerticalBarReservation(_showVerticalBar, verticalBarThickness)),
                horizontalBarThickness));
        }
        else
        {
            _horizontalBar.Arrange(new LayoutRect(fullRect.X, fullRect.Y + fullRect.Height, 0f, 0f));
        }

        if (_showVerticalBar)
        {
            _verticalBar.Arrange(new LayoutRect(
                fullRect.X + fullRect.Width - verticalBarThickness,
                fullRect.Y,
                verticalBarThickness,
                MathF.Max(0f, fullRect.Height - GetHorizontalBarReservation(_showHorizontalBar, horizontalBarThickness))));
        }
        else
        {
            _verticalBar.Arrange(new LayoutRect(fullRect.X + fullRect.Width, fullRect.Y, 0f, 0f));
        }

        UpdateScrollBars();
        if (viewportMetricsChanged || offsetsChanged)
        {
            ViewportChanged?.Invoke(this, EventArgs.Empty);
        }

        _diagArrangeOverrideCallCount++;
        _diagArrangeOverrideElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _runtimeArrangeOverrideCallCount++;
        _runtimeArrangeOverrideElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        return finalSize;
    }

    protected override bool CanReuseMeasureForAvailableSizeChange(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
    {
        if (!CanResolveAutoBarsWithoutRemeasure())
        {
            if (TryCanReuseSingleAutoAxisMeasure(previousAvailableSize, nextAvailableSize, out var canReuse))
            {
                return canReuse;
            }

            return false;
        }

        var widthChanged = !AreFloatsClose(previousAvailableSize.X, nextAvailableSize.X);
        var heightChanged = !AreFloatsClose(previousAvailableSize.Y, nextAvailableSize.Y);
        if (!widthChanged && !heightChanged)
        {
            return true;
        }

        if (ContentElement is not FrameworkElement)
        {
            return true;
        }

        var border = MathF.Max(0f, BorderThickness);
        var previousContentWidth = MathF.Max(0f, previousAvailableSize.X - (border * 2f));
        var previousContentHeight = MathF.Max(0f, previousAvailableSize.Y - (border * 2f));
        var nextContentWidth = MathF.Max(0f, nextAvailableSize.X - (border * 2f));
        var nextContentHeight = MathF.Max(0f, nextAvailableSize.Y - (border * 2f));
        return _contentPresenter.TryReuseMeasure(
            previousContentWidth,
            previousContentHeight,
            nextContentWidth,
            nextContentHeight,
            out _,
            out _);
    }

    private bool TryCanReuseSingleAutoAxisMeasure(Vector2 previousAvailableSize, Vector2 nextAvailableSize, out bool canReuse)
    {
        canReuse = false;

        var verticalAutoWithHorizontalDisabled =
            VerticalScrollBarVisibility == ScrollBarVisibility.Auto &&
            HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled;
        var horizontalAutoWithVerticalDisabled =
            HorizontalScrollBarVisibility == ScrollBarVisibility.Auto &&
            VerticalScrollBarVisibility == ScrollBarVisibility.Disabled;

        if (!verticalAutoWithHorizontalDisabled && !horizontalAutoWithVerticalDisabled)
        {
            return false;
        }

        if (!_hasPreviousScrollBarResolution)
        {
            return true;
        }

        if (ContentElement is not FrameworkElement content)
        {
            canReuse = true;
            return true;
        }

        if (content.HasPendingMeasureInvalidationInVisualSubtreeForLayout())
        {
            canReuse = false;
            return true;
        }

        var border = MathF.Max(0f, BorderThickness);
        var previousContentWidth = MathF.Max(0f, previousAvailableSize.X - (border * 2f));
        var previousContentHeight = MathF.Max(0f, previousAvailableSize.Y - (border * 2f));
        var nextContentWidth = MathF.Max(0f, nextAvailableSize.X - (border * 2f));
        var nextContentHeight = MathF.Max(0f, nextAvailableSize.Y - (border * 2f));

        if (verticalAutoWithHorizontalDisabled)
        {
            var verticalBarThickness = ResolveVerticalBarThicknessForLayout();
            var previousChildAvailable = new Vector2(
                MathF.Max(0f, previousContentWidth - GetVerticalBarReservation(_previousShowVerticalScrollBar, verticalBarThickness)),
                float.PositiveInfinity);
            var nextShowVertical = ExtentHeight > nextContentHeight + 0.01f;
            var nextChildAvailable = new Vector2(
                MathF.Max(0f, nextContentWidth - GetVerticalBarReservation(nextShowVertical, verticalBarThickness)),
                float.PositiveInfinity);
            canReuse = content.CanReuseMeasureForAvailableSizeChangeForParentLayout(previousChildAvailable, nextChildAvailable);
            return true;
        }

        var horizontalBarThickness = ResolveHorizontalBarThicknessForLayout();
        var previousHorizontalChildAvailable = new Vector2(
            float.PositiveInfinity,
            MathF.Max(0f, previousContentHeight - GetHorizontalBarReservation(_previousShowHorizontalScrollBar, horizontalBarThickness)));
        var nextShowHorizontal = ExtentWidth > nextContentWidth + 0.01f;
        var nextHorizontalChildAvailable = new Vector2(
            float.PositiveInfinity,
            MathF.Max(0f, nextContentHeight - GetHorizontalBarReservation(nextShowHorizontal, horizontalBarThickness)));
        canReuse = content.CanReuseMeasureForAvailableSizeChangeForParentLayout(previousHorizontalChildAvailable, nextHorizontalChildAvailable);
        return true;
    }

    private (bool ShowHorizontalBar, bool ShowVerticalBar, float ExtentWidth, float ExtentHeight, float ViewportWidth, float ViewportHeight, LayoutRect ViewportRect)
        ResolveBarsAndMeasureContent(LayoutRect bounds)
    {
        var startTicks = Stopwatch.GetTimestamp();
        var useSingleMeasurePath = CanResolveAutoBarsWithoutRemeasure();
        var horizontalBarThickness = ResolveHorizontalBarThicknessForLayout();
        var verticalBarThickness = ResolveVerticalBarThicknessForLayout();
        var showHorizontal = useSingleMeasurePath
            ? ResolveInitialHorizontalScrollBarVisibility()
            : ResolveInitialHorizontalScrollBarVisibilityForRemeasure(bounds.Width);
        var showVertical = useSingleMeasurePath
            ? ResolveInitialVerticalScrollBarVisibility()
            : ResolveInitialVerticalScrollBarVisibilityForRemeasure(bounds.Height);
        var initialShowHorizontal = showHorizontal;
        var initialShowVertical = showVertical;
        var trace = $"bounds={bounds.Width:0.##}x{bounds.Height:0.##};path={(useSingleMeasurePath ? "single" : "remeasure")};initial={(showHorizontal ? 1 : 0)},{(showVertical ? 1 : 0)}";
        _diagResolveBarsAndMeasureContentCallCount++;
        _runtimeResolveBarsAndMeasureContentCallCount++;
        RecordInitialBarState(showHorizontal, showVertical);

        if (useSingleMeasurePath)
        {
            _diagResolveBarsAndMeasureContentSingleMeasurePathCount++;
            _runtimeResolveBarsAndMeasureContentSingleMeasurePathCount++;
        }
        else
        {
            _diagResolveBarsAndMeasureContentRemeasurePathCount++;
            _runtimeResolveBarsAndMeasureContentRemeasurePathCount++;
        }

        var extentWidth = 0f;
        var extentHeight = 0f;
        var hasMeasuredViewport = false;
        var previousViewportWidth = 0f;
        var previousViewportHeight = 0f;
        for (var i = 0; i < 3; i++)
        {
            _diagResolveBarsAndMeasureContentIterationCount++;
            _runtimeResolveBarsAndMeasureContentIterationCount++;
            var viewportWidth = MathF.Max(0f, bounds.Width - GetVerticalBarReservation(showVertical, verticalBarThickness));
            var viewportHeight = MathF.Max(0f, bounds.Height - GetHorizontalBarReservation(showHorizontal, horizontalBarThickness));

            MeasureOrReuseContent(
                viewportWidth,
                viewportHeight,
                hasMeasuredViewport,
                previousViewportWidth,
                previousViewportHeight,
                out extentWidth,
                out extentHeight);

            hasMeasuredViewport = true;
            previousViewportWidth = viewportWidth;
            previousViewportHeight = viewportHeight;
            var nextShowHorizontal = ResolveHorizontalAutoBarVisibility(showHorizontal, extentWidth, viewportWidth);
            var nextShowVertical = ResolveVerticalAutoBarVisibility(showVertical, extentHeight, viewportHeight);
            trace += $"|i{i}:vp={viewportWidth:0.##}x{viewportHeight:0.##},ext={extentWidth:0.##}x{extentHeight:0.##},next={(nextShowHorizontal ? 1 : 0)},{(nextShowVertical ? 1 : 0)}";

            if (nextShowHorizontal == showHorizontal && nextShowVertical == showVertical)
            {
                return FinishResolveBarsAndMeasureContent(
                    bounds,
                    startTicks,
                    trace + $"|result={(nextShowHorizontal ? 1 : 0)},{(nextShowVertical ? 1 : 0)}",
                    nextShowHorizontal,
                    nextShowVertical,
                    extentWidth,
                    extentHeight,
                    viewportWidth,
                    viewportHeight);
            }

            if (i > 0 && nextShowHorizontal == initialShowHorizontal && nextShowVertical == initialShowVertical)
            {
                var fallbackShowHorizontal = showHorizontal || nextShowHorizontal;
                var fallbackShowVertical = showVertical || nextShowVertical;
                var fallbackViewportWidth = MathF.Max(0f, bounds.Width - GetVerticalBarReservation(fallbackShowVertical, verticalBarThickness));
                var fallbackViewportHeight = MathF.Max(0f, bounds.Height - GetHorizontalBarReservation(fallbackShowHorizontal, horizontalBarThickness));
                if (!useSingleMeasurePath)
                {
                    MeasureOrReuseContent(
                        fallbackViewportWidth,
                        fallbackViewportHeight,
                        hasMeasuredViewport,
                        previousViewportWidth,
                        previousViewportHeight,
                        out extentWidth,
                        out extentHeight);
                }

                _diagResolveBarsAndMeasureContentFallbackCount++;
                _runtimeResolveBarsAndMeasureContentFallbackCount++;
                var fallbackTrace = useSingleMeasurePath
                    ? trace + $"|fallback=1,result={(fallbackShowHorizontal ? 1 : 0)},{(fallbackShowVertical ? 1 : 0)}"
                    : trace + $"|fallback=1,fallbackVp={fallbackViewportWidth:0.##}x{fallbackViewportHeight:0.##},fallbackExt={extentWidth:0.##}x{extentHeight:0.##},result={(fallbackShowHorizontal ? 1 : 0)},{(fallbackShowVertical ? 1 : 0)}";
                return FinishResolveBarsAndMeasureContent(
                    bounds,
                    startTicks,
                    fallbackTrace,
                    fallbackShowHorizontal,
                    fallbackShowVertical,
                    extentWidth,
                    extentHeight,
                    fallbackViewportWidth,
                    fallbackViewportHeight);
            }

            showHorizontal = nextShowHorizontal;
            showVertical = nextShowVertical;
        }

        var finalViewportWidth = MathF.Max(0f, bounds.Width - GetVerticalBarReservation(showVertical, verticalBarThickness));
        var finalViewportHeight = MathF.Max(0f, bounds.Height - GetHorizontalBarReservation(showHorizontal, horizontalBarThickness));
        if (!useSingleMeasurePath)
        {
            MeasureOrReuseContent(
                finalViewportWidth,
                finalViewportHeight,
                hasMeasuredViewport,
                previousViewportWidth,
                previousViewportHeight,
                out extentWidth,
                out extentHeight);
        }

        var finalTrace = useSingleMeasurePath
            ? trace + $"|result={(showHorizontal ? 1 : 0)},{(showVertical ? 1 : 0)}"
            : trace + $"|finalVp={finalViewportWidth:0.##}x{finalViewportHeight:0.##},finalExt={extentWidth:0.##}x{extentHeight:0.##},result={(showHorizontal ? 1 : 0)},{(showVertical ? 1 : 0)}";
        return FinishResolveBarsAndMeasureContent(
            bounds,
            startTicks,
            finalTrace,
            showHorizontal,
            showVertical,
            extentWidth,
            extentHeight,
            finalViewportWidth,
            finalViewportHeight);
    }

    private void MeasureOrReuseContent(
        float viewportWidth,
        float viewportHeight,
        bool hasPreviousViewport,
        float previousViewportWidth,
        float previousViewportHeight,
        out float extentWidth,
        out float extentHeight)
    {
        if (hasPreviousViewport &&
            TryReuseMeasuredContent(
                previousViewportWidth,
                previousViewportHeight,
                viewportWidth,
                viewportHeight,
                out extentWidth,
                out extentHeight))
        {
            return;
        }

        if (!TryReuseCurrentMeasuredContentForViewport(viewportWidth, viewportHeight, out extentWidth, out extentHeight))
        {
            MeasureContent(viewportWidth, viewportHeight, out extentWidth, out extentHeight);
        }
    }

    private (bool ShowHorizontalBar, bool ShowVerticalBar, float ExtentWidth, float ExtentHeight, float ViewportWidth, float ViewportHeight, LayoutRect ViewportRect)
        FinishResolveBarsAndMeasureContent(
            LayoutRect bounds,
            long startTicks,
            string trace,
            bool showHorizontal,
            bool showVertical,
            float extentWidth,
            float extentHeight,
            float viewportWidth,
            float viewportHeight)
    {
        RecordResolvedBarState(showHorizontal, showVertical);
        RecordResolveBarsMeasureTrace(trace, Stopwatch.GetTimestamp() - startTicks);
        _diagResolveBarsAndMeasureContentElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _runtimeResolveBarsAndMeasureContentElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        return (
            showHorizontal,
            showVertical,
            extentWidth,
            extentHeight,
            viewportWidth,
            viewportHeight,
            new LayoutRect(bounds.X, bounds.Y, viewportWidth, viewportHeight));
    }

    private void RecordInitialBarState(bool showHorizontal, bool showVertical)
    {
        if (showHorizontal)
        {
            _diagResolveBarsAndMeasureContentInitialHorizontalVisibleCount++;
            _runtimeResolveBarsAndMeasureContentInitialHorizontalVisibleCount++;
        }
        else
        {
            _diagResolveBarsAndMeasureContentInitialHorizontalHiddenCount++;
            _runtimeResolveBarsAndMeasureContentInitialHorizontalHiddenCount++;
        }

        if (showVertical)
        {
            _diagResolveBarsAndMeasureContentInitialVerticalVisibleCount++;
            _runtimeResolveBarsAndMeasureContentInitialVerticalVisibleCount++;
        }
        else
        {
            _diagResolveBarsAndMeasureContentInitialVerticalHiddenCount++;
            _runtimeResolveBarsAndMeasureContentInitialVerticalHiddenCount++;
        }
    }

    private void RecordResolvedBarState(bool showHorizontal, bool showVertical)
    {
        if (showHorizontal)
        {
            _diagResolveBarsAndMeasureContentResolvedHorizontalVisibleCount++;
            _runtimeResolveBarsAndMeasureContentResolvedHorizontalVisibleCount++;
        }
        else
        {
            _diagResolveBarsAndMeasureContentResolvedHorizontalHiddenCount++;
            _runtimeResolveBarsAndMeasureContentResolvedHorizontalHiddenCount++;
        }

        if (showVertical)
        {
            _diagResolveBarsAndMeasureContentResolvedVerticalVisibleCount++;
            _runtimeResolveBarsAndMeasureContentResolvedVerticalVisibleCount++;
        }
        else
        {
            _diagResolveBarsAndMeasureContentResolvedVerticalHiddenCount++;
            _runtimeResolveBarsAndMeasureContentResolvedVerticalHiddenCount++;
        }
    }

    private void RecordResolveBarsMeasureTrace(string trace, long elapsedTicks)
    {
        _runtimeResolveBarsAndMeasureContentLastTrace = trace;
        if (elapsedTicks > _runtimeResolveBarsAndMeasureContentHottestTicks)
        {
            _runtimeResolveBarsAndMeasureContentHottestTicks = elapsedTicks;
            _runtimeResolveBarsAndMeasureContentHottestTrace = trace;
        }
    }

    private (bool ShowHorizontalBar, bool ShowVerticalBar, float ExtentWidth, float ExtentHeight, float ViewportWidth, float ViewportHeight, LayoutRect ViewportRect)
        ResolveBarsForArrange(LayoutRect bounds)
    {
        var startTicks = Stopwatch.GetTimestamp();
        var horizontalBarThickness = ResolveHorizontalBarThicknessForLayout();
        var verticalBarThickness = ResolveVerticalBarThicknessForLayout();
        var extentWidth = MathF.Max(0f, ExtentWidth);
        var extentHeight = MathF.Max(0f, ExtentHeight);
        var showHorizontal = ResolveInitialHorizontalScrollBarVisibility();
        var showVertical = ResolveInitialVerticalScrollBarVisibility();
        var initialShowHorizontal = showHorizontal;
        var initialShowVertical = showVertical;
        _diagResolveBarsForArrangeCallCount++;
        _runtimeResolveBarsForArrangeCallCount++;

        for (var i = 0; i < 3; i++)
        {
            _diagResolveBarsForArrangeIterationCount++;
            _runtimeResolveBarsForArrangeIterationCount++;
            var viewportWidth = MathF.Max(0f, bounds.Width - GetVerticalBarReservation(showVertical, verticalBarThickness));
            var viewportHeight = MathF.Max(0f, bounds.Height - GetHorizontalBarReservation(showHorizontal, horizontalBarThickness));
            var nextShowHorizontal = showHorizontal;
            var nextShowVertical = showVertical;

            if (HorizontalScrollBarVisibility == ScrollBarVisibility.Auto)
            {
                nextShowHorizontal = extentWidth > viewportWidth + 0.01f;
                if (showHorizontal != nextShowHorizontal)
                {
                    _diagResolveBarsForArrangeHorizontalFlipCount++;
                    _runtimeResolveBarsForArrangeHorizontalFlipCount++;
                }
            }

            if (VerticalScrollBarVisibility == ScrollBarVisibility.Auto)
            {
                nextShowVertical = extentHeight > viewportHeight + 0.01f;
                if (showVertical != nextShowVertical)
                {
                    _diagResolveBarsForArrangeVerticalFlipCount++;
                    _runtimeResolveBarsForArrangeVerticalFlipCount++;
                }
            }

            if (nextShowHorizontal == showHorizontal && nextShowVertical == showVertical)
            {
                showHorizontal = nextShowHorizontal;
                showVertical = nextShowVertical;
                break;
            }

            if (i > 0 && nextShowHorizontal == initialShowHorizontal && nextShowVertical == initialShowVertical)
            {
                showHorizontal = showHorizontal || nextShowHorizontal;
                showVertical = showVertical || nextShowVertical;
                break;
            }

            showHorizontal = nextShowHorizontal;
            showVertical = nextShowVertical;
        }

        var finalViewportWidth = MathF.Max(0f, bounds.Width - GetVerticalBarReservation(showVertical, verticalBarThickness));
        var finalViewportHeight = MathF.Max(0f, bounds.Height - GetHorizontalBarReservation(showHorizontal, horizontalBarThickness));
        _diagResolveBarsForArrangeElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _runtimeResolveBarsForArrangeElapsedTicks += Stopwatch.GetTimestamp() - startTicks;

        return (
            showHorizontal,
            showVertical,
            extentWidth,
            extentHeight,
            finalViewportWidth,
            finalViewportHeight,
            new LayoutRect(bounds.X, bounds.Y, finalViewportWidth, finalViewportHeight));
    }

    private void MeasureContent(float viewportWidth, float viewportHeight, out float extentWidth, out float extentHeight)
    {
        _contentPresenter.MeasureContent(viewportWidth, viewportHeight, out extentWidth, out extentHeight);
    }

    private bool TryReuseCurrentMeasuredContentForViewport(float viewportWidth, float viewportHeight, out float extentWidth, out float extentHeight)
    {
        return _contentPresenter.TryReuseCurrentMeasureForViewport(viewportWidth, viewportHeight, out extentWidth, out extentHeight);
    }

    private bool TryReuseMeasuredContent(
        float previousViewportWidth,
        float previousViewportHeight,
        float nextViewportWidth,
        float nextViewportHeight,
        out float extentWidth,
        out float extentHeight)
    {
        return _contentPresenter.TryReuseMeasure(
            previousViewportWidth,
            previousViewportHeight,
            nextViewportWidth,
            nextViewportHeight,
            out extentWidth,
            out extentHeight);
    }

    private bool IsContentDescendant(UIElement element)
    {
        for (UIElement? current = element; current != null; current = current.GetInvalidationParent())
        {
            if (ReferenceEquals(current, ContentElement))
            {
                return true;
            }

            if (ReferenceEquals(current, this))
            {
                break;
            }
        }

        return false;
    }

    private bool CanResolveAutoBarsWithoutRemeasure()
    {
        var horizontalCanUseSingleMeasure = HorizontalScrollBarVisibility != ScrollBarVisibility.Auto ||
                                            VerticalScrollBarVisibility != ScrollBarVisibility.Disabled;
        var verticalCanUseSingleMeasure = VerticalScrollBarVisibility != ScrollBarVisibility.Auto ||
                                          HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled;
        return horizontalCanUseSingleMeasure && verticalCanUseSingleMeasure;
    }

    internal static bool AreFloatsClose(float first, float second)
    {
        if (float.IsNaN(first) || float.IsNaN(second))
        {
            return false;
        }

        if (float.IsInfinity(first) || float.IsInfinity(second))
        {
            return float.IsPositiveInfinity(first) == float.IsPositiveInfinity(second) &&
                   float.IsNegativeInfinity(first) == float.IsNegativeInfinity(second);
        }

        return MathF.Abs(first - second) < 0.01f;
    }

    internal static LayoutRect OffsetCachedRect(LayoutRect rect, Vector2 delta)
    {
        return new LayoutRect(rect.X + delta.X, rect.Y + delta.Y, rect.Width, rect.Height);
    }

    private bool ResolveHorizontalAutoBarVisibility(bool currentShowHorizontal, float extentWidth, float viewportWidth)
    {
        if (HorizontalScrollBarVisibility != ScrollBarVisibility.Auto)
        {
            return currentShowHorizontal;
        }

        var nextShowHorizontal = extentWidth > viewportWidth + 0.01f;
        if (currentShowHorizontal != nextShowHorizontal)
        {
            _diagResolveBarsAndMeasureContentHorizontalFlipCount++;
            _runtimeResolveBarsAndMeasureContentHorizontalFlipCount++;
        }

        return nextShowHorizontal;
    }

    private bool ResolveVerticalAutoBarVisibility(bool currentShowVertical, float extentHeight, float viewportHeight)
    {
        if (VerticalScrollBarVisibility != ScrollBarVisibility.Auto)
        {
            return currentShowVertical;
        }

        var nextShowVertical = extentHeight > viewportHeight + 0.01f;
        if (currentShowVertical != nextShowVertical)
        {
            _diagResolveBarsAndMeasureContentVerticalFlipCount++;
            _runtimeResolveBarsAndMeasureContentVerticalFlipCount++;
        }

        return nextShowVertical;
    }

    private void CacheResolvedScrollBarVisibility(bool showHorizontalBar, bool showVerticalBar)
    {
        _hasPreviousScrollBarResolution = true;
        _previousShowHorizontalScrollBar = showHorizontalBar;
        _previousShowVerticalScrollBar = showVerticalBar;
    }

    private bool ResolveInitialHorizontalScrollBarVisibility()
    {
        return HorizontalScrollBarVisibility switch
        {
            ScrollBarVisibility.Visible => true,
            ScrollBarVisibility.Disabled => false,
            ScrollBarVisibility.Hidden => false,
            ScrollBarVisibility.Auto => _hasPreviousScrollBarResolution && _previousShowHorizontalScrollBar,
            _ => false
        };
    }


    private bool CanPreserveArrangeRepairPath(bool showHorizontalBar, bool showVerticalBar, LayoutRect viewportRect)
    {
        return NeedsArrange &&
               UsesTransformBasedContentScrolling() &&
               showHorizontalBar == _showHorizontalBar &&
               showVerticalBar == _showVerticalBar &&
               AreLayoutRectsClose(viewportRect, _contentViewportRect);
    }

    internal static bool AreLayoutRectsClose(LayoutRect left, LayoutRect right)
    {
        return AreFloatsClose(left.X, right.X) &&
               AreFloatsClose(left.Y, right.Y) &&
               AreFloatsClose(left.Width, right.Width) &&
               AreFloatsClose(left.Height, right.Height);
    }
    private bool ResolveInitialVerticalScrollBarVisibility()
    {
        return VerticalScrollBarVisibility switch
        {
            ScrollBarVisibility.Visible => true,
            ScrollBarVisibility.Disabled => false,
            ScrollBarVisibility.Hidden => false,
            ScrollBarVisibility.Auto => _hasPreviousScrollBarResolution && _previousShowVerticalScrollBar,
            _ => false
        };
    }

    private bool ResolveInitialHorizontalScrollBarVisibilityForRemeasure(float availableWidth)
    {
        return HorizontalScrollBarVisibility switch
        {
            ScrollBarVisibility.Visible => true,
            ScrollBarVisibility.Disabled => false,
            ScrollBarVisibility.Hidden => false,
            ScrollBarVisibility.Auto => _hasPreviousScrollBarResolution && ExtentWidth > availableWidth + 0.01f,
            _ => false
        };
    }

    private bool ResolveInitialVerticalScrollBarVisibilityForRemeasure(float availableHeight)
    {
        return VerticalScrollBarVisibility switch
        {
            ScrollBarVisibility.Visible => true,
            ScrollBarVisibility.Disabled => false,
            ScrollBarVisibility.Hidden => false,
            ScrollBarVisibility.Auto => _hasPreviousScrollBarResolution && ExtentHeight > availableHeight + 0.01f,
            _ => false
        };
    }

    private bool ApplyScrollMetrics(
        float extentWidth,
        float extentHeight,
        float viewportWidth,
        float viewportHeight,
        bool publishViewportMetrics)
    {
        var previousExtentWidth = ExtentWidth;
        var previousExtentHeight = ExtentHeight;
        var previousViewportWidth = ViewportWidth;
        var previousViewportHeight = ViewportHeight;
        var previousScrollableWidth = ScrollableWidth;
        var previousScrollableHeight = ScrollableHeight;

        SetIfChanged(ExtentWidthProperty, CoerceNonNegativeFinite(extentWidth, previousExtentWidth), "scrollviewer_extent_width_write_count");
        SetIfChanged(ExtentHeightProperty, CoerceNonNegativeFinite(extentHeight, previousExtentHeight), "scrollviewer_extent_height_write_count");
        if (!publishViewportMetrics)
        {
            return false;
        }

        SetIfChanged(ViewportWidthProperty, CoerceViewportMetric(viewportWidth, previousViewportWidth, ExtentWidth), "scrollviewer_viewport_width_write_count");
        SetIfChanged(ViewportHeightProperty, CoerceViewportMetric(viewportHeight, previousViewportHeight, ExtentHeight), "scrollviewer_viewport_height_write_count");
        UpdateDerivedScrollState();
        RaiseScrollChangedIfNeeded(
            previousHorizontalOffset: HorizontalOffset,
            previousVerticalOffset: VerticalOffset,
            previousExtentWidth,
            previousExtentHeight,
            previousViewportWidth,
            previousViewportHeight,
            previousScrollableWidth,
            previousScrollableHeight);

        return MathF.Abs(previousExtentWidth - ExtentWidth) > 0.01f ||
               MathF.Abs(previousExtentHeight - ExtentHeight) > 0.01f ||
               MathF.Abs(previousViewportWidth - ViewportWidth) > 0.01f ||
               MathF.Abs(previousViewportHeight - ViewportHeight) > 0.01f;
    }

}
