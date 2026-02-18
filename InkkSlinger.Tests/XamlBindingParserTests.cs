using System;
using Xunit;

namespace InkkSlinger.Tests;

public class XamlBindingParserTests
{
    [Fact]
    public void BindingMarkup_WithConverterStaticResource_ParsesAndApplies()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <IdentityValueConverter x:Key="IdentityConverter" />
  </UserControl.Resources>
  <Grid>
    <TextBox x:Name="Input" Text="alpha" />
    <TextBlock x:Name="Output" Text="{Binding Path=Text, ElementName=Input, Converter={StaticResource IdentityConverter}}" />
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var output = (TextBlock?)root.FindName("Output");

        Assert.NotNull(output);
        Assert.Equal("alpha", output!.Text);
    }

    [Fact]
    public void MultiBindingElement_ParsesAndApplies()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <DelimitedMultiValueConverter x:Key="Joiner" Separator="|" />
  </UserControl.Resources>
  <Grid>
    <TextBox x:Name="Left" Text="L" />
    <TextBox x:Name="Right" Text="R" />
    <TextBlock x:Name="Output">
      <TextBlock.Text>
        <MultiBinding Converter="{StaticResource Joiner}">
          <Binding Path="Text" ElementName="Left" />
          <Binding Path="Text" ElementName="Right" />
        </MultiBinding>
      </TextBlock.Text>
    </TextBlock>
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var output = (TextBlock?)root.FindName("Output");

        Assert.NotNull(output);
        Assert.Equal("L|R", output!.Text);
    }

    [Fact]
    public void BindingMarkup_WithUnsupportedConverterResourceType_Throws()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="NotConverter" TargetType="{x:Type TextBlock}" />
  </UserControl.Resources>
  <Grid>
    <TextBox x:Name="Input" Text="alpha" />
    <TextBlock Text="{Binding Path=Text, ElementName=Input, Converter={StaticResource NotConverter}}" />
  </Grid>
</UserControl>
""";

        var ex = Assert.ThrowsAny<Exception>(() => XamlLoader.LoadFromString(xaml));

        Assert.Contains("Converter", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InputBindingsPropertyElement_WithKeyBinding_ParsesAndApplies()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <RoutedCommand x:Key="OpenCommand" />
  </UserControl.Resources>
  <Grid x:Name="Root">
    <UIElement.InputBindings>
      <KeyBinding Key="O" Modifiers="Control" Command="{StaticResource OpenCommand}" />
    </UIElement.InputBindings>
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var grid = (Grid?)root.FindName("Root");

        Assert.NotNull(grid);
        var keyBinding = Assert.IsType<KeyBinding>(Assert.Single(grid!.InputBindings));
        Assert.Equal(Microsoft.Xna.Framework.Input.Keys.O, keyBinding.Key);
        Assert.Equal(ModifierKeys.Control, keyBinding.Modifiers);
        Assert.IsType<RoutedCommand>(keyBinding.Command);
    }

    [Fact]
    public void InputBindingsPropertyElement_WithUnknownKeyBindingAttribute_Throws()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <UIElement.InputBindings>
      <KeyBinding Key="O" Modifiers="Control" UnknownOption="x" />
    </UIElement.InputBindings>
  </Grid>
</UserControl>
""";

        var ex = Assert.ThrowsAny<Exception>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("UnknownOption", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InputBindingsPropertyElement_WithInvalidKey_ThrowsWithLineInfo()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <UIElement.InputBindings>
      <KeyBinding Key="NotARealKey" Modifiers="Control" />
    </UIElement.InputBindings>
  </Grid>
</UserControl>
""";

        var ex = Assert.ThrowsAny<Exception>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("NotARealKey", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Line", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
