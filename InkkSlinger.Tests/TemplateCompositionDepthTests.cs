using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class TemplateCompositionDepthTests
{
    public TemplateCompositionDepthTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void TemplateBinding_UsesFallbackWhenSourceIsDefault()
    {
        var control = new TestTemplateControl();
        control.Template = new ControlTemplate(_ =>
        {
            var button = new Button { Name = "PART_Content" };
            return button;
        }).BindTemplate("PART_Content", UIElement.OpacityProperty, FrameworkElement.WidthProperty, fallbackValue: 0.25f);

        control.ApplyTemplate();
        var part = Assert.IsType<Button>(control.Part);
        Assert.Equal(0.25f, part.Opacity, 3);
    }

    [Fact]
    public void TemplateBinding_UsesTargetNullValue()
    {
        var control = new TestTemplateControl
        {
            CustomValue = null
        };

        control.Template = new ControlTemplate(_ =>
        {
            var button = new Button { Name = "PART_Content" };
            return button;
        }).BindTemplate("PART_Content", Control.CommandParameterProperty, TestTemplateControl.CustomValueProperty, fallbackValue: null, targetNullValue: "NULL");

        control.ApplyTemplate();
        var part = Assert.IsType<Button>(control.Part);
        Assert.Equal("NULL", part.CommandParameter);
    }

    [Fact]
    public void ControlTemplate_Trigger_SetsTargetPartValue()
    {
        var control = new TestTemplateControl();
        var template = new ControlTemplate(_ =>
        {
            var button = new Button { Name = "PART_Content" };
            return button;
        });
        var trigger = new Trigger(UIElement.IsEnabledProperty, false);
        trigger.Setters.Add(new Setter("PART_Content", Button.BackgroundProperty, new Color(12, 34, 56)));
        template.Triggers.Add(trigger);
        control.Template = template;

        control.ApplyTemplate();
        var part = Assert.IsType<Button>(control.Part);
        Assert.NotEqual(new Color(12, 34, 56), part.Background);

        control.IsEnabled = false;
        Assert.Equal(new Color(12, 34, 56), part.Background);
        Assert.Equal(DependencyPropertyValueSource.TemplateTrigger, part.GetValueSource(Button.BackgroundProperty));

        control.IsEnabled = true;
        Assert.NotEqual(DependencyPropertyValueSource.TemplateTrigger, part.GetValueSource(Button.BackgroundProperty));
    }

    [Fact]
    public void TemplatePartAttribute_ThrowsWhenRequiredPartMissing()
    {
        var control = new RequiredPartControl();
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            control.Template = new ControlTemplate(_ => new Grid());
        });
        Assert.Contains("PART_Content", ex.Message);
    }

    [Fact]
    public void XamlLoader_ParsesControlTemplate_AndTemplateTriggers()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <UserControl.Resources>
                                <ControlTemplate x:Key="TemplateA" TargetType="{x:Type Button}">
                                  <Button x:Name="PART_Content" Text="Hello" />
                                  <ControlTemplate.Triggers>
                                    <Trigger Property="IsEnabled" Value="False">
                                      <Setter TargetName="PART_Content" Property="Foreground" Value="#112233" />
                                    </Trigger>
                                  </ControlTemplate.Triggers>
                                </ControlTemplate>
                              </UserControl.Resources>
                              <Button x:Name="HostButton" Template="{StaticResource TemplateA}" />
                            </UserControl>
                            """;

        var host = new TemplateXamlHost();
        XamlLoader.LoadIntoFromString(host, xaml, host);
        Assert.NotNull(host.HostButton);

        host.HostButton!.ApplyTemplate();
        var part = Assert.IsType<Button>(Assert.Single(host.HostButton.GetVisualChildren()));

        host.HostButton.IsEnabled = false;
        Assert.Equal(new Color(0x11, 0x22, 0x33), part.Foreground);
    }

    private sealed class TemplateXamlHost : UserControl
    {
        public Button? HostButton { get; set; }
    }

    private class TestTemplateControl : Control
    {
        public static readonly DependencyProperty CustomValueProperty =
            DependencyProperty.Register(
                nameof(CustomValue),
                typeof(object),
                typeof(TestTemplateControl),
                new FrameworkPropertyMetadata(null));

        public UIElement? Part { get; private set; }

        public object? CustomValue
        {
            get => GetValue(CustomValueProperty);
            set => SetValue(CustomValueProperty, value);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            Part = GetTemplateChild("PART_Content");
        }
    }

    [TemplatePart("PART_Content", typeof(Button))]
    private sealed class RequiredPartControl : Control
    {
    }
}
