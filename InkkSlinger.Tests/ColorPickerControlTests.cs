using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ColorPickerControlTests
{
    [Fact]
    public void ColorSpectrum_SelectedColor_SynchronizesHueAndArgb()
    {
        var spectrum = new ColorSpectrum();

        spectrum.SelectedColor = new Color(0, 255, 255, 128);

        Assert.InRange(spectrum.Hue, 179f, 181f);
        Assert.InRange(spectrum.Saturation, 0.99f, 1f);
        Assert.InRange(spectrum.Value, 0.99f, 1f);
        Assert.InRange(spectrum.Alpha, 0.49f, 0.51f);
        Assert.Equal(new Color(0, 255, 255, 128), spectrum.SelectedColor);
    }

    [Fact]
    public void ColorSpectrum_HorizontalPointerSelection_UpdatesHueAndRaisesEvents()
    {
        var host = new Canvas
        {
            Width = 320f,
            Height = 140f
        };
        var spectrum = new ColorSpectrum
        {
            Width = 180f,
            Height = 18f,
            Orientation = Orientation.Horizontal
        };

        host.AddChild(spectrum);
        Canvas.SetLeft(spectrum, 30f);
        Canvas.SetTop(spectrum, 40f);

        var selectedColorChangedCount = 0;
        var hueChangedCount = 0;
        spectrum.SelectedColorChanged += (_, args) =>
        {
            selectedColorChangedCount++;
            Assert.NotEqual(args.OldColor, args.NewColor);
        };
        spectrum.HueChanged += (_, _) => hueChangedCount++;

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 320, 140);
        var snapshot = spectrum.GetColorSpectrumSnapshotForDiagnostics();
        var pointer = new Vector2(snapshot.SpectrumRect.X + (snapshot.SpectrumRect.Width * 0.5f), snapshot.SpectrumRect.Y + (snapshot.SpectrumRect.Height * 0.5f));

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));
        RunLayout(uiRoot, 320, 140);

        Assert.InRange(spectrum.Hue, 175f, 185f);
        Assert.True(selectedColorChangedCount > 0);
        Assert.True(hueChangedCount > 0);
        Assert.InRange(spectrum.GetColorSpectrumSnapshotForDiagnostics().SelectorNormalizedOffset, 0.49f, 0.51f);
    }

    [Fact]
    public void ColorSpectrum_HorizontalPointerSelection_AtMaximum_KeepsTrailingEndpoint()
    {
        var host = new Canvas
        {
            Width = 320f,
            Height = 140f
        };
        var spectrum = new ColorSpectrum
        {
            Width = 180f,
            Height = 18f,
            Orientation = Orientation.Horizontal
        };

        host.AddChild(spectrum);
        Canvas.SetLeft(spectrum, 30f);
        Canvas.SetTop(spectrum, 40f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 320, 140);
        var snapshot = spectrum.GetColorSpectrumSnapshotForDiagnostics();
        var pointer = new Vector2(snapshot.SpectrumRect.X + snapshot.SpectrumRect.Width, snapshot.SpectrumRect.Y + (snapshot.SpectrumRect.Height * 0.5f));

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));
        RunLayout(uiRoot, 320, 140);

        Assert.InRange(spectrum.Hue, 359f, 360f);
        Assert.InRange(spectrum.GetColorSpectrumSnapshotForDiagnostics().SelectorNormalizedOffset, 0.99f, 1f);
    }

    [Fact]
    public void ColorSpectrum_AlphaModePointerSelection_UpdatesAlphaAndRaisesEvents()
    {
        var host = new Canvas
        {
            Width = 320f,
            Height = 220f
        };
        var spectrum = new ColorSpectrum
        {
            Width = 24f,
            Height = 180f,
            Orientation = Orientation.Vertical,
            Mode = ColorSpectrumMode.Alpha,
            Hue = 12f,
            Saturation = 0.8f,
            Value = 0.9f,
            Alpha = 1f
        };

        host.AddChild(spectrum);
        Canvas.SetLeft(spectrum, 30f);
        Canvas.SetTop(spectrum, 20f);

        var selectedColorChangedCount = 0;
        var alphaChangedCount = 0;
        spectrum.SelectedColorChanged += (_, args) =>
        {
            selectedColorChangedCount++;
            Assert.NotEqual(args.OldColor, args.NewColor);
        };
        spectrum.AlphaChanged += (_, _) => alphaChangedCount++;

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 320, 220);
        var snapshot = spectrum.GetColorSpectrumSnapshotForDiagnostics();
        var pointer = new Vector2(snapshot.SpectrumRect.X + (snapshot.SpectrumRect.Width * 0.5f), snapshot.SpectrumRect.Y + (snapshot.SpectrumRect.Height * 0.8f));

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));
        RunLayout(uiRoot, 320, 220);

        Assert.InRange(spectrum.Alpha, 0.18f, 0.22f);
        Assert.True(selectedColorChangedCount > 0);
        Assert.True(alphaChangedCount > 0);
        Assert.InRange(spectrum.GetColorSpectrumSnapshotForDiagnostics().SelectorNormalizedOffset, 0.79f, 0.81f);
    }

    [Fact]
    public void ColorSpectrum_DragSelection_DefersSelectedColorChangedUntilPointerRelease()
    {
        var host = new Canvas
        {
            Width = 320f,
            Height = 140f
        };
        var spectrum = new ColorSpectrum
        {
            Width = 180f,
            Height = 18f,
            Orientation = Orientation.Horizontal
        };

        host.AddChild(spectrum);
        Canvas.SetLeft(spectrum, 30f);
        Canvas.SetTop(spectrum, 40f);

        var selectedColorChangedCount = 0;
        spectrum.SelectedColorChanged += (_, args) =>
        {
            selectedColorChangedCount++;
            Assert.NotEqual(args.OldColor, args.NewColor);
        };

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 320, 140);

        var snapshot = spectrum.GetColorSpectrumSnapshotForDiagnostics();
        var pressPoint = new Vector2(
            snapshot.SpectrumRect.X + (snapshot.SpectrumRect.Width * 0.2f),
            snapshot.SpectrumRect.Y + (snapshot.SpectrumRect.Height * 0.5f));
        var dragPoint = new Vector2(
            snapshot.SpectrumRect.X + (snapshot.SpectrumRect.Width * 0.8f),
            snapshot.SpectrumRect.Y + (snapshot.SpectrumRect.Height * 0.5f));

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pressPoint, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(dragPoint, pointerMoved: true));
        RunLayout(uiRoot, 320, 140);

        Assert.Equal(0, selectedColorChangedCount);
        Assert.InRange(spectrum.Hue, 285f, 295f);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(dragPoint, leftReleased: true));
        RunLayout(uiRoot, 320, 140);

        Assert.Equal(1, selectedColorChangedCount);
    }

    [Fact]
    public void ColorPicker_SelectedColor_SynchronizesHueSaturationValueAndAlpha()
    {
        var picker = new ColorPicker();

        picker.SelectedColor = new Color(255, 0, 0, 64);

        Assert.InRange(picker.Hue, -0.001f, 0.001f);
        Assert.InRange(picker.Saturation, 0.99f, 1f);
        Assert.InRange(picker.Value, 0.99f, 1f);
        Assert.InRange(picker.Alpha, 0.24f, 0.26f);
    }

    [Fact]
    public void ColorPicker_SelectedColorSynchronization_RaisesComponentChangedEvents()
    {
        var picker = new ColorPicker();
        var hueChangedCount = 0;
        var saturationChangedCount = 0;
        var valueChangedCount = 0;
        var alphaChangedCount = 0;

        picker.HueChanged += (_, _) => hueChangedCount++;
        picker.SaturationChanged += (_, _) => saturationChangedCount++;
        picker.ValueChanged += (_, _) => valueChangedCount++;
        picker.AlphaChanged += (_, _) => alphaChangedCount++;

        picker.SelectedColor = new Color(0x33, 0x66, 0x99, 0x80);

    Assert.True(hueChangedCount > 0);
    Assert.True(saturationChangedCount > 0);
    Assert.True(valueChangedCount > 0);
    Assert.True(alphaChangedCount > 0);
    }

    [Fact]
    public void ColorPicker_PointerSelection_UpdatesSvInsideOwnRenderedRegion()
    {
        var host = new Canvas
        {
            Width = 420f,
            Height = 260f
        };
        var picker = new ColorPicker
        {
            Width = 220f,
            Height = 160f,
            Hue = 0f,
            Alpha = 1f
        };

        host.AddChild(picker);
        Canvas.SetLeft(picker, 40f);
        Canvas.SetTop(picker, 30f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 420, 260);

        var initial = picker.GetColorPickerSnapshotForDiagnostics();
        var svPointer = new Vector2(initial.SpectrumRect.X + (initial.SpectrumRect.Width * 0.75f), initial.SpectrumRect.Y + (initial.SpectrumRect.Height * 0.25f));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(svPointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(svPointer, leftReleased: true));
        RunLayout(uiRoot, 420, 260);

        Assert.InRange(picker.Saturation, 0.73f, 0.77f);
        Assert.InRange(picker.Value, 0.73f, 0.77f);

        var finalSnapshot = picker.GetColorPickerSnapshotForDiagnostics();
        Assert.InRange(finalSnapshot.SaturationSelector.X, finalSnapshot.SpectrumRect.X, finalSnapshot.SpectrumRect.X + finalSnapshot.SpectrumRect.Width);
        Assert.InRange(finalSnapshot.SaturationSelector.Y, finalSnapshot.SpectrumRect.Y, finalSnapshot.SpectrumRect.Y + finalSnapshot.SpectrumRect.Height);
    }

    [Fact]
    public void ColorPicker_DragSelection_CommitsSelectedColorOnPointerRelease()
    {
        var host = new Canvas
        {
            Width = 420f,
            Height = 260f
        };
        var picker = new ColorPicker
        {
            Width = 220f,
            Height = 160f,
            Hue = 0f,
            Alpha = 1f
        };

        host.AddChild(picker);
        Canvas.SetLeft(picker, 40f);
        Canvas.SetTop(picker, 30f);

        var selectedColorChangedCount = 0;
        picker.SelectedColorChanged += (_, args) =>
        {
            selectedColorChangedCount++;
            Assert.NotEqual(args.OldColor, args.NewColor);
        };

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 420, 260);

        var snapshot = picker.GetColorPickerSnapshotForDiagnostics();
        var pressPoint = new Vector2(
            snapshot.SpectrumRect.X + (snapshot.SpectrumRect.Width * 0.2f),
            snapshot.SpectrumRect.Y + (snapshot.SpectrumRect.Height * 0.8f));
        var dragPoint = new Vector2(
            snapshot.SpectrumRect.X + (snapshot.SpectrumRect.Width * 0.8f),
            snapshot.SpectrumRect.Y + (snapshot.SpectrumRect.Height * 0.2f));

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pressPoint, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(dragPoint, pointerMoved: true));
        RunLayout(uiRoot, 420, 260);

        Assert.Equal(0, selectedColorChangedCount);
        Assert.InRange(picker.Saturation, 0.78f, 0.82f);
        Assert.InRange(picker.Value, 0.78f, 0.82f);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(dragPoint, leftReleased: true));
    Assert.Equal(1, selectedColorChangedCount);

        RunLayout(uiRoot, 420, 260);

        Assert.Equal(1, selectedColorChangedCount);
    }

    [Fact]
    public void ColorPicker_AlphaOnlyChanges_DoNotInvalidateRender()
    {
        var host = new Canvas
        {
            Width = 420f,
            Height = 260f
        };
        var picker = new ColorPicker
        {
            Width = 220f,
            Height = 160f,
            Hue = 30f,
            Saturation = 0.7f,
            Value = 0.8f,
            Alpha = 1f
        };

        host.AddChild(picker);
        Canvas.SetLeft(picker, 40f);
        Canvas.SetTop(picker, 30f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 420, 260);
        ResetRenderBaseline(host, uiRoot);

        var initialPickerInvalidations = picker.RenderInvalidationCount;
        var initialRootInvalidations = uiRoot.RenderInvalidationCount;

        picker.Alpha = 0.35f;
        RunLayout(uiRoot, 420, 260);

        Assert.False(picker.NeedsRender);
        Assert.Equal(initialPickerInvalidations, picker.RenderInvalidationCount);
        Assert.Equal(initialRootInvalidations, uiRoot.RenderInvalidationCount);
    }

    [Fact]
    public void ColorPicker_RenderDiagnostics_ExposeFullSvSurface()
    {
        var picker = new ColorPicker
        {
            Width = 240f,
            Height = 180f
        };

        picker.Measure(new Vector2(240f, 180f));
        picker.Arrange(new LayoutRect(10f, 20f, 240f, 180f));

        var snapshot = picker.GetColorPickerSnapshotForDiagnostics();

        Assert.Equal(236f, snapshot.SpectrumRect.Width, 3);
        Assert.Equal(176f, snapshot.SpectrumRect.Height, 3);
        Assert.True(snapshot.SelectionIndicatorRadius > 0f);
    }

    [Fact]
    public void ColorSpectrum_HueMode_NonHueComponentChanges_DoNotInvalidateRender()
    {
        var host = new Canvas
        {
            Width = 320f,
            Height = 140f
        };
        var spectrum = new ColorSpectrum
        {
            Width = 180f,
            Height = 18f,
            Orientation = Orientation.Horizontal,
            Mode = ColorSpectrumMode.Hue,
            Hue = 42f,
            Saturation = 0.6f,
            Value = 0.7f,
            Alpha = 1f
        };

        host.AddChild(spectrum);
        Canvas.SetLeft(spectrum, 30f);
        Canvas.SetTop(spectrum, 40f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 320, 140);
        ResetRenderBaseline(host, uiRoot);

        var initialSpectrumInvalidations = spectrum.RenderInvalidationCount;
        var initialRootInvalidations = uiRoot.RenderInvalidationCount;

        spectrum.Saturation = 0.2f;
        spectrum.Value = 0.9f;
        spectrum.Alpha = 0.4f;
        RunLayout(uiRoot, 320, 140);

        Assert.False(spectrum.NeedsRender);
        Assert.Equal(initialSpectrumInvalidations, spectrum.RenderInvalidationCount);
        Assert.Equal(initialRootInvalidations, uiRoot.RenderInvalidationCount);
    }

    [Fact]
    public void ColorSpectrum_SelectedColorSynchronization_RaisesComponentChangedEvents()
    {
        var spectrum = new ColorSpectrum
        {
            Mode = ColorSpectrumMode.Alpha
        };
        var hueChangedCount = 0;
        var saturationChangedCount = 0;
        var valueChangedCount = 0;
        var alphaChangedCount = 0;

        spectrum.HueChanged += (_, _) => hueChangedCount++;
        spectrum.SaturationChanged += (_, _) => saturationChangedCount++;
        spectrum.ValueChanged += (_, _) => valueChangedCount++;
        spectrum.AlphaChanged += (_, _) => alphaChangedCount++;

        spectrum.SelectedColor = new Color(0x11, 0x88, 0x44, 0x40);

    Assert.True(hueChangedCount > 0);
    Assert.True(saturationChangedCount > 0);
    Assert.True(valueChangedCount > 0);
    Assert.True(alphaChangedCount > 0);
    }

    [Fact]
    public void ColorControls_TelemetrySnapshots_RecordRuntimeAndAggregateActivity()
    {
        _ = ColorPicker.GetTelemetryAndReset();
        _ = ColorSpectrum.GetTelemetryAndReset();

        var host = new Canvas
        {
            Width = 420f,
            Height = 260f
        };
        var picker = new ColorPicker
        {
            Width = 220f,
            Height = 160f,
            Hue = 0f,
            Alpha = 1f
        };
        var spectrum = new ColorSpectrum
        {
            Width = 180f,
            Height = 18f,
            Orientation = Orientation.Horizontal
        };

        host.AddChild(picker);
        host.AddChild(spectrum);
        Canvas.SetLeft(picker, 40f);
        Canvas.SetTop(picker, 30f);
        Canvas.SetLeft(spectrum, 40f);
        Canvas.SetTop(spectrum, 210f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 420, 260);

        var pickerSnapshot = picker.GetColorPickerSnapshotForDiagnostics();
        var pickerPointer = new Vector2(
            pickerSnapshot.SpectrumRect.X + (pickerSnapshot.SpectrumRect.Width * 0.75f),
            pickerSnapshot.SpectrumRect.Y + (pickerSnapshot.SpectrumRect.Height * 0.25f));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pickerPointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pickerPointer, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pickerPointer, leftReleased: true));
        RunLayout(uiRoot, 420, 260);

        var spectrumSnapshot = spectrum.GetColorSpectrumSnapshotForDiagnostics();
        var spectrumPointer = new Vector2(
            spectrumSnapshot.SpectrumRect.X + (spectrumSnapshot.SpectrumRect.Width * 0.5f),
            spectrumSnapshot.SpectrumRect.Y + (spectrumSnapshot.SpectrumRect.Height * 0.5f));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(spectrumPointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(spectrumPointer, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(spectrumPointer, leftReleased: true));
        RunLayout(uiRoot, 420, 260);

        var pickerRuntime = picker.GetColorPickerSnapshotForDiagnostics();
        var spectrumRuntime = spectrum.GetColorSpectrumSnapshotForDiagnostics();

        Assert.True(pickerRuntime.HandlePointerDownCallCount >= 1);
        Assert.True(pickerRuntime.HandlePointerMoveCallCount >= 1);
        Assert.True(pickerRuntime.UpdateSpectrumFromPointerCallCount >= 1);

        Assert.True(spectrumRuntime.HandlePointerDownCallCount >= 1);
        Assert.True(spectrumRuntime.HandlePointerMoveCallCount >= 1);
        Assert.True(spectrumRuntime.UpdateFromPointerCallCount >= 1);

        var pickerAggregate = ColorPicker.GetTelemetryAndReset();
        var spectrumAggregate = ColorSpectrum.GetTelemetryAndReset();

        Assert.True(pickerAggregate.HandlePointerDownCallCount >= 1);
        Assert.True(pickerAggregate.HandlePointerMoveCallCount >= 1);
        Assert.True(pickerAggregate.UpdateSpectrumFromPointerCallCount >= 1);

        Assert.True(spectrumAggregate.HandlePointerDownCallCount >= 1);
        Assert.True(spectrumAggregate.HandlePointerMoveCallCount >= 1);
        Assert.True(spectrumAggregate.UpdateFromPointerCallCount >= 1);

        var clearedPicker = ColorPicker.GetTelemetryAndReset();
        var clearedSpectrum = ColorSpectrum.GetTelemetryAndReset();

        Assert.Equal(0, clearedPicker.HandlePointerDownCallCount);
        Assert.Equal(0, clearedSpectrum.HandlePointerDownCallCount);
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

    private static void ResetRenderBaseline(UIElement root, UiRoot uiRoot)
    {
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
    }
}