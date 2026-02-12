using System;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public sealed class ImageSource
{
    private ImageSource(Texture2D? texture, int pixelWidth, int pixelHeight, string? uri)
    {
        Texture = texture;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        Uri = uri;
    }

    public static Func<string, ImageSource?>? UriSourceResolver { get; set; }

    public Texture2D? Texture { get; }

    public int PixelWidth { get; }

    public int PixelHeight { get; }

    public string? Uri { get; }

    public bool HasPixelSize => PixelWidth > 0 && PixelHeight > 0;

    public static ImageSource FromTexture(Texture2D texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        return new ImageSource(texture, texture.Width, texture.Height, null);
    }

    public static ImageSource FromPixels(int pixelWidth, int pixelHeight)
    {
        if (pixelWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelWidth), "Pixel width must be positive.");
        }

        if (pixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelHeight), "Pixel height must be positive.");
        }

        return new ImageSource(null, pixelWidth, pixelHeight, null);
    }

    public static ImageSource FromUri(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            throw new ArgumentException("Image URI cannot be empty.", nameof(uri));
        }

        return new ImageSource(null, 0, 0, uri.Trim());
    }

    public ImageSource? Resolve()
    {
        if (HasPixelSize || string.IsNullOrWhiteSpace(Uri))
        {
            return this;
        }

        return UriSourceResolver?.Invoke(Uri);
    }
}
