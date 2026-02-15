using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class ProgressBar : Control
{
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(ProgressBar),
            new FrameworkPropertyMetadata(
                Orientation.Horizontal,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(
            nameof(Minimum),
            typeof(float),
            typeof(ProgressBar),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is ProgressBar progressBar)
                    {
                        progressBar.CoerceRangeAndValue();
                    }
                }));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            nameof(Maximum),
            typeof(float),
            typeof(ProgressBar),
            new FrameworkPropertyMetadata(
                100f,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is ProgressBar progressBar)
                    {
                        progressBar.CoerceRangeAndValue();
                    }
                }));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(float),
            typeof(ProgressBar),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (dependencyObject, value) =>
                {
                    var numericValue = value is float v ? v : 0f;
                    if (dependencyObject is not ProgressBar progressBar)
                    {
                        return numericValue;
                    }

                    var min = progressBar.Minimum;
                    var max = progressBar.Maximum;
                    if (numericValue < min)
                    {
                        return min;
                    }

                    if (numericValue > max)
                    {
                        return max;
                    }

                    return numericValue;
                }));

    public static readonly DependencyProperty IsIndeterminateProperty =
        DependencyProperty.Register(
            nameof(IsIndeterminate),
            typeof(bool),
            typeof(ProgressBar),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(ProgressBar),
            new FrameworkPropertyMetadata(new Color(24, 24, 24), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(ProgressBar),
            new FrameworkPropertyMetadata(new Color(72, 146, 210), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(ProgressBar),
            new FrameworkPropertyMetadata(new Color(98, 98, 98), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(ProgressBar),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float thickness && thickness >= 0f ? thickness : 0f));

    private float _indeterminatePhase;

    public ProgressBar()
    {
        IsHitTestVisible = false;
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

    public bool IsIndeterminate
    {
        get => GetValue<bool>(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    public Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Color Foreground
    {
        get => GetValue<Color>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public Color BorderBrush
    {
        get => GetValue<Color>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public float BorderThickness
    {
        get => GetValue<float>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        if (Orientation == Orientation.Horizontal)
        {
            desired.X = MathF.Max(desired.X, 120f);
            desired.Y = MathF.Max(desired.Y, 18f);
        }
        else
        {
            desired.X = MathF.Max(desired.X, 18f);
            desired.Y = MathF.Max(desired.Y, 120f);
        }

        return desired;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!IsIndeterminate)
        {
            return;
        }

        var deltaSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (deltaSeconds <= 0f)
        {
            return;
        }

        _indeterminatePhase += deltaSeconds * 0.9f;
        if (_indeterminatePhase > 1f)
        {
            _indeterminatePhase -= MathF.Floor(_indeterminatePhase);
        }
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var slot = LayoutSlot;
        var border = BorderThickness;
        var inner = new LayoutRect(
            slot.X + border,
            slot.Y + border,
            MathF.Max(0f, slot.Width - (border * 2f)),
            MathF.Max(0f, slot.Height - (border * 2f)));

        UiDrawing.DrawFilledRect(spriteBatch, slot, Background, Opacity);
        if (border > 0f)
        {
            UiDrawing.DrawRectStroke(spriteBatch, slot, border, BorderBrush, Opacity);
        }

        if (inner.Width <= 0f || inner.Height <= 0f)
        {
            return;
        }

        if (IsIndeterminate)
        {
            DrawIndeterminateFill(spriteBatch, inner);
            return;
        }

        var normalized = GetNormalizedValue();
        if (normalized <= 0f)
        {
            return;
        }

        if (Orientation == Orientation.Horizontal)
        {
            var width = inner.Width * normalized;
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(inner.X, inner.Y, width, inner.Height),
                Foreground,
                Opacity);
            return;
        }

        var height = inner.Height * normalized;
        UiDrawing.DrawFilledRect(
            spriteBatch,
            new LayoutRect(inner.X, inner.Y + (inner.Height - height), inner.Width, height),
            Foreground,
            Opacity);
    }

    private void DrawIndeterminateFill(SpriteBatch spriteBatch, LayoutRect inner)
    {
        const float chunkRatio = 0.32f;

        if (Orientation == Orientation.Horizontal)
        {
            var segment = MathF.Max(6f, inner.Width * chunkRatio);
            var travel = inner.Width + segment;
            var start = inner.X + (_indeterminatePhase * travel) - segment;
            var end = start + segment;
            var visibleStart = MathF.Max(inner.X, start);
            var visibleEnd = MathF.Min(inner.X + inner.Width, end);
            var visibleWidth = MathF.Max(0f, visibleEnd - visibleStart);
            if (visibleWidth > 0f)
            {
                UiDrawing.DrawFilledRect(
                    spriteBatch,
                    new LayoutRect(visibleStart, inner.Y, visibleWidth, inner.Height),
                    Foreground,
                    Opacity);
            }

            return;
        }

        var segmentHeight = MathF.Max(6f, inner.Height * chunkRatio);
        var travelHeight = inner.Height + segmentHeight;
        var endY = inner.Y + inner.Height - (_indeterminatePhase * travelHeight) + segmentHeight;
        var startY = endY - segmentHeight;
        var visibleStartY = MathF.Max(inner.Y, startY);
        var visibleEndY = MathF.Min(inner.Y + inner.Height, endY);
        var visibleHeight = MathF.Max(0f, visibleEndY - visibleStartY);
        if (visibleHeight > 0f)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(inner.X, visibleStartY, inner.Width, visibleHeight),
                Foreground,
                Opacity);
        }
    }

    private float GetNormalizedValue()
    {
        var range = Maximum - Minimum;
        if (range <= 0f)
        {
            return 0f;
        }

        var normalized = (Value - Minimum) / range;
        return MathF.Max(0f, MathF.Min(1f, normalized));
    }

    private void CoerceRangeAndValue()
    {
        var min = MathF.Min(Minimum, Maximum);
        var max = MathF.Max(Minimum, Maximum);

        if (MathF.Abs(min - Minimum) > 0.0001f)
        {
            SetValue(MinimumProperty, min);
        }

        if (MathF.Abs(max - Maximum) > 0.0001f)
        {
            SetValue(MaximumProperty, max);
        }

        Value = MathF.Max(min, MathF.Min(max, Value));
    }
}
