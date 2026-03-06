using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class DataGridCell : Control
{
    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(
            nameof(Content),
            typeof(object),
            typeof(DataGridCell),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is not DataGridCell cell || cell._isSynchronizingContentValue)
                    {
                        return;
                    }

                    cell.SyncAliasValue(ContentProperty, ValueProperty, args.NewValue);
                }));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(object),
            typeof(DataGridCell),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is not DataGridCell cell || cell._isSynchronizingContentValue)
                    {
                        return;
                    }

                    cell.SyncAliasValue(ValueProperty, ContentProperty, args.NewValue);
                }));

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

    private bool _isSynchronizingContentValue;

    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

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
        var desired = base.MeasureOverride(availableSize);
        if (HasTemplateRoot)
        {
            return desired;
        }

        var text = Value?.ToString() ?? string.Empty;
        var padding = Padding;
        var border = BorderThickness;
        var chromeWidth = padding.Left + padding.Right + border.Left + border.Right;
        var chromeHeight = padding.Top + padding.Bottom + border.Top + border.Bottom;
        var width = FontStashTextRenderer.MeasureWidth(Font, text) + chromeWidth;
        var height = FontStashTextRenderer.GetLineHeight(Font) + chromeHeight;
        desired.X = System.MathF.Max(desired.X, width);
        desired.Y = System.MathF.Max(desired.Y, height);
        return desired;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        base.OnRender(spriteBatch);

        if (!HasTemplateRoot)
        {
            var hasStyleDrivenBackground = GetValueSource(BackgroundProperty) != DependencyPropertyValueSource.Default;
            var fill = hasStyleDrivenBackground
                ? Background
                : (IsSelected ? SelectedBackground : Background);
            UiDrawing.DrawFilledRect(spriteBatch, LayoutSlot, fill, Opacity);

            var border = BorderThickness;
            if (border.Left > 0f)
            {
                UiDrawing.DrawFilledRect(
                    spriteBatch,
                    new LayoutRect(LayoutSlot.X, LayoutSlot.Y, border.Left, LayoutSlot.Height),
                    BorderBrush,
                    Opacity);
            }

            if (border.Right > 0f)
            {
                UiDrawing.DrawFilledRect(
                    spriteBatch,
                    new LayoutRect(LayoutSlot.X + LayoutSlot.Width - border.Right, LayoutSlot.Y, border.Right, LayoutSlot.Height),
                    BorderBrush,
                    Opacity);
            }

            if (border.Top > 0f)
            {
                UiDrawing.DrawFilledRect(
                    spriteBatch,
                    new LayoutRect(LayoutSlot.X, LayoutSlot.Y, LayoutSlot.Width, border.Top),
                    BorderBrush,
                    Opacity);
            }

            if (border.Bottom > 0f)
            {
                UiDrawing.DrawFilledRect(
                    spriteBatch,
                    new LayoutRect(LayoutSlot.X, LayoutSlot.Y + LayoutSlot.Height - border.Bottom, LayoutSlot.Width, border.Bottom),
                    BorderBrush,
                    Opacity);
            }
        }

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

        if (HasTemplateRoot)
        {
            return;
        }

        var text = Value?.ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var padding = Padding;
        var borderThickness = BorderThickness;
        var left = LayoutSlot.X + borderThickness.Left + padding.Left;
        var top = LayoutSlot.Y + borderThickness.Top + padding.Top;
        var bottom = LayoutSlot.Y + LayoutSlot.Height - borderThickness.Bottom - padding.Bottom;
        var contentHeight = System.MathF.Max(0f, bottom - top);
        var lineHeight = FontStashTextRenderer.GetLineHeight(Font);
        var x = left;
        var y = top + ((contentHeight - lineHeight) / 2f);
        FontStashTextRenderer.DrawString(spriteBatch, Font, text, new Vector2(x, y), Foreground * Opacity);
    }

    private void SyncAliasValue(DependencyProperty? sourceProperty, DependencyProperty? targetProperty, object? value)
    {
        if (sourceProperty == null || targetProperty == null)
        {
            return;
        }

        _isSynchronizingContentValue = true;
        try
        {
            if (!Equals(GetValue(targetProperty), value))
            {
                SetValue(targetProperty, value);
            }
        }
        finally
        {
            _isSynchronizingContentValue = false;
        }
    }
}

