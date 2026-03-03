using System;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class MultiTriggerTests
{
    [Fact]
    public void MultiTrigger_Runtime_AppliesSettersAndEnterExitActions_OnConditionTransitions()
    {
        var enter = new CountingTriggerAction();
        var exit = new CountingTriggerAction();
        var trigger = new MultiTrigger();
        trigger.Conditions.Add(new Condition
        {
            Property = FrameworkElement.WidthProperty,
            Value = 120f
        });
        trigger.Conditions.Add(new Condition
        {
            Property = FrameworkElement.HeightProperty,
            Value = 40f
        });
        trigger.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(7f)));
        trigger.EnterActions.Add(enter);
        trigger.ExitActions.Add(exit);

        var style = new Style(typeof(Button));
        style.Triggers.Add(trigger);

        var button = new Button();
        button.Style = style;

        Assert.Equal(0, enter.Count);
        Assert.Equal(0, exit.Count);

        button.Width = 120f;
        Assert.Equal(0, enter.Count);
        Assert.Equal(0, exit.Count);

        button.Height = 40f;
        Assert.Equal(1, enter.Count);
        Assert.Equal(0, exit.Count);
        Assert.Equal(new Thickness(7f), button.Margin);
        Assert.Equal(DependencyPropertyValueSource.StyleTrigger, button.GetValueSource(FrameworkElement.MarginProperty));

        button.Height = 24f;
        Assert.Equal(1, enter.Count);
        Assert.Equal(1, exit.Count);
        Assert.NotEqual(new Thickness(7f), button.Margin);
        Assert.NotEqual(DependencyPropertyValueSource.StyleTrigger, button.GetValueSource(FrameworkElement.MarginProperty));
    }

    [Fact]
    public void Xaml_StyleMultiTrigger_WithPropertyConditions_ParsesAndApplies()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="ProbeStyle" TargetType="{x:Type Button}">
      <Style.Triggers>
        <MultiTrigger>
          <Condition Property="Width" Value="240" />
          <Condition Property="Height" Value="80" />
          <MultiTrigger.Setters>
            <Setter Property="Margin" Value="5" />
          </MultiTrigger.Setters>
        </MultiTrigger>
      </Style.Triggers>
    </Style>
  </UserControl.Resources>
  <Grid>
    <Button x:Name="Probe" Style="{StaticResource ProbeStyle}" />
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var probe = Assert.IsType<Button>(root.FindName("Probe"));

        probe.Width = 240f;
        probe.Height = 80f;

        Assert.Equal(new Thickness(5f), probe.Margin);
        Assert.Equal(DependencyPropertyValueSource.StyleTrigger, probe.GetValueSource(FrameworkElement.MarginProperty));
    }

    [Fact]
    public void Xaml_ControlTemplate_MultiTrigger_ParsesIntoTemplateTriggers()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="ProbeStyle" TargetType="{x:Type Button}">
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="{x:Type Button}">
            <Border x:Name="Chrome" Background="{TemplateBinding Background}" />
            <ControlTemplate.Triggers>
              <MultiTrigger>
                <Condition Property="IsEnabled" Value="True" />
                <Condition Property="IsMouseOver" Value="True" />
                <MultiTrigger.Setters>
                  <Setter TargetName="Chrome" Property="Background" Value="#AA5500" />
                </MultiTrigger.Setters>
              </MultiTrigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </UserControl.Resources>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var style = Assert.IsType<Style>(root.Resources["ProbeStyle"]);
        var templateSetter = Assert.Single(style.Setters, s => ReferenceEquals(s.Property, Control.TemplateProperty));
        var template = Assert.IsType<ControlTemplate>(templateSetter.Value);
        var multiTrigger = Assert.IsType<MultiTrigger>(Assert.Single(template.Triggers));
        Assert.Equal(2, multiTrigger.Conditions.Count);
        Assert.All(multiTrigger.Conditions, condition => Assert.NotNull(condition.Property));
    }

    [Fact]
    public void Xaml_MultiTriggerCondition_WithBindingOnly_ThrowsClearError()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="ProbeStyle" TargetType="{x:Type Button}">
      <Style.Triggers>
        <MultiTrigger>
          <Condition Binding="{Binding Path=IsReady}" Value="True" />
        </MultiTrigger>
      </Style.Triggers>
    </Style>
  </UserControl.Resources>
</UserControl>
""";

        var ex = Assert.ThrowsAny<InvalidOperationException>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("MultiTrigger condition requires Property and forbids Binding.", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Xaml_MultiDataTriggerCondition_WithPropertyOnly_ThrowsClearError()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="ProbeStyle" TargetType="{x:Type Button}">
      <Style.Triggers>
        <MultiDataTrigger>
          <Condition Property="IsMouseOver" Value="True" />
        </MultiDataTrigger>
      </Style.Triggers>
    </Style>
  </UserControl.Resources>
</UserControl>
""";

        var ex = Assert.ThrowsAny<InvalidOperationException>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("MultiDataTrigger condition requires Binding and forbids Property.", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Xaml_MultiTriggerCondition_WithPropertyAndBinding_ThrowsClearError()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="ProbeStyle" TargetType="{x:Type Button}">
      <Style.Triggers>
        <MultiTrigger>
          <Condition Property="IsMouseOver" Binding="{Binding Path=IsReady}" Value="True" />
        </MultiTrigger>
      </Style.Triggers>
    </Style>
  </UserControl.Resources>
</UserControl>
""";

        var ex = Assert.ThrowsAny<InvalidOperationException>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("MultiTrigger condition requires Property and forbids Binding.", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Xaml_MultiTrigger_WithNoConditions_ThrowsClearError()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="ProbeStyle" TargetType="{x:Type Button}">
      <Style.Triggers>
        <MultiTrigger>
          <MultiTrigger.Setters>
            <Setter Property="Background" Value="#101010" />
          </MultiTrigger.Setters>
        </MultiTrigger>
      </Style.Triggers>
    </Style>
  </UserControl.Resources>
</UserControl>
""";

        var ex = Assert.ThrowsAny<InvalidOperationException>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("MultiTrigger requires at least one Condition.", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Xaml_MultiTriggerConditions_WithNonConditionChild_Throws()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="ProbeStyle" TargetType="{x:Type Button}">
      <Style.Triggers>
        <MultiTrigger>
          <MultiTrigger.Conditions>
            <Setter Property="Background" Value="#202020" />
          </MultiTrigger.Conditions>
        </MultiTrigger>
      </Style.Triggers>
    </Style>
  </UserControl.Resources>
</UserControl>
""";

        var ex = Assert.ThrowsAny<InvalidOperationException>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("MultiTrigger.Conditions can only contain Condition elements.", ex.Message, StringComparison.Ordinal);
    }

    private sealed class CountingTriggerAction : TriggerAction
    {
        public int Count { get; private set; }

        public override void Invoke(DependencyObject target)
        {
            Count++;
        }
    }
}
