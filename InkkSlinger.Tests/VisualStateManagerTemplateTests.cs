using System;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class VisualStateManagerTemplateTests
{
    [Fact]
    public void GoToState_TemplateStateSetters_ResolveNamedParts_AndClearPreviousState()
    {
        var root = (UserControl)XamlLoader.LoadFromString(
            """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="ProbeStyle" TargetType="{x:Type Button}">
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="{x:Type Button}">
            <Grid x:Name="RootGrid">
              <VisualStateManager.VisualStateGroups>
                <VisualStateGroup x:Name="CommonStates">
                  <VisualState x:Name="Normal" />
                  <VisualState x:Name="Accent">
                    <VisualState.Setters>
                      <Setter TargetName="Chrome" Property="Opacity" Value="0.25" />
                    </VisualState.Setters>
                  </VisualState>
                </VisualStateGroup>
              </VisualStateManager.VisualStateGroups>
              <Border x:Name="Chrome" />
            </Grid>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </UserControl.Resources>
  <Button x:Name="Probe" Style="{StaticResource ProbeStyle}" />
</UserControl>
""");

        var button = Assert.IsType<Button>(root.FindName("Probe"));
        Assert.True(button.ApplyTemplate());

        var chrome = FindNamedVisualChild<Border>(button, "Chrome");
        Assert.NotNull(chrome);
        Assert.Equal(1f, chrome!.Opacity, 3);

        Assert.True(VisualStateManager.GoToState(button, "Accent"));
        Assert.Equal(0.25f, chrome.Opacity, 3);

        Assert.True(VisualStateManager.GoToState(button, "Normal"));
        Assert.Equal(1f, chrome.Opacity, 3);
    }

    [Fact]
    public void GoToState_TemplateStoryboards_ResolveNamedParts()
    {
        var manager = AnimationManager.Current;
        manager.ResetForTests();

        try
        {
            var root = (UserControl)XamlLoader.LoadFromString(
                """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="ProbeStyle" TargetType="{x:Type Button}">
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="{x:Type Button}">
            <Grid x:Name="RootGrid">
              <VisualStateManager.VisualStateGroups>
                <VisualStateGroup x:Name="CommonStates">
                  <VisualState x:Name="Normal">
                    <Storyboard>
                      <DoubleAnimation Storyboard.TargetName="Chrome" Storyboard.TargetProperty="Opacity" To="1" Duration="0:0:0" FillBehavior="HoldEnd" />
                    </Storyboard>
                  </VisualState>
                  <VisualState x:Name="Pressed">
                    <Storyboard>
                      <DoubleAnimation Storyboard.TargetName="Chrome" Storyboard.TargetProperty="Opacity" To="0.4" Duration="0:0:0" FillBehavior="HoldEnd" />
                    </Storyboard>
                  </VisualState>
                </VisualStateGroup>
              </VisualStateManager.VisualStateGroups>
              <Border x:Name="Chrome" Opacity="1" />
            </Grid>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </UserControl.Resources>
  <Button x:Name="Probe" Style="{StaticResource ProbeStyle}" />
</UserControl>
""");

            var button = Assert.IsType<Button>(root.FindName("Probe"));
            Assert.True(button.ApplyTemplate());

            var chrome = FindNamedVisualChild<Border>(button, "Chrome");
            Assert.NotNull(chrome);

            Assert.True(VisualStateManager.GoToState(button, "Pressed"));
            manager.Update(new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)));
            Assert.Equal(0.4f, chrome!.Opacity, 3);

            Assert.True(VisualStateManager.GoToState(button, "Normal"));
            manager.Update(new GameTime(TimeSpan.FromMilliseconds(32), TimeSpan.FromMilliseconds(16)));
            Assert.Equal(1f, chrome.Opacity, 3);
        }
        finally
        {
            manager.ResetForTests();
        }
    }

    private static TElement? FindNamedVisualChild<TElement>(UIElement root, string name)
        where TElement : FrameworkElement
    {
        if (root is TElement typed && typed.Name == name)
        {
            return typed;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindNamedVisualChild<TElement>(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}