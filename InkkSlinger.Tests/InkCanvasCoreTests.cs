using System;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class InkCanvasCoreTests
{
    [Fact]
    public void DefaultStrokeCollection_IsNotNull()
    {
        var canvas = new InkCanvas();
        Assert.NotNull(canvas.Strokes);
        Assert.Equal(0, canvas.Strokes!.Count);
    }

    [Fact]
    public void SettingStrokes_UpdatesPresenter()
    {
        var canvas = new InkCanvas();
        var strokes = new InkStrokeCollection();
        canvas.Strokes = strokes;

        Assert.Same(strokes, canvas.Presenter.Strokes);
    }

    [Fact]
    public void AddingStroke_UpdatesCollection()
    {
        var canvas = new InkCanvas();
        var stroke = new InkStroke(new[] { new Vector2(0, 0), new Vector2(10, 10) });
        canvas.Strokes!.Add(stroke);

        Assert.Equal(1, canvas.Strokes.Count);
        Assert.Same(stroke, canvas.Strokes[0]);
    }

    [Fact]
    public void RemovingStroke_UpdatesCollection()
    {
        var canvas = new InkCanvas();
        var stroke = new InkStroke(new[] { new Vector2(0, 0), new Vector2(10, 10) });
        canvas.Strokes!.Add(stroke);
        canvas.Strokes.Remove(stroke);

        Assert.Equal(0, canvas.Strokes.Count);
    }

    [Fact]
    public void ClearingStrokes_EmptiesCollection()
    {
        var canvas = new InkCanvas();
        canvas.Strokes!.Add(new InkStroke(new[] { new Vector2(0, 0), new Vector2(5, 5) }));
        canvas.Strokes.Add(new InkStroke(new[] { new Vector2(10, 10), new Vector2(20, 20) }));
        Assert.Equal(2, canvas.Strokes.Count);

        canvas.Strokes.Clear();
        Assert.Equal(0, canvas.Strokes.Count);
    }

    [Fact]
    public void InkStroke_GetBounds_CalculatesCorrectly()
    {
        var points = new[] { new Vector2(10, 20), new Vector2(30, 40) };
        var stroke = new InkStroke(points, new InkDrawingAttributes { Width = 4f, Height = 4f });

        var bounds = stroke.GetBounds();
        Assert.Equal(8f, bounds.X);  // 10 - 2
        Assert.Equal(18f, bounds.Y); // 20 - 2
        Assert.Equal(24f, bounds.Width);  // (30+2) - (10-2)
        Assert.Equal(24f, bounds.Height); // (40+2) - (20-2)
    }

    [Fact]
    public void InkStroke_GetBounds_CachesResult()
    {
        var points = new[] { new Vector2(10, 20), new Vector2(30, 40) };
        var stroke = new InkStroke(points);

        var bounds1 = stroke.GetBounds();
        var bounds2 = stroke.GetBounds();
        Assert.Equal(bounds1, bounds2);
    }

    [Fact]
    public void InkStroke_AddPoint_InvalidatesBounds()
    {
        var stroke = new InkStroke(new[] { new Vector2(0, 0) });
        var bounds1 = stroke.GetBounds();

        stroke.AddPoint(new Vector2(100, 100));
        var bounds2 = stroke.GetBounds();

        Assert.True(bounds2.Width > bounds1.Width);
        Assert.True(bounds2.Height > bounds1.Height);
    }

    [Fact]
    public void InkDrawingAttributes_Clone_CreatesIndependentCopy()
    {
        var original = new InkDrawingAttributes
        {
            Color = Color.Red,
            Width = 5f,
            Height = 3f,
            Opacity = 0.5f
        };

        var clone = original.Clone();
        clone.Color = Color.Blue;
        clone.Width = 10f;

        Assert.Equal(Color.Red, original.Color);
        Assert.Equal(5f, original.Width);
        Assert.Equal(Color.Blue, clone.Color);
        Assert.Equal(10f, clone.Width);
    }

    [Fact]
    public void InkStrokeCollection_Changed_FiresOnAdd()
    {
        var collection = new InkStrokeCollection();
        bool changed = false;
        collection.Changed += (_, _) => changed = true;

        collection.Add(new InkStroke(new[] { Vector2.Zero }));
        Assert.True(changed);
    }

    [Fact]
    public void InkStrokeCollection_Changed_FiresOnRemove()
    {
        var collection = new InkStrokeCollection();
        var stroke = new InkStroke(new[] { Vector2.Zero });
        collection.Add(stroke);

        bool changed = false;
        collection.Changed += (_, _) => changed = true;
        collection.Remove(stroke);
        Assert.True(changed);
    }

    [Fact]
    public void InkStrokeCollection_Changed_FiresOnClear()
    {
        var collection = new InkStrokeCollection();
        collection.Add(new InkStroke(new[] { Vector2.Zero }));

        bool changed = false;
        collection.Changed += (_, _) => changed = true;
        collection.Clear();
        Assert.True(changed);
    }

    [Fact]
    public void InkStrokeCollection_Changed_DoesNotFireOnEmptyClear()
    {
        var collection = new InkStrokeCollection();
        bool changed = false;
        collection.Changed += (_, _) => changed = true;
        collection.Clear();
        Assert.False(changed);
    }

    [Fact]
    public void InkStroke_EmptyPoints_ReturnsEmptyBounds()
    {
        var stroke = new InkStroke(Array.Empty<Vector2>());
        var bounds = stroke.GetBounds();
        Assert.Equal(0f, bounds.Width);
        Assert.Equal(0f, bounds.Height);
    }

    [Fact]
    public void InkCanvas_DefaultDrawingAttributes_CanBeSet()
    {
        var canvas = new InkCanvas();
        var attrs = new InkDrawingAttributes { Color = Color.Blue, Width = 8f };
        canvas.DefaultDrawingAttributes = attrs;

        Assert.Same(attrs, canvas.DefaultDrawingAttributes);
    }

    [Fact]
    public void InkPresenter_StrokesProperty_UpdatesRendering()
    {
        var presenter = new InkPresenter();
        var strokes = new InkStrokeCollection();
        presenter.Strokes = strokes;

        Assert.Same(strokes, presenter.Strokes);
    }
}
