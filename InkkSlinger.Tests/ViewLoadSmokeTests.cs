using Xunit;

namespace InkkSlinger.Tests;

public class ViewLoadSmokeTests
{
    public ViewLoadSmokeTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void MainMenuView_Loads_FromXml_WithoutXamlExceptions()
    {
        var view = new MainMenuView();
        Assert.NotNull(view);
        Assert.NotNull(view.Content);
    }
}
