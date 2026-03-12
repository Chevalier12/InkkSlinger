using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

internal static class UiTextRenderer
{
    private static SpriteFont? _defaultFont;
    private static long _measureWidthElapsedTicks;
    private static long _getLineHeightElapsedTicks;
    private static long _tryGetFontElapsedTicks;
    private static long _ensureInitializedElapsedTicks;
    private static long _rendererDrawStringElapsedTicks;
    private static long _spriteFontDrawStringElapsedTicks;
    private static int _measureWidthCallCount;
    private static int _getLineHeightCallCount;
    private static int _tryGetFontCallCount;
    private static int _ensureInitializedCallCount;
    private static int _rendererDrawStringCallCount;
    private static int _spriteFontDrawStringCallCount;
    private static int _spriteFontDrawStringEnabledPathCount;
    private static int _spriteFontDrawStringFallbackPathCount;
    private static int _fontCacheHitCount;
    private static int _fontCacheMissCount;

    public static bool IsEnabled => false;

    public static void SetDefaultFont(SpriteFont? font)
    {
        _defaultFont = font;
        if (font != null)
        {
            UiApplication.Current.Resources["DefaultFont"] = font;
        }
        else if (UiApplication.Current.Resources.ContainsKey("DefaultFont"))
        {
            UiApplication.Current.Resources.Remove("DefaultFont");
        }
    }

    internal static SpriteFont? ResolveFont(SpriteFont? spriteFont)
    {
        if (spriteFont != null)
        {
            return spriteFont;
        }

        if (_defaultFont != null)
        {
            return _defaultFont;
        }

        if (UiApplication.Current.Resources.TryGetValue("DefaultFont", out var resource) &&
            resource is SpriteFont defaultFont)
        {
            _defaultFont = defaultFont;
            return defaultFont;
        }

        return null;
    }

    internal static bool HasRenderableFont(SpriteFont? spriteFont)
    {
        return ResolveFont(spriteFont) != null;
    }

    public static void DrawString(
        SpriteBatch spriteBatch,
        string text,
        Vector2 position,
        Color color,
        float fontSize)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            _rendererDrawStringCallCount++;
            _spriteFontDrawStringFallbackPathCount++;
        }
        finally
        {
            _rendererDrawStringElapsedTicks += Stopwatch.GetTimestamp() - start;
        }
    }

    public static void DrawString(
        SpriteBatch spriteBatch,
        SpriteFont? spriteFont,
        string text,
        Vector2 position,
        Color color)
    {
        DrawString(spriteBatch, spriteFont, text, position, color, GetRenderFontSize(spriteFont));
    }

    public static void DrawString(
        SpriteBatch spriteBatch,
        SpriteFont? spriteFont,
        string text,
        Vector2 position,
        Color color,
        float fontSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var start = Stopwatch.GetTimestamp();
        try
        {
            _spriteFontDrawStringCallCount++;
            _spriteFontDrawStringFallbackPathCount++;
            DrawSpriteFontString(spriteBatch, ResolveFont(spriteFont), text, position, color, fontSize);
        }
        finally
        {
            _spriteFontDrawStringElapsedTicks += Stopwatch.GetTimestamp() - start;
        }
    }

    public static void DrawString(
        SpriteBatch spriteBatch,
        SpriteFont? spriteFont,
        string text,
        Vector2 position,
        Color color,
        bool bold)
    {
        DrawString(spriteBatch, spriteFont, text, position, color, GetRenderFontSize(spriteFont), bold);
    }

    public static void DrawString(
        SpriteBatch spriteBatch,
        SpriteFont? spriteFont,
        string text,
        Vector2 position,
        Color color,
        float fontSize,
        bool bold)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var start = Stopwatch.GetTimestamp();
        try
        {
            _spriteFontDrawStringCallCount++;
            _spriteFontDrawStringFallbackPathCount++;
            DrawSpriteFontString(spriteBatch, ResolveFont(spriteFont), text, position, color, fontSize);
            if (bold)
            {
                DrawSpriteFontString(spriteBatch, ResolveFont(spriteFont), text, position, color * 0.45f, fontSize);
            }
        }
        finally
        {
            _spriteFontDrawStringElapsedTicks += Stopwatch.GetTimestamp() - start;
        }
    }

    public static float MeasureWidth(string text, float fontSize)
    {
        var start = Stopwatch.GetTimestamp();
        _measureWidthCallCount++;
        try
        {
            return string.IsNullOrEmpty(text)
                ? 0f
                : text.Length * MathF.Max(1f, MathF.Max(8f, fontSize) * 0.5f);
        }
        finally
        {
            _measureWidthElapsedTicks += Stopwatch.GetTimestamp() - start;
        }
    }

    public static float MeasureWidth(SpriteFont? spriteFont, string text)
    {
        return MeasureWidth(spriteFont, text, GetRenderFontSize(spriteFont));
    }

    public static float MeasureWidth(SpriteFont? spriteFont, string text, float fontSize)
    {
        var start = Stopwatch.GetTimestamp();
        _measureWidthCallCount++;
        try
        {
            spriteFont = ResolveFont(spriteFont);
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }

            if (TryMeasureSpriteFontString(spriteFont, text, out var measuredSize))
            {
                return measuredSize.X * GetSpriteFontScale(spriteFont!, fontSize);
            }

            return text.Length * MathF.Max(1f, ResolveRenderFontSize(spriteFont, fontSize) * 0.5f);
        }
        finally
        {
            _measureWidthElapsedTicks += Stopwatch.GetTimestamp() - start;
        }
    }

    public static float MeasureWidth(SpriteFont? spriteFont, string text, bool bold)
    {
        return MeasureWidth(spriteFont, text);
    }

    public static float MeasureWidth(SpriteFont? spriteFont, string text, float fontSize, bool bold)
    {
        return MeasureWidth(spriteFont, text, fontSize);
    }

    public static float MeasureHeight(SpriteFont? spriteFont, string text)
    {
        return MeasureHeight(spriteFont, text, GetRenderFontSize(spriteFont));
    }

    public static float MeasureHeight(SpriteFont? spriteFont, string text, float fontSize)
    {
        spriteFont = ResolveFont(spriteFont);
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        if (TryMeasureSpriteFontString(spriteFont, text, out var measuredSize))
        {
            return measuredSize.Y * GetSpriteFontScale(spriteFont!, fontSize);
        }

        return ResolveRenderFontSize(spriteFont, fontSize);
    }

    public static float GetLineHeight(SpriteFont? spriteFont)
    {
        return GetLineHeight(spriteFont, GetRenderFontSize(spriteFont));
    }

    public static float GetLineHeight(SpriteFont? spriteFont, float fontSize)
    {
        var start = Stopwatch.GetTimestamp();
        _getLineHeightCallCount++;
        try
        {
            spriteFont = ResolveFont(spriteFont);
            if (spriteFont != null && spriteFont.LineSpacing > 0)
            {
                return spriteFont.LineSpacing * GetSpriteFontScale(spriteFont, fontSize);
            }

            return ResolveRenderFontSize(spriteFont, fontSize);
        }
        finally
        {
            _getLineHeightElapsedTicks += Stopwatch.GetTimestamp() - start;
        }
    }

    private static void DrawSpriteFontString(
        SpriteBatch spriteBatch,
        SpriteFont? spriteFont,
        string text,
        Vector2 position,
        Color color,
        float fontSize)
    {
        if (spriteFont == null)
        {
            return;
        }

        try
        {
            var transformedPosition = UiDrawing.TransformPoint(spriteBatch, position);
            var scaleX = MathF.Abs(UiDrawing.GetScaleX(spriteBatch));
            var scaleY = MathF.Abs(UiDrawing.GetScaleY(spriteBatch));
            var fontScale = GetSpriteFontScale(spriteFont, fontSize);
            if (scaleX <= 0f)
            {
                scaleX = 1f;
            }

            if (scaleY <= 0f)
            {
                scaleY = 1f;
            }

            spriteBatch.DrawString(
                spriteFont,
                text,
                transformedPosition,
                color,
                0f,
                Vector2.Zero,
                new Vector2(scaleX * fontScale, scaleY * fontScale),
                SpriteEffects.None,
                0f);
        }
        catch (NullReferenceException)
        {
        }
        catch (ArgumentException)
        {
        }
    }

    private static bool TryMeasureSpriteFontString(SpriteFont? spriteFont, string text, out Vector2 measuredSize)
    {
        if (spriteFont != null)
        {
            try
            {
                measuredSize = spriteFont.MeasureString(text);
                return true;
            }
            catch (NullReferenceException)
            {
            }
            catch (ArgumentException)
            {
            }
        }

        measuredSize = Vector2.Zero;
        return false;
    }

    private static float GetRenderFontSize(SpriteFont? spriteFont)
    {
        if (spriteFont == null)
        {
            return 16f;
        }

        return MathF.Max(8f, spriteFont.LineSpacing - 2f);
    }

    private static float ResolveRenderFontSize(SpriteFont? spriteFont, float requestedFontSize)
    {
        var baseFontSize = GetRenderFontSize(spriteFont);
        if (requestedFontSize <= 0f)
        {
            return baseFontSize;
        }

        return MathF.Max(8f, requestedFontSize);
    }

    private static float GetSpriteFontScale(SpriteFont spriteFont, float requestedFontSize)
    {
        var effectiveFontSize = ResolveRenderFontSize(spriteFont, requestedFontSize);
        if (spriteFont.LineSpacing <= 0f)
        {
            return 1f;
        }

        return effectiveFontSize / spriteFont.LineSpacing;
    }

    internal static UiTextRendererTimingSnapshot GetTimingSnapshotForTests()
    {
        return new UiTextRendererTimingSnapshot(
            _measureWidthElapsedTicks,
            _measureWidthCallCount,
            _getLineHeightElapsedTicks,
            _getLineHeightCallCount,
            _tryGetFontElapsedTicks,
            _tryGetFontCallCount,
            _ensureInitializedElapsedTicks,
            _ensureInitializedCallCount,
            _rendererDrawStringElapsedTicks,
            _rendererDrawStringCallCount,
            _spriteFontDrawStringElapsedTicks,
            _spriteFontDrawStringCallCount,
            _spriteFontDrawStringEnabledPathCount,
            _spriteFontDrawStringFallbackPathCount,
            _fontCacheHitCount,
            _fontCacheMissCount);
    }

    internal static void ResetTimingForTests()
    {
        _measureWidthElapsedTicks = 0;
        _getLineHeightElapsedTicks = 0;
        _tryGetFontElapsedTicks = 0;
        _ensureInitializedElapsedTicks = 0;
        _rendererDrawStringElapsedTicks = 0;
        _spriteFontDrawStringElapsedTicks = 0;
        _measureWidthCallCount = 0;
        _getLineHeightCallCount = 0;
        _tryGetFontCallCount = 0;
        _ensureInitializedCallCount = 0;
        _rendererDrawStringCallCount = 0;
        _spriteFontDrawStringCallCount = 0;
        _spriteFontDrawStringEnabledPathCount = 0;
        _spriteFontDrawStringFallbackPathCount = 0;
        _fontCacheHitCount = 0;
        _fontCacheMissCount = 0;
    }
}

internal readonly record struct UiTextRendererTimingSnapshot(
    long MeasureWidthElapsedTicks,
    int MeasureWidthCallCount,
    long GetLineHeightElapsedTicks,
    int GetLineHeightCallCount,
    long TryGetFontElapsedTicks,
    int TryGetFontCallCount,
    long EnsureInitializedElapsedTicks,
    int EnsureInitializedCallCount,
    long RendererDrawStringElapsedTicks,
    int RendererDrawStringCallCount,
    long SpriteFontDrawStringElapsedTicks,
    int SpriteFontDrawStringCallCount,
    int SpriteFontDrawStringEnabledPathCount,
    int SpriteFontDrawStringFallbackPathCount,
    int FontCacheHitCount,
    int FontCacheMissCount);
