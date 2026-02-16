using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public class Button : ContentControl
{
    private static readonly System.Lazy<Style> DefaultButtonStyle = new(BuildDefaultButtonStyle);

    public static readonly RoutedEvent ClickEvent =
        new(nameof(Click), RoutingStrategy.Bubble);

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(Button),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontProperty =
        DependencyProperty.Register(
            nameof(Font),
            typeof(SpriteFont),
            typeof(Button),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(Button),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TextWrappingProperty =
        DependencyProperty.Register(
            nameof(TextWrapping),
            typeof(TextWrapping),
            typeof(Button),
            new FrameworkPropertyMetadata(
                TextWrapping.NoWrap,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(Button),
            new FrameworkPropertyMetadata(new Color(45, 45, 45), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(Button),
            new FrameworkPropertyMetadata(new Color(185, 185, 185), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(Button),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float f && f >= 0f ? f : 0f));

    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(Button),
            new FrameworkPropertyMetadata(new Thickness(10f, 6f, 10f, 6f), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsMouseOverProperty =
        DependencyProperty.Register(
            nameof(IsMouseOver),
            typeof(bool),
            typeof(Button),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsPressedProperty =
        DependencyProperty.Register(
            nameof(IsPressed),
            typeof(bool),
            typeof(Button),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public Button()
    {
    }

    public event System.EventHandler<RoutedSimpleEventArgs> Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    public string Text
    {
        get => GetValue<string>(TextProperty) ?? string.Empty;
        set => SetValue(TextProperty, value);
    }

    public SpriteFont? Font
    {
        get => GetValue<SpriteFont>(FontProperty);
        set => SetValue(FontProperty, value);
    }

    public Color Foreground
    {
        get => GetValue<Color>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public TextWrapping TextWrapping
    {
        get => GetValue<TextWrapping>(TextWrappingProperty);
        set => SetValue(TextWrappingProperty, value);
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

    public bool IsMouseOver
    {
        get => GetValue<bool>(IsMouseOverProperty);
        private set => SetValue(IsMouseOverProperty, value);
    }

    public bool IsPressed
    {
        get => GetValue<bool>(IsPressedProperty);
        private set => SetValue(IsPressedProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        var padding = Padding;
        var border = BorderThickness * 2f;
        var innerAvailableWidth = MathF.Max(0f, availableSize.X - padding.Horizontal - border);

        if (Font != null && !string.IsNullOrEmpty(Text))
        {
            var textAvailableWidth = TextWrapping == TextWrapping.NoWrap
                ? float.PositiveInfinity
                : innerAvailableWidth;
            var textSize = TextLayout.Layout(Text, Font, textAvailableWidth, TextWrapping).Size;
            desired.X = System.MathF.Max(desired.X, textSize.X + padding.Horizontal + border);
            desired.Y = System.MathF.Max(desired.Y, textSize.Y + padding.Vertical + border);
            return desired;
        }

        desired.X = System.MathF.Max(desired.X, padding.Horizontal + border);
        desired.Y = System.MathF.Max(desired.Y, padding.Vertical + border);
        return desired;
    }


    protected virtual void OnClick()
    {
        RaiseClickEvent();
        ExecuteCommand();
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        base.OnRender(spriteBatch);

        var slot = LayoutSlot;
        UiDrawing.DrawFilledRect(spriteBatch, slot, Background, Opacity);

        if (BorderThickness > 0f)
        {
            var thickness = BorderThickness;
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(slot.X, slot.Y, slot.Width, thickness), BorderBrush, Opacity);
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(slot.X, slot.Y + slot.Height - thickness, slot.Width, thickness),
                BorderBrush,
                Opacity);
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(slot.X, slot.Y, thickness, slot.Height), BorderBrush, Opacity);
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(slot.X + slot.Width - thickness, slot.Y, thickness, slot.Height),
                BorderBrush,
                Opacity);
        }

        if (Font == null || string.IsNullOrEmpty(Text))
        {
            return;
        }

        var padding = Padding;
        var left = slot.X + padding.Left + BorderThickness;
        var right = slot.X + slot.Width - padding.Right - BorderThickness;
        var top = slot.Y + padding.Top + BorderThickness;
        var bottom = slot.Y + slot.Height - padding.Bottom - BorderThickness;

        var maxTextWidth = System.MathF.Max(0f, right - left);
        var maxTextHeight = System.MathF.Max(0f, bottom - top);
        if (maxTextWidth <= 0f || maxTextHeight <= 0f)
        {
            return;
        }

        var availableWidth = TextWrapping == TextWrapping.NoWrap
            ? float.PositiveInfinity
            : maxTextWidth;
        var layout = TextLayout.Layout(Text, Font, availableWidth, TextWrapping);
        var textX = left + ((maxTextWidth - layout.Size.X) / 2f);
        var textY = top + ((maxTextHeight - layout.Size.Y) / 2f);

        var lineSpacing = FontStashTextRenderer.GetLineHeight(Font);
        for (var i = 0; i < layout.Lines.Count; i++)
        {
            var line = layout.Lines[i];
            if (line.Length == 0)
            {
                continue;
            }

            var lineWidth = FontStashTextRenderer.MeasureWidth(Font, line);
            var lineX = textX + ((layout.Size.X - lineWidth) / 2f);
            var linePosition = new Vector2(lineX, textY + (i * lineSpacing));
            FontStashTextRenderer.DrawString(spriteBatch, Font, line, linePosition, Foreground * Opacity);
        }
    }

    private static Style BuildDefaultButtonStyle()
    {
        var style = new Style(typeof(Button));

        var hoverTrigger = new Trigger(IsMouseOverProperty, true);
        hoverTrigger.Setters.Add(new Setter(BackgroundProperty, new Color(65, 65, 65)));

        var pressedTrigger = new Trigger(IsPressedProperty, true);
        pressedTrigger.Setters.Add(new Setter(BackgroundProperty, new Color(28, 28, 28)));

        var disabledTrigger = new Trigger(IsEnabledProperty, false);
        disabledTrigger.Setters.Add(new Setter(BackgroundProperty, new Color(34, 34, 34)));
        disabledTrigger.Setters.Add(new Setter(ForegroundProperty, new Color(180, 180, 180)));

        style.Triggers.Add(hoverTrigger);
        style.Triggers.Add(pressedTrigger);
        style.Triggers.Add(disabledTrigger);

        return style;
    }

    protected override Style? GetFallbackStyle()
    {
        return DefaultButtonStyle.Value;
    }

    private void RaiseClickEvent()
    {
        var args = new RoutedSimpleEventArgs(ClickEvent);
        RaiseRoutedEvent(ClickEvent, args);
    }

    internal void SetMouseOverFromInput(bool isMouseOver)
    {
        if (IsMouseOver == isMouseOver)
        {
            return;
        }

        IsMouseOver = isMouseOver;
    }

    internal void SetPressedFromInput(bool isPressed)
    {
        if (IsPressed == isPressed)
        {
            return;
        }

        IsPressed = isPressed;
    }

    internal void InvokeFromInput()
    {
        OnClick();
    }
}

