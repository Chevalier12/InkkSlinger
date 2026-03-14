using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class InkCanvasCoreTests
{
    [Fact]
    public void DefaultStrokes_IsNonNull()
    {
        var canvas = new InkCanvas();

        Assert.NotNull(canvas.Strokes);
    }

    [Fact]
    public void DefaultEditingMode_IsInk()
    {
        var canvas = new InkCanvas();

        Assert.Equal(InkCanvasEditingMode.Ink, canvas.EditingMode);
    }

    [Fact]
    public void DefaultDrawingAttributes_IsNonNull()
    {
        var canvas = new InkCanvas();

        Assert.NotNull(canvas.DefaultDrawingAttributes);
    }

    [Fact]
    public void DefaultActiveEditingMode_MatchesEditingMode()
    {
        var canvas = new InkCanvas();

        Assert.Equal(canvas.EditingMode, canvas.ActiveEditingMode);
    }

    [Fact]
    public void EditingModeChange_UpdatesActiveEditingMode()
    {
        var canvas = new InkCanvas();

        canvas.EditingMode = InkCanvasEditingMode.EraseByStroke;

        Assert.Equal(InkCanvasEditingMode.EraseByStroke, canvas.ActiveEditingMode);
    }

    [Fact]
    public void UseCustomCursor_DefaultIsFalse()
    {
        var canvas = new InkCanvas();

        Assert.False(canvas.UseCustomCursor);
    }

    [Fact]
    public void ClearStrokes_RemovesAllStrokes()
    {
        var canvas = new InkCanvas();
        canvas.Strokes.Add(new InkStroke(new[] { Vector2.Zero, new Vector2(10, 10) }));

        canvas.ClearStrokes();

        Assert.Empty(canvas.Strokes);
    }
}

public sealed class InkCanvasInputTests
{
    [Fact]
    public void PointerDown_StartsStroke_WhenEnabled()
    {
        var canvas = new InkCanvas();
        canvas.IsEnabled = true;

        var result = canvas.HandlePointerDownFromInput(new Vector2(50, 50), extendSelection: false);

        Assert.True(result);
    }

    [Fact]
    public void PointerDown_DoesNotStartStroke_WhenDisabled()
    {
        var canvas = new InkCanvas();
        canvas.IsEnabled = false;

        var result = canvas.HandlePointerDownFromInput(new Vector2(50, 50), extendSelection: false);

        Assert.False(result);
    }

    [Fact]
    public void PointerDown_DoesNotStartStroke_WhenEditingModeIsNone()
    {
        var canvas = new InkCanvas();
        canvas.IsEnabled = true;
        canvas.EditingMode = InkCanvasEditingMode.None;

        var result = canvas.HandlePointerDownFromInput(new Vector2(50, 50), extendSelection: false);

        Assert.False(result);
    }

    [Fact]
    public void PointerMove_AppendsPoints_WhenCapturing()
    {
        var canvas = new InkCanvas();
        canvas.IsEnabled = true;
        canvas.HandlePointerDownFromInput(new Vector2(50, 50), extendSelection: false);

        var result = canvas.HandlePointerMoveFromInput(new Vector2(60, 60));

        Assert.True(result);
    }

    [Fact]
    public void PointerUp_FinalizesStroke()
    {
        var canvas = new InkCanvas();
        canvas.IsEnabled = true;
        canvas.HandlePointerDownFromInput(new Vector2(50, 50), extendSelection: false);
        canvas.HandlePointerMoveFromInput(new Vector2(60, 60));

        var result = canvas.HandlePointerUpFromInput();

        Assert.True(result);
        Assert.Empty(canvas.Strokes); // Single point stroke is not added
    }

    [Fact]
    public void PointerUp_AddsStroke_WhenMultiplePoints()
    {
        var canvas = new InkCanvas();
        canvas.IsEnabled = true;
        canvas.HandlePointerDownFromInput(new Vector2(50, 50), extendSelection: false);
        canvas.HandlePointerMoveFromInput(new Vector2(60, 60));
        canvas.HandlePointerMoveFromInput(new Vector2(70, 70));

        canvas.HandlePointerUpFromInput();

        Assert.Single(canvas.Strokes);
    }

    [Fact]
    public void PointerMove_DoesNothing_WhenNotCapturing()
    {
        var canvas = new InkCanvas();
        canvas.IsEnabled = true;

        var result = canvas.HandlePointerMoveFromInput(new Vector2(60, 60));

        Assert.False(result);
    }

    [Fact]
    public void PointerUp_DoesNothing_WhenNotCapturing()
    {
        var canvas = new InkCanvas();
        canvas.IsEnabled = true;

        var result = canvas.HandlePointerUpFromInput();

        Assert.False(result);
    }
}

public sealed class InkCanvasRenderTests
{
    [Fact]
    public void Measure_DoesNotThrow()
    {
        var canvas = new InkCanvas();

        canvas.Measure(new Vector2(200, 200));

        Assert.True(canvas.DesiredSize.X > 0);
        Assert.True(canvas.DesiredSize.Y > 0);
    }

    [Fact]
    public void Measure_UsesProvidedSize()
    {
        var canvas = new InkCanvas();

        canvas.Measure(new Vector2(200, 200));

        Assert.Equal(200, canvas.DesiredSize.X);
        Assert.Equal(200, canvas.DesiredSize.Y);
    }
}
