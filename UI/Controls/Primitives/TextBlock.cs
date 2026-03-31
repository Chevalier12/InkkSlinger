using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class TextBlock : FrameworkElement
{
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
    private int _runtimeMeasureOverrideCallCount;
    private long _runtimeMeasureOverrideElapsedTicks;
    private int _runtimeEmptyMeasureCallCount;
    private int _runtimeSameTextSameWidthMeasureCallCount;
    private int _runtimeIntrinsicMeasurePathCallCount;
    private int _runtimeIntrinsicMeasureCacheHitCount;
    private int _runtimeIntrinsicMeasureCacheMissCount;
    private int _runtimeResolveLayoutCallCount;
    private int _runtimeResolveLayoutCacheHitCount;
    private int _runtimeResolveLayoutCacheMissCount;
    private int _runtimeResolveLayoutSameTextSameWidthCallCount;
    private int _runtimeTextPropertyChangeCount;
    private int _runtimeLayoutAffectingPropertyChangeCount;
    private int _runtimeLayoutCacheInvalidationCount;
    private int _runtimeIntrinsicMeasureInvalidationCount;
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
        return TextWrapping == TextWrapping.NoWrap;
    }

    internal int LastRenderedLineCountForTests => _lastRenderedLineCount;

    internal float LastRenderedLayoutWidthForTests => _lastRenderedLayoutWidth;

    internal string LastRenderedLayoutTextForTests => _lastRenderedLayoutText;

    internal TextBlockRuntimeDiagnosticsSnapshot GetRuntimeDiagnosticsForTests()
    {
        return new TextBlockRuntimeDiagnosticsSnapshot(
            _runtimeMeasureOverrideCallCount,
            TicksToMilliseconds(_runtimeMeasureOverrideElapsedTicks),
            _runtimeEmptyMeasureCallCount,
            _runtimeSameTextSameWidthMeasureCallCount,
            _runtimeIntrinsicMeasurePathCallCount,
            _runtimeIntrinsicMeasureCacheHitCount,
            _runtimeIntrinsicMeasureCacheMissCount,
            _runtimeResolveLayoutCallCount,
            _runtimeResolveLayoutCacheHitCount,
            _runtimeResolveLayoutCacheMissCount,
            _runtimeResolveLayoutSameTextSameWidthCallCount,
            _runtimeTextPropertyChangeCount,
            _runtimeLayoutAffectingPropertyChangeCount,
            _runtimeLayoutCacheInvalidationCount,
            _runtimeIntrinsicMeasureInvalidationCount);
    }

    protected virtual string GetLayoutText()
    {
        return Text;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var startTicks = Stopwatch.GetTimestamp();
        var layoutText = GetLayoutText();
        if (string.IsNullOrEmpty(layoutText))
        {
            _runtimeMeasureOverrideCallCount++;
            _runtimeEmptyMeasureCallCount++;
            _runtimeMeasureOverrideElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            return Vector2.Zero;
        }

        var effectiveAvailableWidth = ResolveMeasureTextLayoutWidth(availableSize.X);
        var sameTextSameWidth = _runtimeLastMeasureTextVersion == _textVersion &&
                                FloatMatches(_runtimeLastMeasureWidth, effectiveAvailableWidth) &&
                                _runtimeLastMeasureWrapping == TextWrapping &&
                                FloatMatches(_runtimeLastMeasureFontSize, FontSize) &&
                                FloatMatches(_runtimeLastMeasureLineHeight, LineHeight);
        if (sameTextSameWidth)
        {
            _runtimeSameTextSameWidthMeasureCallCount++;
        }

        _runtimeLastMeasureTextVersion = _textVersion;
        _runtimeLastMeasureWidth = effectiveAvailableWidth;
        _runtimeLastMeasureWrapping = TextWrapping;
        _runtimeLastMeasureFontSize = FontSize;
        _runtimeLastMeasureLineHeight = LineHeight;

        Vector2 result;
        if (CanUseIntrinsicMeasure(layoutText, effectiveAvailableWidth))
        {
            _runtimeIntrinsicMeasurePathCallCount++;
            result = ResolveIntrinsicNoWrapTextSize(layoutText);
        }
        else
        {
            var availableWidth = TextWrapping == TextWrapping.NoWrap
                ? float.PositiveInfinity
                : effectiveAvailableWidth;

            result = ResolveLayout(availableWidth).Size;
        }

        _runtimeMeasureOverrideCallCount++;
        _runtimeMeasureOverrideElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        return result;
    }

    protected override bool CanReuseMeasureForAvailableSizeChange(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
    {
        var layoutText = GetLayoutText();
        if (string.IsNullOrEmpty(layoutText))
        {
            return true;
        }

        if (TextWrapping == TextWrapping.NoWrap)
        {
            return true;
        }

        if (layoutText.IndexOfAny(['\r', '\n']) >= 0)
        {
            return false;
        }

        var intrinsicSize = ResolveIntrinsicNoWrapTextSize(layoutText);
        return previousAvailableSize.X >= intrinsicSize.X &&
               nextAvailableSize.X >= intrinsicSize.X;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        base.OnRender(spriteBatch);

        var layoutText = GetLayoutText();
        if (string.IsNullOrEmpty(layoutText))
        {
            return;
        }

        var renderStart = Stopwatch.GetTimestamp();
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
        var transformedBaseY = useAxisAlignedClipFastPath
            ? (LayoutSlot.Y * scaleY) + offsetY
            : 0f;
        var transformedLineSpacing = useAxisAlignedClipFastPath
            ? lineSpacing * scaleY
            : 0f;
        for (var i = 0; i < layout.Lines.Count; i++)
        {
            var line = layout.Lines[i];
            if (line.Length == 0)
            {
                continue;
            }

            var position = new Vector2(LayoutSlot.X, LayoutSlot.Y + (i * lineSpacing));
            if (useAxisAlignedClipFastPath)
            {
                var transformedLineTop = transformedBaseY + (i * transformedLineSpacing);
                var transformedLineBottom = transformedLineTop + transformedLineSpacing;
                if (transformedLineBottom < currentClip.Y)
                {
                    continue;
                }

                if (transformedLineTop > currentClip.Bottom)
                {
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
                    continue;
                }

                if (transformedBounds.Y > currentClip.Bottom)
                {
                    break;
                }
            }

            UiTextRenderer.DrawString(spriteBatch, typography, line, position, drawColor);
        }

        OnRenderTextDecorations(spriteBatch, layout, lineSpacing);

        var renderTicks = Stopwatch.GetTimestamp() - renderStart;
        _renderSampleCount++;
        _renderTotalTicks += renderTicks;
        _renderMaxTicks = System.Math.Max(_renderMaxTicks, renderTicks);
        _renderLastTicks = renderTicks;
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
            InvalidateLayoutCache();
            InvalidateIntrinsicNoWrapMeasureCache();
        }
    }

    protected override bool ShouldInvalidateMeasureForPropertyChange(
        DependencyPropertyChangedEventArgs args,
        FrameworkPropertyMetadata metadata)
    {
        if (!ReferenceEquals(args.Property, TextProperty))
        {
            return base.ShouldInvalidateMeasureForPropertyChange(args, metadata);
        }

        if (!IsMeasureValidForTests ||
            NeedsMeasure ||
            !TryMeasureDesiredSizeForTextChange(GetLayoutText(), out var nextDesiredSize))
        {
            return base.ShouldInvalidateMeasureForPropertyChange(args, metadata);
        }

        return !FloatMatches(nextDesiredSize.X, DesiredSize.X) ||
               !FloatMatches(nextDesiredSize.Y, DesiredSize.Y);
    }

    private bool CanUseIntrinsicNoWrapTextMeasure(string layoutText)
    {
        return TextWrapping == TextWrapping.NoWrap &&
               !string.IsNullOrEmpty(layoutText) &&
               layoutText.IndexOfAny(['\r', '\n']) < 0;
    }

    private bool CanUseIntrinsicMeasure(string layoutText, float availableWidth)
    {
        if (!CanUseIntrinsicNoWrapTextMeasure(layoutText))
        {
            if (TextWrapping == TextWrapping.NoWrap ||
                string.IsNullOrEmpty(layoutText) ||
                layoutText.IndexOfAny(['\r', '\n']) >= 0)
            {
                return false;
            }

            return availableWidth >= ResolveIntrinsicNoWrapTextSize(layoutText).X;
        }

        return true;
    }

    private bool TryMeasureDesiredSizeForTextChange(string layoutText, out Vector2 desiredSize)
    {
        desiredSize = DesiredSize;
        var availableWidth = PreviousAvailableSizeForTests.X;
        var availableHeight = PreviousAvailableSizeForTests.Y;
        if (float.IsNaN(availableWidth) || float.IsNaN(availableHeight))
        {
            return false;
        }

        Vector2 measured;
        if (string.IsNullOrEmpty(layoutText))
        {
            measured = Vector2.Zero;
        }
        else
        {
            var effectiveAvailableWidth = ResolveMeasureTextLayoutWidth(availableWidth);
            if (CanUseIntrinsicMeasure(layoutText, effectiveAvailableWidth))
            {
                measured = ResolveIntrinsicNoWrapTextSizeUncached(layoutText);
            }
            else
            {
                var layoutWidth = TextWrapping == TextWrapping.NoWrap
                    ? float.PositiveInfinity
                    : effectiveAvailableWidth;
                measured = ResolveLayoutUncached(layoutText, layoutWidth).Size;
            }
        }

        measured = ApplyTextBlockExplicitConstraints(measured);
        if (UseLayoutRounding)
        {
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
        var typography = UiTextRenderer.ResolveTypography(this, FontSize);
        if (_hasIntrinsicNoWrapMeasureCache &&
            _intrinsicNoWrapMeasureTextVersion == _textVersion &&
            Nullable.Equals(_intrinsicNoWrapMeasureTypography, typography) &&
            FloatMatches(_intrinsicNoWrapMeasureFontSize, FontSize) &&
            FloatMatches(_intrinsicNoWrapMeasureLineHeight, LineHeight))
        {
            _runtimeIntrinsicMeasureCacheHitCount++;
            return _intrinsicNoWrapMeasureSize;
        }

        _runtimeIntrinsicMeasureCacheMissCount++;
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

    private TextLayout.TextLayoutResult ResolveLayout(float width)
    {
        _runtimeResolveLayoutCallCount++;
        var layoutText = GetLayoutText();
        if (string.IsNullOrEmpty(layoutText))
        {
            return TextLayout.TextLayoutResult.Empty;
        }

        var typography = UiTextRenderer.ResolveTypography(this, FontSize);
        var lineHeight = LineHeight;
        var widthMatches = FloatMatches(_layoutCacheWidth, width);
        if (_layoutCacheTextVersion == _textVersion && widthMatches)
        {
            _runtimeResolveLayoutSameTextSameWidthCallCount++;
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
            PromoteSecondaryLayoutCache();
            return _layoutCacheResult;
        }

        _layoutCacheMissCount++;
        _runtimeResolveLayoutCacheMissCount++;
        var result = ApplyLineHeight(TextLayout.Layout(layoutText, typography, FontSize, width, TextWrapping), lineHeight);
        CapturePrimaryLayoutCacheAsSecondary();
        _layoutCacheTextVersion = _textVersion;
        _layoutCacheWidth = width;
        _layoutCacheTypography = typography;
        _layoutCacheFontSize = FontSize;
        _layoutCacheWrapping = TextWrapping;
        _layoutCacheLineHeight = lineHeight;
        _layoutCacheResult = result;
        _hasLayoutCache = true;
        return result;
    }

    private TextLayout.TextLayoutResult ResolveLayoutUncached(string layoutText, float width)
    {
        if (string.IsNullOrEmpty(layoutText))
        {
            return TextLayout.TextLayoutResult.Empty;
        }

        var typography = UiTextRenderer.ResolveTypography(this, FontSize);
        return ApplyLineHeight(TextLayout.Layout(layoutText, typography, FontSize, width, TextWrapping), LineHeight);
    }

    private void InvalidateLayoutCache()
    {
        _runtimeLayoutCacheInvalidationCount++;
        _hasLayoutCache = false;
        _hasSecondaryLayoutCache = false;
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
            new Vector2(result.Size.X, result.Lines.Count * lineHeight));
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
}

public readonly record struct TextBlockPerformanceSnapshot(
    int LayoutCacheHitCount,
    int LayoutCacheMissCount,
    int RenderSampleCount,
    double LastRenderMilliseconds,
    double AverageRenderMilliseconds,
    double MaxRenderMilliseconds);

internal readonly record struct TextBlockRuntimeDiagnosticsSnapshot(
    int MeasureOverrideCallCount,
    double MeasureOverrideMilliseconds,
    int EmptyMeasureCallCount,
    int SameTextSameWidthMeasureCallCount,
    int IntrinsicMeasurePathCallCount,
    int IntrinsicMeasureCacheHitCount,
    int IntrinsicMeasureCacheMissCount,
    int ResolveLayoutCallCount,
    int ResolveLayoutCacheHitCount,
    int ResolveLayoutCacheMissCount,
    int ResolveLayoutSameTextSameWidthCallCount,
    int TextPropertyChangeCount,
    int LayoutAffectingPropertyChangeCount,
    int LayoutCacheInvalidationCount,
    int IntrinsicMeasureInvalidationCount);
