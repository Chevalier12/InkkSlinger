using System;
using System.Collections.Generic;
using System.Diagnostics;
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

public sealed partial class DropShadowEffect : Effect
{
    private static readonly Dictionary<ShadowTextureCacheKey, Texture2D> ShadowTextures = new();
    private const float MinimumVisibleShadowAlpha = 0.12f;
    private static long _renderElapsedTicks;
    private static long _blurPathElapsedTicks;
    private static long _drawBlurSlicesElapsedTicks;
    private static int _renderCallCount;
    private static int _blurPathCallCount;
    private static int _calendarDayRenderCallCount;
    private static int _calendarDayBlurPathCallCount;

    private Color _color = Color.Black;
    private float _shadowDepth;
    private float _blurRadius;
    private float _opacity;
    private double _direction = 315d;

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

    public double Direction
    {
        get => _direction;
        set
        {
            WritePreamble();
            if (Math.Abs(_direction - value) <= 0.0001d)
            {
                return;
            }

            _direction = value;
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
        _direction = typedSource._direction;
    }

    internal override void Render(UIElement element, SpriteBatch spriteBatch, float elementOpacity)
    {
        var renderStart = Stopwatch.GetTimestamp();
        var slot = element.LayoutSlot;
        _renderCallCount++;
        var isCalendarDayButton = element is CalendarDayButton;
        if (isCalendarDayButton)
        {
            _calendarDayRenderCallCount++;
        }

        try
        {
            if (slot.Width <= 0f || slot.Height <= 0f)
            {
                return;
            }

            var effectiveOpacity = Opacity * elementOpacity;
            if (effectiveOpacity <= 0f || Color.A == 0)
            {
                return;
            }

            var effectiveShadowAlpha = (Color.A / 255f) * effectiveOpacity;
            if (effectiveShadowAlpha <= MinimumVisibleShadowAlpha)
            {
                return;
            }

            var shadowOffset = GetShadowOffset();
            var shadowRect = new LayoutRect(slot.X + shadowOffset.X, slot.Y + shadowOffset.Y, slot.Width, slot.Height);
            var blur = BlurRadius;
            if (blur <= 0.001f)
            {
                UiDrawing.DrawFilledRect(spriteBatch, shadowRect, Color, effectiveOpacity);
                return;
            }

            var blurStart = Stopwatch.GetTimestamp();
            _blurPathCallCount++;
            if (isCalendarDayButton)
            {
                _calendarDayBlurPathCallCount++;
            }

            try
            {
                var blurSize = Math.Clamp((int)MathF.Ceiling(MathF.Min(blur, 32f)), 1, 32);
                var transformedShadowRect = UiDrawing.TransformRectBounds(spriteBatch, shadowRect);
                var transformedExpandedRect = UiDrawing.TransformRectBounds(
                    spriteBatch,
                    new LayoutRect(
                        shadowRect.X - blurSize,
                        shadowRect.Y - blurSize,
                        shadowRect.Width + (blurSize * 2f),
                        shadowRect.Height + (blurSize * 2f)));
                var rasterLayout = RasterizeShadowLayout(transformedShadowRect, transformedExpandedRect);
                var drawSlicesStart = Stopwatch.GetTimestamp();
                DrawBlurSlices(spriteBatch, blurSize, rasterLayout, Color, effectiveOpacity);
                _drawBlurSlicesElapsedTicks += Stopwatch.GetTimestamp() - drawSlicesStart;
            }
            finally
            {
                _blurPathElapsedTicks += Stopwatch.GetTimestamp() - blurStart;
            }
        }
        finally
        {
            _renderElapsedTicks += Stopwatch.GetTimestamp() - renderStart;
        }
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

        var shadowOffset = GetShadowOffset();
        var shadowRect = new LayoutRect(slot.X + shadowOffset.X, slot.Y + shadowOffset.Y, slot.Width, slot.Height);
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
        int blurSize,
        ShadowRasterLayout rasterLayout,
        Color color,
        float opacity)
    {
        var graphicsDevice = spriteBatch.GraphicsDevice;
        var shadowTexture = GetShadowTexture(graphicsDevice, rasterLayout.Center.Width, rasterLayout.Center.Height, blurSize);
        UiDrawing.DrawTexturePixels(spriteBatch, shadowTexture, rasterLayout.Bounds, color: color, opacity: opacity);
    }

    private static ShadowRasterLayout RasterizeShadowLayout(LayoutRect transformedShadowRect, LayoutRect transformedExpandedRect)
    {
        var outerLeft = (int)MathF.Floor(transformedExpandedRect.X);
        var outerTop = (int)MathF.Floor(transformedExpandedRect.Y);
        var outerRight = (int)MathF.Ceiling(transformedExpandedRect.X + transformedExpandedRect.Width);
        var outerBottom = (int)MathF.Ceiling(transformedExpandedRect.Y + transformedExpandedRect.Height);

        var innerLeft = Math.Clamp((int)MathF.Round(transformedShadowRect.X), outerLeft, outerRight);
        var innerTop = Math.Clamp((int)MathF.Round(transformedShadowRect.Y), outerTop, outerBottom);
        var innerRight = Math.Clamp((int)MathF.Round(transformedShadowRect.X + transformedShadowRect.Width), innerLeft, outerRight);
        var innerBottom = Math.Clamp((int)MathF.Round(transformedShadowRect.Y + transformedShadowRect.Height), innerTop, outerBottom);

        return new ShadowRasterLayout(
            new Rectangle(outerLeft, outerTop, Math.Max(0, outerRight - outerLeft), Math.Max(0, outerBottom - outerTop)),
            new Rectangle(innerLeft, innerTop, Math.Max(0, innerRight - innerLeft), Math.Max(0, innerBottom - innerTop)),
            new Rectangle(innerLeft, outerTop, Math.Max(0, innerRight - innerLeft), Math.Max(0, innerTop - outerTop)),
            new Rectangle(innerLeft, innerBottom, Math.Max(0, innerRight - innerLeft), Math.Max(0, outerBottom - innerBottom)),
            new Rectangle(outerLeft, innerTop, Math.Max(0, innerLeft - outerLeft), Math.Max(0, innerBottom - innerTop)),
            new Rectangle(innerRight, innerTop, Math.Max(0, outerRight - innerRight), Math.Max(0, innerBottom - innerTop)),
            new Rectangle(outerLeft, outerTop, Math.Max(0, innerLeft - outerLeft), Math.Max(0, innerTop - outerTop)),
            new Rectangle(innerRight, outerTop, Math.Max(0, outerRight - innerRight), Math.Max(0, innerTop - outerTop)),
            new Rectangle(outerLeft, innerBottom, Math.Max(0, innerLeft - outerLeft), Math.Max(0, outerBottom - innerBottom)),
            new Rectangle(innerRight, innerBottom, Math.Max(0, outerRight - innerRight), Math.Max(0, outerBottom - innerBottom)));
    }

    private static Texture2D GetShadowTexture(GraphicsDevice graphicsDevice, int innerWidth, int innerHeight, int blurSize)
    {
        var key = new ShadowTextureCacheKey(graphicsDevice, innerWidth, innerHeight, blurSize);
        if (ShadowTextures.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var outerWidth = Math.Max(1, innerWidth + (blurSize * 2));
        var outerHeight = Math.Max(1, innerHeight + (blurSize * 2));
        var texture = new Texture2D(graphicsDevice, outerWidth, outerHeight);
        texture.SetData(BuildShadowTexturePixels(innerWidth, innerHeight, blurSize));
        ShadowTextures[key] = texture;
        return texture;
    }

    private static Color[] BuildShadowTexturePixels(int innerWidth, int innerHeight, int blurSize)
    {
        var clampedInnerWidth = Math.Max(0, innerWidth);
        var clampedInnerHeight = Math.Max(0, innerHeight);
        var clampedBlurSize = Math.Max(1, blurSize);
        var outerWidth = Math.Max(1, clampedInnerWidth + (clampedBlurSize * 2));
        var outerHeight = Math.Max(1, clampedInnerHeight + (clampedBlurSize * 2));
        var pixels = new Color[outerWidth * outerHeight];
        for (var y = 0; y < outerHeight; y++)
        {
            for (var x = 0; x < outerWidth; x++)
            {
                var alphaX = ComputeShadowAxisAlpha(x, clampedInnerWidth, clampedBlurSize);
                var alphaY = ComputeShadowAxisAlpha(y, clampedInnerHeight, clampedBlurSize);
                pixels[(y * outerWidth) + x] = Color.White * MathF.Min(alphaX, alphaY);
            }
        }

        return pixels;
    }

    private static float ComputeShadowAxisAlpha(int index, int innerLength, int blurSize)
    {
        if (blurSize <= 1)
        {
            return 1f;
        }

        if (index < blurSize)
        {
            return ComputeEdgeShadowAlpha(index, blurSize, nearOpaqueAtEnd: true);
        }

        var centerEnd = blurSize + innerLength;
        if (index >= centerEnd)
        {
            return ComputeEdgeShadowAlpha(index - centerEnd, blurSize, nearOpaqueAtEnd: false);
        }

        return 1f;
    }

    private static float ComputeEdgeShadowAlpha(int index, int blurSize, bool nearOpaqueAtEnd)
    {
        if (blurSize <= 1)
        {
            return 1f;
        }

        var maxIndex = blurSize - 1;
        var alpha = nearOpaqueAtEnd
            ? index / (float)maxIndex
            : (maxIndex - index) / (float)maxIndex;
        return Math.Clamp(alpha, 0f, 1f);
    }

    private static float ComputeCornerShadowAlpha(
        int x,
        int y,
        int blurSize,
        bool nearRectXAtEnd,
        bool nearRectYAtEnd)
    {
        var alphaX = ComputeEdgeShadowAlpha(x, blurSize, nearRectXAtEnd);
        var alphaY = ComputeEdgeShadowAlpha(y, blurSize, nearRectYAtEnd);
        return MathF.Min(alphaX, alphaY);
    }

    private readonly record struct ShadowTextureCacheKey(GraphicsDevice GraphicsDevice, int InnerWidth, int InnerHeight, int BlurSize);

    private readonly record struct ShadowRasterLayout(
        Rectangle Bounds,
        Rectangle Center,
        Rectangle Top,
        Rectangle Bottom,
        Rectangle Left,
        Rectangle Right,
        Rectangle TopLeft,
        Rectangle TopRight,
        Rectangle BottomLeft,
        Rectangle BottomRight);

    private static LayoutRect Union(LayoutRect left, LayoutRect right)
    {
        var x = MathF.Min(left.X, right.X);
        var y = MathF.Min(left.Y, right.Y);
        var rightEdge = MathF.Max(left.X + left.Width, right.X + right.Width);
        var bottomEdge = MathF.Max(left.Y + left.Height, right.Y + right.Height);
        return new LayoutRect(x, y, MathF.Max(0f, rightEdge - x), MathF.Max(0f, bottomEdge - y));
    }

    private Vector2 GetShadowOffset()
    {
        if (MathF.Abs(ShadowDepth) <= 0.0001f)
        {
            return Vector2.Zero;
        }

        var radians = (float)(Direction * (Math.PI / 180d));
        var offsetX = ShadowDepth * MathF.Cos(radians);
        var offsetY = -ShadowDepth * MathF.Sin(radians);
        return new Vector2(offsetX, offsetY);
    }

}

internal readonly record struct DropShadowEffectTimingSnapshot(
    long RenderElapsedTicks,
    long BlurPathElapsedTicks,
    long DrawBlurSlicesElapsedTicks,
    int RenderCallCount,
    int BlurPathCallCount,
    int CalendarDayRenderCallCount,
    int CalendarDayBlurPathCallCount);

public sealed partial class DropShadowEffect
{
    internal static DropShadowEffectTimingSnapshot GetTimingSnapshotForTests()
    {
        return new DropShadowEffectTimingSnapshot(
            _renderElapsedTicks,
            _blurPathElapsedTicks,
            _drawBlurSlicesElapsedTicks,
            _renderCallCount,
            _blurPathCallCount,
            _calendarDayRenderCallCount,
            _calendarDayBlurPathCallCount);
    }

    internal static void ResetTimingForTests()
    {
        _renderElapsedTicks = 0;
        _blurPathElapsedTicks = 0;
        _drawBlurSlicesElapsedTicks = 0;
        _renderCallCount = 0;
        _blurPathCallCount = 0;
        _calendarDayRenderCallCount = 0;
        _calendarDayBlurPathCallCount = 0;
    }
}
