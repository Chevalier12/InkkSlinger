using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public sealed class ColorSpectrum : Control
{
    private const float MinimumThickness = 18f;
    private const float MinimumLength = 120f;
    private const float SelectorThickness = 2f;
    private const float SelectorInset = 1f;
    private const int CheckerSize = 4;

    private static readonly Dictionary<GraphicsDevice, Dictionary<SpectrumTextureCacheKey, Texture2D>> SpectrumTextureCaches = new();

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

    public ColorSpectrum()
    {
        Focusable = false;
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

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var spectrumRect = GetSpectrumRect();
        if (spectrumRect.Width <= 0f || spectrumRect.Height <= 0f)
        {
            return;
        }

        var baseColor = ColorControlUtilities.FromHsva(Hue, Saturation, Value, 1f);
        var texture = GetOrCreateSpectrumTexture(
            spriteBatch.GraphicsDevice,
            Math.Max(1, (int)MathF.Round(spectrumRect.Width)),
            Math.Max(1, (int)MathF.Round(spectrumRect.Height)),
            Orientation,
            Mode,
            baseColor);
        UiDrawing.DrawTexture(spriteBatch, texture, spectrumRect, color: Color.White, opacity: Opacity);

        var outline = _isMouseOver ? new Color(255, 255, 255, 220) : new Color(20, 20, 20, 220);
        UiDrawing.DrawRectStroke(spriteBatch, spectrumRect, 1f, outline, Opacity);
        DrawSelector(spriteBatch, spectrumRect);
    }

    internal bool HandlePointerDownFromInput(Vector2 pointerPosition)
    {
        var spectrumRect = GetSpectrumRect();
        if (!IsEnabled || !Contains(spectrumRect, pointerPosition))
        {
            return false;
        }

        _isDragging = true;
        UpdateFromPointer(pointerPosition, spectrumRect);
        return true;
    }

    internal void HandlePointerMoveFromInput(Vector2 pointerPosition)
    {
        if (!_isDragging)
        {
            return;
        }

        UpdateFromPointer(pointerPosition, GetSpectrumRect());
    }

    internal void HandlePointerUpFromInput()
    {
        _isDragging = false;
    }

    internal void SetMouseOverFromInput(bool isMouseOver)
    {
        if (_isMouseOver == isMouseOver)
        {
            return;
        }

        _isMouseOver = isMouseOver;
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
            _isMouseOver);
    }

    private void OnSelectedColorChanged(Color oldColor, Color newColor)
    {
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
            RaiseRoutedEvent(SelectedColorChangedEvent, new ColorChangedEventArgs(SelectedColorChangedEvent, oldColor, newColor));
        }
    }

    private void OnHueChanged(float oldValue, float newValue)
    {
        if (!AreClose(oldValue, newValue))
        {
            SyncSelectedColorFromComponents();
            RaiseRoutedEvent(HueChangedEvent, new RoutedSimpleEventArgs(HueChangedEvent));
        }
    }

    private void OnSaturationChanged(float oldValue, float newValue)
    {
        if (!AreClose(oldValue, newValue))
        {
            SyncSelectedColorFromComponents();
            RaiseRoutedEvent(SaturationChangedEvent, new RoutedSimpleEventArgs(SaturationChangedEvent));
        }
    }

    private void OnValueComponentChanged(float oldValue, float newValue)
    {
        if (!AreClose(oldValue, newValue))
        {
            SyncSelectedColorFromComponents();
            RaiseRoutedEvent(ValueChangedEvent, new RoutedSimpleEventArgs(ValueChangedEvent));
        }
    }

    private void OnAlphaChanged(float oldValue, float newValue)
    {
        if (!AreClose(oldValue, newValue))
        {
            SyncSelectedColorFromComponents();
            RaiseRoutedEvent(AlphaChangedEvent, new RoutedSimpleEventArgs(AlphaChangedEvent));
        }
    }

    private void SyncSelectedColorFromComponents()
    {
        if (_isSynchronizingSelectedColor)
        {
            return;
        }

        var color = ColorControlUtilities.FromHsva(Hue, Saturation, Value, Alpha);
        if (color == SelectedColor)
        {
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
    }

    private LayoutRect GetSpectrumRect()
    {
        var slot = LayoutSlot;
        return new LayoutRect(slot.X, slot.Y, MathF.Max(0f, slot.Width), MathF.Max(0f, slot.Height));
    }

    private void UpdateFromPointer(Vector2 pointerPosition, LayoutRect spectrumRect)
    {
        if (spectrumRect.Width <= 0f || spectrumRect.Height <= 0f)
        {
            return;
        }

        var normalized = Orientation == Orientation.Horizontal
            ? (pointerPosition.X - spectrumRect.X) / spectrumRect.Width
            : (pointerPosition.Y - spectrumRect.Y) / spectrumRect.Height;
        normalized = ColorControlUtilities.Clamp01(normalized);

        if (Mode == ColorSpectrumMode.Alpha)
        {
            Alpha = Orientation == Orientation.Horizontal
                ? normalized
                : 1f - normalized;
            return;
        }

        Hue = CoerceHueValue(normalized * 360f);
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

    private static Texture2D GetOrCreateSpectrumTexture(GraphicsDevice graphicsDevice, int width, int height, Orientation orientation, ColorSpectrumMode mode, Color baseColor)
    {
        if (!SpectrumTextureCaches.TryGetValue(graphicsDevice, out var cache))
        {
            cache = new Dictionary<SpectrumTextureCacheKey, Texture2D>();
            SpectrumTextureCaches[graphicsDevice] = cache;
        }

        var key = new SpectrumTextureCacheKey(width, height, orientation, mode, mode == ColorSpectrumMode.Alpha ? baseColor.PackedValue : 0u);
        if (!cache.TryGetValue(key, out var texture))
        {
            texture = BuildSpectrumTexture(graphicsDevice, width, height, orientation, mode, baseColor);
            cache[key] = texture;
        }

        return texture;
    }

    private static Texture2D BuildSpectrumTexture(GraphicsDevice graphicsDevice, int width, int height, Orientation orientation, ColorSpectrumMode mode, Color baseColor)
    {
        var texture = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
        var pixels = new Color[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var normalized = orientation == Orientation.Horizontal
                    ? width <= 1 ? 0f : (float)x / (width - 1)
                    : height <= 1 ? 0f : (float)y / (height - 1);

                if (mode == ColorSpectrumMode.Alpha)
                {
                    var alpha = orientation == Orientation.Horizontal
                        ? normalized
                        : 1f - normalized;
                    pixels[(y * width) + x] = BlendOverChecker(baseColor, alpha, x, y);
                }
                else
                {
                    var hue = ColorControlUtilities.NormalizeHue(normalized * 360f);
                    pixels[(y * width) + x] = ColorControlUtilities.FromHsva(hue, 1f, 1f, 1f);
                }
            }
        }

        texture.SetData(pixels);
        return texture;
    }

    private static Color BlendOverChecker(Color baseColor, float alpha, int x, int y)
    {
        var checkerDark = ((x / CheckerSize) + (y / CheckerSize)) % 2 == 0;
        var checker = checkerDark ? new Color(150, 150, 150) : new Color(215, 215, 215);
        var checkerR = checker.R / 255f;
        var checkerG = checker.G / 255f;
        var checkerB = checker.B / 255f;
        var baseR = baseColor.R / 255f;
        var baseG = baseColor.G / 255f;
        var baseB = baseColor.B / 255f;
        var r = checkerR + ((baseR - checkerR) * alpha);
        var g = checkerG + ((baseG - checkerG) * alpha);
        var b = checkerB + ((baseB - checkerB) * alpha);
        return new Color(
            ColorControlUtilities.ToByte(r),
            ColorControlUtilities.ToByte(g),
            ColorControlUtilities.ToByte(b),
            (byte)255);
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
}

internal readonly record struct ColorSpectrumRuntimeDiagnosticsSnapshot(
    LayoutRect SpectrumRect,
    float SelectorNormalizedOffset,
    float SelectorPosition,
    ColorSpectrumMode Mode,
    bool IsDragging,
    bool IsMouseOver);