using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class StatusBarItem : ContentControl
{
    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(StatusBarItem),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontProperty =
        DependencyProperty.Register(
            nameof(Font),
            typeof(SpriteFont),
            typeof(StatusBarItem),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(StatusBarItem),
            new FrameworkPropertyMetadata(Color.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(StatusBarItem),
            new FrameworkPropertyMetadata(Color.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(StatusBarItem),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float v && v >= 0f ? v : 0f));

    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(StatusBarItem),
            new FrameworkPropertyMetadata(
                new Thickness(6f, 3f, 6f, 3f),
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty HorizontalContentAlignmentProperty =
        DependencyProperty.Register(
            nameof(HorizontalContentAlignment),
            typeof(HorizontalAlignment),
            typeof(StatusBarItem),
            new FrameworkPropertyMetadata(HorizontalAlignment.Left, FrameworkPropertyMetadataOptions.AffectsArrange));

    public Color Foreground
    {
        get => GetValue<Color>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public SpriteFont? Font
    {
        get => GetValue<SpriteFont>(FontProperty);
        set => SetValue(FontProperty, value);
    }

    public Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
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

    public Thickness Padding
    {
        get => GetValue<Thickness>(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    public HorizontalAlignment HorizontalContentAlignment
    {
        get => GetValue<HorizontalAlignment>(HorizontalContentAlignmentProperty);
        set => SetValue(HorizontalContentAlignmentProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var contentDesired = base.MeasureOverride(availableSize);
        var padding = Padding;
        var border = BorderThickness * 2f;

        if (ContentElement == null && Content is string text)
        {
            var width = FontStashTextRenderer.MeasureWidth(Font, text);
            var height = FontStashTextRenderer.GetLineHeight(Font);
            contentDesired = new Vector2(MathF.Max(contentDesired.X, width), MathF.Max(contentDesired.Y, height));
        }

        return new Vector2(
            contentDesired.X + padding.Horizontal + border,
            contentDesired.Y + padding.Vertical + border);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var padding = Padding;
        var border = BorderThickness;

        if (ContentElement is FrameworkElement content)
        {
            var contentRect = new LayoutRect(
                LayoutSlot.X + border + padding.Left,
                LayoutSlot.Y + border + padding.Top,
                MathF.Max(0f, finalSize.X - (border * 2f) - padding.Horizontal),
                MathF.Max(0f, finalSize.Y - (border * 2f) - padding.Vertical));
            content.Arrange(contentRect);
        }

        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        if (Background.A > 0)
        {
            UiDrawing.DrawFilledRect(spriteBatch, LayoutSlot, Background, Opacity);
        }

        if (BorderThickness > 0f)
        {
            UiDrawing.DrawRectStroke(spriteBatch, LayoutSlot, BorderThickness, BorderBrush, Opacity);
        }

        if (ContentElement == null && Content is string text && !string.IsNullOrEmpty(text))
        {
            var padding = Padding;
            var textX = LayoutSlot.X + BorderThickness + padding.Left;
            var textY = LayoutSlot.Y + ((LayoutSlot.Height - FontStashTextRenderer.GetLineHeight(Font)) / 2f);
            FontStashTextRenderer.DrawString(spriteBatch, Font, text, new Vector2(textX, textY), Foreground * Opacity);
        }
    }
}
