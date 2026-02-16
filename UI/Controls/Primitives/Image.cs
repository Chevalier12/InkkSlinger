using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class Image : FrameworkElement
{
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(
            nameof(Source),
            typeof(ImageSource),
            typeof(Image),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (d, _) => ((Image)d).OnSourceChanged()));

    public static readonly DependencyProperty StretchProperty =
        DependencyProperty.Register(
            nameof(Stretch),
            typeof(Stretch),
            typeof(Image),
            new FrameworkPropertyMetadata(
                Stretch.Uniform,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StretchDirectionProperty =
        DependencyProperty.Register(
            nameof(StretchDirection),
            typeof(StretchDirection),
            typeof(Image),
            new FrameworkPropertyMetadata(
                StretchDirection.Both,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    private LayoutRect _renderRect;
    private ImageSource? _cachedResolvedSource;
    private ImageSource? _cachedSourceRequest;

    public ImageSource? Source
    {
        get => GetValue<ImageSource>(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

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
        var resolvedSource = ResolveSource();
        if (resolvedSource == null || !resolvedSource.HasPixelSize)
        {
            return false;
        }

        naturalSize = new Vector2(resolvedSource.PixelWidth, resolvedSource.PixelHeight);
        return true;
    }

    private bool TryGetRenderableTexture(out Texture2D texture)
    {
        texture = null!;
        var resolvedSource = ResolveSource();
        if (resolvedSource?.Texture == null)
        {
            return false;
        }

        texture = resolvedSource.Texture;
        return true;
    }

    private ImageSource? ResolveSource()
    {
        var requested = Source;
        if (requested == null)
        {
            return null;
        }

        if (requested.HasPixelSize)
        {
            return requested;
        }

        if (ReferenceEquals(_cachedSourceRequest, requested) && _cachedResolvedSource != null)
        {
            return _cachedResolvedSource;
        }

        var resolved = requested.Resolve();
        if (resolved != null && resolved.HasPixelSize)
        {
            _cachedSourceRequest = requested;
            _cachedResolvedSource = resolved;
            return resolved;
        }

        return null;
    }

    private void OnSourceChanged()
    {
        _cachedSourceRequest = null;
        _cachedResolvedSource = null;
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
                var uniform = System.MathF.Min(scaleX, scaleY);
                scaleX = uniform;
                scaleY = uniform;
            }
            else if (stretch == Stretch.UniformToFill)
            {
                var uniformToFill = System.MathF.Max(scaleX, scaleY);
                scaleX = uniformToFill;
                scaleY = uniformToFill;
            }
        }

        if (stretchDirection == StretchDirection.UpOnly)
        {
            scaleX = System.MathF.Max(1f, scaleX);
            scaleY = System.MathF.Max(1f, scaleY);
        }
        else if (stretchDirection == StretchDirection.DownOnly)
        {
            scaleX = System.MathF.Min(1f, scaleX);
            scaleY = System.MathF.Min(1f, scaleY);
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
