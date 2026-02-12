using System.Reflection;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class MainMenuViewLayoutTests
{
    public MainMenuViewLayoutTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void MainMenuView_ArrangesTextBoxDemo_WithNonZeroSize_AndInitialDocument()
    {
        var view = new MainMenuView();
        view.Measure(new Vector2(1600f, 900f));
        view.Arrange(new LayoutRect(0f, 0f, 1600f, 900f));

        var textBox = GetPrivateProperty<TextBox>(view, "DemoTextBox");

        Assert.NotNull(textBox);
        Assert.True(textBox!.LayoutSlot.Width > 200f);
        Assert.True(textBox.LayoutSlot.Height > 200f);
        Assert.True(textBox.Text.Length > 0);
    }

    private static T? GetPrivateProperty<T>(object instance, string propertyName)
        where T : class
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);
        return property?.GetValue(instance) as T;
    }
}
