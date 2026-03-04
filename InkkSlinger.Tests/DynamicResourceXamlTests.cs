using System;
using Xunit;

namespace InkkSlinger.Tests;

public class DynamicResourceXamlTests
{
    [Fact]
    public void DynamicResource_OnDependencyProperty_ResolvesFromAncestorResources()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <TextBlock x:Key="SharedContent" Text="hello" />
  </UserControl.Resources>
  <Grid>
    <ContentControl x:Name="Host" Content="{DynamicResource SharedContent}" />
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var host = Assert.IsType<ContentControl>(root.FindName("Host"));
        var content = Assert.IsType<TextBlock>(host.Content);
        Assert.Equal("hello", content.Text);
    }

    [Fact]
    public void DynamicResource_UpdatesWhenAncestorResourceChanges()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <TextBlock x:Key="SharedContent" Text="first" />
  </UserControl.Resources>
  <Grid>
    <ContentControl x:Name="Host" Content="{DynamicResource SharedContent}" />
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var host = Assert.IsType<ContentControl>(root.FindName("Host"));
        var replacement = new TextBlock { Text = "second" };

        root.Resources["SharedContent"] = replacement;

        Assert.Same(replacement, host.Content);
    }

    [Fact]
    public void DynamicResource_MissingAtLoad_UpdatesWhenKeyAddedLater()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <ContentControl x:Name="Host" Content="{DynamicResource LateContent}" />
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var host = Assert.IsType<ContentControl>(root.FindName("Host"));
        Assert.Null(host.Content);

        var lateContent = new TextBlock { Text = "late" };
        root.Resources["LateContent"] = lateContent;

        Assert.Same(lateContent, host.Content);
    }

    [Fact]
    public void StaticResource_OnAttachedProperty_ResolvesAtLoad()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <ContextMenu x:Key="SharedMenu" />
  </UserControl.Resources>
  <Grid>
    <TextBlock x:Name="Target" ContextMenu.ContextMenu="{StaticResource SharedMenu}" />
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var target = Assert.IsType<TextBlock>(root.FindName("Target"));
        var menu = ContextMenu.GetContextMenu(target);
        Assert.NotNull(menu);
        Assert.Same(root.Resources["SharedMenu"], menu);
    }

    [Fact]
    public void DynamicResource_OnAttachedProperty_ResolvesAndUpdates()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <ContextMenu x:Key="SharedMenu" />
  </UserControl.Resources>
  <Grid>
    <TextBlock x:Name="Target" ContextMenu.ContextMenu="{DynamicResource SharedMenu}" />
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var target = Assert.IsType<TextBlock>(root.FindName("Target"));
        var firstMenu = ContextMenu.GetContextMenu(target);
        Assert.NotNull(firstMenu);

        var replacementMenu = new ContextMenu();
        root.Resources["SharedMenu"] = replacementMenu;

        Assert.Same(replacementMenu, ContextMenu.GetContextMenu(target));
    }

    [Fact]
    public void DynamicResource_OnNonDependencyProperty_ThrowsHelpfulXamlException()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <TextBlock Resources="{DynamicResource AnyKey}" />
  </Grid>
</UserControl>
""";

        var ex = Assert.ThrowsAny<Exception>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("requires a dependency property named 'ResourcesProperty'", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DynamicResource_InSetterValue_ResolvesAndUpdates()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <SolidColorBrush x:Key="SharedBrush" Color="#112233" />
    <Style x:Key="ButtonStyle" TargetType="{x:Type Button}">
      <Style.Setters>
        <Setter Property="Background" Value="{DynamicResource SharedBrush}" />
      </Style.Setters>
    </Style>
  </UserControl.Resources>
  <Grid>
    <Button x:Name="Probe" Style="{StaticResource ButtonStyle}" />
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var probe = Assert.IsType<Button>(root.FindName("Probe"));
        Assert.Equal(new Microsoft.Xna.Framework.Color(0x11, 0x22, 0x33), probe.Background);

        root.Resources["SharedBrush"] = new SolidColorBrush(new Microsoft.Xna.Framework.Color(0x44, 0x55, 0x66));
        Assert.Equal(new Microsoft.Xna.Framework.Color(0x44, 0x55, 0x66), probe.Background);
    }

    [Fact]
    public void DynamicResource_BrushResource_CoercesToColorProperty_OnEachUpdate()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <SolidColorBrush x:Key="AccentBrush" Color="#112233" />
  </UserControl.Resources>
  <Grid>
    <Button x:Name="Probe" Background="{DynamicResource AccentBrush}" />
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var button = Assert.IsType<Button>(root.FindName("Probe"));
        Assert.Equal(new Microsoft.Xna.Framework.Color(0x11, 0x22, 0x33), button.Background);

        root.Resources["AccentBrush"] = new SolidColorBrush(new Microsoft.Xna.Framework.Color(0x44, 0x55, 0x66));

        Assert.Equal(new Microsoft.Xna.Framework.Color(0x44, 0x55, 0x66), button.Background);
    }

    [Fact]
    public void DynamicResource_InTriggerSetter_UpdatesWhileActive()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <SolidColorBrush x:Key="HoverBrush" Color="#223344" />
    <Style x:Key="ProbeStyle" TargetType="{x:Type Button}">
      <Style.Triggers>
        <Trigger Property="IsEnabled" Value="False">
          <Trigger.Setters>
            <Setter Property="Background" Value="{DynamicResource HoverBrush}" />
          </Trigger.Setters>
        </Trigger>
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
        probe.IsEnabled = false;
        Assert.Equal(new Microsoft.Xna.Framework.Color(0x22, 0x33, 0x44), probe.Background);

        root.Resources["HoverBrush"] = new SolidColorBrush(new Microsoft.Xna.Framework.Color(0x55, 0x66, 0x77));
        Assert.Equal(new Microsoft.Xna.Framework.Color(0x55, 0x66, 0x77), probe.Background);
    }

    [Fact]
    public void DynamicResource_InSetValueAction_ResolvesAtInvokeTime()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <SolidColorBrush x:Key="SharedBrush" Color="#102030" />
    <Style x:Key="ProbeStyle" TargetType="{x:Type Button}">
      <Style.Triggers>
        <Trigger Property="IsEnabled" Value="False">
          <Trigger.EnterActions>
            <SetValueAction Property="Background" Value="{DynamicResource SharedBrush}" />
          </Trigger.EnterActions>
        </Trigger>
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
        probe.IsEnabled = false;
        Assert.Equal(new Microsoft.Xna.Framework.Color(0x10, 0x20, 0x30), probe.Background);

        root.Resources["SharedBrush"] = new SolidColorBrush(new Microsoft.Xna.Framework.Color(0x40, 0x50, 0x60));
        probe.IsEnabled = true;
        probe.IsEnabled = false;
        Assert.Equal(new Microsoft.Xna.Framework.Color(0x40, 0x50, 0x60), probe.Background);
    }

    [Fact]
    public void DynamicResource_InBeginStoryboardAttribute_LoadsSuccessfully()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Storyboard x:Key="DisableStoryboard">
      <DoubleAnimation Storyboard.TargetName="Probe" Storyboard.TargetProperty="Opacity" To="0.4" Duration="0:0:0" />
    </Storyboard>
    <Style x:Key="ProbeStyle" TargetType="{x:Type Button}">
      <Style.Triggers>
        <Trigger Property="IsEnabled" Value="False">
          <Trigger.EnterActions>
            <BeginStoryboard Storyboard="{DynamicResource DisableStoryboard}" />
          </Trigger.EnterActions>
        </Trigger>
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
        Assert.NotNull(probe);
    }
}
