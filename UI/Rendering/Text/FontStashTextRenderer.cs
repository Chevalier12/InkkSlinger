using System;
using System.Collections.Generic;
using System.IO;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

internal static class FontStashTextRenderer
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<int, DynamicSpriteFont> FontsBySize = new();
    private static FontSystem? _fontSystem;
    private static bool _initializationAttempted;
    private static bool _isEnabled;

    public static bool IsEnabled
    {
        get
        {
            EnsureInitialized();
            return _isEnabled;
        }
    }

    public static void DrawString(
        SpriteBatch spriteBatch,
        string text,
        Vector2 position,
        Color color,
        float fontSize)
    {
        var scale = MathF.Abs(UiDrawing.GetScaleY(spriteBatch));
        var effectiveScale = scale <= 0f ? 1f : scale;
        var effectiveFontSize = fontSize * effectiveScale;
        if (!TryGetFont(effectiveFontSize, out var font))
        {
            return;
        }

        var transformedPosition = UiDrawing.TransformPoint(spriteBatch, position);
        spriteBatch.DrawString(font, text, transformedPosition, color);
    }

    public static void DrawString(
        SpriteBatch spriteBatch,
        SpriteFont? spriteFont,
        string text,
        Vector2 position,
        Color color)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (IsEnabled)
        {
            DrawString(spriteBatch, text, position, color, GetRenderFontSize(spriteFont));
            return;
        }

        if (spriteFont != null)
        {
            var transformedPosition = UiDrawing.TransformPoint(spriteBatch, position);
            var scaleX = MathF.Abs(UiDrawing.GetScaleX(spriteBatch));
            var scaleY = MathF.Abs(UiDrawing.GetScaleY(spriteBatch));
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
                new Vector2(scaleX, scaleY),
                SpriteEffects.None,
                0f);
        }
    }

    public static float MeasureWidth(string text, float fontSize)
    {
        if (!TryGetFont(fontSize, out var font))
        {
            return 0f;
        }

        return font.MeasureString(text).X;
    }

    public static float MeasureWidth(SpriteFont? spriteFont, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        if (IsEnabled)
        {
            return MeasureWidth(text, GetRenderFontSize(spriteFont));
        }

        return spriteFont?.MeasureString(text).X ?? 0f;
    }

    public static float MeasureHeight(SpriteFont? spriteFont, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        if (IsEnabled)
        {
            if (!TryGetFont(GetRenderFontSize(spriteFont), out var font))
            {
                return 0f;
            }

            return font.MeasureString(text).Y;
        }

        return spriteFont?.MeasureString(text).Y ?? 0f;
    }

    public static float GetLineHeight(SpriteFont? spriteFont)
    {
        if (IsEnabled)
        {
            return MathF.Max(8f, GetRenderFontSize(spriteFont));
        }

        return spriteFont?.LineSpacing ?? 14f;
    }

    private static bool TryGetFont(float fontSize, out DynamicSpriteFont font)
    {
        EnsureInitialized();
        if (!_isEnabled || _fontSystem == null)
        {
            font = null!;
            return false;
        }

        var sizeKey = Math.Clamp((int)MathF.Round(fontSize), 8, 96);
        lock (SyncRoot)
        {
            if (FontsBySize.TryGetValue(sizeKey, out var cached))
            {
                font = cached;
                return true;
            }

            var created = _fontSystem.GetFont(sizeKey);
            FontsBySize[sizeKey] = created;
            font = created;
            return true;
        }
    }

    private static void EnsureInitialized()
    {
        if (_initializationAttempted)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (_initializationAttempted)
            {
                return;
            }

            _initializationAttempted = true;
            try
            {
                var fontData = TryLoadFontBytes();
                if (fontData == null || fontData.Length == 0)
                {
                    _isEnabled = false;
                    return;
                }

                _fontSystem = new FontSystem();
                _fontSystem.AddFont(fontData);
                _isEnabled = true;
            }
            catch
            {
                _fontSystem = null;
                _isEnabled = false;
            }
        }
    }

    private static byte[]? TryLoadFontBytes()
    {
        var fontCandidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"),
            Path.Combine(AppContext.BaseDirectory, "Content", "Fonts", "NotoSans-Regular.ttf")
        };

        foreach (var candidate in fontCandidates)
        {
            if (File.Exists(candidate))
            {
                return File.ReadAllBytes(candidate);
            }
        }

        return null;
    }

    private static float GetRenderFontSize(SpriteFont? spriteFont)
    {
        if (spriteFont == null)
        {
            return 16f;
        }

        return MathF.Max(8f, spriteFont.LineSpacing - 2f);
    }
}
