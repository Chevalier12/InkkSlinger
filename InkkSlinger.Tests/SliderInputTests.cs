using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class SliderInputTests
{
    [Fact]
    public void DraggingThumb_ShouldUpdateValue()
    {
        var host = new Canvas
        {
            Width = 460f,
            Height = 220f
        };

        var slider = new Slider
        {
            Width = 240f,
            Height = 28f,
            Minimum = -200f,
            Maximum = 200f,
            Value = 0f
        };
        host.AddChild(slider);
        Canvas.SetLeft(slider, 40f);
        Canvas.SetTop(slider, 80f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        var y = slider.LayoutSlot.Y + (slider.LayoutSlot.Height / 2f);
        var downPoint = new Vector2(slider.LayoutSlot.X + (slider.LayoutSlot.Width / 2f), y);
        var movePoint = new Vector2(slider.LayoutSlot.X + slider.LayoutSlot.Width - 8f, y);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(downPoint, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(movePoint, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(movePoint, leftReleased: true));

        Assert.True(slider.Value > 100f);
    }

    [Fact]
    public void HorizontalKeyboardInput_UsesWpfStepBindings()
    {
        var slider = CreateSlider();
        var uiRoot = CreateUiRoot(slider);
        slider.SmallChange = 2f;
        slider.Value = 4f;

        uiRoot.SetFocusedElementForTests(slider);
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Right));
        Assert.Equal(6f, slider.Value);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Left));
        Assert.Equal(4f, slider.Value);
    }

    [Fact]
    public void VerticalKeyboardInput_DefaultsToBottomToTopValueFlow()
    {
        var slider = CreateSlider();
        slider.Orientation = Orientation.Vertical;
        slider.SmallChange = 5f;
        slider.Value = 20f;

        var uiRoot = CreateUiRoot(slider);
        uiRoot.SetFocusedElementForTests(slider);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Up));
        Assert.Equal(25f, slider.Value);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Down));
        Assert.Equal(20f, slider.Value);
    }

    [Fact]
    public void ReversedHorizontalKeyboardInput_FlipsArrowSemantics()
    {
        var slider = CreateSlider();
        slider.IsDirectionReversed = true;
        slider.SmallChange = 5f;
        slider.Value = 50f;

        var uiRoot = CreateUiRoot(slider);
        uiRoot.SetFocusedElementForTests(slider);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Right));
        Assert.Equal(45f, slider.Value);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Left));
        Assert.Equal(50f, slider.Value);
    }

    [Fact]
    public void MoveToPointEnabled_ClickingTrackMovesThumbToPointer()
    {
        var slider = CreateSlider();
        slider.IsMoveToPointEnabled = true;
        slider.Value = 0f;

        var uiRoot = CreateUiRoot(slider);
        var pointer = new Vector2(slider.LayoutSlot.X + (slider.LayoutSlot.Width * 0.8f), slider.LayoutSlot.Y + (slider.LayoutSlot.Height / 2f));

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));

        Assert.True(slider.Value > 70f);
    }

    [Fact]
    public void SnapToTick_UsesExplicitTicksCollection()
    {
        var slider = CreateSlider();
        slider.IsSnapToTickEnabled = true;
        slider.Ticks.Add(0d);
        slider.Ticks.Add(25d);
        slider.Ticks.Add(60d);
        slider.Ticks.Add(100d);

        slider.Value = 54f;

        Assert.Equal(60f, slider.Value);
    }

    [Fact]
    public void SelectionRangeEnabled_UpdatesSelectionRangeTemplatePart()
    {
        var slider = CreateSlider();
        slider.IsSelectionRangeEnabled = true;
        slider.SelectionStart = 10f;
        slider.SelectionEnd = 90f;

        var uiRoot = CreateUiRoot(slider);
        _ = uiRoot;

        var selectionRange = FindDescendantByName<FrameworkElement>(slider, "PART_SelectionRange");
        Assert.NotNull(selectionRange);
        Assert.Equal(Visibility.Visible, selectionRange!.Visibility);
        Assert.True(selectionRange.Margin.Left > 0f);
        Assert.True(selectionRange.Margin.Right > 0f);
        Assert.True(selectionRange.Margin.Top >= 0f);
        Assert.True(selectionRange.Margin.Bottom >= 0f);
    }

    [Fact]
    public void DraggingThumb_WithAutoToolTip_ShowsAndClosesPopup()
    {
        var host = new Canvas
        {
            Width = 460f,
            Height = 220f
        };
        var slider = CreateSlider();
        slider.AutoToolTipPlacement = AutoToolTipPlacement.TopLeft;
        host.AddChild(slider);
        Canvas.SetLeft(slider, 40f);
        Canvas.SetTop(slider, 80f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        var thumb = FindDescendantByName<Thumb>(slider, "PART_Thumb");
        Assert.NotNull(thumb);
        var start = GetCenter(thumb!.LayoutSlot);
        var end = new Vector2(start.X + 32f, start.Y);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, pointerMoved: true));

        var toolTip = host.Children.OfType<ToolTip>().SingleOrDefault();
        Assert.NotNull(toolTip);
        Assert.True(toolTip!.IsOpen);
        var content = Assert.IsType<TextBlock>(toolTip.Content);
        Assert.False(string.IsNullOrWhiteSpace(content.Text));

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, leftReleased: true));
        Assert.False(toolTip.IsOpen);
    }

    [Fact]
    public void Thumb_IsCenteredOnTrack_UsingThumbSizeAcrossTrackThickness()
    {
        var slider = CreateSlider();
        slider.Value = 50f;

        var uiRoot = CreateUiRoot(slider);
        _ = uiRoot;

        var thumb = FindDescendantByName<Thumb>(slider, "PART_Thumb");
        var track = FindDescendantByName<Track>(slider, "PART_Track");
        Assert.NotNull(thumb);
        Assert.NotNull(track);

        var trackRect = track!.GetTrackRect();
        var thumbRect = thumb!.LayoutSlot;

        Assert.Equal(slider.ThumbSize, thumbRect.Height, 3);
        Assert.Equal(trackRect.Y + (trackRect.Height / 2f), thumbRect.Y + (thumbRect.Height / 2f), 3);
    }

    [Fact]
    public void DraggingThumb_FromVisibleThumbLowerEdge_StartsDragReliably()
    {
        var slider = CreateSlider();
        slider.Minimum = -200f;
        slider.Maximum = 200f;
        slider.Value = 0f;

        var uiRoot = CreateUiRoot(slider);
        var track = FindDescendantByName<Track>(slider, "PART_Track");
        Assert.NotNull(track);

        var trackRect = track!.GetTrackRect();
        var thumbCenterX = track.GetValuePosition(slider.Value);
        var start = new Vector2(thumbCenterX, (trackRect.Y + (trackRect.Height / 2f)) + (slider.ThumbSize / 2f) - 1f);
        var end = new Vector2(slider.LayoutSlot.X + slider.LayoutSlot.Width - 8f, start.Y);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, leftReleased: true));

        Assert.True(slider.Value > 100f);
    }

    private static Slider CreateSlider()
    {
        return new Slider
        {
            Width = 240f,
            Height = 28f,
            Minimum = 0f,
            Maximum = 100f,
            Value = 0f
        };
    }

    private static UiRoot CreateUiRoot(Slider slider)
    {
        var host = new Canvas
        {
            Width = 460f,
            Height = 220f
        };
        host.AddChild(slider);
        Canvas.SetLeft(slider, 40f);
        Canvas.SetTop(slider, 80f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);
        return uiRoot;
    }

    private static T? FindDescendantByName<T>(UIElement root, string name)
        where T : FrameworkElement
    {
        if (root is T typed && string.Equals(typed.Name, name, System.StringComparison.Ordinal))
        {
            return typed;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindDescendantByName<T>(child, name);
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

    private static InputDelta CreateKeyDownDelta(Keys key, KeyboardState? keyboard = null)
    {
        var pointer = new Vector2(16f, 16f);
        var currentKeyboard = keyboard ?? new KeyboardState(key);
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(currentKeyboard, default, pointer),
            PressedKeys = new List<Keys> { key },
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = false,
            WheelDelta = 0,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void RunLayout(UiRoot uiRoot)
    {            uiRoot.Update(
                new GameTime(System.TimeSpan.FromMilliseconds(16), System.TimeSpan.FromMilliseconds(16)),
                new Viewport(0, 0, 460, 220));
    }
}

