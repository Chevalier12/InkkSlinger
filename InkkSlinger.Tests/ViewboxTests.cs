using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class ViewboxTests
{
    public ViewboxTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void Measure_Uniform_ScalesDown_ToFitAvailableSize()
    {
        var viewbox = new Viewbox
        {
            Content = new FixedSizeElement(100f, 50f)
        };

        viewbox.Measure(new Vector2(80f, 80f));

        Assert.Equal(80f, viewbox.DesiredSize.X, 3);
        Assert.Equal(40f, viewbox.DesiredSize.Y, 3);
    }

    [Fact]
    public void Arrange_Uniform_CentersScaledChild()
    {
        var child = new FixedSizeElement(100f, 50f);
        var viewbox = new Viewbox
        {
            Content = child
        };

        viewbox.Measure(new Vector2(200f, 200f));
        viewbox.Arrange(new LayoutRect(10f, 20f, 200f, 200f));

        Assert.Equal(10f, child.LayoutSlot.X, 3);
        Assert.Equal(20f, child.LayoutSlot.Y, 3);
        Assert.Equal(100f, child.LayoutSlot.Width, 3);
        Assert.Equal(50f, child.LayoutSlot.Height, 3);
    }

    [Fact]
    public void HitTest_UsesRenderTransform_ForScaledSubtree()
    {
        var root = new Panel();
        var viewbox = new Viewbox
        {
            Stretch = Stretch.Uniform,
            Content = new Button
            {
                Width = 100f,
                Height = 50f
            }
        };

        root.AddChild(viewbox);
        root.Measure(new Vector2(300f, 300f));
        root.Arrange(new LayoutRect(0f, 0f, 300f, 300f));

        var insideScaledChild = new Vector2(290f, 190f);
        var hit = VisualTreeHelper.HitTest(root, insideScaledChild);
        Assert.IsType<Button>(hit);
    }

    [Fact]
    public void Measure_Fill_UsesAvailableSize()
    {
        var viewbox = new Viewbox
        {
            Stretch = Stretch.Fill,
            Content = new FixedSizeElement(100f, 50f)
        };

        viewbox.Measure(new Vector2(80f, 120f));

        Assert.Equal(80f, viewbox.DesiredSize.X, 3);
        Assert.Equal(120f, viewbox.DesiredSize.Y, 3);
    }

    [Fact]
    public void Measure_DownOnly_DoesNotUpscale()
    {
        var viewbox = new Viewbox
        {
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            Content = new FixedSizeElement(100f, 50f)
        };

        viewbox.Measure(new Vector2(200f, 200f));

        Assert.Equal(100f, viewbox.DesiredSize.X, 3);
        Assert.Equal(50f, viewbox.DesiredSize.Y, 3);
    }

    [Fact]
    public void Measure_UpOnly_DoesNotDownscale()
    {
        var viewbox = new Viewbox
        {
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.UpOnly,
            Content = new FixedSizeElement(100f, 50f)
        };

        viewbox.Measure(new Vector2(50f, 50f));

        Assert.Equal(100f, viewbox.DesiredSize.X, 3);
        Assert.Equal(50f, viewbox.DesiredSize.Y, 3);
    }

    [Fact]
    public void XamlLoader_ParsesViewbox_StretchProperties()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Viewbox Stretch="UniformToFill" StretchDirection="DownOnly">
                                <Label Text="Scaled" />
                              </Viewbox>
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var viewbox = Assert.IsType<Viewbox>(view.Content);
        Assert.Equal(Stretch.UniformToFill, viewbox.Stretch);
        Assert.Equal(StretchDirection.DownOnly, viewbox.StretchDirection);
        Assert.IsType<Label>(viewbox.Content);
    }

    private sealed class FixedSizeElement : FrameworkElement
    {
        private readonly Vector2 _size;

        public FixedSizeElement(float width, float height)
        {
            _size = new Vector2(width, height);
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            return _size;
        }
    }
}
