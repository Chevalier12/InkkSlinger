using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class CatalogButtonStyleBehaviorTests
{
    [Fact]
    public void TemplateHoverTrigger_TargetedBorderInvalidatesTemplatedButtonOwner()
    {
        const string xaml = """
                            <UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <UserControl.Resources>
                                <Style x:Key="HoverTemplateButtonStyle" TargetType="{x:Type Button}">
                                  <Setter Property="Template">
                                    <Setter.Value>
                                      <ControlTemplate TargetType="{x:Type Button}">
                                        <Border x:Name="border"
                                                Background="#202020"
                                                BorderBrush="#404040"
                                                BorderThickness="1">
                                          <ContentPresenter HorizontalAlignment="Center"
                                                            VerticalAlignment="Center" />
                                        </Border>
                                        <ControlTemplate.Triggers>
                                          <Trigger Property="IsMouseOver" Value="True">
                                            <Setter TargetName="border" Property="Background" Value="#303030" />
                                            <Setter TargetName="border" Property="BorderBrush" Value="#FF8C00" />
                                          </Trigger>
                                        </ControlTemplate.Triggers>
                                      </ControlTemplate>
                                    </Setter.Value>
                                  </Setter>
                                </Style>
                              </UserControl.Resources>
                              <Canvas>
                                <Button x:Name="Probe"
                                        Style="{StaticResource HoverTemplateButtonStyle}"
                                        Width="200"
                                        Height="80"
                                        Canvas.Left="20"
                                        Canvas.Top="20" />
                              </Canvas>
                            </UserControl>
                            """;

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var button = Assert.IsType<Button>(root.FindName("Probe"));
        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 420, 220, 16);

        var border = FindNamedVisualChild<Border>(button, "border");
        Assert.NotNull(border);

        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(40f, 40f), pointerMoved: true));
        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.True(button.IsMouseOver);
        var invalidationSnapshot = uiRoot.GetRenderInvalidationDebugSnapshotForTests();

        Assert.Equal("Button", invalidationSnapshot.EffectiveSourceType);
        Assert.Equal("Probe", invalidationSnapshot.EffectiveSourceName);
        Assert.Equal("Button", invalidationSnapshot.DirtyBoundsVisualType);
        Assert.Equal("Probe", invalidationSnapshot.DirtyBoundsVisualName);
        Assert.Equal("Button#Probe", uiRoot.GetLastSynchronizedDirtyRootSummaryForTests());
    }

    [Fact]
    public void FullCatalogLikeButtonStyle_HoverAndLeaveScaleDirection_IsCorrect()
    {
        const string xaml = """
                            <UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <UserControl.Resources>
                                <Style x:Key="CatalogButtonStyle" TargetType="{x:Type Button}">
                                  <Setter Property="Background" Value="#2A2A2A" />
                                  <Setter Property="Foreground" Value="#F0F0F0" />
                                  <Setter Property="BorderBrush" Value="#3F3F3F" />
                                  <Setter Property="BorderThickness" Value="1" />
                                  <Setter Property="Padding" Value="20,10" />
                                  <Setter Property="FontWeight" Value="Medium" />
                                  <Setter Property="FontSize" Value="13" />
                                  <Setter Property="Cursor" Value="Hand" />
                                  <Setter Property="SnapsToDevicePixels" Value="True" />
                                  <Setter Property="RenderTransformOrigin" Value="0.5,0.5" />
                                  <Setter Property="RenderTransform">
                                    <Setter.Value>
                                      <ScaleTransform ScaleX="1" ScaleY="1" />
                                    </Setter.Value>
                                  </Setter>
                                  <Setter Property="Template">
                                    <Setter.Value>
                                      <ControlTemplate TargetType="{x:Type Button}">
                                        <Border x:Name="border"
                                                Background="{TemplateBinding Background}"
                                                BorderBrush="{TemplateBinding BorderBrush}"
                                                BorderThickness="{TemplateBinding BorderThickness}"
                                                CornerRadius="6"
                                                Padding="{TemplateBinding Padding}"
                                                SnapsToDevicePixels="True">
                                          <Border.Effect>
                                            <DropShadowEffect x:Name="shadow"
                                                              Color="#FF8C00"
                                                              ShadowDepth="0"
                                                              BlurRadius="0"
                                                              Opacity="0" />
                                          </Border.Effect>
                                          <ContentPresenter HorizontalAlignment="Center"
                                                            VerticalAlignment="Center" />
                                        </Border>
                                        <ControlTemplate.Triggers>
                                          <EventTrigger RoutedEvent="MouseEnter">
                                            <BeginStoryboard>
                                              <Storyboard>
                                                <DoubleAnimation Storyboard.TargetProperty="RenderTransform.ScaleX"
                                                                 To="1.03" Duration="0:0:0.15" />
                                                <DoubleAnimation Storyboard.TargetProperty="RenderTransform.ScaleY"
                                                                 To="1.03" Duration="0:0:0.15" />
                                                <DoubleAnimation Storyboard.TargetName="shadow"
                                                                 Storyboard.TargetProperty="BlurRadius"
                                                                 To="12" Duration="0:0:0.15" />
                                                <DoubleAnimation Storyboard.TargetName="shadow"
                                                                 Storyboard.TargetProperty="Opacity"
                                                                 To="0.5" Duration="0:0:0.15" />
                                              </Storyboard>
                                            </BeginStoryboard>
                                          </EventTrigger>
                                          <EventTrigger RoutedEvent="MouseLeave">
                                            <BeginStoryboard>
                                              <Storyboard>
                                                <DoubleAnimation Storyboard.TargetProperty="RenderTransform.ScaleX"
                                                                 To="1.0" Duration="0:0:0.15" />
                                                <DoubleAnimation Storyboard.TargetProperty="RenderTransform.ScaleY"
                                                                 To="1.0" Duration="0:0:0.15" />
                                                <DoubleAnimation Storyboard.TargetName="shadow"
                                                                 Storyboard.TargetProperty="BlurRadius"
                                                                 To="0" Duration="0:0:0.15" />
                                                <DoubleAnimation Storyboard.TargetName="shadow"
                                                                 Storyboard.TargetProperty="Opacity"
                                                                 To="0" Duration="0:0:0.15" />
                                              </Storyboard>
                                            </BeginStoryboard>
                                          </EventTrigger>
                                          <Trigger Property="IsMouseOver" Value="True">
                                            <Setter TargetName="border" Property="Background" Value="#333333" />
                                            <Setter TargetName="border" Property="BorderBrush" Value="#FF8C00" />
                                            <Setter Property="Foreground" Value="#FFA940" />
                                          </Trigger>
                                        </ControlTemplate.Triggers>
                                      </ControlTemplate>
                                    </Setter.Value>
                                  </Setter>
                                </Style>
                              </UserControl.Resources>
                              <ScrollViewer Width="320" Height="220" VerticalScrollBarVisibility="Auto">
                                <StackPanel x:Name="Host" />
                              </ScrollViewer>
                            </UserControl>
                            """;

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var host = Assert.IsType<StackPanel>(root.FindName("Host"));
        var style = Assert.IsType<Style>(root.Resources["CatalogButtonStyle"]);
        for (var i = 0; i < 12; i++)
        {
            host.AddChild(new Button
            {
                Content = $"Item {i}",
                Height = 40f,
                Margin = new Thickness(0f, 0f, 0f, 4f),
                Style = style
            });
        }

        var targetButton = Assert.IsType<Button>(host.Children[2]);
        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 460, 360, 16);

        var insideTarget = FindPointHittingTarget(root, targetButton, 460, 360);
        var transform = Assert.IsType<ScaleTransform>(targetButton.RenderTransform);
        Assert.Equal(1f, transform.ScaleX, 3);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(4f, 4f), pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(insideTarget, pointerMoved: true));
        for (var i = 0; i < 12; i++)
        {
            RunLayout(uiRoot, 460, 360, 32 + (i * 16));
        }

        Assert.Equal(1.03f, transform.ScaleX, 3);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(440f, 10f), pointerMoved: true));
        for (var i = 0; i < 12; i++)
        {
            RunLayout(uiRoot, 460, 360, 260 + (i * 16));
        }

        Assert.Equal(1f, transform.ScaleX, 3);
    }

    private static Vector2 FindPointHittingTarget(UIElement root, UIElement target, int width, int height)
    {
        for (var y = 0; y < height; y += 2)
        {
            for (var x = 0; x < width; x += 2)
            {
                var point = new Vector2(x, y);
                var hit = VisualTreeHelper.HitTest(root, point);
                if (IsTargetOrDescendant(hit, target))
                {
                    return point;
                }
            }
        }

        throw new InvalidOperationException("Could not locate point that hits target button.");
    }

    private static bool IsTargetOrDescendant(UIElement? hit, UIElement target)
    {
        for (var current = hit; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (ReferenceEquals(current, target))
            {
                return true;
            }
        }

        return false;
    }

    private static InputDelta CreatePointerDelta(Vector2 pointer, bool pointerMoved)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = pointerMoved,
            WheelDelta = 0,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }

    private static TElement? FindNamedVisualChild<TElement>(UIElement root, string name)
      where TElement : FrameworkElement
    {
      foreach (var child in root.GetVisualChildren())
      {
        if (child is TElement element && string.Equals(element.Name, name, StringComparison.Ordinal))
        {
          return element;
        }

        var nested = FindNamedVisualChild<TElement>(child, name);
        if (nested != null)
        {
          return nested;
        }
      }

      return null;
    }
}
