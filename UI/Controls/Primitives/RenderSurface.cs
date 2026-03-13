using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class RenderSurface : SurfacePresenterBase
{
    public static readonly DependencyProperty SurfaceProperty =
        DependencyProperty.Register(
            nameof(Surface),
            typeof(ImageSource),
            typeof(RenderSurface),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is RenderSurface renderSurface)
                    {
                        renderSurface.InvalidateResolvedSurfaceCache();
                    }
                }));

    public ImageSource? Surface
    {
        get => GetValue<ImageSource>(SurfaceProperty);
        set => SetValue(SurfaceProperty, value);
    }

    public void Present(ImageSource? surface)
    {
        if (ReferenceEquals(Surface, surface))
        {
            if (surface != null)
            {
                InvalidateVisual();
            }

            return;
        }

        Surface = surface;
    }

    public void Present(Texture2D surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var currentSurface = ResolveRequestedSurface();
        if (currentSurface?.Texture == surface &&
            currentSurface.PixelWidth == surface.Width &&
            currentSurface.PixelHeight == surface.Height)
        {
            InvalidateVisual();
            return;
        }

        Surface = ImageSource.FromTexture(surface);
    }

    public void RefreshSurface()
    {
        if (ResolveRequestedSurface() != null)
        {
            InvalidateVisual();
        }
    }

    public void ClearSurface()
    {
        Surface = null;
    }

    protected override ImageSource? RequestedSurface => Surface;
}
