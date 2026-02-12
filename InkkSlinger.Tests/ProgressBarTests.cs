using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class ProgressBarTests
{
    public ProgressBarTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void HorizontalProgressBar_MeasuresWithDefaultTrackSize()
    {
        var progressBar = new ProgressBar
        {
            Orientation = Orientation.Horizontal
        };

        progressBar.Measure(new Vector2(300f, 80f));
        progressBar.Arrange(new LayoutRect(0f, 0f, 300f, 80f));

        Assert.True(progressBar.DesiredSize.X >= 120f);
        Assert.True(progressBar.DesiredSize.Y >= 18f);
        Assert.False(progressBar.Focusable);
        Assert.False(progressBar.IsHitTestVisible);
    }

    [Fact]
    public void VerticalProgressBar_MeasuresWithDefaultTrackSize()
    {
        var progressBar = new ProgressBar
        {
            Orientation = Orientation.Vertical
        };

        progressBar.Measure(new Vector2(80f, 300f));
        progressBar.Arrange(new LayoutRect(0f, 0f, 80f, 300f));

        Assert.True(progressBar.DesiredSize.X >= 18f);
        Assert.True(progressBar.DesiredSize.Y >= 120f);
    }

    [Fact]
    public void RangeAndValue_AreCoercedWhenMinimumMaximumChange()
    {
        var progressBar = new ProgressBar
        {
            Minimum = 0f,
            Maximum = 100f,
            Value = 80f
        };

        progressBar.Maximum = 50f;
        Assert.Equal(50f, progressBar.Value, 3);

        progressBar.Minimum = 60f;
        Assert.Equal(50f, progressBar.Minimum, 3);
        Assert.Equal(60f, progressBar.Maximum, 3);
        Assert.Equal(50f, progressBar.Value, 3);
    }

    [Fact]
    public void XamlLoader_ParsesProgressBarProperties()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <ProgressBar Orientation="Vertical"
                                           Minimum="10"
                                           Maximum="40"
                                           Value="24"
                                           IsIndeterminate="true"
                                           BorderThickness="2"
                                           Foreground="#4E9BD8" />
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var progressBar = Assert.IsType<ProgressBar>(view.Content);
        Assert.Equal(Orientation.Vertical, progressBar.Orientation);
        Assert.Equal(10f, progressBar.Minimum, 3);
        Assert.Equal(40f, progressBar.Maximum, 3);
        Assert.Equal(24f, progressBar.Value, 3);
        Assert.True(progressBar.IsIndeterminate);
        Assert.Equal(2f, progressBar.BorderThickness, 3);
        Assert.Equal(new Color(0x4E, 0x9B, 0xD8), progressBar.Foreground);
    }
}
