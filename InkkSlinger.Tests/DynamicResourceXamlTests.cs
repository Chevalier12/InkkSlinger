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
    public void DynamicResource_InSetterValue_ThrowsHelpfulXamlException()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="TextStyle" TargetType="{x:Type TextBlock}">
      <Style.Setters>
        <Setter Property="Text" Value="{DynamicResource SharedText}" />
      </Style.Setters>
    </Style>
  </UserControl.Resources>
  <Grid>
    <TextBlock Style="{StaticResource TextStyle}" />
  </Grid>
</UserControl>
""";

        var ex = Assert.ThrowsAny<Exception>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("DynamicResource is not supported", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
