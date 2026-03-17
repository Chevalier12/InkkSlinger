using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public class Button : ContentControl
{
    private static readonly System.Lazy<Style> DefaultButtonStyle = new(BuildDefaultButtonStyle);
    private static int _renderLineWidthFallbackCount;
    private static long _measureOverrideElapsedTicks;
    private static long _renderElapsedTicks;
    private static long _resolveTextLayoutElapsedTicks;
    private static long _renderChromeElapsedTicks;
    private static long _renderTextPreparationElapsedTicks;
    private static long _renderTextDrawDispatchElapsedTicks;
    private static int _renderTextPreparationCallCount;
    private static int _renderTextDrawDispatchCallCount;
    private static int _contentPropertyChangedCount;
    private static int _textLayoutCacheHitCount;
    private static int _textLayoutCacheMissCount;
    private static int _intrinsicNoWrapMeasureCacheHitCount;
    private static int _intrinsicNoWrapMeasureCacheMissCount;
    private static int _textLayoutInvalidationCount;
    private static int _intrinsicNoWrapMeasureInvalidationCount;
    private static int _plainTextMeasureFastPathCount;
    private static int _intrinsicNoWrapMeasurePathCount;
    private static int _textLayoutMeasurePathCount;
    private bool _hasTextLayoutCache;
    private bool _hasIntrinsicNoWrapMeasureCache;
    private int _textLayoutCacheTextVersion = -1;
    private int _intrinsicNoWrapMeasureTextVersion = -1;
    private float _textLayoutCacheWidth = float.NaN;
    private UiTypography? _textLayoutCacheTypography;
    private UiTypography? _intrinsicNoWrapMeasureTypography;
    private float _textLayoutCacheFontSize = float.NaN;
    private float _intrinsicNoWrapMeasureFontSize = float.NaN;
    private TextWrapping _textLayoutCacheWrapping = TextWrapping.NoWrap;
    private TextLayout.TextLayoutResult _textLayoutCacheResult = TextLayout.TextLayoutResult.Empty;
    private Vector2 _intrinsicNoWrapMeasureSize = Vector2.Zero;
    private int _contentVersion;

    public static readonly RoutedEvent ClickEvent =
        new(nameof(Click), RoutingStrategy.Bubble);

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
        var start = Stopwatch.GetTimestamp();
        try
        {
            var text = GetSelfRenderedContentText();
            if (CanUsePlainTextMeasureFastPath())
            {
                _plainTextMeasureFastPathCount++;
                return MeasurePlainTextButton(availableSize, text);
            }

            var desired = base.MeasureOverride(availableSize);
            var padding = Padding;
            var border = BorderThickness * 2f;
            var innerAvailableWidth = MathF.Max(0f, availableSize.X - padding.Horizontal - border);

            if (!string.IsNullOrEmpty(text))
            {
                _textLayoutMeasurePathCount++;
                var textAvailableWidth = TextWrapping == TextWrapping.NoWrap
                    ? float.PositiveInfinity
                    : innerAvailableWidth;
                var textSize = ResolveTextLayout(text, textAvailableWidth).Size;
                desired.X = System.MathF.Max(desired.X, textSize.X + padding.Horizontal + border);
                desired.Y = System.MathF.Max(desired.Y, textSize.Y + padding.Vertical + border);
                return desired;
            }

            desired.X = System.MathF.Max(desired.X, padding.Horizontal + border);
            desired.Y = System.MathF.Max(desired.Y, padding.Vertical + border);
            return desired;
        }
        finally
        {
            _measureOverrideElapsedTicks += Stopwatch.GetTimestamp() - start;
        }
    }

    protected override bool CanReuseMeasureForAvailableSizeChange(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
    {
        _ = previousAvailableSize;
        _ = nextAvailableSize;
        return CanUseIntrinsicNoWrapTextMeasure() && CanUsePlainTextMeasureFastPath();
    }


    protected virtual void OnClick()
    {
        RaiseClickEvent();
        UiRoot.Current?.Automation.NotifyInvoke(this);
        ExecuteCommand();
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (args.Property == ContentProperty)
        {
            _contentPropertyChangedCount++;
            _contentVersion++;
            InvalidateTextLayoutCache();
            InvalidateIntrinsicNoWrapMeasureCache();
        }
        else if (args.Property == FontSizeProperty ||
                 args.Property == TextWrappingProperty ||
                 args.Property == FontFamilyProperty ||
                 args.Property == FontWeightProperty ||
                 args.Property == FontStyleProperty)
        {
            InvalidateTextLayoutCache();
            InvalidateIntrinsicNoWrapMeasureCache();
        }
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            base.OnRender(spriteBatch);

            if (HasTemplateRoot)
            {
                return;
            }

            RenderChrome(spriteBatch, LayoutSlot);

            var renderPlan = PrepareTextRenderPlan(LayoutSlot);
            if (!renderPlan.HasValue)
            {
                return;
            }

            DrawTextRenderPlan(spriteBatch, renderPlan.Value);
        }
        finally
        {
            _renderElapsedTicks += Stopwatch.GetTimestamp() - start;
        }
    }

    protected override bool ShouldCreateImplicitContentElement(object? content, DataTemplate? selectedTemplate)
    {
        return content is UIElement || selectedTemplate != null;
    }

    protected string GetDisplayContentText()
    {
        if (ContentElement != null || Content == null)
        {
            return string.Empty;
        }

        return Label.ExtractAutomationText(Content);
    }

    private string GetSelfRenderedContentText()
    {
        return CanUsePlainTextMeasureFastPath() ? GetDisplayContentText() : string.Empty;
    }

    private TextLayout.TextLayoutResult ResolveTextLayout(string text, float availableWidth)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            var typography = UiTextRenderer.ResolveTypography(this, FontSize);
            if (_hasTextLayoutCache &&
                _textLayoutCacheTextVersion == _contentVersion &&
                Nullable.Equals(_textLayoutCacheTypography, typography) &&
                WidthMatches(_textLayoutCacheFontSize, FontSize) &&
                _textLayoutCacheWrapping == TextWrapping &&
                WidthMatches(_textLayoutCacheWidth, availableWidth))
            {
                _textLayoutCacheHitCount++;
                return _textLayoutCacheResult;
            }

            _textLayoutCacheMissCount++;
            var result = TextLayout.Layout(text, typography, FontSize, availableWidth, TextWrapping);
            _textLayoutCacheTextVersion = _contentVersion;
            _textLayoutCacheWidth = availableWidth;
            _textLayoutCacheTypography = typography;
            _textLayoutCacheFontSize = FontSize;
            _textLayoutCacheWrapping = TextWrapping;
            _textLayoutCacheResult = result;
            _hasTextLayoutCache = true;
            return result;
        }
        finally
        {
            _resolveTextLayoutElapsedTicks += Stopwatch.GetTimestamp() - start;
        }
    }

    private void InvalidateTextLayoutCache()
    {
        _textLayoutInvalidationCount++;
        _hasTextLayoutCache = false;
        _textLayoutCacheTextVersion = -1;
        _textLayoutCacheWidth = float.NaN;
        _textLayoutCacheTypography = null;
        _textLayoutCacheFontSize = float.NaN;
        _textLayoutCacheWrapping = TextWrapping.NoWrap;
        _textLayoutCacheResult = TextLayout.TextLayoutResult.Empty;
    }

    private void InvalidateIntrinsicNoWrapMeasureCache()
    {
        _intrinsicNoWrapMeasureInvalidationCount++;
        _hasIntrinsicNoWrapMeasureCache = false;
        _intrinsicNoWrapMeasureTextVersion = -1;
        _intrinsicNoWrapMeasureTypography = null;
        _intrinsicNoWrapMeasureFontSize = float.NaN;
        _intrinsicNoWrapMeasureSize = Vector2.Zero;
    }

    private bool CanUsePlainTextMeasureFastPath()
    {
        return Template == null &&
               !HasTemplateRoot &&
               ContentElement == null &&
               ContentTemplate == null &&
               ContentTemplateSelector == null;
    }

    private Vector2 MeasurePlainTextButton(Vector2 availableSize, string text)
    {
        var padding = Padding;
        var border = BorderThickness * 2f;
        var desired = new Vector2(padding.Horizontal + border, padding.Vertical + border);
        if (string.IsNullOrEmpty(text))
        {
            return desired;
        }

        var innerAvailableWidth = MathF.Max(0f, availableSize.X - padding.Horizontal - border);
        var textSize = CanUseIntrinsicNoWrapTextMeasure()
            ? ResolveIntrinsicNoWrapMeasurePath(text)
            : ResolveTextLayoutMeasurePath(text, innerAvailableWidth);
        desired.X = MathF.Max(desired.X, textSize.X + padding.Horizontal + border);
        desired.Y = MathF.Max(desired.Y, textSize.Y + padding.Vertical + border);
        return desired;
    }

    private Vector2 ResolveIntrinsicNoWrapMeasurePath(string text)
    {
        _intrinsicNoWrapMeasurePathCount++;
        return ResolveIntrinsicNoWrapTextSize(text);
    }

    private Vector2 ResolveTextLayoutMeasurePath(string text, float availableWidth)
    {
        _textLayoutMeasurePathCount++;
        return ResolveTextLayout(text, availableWidth).Size;
    }

    internal bool HasAvailableIndependentDesiredSizeForUniformGrid()
    {
        return TextWrapping == TextWrapping.NoWrap && CanUsePlainTextMeasureFastPath();
    }

    private bool CanUseIntrinsicNoWrapTextMeasure()
    {
        var text = GetDisplayContentText();
        return TextWrapping == TextWrapping.NoWrap &&
               !string.IsNullOrEmpty(text) &&
               text.IndexOfAny(['\r', '\n']) < 0;
    }

    private Vector2 ResolveIntrinsicNoWrapTextSize(string text)
    {
        var typography = UiTextRenderer.ResolveTypography(this, FontSize);
        if (_hasIntrinsicNoWrapMeasureCache &&
            _intrinsicNoWrapMeasureTextVersion == _contentVersion &&
            Nullable.Equals(_intrinsicNoWrapMeasureTypography, typography) &&
            WidthMatches(_intrinsicNoWrapMeasureFontSize, FontSize))
        {
            _intrinsicNoWrapMeasureCacheHitCount++;
            return _intrinsicNoWrapMeasureSize;
        }

        _intrinsicNoWrapMeasureCacheMissCount++;
        var size = new Vector2(
            UiTextRenderer.MeasureWidth(typography, text),
            UiTextRenderer.GetLineHeight(typography));
        _intrinsicNoWrapMeasureTextVersion = _contentVersion;
        _intrinsicNoWrapMeasureTypography = typography;
        _intrinsicNoWrapMeasureFontSize = FontSize;
        _intrinsicNoWrapMeasureSize = size;
        _hasIntrinsicNoWrapMeasureCache = true;
        return size;
    }

    private void RenderChrome(SpriteBatch spriteBatch, LayoutRect slot)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            UiDrawing.DrawFilledRect(spriteBatch, slot, Background, Opacity);

            if (BorderThickness <= 0f)
            {
                return;
            }

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
        finally
        {
            _renderChromeElapsedTicks += Stopwatch.GetTimestamp() - start;
        }
    }

    private void DrawTextRenderPlan(SpriteBatch spriteBatch, ButtonTextRenderPlan renderPlan)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            for (var i = 0; i < renderPlan.LineDraws.Count; i++)
            {
                var lineDraw = renderPlan.LineDraws[i];
                UiTextRenderer.DrawString(spriteBatch, this, lineDraw.Text, lineDraw.Position, Foreground * Opacity, FontSize, opaqueBackground: true);
                _renderTextDrawDispatchCallCount++;
            }
        }
        finally
        {
            _renderTextDrawDispatchElapsedTicks += Stopwatch.GetTimestamp() - start;
        }
    }

    private ButtonTextRenderPlan? PrepareTextRenderPlan(LayoutRect slot)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            var text = GetSelfRenderedContentText();
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            var padding = Padding;
            var left = slot.X + padding.Left + BorderThickness;
            var right = slot.X + slot.Width - padding.Right - BorderThickness;
            var top = slot.Y + padding.Top + BorderThickness;
            var bottom = slot.Y + slot.Height - padding.Bottom - BorderThickness;

            var maxTextWidth = MathF.Max(0f, right - left);
            var maxTextHeight = MathF.Max(0f, bottom - top);
            if (maxTextWidth <= 0f || maxTextHeight <= 0f)
            {
                return null;
            }

            var availableWidth = TextWrapping == TextWrapping.NoWrap
                ? float.PositiveInfinity
                : maxTextWidth;
            var layout = ResolveTextLayout(text, availableWidth);
            var textX = layout.Size.X <= maxTextWidth
                ? left + ((maxTextWidth - layout.Size.X) / 2f)
                : left;
            var textY = layout.Size.Y <= maxTextHeight
                ? top + ((maxTextHeight - layout.Size.Y) / 2f)
                : top;
            var lineSpacing = UiTextRenderer.GetLineHeight(this, FontSize);
            var lineDraws = new ButtonTextLineDraw[layout.Lines.Count];
            var lineDrawCount = 0;

            for (var i = 0; i < layout.Lines.Count; i++)
            {
                var line = layout.Lines[i];
                if (line.Length == 0)
                {
                    continue;
                }

                var lineWidth = ResolveRenderedLineWidth(layout, i, line, FontSize);
                var lineX = lineWidth <= maxTextWidth
                    ? left + ((maxTextWidth - lineWidth) / 2f)
                    : left;
                var linePosition = new Vector2(lineX, textY + (i * lineSpacing));
                lineDraws[lineDrawCount++] = new ButtonTextLineDraw(line, linePosition);
            }

            _renderTextPreparationCallCount++;
            return new ButtonTextRenderPlan(
                layout,
                lineSpacing,
                lineDrawCount == lineDraws.Length
                    ? lineDraws
                    : lineDraws.AsSpan(0, lineDrawCount).ToArray());
        }
        finally
        {
            _renderTextPreparationElapsedTicks += Stopwatch.GetTimestamp() - start;
        }
    }

    internal ButtonTextRenderPlan? PrepareTextRenderPlanForTests(LayoutRect slot)
    {
        return PrepareTextRenderPlan(slot);
    }

    internal static float ResolveRenderedLineWidth(
        TextLayout.TextLayoutResult layout,
        int lineIndex,
        string line,
        float fontSize)
    {
        if (lineIndex < layout.LineWidths.Count)
        {
            return layout.LineWidths[lineIndex];
        }

        _renderLineWidthFallbackCount++;
        return UiTextRenderer.MeasureWidth(line, fontSize);
    }

    internal static int GetRenderLineWidthFallbackCountForTests()
    {
        return _renderLineWidthFallbackCount;
    }

    internal static void ResetRenderLineWidthFallbackCountForTests()
    {
        _renderLineWidthFallbackCount = 0;
    }

    internal static ButtonTimingSnapshot GetTimingSnapshotForTests()
    {
        return new ButtonTimingSnapshot(
            _measureOverrideElapsedTicks,
            _renderElapsedTicks,
            _resolveTextLayoutElapsedTicks,
            _renderChromeElapsedTicks,
            _renderTextPreparationElapsedTicks,
            _renderTextDrawDispatchElapsedTicks,
            _renderTextPreparationCallCount,
            _renderTextDrawDispatchCallCount,
            _contentPropertyChangedCount,
            _textLayoutCacheHitCount,
            _textLayoutCacheMissCount,
            _intrinsicNoWrapMeasureCacheHitCount,
            _intrinsicNoWrapMeasureCacheMissCount,
            _textLayoutInvalidationCount,
            _intrinsicNoWrapMeasureInvalidationCount,
            _plainTextMeasureFastPathCount,
            _intrinsicNoWrapMeasurePathCount,
            _textLayoutMeasurePathCount);
    }

    internal static void ResetTimingForTests()
    {
        _measureOverrideElapsedTicks = 0;
        _renderElapsedTicks = 0;
        _resolveTextLayoutElapsedTicks = 0;
        _renderChromeElapsedTicks = 0;
        _renderTextPreparationElapsedTicks = 0;
        _renderTextDrawDispatchElapsedTicks = 0;
        _renderTextPreparationCallCount = 0;
        _renderTextDrawDispatchCallCount = 0;
        _contentPropertyChangedCount = 0;
        _textLayoutCacheHitCount = 0;
        _textLayoutCacheMissCount = 0;
        _intrinsicNoWrapMeasureCacheHitCount = 0;
        _intrinsicNoWrapMeasureCacheMissCount = 0;
        _textLayoutInvalidationCount = 0;
        _intrinsicNoWrapMeasureInvalidationCount = 0;
        _plainTextMeasureFastPathCount = 0;
        _intrinsicNoWrapMeasurePathCount = 0;
        _textLayoutMeasurePathCount = 0;
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

internal readonly record struct ButtonTimingSnapshot(
    long MeasureOverrideElapsedTicks,
    long RenderElapsedTicks,
    long ResolveTextLayoutElapsedTicks,
    long RenderChromeElapsedTicks,
    long RenderTextPreparationElapsedTicks,
    long RenderTextDrawDispatchElapsedTicks,
    int RenderTextPreparationCallCount,
    int RenderTextDrawDispatchCallCount,
    int ContentPropertyChangedCount,
    int TextLayoutCacheHitCount,
    int TextLayoutCacheMissCount,
    int IntrinsicNoWrapMeasureCacheHitCount,
    int IntrinsicNoWrapMeasureCacheMissCount,
    int TextLayoutInvalidationCount,
    int IntrinsicNoWrapMeasureInvalidationCount,
    int PlainTextMeasureFastPathCount,
    int IntrinsicNoWrapMeasurePathCount,
    int TextLayoutMeasurePathCount);

internal readonly record struct ButtonTextRenderPlan(
    TextLayout.TextLayoutResult Layout,
    float LineSpacing,
    IReadOnlyList<ButtonTextLineDraw> LineDraws);

internal readonly record struct ButtonTextLineDraw(
    string Text,
    Vector2 Position);



