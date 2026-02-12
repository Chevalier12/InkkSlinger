using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class SeparatorTests
{
    public SeparatorTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void HorizontalSeparator_MeasuresUsingThickness()
    {
        var separator = new Separator
        {
            Thickness = 3f,
            Orientation = Orientation.Horizontal
        };

        separator.Measure(new Vector2(200f, 50f));
        separator.Arrange(new LayoutRect(0f, 0f, 200f, 50f));

        Assert.Equal(3f, separator.DesiredSize.Y, 3);
        Assert.False(separator.IsHitTestVisible);
        Assert.False(separator.Focusable);
    }

    [Fact]
    public void VerticalSeparator_MeasuresUsingThickness()
    {
        var separator = new Separator
        {
            Thickness = 4f,
            Orientation = Orientation.Vertical
        };

        separator.Measure(new Vector2(80f, 200f));
        separator.Arrange(new LayoutRect(0f, 0f, 80f, 200f));

        Assert.Equal(4f, separator.DesiredSize.X, 3);
    }

    [Fact]
    public void XamlLoader_ParsesSeparatorProperties()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <StackPanel>
                                <Separator Orientation="Vertical" Thickness="5" Foreground="#4488CC" Height="60" />
                              </StackPanel>
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var panel = Assert.IsType<StackPanel>(view.Content);
        var separator = Assert.IsType<Separator>(panel.Children[0]);
        Assert.Equal(Orientation.Vertical, separator.Orientation);
        Assert.Equal(5f, separator.Thickness, 3);
        Assert.Equal(new Color(0x44, 0x88, 0xCC), separator.Foreground);
        Assert.Equal(60f, separator.Height, 3);
    }
}
