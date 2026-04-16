using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ColorPickerViewRegressionTests
{
    [Fact]
    public void ColorPickerView_DraggingHueSliderToMaximum_ShouldKeepMaximumValue()
    {
        var snapshot = SnapshotApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new ColorPickerView();
            var uiRoot = new UiRoot(view);
            RunLayout(uiRoot, 1280, 900);

            var hueSlider = Assert.IsType<Slider>(view.FindName("HueSlider"));
            var hueSliderLabel = Assert.IsType<Label>(view.FindName("HueSliderLabel"));
            var thumb = FindDescendantByName<Thumb>(hueSlider, "PART_Thumb");
            Assert.NotNull(thumb);

            var start = GetCenter(thumb!.LayoutSlot);
            var end = new Vector2(hueSlider.LayoutSlot.X + hueSlider.LayoutSlot.Width - 4f, start.Y);

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, leftPressed: true));
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, pointerMoved: true));
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, leftReleased: true));

            Assert.InRange(hueSlider.Value, 359f, 360f);
            Assert.Equal("Hue: 360°", hueSliderLabel.Content);
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    [Fact]
    public void ColorPickerView_SettingHueSliderToMaximum_ShouldPreserveMaximumValue()
    {
        var snapshot = SnapshotApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new ColorPickerView();
            var hueSlider = Assert.IsType<Slider>(view.FindName("HueSlider"));
            var hueSliderLabel = Assert.IsType<Label>(view.FindName("HueSliderLabel"));
            var hsvaValueText = Assert.IsType<TextBlock>(view.FindName("HsvaValueText"));

            hueSlider.Value = hueSlider.Maximum;

            Assert.Equal(360f, hueSlider.Value);
            Assert.Equal("Hue: 360°", hueSliderLabel.Content);
            Assert.Contains("HSVA: 360°", hsvaValueText.Text);
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
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

    private static void RunLayout(UiRoot uiRoot, int width, int height)
    {
        uiRoot.Update(
            new GameTime(System.TimeSpan.FromMilliseconds(16), System.TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, width, height));
    }

    private static Dictionary<object, object> SnapshotApplicationResources()
    {
        return new Dictionary<object, object>(UiApplication.Current.Resources);
    }

    private static void RestoreApplicationResources(Dictionary<object, object> snapshot)
    {
        UiApplication.Current.Resources.Clear();
        foreach (var pair in snapshot)
        {
            UiApplication.Current.Resources[pair.Key] = pair.Value;
        }
    }

    private static void LoadRootAppResources()
    {
        TestApplicationResources.LoadDemoAppResources();
    }
}