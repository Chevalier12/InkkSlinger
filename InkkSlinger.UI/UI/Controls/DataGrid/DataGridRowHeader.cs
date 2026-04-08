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
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(DataGridRowHeader),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(DataGridRowHeader),
            new FrameworkPropertyMetadata(new Color(30, 41, 58), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(DataGridRowHeader),
            new FrameworkPropertyMetadata(new Color(57, 80, 111), FrameworkPropertyMetadataOptions.AffectsRender));

    public string Text
    {
        get => GetValue<string>(TextProperty) ?? string.Empty;
        set => SetValue(TextProperty, value);
    }

    public new Color Foreground
    {
        get => GetValue<Color>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public new Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public new Color BorderBrush
    {
        get => GetValue<Color>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        if (float.IsFinite(availableSize.X) && float.IsFinite(availableSize.Y))
        {
            return new Vector2(
                System.MathF.Max(0f, availableSize.X),
                System.MathF.Max(0f, availableSize.Y));
        }

        return new Vector2(
            UiTextRenderer.MeasureWidth(this, Text, FontSize) + 10f,
            UiTextRenderer.GetLineHeight(this, FontSize) + 8f);
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
        var y = LayoutSlot.Y + ((LayoutSlot.Height - UiTextRenderer.GetLineHeight(this, FontSize)) / 2f);
        UiTextRenderer.DrawString(spriteBatch, this, Text, new Vector2(x, y), Foreground * Opacity, FontSize, opaqueBackground: true);
    }
}


