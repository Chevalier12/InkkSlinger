using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public sealed class ColorPicker : Control
{
    private const float DefaultWidth = 160f;
    private const float DefaultHeight = 120f;
    private const float SelectorRadius = 5f;
    private const float OuterPadding = 2f;

    private static readonly Dictionary<GraphicsDevice, Dictionary<SvTextureCacheKey, Texture2D>> SpectrumTextureCaches = new();

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

    public ColorPicker()
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

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var snapshot = GetColorPickerSnapshotForDiagnostics();
        if (snapshot.SpectrumRect.Width <= 0f || snapshot.SpectrumRect.Height <= 0f)
        {
            return;
        }

        var spectrumTexture = GetOrCreateSpectrumTexture(
            spriteBatch.GraphicsDevice,
            Math.Max(1, (int)MathF.Round(snapshot.SpectrumRect.Width)),
            Math.Max(1, (int)MathF.Round(snapshot.SpectrumRect.Height)),
            Hue);
        UiDrawing.DrawTexture(spriteBatch, spectrumTexture, snapshot.SpectrumRect, color: Color.White, opacity: Opacity);

        var outline = _isMouseOver ? new Color(255, 255, 255, 220) : new Color(24, 24, 24, 220);
        UiDrawing.DrawRectStroke(spriteBatch, snapshot.SpectrumRect, 1f, outline, Opacity);

        DrawSvSelector(spriteBatch, snapshot.SaturationSelector);
    }

    internal bool HandlePointerDownFromInput(Vector2 pointerPosition)
    {
        var snapshot = GetColorPickerSnapshotForDiagnostics();
        if (!IsEnabled)
        {
            return false;
        }

        if (Contains(snapshot.SpectrumRect, pointerPosition))
        {
            _isDragging = true;
            UpdateSpectrumFromPointer(pointerPosition, snapshot.SpectrumRect);
            return true;
        }

        return false;
    }

    internal void HandlePointerMoveFromInput(Vector2 pointerPosition)
    {
        if (_isDragging)
        {
            UpdateSpectrumFromPointer(pointerPosition, GetColorPickerSnapshotForDiagnostics().SpectrumRect);
        }
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

    private void UpdateSpectrumFromPointer(Vector2 pointerPosition, LayoutRect spectrumRect)
    {
        if (spectrumRect.Width <= 0f || spectrumRect.Height <= 0f)
        {
            return;
        }

        Saturation = ColorControlUtilities.Clamp01((pointerPosition.X - spectrumRect.X) / spectrumRect.Width);
        Value = 1f - ColorControlUtilities.Clamp01((pointerPosition.Y - spectrumRect.Y) / spectrumRect.Height);
    }

    private void DrawSvSelector(SpriteBatch spriteBatch, Vector2 center)
    {
        UiDrawing.DrawFilledCircle(spriteBatch, center, SelectorRadius, Color.Transparent, Opacity);
        UiDrawing.DrawCircleStroke(spriteBatch, center, SelectorRadius, 2f, Color.White, Opacity);
        UiDrawing.DrawCircleStroke(spriteBatch, center, SelectorRadius + 1.5f, 1f, Color.Black, Opacity);
    }

    private static Texture2D GetOrCreateSpectrumTexture(GraphicsDevice graphicsDevice, int width, int height, float hue)
    {
        if (!SpectrumTextureCaches.TryGetValue(graphicsDevice, out var cache))
        {
            cache = new Dictionary<SvTextureCacheKey, Texture2D>();
            SpectrumTextureCaches[graphicsDevice] = cache;
        }

        var key = new SvTextureCacheKey(width, height, (int)MathF.Round(ColorControlUtilities.NormalizeHue(hue) * 100f));
        if (!cache.TryGetValue(key, out var texture))
        {
            texture = BuildSpectrumTexture(graphicsDevice, width, height, hue);
            cache[key] = texture;
        }

        return texture;
    }

    private static Texture2D BuildSpectrumTexture(GraphicsDevice graphicsDevice, int width, int height, float hue)
    {
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
        return texture;
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

internal readonly record struct ColorPickerRuntimeDiagnosticsSnapshot(
    LayoutRect SpectrumRect,
    Vector2 SaturationSelector,
    float SelectionIndicatorRadius,
    bool IsDragging,
    bool IsMouseOver);