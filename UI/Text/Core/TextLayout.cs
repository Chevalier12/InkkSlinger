using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
            Cache.Count);
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
        Cache.Clear();
        CacheOrder.Clear();
    }

    public static TextLayoutResult Layout(string? text, SpriteFont? font, float fontSize, float availableWidth, TextWrapping wrapping)
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

    public static TextLayoutResult Layout(string? text, SpriteFont? font, float availableWidth, TextWrapping wrapping)
    {
        return Layout(text, font, FontStashTextRenderer.GetLineHeight(font), availableWidth, wrapping);
    }

    private static TextLayoutResult BuildLayout(string text, SpriteFont? font, float fontSize, float availableWidth, TextWrapping wrapping)
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
        var lines = new List<string>();
        var paragraphs = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        foreach (var paragraph in paragraphs)
        {
            LayoutParagraph(paragraph, font, fontSize, availableWidth, lines);
        }

        return BuildResult(lines, font, fontSize);
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
        int CacheEntryCount);

    private static TextLayoutResult BuildNoWrapLayout(string text, SpriteFont? font, float fontSize)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var measured = new Vector2(
            FontStashTextRenderer.MeasureWidth(font, text, fontSize),
            lines.Length * FontStashTextRenderer.GetLineHeight(font, fontSize));
        return new TextLayoutResult(lines, measured);
    }

    private static void LayoutParagraph(string paragraph, SpriteFont? font, float fontSize, float availableWidth, IList<string> lines)
    {
        if (paragraph.Length == 0)
        {
            lines.Add(string.Empty);
            return;
        }

        var initialLineCount = lines.Count;
        var line = string.Empty;
        var index = 0;
        while (index < paragraph.Length)
        {
            var token = ReadToken(paragraph, ref index);
            if (token.Length == 0)
            {
                continue;
            }

            var candidate = line + token;
            var candidateWidth = FontStashTextRenderer.MeasureWidth(font, candidate, fontSize);
            if (candidateWidth <= availableWidth)
            {
                line = candidate;
                continue;
            }

            if (!string.IsNullOrEmpty(line))
            {
                lines.Add(line);
                line = string.Empty;
                index -= token.Length;
                continue;
            }

            BreakLongToken(token, font, fontSize, availableWidth, lines);
        }

        if (line.Length > 0 || lines.Count == initialLineCount)
        {
            lines.Add(line);
        }
    }

    private static void BreakLongToken(string token, SpriteFont? font, float fontSize, float availableWidth, IList<string> lines)
    {
        var start = 0;
        while (start < token.Length)
        {
            var length = 1;
            var fitLength = 0;
            while (start + length <= token.Length)
            {
                var segment = token.Substring(start, length);
                var width = FontStashTextRenderer.MeasureWidth(font, segment, fontSize);
                if (width <= availableWidth)
                {
                    fitLength = length;
                    length++;
                    continue;
                }

                break;
            }

            if (fitLength == 0)
            {
                fitLength = 1;
            }

            lines.Add(token.Substring(start, fitLength));
            start += fitLength;
        }
    }

    private static string ReadToken(string text, ref int index)
    {
        var start = index;
        var isWhitespace = char.IsWhiteSpace(text[index]);
        while (index < text.Length && char.IsWhiteSpace(text[index]) == isWhitespace)
        {
            index++;
        }

        return text[start..index];
    }

    private static bool IsWhitespaceToken(string token)
    {
        for (var i = 0; i < token.Length; i++)
        {
            if (!char.IsWhiteSpace(token[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static TextLayoutResult BuildResult(IReadOnlyList<string> lines, SpriteFont? font, float fontSize)
    {
        if (lines.Count == 0)
        {
            return TextLayoutResult.Empty;
        }

        float maxWidth = 0f;
        for (var i = 0; i < lines.Count; i++)
        {
            var width = lines[i].Length == 0 ? 0f : FontStashTextRenderer.MeasureWidth(font, lines[i], fontSize);
            if (width > maxWidth)
            {
                maxWidth = width;
            }
        }

        var size = new Vector2(maxWidth, lines.Count * FontStashTextRenderer.GetLineHeight(font, fontSize));
        return new TextLayoutResult(lines, size);
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
        public static readonly TextLayoutResult Empty = new(Array.Empty<string>(), Vector2.Zero);

        public TextLayoutResult(IReadOnlyList<string> lines, Vector2 size)
        {
            Lines = lines;
            Size = size;
        }

        public IReadOnlyList<string> Lines { get; }

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
            return new TextLayoutCacheKey(text, font, QuantizeWidth(fontSize), QuantizeWidth(width), wrapping);
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

        private static int QuantizeWidth(float width)
        {
            if (float.IsInfinity(width))
            {
                return int.MaxValue;
            }

            if (float.IsNaN(width))
            {
                return int.MinValue;
            }

            return (int)MathF.Round(width * 100f);
        }
    }
}
