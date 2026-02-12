using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class DataGridRowHeader : Control
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(DataGridRowHeader),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(DataGridRowHeader),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(DataGridRowHeader),
            new FrameworkPropertyMetadata(new Color(30, 41, 58), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(DataGridRowHeader),
            new FrameworkPropertyMetadata(new Color(57, 80, 111), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontProperty =
        DependencyProperty.Register(
            nameof(Font),
            typeof(SpriteFont),
            typeof(DataGridRowHeader),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public string Text
    {
        get => GetValue<string>(TextProperty) ?? string.Empty;
        set => SetValue(TextProperty, value);
    }

    public Color Foreground
    {
        get => GetValue<Color>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
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

    public SpriteFont? Font
    {
        get => GetValue<SpriteFont>(FontProperty);
        set => SetValue(FontProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        return new Vector2(
            FontStashTextRenderer.MeasureWidth(Font, Text) + 10f,
            FontStashTextRenderer.GetLineHeight(Font) + 8f);
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        UiDrawing.DrawFilledRect(spriteBatch, LayoutSlot, Background, Opacity);
        UiDrawing.DrawRectStroke(spriteBatch, LayoutSlot, 1f, BorderBrush, Opacity);

        if (string.IsNullOrWhiteSpace(Text))
        {
            return;
        }

        var x = LayoutSlot.X + 4f;
        var y = LayoutSlot.Y + ((LayoutSlot.Height - FontStashTextRenderer.GetLineHeight(Font)) / 2f);
        FontStashTextRenderer.DrawString(spriteBatch, Font, Text, new Vector2(x, y), Foreground * Opacity);
    }
}
