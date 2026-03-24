using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class StoryboardScaleLaneReplacementTests
{
    [Fact]
    public void RepeatedHoverTransitions_LeaveStoryboardRestoresScaleToBase()
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
                                  <Style.Triggers>
                                    <EventTrigger RoutedEvent="MouseEnter">
                                      <BeginStoryboard>
                                        <Storyboard>
                                          <DoubleAnimation Storyboard.TargetProperty="RenderTransform.ScaleX" To="1.3" Duration="0:0:0.15" />
                                          <DoubleAnimation Storyboard.TargetProperty="RenderTransform.ScaleY" To="1.3" Duration="0:0:0.15" />
                                        </Storyboard>
                                      </BeginStoryboard>
                                    </EventTrigger>
                                    <EventTrigger RoutedEvent="MouseLeave">
                                      <BeginStoryboard>
                                        <Storyboard>
                                          <DoubleAnimation Storyboard.TargetProperty="RenderTransform.ScaleX" To="1.0" Duration="0:0:0.15" />
                                          <DoubleAnimation Storyboard.TargetProperty="RenderTransform.ScaleY" To="1.0" Duration="0:0:0.15" />
                                        </Storyboard>
                                      </BeginStoryboard>
                                    </EventTrigger>
                                  </Style.Triggers>
                                </Style>
                              </UserControl.Resources>
                              <Canvas>
                                <Button x:Name="Probe" Style="{StaticResource HoverScaleStyle}" Width="200" Height="80" Canvas.Left="40" Canvas.Top="40" />
                              </Canvas>
                            </UserControl>
                            """;

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var button = Assert.IsType<Button>(root.FindName("Probe"));
        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 420, 240, 16);

        var transform = Assert.IsType<ScaleTransform>(button.RenderTransform);
        var inside = new Vector2(120f, 90f);
        var outside = new Vector2(10f, 10f);

        for (var i = 0; i < 5; i++)
        {
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(inside, pointerMoved: true));
            AdvanceFrames(uiRoot, 420, 240, startMs: 32 + (i * 400), frameCount: 14);

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(outside, pointerMoved: true));
            AdvanceFrames(uiRoot, 420, 240, startMs: 240 + (i * 400), frameCount: 14);
        }

        Assert.Equal(1f, transform.ScaleX, 3);
        Assert.Equal(1f, transform.ScaleY, 3);
    }

    [Fact]
    public void HoverSweepAcrossMultipleButtons_DoesNotAccumulateBaseValueFrozenLanes()
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
                                  <Style.Triggers>
                                    <EventTrigger RoutedEvent="MouseEnter">
                                      <BeginStoryboard>
                                        <Storyboard>
                                          <DoubleAnimation Storyboard.TargetProperty="RenderTransform.ScaleX" To="1.3" Duration="0:0:0.15" />
                                          <DoubleAnimation Storyboard.TargetProperty="RenderTransform.ScaleY" To="1.3" Duration="0:0:0.15" />
                                        </Storyboard>
                                      </BeginStoryboard>
                                    </EventTrigger>
                                    <EventTrigger RoutedEvent="MouseLeave">
                                      <BeginStoryboard>
                                        <Storyboard>
                                          <DoubleAnimation Storyboard.TargetProperty="RenderTransform.ScaleX" To="1.0" Duration="0:0:0.15" />
                                          <DoubleAnimation Storyboard.TargetProperty="RenderTransform.ScaleY" To="1.0" Duration="0:0:0.15" />
                                        </Storyboard>
                                      </BeginStoryboard>
                                    </EventTrigger>
                                  </Style.Triggers>
                                </Style>
                              </UserControl.Resources>
                              <Canvas>
                                <Button x:Name="Probe1" Style="{StaticResource HoverScaleStyle}" Width="80" Height="40" Canvas.Left="20" Canvas.Top="20" />
                                <Button x:Name="Probe2" Style="{StaticResource HoverScaleStyle}" Width="80" Height="40" Canvas.Left="110" Canvas.Top="20" />
                                <Button x:Name="Probe3" Style="{StaticResource HoverScaleStyle}" Width="80" Height="40" Canvas.Left="200" Canvas.Top="20" />
                                <Button x:Name="Probe4" Style="{StaticResource HoverScaleStyle}" Width="80" Height="40" Canvas.Left="290" Canvas.Top="20" />
                                <Button x:Name="Probe5" Style="{StaticResource HoverScaleStyle}" Width="80" Height="40" Canvas.Left="380" Canvas.Top="20" />
                                <Button x:Name="Probe6" Style="{StaticResource HoverScaleStyle}" Width="80" Height="40" Canvas.Left="470" Canvas.Top="20" />
                              </Canvas>
                            </UserControl>
                            """;

        AnimationManager.Current.ResetForTests();

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 640, 180, 16);

        var buttons = new[]
        {
            Assert.IsType<Button>(root.FindName("Probe1")),
            Assert.IsType<Button>(root.FindName("Probe2")),
            Assert.IsType<Button>(root.FindName("Probe3")),
            Assert.IsType<Button>(root.FindName("Probe4")),
            Assert.IsType<Button>(root.FindName("Probe5")),
            Assert.IsType<Button>(root.FindName("Probe6"))
        };

        for (var i = 0; i < buttons.Length; i++)
        {
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(GetCenter(buttons[i]), pointerMoved: true));
            RunLayout(uiRoot, 640, 180, 32 + (i * 16));
        }

        AdvanceFrames(uiRoot, 640, 180, startMs: 200, frameCount: 20);

        var telemetry = AnimationManager.Current.GetTelemetrySnapshotForTests();

        Assert.True(telemetry.ActiveLaneCount <= 2, $"Expected only the hovered button hold-end lanes to remain active. ActiveLaneCount={telemetry.ActiveLaneCount}.");
        Assert.True(telemetry.ActiveStoryboardCount <= 1, $"Expected completed leave storyboards to be cleaned up. ActiveStoryboardCount={telemetry.ActiveStoryboardCount}.");
    }

    private static void AdvanceFrames(UiRoot uiRoot, int width, int height, int startMs, int frameCount)
    {
        for (var i = 0; i < frameCount; i++)
        {
            RunLayout(uiRoot, width, height, startMs + (i * 16));
        }
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

    private static Vector2 GetCenter(FrameworkElement element)
    {
        var slot = element.LayoutSlot;
        return new Vector2(slot.X + (slot.Width * 0.5f), slot.Y + (slot.Height * 0.5f));
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }
}
