using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public class Button : ContentControl
{
    private static readonly System.Lazy<Style> DefaultButtonStyle = new(BuildDefaultButtonStyle);
    private static long _diagConstructorCallCount;
    private static long _diagMeasureOverrideCallCount;
    private static long _diagMeasureOverrideElapsedTicks;
    private static long _diagMeasureOverridePlainTextFastPathCount;
    private static long _diagMeasureOverrideBaseMeasurePathCount;
    private static long _diagMeasureOverrideChromeOnlyCount;
    private static long _diagCanReuseMeasureCallCount;
    private static long _diagCanReuseMeasureAllowedCount;
    private static long _diagCanReuseMeasureRejectedCount;
    private static long _diagOnClickCallCount;
    private static long _diagOnClickElapsedTicks;
    private static long _diagOnClickAutomationNotifyCount;
    private static long _diagOnClickAutomationSkipCount;
    private static long _diagOnClickExecuteCommandCount;
    private static long _diagDependencyPropertyChangedCallCount;
    private static long _diagDependencyPropertyChangedElapsedTicks;
    private static long _diagContentPropertyChangedCount;
    private static long _diagTextMetricPropertyChangedCount;
    private static long _diagOtherPropertyChangedCount;
    private static long _diagRenderCallCount;
    private static long _diagRenderElapsedTicks;
    private static long _diagRenderSkippedTemplateRootCount;
    private static long _diagRenderPreparedTextPlanCount;
    private static long _diagRenderSkippedNoTextPlanCount;
    private static long _diagShouldCreateImplicitContentElementCallCount;
    private static long _diagShouldCreateImplicitContentElementReturnedTrueCount;
    private static long _diagShouldCreateImplicitContentElementReturnedFalseCount;
    private static long _diagGetDisplayContentTextCallCount;
    private static long _diagGetDisplayContentTextReturnedEmptyForContentElementCount;
    private static long _diagGetDisplayContentTextReturnedEmptyForNullContentCount;
    private static long _diagGetDisplayContentTextExtractedCount;
    private static long _diagGetSelfRenderedContentTextCallCount;
    private static long _diagGetSelfRenderedContentTextReturnedTextCount;
    private static long _diagGetSelfRenderedContentTextReturnedEmptyCount;
    private static long _diagResolveTextLayoutCallCount;
    private static long _diagResolveTextLayoutElapsedTicks;
    private static long _diagTextLayoutCacheHitCount;
    private static long _diagTextLayoutCacheMissCount;
    private static long _diagTextLayoutInvalidationCount;
    private static long _diagTextLayoutInvalidationNoOpCount;
    private static long _diagIntrinsicNoWrapMeasureInvalidationCount;
    private static long _diagIntrinsicNoWrapMeasureInvalidationNoOpCount;
    private static long _diagTextRenderPlanInvalidationCount;
    private static long _diagTextRenderPlanInvalidationNoOpCount;
    private static long _diagCanUsePlainTextMeasureFastPathCallCount;
    private static long _diagPlainTextMeasureFastPathAllowedCount;
    private static long _diagPlainTextMeasureFastPathBlockedByTemplateCount;
    private static long _diagPlainTextMeasureFastPathBlockedByTemplateRootCount;
    private static long _diagPlainTextMeasureFastPathBlockedByContentElementCount;
    private static long _diagPlainTextMeasureFastPathBlockedByContentTemplateCount;
    private static long _diagPlainTextMeasureFastPathBlockedByContentTemplateSelectorCount;
    private static long _diagMeasurePlainTextButtonCallCount;
    private static long _diagMeasurePlainTextButtonEmptyTextCount;
    private static long _diagIntrinsicNoWrapMeasurePathCount;
    private static long _diagTextLayoutMeasurePathCount;
    private static long _diagCanUseIntrinsicNoWrapTextMeasureCallCount;
    private static long _diagIntrinsicNoWrapTextMeasureAllowedCount;
    private static long _diagIntrinsicNoWrapTextMeasureRejectedEmptyTextCount;
    private static long _diagIntrinsicNoWrapTextMeasureRejectedMultilineTextCount;
    private static long _diagResolveIntrinsicNoWrapTextSizeCallCount;
    private static long _diagResolveIntrinsicNoWrapTextSizeElapsedTicks;
    private static long _diagIntrinsicNoWrapMeasureCacheHitCount;
    private static long _diagIntrinsicNoWrapMeasureCacheMissCount;
    private static long _diagRenderChromeCallCount;
    private static long _diagRenderChromeElapsedTicks;
    private static long _diagRenderChromeSkippedBorderCount;
    private static long _diagRenderChromeDrewBorderCount;
    private static long _diagDrawTextRenderPlanCallCount;
    private static long _diagDrawTextRenderPlanElapsedTicks;
    private static long _diagRenderTextPreparationCallCount;
    private static long _diagRenderTextPreparationElapsedTicks;
    private static long _diagRenderTextPreparationNoTextCount;
    private static long _diagRenderTextPreparationNoSpaceCount;
    private static long _diagTextRenderPlanCacheHitCount;
    private static long _diagTextRenderPlanCacheMissCount;
    private static long _diagTextRenderPlanPreparedLineCount;
    private static long _diagTextRenderPlanSkippedEmptyLineCount;
    private static long _diagRenderTextDrawDispatchCallCount;
    private static long _diagResolveRenderedLineWidthCallCount;
    private static long _diagResolveRenderedLineWidthLayoutWidthHitCount;
    private static long _diagResolveRenderedLineWidthFallbackCount;
    private static long _diagBuildDefaultButtonStyleCallCount;
    private static long _diagBuildDefaultButtonStyleElapsedTicks;
    private static long _diagGetFallbackStyleCallCount;
    private static long _diagGetFallbackStyleElapsedTicks;
    private static long _diagGetFallbackStyleCacheHitCount;
    private static long _diagGetFallbackStyleCacheMissCount;
    private static long _diagRaiseClickEventCallCount;
    private static long _diagSetMouseOverFromInputCallCount;
    private static long _diagSetMouseOverFromInputNoOpCount;
    private static long _diagSetMouseOverFromInputChangedCount;
    private static long _diagSetPressedFromInputCallCount;
    private static long _diagSetPressedFromInputNoOpCount;
    private static long _diagSetPressedFromInputChangedCount;
    private static long _diagInvokeFromInputCallCount;
    private static long _diagHasAvailableIndependentDesiredSizeForUniformGridCallCount;
    private static long _diagHasAvailableIndependentDesiredSizeForUniformGridTrueCount;
    private static long _diagHasAvailableIndependentDesiredSizeForUniformGridFalseCount;

    private long _runtimeMeasureOverrideCallCount;
    private long _runtimeMeasureOverrideElapsedTicks;
    private long _runtimeMeasureOverridePlainTextFastPathCount;
    private long _runtimeMeasureOverrideBaseMeasurePathCount;
    private long _runtimeMeasureOverrideChromeOnlyCount;
    private long _runtimeCanReuseMeasureCallCount;
    private long _runtimeCanReuseMeasureAllowedCount;
    private long _runtimeCanReuseMeasureRejectedCount;
    private long _runtimeOnClickCallCount;
    private long _runtimeOnClickElapsedTicks;
    private long _runtimeOnClickAutomationNotifyCount;
    private long _runtimeOnClickAutomationSkipCount;
    private long _runtimeOnClickExecuteCommandCount;
    private long _runtimeDependencyPropertyChangedCallCount;
    private long _runtimeDependencyPropertyChangedElapsedTicks;
    private long _runtimeContentPropertyChangedCount;
    private long _runtimeTextMetricPropertyChangedCount;
    private long _runtimeOtherPropertyChangedCount;
    private long _runtimeRenderCallCount;
    private long _runtimeRenderElapsedTicks;
    private long _runtimeRenderSkippedTemplateRootCount;
    private long _runtimeRenderPreparedTextPlanCount;
    private long _runtimeRenderSkippedNoTextPlanCount;
    private long _runtimeShouldCreateImplicitContentElementCallCount;
    private long _runtimeShouldCreateImplicitContentElementReturnedTrueCount;
    private long _runtimeShouldCreateImplicitContentElementReturnedFalseCount;
    private long _runtimeGetDisplayContentTextCallCount;
    private long _runtimeGetDisplayContentTextReturnedEmptyForContentElementCount;
    private long _runtimeGetDisplayContentTextReturnedEmptyForNullContentCount;
    private long _runtimeGetDisplayContentTextExtractedCount;
    private long _runtimeGetSelfRenderedContentTextCallCount;
    private long _runtimeGetSelfRenderedContentTextReturnedTextCount;
    private long _runtimeGetSelfRenderedContentTextReturnedEmptyCount;
    private long _runtimeResolveTextLayoutCallCount;
    private long _runtimeResolveTextLayoutElapsedTicks;
    private long _runtimeTextLayoutCacheHitCount;
    private long _runtimeTextLayoutCacheMissCount;
    private long _runtimeTextLayoutInvalidationCount;
    private long _runtimeTextLayoutInvalidationNoOpCount;
    private long _runtimeIntrinsicNoWrapMeasureInvalidationCount;
    private long _runtimeIntrinsicNoWrapMeasureInvalidationNoOpCount;
    private long _runtimeTextRenderPlanInvalidationCount;
    private long _runtimeTextRenderPlanInvalidationNoOpCount;
    private long _runtimeCanUsePlainTextMeasureFastPathCallCount;
    private long _runtimePlainTextMeasureFastPathAllowedCount;
    private long _runtimeMeasurePlainTextButtonCallCount;
    private long _runtimeMeasurePlainTextButtonEmptyTextCount;
    private long _runtimeIntrinsicNoWrapMeasurePathCount;
    private long _runtimeTextLayoutMeasurePathCount;
    private long _runtimeCanUseIntrinsicNoWrapTextMeasureCallCount;
    private long _runtimeIntrinsicNoWrapTextMeasureAllowedCount;
    private long _runtimeIntrinsicNoWrapTextMeasureRejectedEmptyTextCount;
    private long _runtimeIntrinsicNoWrapTextMeasureRejectedMultilineTextCount;
    private long _runtimeResolveIntrinsicNoWrapTextSizeCallCount;
    private long _runtimeResolveIntrinsicNoWrapTextSizeElapsedTicks;
    private long _runtimeIntrinsicNoWrapMeasureCacheHitCount;
    private long _runtimeIntrinsicNoWrapMeasureCacheMissCount;
    private long _runtimeRenderChromeCallCount;
    private long _runtimeRenderChromeElapsedTicks;
    private long _runtimeRenderChromeSkippedBorderCount;
    private long _runtimeRenderChromeDrewBorderCount;
    private long _runtimeDrawTextRenderPlanCallCount;
    private long _runtimeDrawTextRenderPlanElapsedTicks;
    private long _runtimeRenderTextPreparationCallCount;
    private long _runtimeRenderTextPreparationElapsedTicks;
    private long _runtimeRenderTextPreparationNoTextCount;
    private long _runtimeRenderTextPreparationNoSpaceCount;
    private long _runtimeTextRenderPlanCacheHitCount;
    private long _runtimeTextRenderPlanCacheMissCount;
    private long _runtimeTextRenderPlanPreparedLineCount;
    private long _runtimeTextRenderPlanSkippedEmptyLineCount;
    private long _runtimeRenderTextDrawDispatchCallCount;
    private long _runtimeResolveRenderedLineWidthLayoutWidthHitCount;
    private long _runtimeRaiseClickEventCallCount;
    private long _runtimeSetMouseOverFromInputCallCount;
    private long _runtimeSetMouseOverFromInputNoOpCount;
    private long _runtimeSetMouseOverFromInputChangedCount;
    private long _runtimeSetPressedFromInputCallCount;
    private long _runtimeSetPressedFromInputNoOpCount;
    private long _runtimeSetPressedFromInputChangedCount;
    private long _runtimeInvokeFromInputCallCount;
    private long _runtimeHasAvailableIndependentDesiredSizeForUniformGridCallCount;
    private long _runtimeHasAvailableIndependentDesiredSizeForUniformGridTrueCount;
    private long _runtimeHasAvailableIndependentDesiredSizeForUniformGridFalseCount;

    private bool _hasTextLayoutCache;
    private bool _hasIntrinsicNoWrapMeasureCache;
    private int _textLayoutCacheTextVersion = -1;
    private int _intrinsicNoWrapMeasureTextVersion = -1;
    private float _textLayoutCacheWidth = float.NaN;
    private UiTypography? _textLayoutCacheTypography;
    private UiTypography? _intrinsicNoWrapMeasureTypography;
    private float _textLayoutCacheFontSize = float.NaN;
    private float _intrinsicNoWrapMeasureFontSize = float.NaN;
    private TextLayout.TextLayoutResult _textLayoutCacheResult = TextLayout.TextLayoutResult.Empty;
    private Vector2 _intrinsicNoWrapMeasureSize = Vector2.Zero;
    private int _contentVersion;
    private bool _hasTextRenderPlanCache;
    private int _textRenderPlanCacheContentVersion = -1;
    private LayoutRect _textRenderPlanCacheSlot;
    private float _textRenderPlanCacheBorderThickness = float.NaN;
    private Thickness _textRenderPlanCachePadding = Thickness.Empty;
    private float _textRenderPlanCacheFontSize = float.NaN;
    private UiTypography? _textRenderPlanCacheTypography;
    private ButtonTextRenderPlan _textRenderPlanCache;

    public static readonly RoutedEvent ClickEvent =
        new(nameof(Click), RoutingStrategy.Bubble);

    public new static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(Button),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

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
        IncrementAggregate(ref _diagConstructorCallCount);
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
        IncrementMetric(ref _runtimeMeasureOverrideCallCount, ref _diagMeasureOverrideCallCount);
        try
        {
            var text = GetSelfRenderedContentText();
            if (CanUsePlainTextMeasureFastPath())
            {
                IncrementMetric(ref _runtimeMeasureOverridePlainTextFastPathCount, ref _diagMeasureOverridePlainTextFastPathCount);
                return MeasurePlainTextButton(availableSize, text);
            }

            IncrementMetric(ref _runtimeMeasureOverrideBaseMeasurePathCount, ref _diagMeasureOverrideBaseMeasurePathCount);
            var desired = base.MeasureOverride(availableSize);
            var padding = Padding;
            var border = BorderThickness * 2f;
            var innerAvailableWidth = MathF.Max(0f, availableSize.X - padding.Horizontal - border);

            if (!string.IsNullOrEmpty(text))
            {
                IncrementMetric(ref _runtimeTextLayoutMeasurePathCount, ref _diagTextLayoutMeasurePathCount);
                var textSize = ResolveTextLayout(text, innerAvailableWidth).Size;
                desired.X = System.MathF.Max(desired.X, textSize.X + padding.Horizontal + border);
                desired.Y = System.MathF.Max(desired.Y, textSize.Y + padding.Vertical + border);
                return desired;
            }

            IncrementMetric(ref _runtimeMeasureOverrideChromeOnlyCount, ref _diagMeasureOverrideChromeOnlyCount);
            desired.X = System.MathF.Max(desired.X, padding.Horizontal + border);
            desired.Y = System.MathF.Max(desired.Y, padding.Vertical + border);
            return desired;
        }
        finally
        {
            AddMetric(ref _runtimeMeasureOverrideElapsedTicks, ref _diagMeasureOverrideElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    protected override bool CanReuseMeasureForAvailableSizeChange(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
    {
        _ = previousAvailableSize;
        _ = nextAvailableSize;
        IncrementMetric(ref _runtimeCanReuseMeasureCallCount, ref _diagCanReuseMeasureCallCount);
        var reusable = CanUseIntrinsicNoWrapTextMeasure() && CanUsePlainTextMeasureFastPath();
        if (reusable)
        {
            IncrementMetric(ref _runtimeCanReuseMeasureAllowedCount, ref _diagCanReuseMeasureAllowedCount);
        }
        else
        {
            IncrementMetric(ref _runtimeCanReuseMeasureRejectedCount, ref _diagCanReuseMeasureRejectedCount);
        }

        return reusable;
    }


    protected virtual void OnClick()
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeOnClickCallCount, ref _diagOnClickCallCount);
        try
        {
            RaiseClickEvent();

            if (UiRoot.Current is UiRoot uiRoot)
            {
                IncrementMetric(ref _runtimeOnClickAutomationNotifyCount, ref _diagOnClickAutomationNotifyCount);
                uiRoot.Automation.NotifyInvoke(this);
            }
            else
            {
                IncrementMetric(ref _runtimeOnClickAutomationSkipCount, ref _diagOnClickAutomationSkipCount);
            }

            IncrementMetric(ref _runtimeOnClickExecuteCommandCount, ref _diagOnClickExecuteCommandCount);
            ExecuteCommand();
        }
        finally
        {
            AddMetric(ref _runtimeOnClickElapsedTicks, ref _diagOnClickElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeDependencyPropertyChangedCallCount, ref _diagDependencyPropertyChangedCallCount);
        base.OnDependencyPropertyChanged(args);

        try
        {
            if (args.Property == ContentProperty)
            {
                IncrementMetric(ref _runtimeContentPropertyChangedCount, ref _diagContentPropertyChangedCount);
                _contentVersion++;
                InvalidateTextLayoutCache();
                InvalidateIntrinsicNoWrapMeasureCache();
                InvalidateTextRenderPlanCache();
            }
            else if (args.Property == FontSizeProperty ||
                     args.Property == FontFamilyProperty ||
                     args.Property == FontWeightProperty ||
                     args.Property == FontStyleProperty ||
                     args.Property == PaddingProperty ||
                     args.Property == BorderThicknessProperty)
            {
                IncrementMetric(ref _runtimeTextMetricPropertyChangedCount, ref _diagTextMetricPropertyChangedCount);
                InvalidateTextLayoutCache();
                InvalidateIntrinsicNoWrapMeasureCache();
                InvalidateTextRenderPlanCache();
            }
            else
            {
                IncrementMetric(ref _runtimeOtherPropertyChangedCount, ref _diagOtherPropertyChangedCount);
            }
        }
        finally
        {
            AddMetric(ref _runtimeDependencyPropertyChangedElapsedTicks, ref _diagDependencyPropertyChangedElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeRenderCallCount, ref _diagRenderCallCount);
        try
        {
            base.OnRender(spriteBatch);

            if (HasTemplateRoot)
            {
                IncrementMetric(ref _runtimeRenderSkippedTemplateRootCount, ref _diagRenderSkippedTemplateRootCount);
                return;
            }

            RenderChrome(spriteBatch, LayoutSlot);

            var renderPlan = PrepareTextRenderPlan(LayoutSlot);
            if (!renderPlan.HasValue)
            {
                IncrementMetric(ref _runtimeRenderSkippedNoTextPlanCount, ref _diagRenderSkippedNoTextPlanCount);
                return;
            }

            IncrementMetric(ref _runtimeRenderPreparedTextPlanCount, ref _diagRenderPreparedTextPlanCount);
            DrawTextRenderPlan(spriteBatch, renderPlan.Value);
        }
        finally
        {
            AddMetric(ref _runtimeRenderElapsedTicks, ref _diagRenderElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    protected override bool ShouldCreateImplicitContentElement(object? content, DataTemplate? selectedTemplate)
    {
        IncrementMetric(ref _runtimeShouldCreateImplicitContentElementCallCount, ref _diagShouldCreateImplicitContentElementCallCount);
        var shouldCreate = content is UIElement || selectedTemplate != null;
        if (shouldCreate)
        {
            IncrementMetric(ref _runtimeShouldCreateImplicitContentElementReturnedTrueCount, ref _diagShouldCreateImplicitContentElementReturnedTrueCount);
        }
        else
        {
            IncrementMetric(ref _runtimeShouldCreateImplicitContentElementReturnedFalseCount, ref _diagShouldCreateImplicitContentElementReturnedFalseCount);
        }

        return shouldCreate;
    }

    protected string GetDisplayContentText()
    {
        IncrementMetric(ref _runtimeGetDisplayContentTextCallCount, ref _diagGetDisplayContentTextCallCount);
        if (ContentElement != null)
        {
            IncrementMetric(ref _runtimeGetDisplayContentTextReturnedEmptyForContentElementCount, ref _diagGetDisplayContentTextReturnedEmptyForContentElementCount);
            return string.Empty;
        }

        if (Content == null)
        {
            IncrementMetric(ref _runtimeGetDisplayContentTextReturnedEmptyForNullContentCount, ref _diagGetDisplayContentTextReturnedEmptyForNullContentCount);
            return string.Empty;
        }

        IncrementMetric(ref _runtimeGetDisplayContentTextExtractedCount, ref _diagGetDisplayContentTextExtractedCount);
        return Label.ExtractAutomationText(Content);
    }

    private string GetSelfRenderedContentText()
    {
        IncrementMetric(ref _runtimeGetSelfRenderedContentTextCallCount, ref _diagGetSelfRenderedContentTextCallCount);
        if (CanUsePlainTextMeasureFastPath())
        {
            IncrementMetric(ref _runtimeGetSelfRenderedContentTextReturnedTextCount, ref _diagGetSelfRenderedContentTextReturnedTextCount);
            return GetDisplayContentText();
        }

        IncrementMetric(ref _runtimeGetSelfRenderedContentTextReturnedEmptyCount, ref _diagGetSelfRenderedContentTextReturnedEmptyCount);
        return string.Empty;
    }

    private TextLayout.TextLayoutResult ResolveTextLayout(string text, float availableWidth)
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeResolveTextLayoutCallCount, ref _diagResolveTextLayoutCallCount);
        try
        {
            var typography = UiTextRenderer.ResolveTypography(this, FontSize);
            if (_hasTextLayoutCache &&
                _textLayoutCacheTextVersion == _contentVersion &&
                Nullable.Equals(_textLayoutCacheTypography, typography) &&
                WidthMatches(_textLayoutCacheFontSize, FontSize) &&
                WidthMatches(_textLayoutCacheWidth, availableWidth))
            {
                IncrementMetric(ref _runtimeTextLayoutCacheHitCount, ref _diagTextLayoutCacheHitCount);
                return _textLayoutCacheResult;
            }

            IncrementMetric(ref _runtimeTextLayoutCacheMissCount, ref _diagTextLayoutCacheMissCount);
            var result = TextLayout.Layout(text, typography, FontSize, float.PositiveInfinity, TextWrapping.NoWrap);
            _textLayoutCacheTextVersion = _contentVersion;
            _textLayoutCacheWidth = availableWidth;
            _textLayoutCacheTypography = typography;
            _textLayoutCacheFontSize = FontSize;
            _textLayoutCacheResult = result;
            _hasTextLayoutCache = true;
            return result;
        }
        finally
        {
            AddMetric(ref _runtimeResolveTextLayoutElapsedTicks, ref _diagResolveTextLayoutElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    private void InvalidateTextLayoutCache()
    {
        if (_hasTextLayoutCache)
        {
            IncrementMetric(ref _runtimeTextLayoutInvalidationCount, ref _diagTextLayoutInvalidationCount);
        }
        else
        {
            IncrementMetric(ref _runtimeTextLayoutInvalidationNoOpCount, ref _diagTextLayoutInvalidationNoOpCount);
        }

        _hasTextLayoutCache = false;
        _textLayoutCacheTextVersion = -1;
        _textLayoutCacheWidth = float.NaN;
        _textLayoutCacheTypography = null;
        _textLayoutCacheFontSize = float.NaN;
        _textLayoutCacheResult = TextLayout.TextLayoutResult.Empty;
    }

    private void InvalidateIntrinsicNoWrapMeasureCache()
    {
        if (_hasIntrinsicNoWrapMeasureCache)
        {
            IncrementMetric(ref _runtimeIntrinsicNoWrapMeasureInvalidationCount, ref _diagIntrinsicNoWrapMeasureInvalidationCount);
        }
        else
        {
            IncrementMetric(ref _runtimeIntrinsicNoWrapMeasureInvalidationNoOpCount, ref _diagIntrinsicNoWrapMeasureInvalidationNoOpCount);
        }

        _hasIntrinsicNoWrapMeasureCache = false;
        _intrinsicNoWrapMeasureTextVersion = -1;
        _intrinsicNoWrapMeasureTypography = null;
        _intrinsicNoWrapMeasureFontSize = float.NaN;
        _intrinsicNoWrapMeasureSize = Vector2.Zero;
    }

    private void InvalidateTextRenderPlanCache()
    {
        if (_hasTextRenderPlanCache)
        {
            IncrementMetric(ref _runtimeTextRenderPlanInvalidationCount, ref _diagTextRenderPlanInvalidationCount);
        }
        else
        {
            IncrementMetric(ref _runtimeTextRenderPlanInvalidationNoOpCount, ref _diagTextRenderPlanInvalidationNoOpCount);
        }

        _hasTextRenderPlanCache = false;
        _textRenderPlanCacheContentVersion = -1;
        _textRenderPlanCacheSlot = default;
        _textRenderPlanCacheBorderThickness = float.NaN;
        _textRenderPlanCachePadding = Thickness.Empty;
        _textRenderPlanCacheFontSize = float.NaN;
        _textRenderPlanCacheTypography = null;
        _textRenderPlanCache = default;
    }

    private bool CanUsePlainTextMeasureFastPath()
    {
        IncrementMetric(ref _runtimeCanUsePlainTextMeasureFastPathCallCount, ref _diagCanUsePlainTextMeasureFastPathCallCount);
        var blockedByTemplate = Template != null;
        var blockedByTemplateRoot = HasTemplateRoot;
        var blockedByContentElement = ContentElement != null;
        var blockedByContentTemplate = ContentTemplate != null;
        var blockedByContentTemplateSelector = ContentTemplateSelector != null;
        var allowed = !blockedByTemplate &&
                      !blockedByTemplateRoot &&
                      !blockedByContentElement &&
                      !blockedByContentTemplate &&
                      !blockedByContentTemplateSelector;

        if (allowed)
        {
            IncrementMetric(ref _runtimePlainTextMeasureFastPathAllowedCount, ref _diagPlainTextMeasureFastPathAllowedCount);
        }
        else
        {
            if (blockedByTemplate)
            {
                IncrementAggregate(ref _diagPlainTextMeasureFastPathBlockedByTemplateCount);
            }

            if (blockedByTemplateRoot)
            {
                IncrementAggregate(ref _diagPlainTextMeasureFastPathBlockedByTemplateRootCount);
            }

            if (blockedByContentElement)
            {
                IncrementAggregate(ref _diagPlainTextMeasureFastPathBlockedByContentElementCount);
            }

            if (blockedByContentTemplate)
            {
                IncrementAggregate(ref _diagPlainTextMeasureFastPathBlockedByContentTemplateCount);
            }

            if (blockedByContentTemplateSelector)
            {
                IncrementAggregate(ref _diagPlainTextMeasureFastPathBlockedByContentTemplateSelectorCount);
            }
        }

        return allowed;
    }

    private Vector2 MeasurePlainTextButton(Vector2 availableSize, string text)
    {
        IncrementMetric(ref _runtimeMeasurePlainTextButtonCallCount, ref _diagMeasurePlainTextButtonCallCount);
        var padding = Padding;
        var border = BorderThickness * 2f;
        var desired = new Vector2(padding.Horizontal + border, padding.Vertical + border);
        if (string.IsNullOrEmpty(text))
        {
            IncrementMetric(ref _runtimeMeasurePlainTextButtonEmptyTextCount, ref _diagMeasurePlainTextButtonEmptyTextCount);
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
        IncrementMetric(ref _runtimeIntrinsicNoWrapMeasurePathCount, ref _diagIntrinsicNoWrapMeasurePathCount);
        return ResolveIntrinsicNoWrapTextSize(text);
    }

    private Vector2 ResolveTextLayoutMeasurePath(string text, float availableWidth)
    {
        IncrementMetric(ref _runtimeTextLayoutMeasurePathCount, ref _diagTextLayoutMeasurePathCount);
        return ResolveTextLayout(text, availableWidth).Size;
    }

    internal bool HasAvailableIndependentDesiredSizeForUniformGrid()
    {
        IncrementMetric(ref _runtimeHasAvailableIndependentDesiredSizeForUniformGridCallCount, ref _diagHasAvailableIndependentDesiredSizeForUniformGridCallCount);
        var hasIndependentSize = CanUsePlainTextMeasureFastPath();
        if (hasIndependentSize)
        {
            IncrementMetric(ref _runtimeHasAvailableIndependentDesiredSizeForUniformGridTrueCount, ref _diagHasAvailableIndependentDesiredSizeForUniformGridTrueCount);
        }
        else
        {
            IncrementMetric(ref _runtimeHasAvailableIndependentDesiredSizeForUniformGridFalseCount, ref _diagHasAvailableIndependentDesiredSizeForUniformGridFalseCount);
        }

        return hasIndependentSize;
    }

    private bool CanUseIntrinsicNoWrapTextMeasure()
    {
        IncrementMetric(ref _runtimeCanUseIntrinsicNoWrapTextMeasureCallCount, ref _diagCanUseIntrinsicNoWrapTextMeasureCallCount);
        var text = GetDisplayContentText();
        if (string.IsNullOrEmpty(text))
        {
            IncrementMetric(ref _runtimeIntrinsicNoWrapTextMeasureRejectedEmptyTextCount, ref _diagIntrinsicNoWrapTextMeasureRejectedEmptyTextCount);
            return false;
        }

        if (text.IndexOfAny(['\r', '\n']) >= 0)
        {
            IncrementMetric(ref _runtimeIntrinsicNoWrapTextMeasureRejectedMultilineTextCount, ref _diagIntrinsicNoWrapTextMeasureRejectedMultilineTextCount);
            return false;
        }

        IncrementMetric(ref _runtimeIntrinsicNoWrapTextMeasureAllowedCount, ref _diagIntrinsicNoWrapTextMeasureAllowedCount);
        return true;
    }

    private Vector2 ResolveIntrinsicNoWrapTextSize(string text)
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeResolveIntrinsicNoWrapTextSizeCallCount, ref _diagResolveIntrinsicNoWrapTextSizeCallCount);
        var typography = UiTextRenderer.ResolveTypography(this, FontSize);
        try
        {
            if (_hasIntrinsicNoWrapMeasureCache &&
                _intrinsicNoWrapMeasureTextVersion == _contentVersion &&
                Nullable.Equals(_intrinsicNoWrapMeasureTypography, typography) &&
                WidthMatches(_intrinsicNoWrapMeasureFontSize, FontSize))
            {
                IncrementMetric(ref _runtimeIntrinsicNoWrapMeasureCacheHitCount, ref _diagIntrinsicNoWrapMeasureCacheHitCount);
                return _intrinsicNoWrapMeasureSize;
            }

            IncrementMetric(ref _runtimeIntrinsicNoWrapMeasureCacheMissCount, ref _diagIntrinsicNoWrapMeasureCacheMissCount);
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
        finally
        {
            AddMetric(ref _runtimeResolveIntrinsicNoWrapTextSizeElapsedTicks, ref _diagResolveIntrinsicNoWrapTextSizeElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    private void RenderChrome(SpriteBatch spriteBatch, LayoutRect slot)
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeRenderChromeCallCount, ref _diagRenderChromeCallCount);
        try
        {
            UiDrawing.DrawFilledRect(spriteBatch, slot, Background, Opacity);

            if (BorderThickness <= 0f)
            {
                IncrementMetric(ref _runtimeRenderChromeSkippedBorderCount, ref _diagRenderChromeSkippedBorderCount);
                return;
            }

            IncrementMetric(ref _runtimeRenderChromeDrewBorderCount, ref _diagRenderChromeDrewBorderCount);
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
            AddMetric(ref _runtimeRenderChromeElapsedTicks, ref _diagRenderChromeElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    private void DrawTextRenderPlan(SpriteBatch spriteBatch, ButtonTextRenderPlan renderPlan)
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeDrawTextRenderPlanCallCount, ref _diagDrawTextRenderPlanCallCount);
        try
        {
            for (var i = 0; i < renderPlan.LineDraws.Count; i++)
            {
                var lineDraw = renderPlan.LineDraws[i];
                UiTextRenderer.DrawString(spriteBatch, this, lineDraw.Text, lineDraw.Position, Foreground * Opacity, FontSize, opaqueBackground: true);
                IncrementMetric(ref _runtimeRenderTextDrawDispatchCallCount, ref _diagRenderTextDrawDispatchCallCount);
            }
        }
        finally
        {
            AddMetric(ref _runtimeDrawTextRenderPlanElapsedTicks, ref _diagDrawTextRenderPlanElapsedTicks, Stopwatch.GetTimestamp() - start);
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
                IncrementMetric(ref _runtimeRenderTextPreparationNoTextCount, ref _diagRenderTextPreparationNoTextCount);
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
                IncrementMetric(ref _runtimeRenderTextPreparationNoSpaceCount, ref _diagRenderTextPreparationNoSpaceCount);
                return null;
            }

            var typography = UiTextRenderer.ResolveTypography(this, FontSize);
            if (_hasTextRenderPlanCache &&
                _textRenderPlanCacheContentVersion == _contentVersion &&
                LayoutRectMatches(_textRenderPlanCacheSlot, slot) &&
                WidthMatches(_textRenderPlanCacheBorderThickness, BorderThickness) &&
                ThicknessMatches(_textRenderPlanCachePadding, padding) &&
                WidthMatches(_textRenderPlanCacheFontSize, FontSize) &&
                Nullable.Equals(_textRenderPlanCacheTypography, typography))
            {
                IncrementMetric(ref _runtimeTextRenderPlanCacheHitCount, ref _diagTextRenderPlanCacheHitCount);
                return _textRenderPlanCache;
            }

            IncrementMetric(ref _runtimeTextRenderPlanCacheMissCount, ref _diagTextRenderPlanCacheMissCount);
            var layout = ResolveTextLayout(text, maxTextWidth);
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
                    IncrementMetric(ref _runtimeTextRenderPlanSkippedEmptyLineCount, ref _diagTextRenderPlanSkippedEmptyLineCount);
                    continue;
                }

                if (i < layout.LineWidths.Count)
                {
                    _runtimeResolveRenderedLineWidthLayoutWidthHitCount++;
                }

                var lineWidth = ResolveRenderedLineWidth(layout, i, line, FontSize);
                var lineX = lineWidth <= maxTextWidth
                    ? left + ((maxTextWidth - lineWidth) / 2f)
                    : left;
                var linePosition = new Vector2(lineX, textY + (i * lineSpacing));
                lineDraws[lineDrawCount++] = new ButtonTextLineDraw(line, linePosition);
            }

            IncrementMetric(ref _runtimeRenderTextPreparationCallCount, ref _diagRenderTextPreparationCallCount);
            AddMetric(ref _runtimeTextRenderPlanPreparedLineCount, ref _diagTextRenderPlanPreparedLineCount, lineDrawCount);
            var renderPlan = new ButtonTextRenderPlan(
                layout,
                lineSpacing,
                lineDrawCount == lineDraws.Length
                    ? lineDraws
                    : lineDraws.AsSpan(0, lineDrawCount).ToArray());
            _textRenderPlanCacheContentVersion = _contentVersion;
            _textRenderPlanCacheSlot = slot;
            _textRenderPlanCacheBorderThickness = BorderThickness;
            _textRenderPlanCachePadding = padding;
            _textRenderPlanCacheFontSize = FontSize;
            _textRenderPlanCacheTypography = typography;
            _textRenderPlanCache = renderPlan;
            _hasTextRenderPlanCache = true;
            return renderPlan;
        }
        finally
        {
            AddMetric(ref _runtimeRenderTextPreparationElapsedTicks, ref _diagRenderTextPreparationElapsedTicks, Stopwatch.GetTimestamp() - start);
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
        IncrementAggregate(ref _diagResolveRenderedLineWidthCallCount);
        if (lineIndex < layout.LineWidths.Count)
        {
            IncrementAggregate(ref _diagResolveRenderedLineWidthLayoutWidthHitCount);
            return layout.LineWidths[lineIndex];
        }

        IncrementAggregate(ref _diagResolveRenderedLineWidthFallbackCount);
        return UiTextRenderer.MeasureWidth(line, fontSize);
    }

    internal static int GetRenderLineWidthFallbackCountForTests()
    {
        return SaturateToInt(ReadAggregate(ref _diagResolveRenderedLineWidthFallbackCount));
    }

    internal static void ResetRenderLineWidthFallbackCountForTests()
    {
        Interlocked.Exchange(ref _diagResolveRenderedLineWidthFallbackCount, 0);
    }

    internal ButtonRuntimeDiagnosticsSnapshot GetButtonSnapshotForDiagnostics()
    {
        return new ButtonRuntimeDiagnosticsSnapshot(
            HasTemplateRoot,
            ContentElement != null,
            _hasTextLayoutCache,
            _hasIntrinsicNoWrapMeasureCache,
            _hasTextRenderPlanCache,
            IsMouseOver,
            IsPressed,
            _contentVersion,
            Content?.GetType().Name ?? string.Empty,
            GetDisplayContentText(),
            LayoutSlot.Width,
            LayoutSlot.Height,
            _runtimeMeasureOverrideCallCount,
            TicksToMilliseconds(_runtimeMeasureOverrideElapsedTicks),
            _runtimeMeasureOverridePlainTextFastPathCount,
            _runtimeMeasureOverrideBaseMeasurePathCount,
            _runtimeMeasureOverrideChromeOnlyCount,
            _runtimeCanReuseMeasureCallCount,
            _runtimeCanReuseMeasureAllowedCount,
            _runtimeCanReuseMeasureRejectedCount,
            _runtimeOnClickCallCount,
            TicksToMilliseconds(_runtimeOnClickElapsedTicks),
            _runtimeOnClickAutomationNotifyCount,
            _runtimeOnClickAutomationSkipCount,
            _runtimeOnClickExecuteCommandCount,
            _runtimeDependencyPropertyChangedCallCount,
            TicksToMilliseconds(_runtimeDependencyPropertyChangedElapsedTicks),
            _runtimeContentPropertyChangedCount,
            _runtimeTextMetricPropertyChangedCount,
            _runtimeOtherPropertyChangedCount,
            _runtimeRenderCallCount,
            TicksToMilliseconds(_runtimeRenderElapsedTicks),
            _runtimeRenderSkippedTemplateRootCount,
            _runtimeRenderPreparedTextPlanCount,
            _runtimeRenderSkippedNoTextPlanCount,
            _runtimeGetDisplayContentTextCallCount,
            _runtimeGetDisplayContentTextReturnedEmptyForContentElementCount,
            _runtimeGetDisplayContentTextReturnedEmptyForNullContentCount,
            _runtimeGetDisplayContentTextExtractedCount,
            _runtimeGetSelfRenderedContentTextCallCount,
            _runtimeGetSelfRenderedContentTextReturnedTextCount,
            _runtimeGetSelfRenderedContentTextReturnedEmptyCount,
            _runtimeResolveTextLayoutCallCount,
            TicksToMilliseconds(_runtimeResolveTextLayoutElapsedTicks),
            _runtimeTextLayoutCacheHitCount,
            _runtimeTextLayoutCacheMissCount,
            _runtimeTextLayoutInvalidationCount,
            _runtimeTextLayoutInvalidationNoOpCount,
            _runtimeIntrinsicNoWrapMeasureInvalidationCount,
            _runtimeIntrinsicNoWrapMeasureInvalidationNoOpCount,
            _runtimeTextRenderPlanInvalidationCount,
            _runtimeTextRenderPlanInvalidationNoOpCount,
            _runtimeCanUsePlainTextMeasureFastPathCallCount,
            _runtimePlainTextMeasureFastPathAllowedCount,
            _runtimeMeasurePlainTextButtonCallCount,
            _runtimeMeasurePlainTextButtonEmptyTextCount,
            _runtimeIntrinsicNoWrapMeasurePathCount,
            _runtimeTextLayoutMeasurePathCount,
            _runtimeCanUseIntrinsicNoWrapTextMeasureCallCount,
            _runtimeIntrinsicNoWrapTextMeasureAllowedCount,
            _runtimeIntrinsicNoWrapTextMeasureRejectedEmptyTextCount,
            _runtimeIntrinsicNoWrapTextMeasureRejectedMultilineTextCount,
            _runtimeResolveIntrinsicNoWrapTextSizeCallCount,
            TicksToMilliseconds(_runtimeResolveIntrinsicNoWrapTextSizeElapsedTicks),
            _runtimeIntrinsicNoWrapMeasureCacheHitCount,
            _runtimeIntrinsicNoWrapMeasureCacheMissCount,
            _runtimeRenderChromeCallCount,
            TicksToMilliseconds(_runtimeRenderChromeElapsedTicks),
            _runtimeRenderChromeSkippedBorderCount,
            _runtimeRenderChromeDrewBorderCount,
            _runtimeDrawTextRenderPlanCallCount,
            TicksToMilliseconds(_runtimeDrawTextRenderPlanElapsedTicks),
            _runtimeRenderTextPreparationCallCount,
            TicksToMilliseconds(_runtimeRenderTextPreparationElapsedTicks),
            _runtimeRenderTextPreparationNoTextCount,
            _runtimeRenderTextPreparationNoSpaceCount,
            _runtimeTextRenderPlanCacheHitCount,
            _runtimeTextRenderPlanCacheMissCount,
            _runtimeTextRenderPlanPreparedLineCount,
            _runtimeTextRenderPlanSkippedEmptyLineCount,
            _runtimeRenderTextDrawDispatchCallCount,
            TicksToMilliseconds(_runtimeDrawTextRenderPlanElapsedTicks),
            _runtimeResolveRenderedLineWidthLayoutWidthHitCount,
            _runtimeRaiseClickEventCallCount,
            _runtimeSetMouseOverFromInputCallCount,
            _runtimeSetMouseOverFromInputNoOpCount,
            _runtimeSetMouseOverFromInputChangedCount,
            _runtimeSetPressedFromInputCallCount,
            _runtimeSetPressedFromInputNoOpCount,
            _runtimeSetPressedFromInputChangedCount,
            _runtimeInvokeFromInputCallCount,
            _runtimeHasAvailableIndependentDesiredSizeForUniformGridCallCount,
            _runtimeHasAvailableIndependentDesiredSizeForUniformGridTrueCount,
            _runtimeHasAvailableIndependentDesiredSizeForUniformGridFalseCount);
    }

    internal static ButtonTelemetrySnapshot GetTelemetryAndReset()
    {
        var snapshot = CreateAggregateTelemetrySnapshot();
        ResetAggregate(ref _diagConstructorCallCount);
        ResetAggregate(ref _diagMeasureOverrideCallCount);
        ResetAggregate(ref _diagMeasureOverrideElapsedTicks);
        ResetAggregate(ref _diagMeasureOverridePlainTextFastPathCount);
        ResetAggregate(ref _diagMeasureOverrideBaseMeasurePathCount);
        ResetAggregate(ref _diagMeasureOverrideChromeOnlyCount);
        ResetAggregate(ref _diagCanReuseMeasureCallCount);
        ResetAggregate(ref _diagCanReuseMeasureAllowedCount);
        ResetAggregate(ref _diagCanReuseMeasureRejectedCount);
        ResetAggregate(ref _diagOnClickCallCount);
        ResetAggregate(ref _diagOnClickElapsedTicks);
        ResetAggregate(ref _diagOnClickAutomationNotifyCount);
        ResetAggregate(ref _diagOnClickAutomationSkipCount);
        ResetAggregate(ref _diagOnClickExecuteCommandCount);
        ResetAggregate(ref _diagDependencyPropertyChangedCallCount);
        ResetAggregate(ref _diagDependencyPropertyChangedElapsedTicks);
        ResetAggregate(ref _diagContentPropertyChangedCount);
        ResetAggregate(ref _diagTextMetricPropertyChangedCount);
        ResetAggregate(ref _diagOtherPropertyChangedCount);
        ResetAggregate(ref _diagRenderCallCount);
        ResetAggregate(ref _diagRenderElapsedTicks);
        ResetAggregate(ref _diagRenderSkippedTemplateRootCount);
        ResetAggregate(ref _diagRenderPreparedTextPlanCount);
        ResetAggregate(ref _diagRenderSkippedNoTextPlanCount);
        ResetAggregate(ref _diagShouldCreateImplicitContentElementCallCount);
        ResetAggregate(ref _diagShouldCreateImplicitContentElementReturnedTrueCount);
        ResetAggregate(ref _diagShouldCreateImplicitContentElementReturnedFalseCount);
        ResetAggregate(ref _diagGetDisplayContentTextCallCount);
        ResetAggregate(ref _diagGetDisplayContentTextReturnedEmptyForContentElementCount);
        ResetAggregate(ref _diagGetDisplayContentTextReturnedEmptyForNullContentCount);
        ResetAggregate(ref _diagGetDisplayContentTextExtractedCount);
        ResetAggregate(ref _diagGetSelfRenderedContentTextCallCount);
        ResetAggregate(ref _diagGetSelfRenderedContentTextReturnedTextCount);
        ResetAggregate(ref _diagGetSelfRenderedContentTextReturnedEmptyCount);
        ResetAggregate(ref _diagResolveTextLayoutCallCount);
        ResetAggregate(ref _diagResolveTextLayoutElapsedTicks);
        ResetAggregate(ref _diagTextLayoutCacheHitCount);
        ResetAggregate(ref _diagTextLayoutCacheMissCount);
        ResetAggregate(ref _diagTextLayoutInvalidationCount);
        ResetAggregate(ref _diagTextLayoutInvalidationNoOpCount);
        ResetAggregate(ref _diagIntrinsicNoWrapMeasureInvalidationCount);
        ResetAggregate(ref _diagIntrinsicNoWrapMeasureInvalidationNoOpCount);
        ResetAggregate(ref _diagTextRenderPlanInvalidationCount);
        ResetAggregate(ref _diagTextRenderPlanInvalidationNoOpCount);
        ResetAggregate(ref _diagCanUsePlainTextMeasureFastPathCallCount);
        ResetAggregate(ref _diagPlainTextMeasureFastPathAllowedCount);
        ResetAggregate(ref _diagPlainTextMeasureFastPathBlockedByTemplateCount);
        ResetAggregate(ref _diagPlainTextMeasureFastPathBlockedByTemplateRootCount);
        ResetAggregate(ref _diagPlainTextMeasureFastPathBlockedByContentElementCount);
        ResetAggregate(ref _diagPlainTextMeasureFastPathBlockedByContentTemplateCount);
        ResetAggregate(ref _diagPlainTextMeasureFastPathBlockedByContentTemplateSelectorCount);
        ResetAggregate(ref _diagMeasurePlainTextButtonCallCount);
        ResetAggregate(ref _diagMeasurePlainTextButtonEmptyTextCount);
        ResetAggregate(ref _diagIntrinsicNoWrapMeasurePathCount);
        ResetAggregate(ref _diagTextLayoutMeasurePathCount);
        ResetAggregate(ref _diagCanUseIntrinsicNoWrapTextMeasureCallCount);
        ResetAggregate(ref _diagIntrinsicNoWrapTextMeasureAllowedCount);
        ResetAggregate(ref _diagIntrinsicNoWrapTextMeasureRejectedEmptyTextCount);
        ResetAggregate(ref _diagIntrinsicNoWrapTextMeasureRejectedMultilineTextCount);
        ResetAggregate(ref _diagResolveIntrinsicNoWrapTextSizeCallCount);
        ResetAggregate(ref _diagResolveIntrinsicNoWrapTextSizeElapsedTicks);
        ResetAggregate(ref _diagIntrinsicNoWrapMeasureCacheHitCount);
        ResetAggregate(ref _diagIntrinsicNoWrapMeasureCacheMissCount);
        ResetAggregate(ref _diagRenderChromeCallCount);
        ResetAggregate(ref _diagRenderChromeElapsedTicks);
        ResetAggregate(ref _diagRenderChromeSkippedBorderCount);
        ResetAggregate(ref _diagRenderChromeDrewBorderCount);
        ResetAggregate(ref _diagDrawTextRenderPlanCallCount);
        ResetAggregate(ref _diagDrawTextRenderPlanElapsedTicks);
        ResetAggregate(ref _diagRenderTextPreparationCallCount);
        ResetAggregate(ref _diagRenderTextPreparationElapsedTicks);
        ResetAggregate(ref _diagRenderTextPreparationNoTextCount);
        ResetAggregate(ref _diagRenderTextPreparationNoSpaceCount);
        ResetAggregate(ref _diagTextRenderPlanCacheHitCount);
        ResetAggregate(ref _diagTextRenderPlanCacheMissCount);
        ResetAggregate(ref _diagTextRenderPlanPreparedLineCount);
        ResetAggregate(ref _diagTextRenderPlanSkippedEmptyLineCount);
        ResetAggregate(ref _diagRenderTextDrawDispatchCallCount);
        ResetAggregate(ref _diagResolveRenderedLineWidthCallCount);
        ResetAggregate(ref _diagResolveRenderedLineWidthLayoutWidthHitCount);
        ResetAggregate(ref _diagResolveRenderedLineWidthFallbackCount);
        ResetAggregate(ref _diagBuildDefaultButtonStyleCallCount);
        ResetAggregate(ref _diagBuildDefaultButtonStyleElapsedTicks);
        ResetAggregate(ref _diagGetFallbackStyleCallCount);
        ResetAggregate(ref _diagGetFallbackStyleElapsedTicks);
        ResetAggregate(ref _diagGetFallbackStyleCacheHitCount);
        ResetAggregate(ref _diagGetFallbackStyleCacheMissCount);
        ResetAggregate(ref _diagRaiseClickEventCallCount);
        ResetAggregate(ref _diagSetMouseOverFromInputCallCount);
        ResetAggregate(ref _diagSetMouseOverFromInputNoOpCount);
        ResetAggregate(ref _diagSetMouseOverFromInputChangedCount);
        ResetAggregate(ref _diagSetPressedFromInputCallCount);
        ResetAggregate(ref _diagSetPressedFromInputNoOpCount);
        ResetAggregate(ref _diagSetPressedFromInputChangedCount);
        ResetAggregate(ref _diagInvokeFromInputCallCount);
        ResetAggregate(ref _diagHasAvailableIndependentDesiredSizeForUniformGridCallCount);
        ResetAggregate(ref _diagHasAvailableIndependentDesiredSizeForUniformGridTrueCount);
        ResetAggregate(ref _diagHasAvailableIndependentDesiredSizeForUniformGridFalseCount);
        return snapshot;
    }

    internal static ButtonTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return CreateAggregateTelemetrySnapshot();
    }

    internal static ButtonTimingSnapshot GetTimingSnapshotForTests()
    {
        return new ButtonTimingSnapshot(
            ReadAggregate(ref _diagMeasureOverrideElapsedTicks),
            ReadAggregate(ref _diagRenderElapsedTicks),
            ReadAggregate(ref _diagResolveTextLayoutElapsedTicks),
            ReadAggregate(ref _diagRenderChromeElapsedTicks),
            ReadAggregate(ref _diagRenderTextPreparationElapsedTicks),
            ReadAggregate(ref _diagDrawTextRenderPlanElapsedTicks),
            SaturateToInt(ReadAggregate(ref _diagRenderTextPreparationCallCount)),
            SaturateToInt(ReadAggregate(ref _diagRenderTextDrawDispatchCallCount)),
            SaturateToInt(ReadAggregate(ref _diagContentPropertyChangedCount)),
            SaturateToInt(ReadAggregate(ref _diagTextLayoutCacheHitCount)),
            SaturateToInt(ReadAggregate(ref _diagTextLayoutCacheMissCount)),
            SaturateToInt(ReadAggregate(ref _diagIntrinsicNoWrapMeasureCacheHitCount)),
            SaturateToInt(ReadAggregate(ref _diagIntrinsicNoWrapMeasureCacheMissCount)),
            SaturateToInt(ReadAggregate(ref _diagTextLayoutInvalidationCount)),
            SaturateToInt(ReadAggregate(ref _diagIntrinsicNoWrapMeasureInvalidationCount)),
            SaturateToInt(ReadAggregate(ref _diagMeasureOverridePlainTextFastPathCount)),
            SaturateToInt(ReadAggregate(ref _diagIntrinsicNoWrapMeasurePathCount)),
            SaturateToInt(ReadAggregate(ref _diagTextLayoutMeasurePathCount)));
    }

    internal static void ResetTimingForTests()
    {
        _ = GetTelemetryAndReset();
    }

    private static ButtonTelemetrySnapshot CreateAggregateTelemetrySnapshot()
    {
        return new ButtonTelemetrySnapshot(
            ReadAggregate(ref _diagConstructorCallCount),
            ReadAggregate(ref _diagMeasureOverrideCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagMeasureOverrideElapsedTicks)),
            ReadAggregate(ref _diagMeasureOverridePlainTextFastPathCount),
            ReadAggregate(ref _diagMeasureOverrideBaseMeasurePathCount),
            ReadAggregate(ref _diagMeasureOverrideChromeOnlyCount),
            ReadAggregate(ref _diagCanReuseMeasureCallCount),
            ReadAggregate(ref _diagCanReuseMeasureAllowedCount),
            ReadAggregate(ref _diagCanReuseMeasureRejectedCount),
            ReadAggregate(ref _diagOnClickCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagOnClickElapsedTicks)),
            ReadAggregate(ref _diagOnClickAutomationNotifyCount),
            ReadAggregate(ref _diagOnClickAutomationSkipCount),
            ReadAggregate(ref _diagOnClickExecuteCommandCount),
            ReadAggregate(ref _diagDependencyPropertyChangedCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagDependencyPropertyChangedElapsedTicks)),
            ReadAggregate(ref _diagContentPropertyChangedCount),
            ReadAggregate(ref _diagTextMetricPropertyChangedCount),
            ReadAggregate(ref _diagOtherPropertyChangedCount),
            ReadAggregate(ref _diagRenderCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagRenderElapsedTicks)),
            ReadAggregate(ref _diagRenderSkippedTemplateRootCount),
            ReadAggregate(ref _diagRenderPreparedTextPlanCount),
            ReadAggregate(ref _diagRenderSkippedNoTextPlanCount),
            ReadAggregate(ref _diagShouldCreateImplicitContentElementCallCount),
            ReadAggregate(ref _diagShouldCreateImplicitContentElementReturnedTrueCount),
            ReadAggregate(ref _diagShouldCreateImplicitContentElementReturnedFalseCount),
            ReadAggregate(ref _diagGetDisplayContentTextCallCount),
            ReadAggregate(ref _diagGetDisplayContentTextReturnedEmptyForContentElementCount),
            ReadAggregate(ref _diagGetDisplayContentTextReturnedEmptyForNullContentCount),
            ReadAggregate(ref _diagGetDisplayContentTextExtractedCount),
            ReadAggregate(ref _diagGetSelfRenderedContentTextCallCount),
            ReadAggregate(ref _diagGetSelfRenderedContentTextReturnedTextCount),
            ReadAggregate(ref _diagGetSelfRenderedContentTextReturnedEmptyCount),
            ReadAggregate(ref _diagResolveTextLayoutCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagResolveTextLayoutElapsedTicks)),
            ReadAggregate(ref _diagTextLayoutCacheHitCount),
            ReadAggregate(ref _diagTextLayoutCacheMissCount),
            ReadAggregate(ref _diagTextLayoutInvalidationCount),
            ReadAggregate(ref _diagTextLayoutInvalidationNoOpCount),
            ReadAggregate(ref _diagIntrinsicNoWrapMeasureInvalidationCount),
            ReadAggregate(ref _diagIntrinsicNoWrapMeasureInvalidationNoOpCount),
            ReadAggregate(ref _diagTextRenderPlanInvalidationCount),
            ReadAggregate(ref _diagTextRenderPlanInvalidationNoOpCount),
            ReadAggregate(ref _diagCanUsePlainTextMeasureFastPathCallCount),
            ReadAggregate(ref _diagPlainTextMeasureFastPathAllowedCount),
            ReadAggregate(ref _diagPlainTextMeasureFastPathBlockedByTemplateCount),
            ReadAggregate(ref _diagPlainTextMeasureFastPathBlockedByTemplateRootCount),
            ReadAggregate(ref _diagPlainTextMeasureFastPathBlockedByContentElementCount),
            ReadAggregate(ref _diagPlainTextMeasureFastPathBlockedByContentTemplateCount),
            ReadAggregate(ref _diagPlainTextMeasureFastPathBlockedByContentTemplateSelectorCount),
            ReadAggregate(ref _diagMeasurePlainTextButtonCallCount),
            ReadAggregate(ref _diagMeasurePlainTextButtonEmptyTextCount),
            ReadAggregate(ref _diagIntrinsicNoWrapMeasurePathCount),
            ReadAggregate(ref _diagTextLayoutMeasurePathCount),
            ReadAggregate(ref _diagCanUseIntrinsicNoWrapTextMeasureCallCount),
            ReadAggregate(ref _diagIntrinsicNoWrapTextMeasureAllowedCount),
            ReadAggregate(ref _diagIntrinsicNoWrapTextMeasureRejectedEmptyTextCount),
            ReadAggregate(ref _diagIntrinsicNoWrapTextMeasureRejectedMultilineTextCount),
            ReadAggregate(ref _diagResolveIntrinsicNoWrapTextSizeCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagResolveIntrinsicNoWrapTextSizeElapsedTicks)),
            ReadAggregate(ref _diagIntrinsicNoWrapMeasureCacheHitCount),
            ReadAggregate(ref _diagIntrinsicNoWrapMeasureCacheMissCount),
            ReadAggregate(ref _diagRenderChromeCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagRenderChromeElapsedTicks)),
            ReadAggregate(ref _diagRenderChromeSkippedBorderCount),
            ReadAggregate(ref _diagRenderChromeDrewBorderCount),
            ReadAggregate(ref _diagDrawTextRenderPlanCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagDrawTextRenderPlanElapsedTicks)),
            ReadAggregate(ref _diagRenderTextPreparationCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagRenderTextPreparationElapsedTicks)),
            ReadAggregate(ref _diagRenderTextPreparationNoTextCount),
            ReadAggregate(ref _diagRenderTextPreparationNoSpaceCount),
            ReadAggregate(ref _diagTextRenderPlanCacheHitCount),
            ReadAggregate(ref _diagTextRenderPlanCacheMissCount),
            ReadAggregate(ref _diagTextRenderPlanPreparedLineCount),
            ReadAggregate(ref _diagTextRenderPlanSkippedEmptyLineCount),
            ReadAggregate(ref _diagRenderTextDrawDispatchCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagDrawTextRenderPlanElapsedTicks)),
            ReadAggregate(ref _diagResolveRenderedLineWidthCallCount),
            ReadAggregate(ref _diagResolveRenderedLineWidthLayoutWidthHitCount),
            ReadAggregate(ref _diagResolveRenderedLineWidthFallbackCount),
            ReadAggregate(ref _diagBuildDefaultButtonStyleCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagBuildDefaultButtonStyleElapsedTicks)),
            ReadAggregate(ref _diagGetFallbackStyleCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagGetFallbackStyleElapsedTicks)),
            ReadAggregate(ref _diagGetFallbackStyleCacheHitCount),
            ReadAggregate(ref _diagGetFallbackStyleCacheMissCount),
            ReadAggregate(ref _diagRaiseClickEventCallCount),
            ReadAggregate(ref _diagSetMouseOverFromInputCallCount),
            ReadAggregate(ref _diagSetMouseOverFromInputNoOpCount),
            ReadAggregate(ref _diagSetMouseOverFromInputChangedCount),
            ReadAggregate(ref _diagSetPressedFromInputCallCount),
            ReadAggregate(ref _diagSetPressedFromInputNoOpCount),
            ReadAggregate(ref _diagSetPressedFromInputChangedCount),
            ReadAggregate(ref _diagInvokeFromInputCallCount),
            ReadAggregate(ref _diagHasAvailableIndependentDesiredSizeForUniformGridCallCount),
            ReadAggregate(ref _diagHasAvailableIndependentDesiredSizeForUniformGridTrueCount),
            ReadAggregate(ref _diagHasAvailableIndependentDesiredSizeForUniformGridFalseCount));
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

    private static bool ThicknessMatches(Thickness cached, Thickness current)
    {
        return WidthMatches(cached.Left, current.Left) &&
               WidthMatches(cached.Top, current.Top) &&
               WidthMatches(cached.Right, current.Right) &&
               WidthMatches(cached.Bottom, current.Bottom);
    }

    private static bool LayoutRectMatches(LayoutRect cached, LayoutRect current)
    {
        return WidthMatches(cached.X, current.X) &&
               WidthMatches(cached.Y, current.Y) &&
               WidthMatches(cached.Width, current.Width) &&
               WidthMatches(cached.Height, current.Height);
    }

    private void IncrementMetric(ref long runtimeField, ref long aggregateField)
    {
        runtimeField++;
        IncrementAggregate(ref aggregateField);
    }

    private void AddMetric(ref long runtimeField, ref long aggregateField, long value)
    {
        runtimeField += value;
        AddAggregate(ref aggregateField, value);
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }

    private static void IncrementAggregate(ref long field)
    {
        Interlocked.Increment(ref field);
    }

    private static void AddAggregate(ref long field, long value)
    {
        Interlocked.Add(ref field, value);
    }

    private static long ReadAggregate(ref long field)
    {
        return Interlocked.Read(ref field);
    }

    private static long ResetAggregate(ref long field)
    {
        return Interlocked.Exchange(ref field, 0);
    }

    private static int SaturateToInt(long value)
    {
        return value > int.MaxValue ? int.MaxValue : (int)value;
    }

    private static Style BuildDefaultButtonStyle()
    {
        var start = Stopwatch.GetTimestamp();
        IncrementAggregate(ref _diagBuildDefaultButtonStyleCallCount);
        try
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
        finally
        {
            AddAggregate(ref _diagBuildDefaultButtonStyleElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    protected override Style? GetFallbackStyle()
    {
        var start = Stopwatch.GetTimestamp();
        IncrementAggregate(ref _diagGetFallbackStyleCallCount);
        try
        {
            if (DefaultButtonStyle.IsValueCreated)
            {
                IncrementAggregate(ref _diagGetFallbackStyleCacheHitCount);
            }
            else
            {
                IncrementAggregate(ref _diagGetFallbackStyleCacheMissCount);
            }

            return DefaultButtonStyle.Value;
        }
        finally
        {
            AddAggregate(ref _diagGetFallbackStyleElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    private void RaiseClickEvent()
    {
        IncrementMetric(ref _runtimeRaiseClickEventCallCount, ref _diagRaiseClickEventCallCount);
        var args = new RoutedSimpleEventArgs(ClickEvent);
        RaiseRoutedEvent(ClickEvent, args);
    }

    internal void SetMouseOverFromInput(bool isMouseOver)
    {
        IncrementMetric(ref _runtimeSetMouseOverFromInputCallCount, ref _diagSetMouseOverFromInputCallCount);
        if (IsMouseOver == isMouseOver)
        {
            IncrementMetric(ref _runtimeSetMouseOverFromInputNoOpCount, ref _diagSetMouseOverFromInputNoOpCount);
            return;
        }

        IncrementMetric(ref _runtimeSetMouseOverFromInputChangedCount, ref _diagSetMouseOverFromInputChangedCount);
        IsMouseOver = isMouseOver;
    }

    internal void SetPressedFromInput(bool isPressed)
    {
        IncrementMetric(ref _runtimeSetPressedFromInputCallCount, ref _diagSetPressedFromInputCallCount);
        if (IsPressed == isPressed)
        {
            IncrementMetric(ref _runtimeSetPressedFromInputNoOpCount, ref _diagSetPressedFromInputNoOpCount);
            return;
        }

        IncrementMetric(ref _runtimeSetPressedFromInputChangedCount, ref _diagSetPressedFromInputChangedCount);
        IsPressed = isPressed;
    }

    internal void InvokeFromInput()
    {
        IncrementMetric(ref _runtimeInvokeFromInputCallCount, ref _diagInvokeFromInputCallCount);
        OnClick();
    }
}

internal readonly record struct ButtonTextRenderPlan(
    TextLayout.TextLayoutResult Layout,
    float LineSpacing,
    IReadOnlyList<ButtonTextLineDraw> LineDraws);

internal readonly record struct ButtonTextLineDraw(
    string Text,
    Vector2 Position);



