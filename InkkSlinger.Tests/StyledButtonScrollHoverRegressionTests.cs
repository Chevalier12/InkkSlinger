using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;
using InkkSlinger.Tests.TestDoubles;

namespace InkkSlinger.Tests;

/// <summary>
/// Regression tests for CPU spike when hovering styled buttons (with DropShadowEffect
/// and storyboard hover animations) in a ScrollViewer during wheel scroll.
///
/// Prior analysis found that no existing test exercised the styled button +
/// DropShadowEffect + wheel scroll scenario - both scroll tests and hover tests
/// used plain buttons. DropShadowEffect.* instrumentation was never triggered.
/// </summary>
public sealed class StyledButtonScrollHoverRegressionTests
{
    [Fact]
    public void WheelScroll_WithStationaryPointer_OverStyledButtons_CapturesInstrumentation()
    {
        var (root, scrollViewer, buttons) = BuildStyledButtonScrollSurface();
        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 260, 16);

        // Find a point that actually hits button 1 (should be in initial viewport)
        var targetButton = buttons[1];
        var pointer = FindPointHittingTarget(root, targetButton, 320, 260);

        // Move to establish initial hover
        MovePointer(uiRoot, pointer);
        RunLayout(uiRoot, 320, 260, 16);

        if (!targetButton.IsMouseOver)
        {
            // Fallback: try button 0
            targetButton = buttons[0];
            pointer = FindPointHittingTarget(root, targetButton, 320, 260);
            MovePointer(uiRoot, pointer);
            RunLayout(uiRoot, 320, 260, 16);
        }

        Assert.True(targetButton.IsMouseOver, $"Target button should be hovered before wheel scroll. IsMouseOver={targetButton.IsMouseOver}");

        // Capture instrumentation during wheel scroll + hover
        using var capture = new InstrumentationCapture();

        // Do multiple wheel ticks while pointer is stationary over styled button
        const int tickCount = 10;
        for (var i = 0; i < tickCount; i++)
        {
            Wheel(uiRoot, pointer, delta: -120);
            RunLayout(uiRoot, 320, 260, 16);
        }

        var lines = capture.GetInstrumentLines();

        // Parse instrumentation
        var timings = lines.Select(l => InstrumentationCapture.TryParseTiming(l)).Where(t => t.HasValue).Select(t => t!.Value).ToList();
        var counters = lines.Select(l => InstrumentationCapture.TryParseCounter(l)).Where(c => c.HasValue).Select(c => c!.Value).ToList();

        Console.WriteLine($"[METRICS] Raw instrument lines captured: {lines.Count}");
        foreach (var line in lines)
        {
            Console.WriteLine(line);
        }

        // Report slowest methods
        Console.WriteLine("\n[METRICS] SLOWEST methods:");
        foreach (var timing in timings.OrderByDescending(t => t.microseconds).Take(10))
        {
            Console.WriteLine($"  {timing.method} = {timing.microseconds}us");
        }

        // Report hottest methods (most calls)
        Console.WriteLine("\n[METRICS] HOTTEST methods (call counts):");
        var groupedCounters = counters.GroupBy(c => c.method).Select(g => (method: g.Key, totalCalls: g.Sum(x => x.count))).OrderByDescending(x => x.totalCalls).ToList();
        foreach (var counter in groupedCounters.Take(10))
        {
            Console.WriteLine($"  {counter.method} = {counter.totalCalls} calls");
        }

        // Check DropShadowEffect instrumentation
        var dropShadowLines = lines.Where(l => l.Contains("DropShadowEffect")).ToList();
        Console.WriteLine($"\n[METRICS] DropShadowEffect lines: {dropShadowLines.Count}");
        foreach (var line in dropShadowLines.Take(5))
        {
            Console.WriteLine($"  {line}");
        }

        // Check MarkFullFrameDirty
        var fullFrameDirtyLines = lines.Where(l => l.Contains("MarkFullFrameDirty")).ToList();
        Console.WriteLine($"\n[METRICS] MarkFullFrameDirty calls: {fullFrameDirtyLines.Count}");
        foreach (var line in fullFrameDirtyLines.Take(5))
        {
            Console.WriteLine($"  {line}");
        }

        // Check BeginStoryboard.Invoke
        var beginStoryboardLines = lines.Where(l => l.Contains("BeginStoryboard")).ToList();
        Console.WriteLine($"\n[METRICS] BeginStoryboard.Invoke calls: {beginStoryboardLines.Count}");
        foreach (var line in beginStoryboardLines.Take(5))
        {
            Console.WriteLine($"  {line}");
        }

        // Verify scroll happened
        Assert.True(scrollViewer.VerticalOffset > 0.01f, "ScrollViewer should have scrolled");

        // Verify hover transitioned
        var afterHit = VisualTreeHelper.HitTest(root, pointer);
        var afterButton = FindAncestor<Button>(afterHit);
        Assert.NotNull(afterButton);
    }

    [Fact]
    public void HoverEnterLeave_WithStyledButtons_CapturesDropShadowEffectInstrumentation()
    {
        var (root, scrollViewer, buttons) = BuildStyledButtonScrollSurface();
        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 260, 16);

        // Find a point that hits button 0 (at top of scroll viewer)
        var targetButton = buttons[0];
        var pointer = FindPointHittingTarget(root, targetButton, 320, 260);

        // Move to gutter first
        MovePointer(uiRoot, new Vector2(4f, 4f));
        RunLayout(uiRoot, 320, 260, 16);

        // Capture during hover sequence
        using var capture = new InstrumentationCapture();

        // Enter button hover
        MovePointer(uiRoot, pointer);
        RunLayout(uiRoot, 320, 260, 32);

        // Leave button hover
        MovePointer(uiRoot, new Vector2(4f, 4f));
        RunLayout(uiRoot, 320, 260, 64);

        var lines = capture.GetInstrumentLines();

        // Parse instrumentation
        var timings = lines.Select(l => InstrumentationCapture.TryParseTiming(l)).Where(t => t.HasValue).Select(t => t!.Value).ToList();
        var counters = lines.Select(l => InstrumentationCapture.TryParseCounter(l)).Where(c => c.HasValue).Select(c => c!.Value).ToList();

        Console.WriteLine($"[METRICS] Raw instrument lines captured: {lines.Count}");

        // Report slowest methods
        Console.WriteLine("\n[METRICS] SLOWEST methods:");
        foreach (var timing in timings.OrderByDescending(t => t.microseconds).Take(5))
        {
            Console.WriteLine($"  {timing.method} = {timing.microseconds}us");
        }

        // Report hottest methods
        Console.WriteLine("\n[METRICS] HOTTEST methods:");
        var groupedCounters = counters.GroupBy(c => c.method).Select(g => (method: g.Key, totalCalls: g.Sum(x => x.count))).OrderByDescending(x => x.totalCalls).ToList();
        foreach (var counter in groupedCounters.Take(5))
        {
            Console.WriteLine($"  {counter.method} = {counter.totalCalls} calls");
        }

        // Check DropShadowEffect instrumentation was captured
        var dropShadowLines = lines.Where(l => l.Contains("DropShadowEffect")).ToList();
        Console.WriteLine($"\n[METRICS] DropShadowEffect lines: {dropShadowLines.Count}");
        foreach (var line in dropShadowLines.Take(10))
        {
            Console.WriteLine($"  {line}");
        }

        // Check BeginStoryboard.Invoke
        var beginStoryboardLines = lines.Where(l => l.Contains("BeginStoryboard")).ToList();
        Console.WriteLine($"\n[METRICS] BeginStoryboard.Invoke calls: {beginStoryboardLines.Count}");
        foreach (var line in beginStoryboardLines.Take(5))
        {
            Console.WriteLine($"  {line}");
        }

        // Verify hover worked
        Assert.True(targetButton.IsMouseOver, "Target button should be hovered");
    }

    [Fact]
    public void StyledButtons_ScrollAndHover_TrackDirtyBoundsCoverage()
    {
        var (root, scrollViewer, buttons) = BuildStyledButtonScrollSurface();
        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 260, 16);

        // Find a point that hits button 1
        var targetButton = buttons[1];
        var pointer = FindPointHittingTarget(root, targetButton, 320, 260);

        MovePointer(uiRoot, pointer);
        RunLayout(uiRoot, 320, 260, 16);

        // Capture during wheel + hover
        using var capture = new InstrumentationCapture();

        Wheel(uiRoot, pointer, delta: -120);
        RunLayout(uiRoot, 320, 260, 16);

        var lines = capture.GetInstrumentLines();

        // Parse TrackDirtyBoundsForVisual calls
        var trackDirtyLines = lines.Where(l => l.Contains("TrackDirtyBoundsForVisual")).ToList();
        Console.WriteLine($"[METRICS] TrackDirtyBoundsForVisual lines: {trackDirtyLines.Count}");
        foreach (var line in trackDirtyLines.Take(5))
        {
            Console.WriteLine($"  {line}");
        }

        // Parse AddDirtyBounds calls
        var addDirtyLines = lines.Where(l => l.Contains("AddDirtyBounds")).ToList();
        Console.WriteLine($"\n[METRICS] AddDirtyBounds lines: {addDirtyLines.Count}");
        foreach (var line in addDirtyLines.Take(5))
        {
            Console.WriteLine($"  {line}");
        }

        // Parse AddDirtyRegion calls
        var addDirtyRegionLines = lines.Where(l => l.Contains("AddDirtyRegion")).ToList();
        Console.WriteLine($"\n[METRICS] AddDirtyRegion lines: {addDirtyRegionLines.Count}");

        // Verify scroll happened
        Assert.True(scrollViewer.VerticalOffset > 0.01f, "ScrollViewer should have scrolled");
    }

    private static (UserControl Root, ScrollViewer ScrollViewer, IReadOnlyList<Button> Buttons) BuildStyledButtonScrollSurface()
    {
        const string xaml = """
            <UserControl xmlns="urn:inkkslinger-ui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <UserControl.Resources>
                <Style x:Key="StyledButtonStyle" TargetType="{x:Type Button}">
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
              <ScrollViewer x:Name="scrollViewer"
                            Width="300"
                            Height="220"
                            VerticalScrollBarVisibility="Auto"
                            HorizontalScrollBarVisibility="Disabled">
                <StackPanel x:Name="ButtonHost" />
              </ScrollViewer>
            </UserControl>
            """;

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var scrollViewer = Assert.IsType<ScrollViewer>(root.FindName("scrollViewer"));
        var host = Assert.IsType<StackPanel>(root.FindName("ButtonHost"));
        var style = Assert.IsType<Style>(root.Resources["StyledButtonStyle"]);

        var buttons = new List<Button>();
        for (var i = 0; i < 12; i++)
        {
            var button = new Button
            {
                Content = $"Styled Button {i}",
                Height = 44f,
                Margin = new Thickness(0, 0, 0, 4),
                Style = style
            };
            host.AddChild(button);
            buttons.Add(button);
        }

        return (root, scrollViewer, buttons);
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

    private static void MovePointer(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreateInputDelta(pointer, pointerMoved: true));
    }

    private static void Wheel(UiRoot uiRoot, Vector2 pointer, int delta)
    {
        uiRoot.RunInputDeltaForTests(CreateInputDelta(pointer, wheelDelta: delta));
    }

    private static InputDelta CreateInputDelta(
        Vector2 pointer,
        bool pointerMoved = false,
        int wheelDelta = 0,
        bool leftPressed = false,
        bool leftReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = pointerMoved,
            WheelDelta = wheelDelta,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static TElement? FindAncestor<TElement>(UIElement? start)
        where TElement : UIElement
    {
        for (var current = start; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is TElement typed)
            {
                return typed;
            }
        }

        return null;
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }
}