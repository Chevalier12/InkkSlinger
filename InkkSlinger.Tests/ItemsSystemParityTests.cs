using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class ItemsSystemParityTests
{
    public ItemsSystemParityTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void ContentPresenter_UsesTemplatedParentContentByDefault()
    {
        var owner = new ContentControl
        {
            Content = "Hello presenter"
        };
        var presenter = new ContentPresenter();
        owner.Template = new ControlTemplate(_ =>
        {
            var panel = new Grid();
            panel.AddChild(presenter);
            return panel;
        });

        owner.Measure(new Vector2(400f, 200f));
        owner.Arrange(new LayoutRect(0f, 0f, 400f, 200f));

        var child = Assert.Single(presenter.GetVisualChildren());
        var label = Assert.IsType<Label>(child);
        Assert.Equal("Hello presenter", label.Text);
    }

    [Fact]
    public void ItemsPresenter_LaysOutItemsFromOwner()
    {
        var itemsControl = new ItemsControl();
        itemsControl.Items.Add("A");
        itemsControl.Items.Add("B");
        itemsControl.Items.Add("C");

        var presenter = new ItemsPresenter();
        itemsControl.Template = new ControlTemplate(_ =>
        {
            var panel = new Grid();
            panel.AddChild(presenter);
            return panel;
        });

        itemsControl.Measure(new Vector2(300f, 200f));
        itemsControl.Arrange(new LayoutRect(0f, 0f, 300f, 200f));

        var children = presenter.GetVisualChildren();
        var count = 0;
        foreach (var child in children)
        {
            Assert.IsType<Label>(child);
            count++;
        }

        Assert.Equal(3, count);
    }

    [Fact]
    public void HeaderedItemsControl_ParsesFromXaml()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <HeaderedItemsControl Header="Inventory">
                                <Label Text="Potion" />
                                <Label Text="Elixir" />
                              </HeaderedItemsControl>
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var control = Assert.IsType<HeaderedItemsControl>(view.Content);
        Assert.Equal("Inventory", control.Header);
        Assert.Equal(2, control.Items.Count);
    }

    [Fact]
    public void HeaderedContentControl_ParsesFromXaml()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <HeaderedContentControl Header="Settings" Content="Body text" />
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var control = Assert.IsType<HeaderedContentControl>(view.Content);
        Assert.Equal("Settings", control.Header);
        Assert.Equal("Body text", control.Content);
    }
}
