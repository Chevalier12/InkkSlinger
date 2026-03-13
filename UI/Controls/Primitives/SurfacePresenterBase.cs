using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public abstract class SurfacePresenterBase : FrameworkElement
{
    public static readonly DependencyProperty StretchProperty =
        DependencyProperty.Register(
            nameof(Stretch),
            typeof(Stretch),
            typeof(SurfacePresenterBase),
            new FrameworkPropertyMetadata(
                Stretch.Uniform,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StretchDirectionProperty =
        DependencyProperty.Register(
            nameof(StretchDirection),
            typeof(StretchDirection),
            typeof(SurfacePresenterBase),
            new FrameworkPropertyMetadata(
                StretchDirection.Both,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    private LayoutRect _renderRect;
    private ImageSource? _cachedResolvedSurface;
    private ImageSource? _cachedSurfaceRequest;

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

    protected abstract ImageSource? RequestedSurface { get; }

    protected void InvalidateResolvedSurfaceCache()
    {
        _cachedSurfaceRequest = null;
        _cachedResolvedSurface = null;
    }

    protected ImageSource? ResolveRequestedSurface()
    {
        var requested = RequestedSurface;
        if (requested == null)
        {
            return null;
        }

        if (requested.HasPixelSize)
        {
            return requested;
        }

        if (ReferenceEquals(_cachedSurfaceRequest, requested) && _cachedResolvedSurface != null)
        {
            return _cachedResolvedSurface;
        }

        var resolved = requested.Resolve();
        if (resolved != null && resolved.HasPixelSize)
        {
            _cachedSurfaceRequest = requested;
            _cachedResolvedSurface = resolved;
            return resolved;
        }

        return null;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        if (!TryGetNaturalSize(out var naturalSize))
        {
            return Vector2.Zero;
        }

        var scale = ComputeScaleFactor(availableSize, naturalSize, Stretch, StretchDirection);
        return new Vector2(
            naturalSize.X * scale.X,
            naturalSize.Y * scale.Y);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        _renderRect = new LayoutRect(LayoutSlot.X, LayoutSlot.Y, 0f, 0f);
        if (!TryGetNaturalSize(out var naturalSize))
        {
            return finalSize;
        }

        var scale = ComputeScaleFactor(finalSize, naturalSize, Stretch, StretchDirection);
        var renderWidth = naturalSize.X * scale.X;
        var renderHeight = naturalSize.Y * scale.Y;
        var offsetX = (finalSize.X - renderWidth) / 2f;
        var offsetY = (finalSize.Y - renderHeight) / 2f;

        _renderRect = new LayoutRect(
            LayoutSlot.X + offsetX,
            LayoutSlot.Y + offsetY,
            renderWidth,
            renderHeight);

        return finalSize;
    }

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        clipRect = LayoutSlot;
        return true;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        base.OnRender(spriteBatch);
        if (!TryGetRenderableTexture(out var texture))
        {
            return;
        }

        UiDrawing.DrawTexture(spriteBatch, texture, _renderRect, opacity: Opacity);
    }

    private bool TryGetNaturalSize(out Vector2 naturalSize)
    {
        naturalSize = Vector2.Zero;
        var resolvedSurface = ResolveRequestedSurface();
        if (resolvedSurface == null || !resolvedSurface.HasPixelSize)
        {
            return false;
        }

        naturalSize = new Vector2(resolvedSurface.PixelWidth, resolvedSurface.PixelHeight);
        return true;
    }

    private bool TryGetRenderableTexture(out Texture2D texture)
    {
        texture = null!;
        var resolvedSurface = ResolveRequestedSurface();
        if (resolvedSurface?.Texture == null)
        {
            return false;
        }

        texture = resolvedSurface.Texture;
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
