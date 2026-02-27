using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public abstract class Effect
{
    internal event Action? Changed;

    protected void RaiseChanged()
    {
        Changed?.Invoke();
    }

    internal abstract void Render(UIElement element, SpriteBatch spriteBatch, float elementOpacity);
}

public sealed class DropShadowEffect : Effect
{
    private Color _color = Color.Black;
    private float _shadowDepth;
    private float _blurRadius;
    private float _opacity;

    public Color Color
    {
        get => _color;
        set
        {
            if (_color == value)
            {
                return;
            }

            _color = value;
            RaiseChanged();
        }
    }

    public float ShadowDepth
    {
        get => _shadowDepth;
        set
        {
            if (MathF.Abs(_shadowDepth - value) <= 0.0001f)
            {
                return;
            }

            _shadowDepth = value;
            RaiseChanged();
        }
    }

    public float BlurRadius
    {
        get => _blurRadius;
        set
        {
            var clamped = value < 0f ? 0f : value;
            if (MathF.Abs(_blurRadius - clamped) <= 0.0001f)
            {
                return;
            }

            _blurRadius = clamped;
            RaiseChanged();
        }
    }

    public float Opacity
    {
        get => _opacity;
        set
        {
            var clamped = Math.Clamp(value, 0f, 1f);
            if (MathF.Abs(_opacity - clamped) <= 0.0001f)
            {
                return;
            }

            _opacity = clamped;
            RaiseChanged();
        }
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

        var maxInflation = MathF.Min(blur, 32f);
        var steps = Math.Clamp((int)MathF.Ceiling(maxInflation), 1, 32);
        var stepOpacity = effectiveOpacity / (steps + 1f);
        for (var i = steps; i >= 1; i--)
        {
            var inflation = i;
            var ringRect = new LayoutRect(
                shadowRect.X - inflation,
                shadowRect.Y - inflation,
                shadowRect.Width + (inflation * 2f),
                shadowRect.Height + (inflation * 2f));
            UiDrawing.DrawFilledRect(spriteBatch, ringRect, Color, stepOpacity);
        }

        UiDrawing.DrawFilledRect(spriteBatch, shadowRect, Color, effectiveOpacity);
    }
}
