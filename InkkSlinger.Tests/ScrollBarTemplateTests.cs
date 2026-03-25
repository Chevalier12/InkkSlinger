using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ScrollBarTemplateTests
{
    [Fact]
    public void ApplyTemplate_MissingThumbPart_Throws()
    {
        var scrollBar = new ScrollBar();

        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            scrollBar.Template = new ControlTemplate(static _ =>
            {
                var track = new Track
                {
                    Name = "PART_Track"
                };
                track.AddChild(new RepeatButton { Name = "PART_LineUpButton" });
                track.AddChild(new RepeatButton { Name = "PART_LineDownButton" });
                return track;
            })
            {
                TargetType = typeof(ScrollBar)
            };
        });

        Assert.Contains("PART_Thumb", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Layout_ComputesThumbSizeAndPosition_FromViewportAndValue()
    {
        var host = new Canvas
        {
            Width = 320f,
            Height = 260f
        };
        var vertical = new ScrollBar
        {
            Width = 16f,
            Height = 200f,
            Minimum = 0f,
            Maximum = 200f,
            ViewportSize = 50f,
            Value = 0f
        };
        var horizontal = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Width = 220f,
            Height = 16f,
            Minimum = 0f,
            Maximum = 200f,
            ViewportSize = 50f,
            Value = 100f
        };

        host.AddChild(vertical);
        host.AddChild(horizontal);
        Canvas.SetLeft(vertical, 24f);
        Canvas.SetTop(vertical, 24f);
        Canvas.SetLeft(horizontal, 56f);
        Canvas.SetTop(horizontal, 24f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 320, 260);

        var verticalThumb = vertical.GetThumbRectForInput();
        var verticalUpButton = FindNamedVisualChild<RepeatButton>(vertical, "PART_LineUpButton");
        var verticalDownButton = FindNamedVisualChild<RepeatButton>(vertical, "PART_LineDownButton");
        Assert.NotNull(verticalUpButton);
        Assert.NotNull(verticalDownButton);

        var verticalTrackLength = vertical.LayoutSlot.Height - verticalUpButton!.LayoutSlot.Height - verticalDownButton!.LayoutSlot.Height;
        var expectedVerticalThumbHeight = MathF.Max(14f, verticalTrackLength * 0.25f);
        Assert.True(MathF.Abs(verticalThumb.Height - expectedVerticalThumbHeight) <= 0.05f);
        Assert.True(verticalThumb.Y >= verticalUpButton.LayoutSlot.Y + verticalUpButton.LayoutSlot.Height - 0.05f);

        var horizontalThumb = horizontal.GetThumbRectForInput();
        var horizontalUpButton = FindNamedVisualChild<RepeatButton>(horizontal, "PART_LineUpButton");
        var horizontalDownButton = FindNamedVisualChild<RepeatButton>(horizontal, "PART_LineDownButton");
        Assert.NotNull(horizontalUpButton);
        Assert.NotNull(horizontalDownButton);

        var horizontalTrackLength = horizontal.LayoutSlot.Width - horizontalUpButton!.LayoutSlot.Width - horizontalDownButton!.LayoutSlot.Width;
        var expectedHorizontalThumbWidth = MathF.Max(14f, horizontalTrackLength * 0.25f);
        Assert.True(MathF.Abs(horizontalThumb.Width - expectedHorizontalThumbWidth) <= 0.05f);
        Assert.True(horizontalThumb.X > horizontalUpButton.LayoutSlot.X + horizontalUpButton.LayoutSlot.Width);
    }

    [Fact]
    public void LineButtons_UseSmallChange()
    {
        var (uiRoot, host, scrollBar) = BuildStandaloneScrollBar();
        _ = host;
        scrollBar.SmallChange = 7f;
        scrollBar.Value = 20f;
        RunLayout(uiRoot, 160, 240);

        var lineDownButton = FindNamedVisualChild<RepeatButton>(scrollBar, "PART_LineDownButton");
        var lineUpButton = FindNamedVisualChild<RepeatButton>(scrollBar, "PART_LineUpButton");
        Assert.NotNull(lineDownButton);
        Assert.NotNull(lineUpButton);

        Click(uiRoot, GetCenter(lineDownButton!.LayoutSlot));
        Assert.True(MathF.Abs(scrollBar.Value - 27f) <= 0.01f);

        Click(uiRoot, GetCenter(lineUpButton!.LayoutSlot));
        Assert.True(MathF.Abs(scrollBar.Value - 20f) <= 0.01f);
    }

    [Fact]
    public void TrackClick_UsesLargeChange()
    {
        var (uiRoot, _, scrollBar) = BuildStandaloneScrollBar();
        scrollBar.LargeChange = 24f;
        scrollBar.Value = 10f;
        RunLayout(uiRoot, 160, 240);

        var thumb = scrollBar.GetThumbRectForInput();
        var increasePoint = new Vector2(GetCenter(thumb).X, thumb.Y + thumb.Height + 6f);

        Click(uiRoot, increasePoint);

        Assert.True(MathF.Abs(scrollBar.Value - 34f) <= 0.01f);
    }

    [Fact]
    public void ThumbDrag_UpdatesValue_AndCapturesThumb()
    {
        var (uiRoot, _, scrollBar) = BuildStandaloneScrollBar();
        scrollBar.Value = 0f;
        RunLayout(uiRoot, 160, 240);

        var thumb = FindNamedVisualChild<Thumb>(scrollBar, "PART_Thumb");
        Assert.NotNull(thumb);

        var start = GetCenter(scrollBar.GetThumbRectForInput());
        var end = new Vector2(start.X, scrollBar.LayoutSlot.Y + scrollBar.LayoutSlot.Height - 20f);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, leftPressed: true));
        Assert.Same(thumb, FocusManager.GetCapturedPointerElement());

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, leftReleased: true));

        Assert.True(scrollBar.Value > 60f);
        Assert.Null(FocusManager.GetCapturedPointerElement());
    }

    [Fact]
    public void Value_IsClampedToScrollableRange()
    {
        var scrollBar = new ScrollBar
        {
            Minimum = 0f,
            Maximum = 100f,
            ViewportSize = 30f,
            Value = 200f
        };

        Assert.True(MathF.Abs(scrollBar.Value - 70f) <= 0.01f);
    }

    [Fact]
    public void TrackValueMutation_UsesLocalizedDirtyBoundsHint()
    {
        var (uiRoot, _, scrollBar) = BuildStandaloneScrollBar();
        RunLayout(uiRoot, 160, 240);

        var track = FindNamedVisualChild<Track>(scrollBar, "PART_Track");
        Assert.NotNull(track);

        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 160f, 240f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        uiRoot.CompleteDrawStateForTests();

        track!.Value = 35f;

        var dirtyRegions = uiRoot.GetDirtyRegionsSnapshotForTests();

        Assert.Single(dirtyRegions);
        Assert.True(dirtyRegions[0].Height < track.LayoutSlot.Height, $"Expected a localized dirty region, got {dirtyRegions[0]} for track height {track.LayoutSlot.Height}.");
    }

    private static (UiRoot UiRoot, Canvas Host, ScrollBar ScrollBar) BuildStandaloneScrollBar()
    {
        var host = new Canvas
        {
            Width = 160f,
            Height = 240f
        };
        var scrollBar = new ScrollBar
        {
            Width = 18f,
            Height = 200f,
            Minimum = 0f,
            Maximum = 100f,
            ViewportSize = 20f
        };
        host.AddChild(scrollBar);
        Canvas.SetLeft(scrollBar, 20f);
        Canvas.SetTop(scrollBar, 20f);

        var uiRoot = new UiRoot(host);
        return (uiRoot, host, scrollBar);
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

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));
    }

    private static void Click(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));
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
            PointerMoved = pointerMoved || leftPressed || leftReleased,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, width, height));
    }
}
