using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class GridXamlTests
{
    [Fact]
    public void Grid_ShowGridLines_XamlAttribute_Parses()
    {
        const string xaml = """
                            <Grid xmlns="urn:inkkslinger-ui"
                                  ShowGridLines="True" />
                            """;

        var grid = Assert.IsType<Grid>(XamlLoader.LoadFromString(xaml));

        Assert.True(grid.ShowGridLines);
    }

    [Fact]
    public void Grid_SharedSizeScopeAndGroups_XamlAttributes_ParseAndSynchronize()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <StackPanel x:Name="ScopeHost"
                                          Grid.IsSharedSizeScope="True">
                                <Grid x:Name="FirstGrid">
                                  <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto" SharedSizeGroup="Label" />
                                    <ColumnDefinition Width="*" />
                                  </Grid.ColumnDefinitions>
                                  <Grid.RowDefinitions>
                                    <RowDefinition Height="Auto" />
                                  </Grid.RowDefinitions>
                                  <Border Width="56" Height="18" />
                                  <Border Grid.Column="1" Width="40" Height="18" />
                                </Grid>
                                <Grid x:Name="SecondGrid">
                                  <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto" SharedSizeGroup="Label" />
                                    <ColumnDefinition Width="*" />
                                  </Grid.ColumnDefinitions>
                                  <Grid.RowDefinitions>
                                    <RowDefinition Height="Auto" />
                                  </Grid.RowDefinitions>
                                  <Border Width="112" Height="18" />
                                  <Border Grid.Column="1" Width="40" Height="18" />
                                </Grid>
                              </StackPanel>
                            </UserControl>
                            """;

        var root = Assert.IsType<UserControl>(XamlLoader.LoadFromString(xaml));
        var scopeHost = Assert.IsType<StackPanel>(root.FindName("ScopeHost"));
        var firstGrid = Assert.IsType<Grid>(root.FindName("FirstGrid"));
        var secondGrid = Assert.IsType<Grid>(root.FindName("SecondGrid"));

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 240, 16);

        Assert.True(Grid.GetIsSharedSizeScope(scopeHost));
        Assert.Equal("Label", firstGrid.ColumnDefinitions[0].SharedSizeGroup);
        Assert.Equal(112f, firstGrid.ColumnDefinitions[0].ActualWidth, 0.01f);
        Assert.Equal(112f, secondGrid.ColumnDefinitions[0].ActualWidth, 0.01f);
    }

    [Fact]
    public void Grid_SharedSizeGroup_InvalidXamlIdentifier_Throws()
    {
        const string xaml = """
                            <Grid xmlns="urn:inkkslinger-ui">
                              <Grid.ColumnDefinitions>
                                <ColumnDefinition SharedSizeGroup="123Bad" />
                              </Grid.ColumnDefinitions>
                            </Grid>
                            """;

        Assert.ThrowsAny<Exception>(() => XamlLoader.LoadFromString(xaml));
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }
}