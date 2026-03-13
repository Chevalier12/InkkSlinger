using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class Image : SurfacePresenterBase
{
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(
            nameof(Source),
            typeof(ImageSource),
            typeof(Image),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Image image)
                    {
                        image.InvalidateResolvedSurfaceCache();
                    }
                }));

    public ImageSource? Source
    {
        get => GetValue<ImageSource>(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    protected override ImageSource? RequestedSurface => Source;
}
