using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public sealed class ColorSpectrum : Control, IRenderDirtyBoundsHintProvider
{
    private const float MinimumThickness = 18f;
    private const float MinimumLength = 120f;
    private const float SelectorThickness = 2f;
    private const float SelectorInset = 1f;
    private const int CheckerSize = 4;

    private static readonly Dictionary<GraphicsDevice, Dictionary<SpectrumTextureCacheKey, Texture2D>> SpectrumTextureCaches = new();
    private static readonly Dictionary<GraphicsDevice, Dictionary<AlphaCheckerTextureCacheKey, Texture2D>> AlphaCheckerTextureCaches = new();
    private static readonly Dictionary<GraphicsDevice, Dictionary<AlphaOverlayTextureCacheKey, Texture2D>> AlphaOverlayTextureCaches = new();
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
    private static long _telemetryUpdateFromPointerCallCount;
    private static long _telemetryUpdateFromPointerElapsedTicks;
    private static long _telemetryUpdateFromPointerZeroRectSkipCount;
    private static long _telemetryUpdateFromPointerHuePathCount;
    private static long _telemetryUpdateFromPointerAlphaPathCount;
    private static long _telemetrySyncSelectedColorFromComponentsCallCount;
    private static long _telemetrySyncSelectedColorFromComponentsElapsedTicks;
    private static long _telemetrySyncSelectedColorFromComponentsNoOpCount;
    private static long _telemetrySelectedColorChangedCallCount;
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

    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(ColorSpectrum),
            new FrameworkPropertyMetadata(
                Orientation.Vertical,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.Register(
            nameof(Mode),
            typeof(ColorSpectrumMode),
            typeof(ColorSpectrum),
            new FrameworkPropertyMetadata(
                ColorSpectrumMode.Hue,
                FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is ColorSpectrumMode mode ? mode : ColorSpectrumMode.Hue));

    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(
            nameof(SelectedColor),
            typeof(Color),
            typeof(ColorSpectrum),
            new FrameworkPropertyMetadata(
                new Color(255, 0, 0, 255),
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is ColorSpectrum spectrum &&
                        args.OldValue is Color oldColor &&
                        args.NewValue is Color newColor)
                    {
                        spectrum.OnSelectedColorChanged(oldColor, newColor);
                    }
                }));

    public static readonly DependencyProperty HueProperty =
        DependencyProperty.Register(
            nameof(Hue),
            typeof(float),
            typeof(ColorSpectrum),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is ColorSpectrum spectrum &&
                        args.OldValue is float oldValue &&
                        args.NewValue is float newValue)
                    {
                        spectrum.OnHueChanged(oldValue, newValue);
                    }
                },
                coerceValueCallback: static (_, value) => value is float numeric ? CoerceHueValue(numeric) : 0f));

    public static readonly DependencyProperty SaturationProperty =
        DependencyProperty.Register(
            nameof(Saturation),
            typeof(float),
            typeof(ColorSpectrum),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is ColorSpectrum spectrum &&
                        args.OldValue is float oldValue &&
                        args.NewValue is float newValue)
                    {
                        spectrum.OnSaturationChanged(oldValue, newValue);
                    }
                },
                coerceValueCallback: static (_, value) => value is float numeric ? ColorControlUtilities.Clamp01(numeric) : 1f));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(float),
            typeof(ColorSpectrum),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is ColorSpectrum spectrum &&
                        args.OldValue is float oldValue &&
                        args.NewValue is float newValue)
                    {
                        spectrum.OnValueComponentChanged(oldValue, newValue);
                    }
                },
                coerceValueCallback: static (_, value) => value is float numeric ? ColorControlUtilities.Clamp01(numeric) : 1f));

    public static readonly DependencyProperty AlphaProperty =
        DependencyProperty.Register(
            nameof(Alpha),
            typeof(float),
            typeof(ColorSpectrum),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is ColorSpectrum spectrum &&
                        args.OldValue is float oldValue &&
                        args.NewValue is float newValue)
                    {
                        spectrum.OnAlphaChanged(oldValue, newValue);
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
    private long _runtimeUpdateFromPointerCallCount;
    private long _runtimeUpdateFromPointerElapsedTicks;
    private long _runtimeUpdateFromPointerZeroRectSkipCount;
    private long _runtimeUpdateFromPointerHuePathCount;
    private long _runtimeUpdateFromPointerAlphaPathCount;
    private long _runtimeSyncSelectedColorFromComponentsCallCount;
    private long _runtimeSyncSelectedColorFromComponentsElapsedTicks;
    private long _runtimeSyncSelectedColorFromComponentsNoOpCount;
    private long _runtimeSelectedColorChangedCallCount;
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

    public ColorSpectrum()
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

    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public Color SelectedColor
    {
        get => GetValue<Color>(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public ColorSpectrumMode Mode
    {
        get => GetValue<ColorSpectrumMode>(ModeProperty);
        set => SetValue(ModeProperty, value);
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
        if (Orientation == Orientation.Horizontal)
        {
            desired.X = MathF.Max(desired.X, MinimumLength);
            desired.Y = MathF.Max(desired.Y, MinimumThickness);
        }
        else
        {
            desired.X = MathF.Max(desired.X, MinimumThickness);
            desired.Y = MathF.Max(desired.Y, MinimumLength);
        }

        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        PrewarmSpectrumTexturesIfPossible();
        return arranged;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        _runtimeRenderCallCount++;
        Interlocked.Increment(ref _telemetryRenderCallCount);
        var start = Stopwatch.GetTimestamp();

        var spectrumRect = GetSpectrumRect();
        if (spectrumRect.Width <= 0f || spectrumRect.Height <= 0f)
        {
            _runtimeRenderSkippedZeroRectCount++;
            Interlocked.Increment(ref _telemetryRenderSkippedZeroRectCount);
            AccumulateRenderElapsed(start);
            return;
        }

        var baseColor = ColorControlUtilities.FromHsva(Hue, Saturation, Value, 1f);
        var textureWidth = Math.Max(1, (int)MathF.Round(spectrumRect.Width));
        var textureHeight = Math.Max(1, (int)MathF.Round(spectrumRect.Height));
        if (Mode == ColorSpectrumMode.Alpha)
        {
            var checkerTexture = GetOrCreateAlphaCheckerTexture(
                this,
                spriteBatch.GraphicsDevice,
                textureWidth,
                textureHeight,
                Orientation);
            UiDrawing.DrawTexture(spriteBatch, checkerTexture, spectrumRect, color: Color.White, opacity: Opacity);

            var overlayTexture = GetOrCreateAlphaOverlayTexture(
                this,
                spriteBatch.GraphicsDevice,
                textureWidth,
                textureHeight,
                Orientation,
                baseColor);
            UiDrawing.DrawTexture(spriteBatch, overlayTexture, spectrumRect, color: Color.White, opacity: Opacity);
        }
        else
        {
            var texture = GetOrCreateSpectrumTexture(
                this,
                spriteBatch.GraphicsDevice,
                textureWidth,
                textureHeight,
                Orientation,
                Mode,
                baseColor);
            UiDrawing.DrawTexture(spriteBatch, texture, spectrumRect, color: Color.White, opacity: Opacity);
        }

        var outline = _isMouseOver ? new Color(255, 255, 255, 220) : new Color(20, 20, 20, 220);
        UiDrawing.DrawRectStroke(spriteBatch, spectrumRect, 1f, outline, Opacity);
        DrawSelector(spriteBatch, spectrumRect);
        AccumulateRenderElapsed(start);
    }

    internal bool HandlePointerDownFromInput(Vector2 pointerPosition)
    {
        _runtimeHandlePointerDownCallCount++;
        Interlocked.Increment(ref _telemetryHandlePointerDownCallCount);
        var start = Stopwatch.GetTimestamp();

        var spectrumRect = GetSpectrumRect();
        if (!IsEnabled || !Contains(spectrumRect, pointerPosition))
        {
            _runtimeHandlePointerDownMissCount++;
            Interlocked.Increment(ref _telemetryHandlePointerDownMissCount);
            AccumulateHandlePointerDownElapsed(start);
            return false;
        }

        _isDragging = true;
        _runtimeHandlePointerDownHitCount++;
        Interlocked.Increment(ref _telemetryHandlePointerDownHitCount);
        UpdateFromPointer(pointerPosition, spectrumRect);
        AccumulateHandlePointerDownElapsed(start);
        return true;
    }

    private void OnPreviewMouseLeftButtonDownClaimInput(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        if (!IsEnabled || args.Button != MouseButton.Left || args.Handled)
        {
            return;
        }

        if (Contains(GetSpectrumRect(), args.Position))
        {
            args.Handled = true;
        }
    }

    internal void HandlePointerMoveFromInput(Vector2 pointerPosition)
    {
        _runtimeHandlePointerMoveCallCount++;
        Interlocked.Increment(ref _telemetryHandlePointerMoveCallCount);
        var start = Stopwatch.GetTimestamp();

        if (!_isDragging)
        {
            _runtimeHandlePointerMoveIgnoredCount++;
            Interlocked.Increment(ref _telemetryHandlePointerMoveIgnoredCount);
            var elapsedIgnored = Stopwatch.GetTimestamp() - start;
            _runtimeHandlePointerMoveElapsedTicks += elapsedIgnored;
            Interlocked.Add(ref _telemetryHandlePointerMoveElapsedTicks, elapsedIgnored);
            return;
        }

        _runtimeHandlePointerMoveDragCount++;
        Interlocked.Increment(ref _telemetryHandlePointerMoveDragCount);
        UpdateFromPointer(pointerPosition, GetSpectrumRect());
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

    internal ColorSpectrumRuntimeDiagnosticsSnapshot GetColorSpectrumSnapshotForDiagnostics()
    {
        var rect = GetSpectrumRect();
        return new ColorSpectrumRuntimeDiagnosticsSnapshot(
            rect,
            GetSelectorNormalizedOffset(),
            GetSelectorPosition(rect),
            Mode,
            _isDragging,
            _isMouseOver,
            Orientation,
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
            _runtimeUpdateFromPointerCallCount,
            ToMilliseconds(_runtimeUpdateFromPointerElapsedTicks),
            _runtimeUpdateFromPointerZeroRectSkipCount,
            _runtimeUpdateFromPointerHuePathCount,
            _runtimeUpdateFromPointerAlphaPathCount,
            _runtimeSyncSelectedColorFromComponentsCallCount,
            ToMilliseconds(_runtimeSyncSelectedColorFromComponentsElapsedTicks),
            _runtimeSyncSelectedColorFromComponentsNoOpCount,
            _runtimeSelectedColorChangedCallCount,
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

    internal static new ColorSpectrumTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return CreateTelemetrySnapshot(reset: false);
    }

    internal static new ColorSpectrumTelemetrySnapshot GetTelemetryAndReset()
    {
        return CreateTelemetrySnapshot(reset: true);
    }

    private void OnSelectedColorChanged(Color oldColor, Color newColor)
    {
        _runtimeSelectedColorChangedCallCount++;
        Interlocked.Increment(ref _telemetrySelectedColorChangedCallCount);

        if (!_isSynchronizingComponents && oldColor != newColor)
        {
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

        if (args.Property == SelectedColorProperty)
        {
            return false;
        }

        if (Mode == ColorSpectrumMode.Alpha)
        {
            if (args.Property == AlphaProperty &&
                args.OldValue is float oldAlpha &&
                args.NewValue is float newAlpha &&
                TryBuildLocalizedSelectorDirtyBounds(oldAlpha, newAlpha, out var selectorDirtyBounds))
            {
                PrimeRenderDirtyBoundsHint(selectorDirtyBounds);
            }

            return true;
        }

        if (args.Property == SaturationProperty ||
            args.Property == ValueProperty ||
            args.Property == AlphaProperty)
        {
            return false;
        }

        if (args.Property == HueProperty &&
            args.OldValue is float oldHue &&
            args.NewValue is float newHue &&
            TryBuildLocalizedSelectorDirtyBounds(oldHue, newHue, out var hueDirtyBounds))
        {
            PrimeRenderDirtyBoundsHint(hueDirtyBounds);
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
        _hasPendingSelectedColorSync = true;
        if (_isDragging)
        {
            return;
        }

        QueueDeferredSelectedColorSync();
    }

    private void QueueDeferredSelectedColorSync()
    {
        if (_isSelectedColorSyncDeferred)
        {
            return;
        }

        _isSelectedColorSyncDeferred = true;
        Dispatcher.EnqueueDeferred(FlushDeferredSelectedColorSync);
    }

    private void FlushDeferredSelectedColorSync()
    {
        _isSelectedColorSyncDeferred = false;
        if (!_hasPendingSelectedColorSync)
        {
            return;
        }

        if (_isDragging)
        {
            QueueDeferredSelectedColorSync();
            return;
        }

        _hasPendingSelectedColorSync = false;
        SyncSelectedColorFromComponents();
    }

    private void FlushPendingSelectedColorSyncAfterDrag()
    {
        if (!_hasPendingSelectedColorSync)
        {
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

    private LayoutRect GetSpectrumRect()
    {
        var slot = LayoutSlot;
        return new LayoutRect(slot.X, slot.Y, MathF.Max(0f, slot.Width), MathF.Max(0f, slot.Height));
    }

    private void UpdateFromPointer(Vector2 pointerPosition, LayoutRect spectrumRect)
    {
        _runtimeUpdateFromPointerCallCount++;
        Interlocked.Increment(ref _telemetryUpdateFromPointerCallCount);
        var start = Stopwatch.GetTimestamp();

        if (spectrumRect.Width <= 0f || spectrumRect.Height <= 0f)
        {
            _runtimeUpdateFromPointerZeroRectSkipCount++;
            Interlocked.Increment(ref _telemetryUpdateFromPointerZeroRectSkipCount);
            AccumulateUpdateFromPointerElapsed(start);
            return;
        }

        var normalized = Orientation == Orientation.Horizontal
            ? (pointerPosition.X - spectrumRect.X) / spectrumRect.Width
            : (pointerPosition.Y - spectrumRect.Y) / spectrumRect.Height;
        normalized = ColorControlUtilities.Clamp01(normalized);

        if (Mode == ColorSpectrumMode.Alpha)
        {
            _runtimeUpdateFromPointerAlphaPathCount++;
            Interlocked.Increment(ref _telemetryUpdateFromPointerAlphaPathCount);
            Alpha = Orientation == Orientation.Horizontal
                ? normalized
                : 1f - normalized;
            AccumulateUpdateFromPointerElapsed(start);
            return;
        }

        _runtimeUpdateFromPointerHuePathCount++;
        Interlocked.Increment(ref _telemetryUpdateFromPointerHuePathCount);
        Hue = CoerceHueValue(normalized * 360f);
        AccumulateUpdateFromPointerElapsed(start);
    }

    private float GetSelectorNormalizedOffset()
    {
        if (Mode == ColorSpectrumMode.Alpha)
        {
            return Orientation == Orientation.Horizontal
                ? ColorControlUtilities.Clamp01(Alpha)
                : 1f - ColorControlUtilities.Clamp01(Alpha);
        }

        return GetDisplayHue(Hue) / 360f;
    }

    private float GetSelectorPosition(LayoutRect spectrumRect)
    {
        return Orientation == Orientation.Horizontal
            ? spectrumRect.X + (spectrumRect.Width * GetSelectorNormalizedOffset())
            : spectrumRect.Y + (spectrumRect.Height * GetSelectorNormalizedOffset());
    }

    private bool TryBuildLocalizedSelectorDirtyBounds(float oldValue, float newValue, out LayoutRect bounds)
    {
        bounds = default;

        var spectrumRect = GetSpectrumRect();
        if (spectrumRect.Width <= 0f || spectrumRect.Height <= 0f)
        {
            return false;
        }

        var oldSelector = GetSelectorRectForNormalizedOffset(GetSelectorNormalizedOffsetForValue(oldValue), spectrumRect);
        var newSelector = GetSelectorRectForNormalizedOffset(GetSelectorNormalizedOffsetForValue(newValue), spectrumRect);
        bounds = ExpandRect(Union(oldSelector, newSelector), 2f);
        return bounds.Width > 0f && bounds.Height > 0f;
    }

    private float GetSelectorNormalizedOffsetForValue(float value)
    {
        if (Mode == ColorSpectrumMode.Alpha)
        {
            var clamped = ColorControlUtilities.Clamp01(value);
            return Orientation == Orientation.Horizontal
                ? clamped
                : 1f - clamped;
        }

        return GetDisplayHue(value) / 360f;
    }

    private LayoutRect GetSelectorRectForNormalizedOffset(float normalizedOffset, LayoutRect spectrumRect)
    {
        var selectorPosition = Orientation == Orientation.Horizontal
            ? spectrumRect.X + (spectrumRect.Width * normalizedOffset)
            : spectrumRect.Y + (spectrumRect.Height * normalizedOffset);

        return Orientation == Orientation.Horizontal
            ? new LayoutRect(
                selectorPosition - (SelectorThickness / 2f),
                spectrumRect.Y + SelectorInset,
                SelectorThickness,
                MathF.Max(0f, spectrumRect.Height - (SelectorInset * 2f)))
            : new LayoutRect(
                spectrumRect.X + SelectorInset,
                selectorPosition - (SelectorThickness / 2f),
                MathF.Max(0f, spectrumRect.Width - (SelectorInset * 2f)),
                SelectorThickness);
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

    private static float CoerceHueValue(float hue)
    {
        if (!float.IsFinite(hue))
        {
            return 0f;
        }

        var normalized = ColorControlUtilities.NormalizeHue(hue);
        if (normalized <= 0f && hue > 0f)
        {
            return 360f;
        }

        return normalized;
    }

    private static float GetDisplayHue(float hue)
    {
        var normalized = ColorControlUtilities.NormalizeHue(hue);
        if (normalized <= 0f && hue > 0f)
        {
            return 360f;
        }

        return normalized;
    }

    private void DrawSelector(SpriteBatch spriteBatch, LayoutRect spectrumRect)
    {
        var selectorPosition = GetSelectorPosition(spectrumRect);
        if (Orientation == Orientation.Horizontal)
        {
            var lineRect = new LayoutRect(selectorPosition - (SelectorThickness / 2f), spectrumRect.Y + SelectorInset, SelectorThickness, MathF.Max(0f, spectrumRect.Height - (SelectorInset * 2f)));
            UiDrawing.DrawFilledRect(spriteBatch, lineRect, Color.White, Opacity);
            UiDrawing.DrawRectStroke(spriteBatch, lineRect, 1f, Color.Black, Opacity);
        }
        else
        {
            var lineRect = new LayoutRect(spectrumRect.X + SelectorInset, selectorPosition - (SelectorThickness / 2f), MathF.Max(0f, spectrumRect.Width - (SelectorInset * 2f)), SelectorThickness);
            UiDrawing.DrawFilledRect(spriteBatch, lineRect, Color.White, Opacity);
            UiDrawing.DrawRectStroke(spriteBatch, lineRect, 1f, Color.Black, Opacity);
        }
    }

    private void PrewarmSpectrumTexturesIfPossible()
    {
        var graphicsDevice = UiRoot.Current?.LastGraphicsDeviceForResources;
        if (graphicsDevice == null)
        {
            return;
        }

        var spectrumRect = GetSpectrumRect();
        if (spectrumRect.Width <= 0f || spectrumRect.Height <= 0f)
        {
            return;
        }

        var textureWidth = Math.Max(1, (int)MathF.Round(spectrumRect.Width));
        var textureHeight = Math.Max(1, (int)MathF.Round(spectrumRect.Height));
        if (Mode == ColorSpectrumMode.Alpha)
        {
            var baseColor = ColorControlUtilities.FromHsva(Hue, Saturation, Value, 1f);
            _ = GetOrCreateAlphaCheckerTexture(this, graphicsDevice, textureWidth, textureHeight, Orientation);
            _ = GetOrCreateAlphaOverlayTexture(this, graphicsDevice, textureWidth, textureHeight, Orientation, baseColor);
            return;
        }

        _ = GetOrCreateSpectrumTexture(
            this,
            graphicsDevice,
            textureWidth,
            textureHeight,
            Orientation,
            Mode,
            ColorControlUtilities.FromHsva(Hue, Saturation, Value, 1f));
    }

    private static Texture2D GetOrCreateSpectrumTexture(ColorSpectrum owner, GraphicsDevice graphicsDevice, int width, int height, Orientation orientation, ColorSpectrumMode mode, Color baseColor)
    {
        if (!SpectrumTextureCaches.TryGetValue(graphicsDevice, out var cache))
        {
            cache = new Dictionary<SpectrumTextureCacheKey, Texture2D>();
            SpectrumTextureCaches[graphicsDevice] = cache;
        }

        var key = new SpectrumTextureCacheKey(width, height, orientation, mode, mode == ColorSpectrumMode.Alpha ? baseColor.PackedValue : 0u);
        if (!cache.TryGetValue(key, out var texture))
        {
            owner._runtimeSpectrumTextureCacheMissCount++;
            Interlocked.Increment(ref _telemetrySpectrumTextureCacheMissCount);
            texture = BuildSpectrumTexture(owner, graphicsDevice, width, height, orientation, mode, baseColor);
            cache[key] = texture;
        }
        else
        {
            owner._runtimeSpectrumTextureCacheHitCount++;
            Interlocked.Increment(ref _telemetrySpectrumTextureCacheHitCount);
        }

        return texture;
    }

    private static Texture2D GetOrCreateAlphaCheckerTexture(ColorSpectrum owner, GraphicsDevice graphicsDevice, int width, int height, Orientation orientation)
    {
        if (!AlphaCheckerTextureCaches.TryGetValue(graphicsDevice, out var cache))
        {
            cache = new Dictionary<AlphaCheckerTextureCacheKey, Texture2D>();
            AlphaCheckerTextureCaches[graphicsDevice] = cache;
        }

        var key = new AlphaCheckerTextureCacheKey(width, height, orientation);
        if (!cache.TryGetValue(key, out var texture))
        {
            owner._runtimeSpectrumTextureCacheMissCount++;
            Interlocked.Increment(ref _telemetrySpectrumTextureCacheMissCount);
            texture = BuildAlphaCheckerTexture(owner, graphicsDevice, width, height);
            cache[key] = texture;
        }
        else
        {
            owner._runtimeSpectrumTextureCacheHitCount++;
            Interlocked.Increment(ref _telemetrySpectrumTextureCacheHitCount);
        }

        return texture;
    }

    private static Texture2D GetOrCreateAlphaOverlayTexture(ColorSpectrum owner, GraphicsDevice graphicsDevice, int width, int height, Orientation orientation, Color baseColor)
    {
        if (!AlphaOverlayTextureCaches.TryGetValue(graphicsDevice, out var cache))
        {
            cache = new Dictionary<AlphaOverlayTextureCacheKey, Texture2D>();
            AlphaOverlayTextureCaches[graphicsDevice] = cache;
        }

        var overlayWidth = orientation == Orientation.Horizontal ? width : 1;
        var overlayHeight = orientation == Orientation.Horizontal ? 1 : height;
        var key = new AlphaOverlayTextureCacheKey(overlayWidth, overlayHeight, orientation, baseColor.PackedValue);
        if (!cache.TryGetValue(key, out var texture))
        {
            owner._runtimeSpectrumTextureCacheMissCount++;
            Interlocked.Increment(ref _telemetrySpectrumTextureCacheMissCount);
            texture = BuildAlphaOverlayTexture(owner, graphicsDevice, width, height, orientation, baseColor);
            cache[key] = texture;
        }
        else
        {
            owner._runtimeSpectrumTextureCacheHitCount++;
            Interlocked.Increment(ref _telemetrySpectrumTextureCacheHitCount);
        }

        return texture;
    }

    private static Texture2D BuildSpectrumTexture(ColorSpectrum owner, GraphicsDevice graphicsDevice, int width, int height, Orientation orientation, ColorSpectrumMode mode, Color baseColor)
    {
        var start = Stopwatch.GetTimestamp();
        var texture = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
        var pixels = new Color[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var normalized = orientation == Orientation.Horizontal
                    ? width <= 1 ? 0f : (float)x / (width - 1)
                    : height <= 1 ? 0f : (float)y / (height - 1);

                var hue = ColorControlUtilities.NormalizeHue(normalized * 360f);
                pixels[(y * width) + x] = ColorControlUtilities.FromHsva(hue, 1f, 1f, 1f);
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

    private static Texture2D BuildAlphaCheckerTexture(ColorSpectrum owner, GraphicsDevice graphicsDevice, int width, int height)
    {
        var start = Stopwatch.GetTimestamp();
        var texture = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
        var pixels = new Color[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var checkerDark = ((x / CheckerSize) + (y / CheckerSize)) % 2 == 0;
                pixels[(y * width) + x] = checkerDark ? new Color(150, 150, 150) : new Color(215, 215, 215);
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

    private static Texture2D BuildAlphaOverlayTexture(ColorSpectrum owner, GraphicsDevice graphicsDevice, int width, int height, Orientation orientation, Color baseColor)
    {
        var start = Stopwatch.GetTimestamp();
        var overlayWidth = orientation == Orientation.Horizontal ? width : 1;
        var overlayHeight = orientation == Orientation.Horizontal ? 1 : height;
        var texture = new Texture2D(graphicsDevice, overlayWidth, overlayHeight, false, SurfaceFormat.Color);
        var pixels = new Color[overlayWidth * overlayHeight];
        if (orientation == Orientation.Horizontal)
        {
            for (var x = 0; x < overlayWidth; x++)
            {
                var normalized = overlayWidth <= 1 ? 0f : (float)x / (overlayWidth - 1);
                pixels[x] = new Color(baseColor.R, baseColor.G, baseColor.B, ColorControlUtilities.ToByte(normalized));
            }
        }
        else
        {
            for (var y = 0; y < overlayHeight; y++)
            {
                var normalized = overlayHeight <= 1 ? 1f : 1f - ((float)y / (overlayHeight - 1));
                pixels[y] = new Color(baseColor.R, baseColor.G, baseColor.B, ColorControlUtilities.ToByte(normalized));
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
        _runtimeUpdateFromPointerElapsedTicks += elapsed;
        Interlocked.Add(ref _telemetryUpdateFromPointerElapsedTicks, elapsed);
    }

    private static ColorSpectrumTelemetrySnapshot CreateTelemetrySnapshot(bool reset)
    {
        return new ColorSpectrumTelemetrySnapshot(
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
            ReadCounter(ref _telemetryUpdateFromPointerCallCount, reset),
            ReadMilliseconds(ref _telemetryUpdateFromPointerElapsedTicks, reset),
            ReadCounter(ref _telemetryUpdateFromPointerZeroRectSkipCount, reset),
            ReadCounter(ref _telemetryUpdateFromPointerHuePathCount, reset),
            ReadCounter(ref _telemetryUpdateFromPointerAlphaPathCount, reset),
            ReadCounter(ref _telemetrySyncSelectedColorFromComponentsCallCount, reset),
            ReadMilliseconds(ref _telemetrySyncSelectedColorFromComponentsElapsedTicks, reset),
            ReadCounter(ref _telemetrySyncSelectedColorFromComponentsNoOpCount, reset),
            ReadCounter(ref _telemetrySelectedColorChangedCallCount, reset),
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

    private readonly record struct SpectrumTextureCacheKey(int Width, int Height, Orientation Orientation, ColorSpectrumMode Mode, uint BaseColorPackedValue);
    private readonly record struct AlphaCheckerTextureCacheKey(int Width, int Height, Orientation Orientation);
    private readonly record struct AlphaOverlayTextureCacheKey(int Width, int Height, Orientation Orientation, uint BaseColorPackedValue);
}