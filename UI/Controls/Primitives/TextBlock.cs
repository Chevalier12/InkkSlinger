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

    private bool _hasLayoutCache;
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

    protected virtual string GetLayoutText()
    {
        return Text;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var layoutText = GetLayoutText();
        if (string.IsNullOrEmpty(layoutText))
        {
            return Vector2.Zero;
        }

        var effectiveAvailableWidth = ResolveMeasureTextLayoutWidth(availableSize.X);

        if (CanUseIntrinsicMeasure(layoutText, effectiveAvailableWidth))
        {
            return ResolveIntrinsicNoWrapTextSize(layoutText);
        }

        var availableWidth = TextWrapping == TextWrapping.NoWrap
            ? float.PositiveInfinity
            : effectiveAvailableWidth;

        return ResolveLayout(availableWidth).Size;
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
        var lineSpacing = UiTextRenderer.GetLineHeight(typography);
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
            InvalidateLayoutCache();
            InvalidateIntrinsicNoWrapMeasureCache();
            return;
        }

        if (ReferenceEquals(args.Property, TextWrappingProperty) ||
            ReferenceEquals(args.Property, FontSizeProperty) ||
            ReferenceEquals(args.Property, FontFamilyProperty) ||
            ReferenceEquals(args.Property, FontWeightProperty) ||
            ReferenceEquals(args.Property, FontStyleProperty))
        {
            InvalidateLayoutCache();
            InvalidateIntrinsicNoWrapMeasureCache();
        }
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

    private Vector2 ResolveIntrinsicNoWrapTextSize(string layoutText)
    {
        var typography = UiTextRenderer.ResolveTypography(this, FontSize);
        if (_hasIntrinsicNoWrapMeasureCache &&
            _intrinsicNoWrapMeasureTextVersion == _textVersion &&
            Nullable.Equals(_intrinsicNoWrapMeasureTypography, typography) &&
            WidthMatches(_intrinsicNoWrapMeasureFontSize, FontSize))
        {
            return _intrinsicNoWrapMeasureSize;
        }

        var size = new Vector2(
            UiTextRenderer.MeasureWidth(typography, layoutText),
            UiTextRenderer.GetLineHeight(typography));
        _intrinsicNoWrapMeasureTextVersion = _textVersion;
        _intrinsicNoWrapMeasureTypography = typography;
        _intrinsicNoWrapMeasureFontSize = FontSize;
        _intrinsicNoWrapMeasureSize = size;
        _hasIntrinsicNoWrapMeasureCache = true;
        return size;
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
        var layoutText = GetLayoutText();
        if (string.IsNullOrEmpty(layoutText))
        {
            return TextLayout.TextLayoutResult.Empty;
        }

        var typography = UiTextRenderer.ResolveTypography(this, FontSize);
        var widthMatches = WidthMatches(_layoutCacheWidth, width);
        if (_hasLayoutCache &&
            _layoutCacheTextVersion == _textVersion &&
            Nullable.Equals(_layoutCacheTypography, typography) &&
            WidthMatches(_layoutCacheFontSize, FontSize) &&
            _layoutCacheWrapping == TextWrapping &&
            widthMatches)
        {
            _layoutCacheHitCount++;
            return _layoutCacheResult;
        }

        _layoutCacheMissCount++;
        var result = TextLayout.Layout(layoutText, typography, FontSize, width, TextWrapping);
        _layoutCacheTextVersion = _textVersion;
        _layoutCacheWidth = width;
        _layoutCacheTypography = typography;
        _layoutCacheFontSize = FontSize;
        _layoutCacheWrapping = TextWrapping;
        _layoutCacheResult = result;
        _hasLayoutCache = true;
        return result;
    }

    private void InvalidateLayoutCache()
    {
        _hasLayoutCache = false;
        _layoutCacheTextVersion = -1;
        _layoutCacheWidth = float.NaN;
        _layoutCacheTypography = null;
        _layoutCacheFontSize = float.NaN;
        _layoutCacheResult = TextLayout.TextLayoutResult.Empty;
    }

    private void InvalidateIntrinsicNoWrapMeasureCache()
    {
        _hasIntrinsicNoWrapMeasureCache = false;
        _intrinsicNoWrapMeasureTextVersion = -1;
        _intrinsicNoWrapMeasureTypography = null;
        _intrinsicNoWrapMeasureFontSize = float.NaN;
        _intrinsicNoWrapMeasureSize = Vector2.Zero;
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
