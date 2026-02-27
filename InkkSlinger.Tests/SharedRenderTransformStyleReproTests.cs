using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class SharedRenderTransformStyleReproTests
{
    [Fact]
    public void StyleSetter_RenderTransform_IsolatedPerButton_AndHoverAnimationDoesNotLeak()
    {
        const string xaml = """
                            <UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <UserControl.Resources>
                                <Style x:Key="HoverScaleButtonStyle" TargetType="{x:Type Button}">
                                  <Setter Property="RenderTransformOrigin" Value="0.5,0.5" />
                                  <Setter Property="RenderTransform">
                                    <Setter.Value>
                                      <ScaleTransform ScaleX="1" ScaleY="1" />
                                    </Setter.Value>
                                  </Setter>
                                  <Style.Triggers>
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
                                  </Style.Triggers>
                                </Style>
                              </UserControl.Resources>
                              <Canvas>
                                <Button x:Name="First" Style="{StaticResource HoverScaleButtonStyle}" Width="120" Height="44" Canvas.Left="20" Canvas.Top="20" />
                                <Button x:Name="Second" Style="{StaticResource HoverScaleButtonStyle}" Width="120" Height="44" Canvas.Left="220" Canvas.Top="20" />
                              </Canvas>
                            </UserControl>
                            """;

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var first = Assert.IsType<Button>(root.FindName("First"));
        var second = Assert.IsType<Button>(root.FindName("Second"));
        var uiRoot = new UiRoot(root);

        RunLayout(uiRoot, width: 420, height: 160, elapsedMs: 16);

        var firstTransform = Assert.IsType<ScaleTransform>(first.RenderTransform);
        var secondTransform = Assert.IsType<ScaleTransform>(second.RenderTransform);
        Assert.NotSame(firstTransform, secondTransform);
        Assert.Equal(1f, firstTransform.ScaleX, 3);
        Assert.Equal(1f, secondTransform.ScaleX, 3);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(40f, 40f), pointerMoved: true));
        RunLayout(uiRoot, width: 420, height: 160, elapsedMs: 32);

        Assert.Equal(1.03f, firstTransform.ScaleX, 3);
        Assert.Equal(1f, secondTransform.ScaleX, 3);
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
