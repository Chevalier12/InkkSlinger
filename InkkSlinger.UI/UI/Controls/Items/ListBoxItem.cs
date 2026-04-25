using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class ListBoxItem : ContentControl
{
    public static readonly RoutedEvent SelectedEvent =
        new(nameof(Selected), RoutingStrategy.Bubble);

    public static readonly RoutedEvent UnselectedEvent =
        new(nameof(Unselected), RoutingStrategy.Bubble);

    public new static readonly DependencyProperty IsMouseOverProperty =
        DependencyProperty.Register(
            nameof(IsMouseOver),
            typeof(bool),
            typeof(ListBoxItem),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(
            nameof(IsSelected),
            typeof(bool),
            typeof(ListBoxItem),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is ListBoxItem listBoxItem &&
                        args.NewValue is bool isSelected &&
                        args.OldValue is bool wasSelected &&
                        isSelected != wasSelected)
                    {
                        var routedEvent = isSelected ? SelectedEvent : UnselectedEvent;
                        listBoxItem.RaiseRoutedEvent(routedEvent, new RoutedSimpleEventArgs(routedEvent));
                    }
                }));

    public new static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(ListBoxItem),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BackgroundProperty =
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

    public new static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(ListBoxItem),
            new FrameworkPropertyMetadata(new Color(90, 90, 90), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(ListBoxItem),
            new FrameworkPropertyMetadata(new Thickness(8f, 6f, 8f, 6f), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public new bool IsSelected
    {
        get => GetValue<bool>(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public event EventHandler<RoutedSimpleEventArgs> Selected
    {
        add => AddHandler(SelectedEvent, value);
        remove => RemoveHandler(SelectedEvent, value);
    }

    public event EventHandler<RoutedSimpleEventArgs> Unselected
    {
        add => AddHandler(UnselectedEvent, value);
        remove => RemoveHandler(UnselectedEvent, value);
    }

    public new bool IsMouseOver
    {
        get => GetValue<bool>(IsMouseOverProperty);
        set => SetValue(IsMouseOverProperty, value);
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

    public new Thickness Padding
    {
        get => GetValue<Thickness>(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        if (HasTemplateRoot)
        {
            return base.MeasureOverride(availableSize);
        }

        EnsureContentElementForLayout();

        var padding = Padding;
        var content = ContentElement as FrameworkElement;
        if (content == null)
        {
            return new Vector2(padding.Horizontal, padding.Vertical);
        }

        content.Measure(
            new Vector2(
                System.MathF.Max(0f, availableSize.X - padding.Horizontal),
                System.MathF.Max(0f, availableSize.Y - padding.Vertical)));

        return new Vector2(
            content.DesiredSize.X + padding.Horizontal,
            content.DesiredSize.Y + padding.Vertical);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        base.ArrangeOverride(finalSize);

        if (HasTemplateRoot)
        {
            return finalSize;
        }

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
        base.OnRender(spriteBatch);

        if (HasTemplateRoot)
        {
            return;
        }

        var slot = LayoutSlot;
        UiDrawing.DrawFilledRect(spriteBatch, slot, IsSelected ? SelectedBackground : Background, Opacity);
        UiDrawing.DrawRectStroke(spriteBatch, slot, 1f, BorderBrush, Opacity);
    }
}
