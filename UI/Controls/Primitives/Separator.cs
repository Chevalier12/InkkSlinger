using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class Separator : Control
{
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(Separator),
            new FrameworkPropertyMetadata(
                Orientation.Horizontal,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThicknessProperty =
        DependencyProperty.Register(
            nameof(Thickness),
            typeof(float),
            typeof(Separator),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float thickness && thickness >= 1f ? thickness : 1f));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(Separator),
            new FrameworkPropertyMetadata(new Color(112, 112, 112), FrameworkPropertyMetadataOptions.AffectsRender));

    public Separator()
    {
        IsHitTestVisible = false;
    }

    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public float Thickness
    {
        get => GetValue<float>(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public Color Foreground
    {
        get => GetValue<Color>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        if (Orientation == Orientation.Horizontal)
        {
            desired.Y = MathF.Max(desired.Y, Thickness);
            desired.X = MathF.Max(desired.X, 8f);
        }
        else
        {
            desired.X = MathF.Max(desired.X, Thickness);
            desired.Y = MathF.Max(desired.Y, 8f);
        }

        return desired;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var slot = LayoutSlot;
        if (Orientation == Orientation.Horizontal)
        {
            var y = slot.Y + ((slot.Height - Thickness) / 2f);
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(slot.X, y, slot.Width, Thickness), Foreground, Opacity);
            return;
        }

        var x = slot.X + ((slot.Width - Thickness) / 2f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x, slot.Y, Thickness, slot.Height), Foreground, Opacity);
    }
}
