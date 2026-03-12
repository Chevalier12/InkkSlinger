using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public static class TextLayout
{
    private const int CacheCapacity = 512;
    private static readonly Dictionary<TextLayoutCacheKey, TextLayoutResult> Cache = new();
    private static readonly Queue<TextLayoutCacheKey> CacheOrder = new();
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
    }

    public static TextLayoutResult Layout(string? text, SpriteFont? font, float fontSize, float availableWidth, TextWrapping wrapping)
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
        var key = TextLayoutCacheKey.Create(text, font, fontSize, availableWidth, wrapping);
        if (Cache.TryGetValue(key, out var cached))
        {
            _cacheHitCount++;
            return cached;
        }

        _cacheMissCount++;
        var result = BuildLayout(text, font, fontSize, availableWidth, wrapping);
        _totalProducedLineCount += result.Lines.Count;
        AddToCache(key, result);
        return result;
        }
        finally
        {
            _layoutElapsedTicks += Stopwatch.GetTimestamp() - start;
        }
    }

    public static TextLayoutResult Layout(string? text, SpriteFont? font, float availableWidth, TextWrapping wrapping)
    {
        return Layout(text, font, FontStashTextRenderer.GetLineHeight(font), availableWidth, wrapping);
    }

    private static TextLayoutResult BuildLayout(string text, SpriteFont? font, float fontSize, float availableWidth, TextWrapping wrapping)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
        _buildCount++;
        if (wrapping == TextWrapping.NoWrap ||
            float.IsInfinity(availableWidth) ||
            float.IsNaN(availableWidth) ||
            availableWidth <= 0f)
        {
            _noWrapBuildCount++;
            return BuildNoWrapLayout(text, font, fontSize);
        }

        _wrappedBuildCount++;
        return BuildWrappedLayout(text, font, fontSize, availableWidth);
        }
        finally
        {
            _buildElapsedTicks += Stopwatch.GetTimestamp() - start;
        }
    }

    public readonly record struct TextLayoutMetricsSnapshot(
        int LayoutRequestCount,
        int CacheHitCount,
        int CacheMissCount,
        int BuildCount,
        int NoWrapBuildCount,
        int WrappedBuildCount,
        int TotalMeasuredTextLength,
        int TotalProducedLineCount,
        int CacheEntryCount,
        long LayoutElapsedTicks,
        long BuildElapsedTicks);

    private static TextLayoutResult BuildNoWrapLayout(string text, SpriteFont? font, float fontSize)
    {
        var lines = new List<string>();
        var widths = new List<float>();
        var index = 0;
        while (TryReadLogicalLine(text, ref index, out var start, out var length))
        {
            var line = length == 0 ? string.Empty : text.Substring(start, length);
            lines.Add(line);
            widths.Add(line.Length == 0 ? 0f : FontStashTextRenderer.MeasureWidth(font, line, fontSize));
        }

        return BuildResult(lines, widths, font, fontSize);
    }

    private static TextLayoutResult BuildWrappedLayout(string text, SpriteFont? font, float fontSize, float availableWidth)
    {
        var lines = new List<string>();
        var widths = new List<float>();
        var index = 0;
        while (TryReadLogicalLine(text, ref index, out var start, out var length))
        {
            LayoutParagraph(text, start, length, font, fontSize, availableWidth, lines, widths);
        }

        return BuildResult(lines, widths, font, fontSize);
    }

    private static void LayoutParagraph(
        string text,
        int start,
        int length,
        SpriteFont? font,
        float fontSize,
        float availableWidth,
        IList<string> lines,
        IList<float> widths)
    {
        if (length == 0)
        {
            lines.Add(string.Empty);
            widths.Add(0f);
            return;
        }

        var initialLineCount = lines.Count;
        var builder = new StringBuilder(length);
        var lineWidth = 0f;
        var index = start;
        var end = start + length;
        while (index < end)
        {
            var tokenStart = index;
            var tokenIsWhitespace = char.IsWhiteSpace(text[index]);
            index++;
            while (index < end && char.IsWhiteSpace(text[index]) == tokenIsWhitespace)
            {
                index++;
            }

            var tokenLength = index - tokenStart;
            if (tokenLength == 0)
            {
                continue;
            }

            var tokenWidth = MeasureSegment(text, tokenStart, tokenLength, font, fontSize);
            if ((lineWidth + tokenWidth) <= availableWidth)
            {
                builder.Append(text, tokenStart, tokenLength);
                lineWidth += tokenWidth;
                continue;
            }

            if (builder.Length > 0)
            {
                lines.Add(builder.ToString());
                widths.Add(lineWidth);
                builder.Clear();
                lineWidth = 0f;
                index = tokenStart;
                continue;
            }

            BreakLongToken(text, tokenStart, tokenLength, font, fontSize, availableWidth, lines, widths);
        }

        if (builder.Length > 0 || lines.Count == initialLineCount)
        {
            lines.Add(builder.ToString());
            widths.Add(lineWidth);
        }
    }

    private static void BreakLongToken(
        string text,
        int start,
        int length,
        SpriteFont? font,
        float fontSize,
        float availableWidth,
        IList<string> lines,
        IList<float> widths)
    {
        var remainingStart = start;
        var remainingLength = length;
        while (remainingLength > 0)
        {
            var fitLength = FindLongestFittingSegmentLength(
                text,
                remainingStart,
                remainingLength,
                font,
                fontSize,
                availableWidth);
            if (fitLength <= 0)
            {
                fitLength = 1;
            }

            var line = text.Substring(remainingStart, fitLength);
            lines.Add(line);
            widths.Add(line.Length == 0 ? 0f : FontStashTextRenderer.MeasureWidth(font, line, fontSize));
            remainingStart += fitLength;
            remainingLength -= fitLength;
        }
    }

    private static int FindLongestFittingSegmentLength(
        string text,
        int start,
        int maxLength,
        SpriteFont? font,
        float fontSize,
        float availableWidth)
    {
        var low = 1;
        var high = maxLength;
        var best = 0;
        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var width = MeasureSegment(text, start, mid, font, fontSize);
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

    private static float MeasureSegment(string text, int start, int length, SpriteFont? font, float fontSize)
    {
        if (length <= 0)
        {
            return 0f;
        }

        if (start == 0 && length == text.Length)
        {
            return FontStashTextRenderer.MeasureWidth(font, text, fontSize);
        }

        return FontStashTextRenderer.MeasureWidth(font, text.Substring(start, length), fontSize);
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

    private static TextLayoutResult BuildResult(IReadOnlyList<string> lines, IReadOnlyList<float> widths, SpriteFont? font, float fontSize)
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

        var size = new Vector2(maxWidth, lines.Count * FontStashTextRenderer.GetLineHeight(font, fontSize));
        return new TextLayoutResult(lines, widths, size);
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
        public static readonly TextLayoutResult Empty = new(Array.Empty<string>(), Array.Empty<float>(), Vector2.Zero);

        public TextLayoutResult(IReadOnlyList<string> lines, IReadOnlyList<float> lineWidths, Vector2 size)
        {
            Lines = lines;
            LineWidths = lineWidths;
            Size = size;
        }

        public IReadOnlyList<string> Lines { get; }

        internal IReadOnlyList<float> LineWidths { get; }

        public Vector2 Size { get; }
    }

    private readonly struct TextLayoutCacheKey : IEquatable<TextLayoutCacheKey>
    {
        private TextLayoutCacheKey(string text, SpriteFont? font, int fontSizeBucket, int widthBucket, TextWrapping wrapping)
        {
            Text = text;
            Font = font;
            FontSizeBucket = fontSizeBucket;
            WidthBucket = widthBucket;
            Wrapping = wrapping;
        }

        private string Text { get; }

        private SpriteFont? Font { get; }

        private int FontSizeBucket { get; }

        private int WidthBucket { get; }

        private TextWrapping Wrapping { get; }

        public static TextLayoutCacheKey Create(string text, SpriteFont? font, float fontSize, float width, TextWrapping wrapping)
        {
            return new TextLayoutCacheKey(text, font, QuantizeValue(fontSize), QuantizeValue(width), wrapping);
        }

        public bool Equals(TextLayoutCacheKey other)
        {
            return FontSizeBucket == other.FontSizeBucket &&
                   WidthBucket == other.WidthBucket &&
                   Wrapping == other.Wrapping &&
                   ReferenceEquals(Font, other.Font) &&
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
                Font is null ? 0 : RuntimeHelpers.GetHashCode(Font),
                FontSizeBucket,
                WidthBucket,
                (int)Wrapping);
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
    }
}
