using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class ListBoxItem : ContentControl
{
    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(
            nameof(IsSelected),
            typeof(bool),
            typeof(ListBoxItem),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(ListBoxItem),
            new FrameworkPropertyMetadata(new Color(26, 26, 26), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedBackgroundProperty =
        DependencyProperty.Register(
            nameof(SelectedBackground),
            typeof(Color),
            typeof(ListBoxItem),
            new FrameworkPropertyMetadata(new Color(55, 98, 145), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(ListBoxItem),
            new FrameworkPropertyMetadata(new Color(90, 90, 90), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(ListBoxItem),
            new FrameworkPropertyMetadata(new Thickness(8f, 6f, 8f, 6f), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public bool IsSelected
    {
        get => GetValue<bool>(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
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

    public Thickness Padding
    {
        get => GetValue<Thickness>(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        var padding = Padding;
        desired.X += padding.Horizontal;
        desired.Y += padding.Vertical;
        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var content = ContentElement as FrameworkElement;
        if (content != null)
        {
            var padding = Padding;
            content.Arrange(
                new LayoutRect(
                    LayoutSlot.X + padding.Left,
                    LayoutSlot.Y + padding.Top,
                    System.MathF.Max(0f, finalSize.X - padding.Horizontal),
                    System.MathF.Max(0f, finalSize.Y - padding.Vertical)));
        }

        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var slot = LayoutSlot;
        UiDrawing.DrawFilledRect(spriteBatch, slot, IsSelected ? SelectedBackground : Background, Opacity);
        UiDrawing.DrawRectStroke(spriteBatch, slot, 1f, BorderBrush, Opacity);
    }
}
