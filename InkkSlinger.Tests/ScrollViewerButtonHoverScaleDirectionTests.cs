using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ScrollViewerButtonHoverScaleDirectionTests
{
    [Fact]
    public void ScrollViewerButtons_MouseEnterGrows_MouseLeaveResets()
    {
        const string xaml = """
                            <UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <UserControl.Resources>
                                <Style x:Key="HoverScaleStyle" TargetType="{x:Type Button}">
                                  <Setter Property="RenderTransformOrigin" Value="0.5,0.5" />
                                  <Setter Property="RenderTransform">
                                    <Setter.Value>
                                      <ScaleTransform ScaleX="1" ScaleY="1" />
                                    </Setter.Value>
                                  </Setter>
                                  <Setter Property="Template">
                                    <Setter.Value>
                                      <ControlTemplate TargetType="{x:Type Button}">
                                        <Border x:Name="border">
                                          <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                                        </Border>
                                        <ControlTemplate.Triggers>
                                          <EventTrigger RoutedEvent="MouseEnter">
                                            <BeginStoryboard>
                                              <Storyboard>
                                                <DoubleAnimation Storyboard.TargetProperty="RenderTransform.ScaleX" To="1.03" Duration="0:0:0" />
                                                <DoubleAnimation Storyboard.TargetProperty="RenderTransform.ScaleY" To="1.03" Duration="0:0:0" />
                                              </Storyboard>
                                            </BeginStoryboard>
                                          </EventTrigger>
                                          <EventTrigger RoutedEvent="MouseLeave">
                                            <BeginStoryboard>
                                              <Storyboard>
                                                <DoubleAnimation Storyboard.TargetProperty="RenderTransform.ScaleX" To="1.0" Duration="0:0:0" />
                                                <DoubleAnimation Storyboard.TargetProperty="RenderTransform.ScaleY" To="1.0" Duration="0:0:0" />
                                              </Storyboard>
                                            </BeginStoryboard>
                                          </EventTrigger>
                                        </ControlTemplate.Triggers>
                                      </ControlTemplate>
                                    </Setter.Value>
                                  </Setter>
                                </Style>
                              </UserControl.Resources>
                              <ScrollViewer Width="300" Height="200" VerticalScrollBarVisibility="Auto">
                                <StackPanel x:Name="Host" />
                              </ScrollViewer>
                            </UserControl>
                            """;

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var host = Assert.IsType<StackPanel>(root.FindName("Host"));
        for (var i = 0; i < 20; i++)
        {
            host.AddChild(new Button
            {
                Text = $"Item {i}",
                Height = 36f,
                Margin = new Thickness(0f, 0f, 0f, 4f),
                Style = Assert.IsType<Style>(root.Resources["HoverScaleStyle"])
            });
        }

        var targetButton = Assert.IsType<Button>(host.Children[1]);
        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 420, 340, 16);

        var transform = Assert.IsType<ScaleTransform>(targetButton.RenderTransform);
        Assert.Equal(1f, transform.ScaleX, 3);

        var insideTarget = FindPointHittingTarget(root, targetButton, 420, 340);

        // Move from outside to inside target button.
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(4f, 4f), pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(insideTarget, pointerMoved: true));
        RunLayout(uiRoot, 420, 340, 32);
        Assert.Equal(1.03f, transform.ScaleX, 3);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(360f, 20f), pointerMoved: true));
        RunLayout(uiRoot, 420, 340, 48);
        Assert.Equal(1f, transform.ScaleX, 3);
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

        throw new InvalidOperationException("Could not locate a hit-test point for target button.");
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
}
