using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class Viewbox : ContentControl
{
    public static readonly DependencyProperty StretchProperty =
        DependencyProperty.Register(
            nameof(Stretch),
            typeof(Stretch),
            typeof(Viewbox),
            new FrameworkPropertyMetadata(
                Stretch.Uniform,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty StretchDirectionProperty =
        DependencyProperty.Register(
            nameof(StretchDirection),
            typeof(StretchDirection),
            typeof(Viewbox),
            new FrameworkPropertyMetadata(
                StretchDirection.Both,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    private Vector2 _unscaledChildSize;
    private Vector2 _arrangedScale = Vector2.One;
    private Vector2 _arrangedOffset = Vector2.Zero;

    public Stretch Stretch
    {
        get => GetValue<Stretch>(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    public StretchDirection StretchDirection
    {
        get => GetValue<StretchDirection>(StretchDirectionProperty);
        set => SetValue(StretchDirectionProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = Vector2.Zero;

        if (ContentElement is not FrameworkElement child)
        {
            _unscaledChildSize = Vector2.Zero;
            return desired;
        }

        child.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
        _unscaledChildSize = child.DesiredSize;

        var scale = ComputeScaleFactor(availableSize, _unscaledChildSize, Stretch, StretchDirection);
        var scaledSize = new Vector2(_unscaledChildSize.X * scale.X, _unscaledChildSize.Y * scale.Y);
        desired.X = MathF.Max(desired.X, scaledSize.X);
        desired.Y = MathF.Max(desired.Y, scaledSize.Y);
        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (ContentElement is not FrameworkElement child)
        {
            _arrangedScale = Vector2.One;
            _arrangedOffset = Vector2.Zero;
            return finalSize;
        }

        var sourceSize = _unscaledChildSize;
        if (sourceSize.X < 0f || sourceSize.Y < 0f)
        {
            sourceSize = Vector2.Zero;
        }

        var scale = ComputeScaleFactor(finalSize, sourceSize, Stretch, StretchDirection);
        var arrangedChildSize = new Vector2(sourceSize.X * scale.X, sourceSize.Y * scale.Y);
        _arrangedScale = scale;
        _arrangedOffset = new Vector2(
            (finalSize.X - arrangedChildSize.X) / 2f,
            (finalSize.Y - arrangedChildSize.Y) / 2f);

        child.Arrange(new LayoutRect(LayoutSlot.X, LayoutSlot.Y, sourceSize.X, sourceSize.Y));

        return finalSize;
    }

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        clipRect = LayoutSlot;
        return true;
    }

    protected override bool TryGetLocalRenderTransform(out Matrix transform, out Matrix inverseTransform)
    {
        transform = Matrix.Identity;
        inverseTransform = Matrix.Identity;

        if (ContentElement is not FrameworkElement)
        {
            return false;
        }

        if (_arrangedScale == Vector2.One && _arrangedOffset == Vector2.Zero)
        {
            return false;
        }

        var originX = LayoutSlot.X;
        var originY = LayoutSlot.Y;
        var translateX = originX + _arrangedOffset.X;
        var translateY = originY + _arrangedOffset.Y;

        transform =
            Matrix.CreateTranslation(-originX, -originY, 0f) *
            Matrix.CreateScale(_arrangedScale.X, _arrangedScale.Y, 1f) *
            Matrix.CreateTranslation(translateX, translateY, 0f);

        var inverseScaleX = _arrangedScale.X == 0f ? 0f : 1f / _arrangedScale.X;
        var inverseScaleY = _arrangedScale.Y == 0f ? 0f : 1f / _arrangedScale.Y;
        inverseTransform =
            Matrix.CreateTranslation(-translateX, -translateY, 0f) *
            Matrix.CreateScale(inverseScaleX, inverseScaleY, 1f) *
            Matrix.CreateTranslation(originX, originY, 0f);

        return true;
    }

    private static Vector2 ComputeScaleFactor(
        Vector2 availableSize,
        Vector2 contentSize,
        Stretch stretch,
        StretchDirection stretchDirection)
    {
        var scaleX = 1f;
        var scaleY = 1f;

        var hasFiniteWidth = !float.IsPositiveInfinity(availableSize.X);
        var hasFiniteHeight = !float.IsPositiveInfinity(availableSize.Y);
        var canComputeX = contentSize.X > 0f;
        var canComputeY = contentSize.Y > 0f;

        if (stretch != Stretch.None && (hasFiniteWidth || hasFiniteHeight))
        {
            if (canComputeX && hasFiniteWidth)
            {
                scaleX = availableSize.X / contentSize.X;
            }

            if (canComputeY && hasFiniteHeight)
            {
                scaleY = availableSize.Y / contentSize.Y;
            }

            if (!hasFiniteWidth)
            {
                scaleX = scaleY;
            }
            else if (!hasFiniteHeight)
            {
                scaleY = scaleX;
            }

            if (stretch == Stretch.Uniform)
            {
                var uniform = MathF.Min(scaleX, scaleY);
                scaleX = uniform;
                scaleY = uniform;
            }
            else if (stretch == Stretch.UniformToFill)
            {
                var uniformToFill = MathF.Max(scaleX, scaleY);
                scaleX = uniformToFill;
                scaleY = uniformToFill;
            }
        }

        if (stretchDirection == StretchDirection.UpOnly)
        {
            scaleX = MathF.Max(1f, scaleX);
            scaleY = MathF.Max(1f, scaleY);
        }
        else if (stretchDirection == StretchDirection.DownOnly)
        {
            scaleX = MathF.Min(1f, scaleX);
            scaleY = MathF.Min(1f, scaleY);
        }

        if (float.IsNaN(scaleX) || float.IsInfinity(scaleX))
        {
            scaleX = 1f;
        }

        if (float.IsNaN(scaleY) || float.IsInfinity(scaleY))
        {
            scaleY = 1f;
        }

        return new Vector2(scaleX, scaleY);
    }
}
