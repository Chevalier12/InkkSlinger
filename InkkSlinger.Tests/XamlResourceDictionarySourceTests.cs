using System;
using System.IO;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class XamlResourceDictionarySourceTests
{
    [Fact]
    public void MergedResourceDictionary_SourceRelativeFile_AppliesButtonStyleViaStaticResource()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var themePath = Path.Combine(tempRoot, "ButtonTheme.xaml");
            File.WriteAllText(
                themePath,
                """
<ResourceDictionary xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style x:Key="ThemeButtonStyle" TargetType="{x:Type Button}">
    <Setter Property="Background" Value="#B100E8" />
    <Setter Property="BorderBrush" Value="#14FFC8" />
    <Setter Property="BorderThickness" Value="6" />
  </Style>
</ResourceDictionary>
""");

            var mainPath = Path.Combine(tempRoot, "Main.xaml");
            File.WriteAllText(
                mainPath,
                """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="ButtonTheme.xaml" />
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </UserControl.Resources>
  <Grid>
    <Button x:Name="ThemedButton" Style="{StaticResource ThemeButtonStyle}" Text="Themed" />
  </Grid>
</UserControl>
""");

            var root = (UserControl)XamlLoader.LoadFromFile(mainPath);
            var button = Assert.IsType<Button>(root.FindName("ThemedButton"));

            Assert.Equal(new Microsoft.Xna.Framework.Color(177, 0, 232), button.Background);
            Assert.Equal(new Microsoft.Xna.Framework.Color(20, 255, 200), button.BorderBrush);
            Assert.Equal(6f, button.BorderThickness);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void MergedResourceDictionary_SourceAbsoluteFile_ResolvesWhenLoadedFromString()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var themePath = Path.Combine(tempRoot, "AbsoluteTheme.xaml");
            File.WriteAllText(
                themePath,
                """
<ResourceDictionary xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style x:Key="LoudButtonStyle" TargetType="{x:Type Button}">
    <Setter Property="Padding" Value="21,3,4,9" />
  </Style>
</ResourceDictionary>
""");

            var normalizedThemePath = themePath.Replace('\\', '/');
            var xaml = $$"""
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="{{normalizedThemePath}}" />
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </UserControl.Resources>
  <Grid>
    <Button x:Name="StyledButton" Style="{StaticResource LoudButtonStyle}" Text="Styled" />
  </Grid>
</UserControl>
""";

            var root = (UserControl)XamlLoader.LoadFromString(xaml);
            var button = Assert.IsType<Button>(root.FindName("StyledButton"));

            Assert.Equal(new Thickness(21f, 3f, 4f, 9f), button.Padding);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "InkkSlinger.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup for temp artifacts.
        }
    }
}
