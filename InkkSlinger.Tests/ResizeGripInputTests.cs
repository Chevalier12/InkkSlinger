using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ResizeGripInputTests
{
    [Fact]
    public void PointerDrag_ResizesExplicitTarget_AndReleasesPointerCapture()
    {
        var (uiRoot, target, grip) = CreateExplicitTargetFixture();
        var start = GetCenter(grip.LayoutSlot);
        var end = new Vector2(start.X + 37f, start.Y + 22f);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, leftPressed: true));

        Assert.True(grip.IsDragging);
        Assert.Same(grip, FocusManager.GetCapturedPointerElement());

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, pointerMoved: true));
        RunLayout(uiRoot, 420, 280, 32);

        Assert.Equal(140f, target.Width, 0.01f);
        Assert.Equal(120f, target.Height, 0.01f);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, leftReleased: true));

        Assert.False(grip.IsDragging);
        Assert.Null(FocusManager.GetCapturedPointerElement());
    }

    [Fact]
    public void PointerDrag_ClampsToTargetBounds()
    {
        var (uiRoot, target, grip) = CreateExplicitTargetFixture();
        var start = GetCenter(grip.LayoutSlot);
        var end = new Vector2(start.X + 300f, start.Y + 300f);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, pointerMoved: true));
        RunLayout(uiRoot, 420, 280, 32);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, leftReleased: true));

        Assert.Equal(180f, target.Width, 0.01f);
        Assert.Equal(150f, target.Height, 0.01f);
    }

    [Fact]
    public void PointerMove_TogglesHoverState()
    {
        var (uiRoot, _, grip) = CreateExplicitTargetFixture();
        var inside = GetCenter(grip.LayoutSlot);
        var outside = new Vector2(grip.LayoutSlot.X - 24f, grip.LayoutSlot.Y - 24f);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(inside, pointerMoved: true));
        Assert.True(grip.IsMouseOver);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(outside, pointerMoved: true));
        Assert.False(grip.IsMouseOver);
    }

    [Fact]
    public void XamlLoader_ResolvesResizeGripTargetReference()
    {
        const string xaml = """
<UserControl xmlns="urn:inkkslinger-ui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <Border x:Name="Resizable" Width="100" Height="80" />
    <ResizeGrip x:Name="Grip" Target="{x:Reference Resizable}" />
  </Grid>
</UserControl>
""";

        var root = Assert.IsType<UserControl>(XamlLoader.LoadFromString(xaml));
        var target = Assert.IsType<Border>(root.FindName("Resizable"));
        var grip = Assert.IsType<ResizeGrip>(root.FindName("Grip"));

        Assert.Same(target, grip.Target);
    }

    [Fact]
    public void ResizeGripView_LoadsWithNumericSizeBindings()
    {
        var snapshot = CaptureApplicationResources();
        try
        {
            TestApplicationResources.LoadDemoAppResources();
            var view = new ResizeGripView();

            Assert.NotNull(view.FindName("PrimaryResizableCard"));
            Assert.NotNull(view.FindName("FloatingResizePopup"));
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    private static (UiRoot UiRoot, Border Target, ResizeGrip Grip) CreateExplicitTargetFixture()
    {
        var host = new Canvas
        {
            Width = 420f,
            Height = 280f
        };

        var target = new Border
        {
            Width = 100f,
            Height = 100f,
            MinWidth = 80f,
            MinHeight = 70f,
            MaxWidth = 180f,
            MaxHeight = 150f
        };

        var grip = new ResizeGrip
        {
            Target = target,
            GripSize = 20f,
            ResizeIncrement = 10f,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        };

        var grid = new Grid();
        grid.AddChild(target);
        grid.AddChild(grip);
        host.AddChild(grid);
        Canvas.SetLeft(grid, 24f);
        Canvas.SetTop(grid, 24f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 420, 280, 16);
        return (uiRoot, target, grip);
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));
    }

    private static InputDelta CreatePointerDelta(
        Vector2 pointer,
        bool pointerMoved = false,
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
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
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

    private static ResourceSnapshot CaptureApplicationResources()
    {
        var resources = UiApplication.Current.Resources;
        return new ResourceSnapshot(
            resources.ToList(),
            resources.MergedDictionaries.ToList());
    }

    private static void RestoreApplicationResources(ResourceSnapshot snapshot)
    {
        TestApplicationResources.Restore(snapshot.Entries, snapshot.MergedDictionaries);
    }

    private sealed record ResourceSnapshot(
        List<KeyValuePair<object, object>> Entries,
        List<ResourceDictionary> MergedDictionaries);
}
