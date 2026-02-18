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
    public void PriorityBindingElement_ParsesAndApplies()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <TextBox x:Name="Fallback" Text="fallback-value" />
    <TextBlock x:Name="Output">
      <TextBlock.Text>
        <PriorityBinding>
          <Binding Path="Text" ElementName="MissingElement" />
          <Binding Path="Text" ElementName="Fallback" />
        </PriorityBinding>
      </TextBlock.Text>
    </TextBlock>
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var output = (TextBlock?)root.FindName("Output");

        Assert.NotNull(output);
        Assert.Equal("fallback-value", output!.Text);
    }

    [Fact]
    public void FrameworkElementBindingGroupPropertyElement_ParsesAndApplies()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid x:Name="Root">
    <FrameworkElement.BindingGroup>
      <BindingGroup Name="RootGroup" />
    </FrameworkElement.BindingGroup>
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var grid = (Grid?)root.FindName("Root");

        Assert.NotNull(grid);
        Assert.NotNull(grid!.BindingGroup);
        Assert.Equal("RootGroup", grid.BindingGroup!.Name);
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

    [Fact]
    public void BindingMarkup_UpdateSourceExceptionFilter_FromStaticResource_ParsesAndInvokes()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <TextBox x:Name="Input"
             Text="{Binding Path=Value, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged, ValidatesOnExceptions=True, UpdateSourceExceptionFilter={StaticResource Filter}}" />
  </Grid>
</UserControl>
""";

        var host = new UserControl();
        host.Resources.Add("Filter", new UpdateSourceExceptionFilterCallback(static (_, _) => "xaml-filtered"));
        XamlLoader.LoadIntoFromString(host, xaml);
        host.DataContext = new ThrowingSetterViewModel();

        var input = (TextBox?)host.FindName("Input");
        Assert.NotNull(input);

        input!.Text = "boom";

        Assert.True(Validation.GetHasError(input));
        Assert.Contains(
            Validation.GetErrors(input),
            error => string.Equals(error.ErrorContent?.ToString(), "xaml-filtered", StringComparison.Ordinal));
    }

    [Fact]
    public void PriorityBindingElement_WithUnsupportedAttribute_Throws()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <TextBlock>
      <TextBlock.Text>
        <PriorityBinding UnknownOption="x">
          <Binding Path="Text" ElementName="Missing" />
        </PriorityBinding>
      </TextBlock.Text>
    </TextBlock>
  </Grid>
</UserControl>
""";

        var ex = Assert.ThrowsAny<Exception>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("UnknownOption", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PriorityBindingElement_WithNonBindingChild_Throws()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <TextBlock>
      <TextBlock.Text>
        <PriorityBinding>
          <TextBox />
        </PriorityBinding>
      </TextBlock.Text>
    </TextBlock>
  </Grid>
</UserControl>
""";

        var ex = Assert.ThrowsAny<Exception>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("can only contain Binding", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FrameworkElementBindingGroup_WithInvalidCulture_Throws()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <FrameworkElement.BindingGroup>
      <BindingGroup Culture="a*b" />
    </FrameworkElement.BindingGroup>
  </Grid>
</UserControl>
""";

        var ex = Assert.ThrowsAny<Exception>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("culture", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BindingMarkup_UpdateSourceExceptionFilter_WithWrongResourceType_Throws()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="NotFilter" TargetType="{x:Type TextBlock}" />
  </UserControl.Resources>
  <Grid>
    <TextBox x:Name="Input"
             Text="{Binding Path=Value, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged, ValidatesOnExceptions=True, UpdateSourceExceptionFilter={StaticResource NotFilter}}" />
  </Grid>
</UserControl>
""";

        var ex = Assert.ThrowsAny<Exception>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("UpdateSourceExceptionFilter", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ThrowingSetterViewModel
    {
        private string _value = "seed";

        public string Value
        {
            get => _value;
            set => throw new InvalidOperationException("setter failed");
        }
    }
}
