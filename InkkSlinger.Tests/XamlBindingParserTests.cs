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
    public void InputBindingsPropertyElement_WithMouseBinding_ParsesAndApplies()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <RoutedCommand x:Key="OpenCommand" />
  </UserControl.Resources>
  <Grid x:Name="Root">
    <UIElement.InputBindings>
      <MouseBinding Button="Left" Modifiers="Control" Command="{StaticResource OpenCommand}" />
    </UIElement.InputBindings>
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var grid = (Grid?)root.FindName("Root");

        Assert.NotNull(grid);
        var mouseBinding = Assert.IsType<MouseBinding>(Assert.Single(grid!.InputBindings));
        Assert.Equal(MouseButton.Left, mouseBinding.Button);
        Assert.Equal(ModifierKeys.Control, mouseBinding.Modifiers);
        Assert.IsType<RoutedCommand>(mouseBinding.Command);
    }

    [Fact]
    public void InputBindingsPropertyElement_WithUnknownMouseBindingAttribute_Throws()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <UIElement.InputBindings>
      <MouseBinding Button="Left" Modifiers="Control" UnknownOption="x" />
    </UIElement.InputBindings>
  </Grid>
</UserControl>
""";

        var ex = Assert.ThrowsAny<Exception>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("UnknownOption", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InputBindingsPropertyElement_WithInvalidMouseButton_ThrowsWithLineInfo()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <UIElement.InputBindings>
      <MouseBinding Button="NotARealButton" Modifiers="Control" />
    </UIElement.InputBindings>
  </Grid>
</UserControl>
""";

        var ex = Assert.ThrowsAny<Exception>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("NotARealButton", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Line", ex.Message, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public void BindingMarkup_WithXStaticSource_ParsesAndApplies()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <ContentControl x:Name="Probe" Content="{Binding Path=Length, Source={x:Static x:String.Empty}}" />
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var probe = (ContentControl?)root.FindName("Probe");

        Assert.NotNull(probe);
        Assert.Equal(0, Assert.IsType<int>(probe!.Content));
    }

    [Fact]
    public void BindingMarkup_WithXReferenceForwardSource_ParsesAndApplies()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <ContentControl x:Name="Probe" Content="{Binding Path=Text, Source={x:Reference Source}}" />
    <TextBlock x:Name="Source" Text="ref-text" />
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var probe = (ContentControl?)root.FindName("Probe");

        Assert.NotNull(probe);
        Assert.Equal("ref-text", probe!.Content);
    }

    [Fact]
    public void MultiBindingElement_WithForwardXReferenceChildSources_ParsesAndApplies()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <DelimitedMultiValueConverter x:Key="Joiner" Separator="|" />
  </UserControl.Resources>
  <Grid>
    <ContentControl x:Name="Probe">
      <ContentControl.Content>
        <MultiBinding Converter="{StaticResource Joiner}">
          <Binding Path="Text" Source="{x:Reference Left}" />
          <Binding Path="Text" Source="{x:Reference Right}" />
        </MultiBinding>
      </ContentControl.Content>
    </ContentControl>
    <TextBlock x:Name="Left" Text="L" />
    <TextBlock x:Name="Right" Text="R" />
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var probe = (ContentControl?)root.FindName("Probe");

        Assert.NotNull(probe);
        Assert.Equal("L|R", probe!.Content);
    }

    [Fact]
    public void PriorityBindingElement_WithForwardXReferenceChildSource_ParsesAndApplies()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <TextBlock x:Name="Fallback" Text="fallback" />
    <ContentControl x:Name="Probe">
      <ContentControl.Content>
        <PriorityBinding>
          <Binding Path="Text" Source="{x:Reference Preferred}" />
          <Binding Path="Text" ElementName="Fallback" />
        </PriorityBinding>
      </ContentControl.Content>
    </ContentControl>
    <TextBlock x:Name="Preferred" Text="preferred" />
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var probe = (ContentControl?)root.FindName("Probe");

        Assert.NotNull(probe);
        Assert.Equal("preferred", probe!.Content);
    }

    [Fact]
    public void BindingMarkup_WithRelativeSourceFindAncestorMixedSyntax_ParsesAndApplies()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid Width="37">
    <Border>
      <ContentControl x:Name="Probe"
                      Content="{Binding Path=Width, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type Grid}}}" />
    </Border>
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var probe = (ContentControl?)root.FindName("Probe");

        Assert.NotNull(probe);
        Assert.Equal(37f, Assert.IsType<float>(probe!.Content));
    }

    [Fact]
    public void BindingMarkup_WithRelativeSourcePreviousData_ParsesAndUsesFallbackOutsideItemsContext()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <ContentControl x:Name="Probe"
                    Content="{Binding Path=Name, RelativeSource={RelativeSource Mode=PreviousData}, FallbackValue=no-prev}" />
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var probe = (ContentControl?)root.FindName("Probe");

        Assert.NotNull(probe);
        Assert.Equal("no-prev", probe!.Content);
    }

    [Fact]
    public void BindingElement_WithRelativeSourcePropertyElement_ParsesAndApplies()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid Width="42">
    <Grid Width="11">
      <ContentControl x:Name="Probe">
        <ContentControl.Content>
          <Binding Path="Width">
            <Binding.RelativeSource>
              <RelativeSource Mode="FindAncestor" AncestorType="{x:Type Grid}" AncestorLevel="2" />
            </Binding.RelativeSource>
          </Binding>
        </ContentControl.Content>
      </ContentControl>
    </Grid>
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var probe = (ContentControl?)root.FindName("Probe");

        Assert.NotNull(probe);
        Assert.Equal(42f, Assert.IsType<float>(probe!.Content));
    }

    [Fact]
    public void BindingMarkup_WithRelativeSourceFindAncestorWithoutAncestorType_Throws()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <ContentControl Content="{Binding Path=Width, RelativeSource={RelativeSource FindAncestor}}" />
  </Grid>
</UserControl>
""";

        var ex = Assert.ThrowsAny<Exception>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("AncestorType", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BindingMarkup_WithRelativeSourceSelfAndAncestorType_Throws()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <ContentControl Content="{Binding RelativeSource={RelativeSource Mode=Self, AncestorType={x:Type Grid}}}" />
  </Grid>
</UserControl>
""";

        var ex = Assert.ThrowsAny<Exception>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("only valid when Mode=FindAncestor", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BindingMarkup_WithRelativeSourceAncestorLevelZero_Throws()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <ContentControl Content="{Binding RelativeSource={RelativeSource Mode=FindAncestor, AncestorType={x:Type Grid}, AncestorLevel=0}}" />
  </Grid>
</UserControl>
""";

        var ex = Assert.ThrowsAny<Exception>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("AncestorLevel", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BindingMarkup_WithRelativeSourceUnknownKey_Throws()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <ContentControl Content="{Binding RelativeSource={RelativeSource Mode=Self, UnknownOption=x}}" />
  </Grid>
</UserControl>
""";

        var ex = Assert.ThrowsAny<Exception>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("not supported", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BindingMarkup_WithRelativeSourcePositionalModeNotFirst_Throws()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <ContentControl Content="{Binding RelativeSource={RelativeSource AncestorType={x:Type Grid}, FindAncestor}}" />
  </Grid>
</UserControl>
""";

        var ex = Assert.ThrowsAny<Exception>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("mode segment to appear first", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BindingMarkup_WithConflictingSourceSelectors_Throws()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <TextBlock x:Name="Source" Text="ok" />
    <ContentControl Content="{Binding Path=Text, Source={x:Reference Source}, ElementName=Source}" />
  </Grid>
</UserControl>
""";

        var ex = Assert.ThrowsAny<Exception>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("mutually exclusive", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BindingMarkup_WithExplicitNullSourceAndElementName_Throws()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <TextBlock x:Name="Source" Text="ok" />
    <ContentControl Content="{Binding Path=Text, Source={x:Null}, ElementName=Source}" />
  </Grid>
</UserControl>
""";

        var ex = Assert.ThrowsAny<Exception>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("mutually exclusive", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BindingElement_WithRelativeSourceAttributeAndPropertyElement_Throws()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <ContentControl>
      <ContentControl.Content>
        <Binding Path="Width" RelativeSource="{RelativeSource Mode=Self}">
          <Binding.RelativeSource>
            <RelativeSource Mode="FindAncestor" AncestorType="{x:Type Grid}" />
          </Binding.RelativeSource>
        </Binding>
      </ContentControl.Content>
    </ContentControl>
  </Grid>
</UserControl>
""";

        var ex = Assert.ThrowsAny<Exception>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("both as an attribute and as a Binding.RelativeSource", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TabItemBindings_InheritDataContext_AndFindAncestor_ThroughLogicalTree_WhenTabIsNotSelected()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Width="77">
  <TabControl x:Name="Tabs"
              SelectedIndex="0">
    <TabItem Header="Source">
      <TextBlock Text="source" />
    </TabItem>
    <TabItem x:Name="DiagnosticsTab"
             Header="{Binding HeaderText}">
      <Grid>
        <ContentControl x:Name="Probe"
                        Content="{Binding Width, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type UserControl}}}" />
      </Grid>
    </TabItem>
  </TabControl>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        root.DataContext = new HeaderTextViewModel
        {
            HeaderText = "Diagnostics"
        };

        var diagnosticsTab = (TabItem?)root.FindName("DiagnosticsTab");
        var probe = (ContentControl?)root.FindName("Probe");

        Assert.NotNull(diagnosticsTab);
        Assert.NotNull(probe);
        Assert.Equal("Diagnostics", diagnosticsTab!.Header);
        Assert.Equal(77f, Assert.IsType<float>(probe!.Content));
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

        private sealed class HeaderTextViewModel
        {
          public string HeaderText { get; set; } = string.Empty;
        }
}
