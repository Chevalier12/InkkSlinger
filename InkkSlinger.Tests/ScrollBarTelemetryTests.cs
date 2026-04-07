using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ScrollBarTelemetryTests
{
    [Fact]
    public void ScrollBar_ThumbDragTelemetry_CapturesVerticalDragPipeline_AndResets()
    {
        _ = ScrollBar.GetThumbDragTelemetryAndReset();
        _ = Track.GetThumbTravelTelemetryAndReset();
        _ = Thumb.GetDragTelemetryAndReset();

        var (uiRoot, scrollBar) = BuildStandaloneScrollBar();
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

        var scrollBarTelemetry = ScrollBar.GetThumbDragTelemetryAndReset();
        var trackTelemetry = Track.GetThumbTravelTelemetryAndReset();
        var thumbTelemetry = Thumb.GetDragTelemetryAndReset();

        Assert.True(scrollBarTelemetry.OnThumbDragDeltaCallCount > 0);
        Assert.True(scrollBarTelemetry.OnThumbDragDeltaMilliseconds >= 0d);
        Assert.True(scrollBarTelemetry.OnThumbDragDeltaValueSetMilliseconds >= 0d);
        Assert.True(scrollBarTelemetry.OnValueChangedBaseMilliseconds >= 0d);
        Assert.True(scrollBarTelemetry.OnValueChangedSyncTrackStateMilliseconds >= 0d);
        Assert.True(scrollBarTelemetry.SyncTrackStateMilliseconds >= 0d);
        Assert.True(scrollBarTelemetry.RefreshTrackLayoutMilliseconds >= 0d);

        Assert.True(trackTelemetry.GetValueFromThumbTravelCallCount > 0);
        Assert.True(trackTelemetry.GetValueFromThumbTravelMilliseconds >= 0d);
        Assert.True(trackTelemetry.RefreshLayoutForStateMutationCallCount > 0);
        Assert.True(trackTelemetry.RefreshLayoutValueMutationCallCount > 0);
        Assert.True(trackTelemetry.RefreshLayoutForStateMutationMilliseconds >= 0d);

        Assert.True(thumbTelemetry.HandlePointerMoveCallCount > 0);
        Assert.True(thumbTelemetry.HandlePointerMoveMilliseconds >= 0d);
        Assert.True(thumbTelemetry.RaiseDragDeltaMilliseconds >= 0d);

        var clearedScrollBarTelemetry = ScrollBar.GetThumbDragTelemetryAndReset();
        var clearedTrackTelemetry = Track.GetThumbTravelTelemetryAndReset();
        var clearedThumbTelemetry = Thumb.GetDragTelemetryAndReset();

        Assert.Equal(0, clearedScrollBarTelemetry.OnThumbDragDeltaCallCount);
        Assert.Equal(0, clearedTrackTelemetry.GetValueFromThumbTravelCallCount);
        Assert.Equal(0, clearedTrackTelemetry.RefreshLayoutForStateMutationCallCount);
        Assert.Equal(0, clearedThumbTelemetry.HandlePointerMoveCallCount);
    }

    [Fact]
    public void ScrollBar_ThumbDragTelemetry_CapturesHorizontalDragPath()
    {
        _ = ScrollBar.GetThumbDragTelemetryAndReset();
        _ = Track.GetThumbTravelTelemetryAndReset();
        _ = Thumb.GetDragTelemetryAndReset();

        var (uiRoot, scrollBar) = BuildStandaloneScrollBar(Orientation.Horizontal, width: 220f, height: 18f);
        RunLayout(uiRoot, 300, 120);

        var thumb = FindNamedVisualChild<Thumb>(scrollBar, "PART_Thumb");
        Assert.NotNull(thumb);

        var start = GetCenter(scrollBar.GetThumbRectForInput());
        var end = new Vector2(scrollBar.LayoutSlot.X + scrollBar.LayoutSlot.Width - 20f, start.Y);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, leftReleased: true));

        Assert.True(scrollBar.Value > 60f);

        var scrollBarTelemetry = ScrollBar.GetThumbDragTelemetryAndReset();
        var trackTelemetry = Track.GetThumbTravelTelemetryAndReset();
        var thumbTelemetry = Thumb.GetDragTelemetryAndReset();

        Assert.True(scrollBarTelemetry.OnThumbDragDeltaCallCount > 0);
        Assert.True(trackTelemetry.GetValueFromThumbTravelCallCount > 0);
        Assert.True(thumbTelemetry.HandlePointerMoveCallCount > 0);
    }

    [Fact]
    public void Track_RuntimeAndAggregateTelemetry_CaptureLayoutHitTestingAndDirtyHintPaths()
    {
        _ = Track.GetTelemetryAndReset();

        var track = new Track
        {
            Width = 18f,
            Height = 180f,
            Minimum = 0f,
            Maximum = 100f,
            Value = 30f,
            ViewportSize = 20f,
            BorderThickness = new Thickness(1f)
        };

        var decreaseButton = new RepeatButton { Height = 18f };
        var thumb = new Thumb();
        var increaseButton = new RepeatButton { Height = 18f };
        Track.SetPartRole(decreaseButton, TrackPartRole.DecreaseButton);
        Track.SetPartRole(thumb, TrackPartRole.Thumb);
        Track.SetPartRole(increaseButton, TrackPartRole.IncreaseButton);
        track.AddChild(decreaseButton);
        track.AddChild(thumb);
        track.AddChild(increaseButton);

        var host = new Canvas { Width = 120f, Height = 240f };
        host.AddChild(track);
        Canvas.SetLeft(track, 20f);
        Canvas.SetTop(track, 20f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 160, 260);

        _ = track.GetTrackRect();
        var thumbRect = track.GetThumbRect();
        _ = track.GetThumbTravel();
        var thumbCenter = GetCenter(thumbRect);
        _ = track.GetValuePosition(track.Value);
        _ = track.GetValueFromPoint(thumbCenter, useThumbCenterOffset: true);
        _ = track.HitTestDecreaseRegion(new Vector2(thumbCenter.X, track.LayoutSlot.Y + 2f));
        _ = track.HitTestIncreaseRegion(new Vector2(thumbCenter.X, track.LayoutSlot.Y + track.LayoutSlot.Height - 2f));
        track.Value = 70f;
        var hintProvider = (IRenderDirtyBoundsHintProvider)track;
        _ = hintProvider.TryConsumeRenderDirtyBoundsHint(out var dirtyBounds);

        var runtime = track.GetTrackSnapshotForDiagnostics();
        var aggregate = Track.GetTelemetryAndReset();
        var cleared = Track.GetTelemetryAndReset();

        Assert.True(runtime.GetTrackRectCallCount > 0);
        Assert.True(runtime.GetThumbRectCallCount > 0);
        Assert.True(runtime.GetThumbTravelCallCount > 0);
        Assert.True(runtime.GetValueFromPointCallCount > 0);
        Assert.True(runtime.HitTestDecreaseRegionCallCount > 0);
        Assert.True(runtime.HitTestIncreaseRegionCallCount > 0);
        Assert.True(runtime.TryConsumeRenderDirtyBoundsHintCallCount > 0);
        Assert.True(runtime.RefreshLayoutForStateMutationCallCount > 0);

        Assert.True(aggregate.GetTrackRectCallCount > 0);
        Assert.True(aggregate.GetThumbRectCallCount > 0);
        Assert.True(aggregate.GetThumbTravelCallCount > 0);
        Assert.True(aggregate.GetValueFromPointCallCount > 0);
        Assert.True(aggregate.GetValueFromPointThumbCenterOffsetCount > 0);
        Assert.True(aggregate.HitTestDecreaseRegionCallCount > 0);
        Assert.True(aggregate.HitTestIncreaseRegionCallCount > 0);
        Assert.True(aggregate.TryConsumeRenderDirtyBoundsHintCallCount > 0);
        Assert.True(aggregate.RefreshLayoutForStateMutationCallCount > 0);

        Assert.Equal(0, cleared.GetTrackRectCallCount);
        Assert.Equal(0, cleared.GetValueFromPointCallCount);
        Assert.Equal(0, cleared.TryConsumeRenderDirtyBoundsHintCallCount);
    }

    private static (UiRoot UiRoot, ScrollBar ScrollBar) BuildStandaloneScrollBar(
        Orientation orientation = Orientation.Vertical,
        float width = 18f,
        float height = 200f)
    {
        var host = new Canvas
        {
            Width = MathF.Max(160f, width + 40f),
            Height = MathF.Max(240f, height + 40f)
        };
        var scrollBar = new ScrollBar
        {
            Orientation = orientation,
            Width = width,
            Height = height,
            Minimum = 0f,
            Maximum = 100f,
            ViewportSize = 20f
        };

        host.AddChild(scrollBar);
        Canvas.SetLeft(scrollBar, 20f);
        Canvas.SetTop(scrollBar, 20f);

        return (new UiRoot(host), scrollBar);
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
