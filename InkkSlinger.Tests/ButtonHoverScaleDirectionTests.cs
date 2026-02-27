using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ButtonHoverScaleDirectionTests
{
    [Fact]
    public void StyleEventTriggers_MouseEnterGrows_MouseLeaveResets()
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
                              <Canvas>
                                <Button x:Name="Probe" Style="{StaticResource HoverScaleStyle}" Width="200" Height="80" Canvas.Left="20" Canvas.Top="20" />
                              </Canvas>
                            </UserControl>
                            """;

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var button = Assert.IsType<Button>(root.FindName("Probe"));
        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 420, 220, 16);

        var transform = Assert.IsType<ScaleTransform>(button.RenderTransform);
        Assert.Equal(1f, transform.ScaleX, 3);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(40f, 40f), pointerMoved: true));
        RunLayout(uiRoot, 420, 220, 32);
        Assert.Equal(1.03f, transform.ScaleX, 3);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(360f, 180f), pointerMoved: true));
        RunLayout(uiRoot, 420, 220, 48);
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
}
