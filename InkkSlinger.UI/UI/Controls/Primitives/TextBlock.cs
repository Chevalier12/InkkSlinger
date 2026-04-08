using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using InkkSlinger.UI.Telemetry;

namespace InkkSlinger;

public class TextBlock : FrameworkElement
{
    private static long _diagMeasureOverrideCallCount;
    private static long _diagMeasureOverrideElapsedTicks;
    private static long _diagEmptyMeasureCallCount;
    private static long _diagSameTextSameWidthMeasureCallCount;
    private static long _diagIntrinsicMeasurePathCallCount;
    private static long _diagLayoutMeasurePathCallCount;
    private static long _diagCanReuseMeasureCallCount;
    private static long _diagCanReuseMeasureTrueCount;
    private static long _diagCanReuseMeasureEmptyTextCount;
    private static long _diagCanReuseMeasureNoWrapCount;
    private static long _diagCanReuseMeasureMultilineRejectCount;
    private static long _diagCanReuseMeasureIntrinsicFitCount;
    private static long _diagCanReuseMeasureTooNarrowRejectCount;
    private static long _diagShouldInvalidateMeasureCallCount;
    private static long _diagShouldInvalidateMeasureTextPropertyCallCount;
    private static long _diagShouldInvalidateMeasureBaseFallbackCount;
    private static long _diagShouldInvalidateMeasureReusedDesiredSizeCount;
    private static long _diagShouldInvalidateMeasureChangedDesiredSizeCount;
    private static long _diagTryMeasureDesiredSizeForTextChangeCallCount;
    private static long _diagTryMeasureDesiredSizeForTextChangeSuccessCount;
    private static long _diagTryMeasureDesiredSizeForTextChangeNoPreviousAvailableCount;
    private static long _diagTryMeasureDesiredSizeForTextChangeEmptyTextCount;
    private static long _diagTryMeasureDesiredSizeForTextChangeIntrinsicPathCount;
    private static long _diagTryMeasureDesiredSizeForTextChangeLayoutPathCount;
    private static long _diagTryMeasureDesiredSizeForTextChangeLayoutRoundingCount;
    private static long _diagHasAvailableIndependentDesiredSizeForUniformGridCallCount;
    private static long _diagHasAvailableIndependentDesiredSizeForUniformGridTrueCount;
    private static long _diagHasAvailableIndependentDesiredSizeForUniformGridFalseCount;
    private static long _diagCanUseIntrinsicNoWrapMeasureCallCount;
    private static long _diagCanUseIntrinsicNoWrapMeasureAllowedCount;
    private static long _diagCanUseIntrinsicNoWrapMeasureRejectedWrapCount;
    private static long _diagCanUseIntrinsicNoWrapMeasureRejectedEmptyCount;
    private static long _diagCanUseIntrinsicNoWrapMeasureRejectedMultilineCount;
    private static long _diagCanUseIntrinsicMeasureCallCount;
    private static long _diagCanUseIntrinsicMeasureAllowedNoWrapCount;
    private static long _diagCanUseIntrinsicMeasureAllowedWrappedWidthCount;
    private static long _diagCanUseIntrinsicMeasureRejectedNoWrapUnavailableCount;
    private static long _diagCanUseIntrinsicMeasureRejectedEmptyTextCount;
    private static long _diagCanUseIntrinsicMeasureRejectedMultilineTextCount;
    private static long _diagCanUseIntrinsicMeasureRejectedWidthTooNarrowCount;
    private static long _diagResolveIntrinsicNoWrapTextSizeCallCount;
    private static long _diagResolveIntrinsicNoWrapTextSizeElapsedTicks;
    private static long _diagIntrinsicMeasureCacheHitCount;
    private static long _diagIntrinsicMeasureCacheMissCount;
    private static long _diagResolveLayoutCallCount;
    private static long _diagResolveLayoutElapsedTicks;
    private static long _diagResolveLayoutEmptyTextCount;
    private static long _diagResolveLayoutSameTextSameWidthCallCount;
    private static long _diagResolveLayoutCacheHitCount;
    private static long _diagResolveLayoutPrimaryCacheHitCount;
    private static long _diagResolveLayoutSecondaryCacheHitCount;
    private static long _diagResolveLayoutCacheMissCount;
    private static long _diagResolveLayoutUncachedCallCount;
    private static long _diagResolveLayoutUncachedElapsedTicks;
    private static long _diagTextPropertyChangeCount;
    private static long _diagLayoutAffectingPropertyChangeCount;
    private static long _diagOtherPropertyChangeCount;
    private static long _diagLayoutCacheInvalidationCount;
    private static long _diagLayoutCacheInvalidationNoOpCount;
    private static long _diagIntrinsicMeasureInvalidationCount;
    private static long _diagIntrinsicMeasureInvalidationNoOpCount;
    private static long _diagRenderCallCount;
    private static long _diagRenderElapsedTicks;
    private static long _diagRenderEmptyTextSkipCount;
    private static long _diagRenderLineIterationCount;
    private static long _diagRenderEmptyLineSkipCount;
    private static long _diagRenderAxisAlignedClipFastPathCount;
    private static long _diagRenderTransformedClipPathCount;
    private static long _diagRenderClipSkipCount;
    private static long _diagRenderClipBreakCount;
    private static long _diagRenderDrawLineCount;
    private static long _diagRenderTextDecorationsCallCount;

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(TextBlock),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(TextBlock),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TextWrappingProperty =
        DependencyProperty.Register(
            nameof(TextWrapping),
            typeof(TextWrapping),
            typeof(TextBlock),
            new FrameworkPropertyMetadata(
                TextWrapping.NoWrap,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineHeightProperty =
        DependencyProperty.Register(
            nameof(LineHeight),
            typeof(float),
            typeof(TextBlock),
            new FrameworkPropertyMetadata(
                float.NaN,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender),
            static value => value is float lineHeight && (float.IsNaN(lineHeight) || lineHeight > 0f));

    public static readonly DependencyProperty CharacterSpacingProperty =
        DependencyProperty.Register(
            nameof(CharacterSpacing),
            typeof(int),
            typeof(TextBlock),
            new FrameworkPropertyMetadata(
                0,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    private bool _hasLayoutCache;
    private bool _hasSecondaryLayoutCache;
    private bool _hasIntrinsicNoWrapMeasureCache;
    private int _layoutCacheTextVersion = -1;
    private int _intrinsicNoWrapMeasureTextVersion = -1;
    private float _layoutCacheWidth = float.NaN;
    private UiTypography? _layoutCacheTypography;
    private UiTypography? _intrinsicNoWrapMeasureTypography;
    private TextWrapping _layoutCacheWrapping = TextWrapping.NoWrap;
    private TextLayout.TextLayoutResult _layoutCacheResult = TextLayout.TextLayoutResult.Empty;
    private Vector2 _intrinsicNoWrapMeasureSize = Vector2.Zero;
    private int _textVersion;
    private int _layoutCacheHitCount;
    private int _layoutCacheMissCount;
    private int _renderSampleCount;
    private long _renderTotalTicks;
    private long _renderMaxTicks;
    private long _renderLastTicks;
    private float _layoutCacheFontSize = float.NaN;
    private float _intrinsicNoWrapMeasureFontSize = float.NaN;
    private int _lastRenderedLineCount;
    private float _lastRenderedLayoutWidth = float.NaN;
    private string _lastRenderedLayoutText = string.Empty;
    private float _layoutCacheLineHeight = float.NaN;
    private int _secondaryLayoutCacheTextVersion = -1;
    private float _secondaryLayoutCacheWidth = float.NaN;
    private UiTypography? _secondaryLayoutCacheTypography;
    private TextWrapping _secondaryLayoutCacheWrapping = TextWrapping.NoWrap;
    private TextLayout.TextLayoutResult _secondaryLayoutCacheResult = TextLayout.TextLayoutResult.Empty;
    private float _secondaryLayoutCacheFontSize = float.NaN;
    private float _secondaryLayoutCacheLineHeight = float.NaN;
    private float _intrinsicNoWrapMeasureLineHeight = float.NaN;
    private bool _hasLastMeasuredWrappedLayout;
    private float _lastMeasuredWrappedLayoutWidth = float.NaN;
    private TextLayout.TextLayoutResult _lastMeasuredWrappedLayout = TextLayout.TextLayoutResult.Empty;
    private int _runtimeMeasureOverrideCallCount;
    private long _runtimeMeasureOverrideElapsedTicks;
    private int _runtimeEmptyMeasureCallCount;
    private int _runtimeSameTextSameWidthMeasureCallCount;
    private int _runtimeIntrinsicMeasurePathCallCount;
    private int _runtimeLayoutMeasurePathCallCount;
    private int _runtimeCanReuseMeasureCallCount;
    private int _runtimeCanReuseMeasureTrueCount;
    private int _runtimeCanReuseMeasureEmptyTextCount;
    private int _runtimeCanReuseMeasureNoWrapCount;
    private int _runtimeCanReuseMeasureMultilineRejectCount;
    private int _runtimeCanReuseMeasureIntrinsicFitCount;
    private int _runtimeCanReuseMeasureTooNarrowRejectCount;
    private int _runtimeShouldInvalidateMeasureCallCount;
    private int _runtimeShouldInvalidateMeasureTextPropertyCallCount;
    private int _runtimeShouldInvalidateMeasureBaseFallbackCount;
    private int _runtimeShouldInvalidateMeasureReusedDesiredSizeCount;
    private int _runtimeShouldInvalidateMeasureChangedDesiredSizeCount;
    private int _runtimeTryMeasureDesiredSizeForTextChangeCallCount;
    private int _runtimeTryMeasureDesiredSizeForTextChangeSuccessCount;
    private int _runtimeTryMeasureDesiredSizeForTextChangeNoPreviousAvailableCount;
    private int _runtimeTryMeasureDesiredSizeForTextChangeEmptyTextCount;
    private int _runtimeTryMeasureDesiredSizeForTextChangeIntrinsicPathCount;
    private int _runtimeTryMeasureDesiredSizeForTextChangeLayoutPathCount;
    private int _runtimeTryMeasureDesiredSizeForTextChangeLayoutRoundingCount;
    private int _runtimeHasAvailableIndependentDesiredSizeForUniformGridCallCount;
    private int _runtimeHasAvailableIndependentDesiredSizeForUniformGridTrueCount;
    private int _runtimeHasAvailableIndependentDesiredSizeForUniformGridFalseCount;
    private int _runtimeCanUseIntrinsicNoWrapMeasureCallCount;
    private int _runtimeCanUseIntrinsicNoWrapMeasureAllowedCount;
    private int _runtimeCanUseIntrinsicNoWrapMeasureRejectedWrapCount;
    private int _runtimeCanUseIntrinsicNoWrapMeasureRejectedEmptyCount;
    private int _runtimeCanUseIntrinsicNoWrapMeasureRejectedMultilineCount;
    private int _runtimeCanUseIntrinsicMeasureCallCount;
    private int _runtimeCanUseIntrinsicMeasureAllowedNoWrapCount;
    private int _runtimeCanUseIntrinsicMeasureAllowedWrappedWidthCount;
    private int _runtimeCanUseIntrinsicMeasureRejectedNoWrapUnavailableCount;
    private int _runtimeCanUseIntrinsicMeasureRejectedEmptyTextCount;
    private int _runtimeCanUseIntrinsicMeasureRejectedMultilineTextCount;
    private int _runtimeCanUseIntrinsicMeasureRejectedWidthTooNarrowCount;
    private int _runtimeResolveIntrinsicNoWrapTextSizeCallCount;
    private long _runtimeResolveIntrinsicNoWrapTextSizeElapsedTicks;
    private int _runtimeIntrinsicMeasureCacheHitCount;
    private int _runtimeIntrinsicMeasureCacheMissCount;
    private int _runtimeResolveLayoutCallCount;
    private long _runtimeResolveLayoutElapsedTicks;
    private int _runtimeResolveLayoutEmptyTextCount;
    private int _runtimeResolveLayoutCacheHitCount;
    private int _runtimeResolveLayoutPrimaryCacheHitCount;
    private int _runtimeResolveLayoutSecondaryCacheHitCount;
    private int _runtimeResolveLayoutCacheMissCount;
    private int _runtimeResolveLayoutSameTextSameWidthCallCount;
    private int _runtimeResolveLayoutUncachedCallCount;
    private long _runtimeResolveLayoutUncachedElapsedTicks;
    private int _runtimeTextPropertyChangeCount;
    private int _runtimeLayoutAffectingPropertyChangeCount;
    private int _runtimeOtherPropertyChangeCount;
    private int _runtimeLayoutCacheInvalidationCount;
    private int _runtimeLayoutCacheInvalidationNoOpCount;
    private int _runtimeIntrinsicMeasureInvalidationCount;
    private int _runtimeIntrinsicMeasureInvalidationNoOpCount;
    private int _runtimeRenderCallCount;
    private long _runtimeRenderElapsedTicks;
    private int _runtimeRenderEmptyTextSkipCount;
    private int _runtimeRenderLineIterationCount;
    private int _runtimeRenderEmptyLineSkipCount;
    private int _runtimeRenderAxisAlignedClipFastPathCount;
    private int _runtimeRenderTransformedClipPathCount;
    private int _runtimeRenderClipSkipCount;
    private int _runtimeRenderClipBreakCount;
    private int _runtimeRenderDrawLineCount;
    private int _runtimeRenderTextDecorationsCallCount;
    private int _runtimeLastMeasureTextVersion = -1;
    private float _runtimeLastMeasureWidth = float.NaN;
    private TextWrapping _runtimeLastMeasureWrapping = TextWrapping.NoWrap;
    private float _runtimeLastMeasureFontSize = float.NaN;
    private float _runtimeLastMeasureLineHeight = float.NaN;

    public string Text
    {
        get => GetValue<string>(TextProperty) ?? string.Empty;
        set => SetValue(TextProperty, value);
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

    public float LineHeight
    {
        get => GetValue<float>(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    public int CharacterSpacing
    {
        get => GetValue<int>(CharacterSpacingProperty);
        set => SetValue(CharacterSpacingProperty, value);
    }

    public TextBlockPerformanceSnapshot GetPerformanceSnapshot()
    {
        return new TextBlockPerformanceSnapshot(
            _layoutCacheHitCount,
            _layoutCacheMissCount,
            _renderSampleCount,
            TicksToMilliseconds(_renderLastTicks),
            AverageTicksToMilliseconds(_renderTotalTicks, _renderSampleCount),
            TicksToMilliseconds(_renderMaxTicks));
    }

    public void ResetPerformanceSnapshot()
    {
        _layoutCacheHitCount = 0;
        _layoutCacheMissCount = 0;
        _renderSampleCount = 0;
        _renderTotalTicks = 0L;
        _renderMaxTicks = 0L;
        _renderLastTicks = 0L;
    }

    internal void PrimeLayoutCacheForTests(float width)
    {
        _ = ResolveLayout(width);
    }

    internal bool HasAvailableIndependentDesiredSizeForUniformGrid()
    {
        _runtimeHasAvailableIndependentDesiredSizeForUniformGridCallCount++;
        IncrementAggregate(ref _diagHasAvailableIndependentDesiredSizeForUniformGridCallCount);
        if (TextWrapping == TextWrapping.NoWrap)
        {
            _runtimeHasAvailableIndependentDesiredSizeForUniformGridTrueCount++;
            IncrementAggregate(ref _diagHasAvailableIndependentDesiredSizeForUniformGridTrueCount);
            return true;
        }

        _runtimeHasAvailableIndependentDesiredSizeForUniformGridFalseCount++;
        IncrementAggregate(ref _diagHasAvailableIndependentDesiredSizeForUniformGridFalseCount);
        return false;
    }

    internal int LastRenderedLineCountForTests => _lastRenderedLineCount;

    internal float LastRenderedLayoutWidthForTests => _lastRenderedLayoutWidth;

    internal string LastRenderedLayoutTextForTests => _lastRenderedLayoutText;

    internal TextBlockRuntimeDiagnosticsSnapshot GetTextBlockSnapshotForDiagnostics()
    {
        return new TextBlockRuntimeDiagnosticsSnapshot(
            _hasLayoutCache,
            _hasSecondaryLayoutCache,
            _hasIntrinsicNoWrapMeasureCache,
            _textVersion,
            _layoutCacheWidth,
            _secondaryLayoutCacheWidth,
            _runtimeMeasureOverrideCallCount,
            TicksToMilliseconds(_runtimeMeasureOverrideElapsedTicks),
            _runtimeEmptyMeasureCallCount,
            _runtimeSameTextSameWidthMeasureCallCount,
            _runtimeIntrinsicMeasurePathCallCount,
            _runtimeLayoutMeasurePathCallCount,
            _runtimeCanReuseMeasureCallCount,
            _runtimeCanReuseMeasureTrueCount,
            _runtimeCanReuseMeasureEmptyTextCount,
            _runtimeCanReuseMeasureNoWrapCount,
            _runtimeCanReuseMeasureMultilineRejectCount,
            _runtimeCanReuseMeasureIntrinsicFitCount,
            _runtimeCanReuseMeasureTooNarrowRejectCount,
            _runtimeShouldInvalidateMeasureCallCount,
            _runtimeShouldInvalidateMeasureTextPropertyCallCount,
            _runtimeShouldInvalidateMeasureBaseFallbackCount,
            _runtimeShouldInvalidateMeasureReusedDesiredSizeCount,
            _runtimeShouldInvalidateMeasureChangedDesiredSizeCount,
            _runtimeTryMeasureDesiredSizeForTextChangeCallCount,
            _runtimeTryMeasureDesiredSizeForTextChangeSuccessCount,
            _runtimeTryMeasureDesiredSizeForTextChangeNoPreviousAvailableCount,
            _runtimeTryMeasureDesiredSizeForTextChangeEmptyTextCount,
            _runtimeTryMeasureDesiredSizeForTextChangeIntrinsicPathCount,
            _runtimeTryMeasureDesiredSizeForTextChangeLayoutPathCount,
            _runtimeTryMeasureDesiredSizeForTextChangeLayoutRoundingCount,
            _runtimeHasAvailableIndependentDesiredSizeForUniformGridCallCount,
            _runtimeHasAvailableIndependentDesiredSizeForUniformGridTrueCount,
            _runtimeHasAvailableIndependentDesiredSizeForUniformGridFalseCount,
            _runtimeCanUseIntrinsicNoWrapMeasureCallCount,
            _runtimeCanUseIntrinsicNoWrapMeasureAllowedCount,
            _runtimeCanUseIntrinsicNoWrapMeasureRejectedWrapCount,
            _runtimeCanUseIntrinsicNoWrapMeasureRejectedEmptyCount,
            _runtimeCanUseIntrinsicNoWrapMeasureRejectedMultilineCount,
            _runtimeCanUseIntrinsicMeasureCallCount,
            _runtimeCanUseIntrinsicMeasureAllowedNoWrapCount,
            _runtimeCanUseIntrinsicMeasureAllowedWrappedWidthCount,
            _runtimeCanUseIntrinsicMeasureRejectedNoWrapUnavailableCount,
            _runtimeCanUseIntrinsicMeasureRejectedEmptyTextCount,
            _runtimeCanUseIntrinsicMeasureRejectedMultilineTextCount,
            _runtimeCanUseIntrinsicMeasureRejectedWidthTooNarrowCount,
            _runtimeResolveIntrinsicNoWrapTextSizeCallCount,
            TicksToMilliseconds(_runtimeResolveIntrinsicNoWrapTextSizeElapsedTicks),
            _runtimeIntrinsicMeasureCacheHitCount,
            _runtimeIntrinsicMeasureCacheMissCount,
            _runtimeResolveLayoutCallCount,
            TicksToMilliseconds(_runtimeResolveLayoutElapsedTicks),
            _runtimeResolveLayoutEmptyTextCount,
            _runtimeResolveLayoutCacheHitCount,
            _runtimeResolveLayoutPrimaryCacheHitCount,
            _runtimeResolveLayoutSecondaryCacheHitCount,
            _runtimeResolveLayoutCacheMissCount,
            _runtimeResolveLayoutSameTextSameWidthCallCount,
            _runtimeResolveLayoutUncachedCallCount,
            TicksToMilliseconds(_runtimeResolveLayoutUncachedElapsedTicks),
            _runtimeTextPropertyChangeCount,
            _runtimeLayoutAffectingPropertyChangeCount,
            _runtimeOtherPropertyChangeCount,
            _runtimeLayoutCacheInvalidationCount,
            _runtimeLayoutCacheInvalidationNoOpCount,
            _runtimeIntrinsicMeasureInvalidationCount,
            _runtimeIntrinsicMeasureInvalidationNoOpCount,
            _runtimeRenderCallCount,
            TicksToMilliseconds(_runtimeRenderElapsedTicks),
            _runtimeRenderEmptyTextSkipCount,
            _runtimeRenderLineIterationCount,
            _runtimeRenderEmptyLineSkipCount,
            _runtimeRenderAxisAlignedClipFastPathCount,
            _runtimeRenderTransformedClipPathCount,
            _runtimeRenderClipSkipCount,
            _runtimeRenderClipBreakCount,
            _runtimeRenderDrawLineCount,
            _runtimeRenderTextDecorationsCallCount);
    }

    internal TextBlockRuntimeDiagnosticsSnapshot GetRuntimeDiagnosticsForTests()
    {
        return GetTextBlockSnapshotForDiagnostics();
    }

    internal static TextBlockTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return CreateAggregateTelemetrySnapshot(reset: false);
    }

    internal static TextBlockTelemetrySnapshot GetTelemetryAndReset()
    {
        return CreateAggregateTelemetrySnapshot(reset: true);
    }

    protected virtual string GetLayoutText()
    {
        return Text;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeMeasureOverrideCallCount++;
        IncrementAggregate(ref _diagMeasureOverrideCallCount);
        try
        {
            var layoutText = GetLayoutText();
            if (string.IsNullOrEmpty(layoutText))
            {
                _runtimeEmptyMeasureCallCount++;
                IncrementAggregate(ref _diagEmptyMeasureCallCount);
                return Vector2.Zero;
            }

            var effectiveAvailableWidth = ResolveMeasureTextLayoutWidth(availableSize.X);
            if (ShouldCollapseWrappedMeasure(effectiveAvailableWidth))
            {
                return Vector2.Zero;
            }

            var sameTextSameWidth = _runtimeLastMeasureTextVersion == _textVersion &&
                                    FloatMatches(_runtimeLastMeasureWidth, effectiveAvailableWidth) &&
                                    _runtimeLastMeasureWrapping == TextWrapping &&
                                    FloatMatches(_runtimeLastMeasureFontSize, FontSize) &&
                                    FloatMatches(_runtimeLastMeasureLineHeight, LineHeight);
            if (sameTextSameWidth)
            {
                _runtimeSameTextSameWidthMeasureCallCount++;
                IncrementAggregate(ref _diagSameTextSameWidthMeasureCallCount);
            }

            _runtimeLastMeasureTextVersion = _textVersion;
            _runtimeLastMeasureWidth = effectiveAvailableWidth;
            _runtimeLastMeasureWrapping = TextWrapping;
            _runtimeLastMeasureFontSize = FontSize;
            _runtimeLastMeasureLineHeight = LineHeight;

            if (CanUseIntrinsicMeasure(layoutText, effectiveAvailableWidth))
            {
                _hasLastMeasuredWrappedLayout = false;
                _runtimeIntrinsicMeasurePathCallCount++;
                IncrementAggregate(ref _diagIntrinsicMeasurePathCallCount);
                return ResolveIntrinsicNoWrapTextSize(layoutText);
            }

            _runtimeLayoutMeasurePathCallCount++;
            IncrementAggregate(ref _diagLayoutMeasurePathCallCount);
            var availableWidth = TextWrapping == TextWrapping.NoWrap
                ? float.PositiveInfinity
                : effectiveAvailableWidth;
            var layout = ResolveLayout(availableWidth);
            if (TextWrapping != TextWrapping.NoWrap)
            {
                _hasLastMeasuredWrappedLayout = true;
                _lastMeasuredWrappedLayoutWidth = effectiveAvailableWidth;
                _lastMeasuredWrappedLayout = layout;
            }

            return layout.Size;
        }
        finally
        {
            var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _runtimeMeasureOverrideElapsedTicks += elapsedTicks;
            AddAggregate(ref _diagMeasureOverrideElapsedTicks, elapsedTicks);
        }
    }

    protected override bool CanReuseMeasureForAvailableSizeChange(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
    {
        _runtimeCanReuseMeasureCallCount++;
        IncrementAggregate(ref _diagCanReuseMeasureCallCount);
        var layoutText = GetLayoutText();
        if (string.IsNullOrEmpty(layoutText))
        {
            _runtimeCanReuseMeasureTrueCount++;
            _runtimeCanReuseMeasureEmptyTextCount++;
            IncrementAggregate(ref _diagCanReuseMeasureTrueCount);
            IncrementAggregate(ref _diagCanReuseMeasureEmptyTextCount);
            return true;
        }

        if (TextWrapping == TextWrapping.NoWrap)
        {
            _runtimeCanReuseMeasureTrueCount++;
            _runtimeCanReuseMeasureNoWrapCount++;
            IncrementAggregate(ref _diagCanReuseMeasureTrueCount);
            IncrementAggregate(ref _diagCanReuseMeasureNoWrapCount);
            return true;
        }

        if (layoutText.IndexOfAny(['\r', '\n']) >= 0)
        {
            _runtimeCanReuseMeasureMultilineRejectCount++;
            IncrementAggregate(ref _diagCanReuseMeasureMultilineRejectCount);
            return false;
        }

        var previousWidth = ResolveMeasureTextLayoutWidth(previousAvailableSize.X);
        var nextWidth = ResolveMeasureTextLayoutWidth(nextAvailableSize.X);
        if (CanReuseWrappedLayoutForWidthRange(previousWidth, nextWidth))
        {
            _runtimeCanReuseMeasureTrueCount++;
            _runtimeCanReuseMeasureIntrinsicFitCount++;
            IncrementAggregate(ref _diagCanReuseMeasureTrueCount);
            IncrementAggregate(ref _diagCanReuseMeasureIntrinsicFitCount);
            return true;
        }

        var intrinsicSize = ResolveIntrinsicNoWrapTextSize(layoutText);
        if (previousWidth >= intrinsicSize.X &&
            nextWidth >= intrinsicSize.X)
        {
            _runtimeCanReuseMeasureTrueCount++;
            _runtimeCanReuseMeasureIntrinsicFitCount++;
            IncrementAggregate(ref _diagCanReuseMeasureTrueCount);
            IncrementAggregate(ref _diagCanReuseMeasureIntrinsicFitCount);
            return true;
        }

        _runtimeCanReuseMeasureTooNarrowRejectCount++;
        IncrementAggregate(ref _diagCanReuseMeasureTooNarrowRejectCount);
        return false;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        base.OnRender(spriteBatch);

        var renderStart = Stopwatch.GetTimestamp();
        _runtimeRenderCallCount++;
        IncrementAggregate(ref _diagRenderCallCount);

        try
        {
            var layoutText = GetLayoutText();
            if (string.IsNullOrEmpty(layoutText))
            {
                _runtimeRenderEmptyTextSkipCount++;
                IncrementAggregate(ref _diagRenderEmptyTextSkipCount);
                return;
            }

            if (ShouldCollapseWrappedMeasure(RenderSize.X) || RenderSize.Y <= 0.01f)
            {
                _runtimeRenderEmptyTextSkipCount++;
                IncrementAggregate(ref _diagRenderEmptyTextSkipCount);
                return;
            }

            var renderWidth = TextWrapping == TextWrapping.NoWrap
                ? float.PositiveInfinity
                : RenderSize.X;
            var layout = ResolveLayout(renderWidth);
            _lastRenderedLineCount = layout.Lines.Count;
            _lastRenderedLayoutWidth = renderWidth;
            _lastRenderedLayoutText = string.Join("\n", layout.Lines);
            var typography = UiTextRenderer.ResolveTypography(this, FontSize);
            var lineSpacing = ResolveLineHeight(typography);
            var drawColor = Foreground * Opacity;
            var currentClip = spriteBatch.GraphicsDevice.ScissorRectangle;
            var useAxisAlignedClipFastPath = UiDrawing.TryGetAxisAligned2DTransformInfo(spriteBatch, out _, out var scaleY, out _, out var offsetY) && scaleY > 0f;
            if (useAxisAlignedClipFastPath)
            {
                _runtimeRenderAxisAlignedClipFastPathCount++;
                IncrementAggregate(ref _diagRenderAxisAlignedClipFastPathCount);
            }
            else
            {
                _runtimeRenderTransformedClipPathCount++;
                IncrementAggregate(ref _diagRenderTransformedClipPathCount);
            }

            var transformedBaseY = useAxisAlignedClipFastPath
                ? (LayoutSlot.Y * scaleY) + offsetY
                : 0f;
            var transformedLineSpacing = useAxisAlignedClipFastPath
                ? lineSpacing * scaleY
                : 0f;
            for (var i = 0; i < layout.Lines.Count; i++)
            {
                _runtimeRenderLineIterationCount++;
                IncrementAggregate(ref _diagRenderLineIterationCount);
                var line = layout.Lines[i];
                if (line.Length == 0)
                {
                    _runtimeRenderEmptyLineSkipCount++;
                    IncrementAggregate(ref _diagRenderEmptyLineSkipCount);
                    continue;
                }

                var position = new Vector2(LayoutSlot.X, LayoutSlot.Y + (i * lineSpacing));
                if (useAxisAlignedClipFastPath)
                {
                    var transformedLineTop = transformedBaseY + (i * transformedLineSpacing);
                    var transformedLineBottom = transformedLineTop + transformedLineSpacing;
                    if (transformedLineBottom < currentClip.Y)
                    {
                        _runtimeRenderClipSkipCount++;
                        IncrementAggregate(ref _diagRenderClipSkipCount);
                        continue;
                    }

                    if (transformedLineTop > currentClip.Bottom)
                    {
                        _runtimeRenderClipBreakCount++;
                        IncrementAggregate(ref _diagRenderClipBreakCount);
                        break;
                    }
                }
                else
                {
                    var lineWidth = i < layout.LineWidths.Count
                        ? layout.LineWidths[i]
                        : UiTextRenderer.MeasureWidth(typography, line);
                    var transformedBounds = UiDrawing.TransformRectBounds(
                        spriteBatch,
                        new LayoutRect(position.X, position.Y, lineWidth, lineSpacing));
                    if (transformedBounds.Y + transformedBounds.Height < currentClip.Y)
                    {
                        _runtimeRenderClipSkipCount++;
                        IncrementAggregate(ref _diagRenderClipSkipCount);
                        continue;
                    }

                    if (transformedBounds.Y > currentClip.Bottom)
                    {
                        _runtimeRenderClipBreakCount++;
                        IncrementAggregate(ref _diagRenderClipBreakCount);
                        break;
                    }
                }

                _runtimeRenderDrawLineCount++;
                IncrementAggregate(ref _diagRenderDrawLineCount);
                UiTextRenderer.DrawString(spriteBatch, typography, line, position, drawColor);
            }

            _runtimeRenderTextDecorationsCallCount++;
            IncrementAggregate(ref _diagRenderTextDecorationsCallCount);
            OnRenderTextDecorations(spriteBatch, layout, lineSpacing);
        }
        finally
        {
            var renderTicks = Stopwatch.GetTimestamp() - renderStart;
            _runtimeRenderElapsedTicks += renderTicks;
            AddAggregate(ref _diagRenderElapsedTicks, renderTicks);
            _renderSampleCount++;
            _renderTotalTicks += renderTicks;
            _renderMaxTicks = System.Math.Max(_renderMaxTicks, renderTicks);
            _renderLastTicks = renderTicks;
        }
    }

    protected virtual void OnRenderTextDecorations(SpriteBatch spriteBatch, TextLayout.TextLayoutResult layout, float lineSpacing)
    {
        _ = spriteBatch;
        _ = layout;
        _ = lineSpacing;
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (ReferenceEquals(args.Property, TextProperty))
        {
            _textVersion++;
            _runtimeTextPropertyChangeCount++;
            IncrementAggregate(ref _diagTextPropertyChangeCount);
            InvalidateLayoutCache();
            InvalidateIntrinsicNoWrapMeasureCache();
            return;
        }

        if (ReferenceEquals(args.Property, TextWrappingProperty) ||
            ReferenceEquals(args.Property, LineHeightProperty) ||
            ReferenceEquals(args.Property, CharacterSpacingProperty) ||
            ReferenceEquals(args.Property, FontSizeProperty) ||
            ReferenceEquals(args.Property, FontFamilyProperty) ||
            ReferenceEquals(args.Property, FontWeightProperty) ||
            ReferenceEquals(args.Property, FontStyleProperty))
        {
            _runtimeLayoutAffectingPropertyChangeCount++;
            IncrementAggregate(ref _diagLayoutAffectingPropertyChangeCount);
            InvalidateLayoutCache();
            InvalidateIntrinsicNoWrapMeasureCache();
            return;
        }

        _runtimeOtherPropertyChangeCount++;
        IncrementAggregate(ref _diagOtherPropertyChangeCount);
    }

    protected override bool ShouldInvalidateMeasureForPropertyChange(
        DependencyPropertyChangedEventArgs args,
        FrameworkPropertyMetadata metadata)
    {
        _runtimeShouldInvalidateMeasureCallCount++;
        IncrementAggregate(ref _diagShouldInvalidateMeasureCallCount);
        if (!ReferenceEquals(args.Property, TextProperty))
        {
            _runtimeShouldInvalidateMeasureBaseFallbackCount++;
            IncrementAggregate(ref _diagShouldInvalidateMeasureBaseFallbackCount);
            return base.ShouldInvalidateMeasureForPropertyChange(args, metadata);
        }

        _runtimeShouldInvalidateMeasureTextPropertyCallCount++;
        IncrementAggregate(ref _diagShouldInvalidateMeasureTextPropertyCallCount);

        if (!IsMeasureValidForTests ||
            NeedsMeasure ||
            !TryMeasureDesiredSizeForTextChange(GetLayoutText(), out var nextDesiredSize))
        {
            _runtimeShouldInvalidateMeasureBaseFallbackCount++;
            IncrementAggregate(ref _diagShouldInvalidateMeasureBaseFallbackCount);
            return base.ShouldInvalidateMeasureForPropertyChange(args, metadata);
        }

        if (!FloatMatches(nextDesiredSize.X, DesiredSize.X) ||
            !FloatMatches(nextDesiredSize.Y, DesiredSize.Y))
        {
            _runtimeShouldInvalidateMeasureChangedDesiredSizeCount++;
            IncrementAggregate(ref _diagShouldInvalidateMeasureChangedDesiredSizeCount);
            return true;
        }

        _runtimeShouldInvalidateMeasureReusedDesiredSizeCount++;
        IncrementAggregate(ref _diagShouldInvalidateMeasureReusedDesiredSizeCount);
        return false;
    }

    private bool CanUseIntrinsicNoWrapTextMeasure(string layoutText)
    {
        _runtimeCanUseIntrinsicNoWrapMeasureCallCount++;
        IncrementAggregate(ref _diagCanUseIntrinsicNoWrapMeasureCallCount);
        if (TextWrapping != TextWrapping.NoWrap)
        {
            _runtimeCanUseIntrinsicNoWrapMeasureRejectedWrapCount++;
            IncrementAggregate(ref _diagCanUseIntrinsicNoWrapMeasureRejectedWrapCount);
            return false;
        }

        if (string.IsNullOrEmpty(layoutText))
        {
            _runtimeCanUseIntrinsicNoWrapMeasureRejectedEmptyCount++;
            IncrementAggregate(ref _diagCanUseIntrinsicNoWrapMeasureRejectedEmptyCount);
            return false;
        }

        if (layoutText.IndexOfAny(['\r', '\n']) >= 0)
        {
            _runtimeCanUseIntrinsicNoWrapMeasureRejectedMultilineCount++;
            IncrementAggregate(ref _diagCanUseIntrinsicNoWrapMeasureRejectedMultilineCount);
            return false;
        }

        _runtimeCanUseIntrinsicNoWrapMeasureAllowedCount++;
        IncrementAggregate(ref _diagCanUseIntrinsicNoWrapMeasureAllowedCount);
        return true;
    }

    private bool CanUseIntrinsicMeasure(string layoutText, float availableWidth)
    {
        _runtimeCanUseIntrinsicMeasureCallCount++;
        IncrementAggregate(ref _diagCanUseIntrinsicMeasureCallCount);
        if (!CanUseIntrinsicNoWrapTextMeasure(layoutText))
        {
            if (TextWrapping == TextWrapping.NoWrap ||
                string.IsNullOrEmpty(layoutText) ||
                layoutText.IndexOfAny(['\r', '\n']) >= 0)
            {
                if (TextWrapping == TextWrapping.NoWrap)
                {
                    _runtimeCanUseIntrinsicMeasureRejectedNoWrapUnavailableCount++;
                    IncrementAggregate(ref _diagCanUseIntrinsicMeasureRejectedNoWrapUnavailableCount);
                }
                else if (string.IsNullOrEmpty(layoutText))
                {
                    _runtimeCanUseIntrinsicMeasureRejectedEmptyTextCount++;
                    IncrementAggregate(ref _diagCanUseIntrinsicMeasureRejectedEmptyTextCount);
                }
                else
                {
                    _runtimeCanUseIntrinsicMeasureRejectedMultilineTextCount++;
                    IncrementAggregate(ref _diagCanUseIntrinsicMeasureRejectedMultilineTextCount);
                }
                return false;
            }

            if (availableWidth >= ResolveIntrinsicNoWrapTextSize(layoutText).X)
            {
                _runtimeCanUseIntrinsicMeasureAllowedWrappedWidthCount++;
                IncrementAggregate(ref _diagCanUseIntrinsicMeasureAllowedWrappedWidthCount);
                return true;
            }

            _runtimeCanUseIntrinsicMeasureRejectedWidthTooNarrowCount++;
            IncrementAggregate(ref _diagCanUseIntrinsicMeasureRejectedWidthTooNarrowCount);
            return false;
        }

        _runtimeCanUseIntrinsicMeasureAllowedNoWrapCount++;
        IncrementAggregate(ref _diagCanUseIntrinsicMeasureAllowedNoWrapCount);
        return true;
    }

    private bool TryMeasureDesiredSizeForTextChange(string layoutText, out Vector2 desiredSize)
    {
        _runtimeTryMeasureDesiredSizeForTextChangeCallCount++;
        IncrementAggregate(ref _diagTryMeasureDesiredSizeForTextChangeCallCount);
        desiredSize = DesiredSize;
        var availableWidth = PreviousAvailableSizeForTests.X;
        var availableHeight = PreviousAvailableSizeForTests.Y;
        if (float.IsNaN(availableWidth) || float.IsNaN(availableHeight))
        {
            _runtimeTryMeasureDesiredSizeForTextChangeNoPreviousAvailableCount++;
            IncrementAggregate(ref _diagTryMeasureDesiredSizeForTextChangeNoPreviousAvailableCount);
            return false;
        }

        Vector2 measured;
        if (string.IsNullOrEmpty(layoutText))
        {
            _runtimeTryMeasureDesiredSizeForTextChangeEmptyTextCount++;
            IncrementAggregate(ref _diagTryMeasureDesiredSizeForTextChangeEmptyTextCount);
            measured = Vector2.Zero;
        }
        else
        {
            var effectiveAvailableWidth = ResolveMeasureTextLayoutWidth(availableWidth);
            if (ShouldCollapseWrappedMeasure(effectiveAvailableWidth))
            {
                measured = Vector2.Zero;
            }
            else if (CanUseIntrinsicMeasure(layoutText, effectiveAvailableWidth))
            {
                _runtimeTryMeasureDesiredSizeForTextChangeIntrinsicPathCount++;
                IncrementAggregate(ref _diagTryMeasureDesiredSizeForTextChangeIntrinsicPathCount);
                measured = ResolveIntrinsicNoWrapTextSizeUncached(layoutText);
            }
            else
            {
                _runtimeTryMeasureDesiredSizeForTextChangeLayoutPathCount++;
                IncrementAggregate(ref _diagTryMeasureDesiredSizeForTextChangeLayoutPathCount);
                var layoutWidth = TextWrapping == TextWrapping.NoWrap
                    ? float.PositiveInfinity
                    : effectiveAvailableWidth;
                measured = ResolveLayoutUncached(layoutText, layoutWidth).Size;
            }
        }

        measured = ApplyTextBlockExplicitConstraints(measured);
        if (UseLayoutRounding)
        {
            _runtimeTryMeasureDesiredSizeForTextChangeLayoutRoundingCount++;
            IncrementAggregate(ref _diagTryMeasureDesiredSizeForTextChangeLayoutRoundingCount);
            measured = RoundTextBlockSize(measured);
        }

        var margin = Margin;
        desiredSize = new Vector2(
            measured.X + margin.Horizontal,
            measured.Y + margin.Vertical);
        if (UseLayoutRounding)
        {
            desiredSize = RoundTextBlockSize(desiredSize);
        }

        _runtimeTryMeasureDesiredSizeForTextChangeSuccessCount++;
        IncrementAggregate(ref _diagTryMeasureDesiredSizeForTextChangeSuccessCount);
        return true;
    }

    private Vector2 ApplyTextBlockExplicitConstraints(Vector2 measured)
    {
        var width = float.IsNaN(Width) ? measured.X : Width;
        var height = float.IsNaN(Height) ? measured.Y : Height;
        return new Vector2(
            ClampToRange(width, MinWidth, MaxWidth),
            ClampToRange(height, MinHeight, MaxHeight));
    }

    private static Vector2 RoundTextBlockSize(Vector2 size)
    {
        return new Vector2(
            RoundTextBlockScalar(size.X),
            RoundTextBlockScalar(size.Y));
    }

    private static float RoundTextBlockScalar(float value)
    {
        if (!float.IsFinite(value))
        {
            return value;
        }

        return MathF.Round(value);
    }

    private static float ClampToRange(float value, float min, float max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private Vector2 ResolveIntrinsicNoWrapTextSize(string layoutText)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeResolveIntrinsicNoWrapTextSizeCallCount++;
        IncrementAggregate(ref _diagResolveIntrinsicNoWrapTextSizeCallCount);
        var typography = UiTextRenderer.ResolveTypography(this, FontSize);
        try
        {
            if (_hasIntrinsicNoWrapMeasureCache &&
                _intrinsicNoWrapMeasureTextVersion == _textVersion &&
                Nullable.Equals(_intrinsicNoWrapMeasureTypography, typography) &&
                FloatMatches(_intrinsicNoWrapMeasureFontSize, FontSize) &&
                FloatMatches(_intrinsicNoWrapMeasureLineHeight, LineHeight))
            {
                _runtimeIntrinsicMeasureCacheHitCount++;
                IncrementAggregate(ref _diagIntrinsicMeasureCacheHitCount);
                return _intrinsicNoWrapMeasureSize;
            }

            _runtimeIntrinsicMeasureCacheMissCount++;
            IncrementAggregate(ref _diagIntrinsicMeasureCacheMissCount);
            var lineHeight = ResolveLineHeight(typography);

            var size = new Vector2(
                UiTextRenderer.MeasureWidth(typography, layoutText),
                lineHeight);
            _intrinsicNoWrapMeasureTextVersion = _textVersion;
            _intrinsicNoWrapMeasureTypography = typography;
            _intrinsicNoWrapMeasureFontSize = FontSize;
            _intrinsicNoWrapMeasureLineHeight = LineHeight;
            _intrinsicNoWrapMeasureSize = size;
            _hasIntrinsicNoWrapMeasureCache = true;
            return size;
        }
        finally
        {
            var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _runtimeResolveIntrinsicNoWrapTextSizeElapsedTicks += elapsedTicks;
            AddAggregate(ref _diagResolveIntrinsicNoWrapTextSizeElapsedTicks, elapsedTicks);
        }
    }

    private Vector2 ResolveIntrinsicNoWrapTextSizeUncached(string layoutText)
    {
        var typography = UiTextRenderer.ResolveTypography(this, FontSize);
        var lineHeight = ResolveLineHeight(typography);
        return new Vector2(
            UiTextRenderer.MeasureWidth(typography, layoutText),
            lineHeight);
    }

    protected float ResolveMeasureTextLayoutWidth(float availableWidth)
    {
        if (TextWrapping == TextWrapping.NoWrap)
        {
            return float.PositiveInfinity;
        }

        var resolvedWidth = availableWidth;
        if (!float.IsNaN(Width))
        {
            resolvedWidth = Width;
        }

        if (float.IsFinite(MaxWidth))
        {
            resolvedWidth = MathF.Min(resolvedWidth, MaxWidth);
        }

        return resolvedWidth;
    }

    private bool ShouldCollapseWrappedMeasure(float availableWidth)
    {
        return TextWrapping != TextWrapping.NoWrap && availableWidth <= 0.01f;
    }

    private bool CanReuseWrappedLayoutForWidthRange(float previousWidth, float nextWidth)
    {
        if (TextWrapping == TextWrapping.NoWrap ||
            !float.IsFinite(previousWidth) ||
            !float.IsFinite(nextWidth) ||
            ShouldCollapseWrappedMeasure(previousWidth) ||
            ShouldCollapseWrappedMeasure(nextWidth))
        {
            return false;
        }

        var minimumWidth = MathF.Min(previousWidth, nextWidth);
        var maximumWidth = MathF.Max(previousWidth, nextWidth);
        var typography = UiTextRenderer.ResolveTypography(this, FontSize);
        var lineHeight = LineHeight;

        if (_hasLastMeasuredWrappedLayout &&
            _runtimeLastMeasureTextVersion == _textVersion &&
            _runtimeLastMeasureWrapping == TextWrapping &&
            FloatMatches(_runtimeLastMeasureFontSize, FontSize) &&
            FloatMatches(_runtimeLastMeasureLineHeight, lineHeight) &&
            IsWrappedLayoutWidthRangeReusable(_lastMeasuredWrappedLayout, minimumWidth, maximumWidth))
        {
            return true;
        }

        if (_hasLayoutCache &&
            _layoutCacheTextVersion == _textVersion &&
            Nullable.Equals(_layoutCacheTypography, typography) &&
            FloatMatches(_layoutCacheFontSize, FontSize) &&
            _layoutCacheWrapping == TextWrapping &&
            FloatMatches(_layoutCacheLineHeight, lineHeight) &&
            IsWrappedLayoutWidthRangeReusable(_layoutCacheResult, minimumWidth, maximumWidth))
        {
            return true;
        }

        return _hasSecondaryLayoutCache &&
               _secondaryLayoutCacheTextVersion == _textVersion &&
               Nullable.Equals(_secondaryLayoutCacheTypography, typography) &&
               FloatMatches(_secondaryLayoutCacheFontSize, FontSize) &&
               _secondaryLayoutCacheWrapping == TextWrapping &&
               FloatMatches(_secondaryLayoutCacheLineHeight, lineHeight) &&
               IsWrappedLayoutWidthRangeReusable(_secondaryLayoutCacheResult, minimumWidth, maximumWidth);
    }

    private static bool IsWrappedLayoutWidthRangeReusable(TextLayout.TextLayoutResult layout, float minimumWidth, float maximumWidth)
    {
        if (layout.Lines.Count == 0)
        {
            return true;
        }

        if (minimumWidth + 0.5f < layout.ReusableMinimumWidth)
        {
            return false;
        }

        if (!float.IsFinite(layout.ReusableMaximumWidth))
        {
            return true;
        }

        return maximumWidth <= layout.ReusableMaximumWidth - 0.5f;
    }

    private bool TryGetReusableCachedWrappedLayoutForWidth(float width, UiTypography typography, float lineHeight, out TextLayout.TextLayoutResult layout)
    {
        layout = TextLayout.TextLayoutResult.Empty;
        if (TextWrapping == TextWrapping.NoWrap ||
            !float.IsFinite(width) ||
            ShouldCollapseWrappedMeasure(width))
        {
            return false;
        }

        if (_hasLastMeasuredWrappedLayout &&
            _runtimeLastMeasureTextVersion == _textVersion &&
            _runtimeLastMeasureWrapping == TextWrapping &&
            FloatMatches(_runtimeLastMeasureFontSize, FontSize) &&
            FloatMatches(_runtimeLastMeasureLineHeight, lineHeight) &&
            IsWrappedLayoutWidthRangeReusable(_lastMeasuredWrappedLayout, width, width))
        {
            layout = _lastMeasuredWrappedLayout;
            return true;
        }

        if (_hasLayoutCache &&
            _layoutCacheTextVersion == _textVersion &&
            Nullable.Equals(_layoutCacheTypography, typography) &&
            FloatMatches(_layoutCacheFontSize, FontSize) &&
            _layoutCacheWrapping == TextWrapping &&
            FloatMatches(_layoutCacheLineHeight, lineHeight) &&
            IsWrappedLayoutWidthRangeReusable(_layoutCacheResult, width, width))
        {
            layout = _layoutCacheResult;
            return true;
        }

        if (_hasSecondaryLayoutCache &&
            _secondaryLayoutCacheTextVersion == _textVersion &&
            Nullable.Equals(_secondaryLayoutCacheTypography, typography) &&
            FloatMatches(_secondaryLayoutCacheFontSize, FontSize) &&
            _secondaryLayoutCacheWrapping == TextWrapping &&
            FloatMatches(_secondaryLayoutCacheLineHeight, lineHeight) &&
            IsWrappedLayoutWidthRangeReusable(_secondaryLayoutCacheResult, width, width))
        {
            layout = _secondaryLayoutCacheResult;
            return true;
        }

        return false;
    }

    private void StorePrimaryLayoutCache(float width, UiTypography typography, float lineHeight, TextLayout.TextLayoutResult result)
    {
        CapturePrimaryLayoutCacheAsSecondary();
        _layoutCacheTextVersion = _textVersion;
        _layoutCacheWidth = width;
        _layoutCacheTypography = typography;
        _layoutCacheFontSize = FontSize;
        _layoutCacheWrapping = TextWrapping;
        _layoutCacheLineHeight = lineHeight;
        _layoutCacheResult = result;
        _hasLayoutCache = true;
    }

    private TextLayout.TextLayoutResult ResolveLayout(float width)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeResolveLayoutCallCount++;
        IncrementAggregate(ref _diagResolveLayoutCallCount);
        var layoutText = GetLayoutText();
        if (string.IsNullOrEmpty(layoutText))
        {
            _runtimeResolveLayoutEmptyTextCount++;
            IncrementAggregate(ref _diagResolveLayoutEmptyTextCount);
            return TextLayout.TextLayoutResult.Empty;
        }

        try
        {
            var typography = UiTextRenderer.ResolveTypography(this, FontSize);
            var lineHeight = LineHeight;
            var widthMatches = FloatMatches(_layoutCacheWidth, width);
            if (_layoutCacheTextVersion == _textVersion && widthMatches)
            {
                _runtimeResolveLayoutSameTextSameWidthCallCount++;
                IncrementAggregate(ref _diagResolveLayoutSameTextSameWidthCallCount);
            }

            if (_hasLayoutCache &&
                _layoutCacheTextVersion == _textVersion &&
                Nullable.Equals(_layoutCacheTypography, typography) &&
                FloatMatches(_layoutCacheFontSize, FontSize) &&
                _layoutCacheWrapping == TextWrapping &&
                FloatMatches(_layoutCacheLineHeight, lineHeight) &&
                widthMatches)
            {
                _layoutCacheHitCount++;
                _runtimeResolveLayoutCacheHitCount++;
                _runtimeResolveLayoutPrimaryCacheHitCount++;
                IncrementAggregate(ref _diagResolveLayoutCacheHitCount);
                IncrementAggregate(ref _diagResolveLayoutPrimaryCacheHitCount);
                return _layoutCacheResult;
            }

            if (_hasSecondaryLayoutCache &&
                _secondaryLayoutCacheTextVersion == _textVersion &&
                Nullable.Equals(_secondaryLayoutCacheTypography, typography) &&
                FloatMatches(_secondaryLayoutCacheFontSize, FontSize) &&
                _secondaryLayoutCacheWrapping == TextWrapping &&
                FloatMatches(_secondaryLayoutCacheLineHeight, lineHeight) &&
                FloatMatches(_secondaryLayoutCacheWidth, width))
            {
                _layoutCacheHitCount++;
                _runtimeResolveLayoutCacheHitCount++;
                _runtimeResolveLayoutSecondaryCacheHitCount++;
                IncrementAggregate(ref _diagResolveLayoutCacheHitCount);
                IncrementAggregate(ref _diagResolveLayoutSecondaryCacheHitCount);
                PromoteSecondaryLayoutCache();
                return _layoutCacheResult;
            }

            if (TryGetReusableCachedWrappedLayoutForWidth(width, typography, lineHeight, out var reusableLayout))
            {
                _layoutCacheHitCount++;
                _runtimeResolveLayoutCacheHitCount++;
                IncrementAggregate(ref _diagResolveLayoutCacheHitCount);
                StorePrimaryLayoutCache(width, typography, lineHeight, reusableLayout);
                return _layoutCacheResult;
            }

            _layoutCacheMissCount++;
            _runtimeResolveLayoutCacheMissCount++;
            IncrementAggregate(ref _diagResolveLayoutCacheMissCount);
            var result = ApplyLineHeight(TextLayout.Layout(layoutText, typography, FontSize, width, TextWrapping), lineHeight);
            StorePrimaryLayoutCache(width, typography, lineHeight, result);
            return result;
        }
        finally
        {
            var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _runtimeResolveLayoutElapsedTicks += elapsedTicks;
            AddAggregate(ref _diagResolveLayoutElapsedTicks, elapsedTicks);
        }
    }

    private TextLayout.TextLayoutResult ResolveLayoutUncached(string layoutText, float width)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeResolveLayoutUncachedCallCount++;
        IncrementAggregate(ref _diagResolveLayoutUncachedCallCount);
        if (string.IsNullOrEmpty(layoutText))
        {
            return TextLayout.TextLayoutResult.Empty;
        }

        try
        {
            var typography = UiTextRenderer.ResolveTypography(this, FontSize);
            return ApplyLineHeight(TextLayout.Layout(layoutText, typography, FontSize, width, TextWrapping), LineHeight);
        }
        finally
        {
            var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _runtimeResolveLayoutUncachedElapsedTicks += elapsedTicks;
            AddAggregate(ref _diagResolveLayoutUncachedElapsedTicks, elapsedTicks);
        }
    }

    private void InvalidateLayoutCache()
    {
        _runtimeLayoutCacheInvalidationCount++;
        IncrementAggregate(ref _diagLayoutCacheInvalidationCount);
        if (!_hasLayoutCache && !_hasSecondaryLayoutCache)
        {
            _runtimeLayoutCacheInvalidationNoOpCount++;
            IncrementAggregate(ref _diagLayoutCacheInvalidationNoOpCount);
        }
        _hasLayoutCache = false;
        _hasSecondaryLayoutCache = false;
        _hasLastMeasuredWrappedLayout = false;
        _lastMeasuredWrappedLayoutWidth = float.NaN;
        _lastMeasuredWrappedLayout = TextLayout.TextLayoutResult.Empty;
        _layoutCacheTextVersion = -1;
        _layoutCacheWidth = float.NaN;
        _layoutCacheTypography = null;
        _layoutCacheFontSize = float.NaN;
        _layoutCacheLineHeight = float.NaN;
        _layoutCacheResult = TextLayout.TextLayoutResult.Empty;
        _secondaryLayoutCacheTextVersion = -1;
        _secondaryLayoutCacheWidth = float.NaN;
        _secondaryLayoutCacheTypography = null;
        _secondaryLayoutCacheFontSize = float.NaN;
        _secondaryLayoutCacheWrapping = TextWrapping.NoWrap;
        _secondaryLayoutCacheLineHeight = float.NaN;
        _secondaryLayoutCacheResult = TextLayout.TextLayoutResult.Empty;
    }

    private void CapturePrimaryLayoutCacheAsSecondary()
    {
        if (!_hasLayoutCache)
        {
            _hasSecondaryLayoutCache = false;
            _secondaryLayoutCacheTextVersion = -1;
            _secondaryLayoutCacheWidth = float.NaN;
            _secondaryLayoutCacheTypography = null;
            _secondaryLayoutCacheFontSize = float.NaN;
            _secondaryLayoutCacheWrapping = TextWrapping.NoWrap;
            _secondaryLayoutCacheLineHeight = float.NaN;
            _secondaryLayoutCacheResult = TextLayout.TextLayoutResult.Empty;
            return;
        }

        _hasSecondaryLayoutCache = true;
        _secondaryLayoutCacheTextVersion = _layoutCacheTextVersion;
        _secondaryLayoutCacheWidth = _layoutCacheWidth;
        _secondaryLayoutCacheTypography = _layoutCacheTypography;
        _secondaryLayoutCacheFontSize = _layoutCacheFontSize;
        _secondaryLayoutCacheWrapping = _layoutCacheWrapping;
        _secondaryLayoutCacheLineHeight = _layoutCacheLineHeight;
        _secondaryLayoutCacheResult = _layoutCacheResult;
    }

    private void PromoteSecondaryLayoutCache()
    {
        (_layoutCacheTextVersion, _secondaryLayoutCacheTextVersion) = (_secondaryLayoutCacheTextVersion, _layoutCacheTextVersion);
        (_layoutCacheWidth, _secondaryLayoutCacheWidth) = (_secondaryLayoutCacheWidth, _layoutCacheWidth);
        (_layoutCacheTypography, _secondaryLayoutCacheTypography) = (_secondaryLayoutCacheTypography, _layoutCacheTypography);
        (_layoutCacheFontSize, _secondaryLayoutCacheFontSize) = (_secondaryLayoutCacheFontSize, _layoutCacheFontSize);
        (_layoutCacheWrapping, _secondaryLayoutCacheWrapping) = (_secondaryLayoutCacheWrapping, _layoutCacheWrapping);
        (_layoutCacheLineHeight, _secondaryLayoutCacheLineHeight) = (_secondaryLayoutCacheLineHeight, _layoutCacheLineHeight);
        (_layoutCacheResult, _secondaryLayoutCacheResult) = (_secondaryLayoutCacheResult, _layoutCacheResult);
        (_hasLayoutCache, _hasSecondaryLayoutCache) = (_hasSecondaryLayoutCache, _hasLayoutCache);
    }

    private void InvalidateIntrinsicNoWrapMeasureCache()
    {
        _runtimeIntrinsicMeasureInvalidationCount++;
        IncrementAggregate(ref _diagIntrinsicMeasureInvalidationCount);
        if (!_hasIntrinsicNoWrapMeasureCache)
        {
            _runtimeIntrinsicMeasureInvalidationNoOpCount++;
            IncrementAggregate(ref _diagIntrinsicMeasureInvalidationNoOpCount);
        }
        _hasIntrinsicNoWrapMeasureCache = false;
        _intrinsicNoWrapMeasureTextVersion = -1;
        _intrinsicNoWrapMeasureTypography = null;
        _intrinsicNoWrapMeasureFontSize = float.NaN;
        _intrinsicNoWrapMeasureLineHeight = float.NaN;
        _intrinsicNoWrapMeasureSize = Vector2.Zero;
    }

    private float ResolveLineHeight(UiTypography typography)
    {
        return float.IsNaN(LineHeight)
            ? UiTextRenderer.GetLineHeight(typography)
            : LineHeight;
    }

    private static TextLayout.TextLayoutResult ApplyLineHeight(TextLayout.TextLayoutResult result, float lineHeight)
    {
        if (float.IsNaN(lineHeight) || result.Lines.Count == 0)
        {
            return result;
        }

        return new TextLayout.TextLayoutResult(
            result.Lines,
            result.LineWidths,
            new Vector2(result.Size.X, result.Lines.Count * lineHeight),
            result.ReusableMinimumWidth,
            result.ReusableMaximumWidth);
    }

    private static bool FloatMatches(float cached, float current)
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

        return System.MathF.Abs(cached - current) < 0.01f;
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }

    private static double AverageTicksToMilliseconds(long totalTicks, int sampleCount)
    {
        if (sampleCount <= 0)
        {
            return 0d;
        }

        return ((double)totalTicks * 1000d / Stopwatch.Frequency) / sampleCount;
    }

    private static TextBlockTelemetrySnapshot CreateAggregateTelemetrySnapshot(bool reset)
    {
        return new TextBlockTelemetrySnapshot(
            ReadOrReset(ref _diagMeasureOverrideCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagMeasureOverrideElapsedTicks, reset)),
            ReadOrReset(ref _diagEmptyMeasureCallCount, reset),
            ReadOrReset(ref _diagSameTextSameWidthMeasureCallCount, reset),
            ReadOrReset(ref _diagIntrinsicMeasurePathCallCount, reset),
            ReadOrReset(ref _diagLayoutMeasurePathCallCount, reset),
            ReadOrReset(ref _diagCanReuseMeasureCallCount, reset),
            ReadOrReset(ref _diagCanReuseMeasureTrueCount, reset),
            ReadOrReset(ref _diagCanReuseMeasureEmptyTextCount, reset),
            ReadOrReset(ref _diagCanReuseMeasureNoWrapCount, reset),
            ReadOrReset(ref _diagCanReuseMeasureMultilineRejectCount, reset),
            ReadOrReset(ref _diagCanReuseMeasureIntrinsicFitCount, reset),
            ReadOrReset(ref _diagCanReuseMeasureTooNarrowRejectCount, reset),
            ReadOrReset(ref _diagShouldInvalidateMeasureCallCount, reset),
            ReadOrReset(ref _diagShouldInvalidateMeasureTextPropertyCallCount, reset),
            ReadOrReset(ref _diagShouldInvalidateMeasureBaseFallbackCount, reset),
            ReadOrReset(ref _diagShouldInvalidateMeasureReusedDesiredSizeCount, reset),
            ReadOrReset(ref _diagShouldInvalidateMeasureChangedDesiredSizeCount, reset),
            ReadOrReset(ref _diagTryMeasureDesiredSizeForTextChangeCallCount, reset),
            ReadOrReset(ref _diagTryMeasureDesiredSizeForTextChangeSuccessCount, reset),
            ReadOrReset(ref _diagTryMeasureDesiredSizeForTextChangeNoPreviousAvailableCount, reset),
            ReadOrReset(ref _diagTryMeasureDesiredSizeForTextChangeEmptyTextCount, reset),
            ReadOrReset(ref _diagTryMeasureDesiredSizeForTextChangeIntrinsicPathCount, reset),
            ReadOrReset(ref _diagTryMeasureDesiredSizeForTextChangeLayoutPathCount, reset),
            ReadOrReset(ref _diagTryMeasureDesiredSizeForTextChangeLayoutRoundingCount, reset),
            ReadOrReset(ref _diagHasAvailableIndependentDesiredSizeForUniformGridCallCount, reset),
            ReadOrReset(ref _diagHasAvailableIndependentDesiredSizeForUniformGridTrueCount, reset),
            ReadOrReset(ref _diagHasAvailableIndependentDesiredSizeForUniformGridFalseCount, reset),
            ReadOrReset(ref _diagCanUseIntrinsicNoWrapMeasureCallCount, reset),
            ReadOrReset(ref _diagCanUseIntrinsicNoWrapMeasureAllowedCount, reset),
            ReadOrReset(ref _diagCanUseIntrinsicNoWrapMeasureRejectedWrapCount, reset),
            ReadOrReset(ref _diagCanUseIntrinsicNoWrapMeasureRejectedEmptyCount, reset),
            ReadOrReset(ref _diagCanUseIntrinsicNoWrapMeasureRejectedMultilineCount, reset),
            ReadOrReset(ref _diagCanUseIntrinsicMeasureCallCount, reset),
            ReadOrReset(ref _diagCanUseIntrinsicMeasureAllowedNoWrapCount, reset),
            ReadOrReset(ref _diagCanUseIntrinsicMeasureAllowedWrappedWidthCount, reset),
            ReadOrReset(ref _diagCanUseIntrinsicMeasureRejectedNoWrapUnavailableCount, reset),
            ReadOrReset(ref _diagCanUseIntrinsicMeasureRejectedEmptyTextCount, reset),
            ReadOrReset(ref _diagCanUseIntrinsicMeasureRejectedMultilineTextCount, reset),
            ReadOrReset(ref _diagCanUseIntrinsicMeasureRejectedWidthTooNarrowCount, reset),
            ReadOrReset(ref _diagResolveIntrinsicNoWrapTextSizeCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagResolveIntrinsicNoWrapTextSizeElapsedTicks, reset)),
            ReadOrReset(ref _diagIntrinsicMeasureCacheHitCount, reset),
            ReadOrReset(ref _diagIntrinsicMeasureCacheMissCount, reset),
            ReadOrReset(ref _diagResolveLayoutCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagResolveLayoutElapsedTicks, reset)),
            ReadOrReset(ref _diagResolveLayoutEmptyTextCount, reset),
            ReadOrReset(ref _diagResolveLayoutSameTextSameWidthCallCount, reset),
            ReadOrReset(ref _diagResolveLayoutCacheHitCount, reset),
            ReadOrReset(ref _diagResolveLayoutPrimaryCacheHitCount, reset),
            ReadOrReset(ref _diagResolveLayoutSecondaryCacheHitCount, reset),
            ReadOrReset(ref _diagResolveLayoutCacheMissCount, reset),
            ReadOrReset(ref _diagResolveLayoutUncachedCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagResolveLayoutUncachedElapsedTicks, reset)),
            ReadOrReset(ref _diagTextPropertyChangeCount, reset),
            ReadOrReset(ref _diagLayoutAffectingPropertyChangeCount, reset),
            ReadOrReset(ref _diagOtherPropertyChangeCount, reset),
            ReadOrReset(ref _diagLayoutCacheInvalidationCount, reset),
            ReadOrReset(ref _diagLayoutCacheInvalidationNoOpCount, reset),
            ReadOrReset(ref _diagIntrinsicMeasureInvalidationCount, reset),
            ReadOrReset(ref _diagIntrinsicMeasureInvalidationNoOpCount, reset),
            ReadOrReset(ref _diagRenderCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagRenderElapsedTicks, reset)),
            ReadOrReset(ref _diagRenderEmptyTextSkipCount, reset),
            ReadOrReset(ref _diagRenderLineIterationCount, reset),
            ReadOrReset(ref _diagRenderEmptyLineSkipCount, reset),
            ReadOrReset(ref _diagRenderAxisAlignedClipFastPathCount, reset),
            ReadOrReset(ref _diagRenderTransformedClipPathCount, reset),
            ReadOrReset(ref _diagRenderClipSkipCount, reset),
            ReadOrReset(ref _diagRenderClipBreakCount, reset),
            ReadOrReset(ref _diagRenderDrawLineCount, reset),
            ReadOrReset(ref _diagRenderTextDecorationsCallCount, reset));
    }

    private static void IncrementAggregate(ref long counter)
    {
        Interlocked.Increment(ref counter);
    }

    private static void AddAggregate(ref long counter, long value)
    {
        Interlocked.Add(ref counter, value);
    }

    private static long ReadAggregate(ref long counter)
    {
        return Interlocked.Read(ref counter);
    }

    private static long ResetAggregate(ref long counter)
    {
        return Interlocked.Exchange(ref counter, 0L);
    }

    private static long ReadOrReset(ref long counter, bool reset)
    {
        return reset ? ResetAggregate(ref counter) : ReadAggregate(ref counter);
    }
}

