using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class UserControl : ContentControl
{
    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(UserControl),
            new FrameworkPropertyMetadata(Color.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(UserControl),
            new FrameworkPropertyMetadata(Color.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(Thickness),
            typeof(UserControl),
            new FrameworkPropertyMetadata(
                Thickness.Empty,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(UserControl),
            new FrameworkPropertyMetadata(
                Thickness.Empty,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    // WPF-like mental model: UserControl hosts a single visual root.
    public new UIElement? Content
    {
        get => base.Content as UIElement;
        set => base.Content = value;
    }

    public Color BorderBrush
    {
        get => GetValue<Color>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public Thickness BorderThickness
    {
        get => GetValue<Thickness>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public Thickness Padding
    {
        get => GetValue<Thickness>(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        if (args.Property == ContentProperty &&
            args.NewValue != null &&
            args.NewValue is not UIElement)
        {
            throw new InvalidOperationException(
                "UserControl.Content must be a UIElement. Wrap non-visual data in a visual element.");
        }

        if (args.Property == TemplateProperty &&
            args.NewValue != null)
        {
            throw new NotSupportedException(
                "UserControl does not support custom ControlTemplate. Compose the visual tree through Content instead.");
        }

        base.OnDependencyPropertyChanged(args);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var measured = base.MeasureOverride(availableSize);
        var chrome = GetChromeThickness();
        return new Vector2(measured.X + chrome.Horizontal, measured.Y + chrome.Vertical);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        base.ArrangeOverride(finalSize);

        if (ContentElement is FrameworkElement content)
        {
            var chrome = GetChromeThickness();
            content.Arrange(new LayoutRect(
                LayoutSlot.X + chrome.Left,
                LayoutSlot.Y + chrome.Top,
                MathF.Max(0f, finalSize.X - chrome.Horizontal),
                MathF.Max(0f, finalSize.Y - chrome.Vertical)));
        }

        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        base.OnRender(spriteBatch);

        var slot = LayoutSlot;
        var border = BorderThickness;
        UiDrawing.DrawFilledRect(spriteBatch, slot, Background, Opacity);

        if (border.Left > 0f)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(slot.X, slot.Y, border.Left, slot.Height),
                BorderBrush,
                Opacity);
        }

        if (border.Right > 0f)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(slot.X + slot.Width - border.Right, slot.Y, border.Right, slot.Height),
                BorderBrush,
                Opacity);
        }

        if (border.Top > 0f)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(slot.X, slot.Y, slot.Width, border.Top),
                BorderBrush,
                Opacity);
        }

        if (border.Bottom > 0f)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(slot.X, slot.Y + slot.Height - border.Bottom, slot.Width, border.Bottom),
                BorderBrush,
                Opacity);
        }
    }

    private Thickness GetChromeThickness()
    {
        var border = BorderThickness;
        var padding = Padding;
        return new Thickness(
            border.Left + padding.Left,
            border.Top + padding.Top,
            border.Right + padding.Right,
            border.Bottom + padding.Bottom);
    }
}
