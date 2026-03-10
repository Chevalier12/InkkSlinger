using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public class Button : ContentControl
{
    private static readonly System.Lazy<Style> DefaultButtonStyle = new(BuildDefaultButtonStyle);
    private bool _isSyncingTemplateContent;
    private bool _isTextMirroringTemplateContent;
    private bool _hasExplicitContentOverride;
    private bool _hasTextLayoutCache;
    private int _textLayoutCacheTextVersion = -1;
    private float _textLayoutCacheWidth = float.NaN;
    private SpriteFont? _textLayoutCacheFont;
    private float _textLayoutCacheFontSize = float.NaN;
    private TextWrapping _textLayoutCacheWrapping = TextWrapping.NoWrap;
    private TextLayout.TextLayoutResult _textLayoutCacheResult = TextLayout.TextLayoutResult.Empty;
    private int _textVersion;

    public static readonly RoutedEvent ClickEvent =
        new(nameof(Click), RoutingStrategy.Bubble);

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(Button),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty FontProperty = Control.FontProperty;

    public new static readonly DependencyProperty ForegroundProperty =
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

    public new static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(Button),
            new FrameworkPropertyMetadata(new Color(45, 45, 45), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(Button),
            new FrameworkPropertyMetadata(new Color(185, 185, 185), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(Button),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float f && f >= 0f ? f : 0f));

    public new static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(Button),
            new FrameworkPropertyMetadata(new Thickness(10f, 6f, 10f, 6f), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty IsMouseOverProperty =
        DependencyProperty.Register(
            nameof(IsMouseOver),
            typeof(bool),
            typeof(Button),
            new FrameworkPropertyMetadata(false));

    public new static readonly DependencyProperty IsPressedProperty =
        DependencyProperty.Register(
            nameof(IsPressed),
            typeof(bool),
            typeof(Button),
            new FrameworkPropertyMetadata(false));

    public Button()
    {
        RecognizesAccessKey = true;
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

    public new SpriteFont? Font
    {
        get => GetValue<SpriteFont>(FontProperty);
        set => SetValue(FontProperty, value);
    }

    public new Color Foreground
    {
        get => GetValue<Color>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public TextWrapping TextWrapping
    {
        get => GetValue<TextWrapping>(TextWrappingProperty);
        set => SetValue(TextWrappingProperty, value);
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

    public new float BorderThickness
    {
        get => GetValue<float>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public new Thickness Padding
    {
        get => GetValue<Thickness>(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    public new bool IsMouseOver
    {
        get => GetValue<bool>(IsMouseOverProperty);
        private set => SetValue(IsMouseOverProperty, value);
    }

    public new bool IsPressed
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

        if (!string.IsNullOrEmpty(Text))
        {
            var textAvailableWidth = TextWrapping == TextWrapping.NoWrap
                ? float.PositiveInfinity
                : innerAvailableWidth;
            var textSize = ResolveTextLayout(textAvailableWidth).Size;
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
        UiRoot.Current?.Automation.NotifyInvoke(this);
        ExecuteCommand();
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        SyncTemplateContentFromTextIfNeeded();
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (args.Property == ContentProperty)
        {
            if (!_isSyncingTemplateContent)
            {
                _hasExplicitContentOverride = HasLocalValue(ContentProperty);
                _isTextMirroringTemplateContent = false;
            }

            return;
        }

        if (args.Property == TextProperty)
        {
            _textVersion++;
            InvalidateTextLayoutCache();
        }
        else if (args.Property == FontProperty ||
                 args.Property == FontSizeProperty ||
                 args.Property == TextWrappingProperty)
        {
            InvalidateTextLayoutCache();
        }

        if (args.Property == TextProperty || args.Property == TemplateProperty)
        {
            SyncTemplateContentFromTextIfNeeded();
        }
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        base.OnRender(spriteBatch);

        if (HasTemplateRoot)
        {
            return;
        }

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

        if (string.IsNullOrEmpty(Text))
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
        var layout = ResolveTextLayout(availableWidth);
        var textX = left + ((maxTextWidth - layout.Size.X) / 2f);
        var textY = top + ((maxTextHeight - layout.Size.Y) / 2f);

        var lineSpacing = FontStashTextRenderer.GetLineHeight(Font, FontSize);
        for (var i = 0; i < layout.Lines.Count; i++)
        {
            var line = layout.Lines[i];
            if (line.Length == 0)
            {
                continue;
            }

            var lineWidth = FontStashTextRenderer.MeasureWidth(Font, line, FontSize);
            var lineX = textX + ((layout.Size.X - lineWidth) / 2f);
            var linePosition = new Vector2(lineX, textY + (i * lineSpacing));
            FontStashTextRenderer.DrawString(spriteBatch, Font, line, linePosition, Foreground * Opacity, FontSize);
        }
    }

    private void SyncTemplateContentFromTextIfNeeded()
    {
        if (!HasTemplateRoot)
        {
            if (_isTextMirroringTemplateContent && !_hasExplicitContentOverride)
            {
                _isSyncingTemplateContent = true;
                try
                {
                    Content = null;
                    _isTextMirroringTemplateContent = false;
                }
                finally
                {
                    _isSyncingTemplateContent = false;
                }
            }

            return;
        }

        if (_hasExplicitContentOverride)
        {
            return;
        }

        var nextContent = string.IsNullOrEmpty(Text) ? null : Text;
        if (Equals(Content, nextContent) && _isTextMirroringTemplateContent)
        {
            return;
        }

        _isSyncingTemplateContent = true;
        try
        {
            Content = nextContent;
            _isTextMirroringTemplateContent = true;
        }
        finally
        {
            _isSyncingTemplateContent = false;
        }
    }

    private TextLayout.TextLayoutResult ResolveTextLayout(float availableWidth)
    {
        if (_hasTextLayoutCache &&
            _textLayoutCacheTextVersion == _textVersion &&
            ReferenceEquals(_textLayoutCacheFont, Font) &&
            WidthMatches(_textLayoutCacheFontSize, FontSize) &&
            _textLayoutCacheWrapping == TextWrapping &&
            WidthMatches(_textLayoutCacheWidth, availableWidth))
        {
            return _textLayoutCacheResult;
        }

        var result = TextLayout.Layout(Text, Font, FontSize, availableWidth, TextWrapping);
        _textLayoutCacheTextVersion = _textVersion;
        _textLayoutCacheWidth = availableWidth;
        _textLayoutCacheFont = Font;
        _textLayoutCacheFontSize = FontSize;
        _textLayoutCacheWrapping = TextWrapping;
        _textLayoutCacheResult = result;
        _hasTextLayoutCache = true;
        return result;
    }

    private void InvalidateTextLayoutCache()
    {
        _hasTextLayoutCache = false;
        _textLayoutCacheTextVersion = -1;
        _textLayoutCacheWidth = float.NaN;
        _textLayoutCacheFont = null;
        _textLayoutCacheFontSize = float.NaN;
        _textLayoutCacheWrapping = TextWrapping.NoWrap;
        _textLayoutCacheResult = TextLayout.TextLayoutResult.Empty;
    }

    private static bool WidthMatches(float cached, float current)
    {
        if (float.IsNaN(cached) && float.IsNaN(current))
        {
            return true;
        }

        if (float.IsInfinity(cached) || float.IsInfinity(current))
        {
            return float.IsPositiveInfinity(cached) == float.IsPositiveInfinity(current) &&
                   float.IsNegativeInfinity(cached) == float.IsNegativeInfinity(current);
        }

        return MathF.Abs(cached - current) < 0.01f;
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


