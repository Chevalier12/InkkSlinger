using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public sealed class ColorPicker : Control, IRenderDirtyBoundsHintProvider
{
    private const float DefaultWidth = 160f;
    private const float DefaultHeight = 120f;
    private const float SelectorRadius = 5f;
    private const float OuterPadding = 2f;

    private static readonly Dictionary<GraphicsDevice, Dictionary<SvTextureCacheKey, Texture2D>> SpectrumTextureCaches = new();
    private static long _telemetryHandlePointerDownCallCount;
    private static long _telemetryHandlePointerDownElapsedTicks;
    private static long _telemetryHandlePointerDownHitCount;
    private static long _telemetryHandlePointerDownMissCount;
    private static long _telemetryHandlePointerMoveCallCount;
    private static long _telemetryHandlePointerMoveElapsedTicks;
    private static long _telemetryHandlePointerMoveDragCount;
    private static long _telemetryHandlePointerMoveIgnoredCount;
    private static long _telemetryHandlePointerUpCallCount;
    private static long _telemetryHandlePointerUpStateChangeCount;
    private static long _telemetrySetMouseOverCallCount;
    private static long _telemetrySetMouseOverStateChangeCount;
    private static long _telemetryUpdateSpectrumFromPointerCallCount;
    private static long _telemetryUpdateSpectrumFromPointerElapsedTicks;
    private static long _telemetryUpdateSpectrumFromPointerZeroRectSkipCount;
    private static long _telemetryRequestSelectedColorSyncCallCount;
    private static long _telemetryRequestSelectedColorSyncDragDeferredCount;
    private static long _telemetryQueueDeferredSelectedColorSyncCallCount;
    private static long _telemetryQueueDeferredSelectedColorSyncAlreadyQueuedCount;
    private static long _telemetryFlushDeferredSelectedColorSyncCallCount;
    private static long _telemetryFlushDeferredSelectedColorSyncNoPendingCount;
    private static long _telemetryFlushDeferredSelectedColorSyncRequeueWhileDraggingCount;
    private static long _telemetryFlushPendingSelectedColorSyncAfterDragCallCount;
    private static long _telemetryFlushPendingSelectedColorSyncAfterDragNoPendingCount;
    private static long _telemetrySyncSelectedColorFromComponentsCallCount;
    private static long _telemetrySyncSelectedColorFromComponentsElapsedTicks;
    private static long _telemetrySyncSelectedColorFromComponentsReentrantSkipCount;
    private static long _telemetrySyncSelectedColorFromComponentsNoOpCount;
    private static long _telemetrySelectedColorChangedCallCount;
    private static long _telemetrySelectedColorChangedExternalSyncCount;
    private static long _telemetrySelectedColorChangedComponentWritebackCount;
    private static long _telemetrySelectedColorChangedRaisedCount;
    private static long _telemetryHueChangedCallCount;
    private static long _telemetrySaturationChangedCallCount;
    private static long _telemetryValueChangedCallCount;
    private static long _telemetryAlphaChangedCallCount;
    private static long _telemetryRenderCallCount;
    private static long _telemetryRenderElapsedTicks;
    private static long _telemetryRenderSkippedZeroRectCount;
    private static long _telemetrySpectrumTextureCacheHitCount;
    private static long _telemetrySpectrumTextureCacheMissCount;
    private static long _telemetrySpectrumTextureBuildCount;
    private static long _telemetrySpectrumTextureBuildElapsedTicks;

    public static readonly RoutedEvent SelectedColorChangedEvent =
        new(nameof(SelectedColorChanged), RoutingStrategy.Bubble);

    public static readonly RoutedEvent HueChangedEvent =
        new(nameof(HueChanged), RoutingStrategy.Bubble);

    public static readonly RoutedEvent SaturationChangedEvent =
        new(nameof(SaturationChanged), RoutingStrategy.Bubble);

    public static readonly RoutedEvent ValueChangedEvent =
        new(nameof(ValueChanged), RoutingStrategy.Bubble);

    public static readonly RoutedEvent AlphaChangedEvent =
        new(nameof(AlphaChanged), RoutingStrategy.Bubble);

    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(
            nameof(SelectedColor),
            typeof(Color),
            typeof(ColorPicker),
            new FrameworkPropertyMetadata(
                new Color(255, 0, 0, 255),
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is ColorPicker picker &&
                        args.OldValue is Color oldColor &&
                        args.NewValue is Color newColor)
                    {
                        picker.OnSelectedColorChanged(oldColor, newColor);
                    }
                }));

    public static readonly DependencyProperty HueProperty =
        DependencyProperty.Register(
            nameof(Hue),
            typeof(float),
            typeof(ColorPicker),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is ColorPicker picker &&
                        args.OldValue is float oldValue &&
                        args.NewValue is float newValue)
                    {
                        picker.OnHueChanged(oldValue, newValue);
                    }
                },
                coerceValueCallback: static (_, value) => value is float numeric ? ColorControlUtilities.NormalizeHue(numeric) : 0f));

    public static readonly DependencyProperty SaturationProperty =
        DependencyProperty.Register(
            nameof(Saturation),
            typeof(float),
            typeof(ColorPicker),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is ColorPicker picker &&
                        args.OldValue is float oldValue &&
                        args.NewValue is float newValue)
                    {
                        picker.OnSaturationChanged(oldValue, newValue);
                    }
                },
                coerceValueCallback: static (_, value) => value is float numeric ? ColorControlUtilities.Clamp01(numeric) : 1f));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(float),
            typeof(ColorPicker),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is ColorPicker picker &&
                        args.OldValue is float oldValue &&
                        args.NewValue is float newValue)
                    {
                        picker.OnValueComponentChanged(oldValue, newValue);
                    }
                },
                coerceValueCallback: static (_, value) => value is float numeric ? ColorControlUtilities.Clamp01(numeric) : 1f));

    public static readonly DependencyProperty AlphaProperty =
        DependencyProperty.Register(
            nameof(Alpha),
            typeof(float),
            typeof(ColorPicker),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is ColorPicker picker &&
                        args.OldValue is float oldValue &&
                        args.NewValue is float newValue)
                    {
                        picker.OnAlphaChanged(oldValue, newValue);
                    }
                },
                coerceValueCallback: static (_, value) => value is float numeric ? ColorControlUtilities.Clamp01(numeric) : 1f));

    private bool _isDragging;
    private bool _isMouseOver;
    private bool _isSynchronizingSelectedColor;
    private bool _isSynchronizingComponents;
    private bool _hasPendingSelectedColorSync;
    private bool _isSelectedColorSyncDeferred;
    private bool _hasPendingRenderDirtyBoundsHint;
    private LayoutRect _pendingRenderDirtyBoundsHint;
    private long _runtimeHandlePointerDownCallCount;
    private long _runtimeHandlePointerDownElapsedTicks;
    private long _runtimeHandlePointerDownHitCount;
    private long _runtimeHandlePointerDownMissCount;
    private long _runtimeHandlePointerMoveCallCount;
    private long _runtimeHandlePointerMoveElapsedTicks;
    private long _runtimeHandlePointerMoveDragCount;
    private long _runtimeHandlePointerMoveIgnoredCount;
    private long _runtimeHandlePointerUpCallCount;
    private long _runtimeHandlePointerUpStateChangeCount;
    private long _runtimeSetMouseOverCallCount;
    private long _runtimeSetMouseOverStateChangeCount;
    private long _runtimeUpdateSpectrumFromPointerCallCount;
    private long _runtimeUpdateSpectrumFromPointerElapsedTicks;
    private long _runtimeUpdateSpectrumFromPointerZeroRectSkipCount;
    private long _runtimeRequestSelectedColorSyncCallCount;
    private long _runtimeRequestSelectedColorSyncDragDeferredCount;
    private long _runtimeQueueDeferredSelectedColorSyncCallCount;
    private long _runtimeQueueDeferredSelectedColorSyncAlreadyQueuedCount;
    private long _runtimeFlushDeferredSelectedColorSyncCallCount;
    private long _runtimeFlushDeferredSelectedColorSyncNoPendingCount;
    private long _runtimeFlushDeferredSelectedColorSyncRequeueWhileDraggingCount;
    private long _runtimeFlushPendingSelectedColorSyncAfterDragCallCount;
    private long _runtimeFlushPendingSelectedColorSyncAfterDragNoPendingCount;
    private long _runtimeSyncSelectedColorFromComponentsCallCount;
    private long _runtimeSyncSelectedColorFromComponentsElapsedTicks;
    private long _runtimeSyncSelectedColorFromComponentsReentrantSkipCount;
    private long _runtimeSyncSelectedColorFromComponentsNoOpCount;
    private long _runtimeSelectedColorChangedCallCount;
    private long _runtimeSelectedColorChangedExternalSyncCount;
    private long _runtimeSelectedColorChangedComponentWritebackCount;
    private long _runtimeSelectedColorChangedRaisedCount;
    private long _runtimeHueChangedCallCount;
    private long _runtimeSaturationChangedCallCount;
    private long _runtimeValueChangedCallCount;
    private long _runtimeAlphaChangedCallCount;
    private long _runtimeRenderCallCount;
    private long _runtimeRenderElapsedTicks;
    private long _runtimeRenderSkippedZeroRectCount;
    private long _runtimeSpectrumTextureCacheHitCount;
    private long _runtimeSpectrumTextureCacheMissCount;
    private long _runtimeSpectrumTextureBuildCount;
    private long _runtimeSpectrumTextureBuildElapsedTicks;

    public ColorPicker()
    {
        Focusable = false;
        AddHandler<MouseRoutedEventArgs>(UIElement.PreviewMouseLeftButtonDownEvent, OnPreviewMouseLeftButtonDownClaimInput);
    }

    public event EventHandler<ColorChangedEventArgs> SelectedColorChanged
    {
        add => AddHandler(SelectedColorChangedEvent, value);
        remove => RemoveHandler(SelectedColorChangedEvent, value);
    }

    public event EventHandler<RoutedSimpleEventArgs> HueChanged
    {
        add => AddHandler(HueChangedEvent, value);
        remove => RemoveHandler(HueChangedEvent, value);
    }

    public event EventHandler<RoutedSimpleEventArgs> SaturationChanged
    {
        add => AddHandler(SaturationChangedEvent, value);
        remove => RemoveHandler(SaturationChangedEvent, value);
    }

    public event EventHandler<RoutedSimpleEventArgs> ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    public event EventHandler<RoutedSimpleEventArgs> AlphaChanged
    {
        add => AddHandler(AlphaChangedEvent, value);
        remove => RemoveHandler(AlphaChangedEvent, value);
    }

    public Color SelectedColor
    {
        get => GetValue<Color>(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public float Hue
    {
        get => GetValue<float>(HueProperty);
        set => SetValue(HueProperty, value);
    }

    public float Saturation
    {
        get => GetValue<float>(SaturationProperty);
        set => SetValue(SaturationProperty, value);
    }

    public float Value
    {
        get => GetValue<float>(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public float Alpha
    {
        get => GetValue<float>(AlphaProperty);
        set => SetValue(AlphaProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        desired.X = MathF.Max(desired.X, DefaultWidth);
        desired.Y = MathF.Max(desired.Y, DefaultHeight);
        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        PrewarmSpectrumTextureIfPossible();
        return arranged;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        _runtimeRenderCallCount++;
        Interlocked.Increment(ref _telemetryRenderCallCount);
        var start = Stopwatch.GetTimestamp();

        var snapshot = GetColorPickerSnapshotForDiagnostics();
        if (snapshot.SpectrumRect.Width <= 0f || snapshot.SpectrumRect.Height <= 0f)
        {
            _runtimeRenderSkippedZeroRectCount++;
            Interlocked.Increment(ref _telemetryRenderSkippedZeroRectCount);
            AccumulateRenderElapsed(start);
            return;
        }

        var spectrumTexture = GetOrCreateSpectrumTexture(
            this,
            spriteBatch.GraphicsDevice,
            Math.Max(1, (int)MathF.Round(snapshot.SpectrumRect.Width)),
            Math.Max(1, (int)MathF.Round(snapshot.SpectrumRect.Height)),
            Hue);
        UiDrawing.DrawTexture(spriteBatch, spectrumTexture, snapshot.SpectrumRect, color: Color.White, opacity: Opacity);

        var outline = _isMouseOver ? new Color(255, 255, 255, 220) : new Color(24, 24, 24, 220);
        UiDrawing.DrawRectStroke(spriteBatch, snapshot.SpectrumRect, 1f, outline, Opacity);
        DrawSvSelector(spriteBatch, snapshot.SaturationSelector);
        AccumulateRenderElapsed(start);
    }

    internal bool HandlePointerDownFromInput(Vector2 pointerPosition)
    {
        _runtimeHandlePointerDownCallCount++;
        Interlocked.Increment(ref _telemetryHandlePointerDownCallCount);
        var start = Stopwatch.GetTimestamp();

        var snapshot = GetColorPickerSnapshotForDiagnostics();
        if (!IsEnabled)
        {
            _runtimeHandlePointerDownMissCount++;
            Interlocked.Increment(ref _telemetryHandlePointerDownMissCount);
            AccumulateHandlePointerDownElapsed(start);
            return false;
        }

        if (Contains(snapshot.SpectrumRect, pointerPosition))
        {
            _isDragging = true;
            _runtimeHandlePointerDownHitCount++;
            Interlocked.Increment(ref _telemetryHandlePointerDownHitCount);
            UpdateSpectrumFromPointer(pointerPosition, snapshot.SpectrumRect);
            AccumulateHandlePointerDownElapsed(start);
            return true;
        }

        _runtimeHandlePointerDownMissCount++;
        Interlocked.Increment(ref _telemetryHandlePointerDownMissCount);
        AccumulateHandlePointerDownElapsed(start);
        return false;
    }

    private void OnPreviewMouseLeftButtonDownClaimInput(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        if (!IsEnabled || args.Button != MouseButton.Left || args.Handled)
        {
            return;
        }

        var snapshot = GetColorPickerSnapshotForDiagnostics();
        if (Contains(snapshot.SpectrumRect, args.Position))
        {
            args.Handled = true;
        }
    }

    internal void HandlePointerMoveFromInput(Vector2 pointerPosition)
    {
        _runtimeHandlePointerMoveCallCount++;
        Interlocked.Increment(ref _telemetryHandlePointerMoveCallCount);
        var start = Stopwatch.GetTimestamp();

        if (_isDragging)
        {
            _runtimeHandlePointerMoveDragCount++;
            Interlocked.Increment(ref _telemetryHandlePointerMoveDragCount);
            UpdateSpectrumFromPointer(pointerPosition, GetColorPickerSnapshotForDiagnostics().SpectrumRect);
        }
        else
        {
            _runtimeHandlePointerMoveIgnoredCount++;
            Interlocked.Increment(ref _telemetryHandlePointerMoveIgnoredCount);
        }

        var elapsed = Stopwatch.GetTimestamp() - start;
        _runtimeHandlePointerMoveElapsedTicks += elapsed;
        Interlocked.Add(ref _telemetryHandlePointerMoveElapsedTicks, elapsed);
    }

    internal void HandlePointerUpFromInput()
    {
        _runtimeHandlePointerUpCallCount++;
        Interlocked.Increment(ref _telemetryHandlePointerUpCallCount);
        var wasDragging = _isDragging;
        if (_isDragging)
        {
            _runtimeHandlePointerUpStateChangeCount++;
            Interlocked.Increment(ref _telemetryHandlePointerUpStateChangeCount);
        }

        _isDragging = false;
        if (wasDragging)
        {
            FlushPendingSelectedColorSyncAfterDrag();
        }
    }

    internal void SetMouseOverFromInput(bool isMouseOver)
    {
        _runtimeSetMouseOverCallCount++;
        Interlocked.Increment(ref _telemetrySetMouseOverCallCount);
        if (_isMouseOver == isMouseOver)
        {
            return;
        }

        _isMouseOver = isMouseOver;
        _runtimeSetMouseOverStateChangeCount++;
        Interlocked.Increment(ref _telemetrySetMouseOverStateChangeCount);
        InvalidateVisual();
    }

    internal ColorPickerRuntimeDiagnosticsSnapshot GetColorPickerSnapshotForDiagnostics()
    {
        var slot = LayoutSlot;
        var contentX = slot.X + OuterPadding;
        var contentY = slot.Y + OuterPadding;
        var contentWidth = MathF.Max(0f, slot.Width - (OuterPadding * 2f));
        var contentHeight = MathF.Max(0f, slot.Height - (OuterPadding * 2f));
        var spectrumRect = new LayoutRect(contentX, contentY, contentWidth, contentHeight);
        var saturationSelector = new Vector2(
            spectrumRect.X + (spectrumRect.Width * Saturation),
            spectrumRect.Y + (spectrumRect.Height * (1f - Value)));
        return new ColorPickerRuntimeDiagnosticsSnapshot(
            spectrumRect,
            saturationSelector,
            SelectorRadius,
            SelectedColor,
            Hue,
            Saturation,
            Value,
            Alpha,
            _isDragging,
            _isMouseOver,
            _hasPendingSelectedColorSync,
            _isSelectedColorSyncDeferred,
            _isSynchronizingSelectedColor,
            _isSynchronizingComponents,
            _runtimeHandlePointerDownCallCount,
            ToMilliseconds(_runtimeHandlePointerDownElapsedTicks),
            _runtimeHandlePointerDownHitCount,
            _runtimeHandlePointerDownMissCount,
            _runtimeHandlePointerMoveCallCount,
            ToMilliseconds(_runtimeHandlePointerMoveElapsedTicks),
            _runtimeHandlePointerMoveDragCount,
            _runtimeHandlePointerMoveIgnoredCount,
            _runtimeHandlePointerUpCallCount,
            _runtimeHandlePointerUpStateChangeCount,
            _runtimeSetMouseOverCallCount,
            _runtimeSetMouseOverStateChangeCount,
            _runtimeUpdateSpectrumFromPointerCallCount,
            ToMilliseconds(_runtimeUpdateSpectrumFromPointerElapsedTicks),
            _runtimeUpdateSpectrumFromPointerZeroRectSkipCount,
            _runtimeRequestSelectedColorSyncCallCount,
            _runtimeRequestSelectedColorSyncDragDeferredCount,
            _runtimeQueueDeferredSelectedColorSyncCallCount,
            _runtimeQueueDeferredSelectedColorSyncAlreadyQueuedCount,
            _runtimeFlushDeferredSelectedColorSyncCallCount,
            _runtimeFlushDeferredSelectedColorSyncNoPendingCount,
            _runtimeFlushDeferredSelectedColorSyncRequeueWhileDraggingCount,
            _runtimeFlushPendingSelectedColorSyncAfterDragCallCount,
            _runtimeFlushPendingSelectedColorSyncAfterDragNoPendingCount,
            _runtimeSyncSelectedColorFromComponentsCallCount,
            ToMilliseconds(_runtimeSyncSelectedColorFromComponentsElapsedTicks),
            _runtimeSyncSelectedColorFromComponentsReentrantSkipCount,
            _runtimeSyncSelectedColorFromComponentsNoOpCount,
            _runtimeSelectedColorChangedCallCount,
            _runtimeSelectedColorChangedExternalSyncCount,
            _runtimeSelectedColorChangedComponentWritebackCount,
            _runtimeSelectedColorChangedRaisedCount,
            _runtimeHueChangedCallCount,
            _runtimeSaturationChangedCallCount,
            _runtimeValueChangedCallCount,
            _runtimeAlphaChangedCallCount,
            _runtimeRenderCallCount,
            ToMilliseconds(_runtimeRenderElapsedTicks),
            _runtimeRenderSkippedZeroRectCount,
            _runtimeSpectrumTextureCacheHitCount,
            _runtimeSpectrumTextureCacheMissCount,
            _runtimeSpectrumTextureBuildCount,
            ToMilliseconds(_runtimeSpectrumTextureBuildElapsedTicks));
    }

    internal static new ColorPickerTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return CreateTelemetrySnapshot(reset: false);
    }

    internal static new ColorPickerTelemetrySnapshot GetTelemetryAndReset()
    {
        return CreateTelemetrySnapshot(reset: true);
    }

    private void OnSelectedColorChanged(Color oldColor, Color newColor)
    {
        _runtimeSelectedColorChangedCallCount++;
        Interlocked.Increment(ref _telemetrySelectedColorChangedCallCount);

        if (!_isSynchronizingComponents && oldColor != newColor)
        {
            _runtimeSelectedColorChangedExternalSyncCount++;
            Interlocked.Increment(ref _telemetrySelectedColorChangedExternalSyncCount);
            _isSynchronizingSelectedColor = true;
            try
            {
                ColorControlUtilities.ToHsva(newColor, out var hue, out var saturation, out var value, out var alpha);
                SetValue(HueProperty, hue);
                SetValue(SaturationProperty, saturation);
                SetValue(ValueProperty, value);
                SetValue(AlphaProperty, alpha);
            }
            finally
            {
                _isSynchronizingSelectedColor = false;
            }
        }
        else if (_isSynchronizingComponents && oldColor != newColor)
        {
            _runtimeSelectedColorChangedComponentWritebackCount++;
            Interlocked.Increment(ref _telemetrySelectedColorChangedComponentWritebackCount);
        }

        if (oldColor != newColor)
        {
            _runtimeSelectedColorChangedRaisedCount++;
            Interlocked.Increment(ref _telemetrySelectedColorChangedRaisedCount);
            RaiseRoutedEvent(SelectedColorChangedEvent, new ColorChangedEventArgs(SelectedColorChangedEvent, oldColor, newColor));
        }
    }

    private void OnHueChanged(float oldValue, float newValue)
    {
        _runtimeHueChangedCallCount++;
        Interlocked.Increment(ref _telemetryHueChangedCallCount);
        if (!AreClose(oldValue, newValue))
        {
            if (!_isSynchronizingSelectedColor)
            {
                RequestSelectedColorSync();
            }

            RaiseRoutedEvent(HueChangedEvent, new RoutedSimpleEventArgs(HueChangedEvent));
        }
    }

    private void OnSaturationChanged(float oldValue, float newValue)
    {
        _runtimeSaturationChangedCallCount++;
        Interlocked.Increment(ref _telemetrySaturationChangedCallCount);
        if (!AreClose(oldValue, newValue))
        {
            if (!_isSynchronizingSelectedColor)
            {
                RequestSelectedColorSync();
            }

            RaiseRoutedEvent(SaturationChangedEvent, new RoutedSimpleEventArgs(SaturationChangedEvent));
        }
    }

    private void OnValueComponentChanged(float oldValue, float newValue)
    {
        _runtimeValueChangedCallCount++;
        Interlocked.Increment(ref _telemetryValueChangedCallCount);
        if (!AreClose(oldValue, newValue))
        {
            if (!_isSynchronizingSelectedColor)
            {
                RequestSelectedColorSync();
            }

            RaiseRoutedEvent(ValueChangedEvent, new RoutedSimpleEventArgs(ValueChangedEvent));
        }
    }

    private void OnAlphaChanged(float oldValue, float newValue)
    {
        _runtimeAlphaChangedCallCount++;
        Interlocked.Increment(ref _telemetryAlphaChangedCallCount);
        if (!AreClose(oldValue, newValue))
        {
            if (!_isSynchronizingSelectedColor)
            {
                RequestSelectedColorSync();
            }

            RaiseRoutedEvent(AlphaChangedEvent, new RoutedSimpleEventArgs(AlphaChangedEvent));
        }
    }

    protected override bool ShouldInvalidateVisualForPropertyChange(
        DependencyPropertyChangedEventArgs args,
        FrameworkPropertyMetadata metadata)
    {
        if (!base.ShouldInvalidateVisualForPropertyChange(args, metadata))
        {
            return false;
        }

        if (args.Property == SelectedColorProperty || args.Property == AlphaProperty)
        {
            return false;
        }

        if ((args.Property == SaturationProperty || args.Property == ValueProperty) &&
            TryBuildLocalizedSelectorDirtyBounds(args, out var selectorDirtyBounds))
        {
            PrimeRenderDirtyBoundsHint(selectorDirtyBounds);
        }

        return true;
    }

    bool IRenderDirtyBoundsHintProvider.TryConsumeRenderDirtyBoundsHint(out LayoutRect bounds)
    {
        if (!_hasPendingRenderDirtyBoundsHint)
        {
            bounds = default;
            return false;
        }

        bounds = _pendingRenderDirtyBoundsHint;
        _hasPendingRenderDirtyBoundsHint = false;
        return true;
    }

    private void RequestSelectedColorSync()
    {
        _runtimeRequestSelectedColorSyncCallCount++;
        Interlocked.Increment(ref _telemetryRequestSelectedColorSyncCallCount);
        _hasPendingSelectedColorSync = true;
        if (_isDragging)
        {
            _runtimeRequestSelectedColorSyncDragDeferredCount++;
            Interlocked.Increment(ref _telemetryRequestSelectedColorSyncDragDeferredCount);
            return;
        }

        QueueDeferredSelectedColorSync();
    }

    private void QueueDeferredSelectedColorSync()
    {
        _runtimeQueueDeferredSelectedColorSyncCallCount++;
        Interlocked.Increment(ref _telemetryQueueDeferredSelectedColorSyncCallCount);
        if (_isSelectedColorSyncDeferred)
        {
            _runtimeQueueDeferredSelectedColorSyncAlreadyQueuedCount++;
            Interlocked.Increment(ref _telemetryQueueDeferredSelectedColorSyncAlreadyQueuedCount);
            return;
        }

        _isSelectedColorSyncDeferred = true;
        Dispatcher.EnqueueDeferred(FlushDeferredSelectedColorSync);
    }

    private void FlushDeferredSelectedColorSync()
    {
        _runtimeFlushDeferredSelectedColorSyncCallCount++;
        Interlocked.Increment(ref _telemetryFlushDeferredSelectedColorSyncCallCount);
        _isSelectedColorSyncDeferred = false;
        if (!_hasPendingSelectedColorSync)
        {
            _runtimeFlushDeferredSelectedColorSyncNoPendingCount++;
            Interlocked.Increment(ref _telemetryFlushDeferredSelectedColorSyncNoPendingCount);
            return;
        }

        if (_isDragging)
        {
            _runtimeFlushDeferredSelectedColorSyncRequeueWhileDraggingCount++;
            Interlocked.Increment(ref _telemetryFlushDeferredSelectedColorSyncRequeueWhileDraggingCount);
            QueueDeferredSelectedColorSync();
            return;
        }

        _hasPendingSelectedColorSync = false;
        SyncSelectedColorFromComponents();
    }

    private void FlushPendingSelectedColorSyncAfterDrag()
    {
        _runtimeFlushPendingSelectedColorSyncAfterDragCallCount++;
        Interlocked.Increment(ref _telemetryFlushPendingSelectedColorSyncAfterDragCallCount);
        if (!_hasPendingSelectedColorSync)
        {
            _runtimeFlushPendingSelectedColorSyncAfterDragNoPendingCount++;
            Interlocked.Increment(ref _telemetryFlushPendingSelectedColorSyncAfterDragNoPendingCount);
            return;
        }

        _isSelectedColorSyncDeferred = false;
        _hasPendingSelectedColorSync = false;
        SyncSelectedColorFromComponents();
    }

    private void SyncSelectedColorFromComponents()
    {
        _runtimeSyncSelectedColorFromComponentsCallCount++;
        Interlocked.Increment(ref _telemetrySyncSelectedColorFromComponentsCallCount);
        var start = Stopwatch.GetTimestamp();

        if (_isSynchronizingSelectedColor)
        {
            _runtimeSyncSelectedColorFromComponentsReentrantSkipCount++;
            Interlocked.Increment(ref _telemetrySyncSelectedColorFromComponentsReentrantSkipCount);
            AccumulateSyncSelectedColorElapsed(start);
            return;
        }

        var color = ColorControlUtilities.FromHsva(Hue, Saturation, Value, Alpha);
        if (color == SelectedColor)
        {
            _runtimeSyncSelectedColorFromComponentsNoOpCount++;
            Interlocked.Increment(ref _telemetrySyncSelectedColorFromComponentsNoOpCount);
            AccumulateSyncSelectedColorElapsed(start);
            return;
        }

        _isSynchronizingComponents = true;
        try
        {
            SetValue(SelectedColorProperty, color);
        }
        finally
        {
            _isSynchronizingComponents = false;
        }

        AccumulateSyncSelectedColorElapsed(start);
    }

    private void UpdateSpectrumFromPointer(Vector2 pointerPosition, LayoutRect spectrumRect)
    {
        _runtimeUpdateSpectrumFromPointerCallCount++;
        Interlocked.Increment(ref _telemetryUpdateSpectrumFromPointerCallCount);
        var start = Stopwatch.GetTimestamp();

        if (spectrumRect.Width <= 0f || spectrumRect.Height <= 0f)
        {
            _runtimeUpdateSpectrumFromPointerZeroRectSkipCount++;
            Interlocked.Increment(ref _telemetryUpdateSpectrumFromPointerZeroRectSkipCount);
            AccumulateUpdateFromPointerElapsed(start);
            return;
        }

        Saturation = ColorControlUtilities.Clamp01((pointerPosition.X - spectrumRect.X) / spectrumRect.Width);
        Value = 1f - ColorControlUtilities.Clamp01((pointerPosition.Y - spectrumRect.Y) / spectrumRect.Height);
        AccumulateUpdateFromPointerElapsed(start);
    }

    private bool TryBuildLocalizedSelectorDirtyBounds(DependencyPropertyChangedEventArgs args, out LayoutRect bounds)
    {
        bounds = default;

        var snapshot = GetColorPickerSnapshotForDiagnostics();
        if (snapshot.SpectrumRect.Width <= 0f || snapshot.SpectrumRect.Height <= 0f)
        {
            return false;
        }

        var oldSaturation = Saturation;
        var oldValue = Value;
        var newSaturation = Saturation;
        var newValue = Value;

        if (args.Property == SaturationProperty)
        {
            if (args.OldValue is not float previousSaturation || args.NewValue is not float nextSaturation)
            {
                return false;
            }

            oldSaturation = previousSaturation;
            newSaturation = nextSaturation;
        }
        else if (args.Property == ValueProperty)
        {
            if (args.OldValue is not float previousValue || args.NewValue is not float nextValue)
            {
                return false;
            }

            oldValue = previousValue;
            newValue = nextValue;
        }
        else
        {
            return false;
        }

        var oldSelector = GetSelectorDirtyBounds(snapshot.SpectrumRect, oldSaturation, oldValue);
        var newSelector = GetSelectorDirtyBounds(snapshot.SpectrumRect, newSaturation, newValue);
        bounds = ExpandRect(Union(oldSelector, newSelector), 2f);
        return bounds.Width > 0f && bounds.Height > 0f;
    }

    private LayoutRect GetSelectorDirtyBounds(LayoutRect spectrumRect, float saturation, float value)
    {
        var center = new Vector2(
            spectrumRect.X + (spectrumRect.Width * ColorControlUtilities.Clamp01(saturation)),
            spectrumRect.Y + (spectrumRect.Height * (1f - ColorControlUtilities.Clamp01(value))));
        var radius = SelectorRadius + 3f;
        return new LayoutRect(
            center.X - radius,
            center.Y - radius,
            radius * 2f,
            radius * 2f);
    }

    private void PrimeRenderDirtyBoundsHint(LayoutRect bounds)
    {
        _pendingRenderDirtyBoundsHint = NormalizeRect(bounds);
        _hasPendingRenderDirtyBoundsHint = _pendingRenderDirtyBoundsHint.Width > 0f && _pendingRenderDirtyBoundsHint.Height > 0f;
    }

    private static LayoutRect ExpandRect(LayoutRect rect, float padding)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return rect;
        }

        return new LayoutRect(
            rect.X - padding,
            rect.Y - padding,
            rect.Width + (padding * 2f),
            rect.Height + (padding * 2f));
    }

    private static LayoutRect NormalizeRect(LayoutRect rect)
    {
        var x = MathF.Min(rect.X, rect.X + rect.Width);
        var y = MathF.Min(rect.Y, rect.Y + rect.Height);
        var right = MathF.Max(rect.X, rect.X + rect.Width);
        var bottom = MathF.Max(rect.Y, rect.Y + rect.Height);
        return new LayoutRect(x, y, MathF.Max(0f, right - x), MathF.Max(0f, bottom - y));
    }

    private static LayoutRect Union(LayoutRect left, LayoutRect right)
    {
        var x = MathF.Min(left.X, right.X);
        var y = MathF.Min(left.Y, right.Y);
        var rightEdge = MathF.Max(left.X + left.Width, right.X + right.Width);
        var bottomEdge = MathF.Max(left.Y + left.Height, right.Y + right.Height);
        return new LayoutRect(x, y, MathF.Max(0f, rightEdge - x), MathF.Max(0f, bottomEdge - y));
    }

    private void DrawSvSelector(SpriteBatch spriteBatch, Vector2 center)
    {
        UiDrawing.DrawFilledCircle(spriteBatch, center, SelectorRadius, Color.Transparent, Opacity);
        UiDrawing.DrawCircleStroke(spriteBatch, center, SelectorRadius, 2f, Color.White, Opacity);
        UiDrawing.DrawCircleStroke(spriteBatch, center, SelectorRadius + 1.5f, 1f, Color.Black, Opacity);
    }

    private void PrewarmSpectrumTextureIfPossible()
    {
        var graphicsDevice = UiRoot.Current?.LastGraphicsDeviceForResources;
        if (graphicsDevice == null)
        {
            return;
        }

        var snapshot = GetColorPickerSnapshotForDiagnostics();
        if (snapshot.SpectrumRect.Width <= 0f || snapshot.SpectrumRect.Height <= 0f)
        {
            return;
        }

        _ = GetOrCreateSpectrumTexture(
            this,
            graphicsDevice,
            Math.Max(1, (int)MathF.Round(snapshot.SpectrumRect.Width)),
            Math.Max(1, (int)MathF.Round(snapshot.SpectrumRect.Height)),
            Hue);
    }

    private static Texture2D GetOrCreateSpectrumTexture(ColorPicker owner, GraphicsDevice graphicsDevice, int width, int height, float hue)
    {
        if (!SpectrumTextureCaches.TryGetValue(graphicsDevice, out var cache))
        {
            cache = new Dictionary<SvTextureCacheKey, Texture2D>();
            SpectrumTextureCaches[graphicsDevice] = cache;
        }

        var key = new SvTextureCacheKey(width, height, (int)MathF.Round(ColorControlUtilities.NormalizeHue(hue) * 100f));
        if (!cache.TryGetValue(key, out var texture))
        {
            owner._runtimeSpectrumTextureCacheMissCount++;
            Interlocked.Increment(ref _telemetrySpectrumTextureCacheMissCount);
            texture = BuildSpectrumTexture(owner, graphicsDevice, width, height, hue);
            cache[key] = texture;
        }
        else
        {
            owner._runtimeSpectrumTextureCacheHitCount++;
            Interlocked.Increment(ref _telemetrySpectrumTextureCacheHitCount);
        }

        return texture;
    }

    private static Texture2D BuildSpectrumTexture(ColorPicker owner, GraphicsDevice graphicsDevice, int width, int height, float hue)
    {
        var start = Stopwatch.GetTimestamp();
        var texture = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
        var pixels = new Color[width * height];
        for (var y = 0; y < height; y++)
        {
            var value = height <= 1 ? 1f : 1f - ((float)y / (height - 1));
            for (var x = 0; x < width; x++)
            {
                var saturation = width <= 1 ? 0f : (float)x / (width - 1);
                pixels[(y * width) + x] = ColorControlUtilities.FromHsva(hue, saturation, value, 1f);
            }
        }

        texture.SetData(pixels);
        var elapsed = Stopwatch.GetTimestamp() - start;
        owner._runtimeSpectrumTextureBuildCount++;
        owner._runtimeSpectrumTextureBuildElapsedTicks += elapsed;
        Interlocked.Increment(ref _telemetrySpectrumTextureBuildCount);
        Interlocked.Add(ref _telemetrySpectrumTextureBuildElapsedTicks, elapsed);
        return texture;
    }

    private void AccumulateHandlePointerDownElapsed(long startTimestamp)
    {
        var elapsed = Stopwatch.GetTimestamp() - startTimestamp;
        _runtimeHandlePointerDownElapsedTicks += elapsed;
        Interlocked.Add(ref _telemetryHandlePointerDownElapsedTicks, elapsed);
    }

    private void AccumulateRenderElapsed(long startTimestamp)
    {
        var elapsed = Stopwatch.GetTimestamp() - startTimestamp;
        _runtimeRenderElapsedTicks += elapsed;
        Interlocked.Add(ref _telemetryRenderElapsedTicks, elapsed);
    }

    private void AccumulateSyncSelectedColorElapsed(long startTimestamp)
    {
        var elapsed = Stopwatch.GetTimestamp() - startTimestamp;
        _runtimeSyncSelectedColorFromComponentsElapsedTicks += elapsed;
        Interlocked.Add(ref _telemetrySyncSelectedColorFromComponentsElapsedTicks, elapsed);
    }

    private void AccumulateUpdateFromPointerElapsed(long startTimestamp)
    {
        var elapsed = Stopwatch.GetTimestamp() - startTimestamp;
        _runtimeUpdateSpectrumFromPointerElapsedTicks += elapsed;
        Interlocked.Add(ref _telemetryUpdateSpectrumFromPointerElapsedTicks, elapsed);
    }

    private static ColorPickerTelemetrySnapshot CreateTelemetrySnapshot(bool reset)
    {
        return new ColorPickerTelemetrySnapshot(
            ReadCounter(ref _telemetryHandlePointerDownCallCount, reset),
            ReadMilliseconds(ref _telemetryHandlePointerDownElapsedTicks, reset),
            ReadCounter(ref _telemetryHandlePointerDownHitCount, reset),
            ReadCounter(ref _telemetryHandlePointerDownMissCount, reset),
            ReadCounter(ref _telemetryHandlePointerMoveCallCount, reset),
            ReadMilliseconds(ref _telemetryHandlePointerMoveElapsedTicks, reset),
            ReadCounter(ref _telemetryHandlePointerMoveDragCount, reset),
            ReadCounter(ref _telemetryHandlePointerMoveIgnoredCount, reset),
            ReadCounter(ref _telemetryHandlePointerUpCallCount, reset),
            ReadCounter(ref _telemetryHandlePointerUpStateChangeCount, reset),
            ReadCounter(ref _telemetrySetMouseOverCallCount, reset),
            ReadCounter(ref _telemetrySetMouseOverStateChangeCount, reset),
            ReadCounter(ref _telemetryUpdateSpectrumFromPointerCallCount, reset),
            ReadMilliseconds(ref _telemetryUpdateSpectrumFromPointerElapsedTicks, reset),
            ReadCounter(ref _telemetryUpdateSpectrumFromPointerZeroRectSkipCount, reset),
            ReadCounter(ref _telemetryRequestSelectedColorSyncCallCount, reset),
            ReadCounter(ref _telemetryRequestSelectedColorSyncDragDeferredCount, reset),
            ReadCounter(ref _telemetryQueueDeferredSelectedColorSyncCallCount, reset),
            ReadCounter(ref _telemetryQueueDeferredSelectedColorSyncAlreadyQueuedCount, reset),
            ReadCounter(ref _telemetryFlushDeferredSelectedColorSyncCallCount, reset),
            ReadCounter(ref _telemetryFlushDeferredSelectedColorSyncNoPendingCount, reset),
            ReadCounter(ref _telemetryFlushDeferredSelectedColorSyncRequeueWhileDraggingCount, reset),
            ReadCounter(ref _telemetryFlushPendingSelectedColorSyncAfterDragCallCount, reset),
            ReadCounter(ref _telemetryFlushPendingSelectedColorSyncAfterDragNoPendingCount, reset),
            ReadCounter(ref _telemetrySyncSelectedColorFromComponentsCallCount, reset),
            ReadMilliseconds(ref _telemetrySyncSelectedColorFromComponentsElapsedTicks, reset),
            ReadCounter(ref _telemetrySyncSelectedColorFromComponentsReentrantSkipCount, reset),
            ReadCounter(ref _telemetrySyncSelectedColorFromComponentsNoOpCount, reset),
            ReadCounter(ref _telemetrySelectedColorChangedCallCount, reset),
            ReadCounter(ref _telemetrySelectedColorChangedExternalSyncCount, reset),
            ReadCounter(ref _telemetrySelectedColorChangedComponentWritebackCount, reset),
            ReadCounter(ref _telemetrySelectedColorChangedRaisedCount, reset),
            ReadCounter(ref _telemetryHueChangedCallCount, reset),
            ReadCounter(ref _telemetrySaturationChangedCallCount, reset),
            ReadCounter(ref _telemetryValueChangedCallCount, reset),
            ReadCounter(ref _telemetryAlphaChangedCallCount, reset),
            ReadCounter(ref _telemetryRenderCallCount, reset),
            ReadMilliseconds(ref _telemetryRenderElapsedTicks, reset),
            ReadCounter(ref _telemetryRenderSkippedZeroRectCount, reset),
            ReadCounter(ref _telemetrySpectrumTextureCacheHitCount, reset),
            ReadCounter(ref _telemetrySpectrumTextureCacheMissCount, reset),
            ReadCounter(ref _telemetrySpectrumTextureBuildCount, reset),
            ReadMilliseconds(ref _telemetrySpectrumTextureBuildElapsedTicks, reset));
    }

    private static long ReadCounter(ref long value, bool reset)
    {
        return reset ? Interlocked.Exchange(ref value, 0) : Interlocked.Read(ref value);
    }

    private static double ReadMilliseconds(ref long elapsedTicks, bool reset)
    {
        return ToMilliseconds(reset ? Interlocked.Exchange(ref elapsedTicks, 0) : Interlocked.Read(ref elapsedTicks));
    }

    private static double ToMilliseconds(long elapsedTicks)
    {
        return elapsedTicks * 1000d / Stopwatch.Frequency;
    }

    private static bool Contains(LayoutRect rect, Vector2 point)
    {
        return point.X >= rect.X &&
               point.X <= rect.X + rect.Width &&
               point.Y >= rect.Y &&
               point.Y <= rect.Y + rect.Height;
    }

    private static bool AreClose(float left, float right)
    {
        return MathF.Abs(left - right) <= 0.0001f;
    }

    private readonly record struct SvTextureCacheKey(int Width, int Height, int HueKey);
}