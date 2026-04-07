using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public static class TextLayout
{
    private const int CacheCapacity = 512;
    private const int ParagraphAnalysisCacheCapacity = 256;
    private const float MinimumWrappedWidth = 0.01f;
    private static readonly Dictionary<TextLayoutCacheKey, TextLayoutResult> Cache = new();
    private static readonly Queue<TextLayoutCacheKey> CacheOrder = new();
    private static readonly Dictionary<TextParagraphAnalysisCacheKey, TextParagraphLayoutAnalysis> ParagraphAnalysisCache = new();
    private static readonly Queue<TextParagraphAnalysisCacheKey> ParagraphAnalysisCacheOrder = new();
    private static int _layoutRequestCount;
    private static int _cacheHitCount;
    private static int _cacheMissCount;
    private static int _buildCount;
    private static int _noWrapBuildCount;
    private static int _wrappedBuildCount;
    private static int _totalMeasuredTextLength;
    private static int _totalProducedLineCount;
    private static long _layoutElapsedTicks;
    private static long _buildElapsedTicks;

    public static TextLayoutMetricsSnapshot GetMetricsSnapshot()
    {
        return new TextLayoutMetricsSnapshot(
            _layoutRequestCount,
            _cacheHitCount,
            _cacheMissCount,
            _buildCount,
            _noWrapBuildCount,
            _wrappedBuildCount,
            _totalMeasuredTextLength,
            _totalProducedLineCount,
            Cache.Count,
            _layoutElapsedTicks,
            _buildElapsedTicks);
    }

    public static void ResetMetricsForTests()
    {
        _layoutRequestCount = 0;
        _cacheHitCount = 0;
        _cacheMissCount = 0;
        _buildCount = 0;
        _noWrapBuildCount = 0;
        _wrappedBuildCount = 0;
        _totalMeasuredTextLength = 0;
        _totalProducedLineCount = 0;
        _layoutElapsedTicks = 0;
        _buildElapsedTicks = 0;
        Cache.Clear();
        CacheOrder.Clear();
        ParagraphAnalysisCache.Clear();
        ParagraphAnalysisCacheOrder.Clear();
    }

    internal static TextLayoutResult Layout(string? text, UiTypography typography, float availableWidth, TextWrapping wrapping)
    {
        return Layout(text, typography, typography.Size, availableWidth, wrapping);
    }

    internal static TextLayoutResult Layout(string? text, UiTypography typography, float fontSize, float availableWidth, TextWrapping wrapping)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            _layoutRequestCount++;
            if (string.IsNullOrEmpty(text))
            {
                return TextLayoutResult.Empty;
            }

            _totalMeasuredTextLength += text.Length;
            var key = TextLayoutCacheKey.Create(text, typography, fontSize, availableWidth, wrapping);
            if (Cache.TryGetValue(key, out var cached))
            {
                _cacheHitCount++;
                return cached;
            }

            _cacheMissCount++;
            var result = BuildLayout(text, typography, fontSize, availableWidth, wrapping);
            _totalProducedLineCount += result.Lines.Count;
            AddToCache(key, result);
            return result;
        }
        finally
        {
            _layoutElapsedTicks += Stopwatch.GetTimestamp() - start;
        }
    }

    public static TextLayoutResult LayoutForElement(string? text, FrameworkElement element, float availableWidth, TextWrapping wrapping)
    {
        return Layout(text, UiTextRenderer.ResolveTypography(element), availableWidth, wrapping);
    }

    internal static TextLayoutResult LayoutForElement(string? text, FrameworkElement element, float fontSize, float availableWidth, TextWrapping wrapping)
    {
        return Layout(text, UiTextRenderer.ResolveTypography(element, fontSize), fontSize, availableWidth, wrapping);
    }

    private static TextLayoutResult BuildLayout(string text, UiTypography typography, float fontSize, float availableWidth, TextWrapping wrapping)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            _buildCount++;
            if (wrapping == TextWrapping.NoWrap ||
                float.IsInfinity(availableWidth) ||
                float.IsNaN(availableWidth))
            {
                _noWrapBuildCount++;
                return BuildNoWrapLayout(text, typography, fontSize);
            }

            _wrappedBuildCount++;
            return BuildWrappedLayout(text, typography, fontSize, MathF.Max(availableWidth, MinimumWrappedWidth));
        }
        finally
        {
            _buildElapsedTicks += Stopwatch.GetTimestamp() - start;
        }
    }

    private static TextLayoutResult BuildNoWrapLayout(string text, UiTypography typography, float fontSize)
    {
        var lines = new List<string>();
        var widths = new List<float>();
        var index = 0;
        while (TryReadLogicalLine(text, ref index, out var start, out var length))
        {
            var line = length == 0 ? string.Empty : text.Substring(start, length);
            lines.Add(line);
            widths.Add(line.Length == 0 ? 0f : UiTextRenderer.MeasureWidth(typography with { Size = fontSize }, line));
        }

        return BuildResult(lines, widths, typography, fontSize, 0f, float.PositiveInfinity);
    }

    private static TextLayoutResult BuildWrappedLayout(string text, UiTypography typography, float fontSize, float availableWidth)
    {
        var lines = new List<string>();
        var widths = new List<float>();
        var reusableMinimumWidth = 0f;
        var reusableMaximumWidth = float.PositiveInfinity;
        var analysis = ResolveParagraphLayoutAnalysis(text, typography, fontSize);
        foreach (var paragraph in analysis.Paragraphs)
        {
            LayoutParagraph(
                text,
                paragraph,
                typography,
                fontSize,
                availableWidth,
                lines,
                widths,
                ref reusableMinimumWidth,
                ref reusableMaximumWidth);
        }

        return BuildResult(lines, widths, typography, fontSize, reusableMinimumWidth, reusableMaximumWidth);
    }

    private static void LayoutParagraph(
        string text,
        ParagraphAnalysis paragraph,
        UiTypography typography,
        float fontSize,
        float availableWidth,
        IList<string> lines,
        IList<float> widths,
        ref float reusableMinimumWidth,
        ref float reusableMaximumWidth)
    {
        if (paragraph.Length == 0)
        {
            lines.Add(string.Empty);
            widths.Add(0f);
            return;
        }

        var initialLineCount = lines.Count;
        var lineWidth = 0f;
        var trimmedLineWidth = 0f;
        var lineStart = 0;
        var lineLength = 0;
        for (var tokenIndex = 0; tokenIndex < paragraph.Tokens.Length;)
        {
            var token = paragraph.Tokens[tokenIndex];
            if (token.IsWhitespace && lineLength == 0)
            {
                tokenIndex++;
                continue;
            }

            if ((lineWidth + token.Width) <= availableWidth)
            {
                AppendTokenToLine(token, ref lineStart, ref lineLength);
                lineWidth += token.Width;
                if (!token.IsWhitespace)
                {
                    trimmedLineWidth = lineWidth;
                }

                tokenIndex++;
                continue;
            }

            if (lineLength > 0)
            {
                FinalizeWrappedLine(
                    text,
                    lineStart,
                    lineLength,
                    lineWidth,
                    trimmedLineWidth,
                    lineWidth + token.Width,
                    lines,
                    widths,
                    ref reusableMinimumWidth,
                    ref reusableMaximumWidth);
                lineWidth = 0f;
                trimmedLineWidth = 0f;
                lineLength = 0;
                continue;
            }

            BreakLongToken(
                text,
                token.Start,
                token.Length,
                typography,
                fontSize,
                availableWidth,
                lines,
                widths,
                ref reusableMinimumWidth,
                ref reusableMaximumWidth);

            tokenIndex++;
        }

        if (lineLength > 0 || lines.Count == initialLineCount)
        {
            FinalizeWrappedLine(
                text,
                lineStart,
                lineLength,
                lineWidth,
                trimmedLineWidth,
                float.PositiveInfinity,
                lines,
                widths,
                ref reusableMinimumWidth,
                ref reusableMaximumWidth);
        }
    }

    private static void AppendTokenToLine(TokenAnalysis token, ref int lineStart, ref int lineLength)
    {
        if (lineLength == 0)
        {
            lineStart = token.Start;
            lineLength = token.Length;
            return;
        }

        lineLength = (token.Start + token.Length) - lineStart;
    }

    private static void FinalizeWrappedLine(
        string text,
        int lineStart,
        int lineLength,
        float lineWidth,
        float trimmedLineWidth,
        float stableUpperWidth,
        IList<string> lines,
        IList<float> widths,
        ref float reusableMinimumWidth,
        ref float reusableMaximumWidth)
    {
        var line = lineLength == 0
            ? string.Empty
            : text.Substring(lineStart, lineLength);
        lines.Add(line);
        widths.Add(lineWidth);
        UpdateReusableWidthBounds(
            trimmedLineWidth,
            stableUpperWidth,
            ref reusableMinimumWidth,
            ref reusableMaximumWidth);
    }

    private static void BreakLongToken(
        string text,
        int start,
        int length,
        UiTypography typography,
        float fontSize,
        float availableWidth,
        IList<string> lines,
        IList<float> widths,
        ref float reusableMinimumWidth,
        ref float reusableMaximumWidth)
    {
        var remainingStart = start;
        var remainingLength = length;
        while (remainingLength > 0)
        {
            var fitLength = FindLongestFittingSegmentLength(text, remainingStart, remainingLength, typography, fontSize, availableWidth);
            if (fitLength <= 0)
            {
                fitLength = 1;
            }

            var line = text.Substring(remainingStart, fitLength);
            var lineWidth = line.Length == 0 ? 0f : UiTextRenderer.MeasureWidth(typography with { Size = fontSize }, line);
            var stableUpperWidth = remainingLength > fitLength
                ? MeasureSegment(text, remainingStart, fitLength + 1, typography, fontSize)
                : float.PositiveInfinity;
            lines.Add(line);
            widths.Add(lineWidth);
            UpdateReusableWidthBounds(
                lineWidth,
                stableUpperWidth,
                ref reusableMinimumWidth,
                ref reusableMaximumWidth);
            remainingStart += fitLength;
            remainingLength -= fitLength;
        }
    }

    private static int FindLongestFittingSegmentLength(
        string text,
        int start,
        int maxLength,
        UiTypography typography,
        float fontSize,
        float availableWidth)
    {
        var low = 1;
        var high = maxLength;
        var best = 0;
        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var width = MeasureSegment(text, start, mid, typography, fontSize);
            if (width <= availableWidth)
            {
                best = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return best;
    }

    private static float MeasureSegment(string text, int start, int length, UiTypography typography, float fontSize)
    {
        if (length <= 0)
        {
            return 0f;
        }

        if (start == 0 && length == text.Length)
        {
            return UiTextRenderer.MeasureWidth(typography with { Size = fontSize }, text);
        }

        return UiTextRenderer.MeasureWidth(typography with { Size = fontSize }, text.Substring(start, length));
    }

    private static int QuantizeValue(float value)
    {
        if (float.IsInfinity(value))
        {
            return int.MaxValue;
        }

        if (float.IsNaN(value))
        {
            return int.MinValue;
        }

        return (int)MathF.Round(value * 100f);
    }

    private static bool TryReadLogicalLine(string text, ref int index, out int start, out int length)
    {
        if (index > text.Length)
        {
            start = 0;
            length = 0;
            return false;
        }

        start = index;
        while (index < text.Length && text[index] != '\r' && text[index] != '\n')
        {
            index++;
        }

        length = index - start;
        if (index >= text.Length)
        {
            index = text.Length + 1;
            return true;
        }

        if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
        {
            index += 2;
        }
        else
        {
            index++;
        }

        return true;
    }

    private static void UpdateReusableWidthBounds(
        float trimmedLineWidth,
        float stableUpperWidth,
        ref float reusableMinimumWidth,
        ref float reusableMaximumWidth)
    {
        reusableMinimumWidth = MathF.Max(reusableMinimumWidth, trimmedLineWidth);
        reusableMaximumWidth = MathF.Min(reusableMaximumWidth, stableUpperWidth);
    }

    private static TextParagraphLayoutAnalysis ResolveParagraphLayoutAnalysis(string text, UiTypography typography, float fontSize)
    {
        var key = TextParagraphAnalysisCacheKey.Create(text, typography, fontSize);
        if (ParagraphAnalysisCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var analysis = BuildParagraphLayoutAnalysis(text, typography, fontSize);
        ParagraphAnalysisCache[key] = analysis;
        ParagraphAnalysisCacheOrder.Enqueue(key);

        while (ParagraphAnalysisCacheOrder.Count > ParagraphAnalysisCacheCapacity)
        {
            var expired = ParagraphAnalysisCacheOrder.Dequeue();
            ParagraphAnalysisCache.Remove(expired);
        }

        return analysis;
    }

    private static TextParagraphLayoutAnalysis BuildParagraphLayoutAnalysis(string text, UiTypography typography, float fontSize)
    {
        var paragraphs = new List<ParagraphAnalysis>();
        var index = 0;
        while (TryReadLogicalLine(text, ref index, out var start, out var length))
        {
            if (length == 0)
            {
                paragraphs.Add(new ParagraphAnalysis(start, 0, Array.Empty<TokenAnalysis>()));
                continue;
            }

            var tokens = new List<TokenAnalysis>();
            var paragraphEnd = start + length;
            var tokenStart = start;
            while (tokenStart < paragraphEnd)
            {
                var tokenIsWhitespace = char.IsWhiteSpace(text[tokenStart]);
                var tokenEnd = tokenStart + 1;
                while (tokenEnd < paragraphEnd && char.IsWhiteSpace(text[tokenEnd]) == tokenIsWhitespace)
                {
                    tokenEnd++;
                }

                var tokenLength = tokenEnd - tokenStart;
                var tokenWidth = MeasureSegment(text, tokenStart, tokenLength, typography, fontSize);
                tokens.Add(new TokenAnalysis(tokenStart, tokenLength, tokenIsWhitespace, tokenWidth));
                tokenStart = tokenEnd;
            }

            paragraphs.Add(new ParagraphAnalysis(start, length, tokens.ToArray()));
        }

        return new TextParagraphLayoutAnalysis(paragraphs.ToArray());
    }

    private static TextLayoutResult BuildResult(
        IReadOnlyList<string> lines,
        IReadOnlyList<float> widths,
        UiTypography typography,
        float fontSize,
        float reusableMinimumWidth,
        float reusableMaximumWidth)
    {
        if (lines.Count == 0)
        {
            return TextLayoutResult.Empty;
        }

        float maxWidth = 0f;
        for (var i = 0; i < widths.Count; i++)
        {
            if (widths[i] > maxWidth)
            {
                maxWidth = widths[i];
            }
        }

        var size = new Vector2(maxWidth, lines.Count * UiTextRenderer.GetLineHeight(typography with { Size = fontSize }));
        return new TextLayoutResult(lines, widths, size, reusableMinimumWidth, reusableMaximumWidth);
    }

    private static void AddToCache(TextLayoutCacheKey key, TextLayoutResult result)
    {
        Cache[key] = result;
        CacheOrder.Enqueue(key);

        while (CacheOrder.Count > CacheCapacity)
        {
            var expired = CacheOrder.Dequeue();
            Cache.Remove(expired);
        }
    }

    public readonly struct TextLayoutResult
    {
        public static readonly TextLayoutResult Empty = new(Array.Empty<string>(), Array.Empty<float>(), Vector2.Zero, 0f, float.PositiveInfinity);

        public TextLayoutResult(
            IReadOnlyList<string> lines,
            IReadOnlyList<float> lineWidths,
            Vector2 size,
            float reusableMinimumWidth,
            float reusableMaximumWidth)
        {
            Lines = lines;
            LineWidths = lineWidths;
            Size = size;
            ReusableMinimumWidth = reusableMinimumWidth;
            ReusableMaximumWidth = reusableMaximumWidth;
        }

        public IReadOnlyList<string> Lines { get; }

        internal IReadOnlyList<float> LineWidths { get; }

        public Vector2 Size { get; }

        internal float ReusableMinimumWidth { get; }

        internal float ReusableMaximumWidth { get; }
    }

    private readonly struct TextLayoutCacheKey : IEquatable<TextLayoutCacheKey>
    {
        private TextLayoutCacheKey(string text, UiTypography typography, int fontSizeBucket, int widthBucket, TextWrapping wrapping)
        {
            Text = text;
            Typography = typography;
            FontSizeBucket = fontSizeBucket;
            WidthBucket = widthBucket;
            Wrapping = wrapping;
        }

        private string Text { get; }

        private UiTypography Typography { get; }

        private int FontSizeBucket { get; }

        private int WidthBucket { get; }

        private TextWrapping Wrapping { get; }

        public static TextLayoutCacheKey Create(string text, UiTypography typography, float fontSize, float width, TextWrapping wrapping)
        {
            return new TextLayoutCacheKey(text, typography with { Size = fontSize }, TextLayout.QuantizeValue(fontSize), TextLayout.QuantizeValue(width), wrapping);
        }

        public bool Equals(TextLayoutCacheKey other)
        {
            return FontSizeBucket == other.FontSizeBucket &&
                   WidthBucket == other.WidthBucket &&
                   Wrapping == other.Wrapping &&
                   Typography.Equals(other.Typography) &&
                   string.Equals(Text, other.Text, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is TextLayoutCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(Text),
                Typography,
                FontSizeBucket,
                WidthBucket,
                (int)Wrapping);
        }
    }

    private readonly struct TextParagraphAnalysisCacheKey : IEquatable<TextParagraphAnalysisCacheKey>
    {
        private TextParagraphAnalysisCacheKey(string text, UiTypography typography, int fontSizeBucket)
        {
            Text = text;
            Typography = typography;
            FontSizeBucket = fontSizeBucket;
        }

        private string Text { get; }

        private UiTypography Typography { get; }

        private int FontSizeBucket { get; }

        public static TextParagraphAnalysisCacheKey Create(string text, UiTypography typography, float fontSize)
        {
            return new TextParagraphAnalysisCacheKey(text, typography with { Size = fontSize }, TextLayout.QuantizeValue(fontSize));
        }

        public bool Equals(TextParagraphAnalysisCacheKey other)
        {
            return FontSizeBucket == other.FontSizeBucket &&
                   Typography.Equals(other.Typography) &&
                   string.Equals(Text, other.Text, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is TextParagraphAnalysisCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(Text),
                Typography,
                FontSizeBucket);
        }
    }

    private sealed class TextParagraphLayoutAnalysis
    {
        public TextParagraphLayoutAnalysis(ParagraphAnalysis[] paragraphs)
        {
            Paragraphs = paragraphs;
        }

        public ParagraphAnalysis[] Paragraphs { get; }
    }

    private readonly struct ParagraphAnalysis
    {
        public ParagraphAnalysis(int start, int length, TokenAnalysis[] tokens)
        {
            Start = start;
            Length = length;
            Tokens = tokens;
        }

        public int Start { get; }

        public int Length { get; }

        public TokenAnalysis[] Tokens { get; }
    }

    private readonly struct TokenAnalysis
    {
        public TokenAnalysis(int start, int length, bool isWhitespace, float width)
        {
            Start = start;
            Length = length;
            IsWhitespace = isWhitespace;
            Width = width;
        }

        public int Start { get; }

        public int Length { get; }

        public bool IsWhitespace { get; }

        public float Width { get; }
    }
}
