using System;
using System.Diagnostics;
using System.Threading;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public enum TrackPartRole
{
    None,
    DecreaseButton,
    Thumb,
    IncreaseButton
}

public class Track : Panel, IRenderDirtyBoundsHintProvider
{
    private const float DefaultThumbMinLength = 14f;
    private const float MinimumThumbRatio = 0.05f;
    private const float FallbackThumbRatio = 0.1f;
    private const float ValueEpsilon = 0.01f;

    public static readonly DependencyProperty PartRoleProperty =
        DependencyProperty.RegisterAttached(
            "PartRole",
            typeof(TrackPartRole),
            typeof(Track),
            new FrameworkPropertyMetadata(
                TrackPartRole.None,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is not UIElement element)
                    {
                        return;
                    }

                    if (element.VisualParent is Track visualTrack)
                    {
                        visualTrack.InvalidateMeasure();
                        visualTrack.InvalidateArrange();
                        visualTrack.InvalidateVisual();
                    }
                    else if (element.LogicalParent is Track logicalTrack)
                    {
                        logicalTrack.InvalidateMeasure();
                        logicalTrack.InvalidateArrange();
                        logicalTrack.InvalidateVisual();
                    }
                }));

    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(Track),
            new FrameworkPropertyMetadata(
                Orientation.Vertical,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(
            nameof(Minimum),
            typeof(float),
            typeof(Track),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.None,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Track track)
                    {
                        track.OnStateMutationChanged(TrackStateMutationSource.Minimum);
                    }
                }));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            nameof(Maximum),
            typeof(float),
            typeof(Track),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.None,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Track track)
                    {
                        track.OnStateMutationChanged(TrackStateMutationSource.Maximum);
                    }
                }));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(float),
            typeof(Track),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.None,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Track track)
                    {
                        track.OnStateMutationChanged(TrackStateMutationSource.Value);
                    }
                }));

    public static readonly DependencyProperty ViewportSizeProperty =
        DependencyProperty.Register(
            nameof(ViewportSize),
            typeof(float),
            typeof(Track),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.None,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Track track)
                    {
                        track.OnStateMutationChanged(TrackStateMutationSource.ViewportSize);
                    }
                }));

    public static readonly DependencyProperty IsViewportSizedThumbProperty =
        DependencyProperty.Register(
            nameof(IsViewportSizedThumb),
            typeof(bool),
            typeof(Track),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowLineButtonsProperty =
        DependencyProperty.Register(
            nameof(ShowLineButtons),
            typeof(bool),
            typeof(Track),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsDirectionReversedProperty =
        DependencyProperty.Register(
            nameof(IsDirectionReversed),
            typeof(bool),
            typeof(Track),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.None,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Track track)
                    {
                        track.OnStateMutationChanged(TrackStateMutationSource.IsDirectionReversed);
                    }
                }));

    public static readonly DependencyProperty ThumbLengthProperty =
        DependencyProperty.Register(
            nameof(ThumbLength),
            typeof(float),
            typeof(Track),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float length && length >= 0f ? length : 0f));

    public static readonly DependencyProperty ThumbMinLengthProperty =
        DependencyProperty.Register(
            nameof(ThumbMinLength),
            typeof(float),
            typeof(Track),
            new FrameworkPropertyMetadata(
                DefaultThumbMinLength,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float length && length >= 6f ? length : DefaultThumbMinLength));

    public static readonly DependencyProperty TrackThicknessProperty =
        DependencyProperty.Register(
            nameof(TrackThickness),
            typeof(float),
            typeof(Track),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float thickness && thickness >= 0f ? thickness : 0f));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(Track),
            new FrameworkPropertyMetadata(Color.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(Thickness),
            typeof(Track),
            new FrameworkPropertyMetadata(Thickness.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    private LayoutRect _trackRect;
    private LayoutRect _thumbRect;
    private LayoutRect _decreaseRegionRect;
    private LayoutRect _increaseRegionRect;
    private bool _hasPendingRenderDirtyBoundsHint;
    private bool _preserveRenderDirtyBoundsHint;
    private LayoutRect _pendingRenderDirtyBoundsHint;
    private long _runtimeGetThumbRectCallCount;
    private long _runtimeGetTrackRectCallCount;
    private long _runtimeGetThumbTravelCallCount;
    private long _runtimeGetValueFromThumbTravelCallCount;
    private long _runtimeGetValueFromThumbTravelElapsedTicks;
    private long _runtimeGetValueFromThumbTravelScrollableRangeZeroCount;
    private long _runtimeGetValueFromThumbTravelMaxTravelZeroCount;
    private long _runtimeGetValueFromThumbTravelDirectionReversedCount;
    private long _runtimeGetValueFromThumbTravelClampedCount;
    private long _runtimeGetValueFromPointCallCount;
    private long _runtimeGetValueFromPointElapsedTicks;
    private long _runtimeGetValuePositionCallCount;
    private long _runtimeGetValuePositionElapsedTicks;
    private long _runtimeHitTestDecreaseRegionCallCount;
    private long _runtimeHitTestDecreaseRegionHitCount;
    private long _runtimeHitTestIncreaseRegionCallCount;
    private long _runtimeHitTestIncreaseRegionHitCount;
    private long _runtimeInvalidateVisualCallCount;
    private long _runtimeInvalidateVisualClearedPendingHintCount;
    private long _runtimeTryConsumeRenderDirtyBoundsHintCallCount;
    private long _runtimeTryConsumeRenderDirtyBoundsHintHitCount;
    private long _runtimeMeasureOverrideCallCount;
    private long _runtimeMeasureOverrideElapsedTicks;
    private long _runtimeArrangeOverrideCallCount;
    private long _runtimeArrangeOverrideElapsedTicks;
    private long _runtimeArrangeVerticalCallCount;
    private long _runtimeArrangeVerticalElapsedTicks;
    private long _runtimeArrangeHorizontalCallCount;
    private long _runtimeArrangeHorizontalElapsedTicks;
    private long _runtimeRenderCallCount;
    private long _runtimeRenderElapsedTicks;
    private long _runtimeDrawBorderCallCount;
    private long _runtimeDrawBorderElapsedTicks;
    private long _runtimeResolvePartsCallCount;
    private long _runtimeResolvePartsElapsedTicks;
    private long _runtimeResolvePartsDuplicateRoleCount;
    private long _runtimeComputeThumbRectCallCount;
    private long _runtimeComputeThumbRectElapsedTicks;
    private long _runtimeComputeSliderThumbRectCallCount;
    private long _runtimeComputeSliderThumbRectElapsedTicks;
    private long _runtimeResolveThumbAxisLengthCallCount;
    private long _runtimeResolveThumbAxisLengthElapsedTicks;
    private long _runtimeGetScrollableRangeCallCount;
    private long _runtimeClampValueCallCount;
    private long _runtimeOnStateMutationChangedCallCount;
    private long _runtimeRefreshLayoutForStateMutationCallCount;
    private long _runtimeRefreshLayoutForStateMutationElapsedTicks;
    private long _runtimeRefreshLayoutNeedsMeasureFallbackCount;
    private long _runtimeRefreshLayoutDirtyBoundsHintCount;
    private long _runtimeRefreshLayoutVisualFallbackCount;
    private long _runtimeInvalidateVisualWithDirtyBoundsHintCallCount;
    private long _runtimeInvalidateVisualWithDirtyBoundsHintElapsedTicks;
    private long _runtimeCaptureRenderMutationSnapshotCallCount;
    private long _runtimeCaptureRenderMutationSnapshotElapsedTicks;
    private long _runtimeTryBuildRenderMutationDirtyBoundsCallCount;
    private long _runtimeTryBuildRenderMutationDirtyBoundsBuiltCount;
    private long _runtimeTryBuildRenderMutationDirtyBoundsTrackChangedCount;
    private long _runtimeTryBuildRenderMutationDirtyBoundsPartChangedCount;
    private static long _diagGetThumbRectCallCount;
    private static long _diagGetTrackRectCallCount;
    private static long _diagGetThumbTravelCallCount;
    private static long _diagGetValueFromThumbTravelCallCount;
    private static long _diagGetValueFromThumbTravelElapsedTicks;
    private static long _diagGetValueFromThumbTravelScrollableRangeZeroCount;
    private static long _diagGetValueFromThumbTravelMaxTravelZeroCount;
    private static long _diagGetValueFromThumbTravelDirectionReversedCount;
    private static long _diagGetValueFromThumbTravelClampedCount;
    private static long _diagGetValueFromPointCallCount;
    private static long _diagGetValueFromPointElapsedTicks;
    private static long _diagGetValueFromPointThumbCenterOffsetCount;
    private static long _diagGetValueFromPointScrollableRangeZeroCount;
    private static long _diagGetValueFromPointMaxTravelZeroCount;
    private static long _diagGetValuePositionCallCount;
    private static long _diagGetValuePositionElapsedTicks;
    private static long _diagGetValuePositionZeroRangeFallbackCount;
    private static long _diagGetValuePositionDirectionReversedCount;
    private static long _diagHitTestDecreaseRegionCallCount;
    private static long _diagHitTestDecreaseRegionHitCount;
    private static long _diagHitTestIncreaseRegionCallCount;
    private static long _diagHitTestIncreaseRegionHitCount;
    private static long _diagInvalidateVisualCallCount;
    private static long _diagInvalidateVisualClearedPendingHintCount;
    private static long _diagTryConsumeRenderDirtyBoundsHintCallCount;
    private static long _diagTryConsumeRenderDirtyBoundsHintHitCount;
    private static long _diagMeasureOverrideCallCount;
    private static long _diagMeasureOverrideElapsedTicks;
    private static long _diagMeasureOverrideVerticalCount;
    private static long _diagMeasureOverrideHorizontalCount;
    private static long _diagMeasureOverrideViewportSizedThumbCount;
    private static long _diagMeasureOverrideSliderThumbCount;
    private static long _diagMeasureOverrideResolvedDecreasePartCount;
    private static long _diagMeasureOverrideResolvedThumbPartCount;
    private static long _diagMeasureOverrideResolvedIncreasePartCount;
    private static long _diagArrangeOverrideCallCount;
    private static long _diagArrangeOverrideElapsedTicks;
    private static long _diagArrangeOverrideVerticalCount;
    private static long _diagArrangeOverrideHorizontalCount;
    private static long _diagArrangeOverrideExtraChildArrangeCount;
    private static long _diagArrangeOverrideSkippedNonFrameworkChildCount;
    private static long _diagArrangeVerticalCallCount;
    private static long _diagArrangeVerticalElapsedTicks;
    private static long _diagArrangeVerticalViewportSizedThumbCount;
    private static long _diagArrangeVerticalSliderThumbCount;
    private static long _diagArrangeHorizontalCallCount;
    private static long _diagArrangeHorizontalElapsedTicks;
    private static long _diagArrangeHorizontalViewportSizedThumbCount;
    private static long _diagArrangeHorizontalSliderThumbCount;
    private static long _diagRenderCallCount;
    private static long _diagRenderElapsedTicks;
    private static long _diagRenderSkippedEmptyTrackCount;
    private static long _diagDrawBorderCallCount;
    private static long _diagDrawBorderElapsedTicks;
    private static long _diagDrawBorderLeftSegmentCount;
    private static long _diagDrawBorderRightSegmentCount;
    private static long _diagDrawBorderTopSegmentCount;
    private static long _diagDrawBorderBottomSegmentCount;
    private static long _diagResolvePartsCallCount;
    private static long _diagResolvePartsElapsedTicks;
    private static long _diagResolvePartsScannedChildCount;
    private static long _diagResolvePartsNonFrameworkChildCount;
    private static long _diagResolvePartsDuplicateRoleCount;
    private static long _diagResolvePartsDecreaseFoundCount;
    private static long _diagResolvePartsThumbFoundCount;
    private static long _diagResolvePartsIncreaseFoundCount;
    private static long _diagComputeThumbRectCallCount;
    private static long _diagComputeThumbRectElapsedTicks;
    private static long _diagComputeThumbRectZeroTrackRectCount;
    private static long _diagComputeThumbRectDirectionReversedCount;
    private static long _diagComputeSliderThumbRectCallCount;
    private static long _diagComputeSliderThumbRectElapsedTicks;
    private static long _diagComputeSliderThumbRectZeroTrackRectCount;
    private static long _diagComputeSliderThumbRectDirectionReversedCount;
    private static long _diagResolveThumbAxisLengthCallCount;
    private static long _diagResolveThumbAxisLengthElapsedTicks;
    private static long _diagResolveThumbAxisLengthViewportSizedCount;
    private static long _diagResolveThumbAxisLengthExplicitCount;
    private static long _diagResolveThumbAxisLengthExtentZeroCount;
    private static long _diagResolveThumbAxisLengthFallbackViewportRatioCount;
    private static long _diagResolveThumbAxisLengthViewportRatioCount;
    private static long _diagGetScrollableRangeCallCount;
    private static long _diagGetScrollableRangeViewportSizedCount;
    private static long _diagClampValueCallCount;
    private static long _diagClampValueClampedLowCount;
    private static long _diagClampValueClampedHighCount;
    private static long _diagOnStateMutationChangedCallCount;
    private static long _diagRefreshLayoutForStateMutationCallCount;
    private static long _diagRefreshLayoutForStateMutationElapsedTicks;
    private static long _diagRefreshLayoutValueMutationCallCount;
    private static long _diagRefreshLayoutValueMutationElapsedTicks;
    private static long _diagRefreshLayoutViewportMutationCallCount;
    private static long _diagRefreshLayoutViewportMutationElapsedTicks;
    private static long _diagRefreshLayoutMinimumMutationCallCount;
    private static long _diagRefreshLayoutMinimumMutationElapsedTicks;
    private static long _diagRefreshLayoutMaximumMutationCallCount;
    private static long _diagRefreshLayoutMaximumMutationElapsedTicks;
    private static long _diagRefreshLayoutDirectionMutationCallCount;
    private static long _diagRefreshLayoutDirectionMutationElapsedTicks;
    private static long _diagRefreshLayoutNeedsMeasureFallbackCount;
    private static long _diagRefreshLayoutNeedsMeasureFallbackElapsedTicks;
    private static long _diagRefreshLayoutCaptureSnapshotElapsedTicks;
    private static long _diagRefreshLayoutInvalidateArrangeElapsedTicks;
    private static long _diagRefreshLayoutArrangeElapsedTicks;
    private static long _diagRefreshLayoutDirtyBoundsElapsedTicks;
    private static long _diagRefreshLayoutDirtyBoundsHintCount;
    private static long _diagRefreshLayoutVisualFallbackCount;
    private static long _diagRefreshLayoutVisualInvalidationElapsedTicks;
    private static long _diagInvalidateVisualWithDirtyBoundsHintCallCount;
    private static long _diagInvalidateVisualWithDirtyBoundsHintElapsedTicks;
    private static long _diagCaptureRenderMutationSnapshotCallCount;
    private static long _diagCaptureRenderMutationSnapshotElapsedTicks;
    private static long _diagTryBuildRenderMutationDirtyBoundsCallCount;
    private static long _diagTryBuildRenderMutationDirtyBoundsBuiltCount;
    private static long _diagTryBuildRenderMutationDirtyBoundsTrackChangedCount;
    private static long _diagTryBuildRenderMutationDirtyBoundsPartChangedCount;

    private enum TrackStateMutationSource
    {
        Minimum,
        Maximum,
        Value,
        ViewportSize,
        IsDirectionReversed
    }

    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public float Minimum
    {
        get => GetValue<float>(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public float Maximum
    {
        get => GetValue<float>(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public float Value
    {
        get => GetValue<float>(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public float ViewportSize
    {
        get => GetValue<float>(ViewportSizeProperty);
        set => SetValue(ViewportSizeProperty, value);
    }

    public bool IsViewportSizedThumb
    {
        get => GetValue<bool>(IsViewportSizedThumbProperty);
        set => SetValue(IsViewportSizedThumbProperty, value);
    }

    public bool ShowLineButtons
    {
        get => GetValue<bool>(ShowLineButtonsProperty);
        set => SetValue(ShowLineButtonsProperty, value);
    }

    public bool IsDirectionReversed
    {
        get => GetValue<bool>(IsDirectionReversedProperty);
        set => SetValue(IsDirectionReversedProperty, value);
    }

    public float ThumbLength
    {
        get => GetValue<float>(ThumbLengthProperty);
        set => SetValue(ThumbLengthProperty, value);
    }

    public float ThumbMinLength
    {
        get => GetValue<float>(ThumbMinLengthProperty);
        set => SetValue(ThumbMinLengthProperty, value);
    }

    public float TrackThickness
    {
        get => GetValue<float>(TrackThicknessProperty);
        set => SetValue(TrackThicknessProperty, value);
    }

    public Color BorderBrush
    {
        get => GetValue<Color>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public Thickness BorderThickness
    {
        get => GetValue<Thickness>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public static TrackPartRole GetPartRole(UIElement element)
    {
        return element.GetValue<TrackPartRole>(PartRoleProperty);
    }

    public static void SetPartRole(UIElement element, TrackPartRole role)
    {
        element.SetValue(PartRoleProperty, role);
    }

    internal LayoutRect GetThumbRect()
    {
        _runtimeGetThumbRectCallCount++;
        IncrementAggregate(ref _diagGetThumbRectCallCount);
        return _thumbRect;
    }

    internal LayoutRect GetTrackRect()
    {
        _runtimeGetTrackRectCallCount++;
        IncrementAggregate(ref _diagGetTrackRectCallCount);
        return _trackRect;
    }

    internal float GetThumbTravel()
    {
        _runtimeGetThumbTravelCallCount++;
        IncrementAggregate(ref _diagGetThumbTravelCallCount);
        return Orientation == Orientation.Vertical
            ? _thumbRect.Y - _trackRect.Y
            : _thumbRect.X - _trackRect.X;
    }

    internal float GetValueFromThumbTravel(float thumbTravel)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeGetValueFromThumbTravelCallCount++;
        var scrollableRange = GetScrollableRange();
        if (scrollableRange <= ValueEpsilon)
        {
            _runtimeGetValueFromThumbTravelScrollableRangeZeroCount++;
            IncrementAggregate(ref _diagGetValueFromThumbTravelScrollableRangeZeroCount);
            _runtimeGetValueFromThumbTravelElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            RecordAggregateElapsed(ref _diagGetValueFromThumbTravelCallCount, ref _diagGetValueFromThumbTravelElapsedTicks, startTicks);
            return Minimum;
        }

        var trackLength = GetAxisLength(_trackRect);
        var thumbLength = GetAxisLength(_thumbRect);
        var maxTravel = MathF.Max(0f, trackLength - thumbLength);
        if (maxTravel <= ValueEpsilon)
        {
            _runtimeGetValueFromThumbTravelMaxTravelZeroCount++;
            IncrementAggregate(ref _diagGetValueFromThumbTravelMaxTravelZeroCount);
            _runtimeGetValueFromThumbTravelElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            RecordAggregateElapsed(ref _diagGetValueFromThumbTravelCallCount, ref _diagGetValueFromThumbTravelElapsedTicks, startTicks);
            return Minimum;
        }

        var clampedTravel = MathF.Max(0f, MathF.Min(maxTravel, thumbTravel));
        if (MathF.Abs(clampedTravel - thumbTravel) > ValueEpsilon)
        {
            _runtimeGetValueFromThumbTravelClampedCount++;
            IncrementAggregate(ref _diagGetValueFromThumbTravelClampedCount);
        }

        var normalized = clampedTravel / maxTravel;
        if (IsDirectionReversed)
        {
            _runtimeGetValueFromThumbTravelDirectionReversedCount++;
            IncrementAggregate(ref _diagGetValueFromThumbTravelDirectionReversedCount);
            normalized = 1f - normalized;
        }

        var value = ClampValue(Minimum + (normalized * scrollableRange));
        _runtimeGetValueFromThumbTravelElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        RecordAggregateElapsed(ref _diagGetValueFromThumbTravelCallCount, ref _diagGetValueFromThumbTravelElapsedTicks, startTicks);
        return value;
    }

    internal static TrackThumbTravelTelemetrySnapshot GetThumbTravelTelemetryAndReset()
    {
        var snapshot = new TrackThumbTravelTelemetrySnapshot(
            _diagGetValueFromThumbTravelCallCount,
            (double)_diagGetValueFromThumbTravelElapsedTicks * 1000d / Stopwatch.Frequency,
            _diagRefreshLayoutForStateMutationCallCount,
            (double)_diagRefreshLayoutForStateMutationElapsedTicks * 1000d / Stopwatch.Frequency,
            _diagRefreshLayoutValueMutationCallCount,
            (double)_diagRefreshLayoutValueMutationElapsedTicks * 1000d / Stopwatch.Frequency,
            _diagRefreshLayoutViewportMutationCallCount,
            (double)_diagRefreshLayoutViewportMutationElapsedTicks * 1000d / Stopwatch.Frequency,
            _diagRefreshLayoutMinimumMutationCallCount,
            (double)_diagRefreshLayoutMinimumMutationElapsedTicks * 1000d / Stopwatch.Frequency,
            _diagRefreshLayoutMaximumMutationCallCount,
            (double)_diagRefreshLayoutMaximumMutationElapsedTicks * 1000d / Stopwatch.Frequency,
            _diagRefreshLayoutDirectionMutationCallCount,
            (double)_diagRefreshLayoutDirectionMutationElapsedTicks * 1000d / Stopwatch.Frequency,
            _diagRefreshLayoutNeedsMeasureFallbackCount,
            (double)_diagRefreshLayoutNeedsMeasureFallbackElapsedTicks * 1000d / Stopwatch.Frequency,
            (double)_diagRefreshLayoutCaptureSnapshotElapsedTicks * 1000d / Stopwatch.Frequency,
            (double)_diagRefreshLayoutInvalidateArrangeElapsedTicks * 1000d / Stopwatch.Frequency,
            (double)_diagRefreshLayoutArrangeElapsedTicks * 1000d / Stopwatch.Frequency,
            (double)_diagRefreshLayoutDirtyBoundsElapsedTicks * 1000d / Stopwatch.Frequency,
            _diagRefreshLayoutDirtyBoundsHintCount,
            _diagRefreshLayoutVisualFallbackCount,
            (double)_diagRefreshLayoutVisualInvalidationElapsedTicks * 1000d / Stopwatch.Frequency);
        _diagGetValueFromThumbTravelCallCount = 0;
        _diagGetValueFromThumbTravelElapsedTicks = 0L;
        _diagRefreshLayoutForStateMutationCallCount = 0;
        _diagRefreshLayoutForStateMutationElapsedTicks = 0L;
        _diagRefreshLayoutValueMutationCallCount = 0;
        _diagRefreshLayoutValueMutationElapsedTicks = 0L;
        _diagRefreshLayoutViewportMutationCallCount = 0;
        _diagRefreshLayoutViewportMutationElapsedTicks = 0L;
        _diagRefreshLayoutMinimumMutationCallCount = 0;
        _diagRefreshLayoutMinimumMutationElapsedTicks = 0L;
        _diagRefreshLayoutMaximumMutationCallCount = 0;
        _diagRefreshLayoutMaximumMutationElapsedTicks = 0L;
        _diagRefreshLayoutDirectionMutationCallCount = 0;
        _diagRefreshLayoutDirectionMutationElapsedTicks = 0L;
        _diagRefreshLayoutNeedsMeasureFallbackCount = 0;
        _diagRefreshLayoutNeedsMeasureFallbackElapsedTicks = 0L;
        _diagRefreshLayoutCaptureSnapshotElapsedTicks = 0L;
        _diagRefreshLayoutInvalidateArrangeElapsedTicks = 0L;
        _diagRefreshLayoutArrangeElapsedTicks = 0L;
        _diagRefreshLayoutDirtyBoundsElapsedTicks = 0L;
        _diagRefreshLayoutDirtyBoundsHintCount = 0;
        _diagRefreshLayoutVisualFallbackCount = 0;
        _diagRefreshLayoutVisualInvalidationElapsedTicks = 0L;
        return snapshot;
    }

    internal TrackRuntimeDiagnosticsSnapshot GetTrackSnapshotForDiagnostics()
    {
        return new TrackRuntimeDiagnosticsSnapshot(
            Orientation,
            IsViewportSizedThumb,
            IsDirectionReversed,
            Minimum,
            Maximum,
            Value,
            ViewportSize,
            ThumbLength,
            ThumbMinLength,
            TrackThickness,
            Children.Count,
            _hasPendingRenderDirtyBoundsHint,
            _preserveRenderDirtyBoundsHint,
            _trackRect.X,
            _trackRect.Y,
            _trackRect.Width,
            _trackRect.Height,
            _thumbRect.X,
            _thumbRect.Y,
            _thumbRect.Width,
            _thumbRect.Height,
            _decreaseRegionRect.X,
            _decreaseRegionRect.Y,
            _decreaseRegionRect.Width,
            _decreaseRegionRect.Height,
            _increaseRegionRect.X,
            _increaseRegionRect.Y,
            _increaseRegionRect.Width,
            _increaseRegionRect.Height,
            _runtimeGetThumbRectCallCount,
            _runtimeGetTrackRectCallCount,
            _runtimeGetThumbTravelCallCount,
            _runtimeGetValueFromThumbTravelCallCount,
            TicksToMilliseconds(_runtimeGetValueFromThumbTravelElapsedTicks),
            _runtimeGetValueFromThumbTravelScrollableRangeZeroCount,
            _runtimeGetValueFromThumbTravelMaxTravelZeroCount,
            _runtimeGetValueFromThumbTravelDirectionReversedCount,
            _runtimeGetValueFromThumbTravelClampedCount,
            _runtimeGetValueFromPointCallCount,
            TicksToMilliseconds(_runtimeGetValueFromPointElapsedTicks),
            _runtimeGetValuePositionCallCount,
            TicksToMilliseconds(_runtimeGetValuePositionElapsedTicks),
            _runtimeHitTestDecreaseRegionCallCount,
            _runtimeHitTestDecreaseRegionHitCount,
            _runtimeHitTestIncreaseRegionCallCount,
            _runtimeHitTestIncreaseRegionHitCount,
            _runtimeInvalidateVisualCallCount,
            _runtimeInvalidateVisualClearedPendingHintCount,
            _runtimeTryConsumeRenderDirtyBoundsHintCallCount,
            _runtimeTryConsumeRenderDirtyBoundsHintHitCount,
            _runtimeMeasureOverrideCallCount,
            TicksToMilliseconds(_runtimeMeasureOverrideElapsedTicks),
            _runtimeArrangeOverrideCallCount,
            TicksToMilliseconds(_runtimeArrangeOverrideElapsedTicks),
            _runtimeArrangeVerticalCallCount,
            TicksToMilliseconds(_runtimeArrangeVerticalElapsedTicks),
            _runtimeArrangeHorizontalCallCount,
            TicksToMilliseconds(_runtimeArrangeHorizontalElapsedTicks),
            _runtimeRenderCallCount,
            TicksToMilliseconds(_runtimeRenderElapsedTicks),
            _runtimeDrawBorderCallCount,
            TicksToMilliseconds(_runtimeDrawBorderElapsedTicks),
            _runtimeResolvePartsCallCount,
            TicksToMilliseconds(_runtimeResolvePartsElapsedTicks),
            _runtimeResolvePartsDuplicateRoleCount,
            _runtimeComputeThumbRectCallCount,
            TicksToMilliseconds(_runtimeComputeThumbRectElapsedTicks),
            _runtimeComputeSliderThumbRectCallCount,
            TicksToMilliseconds(_runtimeComputeSliderThumbRectElapsedTicks),
            _runtimeResolveThumbAxisLengthCallCount,
            TicksToMilliseconds(_runtimeResolveThumbAxisLengthElapsedTicks),
            _runtimeGetScrollableRangeCallCount,
            _runtimeClampValueCallCount,
            _runtimeOnStateMutationChangedCallCount,
            _runtimeRefreshLayoutForStateMutationCallCount,
            TicksToMilliseconds(_runtimeRefreshLayoutForStateMutationElapsedTicks),
            _runtimeRefreshLayoutNeedsMeasureFallbackCount,
            _runtimeRefreshLayoutDirtyBoundsHintCount,
            _runtimeRefreshLayoutVisualFallbackCount,
            _runtimeInvalidateVisualWithDirtyBoundsHintCallCount,
            TicksToMilliseconds(_runtimeInvalidateVisualWithDirtyBoundsHintElapsedTicks),
            _runtimeCaptureRenderMutationSnapshotCallCount,
            TicksToMilliseconds(_runtimeCaptureRenderMutationSnapshotElapsedTicks),
            _runtimeTryBuildRenderMutationDirtyBoundsCallCount,
            _runtimeTryBuildRenderMutationDirtyBoundsBuiltCount,
            _runtimeTryBuildRenderMutationDirtyBoundsTrackChangedCount,
            _runtimeTryBuildRenderMutationDirtyBoundsPartChangedCount);
    }

    internal new static TrackTelemetrySnapshot GetTelemetryAndReset()
    {
        return CreateTelemetrySnapshot(reset: true);
    }

    internal new static TrackTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return CreateTelemetrySnapshot(reset: false);
    }

    internal static TrackTelemetrySnapshot GetTelemetrySnapshotForDiagnostics()
    {
        return GetAggregateTelemetrySnapshotForDiagnostics();
    }

    private static TrackTelemetrySnapshot CreateTelemetrySnapshot(bool reset)
    {
        return new TrackTelemetrySnapshot(
            ReadOrReset(ref _diagGetThumbRectCallCount, reset),
            ReadOrReset(ref _diagGetTrackRectCallCount, reset),
            ReadOrReset(ref _diagGetThumbTravelCallCount, reset),
            ReadOrReset(ref _diagGetValueFromThumbTravelCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagGetValueFromThumbTravelElapsedTicks, reset)),
            ReadOrReset(ref _diagGetValueFromThumbTravelScrollableRangeZeroCount, reset),
            ReadOrReset(ref _diagGetValueFromThumbTravelMaxTravelZeroCount, reset),
            ReadOrReset(ref _diagGetValueFromThumbTravelDirectionReversedCount, reset),
            ReadOrReset(ref _diagGetValueFromThumbTravelClampedCount, reset),
            ReadOrReset(ref _diagGetValueFromPointCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagGetValueFromPointElapsedTicks, reset)),
            ReadOrReset(ref _diagGetValueFromPointThumbCenterOffsetCount, reset),
            ReadOrReset(ref _diagGetValueFromPointScrollableRangeZeroCount, reset),
            ReadOrReset(ref _diagGetValueFromPointMaxTravelZeroCount, reset),
            ReadOrReset(ref _diagGetValuePositionCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagGetValuePositionElapsedTicks, reset)),
            ReadOrReset(ref _diagGetValuePositionZeroRangeFallbackCount, reset),
            ReadOrReset(ref _diagGetValuePositionDirectionReversedCount, reset),
            ReadOrReset(ref _diagHitTestDecreaseRegionCallCount, reset),
            ReadOrReset(ref _diagHitTestDecreaseRegionHitCount, reset),
            ReadOrReset(ref _diagHitTestIncreaseRegionCallCount, reset),
            ReadOrReset(ref _diagHitTestIncreaseRegionHitCount, reset),
            ReadOrReset(ref _diagInvalidateVisualCallCount, reset),
            ReadOrReset(ref _diagInvalidateVisualClearedPendingHintCount, reset),
            ReadOrReset(ref _diagTryConsumeRenderDirtyBoundsHintCallCount, reset),
            ReadOrReset(ref _diagTryConsumeRenderDirtyBoundsHintHitCount, reset),
            ReadOrReset(ref _diagMeasureOverrideCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagMeasureOverrideElapsedTicks, reset)),
            ReadOrReset(ref _diagMeasureOverrideVerticalCount, reset),
            ReadOrReset(ref _diagMeasureOverrideHorizontalCount, reset),
            ReadOrReset(ref _diagMeasureOverrideViewportSizedThumbCount, reset),
            ReadOrReset(ref _diagMeasureOverrideSliderThumbCount, reset),
            ReadOrReset(ref _diagMeasureOverrideResolvedDecreasePartCount, reset),
            ReadOrReset(ref _diagMeasureOverrideResolvedThumbPartCount, reset),
            ReadOrReset(ref _diagMeasureOverrideResolvedIncreasePartCount, reset),
            ReadOrReset(ref _diagArrangeOverrideCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagArrangeOverrideElapsedTicks, reset)),
            ReadOrReset(ref _diagArrangeOverrideVerticalCount, reset),
            ReadOrReset(ref _diagArrangeOverrideHorizontalCount, reset),
            ReadOrReset(ref _diagArrangeOverrideExtraChildArrangeCount, reset),
            ReadOrReset(ref _diagArrangeOverrideSkippedNonFrameworkChildCount, reset),
            ReadOrReset(ref _diagArrangeVerticalCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagArrangeVerticalElapsedTicks, reset)),
            ReadOrReset(ref _diagArrangeVerticalViewportSizedThumbCount, reset),
            ReadOrReset(ref _diagArrangeVerticalSliderThumbCount, reset),
            ReadOrReset(ref _diagArrangeHorizontalCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagArrangeHorizontalElapsedTicks, reset)),
            ReadOrReset(ref _diagArrangeHorizontalViewportSizedThumbCount, reset),
            ReadOrReset(ref _diagArrangeHorizontalSliderThumbCount, reset),
            ReadOrReset(ref _diagRenderCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagRenderElapsedTicks, reset)),
            ReadOrReset(ref _diagRenderSkippedEmptyTrackCount, reset),
            ReadOrReset(ref _diagDrawBorderCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagDrawBorderElapsedTicks, reset)),
            ReadOrReset(ref _diagDrawBorderLeftSegmentCount, reset),
            ReadOrReset(ref _diagDrawBorderRightSegmentCount, reset),
            ReadOrReset(ref _diagDrawBorderTopSegmentCount, reset),
            ReadOrReset(ref _diagDrawBorderBottomSegmentCount, reset),
            ReadOrReset(ref _diagResolvePartsCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagResolvePartsElapsedTicks, reset)),
            ReadOrReset(ref _diagResolvePartsScannedChildCount, reset),
            ReadOrReset(ref _diagResolvePartsNonFrameworkChildCount, reset),
            ReadOrReset(ref _diagResolvePartsDuplicateRoleCount, reset),
            ReadOrReset(ref _diagResolvePartsDecreaseFoundCount, reset),
            ReadOrReset(ref _diagResolvePartsThumbFoundCount, reset),
            ReadOrReset(ref _diagResolvePartsIncreaseFoundCount, reset),
            ReadOrReset(ref _diagComputeThumbRectCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagComputeThumbRectElapsedTicks, reset)),
            ReadOrReset(ref _diagComputeThumbRectZeroTrackRectCount, reset),
            ReadOrReset(ref _diagComputeThumbRectDirectionReversedCount, reset),
            ReadOrReset(ref _diagComputeSliderThumbRectCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagComputeSliderThumbRectElapsedTicks, reset)),
            ReadOrReset(ref _diagComputeSliderThumbRectZeroTrackRectCount, reset),
            ReadOrReset(ref _diagComputeSliderThumbRectDirectionReversedCount, reset),
            ReadOrReset(ref _diagResolveThumbAxisLengthCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagResolveThumbAxisLengthElapsedTicks, reset)),
            ReadOrReset(ref _diagResolveThumbAxisLengthViewportSizedCount, reset),
            ReadOrReset(ref _diagResolveThumbAxisLengthExplicitCount, reset),
            ReadOrReset(ref _diagResolveThumbAxisLengthExtentZeroCount, reset),
            ReadOrReset(ref _diagResolveThumbAxisLengthFallbackViewportRatioCount, reset),
            ReadOrReset(ref _diagResolveThumbAxisLengthViewportRatioCount, reset),
            ReadOrReset(ref _diagGetScrollableRangeCallCount, reset),
            ReadOrReset(ref _diagGetScrollableRangeViewportSizedCount, reset),
            ReadOrReset(ref _diagClampValueCallCount, reset),
            ReadOrReset(ref _diagClampValueClampedLowCount, reset),
            ReadOrReset(ref _diagClampValueClampedHighCount, reset),
            ReadOrReset(ref _diagOnStateMutationChangedCallCount, reset),
            ReadOrReset(ref _diagRefreshLayoutForStateMutationCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagRefreshLayoutForStateMutationElapsedTicks, reset)),
            ReadOrReset(ref _diagRefreshLayoutValueMutationCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagRefreshLayoutValueMutationElapsedTicks, reset)),
            ReadOrReset(ref _diagRefreshLayoutViewportMutationCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagRefreshLayoutViewportMutationElapsedTicks, reset)),
            ReadOrReset(ref _diagRefreshLayoutMinimumMutationCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagRefreshLayoutMinimumMutationElapsedTicks, reset)),
            ReadOrReset(ref _diagRefreshLayoutMaximumMutationCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagRefreshLayoutMaximumMutationElapsedTicks, reset)),
            ReadOrReset(ref _diagRefreshLayoutDirectionMutationCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagRefreshLayoutDirectionMutationElapsedTicks, reset)),
            ReadOrReset(ref _diagRefreshLayoutNeedsMeasureFallbackCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagRefreshLayoutNeedsMeasureFallbackElapsedTicks, reset)),
            TicksToMilliseconds(ReadOrReset(ref _diagRefreshLayoutCaptureSnapshotElapsedTicks, reset)),
            TicksToMilliseconds(ReadOrReset(ref _diagRefreshLayoutInvalidateArrangeElapsedTicks, reset)),
            TicksToMilliseconds(ReadOrReset(ref _diagRefreshLayoutArrangeElapsedTicks, reset)),
            TicksToMilliseconds(ReadOrReset(ref _diagRefreshLayoutDirtyBoundsElapsedTicks, reset)),
            ReadOrReset(ref _diagRefreshLayoutDirtyBoundsHintCount, reset),
            ReadOrReset(ref _diagRefreshLayoutVisualFallbackCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagRefreshLayoutVisualInvalidationElapsedTicks, reset)),
            ReadOrReset(ref _diagInvalidateVisualWithDirtyBoundsHintCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagInvalidateVisualWithDirtyBoundsHintElapsedTicks, reset)),
            ReadOrReset(ref _diagCaptureRenderMutationSnapshotCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagCaptureRenderMutationSnapshotElapsedTicks, reset)),
            ReadOrReset(ref _diagTryBuildRenderMutationDirtyBoundsCallCount, reset),
            ReadOrReset(ref _diagTryBuildRenderMutationDirtyBoundsBuiltCount, reset),
            ReadOrReset(ref _diagTryBuildRenderMutationDirtyBoundsTrackChangedCount, reset),
            ReadOrReset(ref _diagTryBuildRenderMutationDirtyBoundsPartChangedCount, reset));
    }

    internal float GetValueFromPoint(Vector2 point, bool useThumbCenterOffset)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeGetValueFromPointCallCount++;
        IncrementAggregate(ref _diagGetValueFromPointCallCount);
        var elapsedTicks = 0L;
        if (useThumbCenterOffset)
        {
            IncrementAggregate(ref _diagGetValueFromPointThumbCenterOffsetCount);
        }

        var scrollableRange = GetScrollableRange();
        if (scrollableRange <= ValueEpsilon)
        {
            IncrementAggregate(ref _diagGetValueFromPointScrollableRangeZeroCount);
            elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _runtimeGetValueFromPointElapsedTicks += elapsedTicks;
            AddAggregate(ref _diagGetValueFromPointElapsedTicks, elapsedTicks);
            return Minimum;
        }

        var trackLength = GetAxisLength(_trackRect);
        var thumbLength = GetAxisLength(_thumbRect);
        var maxTravel = MathF.Max(0f, trackLength - thumbLength);
        if (maxTravel <= ValueEpsilon)
        {
            IncrementAggregate(ref _diagGetValueFromPointMaxTravelZeroCount);
            elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _runtimeGetValueFromPointElapsedTicks += elapsedTicks;
            AddAggregate(ref _diagGetValueFromPointElapsedTicks, elapsedTicks);
            return Minimum;
        }

        var axisPoint = Orientation == Orientation.Vertical ? point.Y : point.X;
        var trackStart = Orientation == Orientation.Vertical ? _trackRect.Y : _trackRect.X;
        var adjustedPoint = axisPoint - trackStart;
        if (useThumbCenterOffset)
        {
            adjustedPoint -= thumbLength / 2f;
        }

        var result = GetValueFromThumbTravel(adjustedPoint);
        elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _runtimeGetValueFromPointElapsedTicks += elapsedTicks;
        AddAggregate(ref _diagGetValueFromPointElapsedTicks, elapsedTicks);
        return result;
    }

    internal float GetValuePosition(float value)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeGetValuePositionCallCount++;
        IncrementAggregate(ref _diagGetValuePositionCallCount);
        var elapsedTicks = 0L;
        var scrollableRange = GetScrollableRange();
        var trackLength = GetAxisLength(_trackRect);
        var thumbLength = GetAxisLength(_thumbRect);
        var maxTravel = MathF.Max(0f, trackLength - thumbLength);
        if (maxTravel <= ValueEpsilon || scrollableRange <= ValueEpsilon)
        {
            IncrementAggregate(ref _diagGetValuePositionZeroRangeFallbackCount);
            var fallback = Orientation == Orientation.Vertical
                ? _trackRect.Y + (trackLength / 2f)
                : _trackRect.X + (trackLength / 2f);
            elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _runtimeGetValuePositionElapsedTicks += elapsedTicks;
            AddAggregate(ref _diagGetValuePositionElapsedTicks, elapsedTicks);
            return fallback;
        }

        var normalized = (ClampValue(value) - Minimum) / scrollableRange;
        if (IsDirectionReversed)
        {
            IncrementAggregate(ref _diagGetValuePositionDirectionReversedCount);
        }

        var positionFraction = IsDirectionReversed ? 1f - normalized : normalized;
        var travel = maxTravel * MathF.Max(0f, MathF.Min(1f, positionFraction));
        var position = Orientation == Orientation.Vertical
            ? _trackRect.Y + travel + (thumbLength / 2f)
            : _trackRect.X + travel + (thumbLength / 2f);
        elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _runtimeGetValuePositionElapsedTicks += elapsedTicks;
        AddAggregate(ref _diagGetValuePositionElapsedTicks, elapsedTicks);
        return position;
    }

    internal bool HitTestDecreaseRegion(Vector2 point)
    {
        _runtimeHitTestDecreaseRegionCallCount++;
        IncrementAggregate(ref _diagHitTestDecreaseRegionCallCount);
        var hit = ContainsPoint(_decreaseRegionRect, point);
        if (hit)
        {
            _runtimeHitTestDecreaseRegionHitCount++;
            IncrementAggregate(ref _diagHitTestDecreaseRegionHitCount);
        }

        return hit;
    }

    internal bool HitTestIncreaseRegion(Vector2 point)
    {
        _runtimeHitTestIncreaseRegionCallCount++;
        IncrementAggregate(ref _diagHitTestIncreaseRegionCallCount);
        var hit = ContainsPoint(_increaseRegionRect, point);
        if (hit)
        {
            _runtimeHitTestIncreaseRegionHitCount++;
            IncrementAggregate(ref _diagHitTestIncreaseRegionHitCount);
        }

        return hit;
    }

    public override void InvalidateVisual()
    {
        _runtimeInvalidateVisualCallCount++;
        IncrementAggregate(ref _diagInvalidateVisualCallCount);
        if (!_preserveRenderDirtyBoundsHint)
        {
            if (_hasPendingRenderDirtyBoundsHint)
            {
                _runtimeInvalidateVisualClearedPendingHintCount++;
                IncrementAggregate(ref _diagInvalidateVisualClearedPendingHintCount);
            }

            _hasPendingRenderDirtyBoundsHint = false;
        }

        base.InvalidateVisual();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeMeasureOverrideCallCount++;
        try
        {
            ResolveParts(out var decreaseButton, out var thumb, out var increaseButton);
            if (decreaseButton != null)
            {
                IncrementAggregate(ref _diagMeasureOverrideResolvedDecreasePartCount);
            }

            if (thumb != null)
            {
                IncrementAggregate(ref _diagMeasureOverrideResolvedThumbPartCount);
            }

            if (increaseButton != null)
            {
                IncrementAggregate(ref _diagMeasureOverrideResolvedIncreasePartCount);
            }

            MeasurePart(decreaseButton, availableSize);
            MeasurePart(thumb, availableSize);
            MeasurePart(increaseButton, availableSize);

            var baseDesired = base.MeasureOverride(availableSize);
            var thumbLength = ResolveThumbAxisLength();
            if (IsViewportSizedThumb)
            {
                IncrementAggregate(ref _diagMeasureOverrideViewportSizedThumbCount);
            }
            else
            {
                IncrementAggregate(ref _diagMeasureOverrideSliderThumbCount);
            }

            if (Orientation == Orientation.Vertical)
            {
                IncrementAggregate(ref _diagMeasureOverrideVerticalCount);
                var cross = MathF.Max(
                    MathF.Max(GetCrossDesiredSize(decreaseButton, isVertical: true), GetCrossDesiredSize(increaseButton, isVertical: true)),
                    MathF.Max(GetCrossDesiredSize(thumb, isVertical: true), MathF.Max(ResolveTrackCrossLength(MathF.Max(GetCrossDesiredSize(thumb, isVertical: true), 12f)), 12f)));
                var desiredHeight =
                    ResolveDesiredButtonLength(decreaseButton, cross, isVertical: true, includeButton: ShowLineButtons) +
                    MathF.Max(thumbLength, ThumbMinLength) +
                    ResolveDesiredButtonLength(increaseButton, cross, isVertical: true, includeButton: ShowLineButtons);
                return new Vector2(MathF.Max(baseDesired.X, cross), MathF.Max(baseDesired.Y, desiredHeight));
            }

            IncrementAggregate(ref _diagMeasureOverrideHorizontalCount);
            var horizontalCross = MathF.Max(
                MathF.Max(GetCrossDesiredSize(decreaseButton, isVertical: false), GetCrossDesiredSize(increaseButton, isVertical: false)),
                MathF.Max(GetCrossDesiredSize(thumb, isVertical: false), MathF.Max(ResolveTrackCrossLength(MathF.Max(GetCrossDesiredSize(thumb, isVertical: false), 12f)), 12f)));
            var desiredWidth =
                ResolveDesiredButtonLength(decreaseButton, horizontalCross, isVertical: false, includeButton: ShowLineButtons) +
                MathF.Max(thumbLength, ThumbMinLength) +
                ResolveDesiredButtonLength(increaseButton, horizontalCross, isVertical: false, includeButton: ShowLineButtons);
            return new Vector2(MathF.Max(baseDesired.X, desiredWidth), MathF.Max(baseDesired.Y, horizontalCross));
        }
        finally
        {
            _runtimeMeasureOverrideElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            RecordAggregateElapsed(ref _diagMeasureOverrideCallCount, ref _diagMeasureOverrideElapsedTicks, startTicks);
        }
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeArrangeOverrideCallCount++;
        try
        {
            ResolveParts(out var decreaseButton, out var thumb, out var increaseButton);

            if (Orientation == Orientation.Vertical)
            {
                IncrementAggregate(ref _diagArrangeOverrideVerticalCount);
                ArrangeVertical(finalSize, decreaseButton, thumb, increaseButton);
            }
            else
            {
                IncrementAggregate(ref _diagArrangeOverrideHorizontalCount);
                ArrangeHorizontal(finalSize, decreaseButton, thumb, increaseButton);
            }

            for (var i = 0; i < Children.Count; i++)
            {
                if (Children[i] is not FrameworkElement child)
                {
                    IncrementAggregate(ref _diagArrangeOverrideSkippedNonFrameworkChildCount);
                    continue;
                }

                if (GetPartRole(child) != TrackPartRole.None)
                {
                    continue;
                }

                IncrementAggregate(ref _diagArrangeOverrideExtraChildArrangeCount);
                ArrangePartIfNeeded(child, new LayoutRect(LayoutSlot.X, LayoutSlot.Y, finalSize.X, finalSize.Y));
            }

            return finalSize;
        }
        finally
        {
            _runtimeArrangeOverrideElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            RecordAggregateElapsed(ref _diagArrangeOverrideCallCount, ref _diagArrangeOverrideElapsedTicks, startTicks);
        }
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeRenderCallCount++;
        if (_trackRect.Width <= 0f || _trackRect.Height <= 0f)
        {
            IncrementAggregate(ref _diagRenderSkippedEmptyTrackCount);
            _runtimeRenderElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            RecordAggregateElapsed(ref _diagRenderCallCount, ref _diagRenderElapsedTicks, startTicks);
            return;
        }

        UiDrawing.DrawFilledRect(spriteBatch, _trackRect, Background, Opacity);
        DrawBorder(spriteBatch, _trackRect);
        _runtimeRenderElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        RecordAggregateElapsed(ref _diagRenderCallCount, ref _diagRenderElapsedTicks, startTicks);
    }

    private void ArrangeVertical(
        Vector2 finalSize,
        FrameworkElement? decreaseButton,
        FrameworkElement? thumb,
        FrameworkElement? increaseButton)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeArrangeVerticalCallCount++;
        IncrementAggregate(ref _diagArrangeVerticalCallCount);
        var slot = LayoutSlot;
        var slotWidth = MathF.Max(0f, finalSize.X);
        var slotHeight = MathF.Max(0f, finalSize.Y);

        if (IsViewportSizedThumb)
        {
            IncrementAggregate(ref _diagArrangeVerticalViewportSizedThumbCount);
            var decreaseLength = MathF.Min(slotHeight, ResolveArrangedButtonLength(decreaseButton, slotWidth, isVertical: true, includeButton: ShowLineButtons));
            var increaseLength = MathF.Min(MathF.Max(0f, slotHeight - decreaseLength), ResolveArrangedButtonLength(increaseButton, slotWidth, isVertical: true, includeButton: ShowLineButtons));

            ArrangePartIfNeeded(decreaseButton, new LayoutRect(slot.X, slot.Y, slotWidth, decreaseLength));
            ArrangePartIfNeeded(increaseButton, new LayoutRect(slot.X, slot.Y + slotHeight - increaseLength, slotWidth, increaseLength));

            _trackRect = CreateCenteredTrackRect(
                new LayoutRect(slot.X, slot.Y + decreaseLength, slotWidth, MathF.Max(0f, slotHeight - decreaseLength - increaseLength)),
                slotWidth,
                MathF.Max(0f, slotHeight - decreaseLength - increaseLength),
                isVertical: true);
            _thumbRect = ComputeThumbRect(_trackRect);
            var trackStartRegionRect = new LayoutRect(_trackRect.X, _trackRect.Y, _trackRect.Width, MathF.Max(0f, _thumbRect.Y - _trackRect.Y));
            var trackEndRegionRect = new LayoutRect(
                _trackRect.X,
                _thumbRect.Y + _thumbRect.Height,
                _trackRect.Width,
                MathF.Max(0f, (_trackRect.Y + _trackRect.Height) - (_thumbRect.Y + _thumbRect.Height)));

            _decreaseRegionRect = IsDirectionReversed ? trackEndRegionRect : trackStartRegionRect;
            _increaseRegionRect = IsDirectionReversed ? trackStartRegionRect : trackEndRegionRect;
            ArrangePartIfNeeded(thumb, _thumbRect);
            _runtimeArrangeVerticalElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            AddAggregate(ref _diagArrangeVerticalElapsedTicks, Stopwatch.GetTimestamp() - startTicks);
            return;
        }

        IncrementAggregate(ref _diagArrangeVerticalSliderThumbCount);
        _trackRect = CreateCenteredTrackRect(slot, slotWidth, slotHeight, isVertical: true);
        _thumbRect = ComputeSliderThumbRect(_trackRect, slotWidth, slotHeight);
        var startRegionRect = new LayoutRect(_trackRect.X, _trackRect.Y, _trackRect.Width, MathF.Max(0f, _thumbRect.Y - _trackRect.Y));
        var endRegionRect = new LayoutRect(
            _trackRect.X,
            _thumbRect.Y + _thumbRect.Height,
            _trackRect.Width,
            MathF.Max(0f, (_trackRect.Y + _trackRect.Height) - (_thumbRect.Y + _thumbRect.Height)));

        var startButtonRect = new LayoutRect(slot.X, startRegionRect.Y, slotWidth, startRegionRect.Height);
        var endButtonRect = new LayoutRect(slot.X, endRegionRect.Y, slotWidth, endRegionRect.Height);
        _decreaseRegionRect = IsDirectionReversed ? endButtonRect : startButtonRect;
        _increaseRegionRect = IsDirectionReversed ? startButtonRect : endButtonRect;

        ArrangePartIfNeeded(decreaseButton, _decreaseRegionRect);
        ArrangePartIfNeeded(increaseButton, _increaseRegionRect);
        ArrangePartIfNeeded(thumb, _thumbRect);
        _runtimeArrangeVerticalElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        AddAggregate(ref _diagArrangeVerticalElapsedTicks, Stopwatch.GetTimestamp() - startTicks);
    }

    private void ArrangeHorizontal(
        Vector2 finalSize,
        FrameworkElement? decreaseButton,
        FrameworkElement? thumb,
        FrameworkElement? increaseButton)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeArrangeHorizontalCallCount++;
        IncrementAggregate(ref _diagArrangeHorizontalCallCount);
        var slot = LayoutSlot;
        var slotWidth = MathF.Max(0f, finalSize.X);
        var slotHeight = MathF.Max(0f, finalSize.Y);

        if (IsViewportSizedThumb)
        {
            IncrementAggregate(ref _diagArrangeHorizontalViewportSizedThumbCount);
            var decreaseLength = MathF.Min(slotWidth, ResolveArrangedButtonLength(decreaseButton, slotHeight, isVertical: false, includeButton: ShowLineButtons));
            var increaseLength = MathF.Min(MathF.Max(0f, slotWidth - decreaseLength), ResolveArrangedButtonLength(increaseButton, slotHeight, isVertical: false, includeButton: ShowLineButtons));

            ArrangePartIfNeeded(decreaseButton, new LayoutRect(slot.X, slot.Y, decreaseLength, slotHeight));
            ArrangePartIfNeeded(increaseButton, new LayoutRect(slot.X + slotWidth - increaseLength, slot.Y, increaseLength, slotHeight));

            _trackRect = CreateCenteredTrackRect(
                new LayoutRect(slot.X + decreaseLength, slot.Y, MathF.Max(0f, slotWidth - decreaseLength - increaseLength), slotHeight),
                MathF.Max(0f, slotWidth - decreaseLength - increaseLength),
                slotHeight,
                isVertical: false);
            _thumbRect = ComputeThumbRect(_trackRect);
            var trackStartRegionRect = new LayoutRect(_trackRect.X, _trackRect.Y, MathF.Max(0f, _thumbRect.X - _trackRect.X), _trackRect.Height);
            var trackEndRegionRect = new LayoutRect(
                _thumbRect.X + _thumbRect.Width,
                _trackRect.Y,
                MathF.Max(0f, (_trackRect.X + _trackRect.Width) - (_thumbRect.X + _thumbRect.Width)),
                _trackRect.Height);

            _decreaseRegionRect = IsDirectionReversed ? trackEndRegionRect : trackStartRegionRect;
            _increaseRegionRect = IsDirectionReversed ? trackStartRegionRect : trackEndRegionRect;
            ArrangePartIfNeeded(thumb, _thumbRect);
            _runtimeArrangeHorizontalElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            AddAggregate(ref _diagArrangeHorizontalElapsedTicks, Stopwatch.GetTimestamp() - startTicks);
            return;
        }

        IncrementAggregate(ref _diagArrangeHorizontalSliderThumbCount);
        _trackRect = CreateCenteredTrackRect(slot, slotWidth, slotHeight, isVertical: false);
        _thumbRect = ComputeSliderThumbRect(_trackRect, slotWidth, slotHeight);
        var startRegionRect = new LayoutRect(_trackRect.X, _trackRect.Y, MathF.Max(0f, _thumbRect.X - _trackRect.X), _trackRect.Height);
        var endRegionRect = new LayoutRect(
            _thumbRect.X + _thumbRect.Width,
            _trackRect.Y,
            MathF.Max(0f, (_trackRect.X + _trackRect.Width) - (_thumbRect.X + _thumbRect.Width)),
            _trackRect.Height);

        var startButtonRect = new LayoutRect(startRegionRect.X, slot.Y, startRegionRect.Width, slotHeight);
        var endButtonRect = new LayoutRect(endRegionRect.X, slot.Y, endRegionRect.Width, slotHeight);
        _decreaseRegionRect = IsDirectionReversed ? endButtonRect : startButtonRect;
        _increaseRegionRect = IsDirectionReversed ? startButtonRect : endButtonRect;

        ArrangePartIfNeeded(decreaseButton, _decreaseRegionRect);
        ArrangePartIfNeeded(increaseButton, _increaseRegionRect);
        ArrangePartIfNeeded(thumb, _thumbRect);
        _runtimeArrangeHorizontalElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        AddAggregate(ref _diagArrangeHorizontalElapsedTicks, Stopwatch.GetTimestamp() - startTicks);
    }

    private LayoutRect CreateCenteredTrackRect(LayoutRect slot, float slotWidth, float slotHeight, bool isVertical)
    {
        if (isVertical)
        {
            var trackWidth = ResolveTrackCrossLength(slotWidth);
            return new LayoutRect(slot.X + ((slotWidth - trackWidth) / 2f), slot.Y, trackWidth, slotHeight);
        }

        var trackHeight = ResolveTrackCrossLength(slotHeight);
        return new LayoutRect(slot.X, slot.Y + ((slotHeight - trackHeight) / 2f), slotWidth, trackHeight);
    }

    private float ResolveTrackCrossLength(float slotCrossLength)
    {
        if (TrackThickness <= 0f || float.IsNaN(TrackThickness))
        {
            return MathF.Max(0f, slotCrossLength);
        }

        return MathF.Min(MathF.Max(0f, slotCrossLength), TrackThickness);
    }

    private void ResolveParts(out FrameworkElement? decreaseButton, out FrameworkElement? thumb, out FrameworkElement? increaseButton)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeResolvePartsCallCount++;
        IncrementAggregate(ref _diagResolvePartsCallCount);
        decreaseButton = null;
        thumb = null;
        increaseButton = null;

        for (var i = 0; i < Children.Count; i++)
        {
            IncrementAggregate(ref _diagResolvePartsScannedChildCount);
            if (Children[i] is not FrameworkElement child)
            {
                IncrementAggregate(ref _diagResolvePartsNonFrameworkChildCount);
                continue;
            }

            switch (GetPartRole(child))
            {
                case TrackPartRole.DecreaseButton when decreaseButton == null:
                    decreaseButton = child;
                    IncrementAggregate(ref _diagResolvePartsDecreaseFoundCount);
                    break;
                case TrackPartRole.DecreaseButton:
                    _runtimeResolvePartsDuplicateRoleCount++;
                    IncrementAggregate(ref _diagResolvePartsDuplicateRoleCount);
                    break;
                case TrackPartRole.Thumb when thumb == null:
                    thumb = child;
                    IncrementAggregate(ref _diagResolvePartsThumbFoundCount);
                    break;
                case TrackPartRole.Thumb:
                    _runtimeResolvePartsDuplicateRoleCount++;
                    IncrementAggregate(ref _diagResolvePartsDuplicateRoleCount);
                    break;
                case TrackPartRole.IncreaseButton when increaseButton == null:
                    increaseButton = child;
                    IncrementAggregate(ref _diagResolvePartsIncreaseFoundCount);
                    break;
                case TrackPartRole.IncreaseButton:
                    _runtimeResolvePartsDuplicateRoleCount++;
                    IncrementAggregate(ref _diagResolvePartsDuplicateRoleCount);
                    break;
            }
        }

        _runtimeResolvePartsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        AddAggregate(ref _diagResolvePartsElapsedTicks, Stopwatch.GetTimestamp() - startTicks);
    }

    private static void MeasurePart(FrameworkElement? element, Vector2 availableSize)
    {
        element?.Measure(availableSize);
    }

    private static float GetCrossDesiredSize(FrameworkElement? element, bool isVertical)
    {
        if (element == null)
        {
            return 0f;
        }

        return isVertical ? element.DesiredSize.X : element.DesiredSize.Y;
    }

    private static float ResolveDesiredButtonLength(FrameworkElement? element, float crossAxisLength, bool isVertical, bool includeButton)
    {
        if (!includeButton || element == null)
        {
            return 0f;
        }

        return isVertical
            ? MathF.Max(crossAxisLength, element.DesiredSize.Y)
            : MathF.Max(crossAxisLength, element.DesiredSize.X);
    }

    private static float ResolveArrangedButtonLength(FrameworkElement? element, float crossAxisLength, bool isVertical, bool includeButton)
    {
        if (!includeButton || element == null)
        {
            return 0f;
        }

        if (isVertical &&
            element.GetValueSource(FrameworkElement.HeightProperty) != DependencyPropertyValueSource.Default &&
            float.IsFinite(element.Height) &&
            element.Height > 0f)
        {
            return element.Height;
        }

        if (!isVertical &&
            element.GetValueSource(FrameworkElement.WidthProperty) != DependencyPropertyValueSource.Default &&
            float.IsFinite(element.Width) &&
            element.Width > 0f)
        {
            return element.Width;
        }

        return ResolveDesiredButtonLength(element, crossAxisLength, isVertical, includeButton: true);
    }

    private float ResolveThumbAxisLength()
    {
        _runtimeResolveThumbAxisLengthCallCount++;
        IncrementAggregate(ref _diagResolveThumbAxisLengthCallCount);
        IncrementAggregate(ref _diagResolveThumbAxisLengthExplicitCount);
        return IsViewportSizedThumb
            ? ThumbMinLength
            : MathF.Max(ThumbMinLength, ThumbLength > 0f ? ThumbLength : ThumbMinLength);
    }

    private LayoutRect ComputeThumbRect(LayoutRect trackRect)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeComputeThumbRectCallCount++;
        IncrementAggregate(ref _diagComputeThumbRectCallCount);
        if (trackRect.Width <= 0f || trackRect.Height <= 0f)
        {
            IncrementAggregate(ref _diagComputeThumbRectZeroTrackRectCount);
            _runtimeComputeThumbRectElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            AddAggregate(ref _diagComputeThumbRectElapsedTicks, Stopwatch.GetTimestamp() - startTicks);
            return new LayoutRect(trackRect.X, trackRect.Y, 0f, 0f);
        }

        var trackLength = Orientation == Orientation.Vertical ? trackRect.Height : trackRect.Width;
        var thumbAxisLength = ResolveThumbAxisLength(trackLength);
        var maxTravel = MathF.Max(0f, trackLength - thumbAxisLength);
        var scrollableRange = GetScrollableRange();
        var normalized = scrollableRange <= ValueEpsilon ? 0f : (ClampValue(Value) - Minimum) / scrollableRange;
        var travelFraction = IsDirectionReversed ? 1f - normalized : normalized;
        if (IsDirectionReversed)
        {
            IncrementAggregate(ref _diagComputeThumbRectDirectionReversedCount);
        }

        var thumbTravel = maxTravel * MathF.Max(0f, MathF.Min(1f, travelFraction));

        var rect = Orientation == Orientation.Vertical
            ? new LayoutRect(trackRect.X, trackRect.Y + thumbTravel, trackRect.Width, thumbAxisLength)
            : new LayoutRect(trackRect.X + thumbTravel, trackRect.Y, thumbAxisLength, trackRect.Height);
        _runtimeComputeThumbRectElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        AddAggregate(ref _diagComputeThumbRectElapsedTicks, Stopwatch.GetTimestamp() - startTicks);
        return rect;
    }

    private float ResolveThumbAxisLength(float trackLength)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeResolveThumbAxisLengthCallCount++;
        IncrementAggregate(ref _diagResolveThumbAxisLengthCallCount);
        if (!IsViewportSizedThumb)
        {
            IncrementAggregate(ref _diagResolveThumbAxisLengthExplicitCount);
            _runtimeResolveThumbAxisLengthElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            AddAggregate(ref _diagResolveThumbAxisLengthElapsedTicks, Stopwatch.GetTimestamp() - startTicks);
            var explicitLength = ThumbLength > 0f ? ThumbLength : ThumbMinLength;
            return MathF.Min(trackLength, MathF.Max(ThumbMinLength, explicitLength));
        }

        IncrementAggregate(ref _diagResolveThumbAxisLengthViewportSizedCount);
        var extent = MathF.Max(0f, Maximum - Minimum);
        if (extent <= ValueEpsilon)
        {
            IncrementAggregate(ref _diagResolveThumbAxisLengthExtentZeroCount);
            _runtimeResolveThumbAxisLengthElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            AddAggregate(ref _diagResolveThumbAxisLengthElapsedTicks, Stopwatch.GetTimestamp() - startTicks);
            return trackLength;
        }

        var viewport = MathF.Max(0f, ViewportSize);
        var ratio = viewport > 0f
            ? MathF.Max(MinimumThumbRatio, MathF.Min(1f, viewport / MathF.Max(viewport, extent)))
            : FallbackThumbRatio;
        if (viewport > 0f)
        {
            IncrementAggregate(ref _diagResolveThumbAxisLengthViewportRatioCount);
        }
        else
        {
            IncrementAggregate(ref _diagResolveThumbAxisLengthFallbackViewportRatioCount);
        }

        _runtimeResolveThumbAxisLengthElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        AddAggregate(ref _diagResolveThumbAxisLengthElapsedTicks, Stopwatch.GetTimestamp() - startTicks);
        return MathF.Min(trackLength, MathF.Max(ThumbMinLength, trackLength * ratio));
    }

    private LayoutRect ComputeSliderThumbRect(LayoutRect trackRect, float slotWidth, float slotHeight)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeComputeSliderThumbRectCallCount++;
        IncrementAggregate(ref _diagComputeSliderThumbRectCallCount);
        if (trackRect.Width <= 0f || trackRect.Height <= 0f)
        {
            IncrementAggregate(ref _diagComputeSliderThumbRectZeroTrackRectCount);
            _runtimeComputeSliderThumbRectElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            AddAggregate(ref _diagComputeSliderThumbRectElapsedTicks, Stopwatch.GetTimestamp() - startTicks);
            return new LayoutRect(trackRect.X, trackRect.Y, 0f, 0f);
        }

        var trackLength = Orientation == Orientation.Vertical ? trackRect.Height : trackRect.Width;
        var thumbAxisLength = ResolveThumbAxisLength(trackLength);
        var thumbCrossLength = ResolveSliderThumbCrossLength(Orientation == Orientation.Vertical ? slotWidth : slotHeight);
        var maxTravel = MathF.Max(0f, trackLength - thumbAxisLength);
        var scrollableRange = GetScrollableRange();
        var normalized = scrollableRange <= ValueEpsilon ? 0f : (ClampValue(Value) - Minimum) / scrollableRange;
        var travelFraction = IsDirectionReversed ? 1f - normalized : normalized;
        if (IsDirectionReversed)
        {
            IncrementAggregate(ref _diagComputeSliderThumbRectDirectionReversedCount);
        }

        var thumbTravel = maxTravel * MathF.Max(0f, MathF.Min(1f, travelFraction));

        var rect = Orientation == Orientation.Vertical
            ? new LayoutRect(
                trackRect.X + ((trackRect.Width - thumbCrossLength) / 2f),
                trackRect.Y + thumbTravel,
                thumbCrossLength,
                thumbAxisLength)
            : new LayoutRect(
                trackRect.X + thumbTravel,
                trackRect.Y + ((trackRect.Height - thumbCrossLength) / 2f),
                thumbAxisLength,
                thumbCrossLength);
        _runtimeComputeSliderThumbRectElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        AddAggregate(ref _diagComputeSliderThumbRectElapsedTicks, Stopwatch.GetTimestamp() - startTicks);
        return rect;
    }

    private float ResolveSliderThumbCrossLength(float availableCrossLength)
    {
        var explicitLength = ThumbLength > 0f ? ThumbLength : ThumbMinLength;
        var desiredLength = MathF.Max(ThumbMinLength, explicitLength);
        if (availableCrossLength <= 0f)
        {
            return desiredLength;
        }

        return MathF.Min(availableCrossLength, desiredLength);
    }

    private float GetScrollableRange()
    {
        _runtimeGetScrollableRangeCallCount++;
        IncrementAggregate(ref _diagGetScrollableRangeCallCount);
        var extent = MathF.Max(0f, Maximum - Minimum);
        if (IsViewportSizedThumb)
        {
            IncrementAggregate(ref _diagGetScrollableRangeViewportSizedCount);
        }

        return IsViewportSizedThumb
            ? MathF.Max(0f, extent - MathF.Max(0f, ViewportSize))
            : extent;
    }

    private float ClampValue(float value)
    {
        _runtimeClampValueCallCount++;
        IncrementAggregate(ref _diagClampValueCallCount);
        var maxValue = Minimum + GetScrollableRange();
        if (maxValue < Minimum)
        {
            maxValue = Minimum;
        }

        if (value < Minimum)
        {
            IncrementAggregate(ref _diagClampValueClampedLowCount);
        }
        else if (value > maxValue)
        {
            IncrementAggregate(ref _diagClampValueClampedHighCount);
        }

        return MathF.Max(Minimum, MathF.Min(maxValue, value));
    }

    private float GetAxisLength(LayoutRect rect)
    {
        return Orientation == Orientation.Vertical ? rect.Height : rect.Width;
    }

    private void DrawBorder(SpriteBatch spriteBatch, LayoutRect rect)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeDrawBorderCallCount++;
        IncrementAggregate(ref _diagDrawBorderCallCount);
        var border = BorderThickness;
        if (border.Left > 0f)
        {
            IncrementAggregate(ref _diagDrawBorderLeftSegmentCount);
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(rect.X, rect.Y, border.Left, rect.Height), BorderBrush, Opacity);
        }

        if (border.Right > 0f)
        {
            IncrementAggregate(ref _diagDrawBorderRightSegmentCount);
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(rect.X + rect.Width - border.Right, rect.Y, border.Right, rect.Height), BorderBrush, Opacity);
        }

        if (border.Top > 0f)
        {
            IncrementAggregate(ref _diagDrawBorderTopSegmentCount);
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(rect.X, rect.Y, rect.Width, border.Top), BorderBrush, Opacity);
        }

        if (border.Bottom > 0f)
        {
            IncrementAggregate(ref _diagDrawBorderBottomSegmentCount);
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(rect.X, rect.Y + rect.Height - border.Bottom, rect.Width, border.Bottom), BorderBrush, Opacity);
        }

        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _runtimeDrawBorderElapsedTicks += elapsedTicks;
        AddAggregate(ref _diagDrawBorderElapsedTicks, elapsedTicks);
    }

    private static bool ContainsPoint(LayoutRect rect, Vector2 point)
    {
        return rect.Width > 0f &&
               rect.Height > 0f &&
               point.X >= rect.X &&
               point.X <= rect.X + rect.Width &&
               point.Y >= rect.Y &&
               point.Y <= rect.Y + rect.Height;
    }

    private static void ArrangePartIfNeeded(FrameworkElement? element, LayoutRect targetRect)
    {
        if (element == null)
        {
            return;
        }

        if (!element.NeedsArrange &&
            AreRectsClose(element.LayoutSlot, targetRect))
        {
            return;
        }

        element.Arrange(targetRect);
    }

    bool IRenderDirtyBoundsHintProvider.TryConsumeRenderDirtyBoundsHint(out LayoutRect bounds)
    {
        _runtimeTryConsumeRenderDirtyBoundsHintCallCount++;
        IncrementAggregate(ref _diagTryConsumeRenderDirtyBoundsHintCallCount);
        if (!_hasPendingRenderDirtyBoundsHint)
        {
            bounds = default;
            return false;
        }

        bounds = _pendingRenderDirtyBoundsHint;
        _hasPendingRenderDirtyBoundsHint = false;
        _runtimeTryConsumeRenderDirtyBoundsHintHitCount++;
        IncrementAggregate(ref _diagTryConsumeRenderDirtyBoundsHintHitCount);
        return true;
    }

    internal bool HasPendingRenderDirtyBoundsHintForRetainedSync()
    {
        return _hasPendingRenderDirtyBoundsHint;
    }

    private void OnStateMutationChanged(TrackStateMutationSource source)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeOnStateMutationChangedCallCount++;
        IncrementAggregate(ref _diagOnStateMutationChangedCallCount);
        if (ShouldDeferStateMutationLayoutRefresh())
        {
            InvalidateArrangeForDirectLayoutOnly();
        }
        else
        {
            RefreshLayoutForStateMutation();
        }
        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;

        switch (source)
        {
            case TrackStateMutationSource.Minimum:
                IncrementAggregate(ref _diagRefreshLayoutMinimumMutationCallCount);
                AddAggregate(ref _diagRefreshLayoutMinimumMutationElapsedTicks, elapsedTicks);
                break;
            case TrackStateMutationSource.Maximum:
                IncrementAggregate(ref _diagRefreshLayoutMaximumMutationCallCount);
                AddAggregate(ref _diagRefreshLayoutMaximumMutationElapsedTicks, elapsedTicks);
                break;
            case TrackStateMutationSource.Value:
                IncrementAggregate(ref _diagRefreshLayoutValueMutationCallCount);
                AddAggregate(ref _diagRefreshLayoutValueMutationElapsedTicks, elapsedTicks);
                break;
            case TrackStateMutationSource.ViewportSize:
                IncrementAggregate(ref _diagRefreshLayoutViewportMutationCallCount);
                AddAggregate(ref _diagRefreshLayoutViewportMutationElapsedTicks, elapsedTicks);
                break;
            case TrackStateMutationSource.IsDirectionReversed:
                IncrementAggregate(ref _diagRefreshLayoutDirectionMutationCallCount);
                AddAggregate(ref _diagRefreshLayoutDirectionMutationElapsedTicks, elapsedTicks);
                break;
        }
    }

    private void RefreshLayoutForStateMutation()
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeRefreshLayoutForStateMutationCallCount++;
        IncrementAggregate(ref _diagRefreshLayoutForStateMutationCallCount);

        if (NeedsMeasure)
        {
            var invalidateVisualStartTicks = Stopwatch.GetTimestamp();
            InvalidateVisual();
            AddAggregate(ref _diagRefreshLayoutVisualInvalidationElapsedTicks, Stopwatch.GetTimestamp() - invalidateVisualStartTicks);
            _runtimeRefreshLayoutNeedsMeasureFallbackCount++;
            IncrementAggregate(ref _diagRefreshLayoutNeedsMeasureFallbackCount);
            AddAggregate(ref _diagRefreshLayoutNeedsMeasureFallbackElapsedTicks, Stopwatch.GetTimestamp() - startTicks);
            _runtimeRefreshLayoutForStateMutationElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            AddAggregate(ref _diagRefreshLayoutForStateMutationElapsedTicks, Stopwatch.GetTimestamp() - startTicks);
            return;
        }

        var captureStartTicks = Stopwatch.GetTimestamp();
        var before = CaptureRenderMutationSnapshot();
        AddAggregate(ref _diagRefreshLayoutCaptureSnapshotElapsedTicks, Stopwatch.GetTimestamp() - captureStartTicks);

        var invalidateArrangeStartTicks = Stopwatch.GetTimestamp();
        InvalidateArrangeForDirectLayoutOnly(invalidateRender: false);
        AddAggregate(ref _diagRefreshLayoutInvalidateArrangeElapsedTicks, Stopwatch.GetTimestamp() - invalidateArrangeStartTicks);

        var arrangeStartTicks = Stopwatch.GetTimestamp();
        Arrange(LayoutSlot);
        AddAggregate(ref _diagRefreshLayoutArrangeElapsedTicks, Stopwatch.GetTimestamp() - arrangeStartTicks);

        captureStartTicks = Stopwatch.GetTimestamp();
        var after = CaptureRenderMutationSnapshot();
        AddAggregate(ref _diagRefreshLayoutCaptureSnapshotElapsedTicks, Stopwatch.GetTimestamp() - captureStartTicks);

        var dirtyBoundsStartTicks = Stopwatch.GetTimestamp();
        if (TryBuildRenderMutationDirtyBounds(before, after, out var dirtyBounds))
        {
            AddAggregate(ref _diagRefreshLayoutDirtyBoundsElapsedTicks, Stopwatch.GetTimestamp() - dirtyBoundsStartTicks);
            var invalidateVisualStartTicks = Stopwatch.GetTimestamp();
            InvalidateVisualWithDirtyBoundsHint(dirtyBounds);
            AddAggregate(ref _diagRefreshLayoutVisualInvalidationElapsedTicks, Stopwatch.GetTimestamp() - invalidateVisualStartTicks);
            _runtimeRefreshLayoutDirtyBoundsHintCount++;
            IncrementAggregate(ref _diagRefreshLayoutDirtyBoundsHintCount);
            _runtimeRefreshLayoutForStateMutationElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            AddAggregate(ref _diagRefreshLayoutForStateMutationElapsedTicks, Stopwatch.GetTimestamp() - startTicks);
            return;
        }

        AddAggregate(ref _diagRefreshLayoutDirtyBoundsElapsedTicks, Stopwatch.GetTimestamp() - dirtyBoundsStartTicks);
        var fallbackInvalidateVisualStartTicks = Stopwatch.GetTimestamp();
        InvalidateVisual();
        AddAggregate(ref _diagRefreshLayoutVisualInvalidationElapsedTicks, Stopwatch.GetTimestamp() - fallbackInvalidateVisualStartTicks);
        _runtimeRefreshLayoutVisualFallbackCount++;
        IncrementAggregate(ref _diagRefreshLayoutVisualFallbackCount);
        _runtimeRefreshLayoutForStateMutationElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        AddAggregate(ref _diagRefreshLayoutForStateMutationElapsedTicks, Stopwatch.GetTimestamp() - startTicks);
    }

    private bool ShouldDeferStateMutationLayoutRefresh()
    {
        if (IsMeasuring || IsArrangingOverride)
        {
            return true;
        }

        for (var current = VisualParent as FrameworkElement ?? LogicalParent as FrameworkElement;
             current != null;
             current = current.VisualParent as FrameworkElement ?? current.LogicalParent as FrameworkElement)
        {
            if (current.IsMeasuring || current.IsArrangingOverride)
            {
                return true;
            }
        }

        return false;
    }

    private void InvalidateVisualWithDirtyBoundsHint(LayoutRect dirtyBounds)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeInvalidateVisualWithDirtyBoundsHintCallCount++;
        IncrementAggregate(ref _diagInvalidateVisualWithDirtyBoundsHintCallCount);
        _pendingRenderDirtyBoundsHint = NormalizeRect(dirtyBounds);
        _hasPendingRenderDirtyBoundsHint = true;
        _preserveRenderDirtyBoundsHint = true;
        try
        {
            base.InvalidateVisual();
        }
        finally
        {
            _preserveRenderDirtyBoundsHint = false;
        }

        _runtimeInvalidateVisualWithDirtyBoundsHintElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        AddAggregate(ref _diagInvalidateVisualWithDirtyBoundsHintElapsedTicks, Stopwatch.GetTimestamp() - startTicks);
    }

    private TrackRenderMutationSnapshot CaptureRenderMutationSnapshot()
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeCaptureRenderMutationSnapshotCallCount++;
        IncrementAggregate(ref _diagCaptureRenderMutationSnapshotCallCount);
        ResolveParts(out var decreaseButton, out var thumb, out var increaseButton);
        var snapshot = new TrackRenderMutationSnapshot(
            _trackRect,
            decreaseButton?.LayoutSlot ?? default,
            decreaseButton != null,
            thumb?.LayoutSlot ?? default,
            thumb != null,
            increaseButton?.LayoutSlot ?? default,
            increaseButton != null);
        _runtimeCaptureRenderMutationSnapshotElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        AddAggregate(ref _diagCaptureRenderMutationSnapshotElapsedTicks, Stopwatch.GetTimestamp() - startTicks);
        return snapshot;
    }

    private bool TryBuildRenderMutationDirtyBounds(
        TrackRenderMutationSnapshot before,
        TrackRenderMutationSnapshot after,
        out LayoutRect dirtyBounds)
    {
        _runtimeTryBuildRenderMutationDirtyBoundsCallCount++;
        IncrementAggregate(ref _diagTryBuildRenderMutationDirtyBoundsCallCount);
        var hasDirtyBounds = false;
        dirtyBounds = default;

        if (!AreRectsClose(before.TrackRect, after.TrackRect))
        {
            _runtimeTryBuildRenderMutationDirtyBoundsTrackChangedCount++;
            IncrementAggregate(ref _diagTryBuildRenderMutationDirtyBoundsTrackChangedCount);
            AddRectToUnion(before.TrackRect, ref hasDirtyBounds, ref dirtyBounds);
            AddRectToUnion(after.TrackRect, ref hasDirtyBounds, ref dirtyBounds);
        }

        var hadDirtyBoundsBeforeParts = hasDirtyBounds;
        AddPartBoundsIfChanged(before.HasDecreasePart, before.DecreasePartRect, after.HasDecreasePart, after.DecreasePartRect, ref hasDirtyBounds, ref dirtyBounds);
        AddPartBoundsIfChanged(before.HasThumbPart, before.ThumbPartRect, after.HasThumbPart, after.ThumbPartRect, ref hasDirtyBounds, ref dirtyBounds);
        AddPartBoundsIfChanged(before.HasIncreasePart, before.IncreasePartRect, after.HasIncreasePart, after.IncreasePartRect, ref hasDirtyBounds, ref dirtyBounds);
        if (!hadDirtyBoundsBeforeParts && hasDirtyBounds)
        {
            _runtimeTryBuildRenderMutationDirtyBoundsPartChangedCount++;
            IncrementAggregate(ref _diagTryBuildRenderMutationDirtyBoundsPartChangedCount);
        }

        if (!hasDirtyBounds)
        {
            AddRectToUnion(after.TrackRect, ref hasDirtyBounds, ref dirtyBounds);
        }

        if (hasDirtyBounds)
        {
            _runtimeTryBuildRenderMutationDirtyBoundsBuiltCount++;
            IncrementAggregate(ref _diagTryBuildRenderMutationDirtyBoundsBuiltCount);
        }

        return hasDirtyBounds;
    }

    private static void AddPartBoundsIfChanged(
        bool hasBefore,
        LayoutRect beforeRect,
        bool hasAfter,
        LayoutRect afterRect,
        ref bool hasDirtyBounds,
        ref LayoutRect dirtyBounds)
    {
        if (hasBefore == hasAfter &&
            (!hasBefore || AreRectsClose(beforeRect, afterRect)))
        {
            return;
        }

        if (hasBefore)
        {
            AddRectToUnion(beforeRect, ref hasDirtyBounds, ref dirtyBounds);
        }

        if (hasAfter)
        {
            AddRectToUnion(afterRect, ref hasDirtyBounds, ref dirtyBounds);
        }
    }

    private static void AddRectToUnion(LayoutRect rect, ref bool hasDirtyBounds, ref LayoutRect dirtyBounds)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        if (!hasDirtyBounds)
        {
            dirtyBounds = rect;
            hasDirtyBounds = true;
            return;
        }

        dirtyBounds = UnionRects(dirtyBounds, rect);
    }

    private static LayoutRect NormalizeRect(LayoutRect rect)
    {
        var x = rect.X;
        var y = rect.Y;
        var width = rect.Width;
        var height = rect.Height;
        if (width < 0f)
        {
            x += width;
            width = -width;
        }

        if (height < 0f)
        {
            y += height;
            height = -height;
        }

        var right = x + width;
        var bottom = y + height;
        var snappedX = MathF.Floor(x);
        var snappedY = MathF.Floor(y);
        var snappedRight = MathF.Ceiling(right);
        var snappedBottom = MathF.Ceiling(bottom);
        return new LayoutRect(
            snappedX,
            snappedY,
            MathF.Max(0f, snappedRight - snappedX),
            MathF.Max(0f, snappedBottom - snappedY));
    }

    private static LayoutRect UnionRects(LayoutRect left, LayoutRect right)
    {
        var x = MathF.Min(left.X, right.X);
        var y = MathF.Min(left.Y, right.Y);
        var rightEdge = MathF.Max(left.X + left.Width, right.X + right.Width);
        var bottomEdge = MathF.Max(left.Y + left.Height, right.Y + right.Height);
        return new LayoutRect(x, y, MathF.Max(0f, rightEdge - x), MathF.Max(0f, bottomEdge - y));
    }

    private static bool AreRectsClose(LayoutRect left, LayoutRect right)
    {
        const float epsilon = 0.0001f;
        return MathF.Abs(left.X - right.X) <= epsilon &&
               MathF.Abs(left.Y - right.Y) <= epsilon &&
               MathF.Abs(left.Width - right.Width) <= epsilon &&
               MathF.Abs(left.Height - right.Height) <= epsilon;
    }

    private static void IncrementAggregate(ref long counter)
    {
        Interlocked.Increment(ref counter);
    }

    private static void AddAggregate(ref long counter, long value)
    {
        if (value != 0)
        {
            Interlocked.Add(ref counter, value);
        }
    }

    private static long ReadAggregate(ref long counter)
    {
        return Interlocked.Read(ref counter);
    }

    private static long ResetAggregate(ref long counter)
    {
        return Interlocked.Exchange(ref counter, 0L);
    }

    private static long ReadOrReset(ref long counter, bool reset)
    {
        return reset ? ResetAggregate(ref counter) : ReadAggregate(ref counter);
    }

    private static void RecordAggregateElapsed(ref long callCount, ref long elapsedTicks, long startTicks)
    {
        IncrementAggregate(ref callCount);
        AddAggregate(ref elapsedTicks, Stopwatch.GetTimestamp() - startTicks);
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }

    private readonly record struct TrackRenderMutationSnapshot(
        LayoutRect TrackRect,
        LayoutRect DecreasePartRect,
        bool HasDecreasePart,
        LayoutRect ThumbPartRect,
        bool HasThumbPart,
        LayoutRect IncreasePartRect,
        bool HasIncreasePart);
}


