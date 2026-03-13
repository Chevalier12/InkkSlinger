using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public abstract class Effect : Freezable
{
    public new Effect Clone()
    {
        return (Effect)base.Clone();
    }

    public new Effect CloneCurrentValue()
    {
        return (Effect)base.CloneCurrentValue();
    }

    internal abstract void Render(UIElement element, SpriteBatch spriteBatch, float elementOpacity);

    internal abstract LayoutRect GetRenderBounds(UIElement element);
}

public sealed class DropShadowEffect : Effect
{
    private static readonly Dictionary<ShadowTextureCacheKey, Texture2D> ShadowTextures = new();

    private Color _color = Color.Black;
    private float _shadowDepth;
    private float _blurRadius;
    private float _opacity;

    public Color Color
    {
        get => _color;
        set
        {
            WritePreamble();
            if (_color == value)
            {
                return;
            }

            _color = value;
            WritePostscript();
        }
    }

    public float ShadowDepth
    {
        get => _shadowDepth;
        set
        {
            WritePreamble();
            if (MathF.Abs(_shadowDepth - value) <= 0.0001f)
            {
                return;
            }

            _shadowDepth = value;
            WritePostscript();
        }
    }

    public float BlurRadius
    {
        get => _blurRadius;
        set
        {
            WritePreamble();
            var clamped = value < 0f ? 0f : value;
            if (MathF.Abs(_blurRadius - clamped) <= 0.0001f)
            {
                return;
            }

            _blurRadius = clamped;
            WritePostscript();
        }
    }

    public float Opacity
    {
        get => _opacity;
        set
        {
            WritePreamble();
            var clamped = Math.Clamp(value, 0f, 1f);
            if (MathF.Abs(_opacity - clamped) <= 0.0001f)
            {
                return;
            }

            _opacity = clamped;
            WritePostscript();
        }
    }

    protected override Freezable CreateInstanceCore()
    {
        return new DropShadowEffect();
    }

    protected override void CloneCore(Freezable source)
    {
        var typedSource = (DropShadowEffect)source;
        _color = typedSource._color;
        _shadowDepth = typedSource._shadowDepth;
        _blurRadius = typedSource._blurRadius;
        _opacity = typedSource._opacity;
    }

    internal override void Render(UIElement element, SpriteBatch spriteBatch, float elementOpacity)
    {
        var slot = element.LayoutSlot;
        if (slot.Width <= 0f || slot.Height <= 0f)
        {
            return;
        }

        var effectiveOpacity = Opacity * elementOpacity;
        if (effectiveOpacity <= 0f || Color.A == 0)
        {
            return;
        }

        var shadowRect = new LayoutRect(slot.X, slot.Y + ShadowDepth, slot.Width, slot.Height);
        var blur = BlurRadius;
        if (blur <= 0.001f)
        {
            UiDrawing.DrawFilledRect(spriteBatch, shadowRect, Color, effectiveOpacity);
            return;
        }

        var blurSize = Math.Clamp((int)MathF.Ceiling(MathF.Min(blur, 32f)), 1, 32);
        DrawBlurSlices(spriteBatch, shadowRect, blurSize, Color, effectiveOpacity);
        UiDrawing.DrawFilledRect(spriteBatch, shadowRect, Color, effectiveOpacity);
    }

    internal override LayoutRect GetRenderBounds(UIElement element)
    {
        var slot = element.LayoutSlot;
        if (slot.Width <= 0f || slot.Height <= 0f)
        {
            return slot;
        }

        var effectiveOpacity = Opacity * element.Opacity;
        if (effectiveOpacity <= 0f || Color.A == 0)
        {
            return slot;
        }

        var shadowRect = new LayoutRect(slot.X, slot.Y + ShadowDepth, slot.Width, slot.Height);
        var blur = BlurRadius;
        if (blur <= 0.001f)
        {
            return Union(slot, shadowRect);
        }

        var expandedShadow = new LayoutRect(
            shadowRect.X - blur,
            shadowRect.Y - blur,
            shadowRect.Width + (blur * 2f),
            shadowRect.Height + (blur * 2f));
        return Union(slot, expandedShadow);
    }

    private static void DrawBlurSlices(
        SpriteBatch spriteBatch,
        LayoutRect shadowRect,
        int blurSize,
        Color color,
        float opacity)
    {
        var graphicsDevice = spriteBatch.GraphicsDevice;
        var topRect = new LayoutRect(shadowRect.X, shadowRect.Y - blurSize, shadowRect.Width, blurSize);
        var bottomRect = new LayoutRect(shadowRect.X, shadowRect.Y + shadowRect.Height, shadowRect.Width, blurSize);
        var leftRect = new LayoutRect(shadowRect.X - blurSize, shadowRect.Y, blurSize, shadowRect.Height);
        var rightRect = new LayoutRect(shadowRect.X + shadowRect.Width, shadowRect.Y, blurSize, shadowRect.Height);
        var topLeftRect = new LayoutRect(shadowRect.X - blurSize, shadowRect.Y - blurSize, blurSize, blurSize);
        var topRightRect = new LayoutRect(shadowRect.X + shadowRect.Width, shadowRect.Y - blurSize, blurSize, blurSize);
        var bottomLeftRect = new LayoutRect(shadowRect.X - blurSize, shadowRect.Y + shadowRect.Height, blurSize, blurSize);
        var bottomRightRect = new LayoutRect(shadowRect.X + shadowRect.Width, shadowRect.Y + shadowRect.Height, blurSize, blurSize);

        UiDrawing.DrawTexture(spriteBatch, GetShadowTexture(graphicsDevice, blurSize, ShadowTextureKind.TopEdge), topRect, color: color, opacity: opacity);
        UiDrawing.DrawTexture(spriteBatch, GetShadowTexture(graphicsDevice, blurSize, ShadowTextureKind.BottomEdge), bottomRect, color: color, opacity: opacity);
        UiDrawing.DrawTexture(spriteBatch, GetShadowTexture(graphicsDevice, blurSize, ShadowTextureKind.LeftEdge), leftRect, color: color, opacity: opacity);
        UiDrawing.DrawTexture(spriteBatch, GetShadowTexture(graphicsDevice, blurSize, ShadowTextureKind.RightEdge), rightRect, color: color, opacity: opacity);
        UiDrawing.DrawTexture(spriteBatch, GetShadowTexture(graphicsDevice, blurSize, ShadowTextureKind.TopLeftCorner), topLeftRect, color: color, opacity: opacity);
        UiDrawing.DrawTexture(spriteBatch, GetShadowTexture(graphicsDevice, blurSize, ShadowTextureKind.TopRightCorner), topRightRect, color: color, opacity: opacity);
        UiDrawing.DrawTexture(spriteBatch, GetShadowTexture(graphicsDevice, blurSize, ShadowTextureKind.BottomLeftCorner), bottomLeftRect, color: color, opacity: opacity);
        UiDrawing.DrawTexture(spriteBatch, GetShadowTexture(graphicsDevice, blurSize, ShadowTextureKind.BottomRightCorner), bottomRightRect, color: color, opacity: opacity);
    }

    private static Texture2D GetShadowTexture(GraphicsDevice graphicsDevice, int blurSize, ShadowTextureKind kind)
    {
        var key = new ShadowTextureCacheKey(graphicsDevice, blurSize, kind);
        if (ShadowTextures.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var texture = kind switch
        {
            ShadowTextureKind.TopEdge => CreateVerticalGradientTexture(graphicsDevice, blurSize, nearOpaqueAtEnd: true),
            ShadowTextureKind.BottomEdge => CreateVerticalGradientTexture(graphicsDevice, blurSize, nearOpaqueAtEnd: false),
            ShadowTextureKind.LeftEdge => CreateHorizontalGradientTexture(graphicsDevice, blurSize, nearOpaqueAtEnd: true),
            ShadowTextureKind.RightEdge => CreateHorizontalGradientTexture(graphicsDevice, blurSize, nearOpaqueAtEnd: false),
            ShadowTextureKind.TopLeftCorner => CreateCornerGradientTexture(graphicsDevice, blurSize, nearRectXAtEnd: true, nearRectYAtEnd: true),
            ShadowTextureKind.TopRightCorner => CreateCornerGradientTexture(graphicsDevice, blurSize, nearRectXAtEnd: false, nearRectYAtEnd: true),
            ShadowTextureKind.BottomLeftCorner => CreateCornerGradientTexture(graphicsDevice, blurSize, nearRectXAtEnd: true, nearRectYAtEnd: false),
            ShadowTextureKind.BottomRightCorner => CreateCornerGradientTexture(graphicsDevice, blurSize, nearRectXAtEnd: false, nearRectYAtEnd: false),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
        ShadowTextures[key] = texture;
        return texture;
    }

    private static Texture2D CreateVerticalGradientTexture(GraphicsDevice graphicsDevice, int blurSize, bool nearOpaqueAtEnd)
    {
        var texture = new Texture2D(graphicsDevice, 1, blurSize);
        var pixels = new Color[blurSize];
        for (var y = 0; y < blurSize; y++)
        {
            var alpha = nearOpaqueAtEnd
                ? (y + 1f) / blurSize
                : (blurSize - y) / (float)blurSize;
            pixels[y] = Color.White * alpha;
        }

        texture.SetData(pixels);
        return texture;
    }

    private static Texture2D CreateHorizontalGradientTexture(GraphicsDevice graphicsDevice, int blurSize, bool nearOpaqueAtEnd)
    {
        var texture = new Texture2D(graphicsDevice, blurSize, 1);
        var pixels = new Color[blurSize];
        for (var x = 0; x < blurSize; x++)
        {
            var alpha = nearOpaqueAtEnd
                ? (x + 1f) / blurSize
                : (blurSize - x) / (float)blurSize;
            pixels[x] = Color.White * alpha;
        }

        texture.SetData(pixels);
        return texture;
    }

    private static Texture2D CreateCornerGradientTexture(
        GraphicsDevice graphicsDevice,
        int blurSize,
        bool nearRectXAtEnd,
        bool nearRectYAtEnd)
    {
        var texture = new Texture2D(graphicsDevice, blurSize, blurSize);
        var pixels = new Color[blurSize * blurSize];
        var maxIndex = Math.Max(1, blurSize - 1);
        for (var y = 0; y < blurSize; y++)
        {
            var normalizedY = nearRectYAtEnd
                ? (maxIndex - y) / (float)maxIndex
                : y / (float)maxIndex;
            for (var x = 0; x < blurSize; x++)
            {
                var normalizedX = nearRectXAtEnd
                    ? (maxIndex - x) / (float)maxIndex
                    : x / (float)maxIndex;
                var alpha = 1f - Math.Clamp(MathF.Max(normalizedX, normalizedY), 0f, 1f);
                pixels[(y * blurSize) + x] = Color.White * alpha;
            }
        }

        texture.SetData(pixels);
        return texture;
    }

    private readonly record struct ShadowTextureCacheKey(GraphicsDevice GraphicsDevice, int BlurSize, ShadowTextureKind Kind);

    private static LayoutRect Union(LayoutRect left, LayoutRect right)
    {
        var x = MathF.Min(left.X, right.X);
        var y = MathF.Min(left.Y, right.Y);
        var rightEdge = MathF.Max(left.X + left.Width, right.X + right.Width);
        var bottomEdge = MathF.Max(left.Y + left.Height, right.Y + right.Height);
        return new LayoutRect(x, y, MathF.Max(0f, rightEdge - x), MathF.Max(0f, bottomEdge - y));
    }

    private enum ShadowTextureKind
    {
        TopEdge,
        BottomEdge,
        LeftEdge,
        RightEdge,
        TopLeftCorner,
        TopRightCorner,
        BottomLeftCorner,
        BottomRightCorner
    }
}
