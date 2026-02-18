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
}
