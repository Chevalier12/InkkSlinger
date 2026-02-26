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
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(
            nameof(IsSelected),
            typeof(bool),
            typeof(DataGridCell),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(DataGridCell),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BackgroundProperty =
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

    public new static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(DataGridCell),
            new FrameworkPropertyMetadata(new Color(57, 80, 111), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty FontProperty =
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

    public new bool IsSelected
    {
        get => GetValue<bool>(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
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

    public Color SelectedBackground
    {
        get => GetValue<Color>(SelectedBackgroundProperty);
        set => SetValue(SelectedBackgroundProperty, value);
    }

    public new Color BorderBrush
    {
        get => GetValue<Color>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public new SpriteFont? Font
    {
        get => GetValue<SpriteFont>(FontProperty);
        set => SetValue(FontProperty, value);
    }

    internal int ColumnIndex { get; set; }

    internal bool ShowHorizontalGridLine { get; set; } = true;

    internal bool ShowVerticalGridLine { get; set; } = true;

    internal Color HorizontalGridLineBrush { get; set; } = new Color(57, 80, 111);

    internal Color VerticalGridLineBrush { get; set; } = new Color(57, 80, 111);

    internal bool ShowHorizontalGridLineForTesting => ShowHorizontalGridLine;

    internal bool ShowVerticalGridLineForTesting => ShowVerticalGridLine;

    internal Color HorizontalGridLineBrushForTesting => HorizontalGridLineBrush;

    internal Color VerticalGridLineBrushForTesting => VerticalGridLineBrush;

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

        if (ShowHorizontalGridLine && LayoutSlot.Width > 0f && LayoutSlot.Height > 0f)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(LayoutSlot.X, LayoutSlot.Y + LayoutSlot.Height - 1f, LayoutSlot.Width, 1f),
                HorizontalGridLineBrush,
                Opacity);
        }

        if (ShowVerticalGridLine && LayoutSlot.Width > 0f && LayoutSlot.Height > 0f)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(LayoutSlot.X + LayoutSlot.Width - 1f, LayoutSlot.Y, 1f, LayoutSlot.Height),
                VerticalGridLineBrush,
                Opacity);
        }

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

