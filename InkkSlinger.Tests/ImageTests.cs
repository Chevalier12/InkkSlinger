using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class ImageTests
{
    public ImageTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
        ImageSource.UriSourceResolver = null;
    }

    [Fact]
    public void Measure_Uniform_ScalesSource_ToFitAvailableSize()
    {
        var image = new Image
        {
            Source = ImageSource.FromPixels(100, 50)
        };

        image.Measure(new Vector2(80f, 80f));

        Assert.Equal(80f, image.DesiredSize.X, 3);
        Assert.Equal(40f, image.DesiredSize.Y, 3);
    }

    [Fact]
    public void Measure_Fill_UsesAvailableSize()
    {
        var image = new Image
        {
            Source = ImageSource.FromPixels(100, 50),
            Stretch = Stretch.Fill
        };

        image.Measure(new Vector2(80f, 120f));

        Assert.Equal(80f, image.DesiredSize.X, 3);
        Assert.Equal(120f, image.DesiredSize.Y, 3);
    }

    [Fact]
    public void Measure_DownOnly_DoesNotUpscale()
    {
        var image = new Image
        {
            Source = ImageSource.FromPixels(100, 50),
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly
        };

        image.Measure(new Vector2(200f, 200f));

        Assert.Equal(100f, image.DesiredSize.X, 3);
        Assert.Equal(50f, image.DesiredSize.Y, 3);
    }

    [Fact]
    public void UriSource_ResolvesThroughResolver_ForLayout()
    {
        ImageSource.UriSourceResolver = uri =>
            uri == "asset://logo"
                ? ImageSource.FromPixels(64, 32)
                : null;

        var image = new Image
        {
            Source = ImageSource.FromUri("asset://logo")
        };

        image.Measure(new Vector2(100f, 100f));

        Assert.Equal(100f, image.DesiredSize.X, 3);
        Assert.Equal(50f, image.DesiredSize.Y, 3);
    }

    [Fact]
    public void XamlLoader_ParsesImageProperties_AndSourceUri()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Image Source="asset://ship"
                                     Stretch="UniformToFill"
                                     StretchDirection="UpOnly" />
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var image = Assert.IsType<Image>(view.Content);
        Assert.Equal(Stretch.UniformToFill, image.Stretch);
        Assert.Equal(StretchDirection.UpOnly, image.StretchDirection);
        Assert.Equal("asset://ship", image.Source?.Uri);
    }
}
