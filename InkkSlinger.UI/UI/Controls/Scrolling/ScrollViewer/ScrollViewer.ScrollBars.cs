using System;
using System.Collections.Generic;
using System.Diagnostics;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class ScrollViewer
{
    private void SyncInternalScrollBarParents()
    {
        SyncInternalScrollBarParent(_horizontalBar);
        SyncInternalScrollBarParent(_verticalBar);
    }

    private void SyncInternalScrollBarParent(ScrollBar scrollBar)
    {
        if (!ReferenceEquals(scrollBar.VisualParent, this))
        {
            scrollBar.SetVisualParent(this);
        }

        if (!ReferenceEquals(scrollBar.LogicalParent, this))
        {
            scrollBar.SetLogicalParent(this);
        }

        if (IsLoaded && !scrollBar.IsLoaded)
        {
            scrollBar.RaiseLoaded();
        }
    }

    private Color ResolveBackgroundColor()
    {
        if (GetValueSource(BackgroundProperty) != DependencyPropertyValueSource.Default)
        {
            return GetValue<Color>(BackgroundProperty);
        }

        if (GetValueSource(Control.BackgroundProperty) != DependencyPropertyValueSource.Default)
        {
            return GetValue<Color>(Control.BackgroundProperty);
        }

        return GetValue<Color>(BackgroundProperty);
    }

    private Color ResolveBorderBrushColor()
    {
        if (GetValueSource(BorderBrushProperty) != DependencyPropertyValueSource.Default)
        {
            return GetValue<Color>(BorderBrushProperty);
        }

        if (GetValueSource(Control.BorderBrushProperty) != DependencyPropertyValueSource.Default)
        {
            return GetValue<Color>(Control.BorderBrushProperty);
        }

        return GetValue<Color>(BorderBrushProperty);
    }

    private float ResolveBorderThicknessValue()
    {
        if (GetValueSource(BorderThicknessProperty) != DependencyPropertyValueSource.Default)
        {
            return GetValue<float>(BorderThicknessProperty);
        }

        if (GetValueSource(Control.BorderThicknessProperty) != DependencyPropertyValueSource.Default)
        {
            var thickness = GetValue<Thickness>(Control.BorderThicknessProperty);
            return MathF.Max(MathF.Max(thickness.Left, thickness.Right), MathF.Max(thickness.Top, thickness.Bottom));
        }

        return GetValue<float>(BorderThicknessProperty);
    }

    private void WriteOffsetProperties(float horizontalOffset, float verticalOffset)
    {
        _suppressOffsetPropertyChange = true;
        try
        {
            SetIfChanged(HorizontalOffsetProperty, horizontalOffset);
            SetIfChanged(VerticalOffsetProperty, verticalOffset);
        }
        finally
        {
            _suppressOffsetPropertyChange = false;
        }
    }

    private static object CoerceOffsetValue(DependencyObject dependencyObject, object? value, bool horizontalAxis)
    {
        if (dependencyObject is not ScrollViewer viewer)
        {
            return value is float numeric && float.IsFinite(numeric)
                ? MathF.Max(0f, numeric)
                : 0f;
        }

        var fallback = horizontalAxis ? viewer.HorizontalOffset : viewer.VerticalOffset;
        var max = MathF.Max(
            0f,
            horizontalAxis
                ? viewer.ExtentWidth - viewer.ViewportWidth
                : viewer.ExtentHeight - viewer.ViewportHeight);
        var candidate = value is float numericValue ? numericValue : fallback;
        return ClampOffsetCandidate(candidate, max, fallback);
    }

    private static float ClampOffsetCandidate(float candidate, float maxOffset, float fallback)
    {
        if (!float.IsFinite(candidate))
        {
            candidate = fallback;
        }

        if (!float.IsFinite(candidate))
        {
            return 0f;
        }

        return Math.Clamp(candidate, 0f, MathF.Max(0f, maxOffset));
    }

    private void UpdateScrollBars()
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeUpdateScrollBarsCallCount++;
        EnsureInternalScrollBarsLoaded();
        var desiredHorizontalViewportSize = ViewportWidth;
        var desiredVerticalViewportSize = ViewportHeight;
        var desiredHorizontalMaximum = ExtentWidth;
        var desiredVerticalMaximum = ExtentHeight;
        var desiredHorizontalLargeChange = MathF.Max(1f, ViewportWidth);
        var desiredVerticalLargeChange = MathF.Max(1f, ViewportHeight);
        var desiredHorizontalValue = HorizontalOffset;
        var desiredVerticalValue = VerticalOffset;
        if (HasSynchronizedScrollBarState(
                desiredHorizontalViewportSize,
                desiredVerticalViewportSize,
                desiredHorizontalMaximum,
                desiredVerticalMaximum,
                desiredHorizontalLargeChange,
                desiredVerticalLargeChange,
                desiredHorizontalValue,
                desiredVerticalValue))
        {
            _runtimeUpdateScrollBarsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            _diagUpdateScrollBarsCallCount++;
            _diagUpdateScrollBarsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            return;
        }

        _suppressInternalScrollBarValueChange = true;
        try
        {
            SetIfChanged(ScrollBar.ViewportSizeProperty, _horizontalBar, desiredHorizontalViewportSize);
            SetIfChanged(ScrollBar.ViewportSizeProperty, _verticalBar, desiredVerticalViewportSize);
            SetIfChanged(ScrollBar.MinimumProperty, _horizontalBar, 0f);
            SetIfChanged(ScrollBar.MinimumProperty, _verticalBar, 0f);
            SetIfChanged(ScrollBar.MaximumProperty, _horizontalBar, desiredHorizontalMaximum);
            SetIfChanged(ScrollBar.MaximumProperty, _verticalBar, desiredVerticalMaximum);
            SetIfChanged(ScrollBar.SmallChangeProperty, _horizontalBar, DefaultLineScrollStep);
            SetIfChanged(ScrollBar.SmallChangeProperty, _verticalBar, DefaultLineScrollStep);
            SetIfChanged(ScrollBar.LargeChangeProperty, _horizontalBar, desiredHorizontalLargeChange);
            SetIfChanged(ScrollBar.LargeChangeProperty, _verticalBar, desiredVerticalLargeChange);

            var applyHorizontalDragValue = _horizontalBar.IsThumbDragInProgress;
            var applyVerticalDragValue = _verticalBar.IsThumbDragInProgress;
            if (applyHorizontalDragValue)
            {
                desiredHorizontalValue = _horizontalBar.GetActiveThumbDragValue();
            }

            if (applyVerticalDragValue)
            {
                desiredVerticalValue = _verticalBar.GetActiveThumbDragValue();
            }

            SetIfChanged(ScrollBar.ValueProperty, _horizontalBar, desiredHorizontalValue);
            SetIfChanged(ScrollBar.ValueProperty, _verticalBar, desiredVerticalValue);
            CacheSynchronizedScrollBarState(
                desiredHorizontalViewportSize,
                desiredVerticalViewportSize,
                desiredHorizontalMaximum,
                desiredVerticalMaximum,
                desiredHorizontalLargeChange,
                desiredVerticalLargeChange,
                desiredHorizontalValue,
                desiredVerticalValue);

            if (applyHorizontalDragValue || applyVerticalDragValue)
            {
                SetOffsets(
                    applyHorizontalDragValue ? desiredHorizontalValue : HorizontalOffset,
                    applyVerticalDragValue ? desiredVerticalValue : VerticalOffset,
                    applyVerticalDragValue ? ScrollOffsetUpdateSource.VerticalScrollBar : ScrollOffsetUpdateSource.HorizontalScrollBar);
            }
        }
        finally
        {
                _runtimeUpdateScrollBarsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            _suppressInternalScrollBarValueChange = false;
        }
        _diagUpdateScrollBarsCallCount++;
        _diagUpdateScrollBarsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
    }

    private void EnsureInternalScrollBarsLoaded()
    {
        if (IsLoaded && !_horizontalBar.IsLoaded)
        {
            _horizontalBar.RaiseLoaded();
        }

        if (IsLoaded && !_verticalBar.IsLoaded)
        {
            _verticalBar.RaiseLoaded();
        }
    }

    private void UnloadInternalScrollBars()
    {
        if (_horizontalBar.IsLoaded)
        {
            _horizontalBar.RaiseUnloaded();
        }

        if (_verticalBar.IsLoaded)
        {
            _verticalBar.RaiseUnloaded();
        }
    }

    private float ResolveHorizontalBarThicknessForLayout()
    {
        return MathF.Max(8f, ScrollBarThickness);
    }

    private float ResolveVerticalBarThicknessForLayout()
    {
        return MathF.Max(8f, ScrollBarThickness);
    }

    private void SyncInternalScrollBarLayoutDimensions(float horizontalBarThickness, float verticalBarThickness)
    {
        SetIfChanged(FrameworkElement.HeightProperty, _horizontalBar, horizontalBarThickness);
        SetIfChanged(FrameworkElement.WidthProperty, _verticalBar, verticalBarThickness);
    }

    private void UpdateScrollBarValues()
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagUpdateScrollBarValuesCallCount++;
        _runtimeUpdateScrollBarValuesCallCount++;
        _suppressInternalScrollBarValueChange = true;
        try
        {
            SetIfChanged(ScrollBar.ValueProperty, _horizontalBar, HorizontalOffset);
            SetIfChanged(ScrollBar.ValueProperty, _verticalBar, VerticalOffset);
        }
        finally
        {
            _suppressInternalScrollBarValueChange = false;
        }

        _diagUpdateScrollBarValuesElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _runtimeUpdateScrollBarValuesElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
    }

    private void UpdateHorizontalScrollBarValue()
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagUpdateHorizontalScrollBarValueCallCount++;
        _runtimeUpdateHorizontalScrollBarValueCallCount++;
        _suppressInternalScrollBarValueChange = true;
        try
        {
            SetIfChanged(ScrollBar.ValueProperty, _horizontalBar, HorizontalOffset);
        }
        finally
        {
            _suppressInternalScrollBarValueChange = false;
        }

        _diagUpdateHorizontalScrollBarValueElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _runtimeUpdateHorizontalScrollBarValueElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
    }

    private void UpdateVerticalScrollBarValue()
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagUpdateVerticalScrollBarValueCallCount++;
        _runtimeUpdateVerticalScrollBarValueCallCount++;
        _suppressInternalScrollBarValueChange = true;
        try
        {
            SetIfChanged(ScrollBar.ValueProperty, _verticalBar, VerticalOffset);
        }
        finally
        {
            _suppressInternalScrollBarValueChange = false;
        }

        _diagUpdateVerticalScrollBarValueElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _runtimeUpdateVerticalScrollBarValueElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
    }

    private void OnHorizontalScrollBarValueChanged(object? sender, RoutedSimpleEventArgs args)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _ = sender;
        _ = args;
        if (_suppressInternalScrollBarValueChange)
        {
            _diagHorizontalValueChangedSuppressedCount++;
            _runtimeHorizontalValueChangedSuppressedCount++;
            _diagHorizontalValueChangedElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            _runtimeHorizontalValueChangedElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            return;
        }

        var setOffsetsStartTicks = Stopwatch.GetTimestamp();
        BeginInputScrollMutation();
        try
        {
            SetOffsets(_horizontalBar.Value, VerticalOffset, ScrollOffsetUpdateSource.HorizontalScrollBar);
        }
        finally
        {
            EndInputScrollMutation();
        }

        _diagHorizontalValueChangedSetOffsetsElapsedTicks += Stopwatch.GetTimestamp() - setOffsetsStartTicks;
        _runtimeHorizontalValueChangedSetOffsetsElapsedTicks += Stopwatch.GetTimestamp() - setOffsetsStartTicks;
        _diagHorizontalValueChangedCallCount++;
        _runtimeHorizontalValueChangedCallCount++;
        _diagHorizontalValueChangedElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _runtimeHorizontalValueChangedElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
    }

    private void OnVerticalScrollBarValueChanged(object? sender, RoutedSimpleEventArgs args)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _ = sender;
        _ = args;
        if (_suppressInternalScrollBarValueChange)
        {
            _diagVerticalValueChangedSuppressedCount++;
            _runtimeVerticalValueChangedSuppressedCount++;
            _diagVerticalValueChangedElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            _runtimeVerticalValueChangedElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            return;
        }

        var setOffsetsStartTicks = Stopwatch.GetTimestamp();
        BeginInputScrollMutation();
        try
        {
            SetOffsets(HorizontalOffset, _verticalBar.Value, ScrollOffsetUpdateSource.VerticalScrollBar);
        }
        finally
        {
            EndInputScrollMutation();
        }
        _diagVerticalValueChangedSetOffsetsElapsedTicks += Stopwatch.GetTimestamp() - setOffsetsStartTicks;
        _runtimeVerticalValueChangedSetOffsetsElapsedTicks += Stopwatch.GetTimestamp() - setOffsetsStartTicks;
        _diagVerticalValueChangedCallCount++;
        _runtimeVerticalValueChangedCallCount++;
        _diagVerticalValueChangedElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _runtimeVerticalValueChangedElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
    }

    private void SetIfChanged(DependencyProperty property, float value, string? diagnosticsCounterName = null)
    {
        if (AreClose(GetValue<float>(property), value))
        {
            return;
        }

        SetValue(property, value);
    }

    private static void SetIfChanged(DependencyProperty property, ScrollBar scrollBar, float value)
    {
        if (AreClose(scrollBar.GetValue<float>(property), value))
        {
            return;
        }

        scrollBar.SetValue(property, value);
    }

    private static bool AreClose(float left, float right)
    {
        return MathF.Abs(left - right) <= 0.01f;
    }

    private bool HasSynchronizedScrollBarState(
        float horizontalViewportSize,
        float verticalViewportSize,
        float horizontalMaximum,
        float verticalMaximum,
        float horizontalLargeChange,
        float verticalLargeChange,
        float horizontalValue,
        float verticalValue)
    {
        return _hasSyncedScrollBarState &&
               AreClose(_lastSyncedHorizontalViewportSize, horizontalViewportSize) &&
               AreClose(_lastSyncedVerticalViewportSize, verticalViewportSize) &&
               AreClose(_lastSyncedHorizontalMaximum, horizontalMaximum) &&
               AreClose(_lastSyncedVerticalMaximum, verticalMaximum) &&
               AreClose(_lastSyncedHorizontalLargeChange, horizontalLargeChange) &&
               AreClose(_lastSyncedVerticalLargeChange, verticalLargeChange) &&
               AreClose(_lastSyncedHorizontalValue, horizontalValue) &&
               AreClose(_lastSyncedVerticalValue, verticalValue);
    }

    private void CacheSynchronizedScrollBarState(
        float horizontalViewportSize,
        float verticalViewportSize,
        float horizontalMaximum,
        float verticalMaximum,
        float horizontalLargeChange,
        float verticalLargeChange,
        float horizontalValue,
        float verticalValue)
    {
        _hasSyncedScrollBarState = true;
        _lastSyncedHorizontalViewportSize = horizontalViewportSize;
        _lastSyncedVerticalViewportSize = verticalViewportSize;
        _lastSyncedHorizontalMaximum = horizontalMaximum;
        _lastSyncedVerticalMaximum = verticalMaximum;
        _lastSyncedHorizontalLargeChange = horizontalLargeChange;
        _lastSyncedVerticalLargeChange = verticalLargeChange;
        _lastSyncedHorizontalValue = horizontalValue;
        _lastSyncedVerticalValue = verticalValue;
    }

    private static float CoerceViewportMetric(float candidate, float previous, float extent)
    {
        if (float.IsFinite(candidate) && candidate >= 0f)
        {
            return candidate;
        }

        if (float.IsFinite(previous) && previous >= 0f)
        {
            return previous;
        }

        if (float.IsFinite(extent) && extent >= 0f)
        {
            return extent;
        }

        return 0f;
    }

    private static float CoerceNonNegativeFinite(float candidate, float previous)
    {
        if (float.IsFinite(candidate) && candidate >= 0f)
        {
            return candidate;
        }

        if (float.IsFinite(previous) && previous >= 0f)
        {
            return previous;
        }

        return 0f;
    }

    private static float GetVerticalBarReservation(bool showVerticalBar, float barSize)
    {
        if (!showVerticalBar)
        {
            return 0f;
        }

        return barSize + ContentScrollBarGap;
    }

    private static float GetHorizontalBarReservation(bool showHorizontalBar, float barSize)
    {
        if (!showHorizontalBar)
        {
            return 0f;
        }

        return barSize + ContentScrollBarGap;
    }
}
