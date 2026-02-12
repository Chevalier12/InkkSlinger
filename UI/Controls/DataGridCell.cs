using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class DataGridCell : Control
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(object),
            typeof(DataGridCell),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(
            nameof(IsSelected),
            typeof(bool),
            typeof(DataGridCell),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(DataGridCell),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(DataGridCell),
            new FrameworkPropertyMetadata(new Color(24, 34, 49), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedBackgroundProperty =
        DependencyProperty.Register(
            nameof(SelectedBackground),
            typeof(Color),
            typeof(DataGridCell),
            new FrameworkPropertyMetadata(new Color(61, 99, 145), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(DataGridCell),
            new FrameworkPropertyMetadata(new Color(57, 80, 111), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontProperty =
        DependencyProperty.Register(
            nameof(Font),
            typeof(SpriteFont),
            typeof(DataGridCell),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public object? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public bool IsSelected
    {
        get => GetValue<bool>(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
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

    public Color SelectedBackground
    {
        get => GetValue<Color>(SelectedBackgroundProperty);
        set => SetValue(SelectedBackgroundProperty, value);
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

    internal int ColumnIndex { get; set; }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var text = Value?.ToString() ?? string.Empty;
        var width = FontStashTextRenderer.MeasureWidth(Font, text) + 12f;
        var height = FontStashTextRenderer.GetLineHeight(Font) + 8f;
        return new Vector2(width, height);
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var fill = IsSelected ? SelectedBackground : Background;
        UiDrawing.DrawFilledRect(spriteBatch, LayoutSlot, fill, Opacity);
        UiDrawing.DrawRectStroke(spriteBatch, LayoutSlot, 1f, BorderBrush, Opacity);

        var text = Value?.ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var x = LayoutSlot.X + 6f;
        var y = LayoutSlot.Y + ((LayoutSlot.Height - FontStashTextRenderer.GetLineHeight(Font)) / 2f);
        FontStashTextRenderer.DrawString(spriteBatch, Font, text, new Vector2(x, y), Foreground * Opacity);
    }
}
