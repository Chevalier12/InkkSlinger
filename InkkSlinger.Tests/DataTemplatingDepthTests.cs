using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class DataTemplatingDepthTests
{
    public DataTemplatingDepthTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void ItemTemplateSelector_IsUsed_WhenItemTemplateMissing()
    {
        var control = new TestItemsControl
        {
            ItemTemplateSelector = new TestSelector()
        };
        control.Items.Add("abc");
        control.Items.Add(42);
        control.Measure(new Vector2(300f, 200f));
        control.Arrange(new LayoutRect(0f, 0f, 300f, 200f));

        var containers = control.ExposedContainers;
        Assert.Equal(2, containers.Count);
        Assert.Equal("str:abc", Assert.IsType<Label>(containers[0]).Text);
        Assert.Equal("int:42", Assert.IsType<Label>(containers[1]).Text);
    }

    [Fact]
    public void ItemTemplate_TakesPrecedence_OverSelector()
    {
        var control = new TestItemsControl
        {
            ItemTemplate = new DataTemplate(item => new Label { Text = "fixed" }),
            ItemTemplateSelector = new TestSelector()
        };
        control.Items.Add("abc");
        control.Measure(new Vector2(300f, 200f));
        control.Arrange(new LayoutRect(0f, 0f, 300f, 200f));

        var child = Assert.Single(control.ExposedContainers);
        Assert.Equal("fixed", Assert.IsType<Label>(child).Text);
    }

    [Fact]
    public void ImplicitTemplate_ByDataType_IsApplied()
    {
        var control = new TestItemsControl();
        control.Resources[typeof(string)] = new DataTemplate(item => new Button { Text = $"btn:{item}" });
        control.Items.Add("hello");
        control.Measure(new Vector2(300f, 200f));
        control.Arrange(new LayoutRect(0f, 0f, 300f, 200f));

        var child = Assert.Single(control.ExposedContainers);
        Assert.Equal("btn:hello", Assert.IsType<Button>(child).Text);
    }

    [Fact]
    public void XamlDataTemplate_DataType_ActsAsImplicitKey()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <UserControl.Resources>
                                <DataTemplate DataType="{x:Type x:String}">
                                  <Label Text="FromTemplate" />
                                </DataTemplate>
                              </UserControl.Resources>
                              <ContentControl x:Name="Host" Content="Hello" />
                            </UserControl>
                            """;

        var host = new TemplateHost();
        XamlLoader.LoadIntoFromString(host, xaml, host);

        Assert.NotNull(host.Host);
        host.Host!.Measure(new Vector2(200f, 100f));
        host.Host.Arrange(new LayoutRect(0f, 0f, 200f, 100f));

        var visual = Assert.Single(host.Host.GetVisualChildren());
        Assert.Equal("FromTemplate", Assert.IsType<Label>(visual).Text);
    }

    private sealed class TestItemsControl : ItemsControl
    {
        public IReadOnlyList<UIElement> ExposedContainers => ItemContainers;
    }

    private sealed class TestSelector : DataTemplateSelector
    {
        public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
        {
            if (item is int)
            {
                return new DataTemplate(x => new Label { Text = $"int:{x}" });
            }

            if (item is string)
            {
                return new DataTemplate(x => new Label { Text = $"str:{x}" });
            }

            return new DataTemplate(x => new Label { Text = $"other:{x}" });
        }
    }

    private sealed class TemplateHost : UserControl
    {
        public ContentControl? Host { get; set; }
    }
}
