using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class InkPresenterCoreTests
{
    [Fact]
    public void DefaultStrokes_IsNonNull()
    {
        var presenter = new InkPresenter();

        Assert.NotNull(presenter.Strokes);
    }

    [Fact]
    public void DefaultStrokeColor_IsBlack()
    {
        var presenter = new InkPresenter();

        Assert.Equal(Color.Black, presenter.StrokeColor);
    }

    [Fact]
    public void DefaultStrokeThickness_IsTwo()
    {
        var presenter = new InkPresenter();

        Assert.Equal(2f, presenter.StrokeThickness);
    }

    [Fact]
    public void StrokeColorChange_InvalidatesRender()
    {
        var presenter = new InkPresenter();
        var initialInvalidationCount = presenter.InvalidationCount;

        presenter.StrokeColor = Color.Red;

        Assert.True(presenter.InvalidationCount > initialInvalidationCount);
    }

    [Fact]
    public void StrokeThicknessChange_InvalidatesRender()
    {
        var presenter = new InkPresenter();
        var initialInvalidationCount = presenter.InvalidationCount;

        presenter.StrokeThickness = 5f;

        Assert.True(presenter.InvalidationCount > initialInvalidationCount);
    }
}

public sealed class InkPresenterRenderTests
{
    [Fact]
    public void Measure_DoesNotThrow()
    {
        var presenter = new InkPresenter();

        presenter.Measure(new Vector2(200, 200));

        Assert.True(presenter.DesiredSize.X > 0);
        Assert.True(presenter.DesiredSize.Y > 0);
    }

    [Fact]
    public void Measure_UsesProvidedSize()
    {
        var presenter = new InkPresenter();

        presenter.Measure(new Vector2(200, 200));

        Assert.Equal(200, presenter.DesiredSize.X);
        Assert.Equal(200, presenter.DesiredSize.Y);
    }

    [Fact]
    public void Render_DoesNotThrow_WhenNoStrokes()
    {
        var presenter = new InkPresenter();
        presenter.Measure(new Vector2(200, 200));
        presenter.Arrange(new LayoutRect(0, 0, 200, 200));

        presenter.Render(new MockSpriteBatch());

        // No exception means success
    }

    [Fact]
    public void Render_DoesNotThrow_WithStrokes()
    {
        var presenter = new InkPresenter();
        var strokes = new InkStrokeCollection();
        strokes.Add(new InkStroke(new[] { Vector2.Zero, new Vector2(100, 100) }));
        presenter.Strokes = strokes;
        presenter.Measure(new Vector2(200, 200));
        presenter.Arrange(new LayoutRect(0, 0, 200, 200));

        presenter.Render(new MockSpriteBatch());

        // No exception means success
    }
}

public sealed class InkStrokeModelTests
{
    [Fact]
    public void InkStroke_StoresPoints()
    {
        var points = new[] { new Vector2(0, 0), new Vector2(10, 10), new Vector2(20, 20) };
        var stroke = new InkStroke(points);

        Assert.Equal(3, stroke.Points.Count);
    }

    [Fact]
    public void InkStroke_AddPoint_AppendsToList()
    {
        var stroke = new InkStroke(new[] { Vector2.Zero });
        stroke.AddPoint(new Vector2(10, 10));

        Assert.Equal(2, stroke.Points.Count);
    }

    [Fact]
    public void InkStroke_DefaultColor_IsBlack()
    {
        var stroke = new InkStroke(new[] { Vector2.Zero });

        Assert.Equal(Color.Black, stroke.Color);
    }

    [Fact]
    public void InkStroke_DefaultThickness_IsTwo()
    {
        var stroke = new InkStroke(new[] { Vector2.Zero });

        Assert.Equal(2f, stroke.Thickness);
    }

    [Fact]
    public void InkStroke_DefaultOpacity_IsOne()
    {
        var stroke = new InkStroke(new[] { Vector2.Zero });

        Assert.Equal(1f, stroke.Opacity);
    }

    [Fact]
    public void InkStroke_Bounds_CalculatesCorrectly()
    {
        var stroke = new InkStroke(new[] { new Vector2(10, 10), new Vector2(50, 50) });

        var bounds = stroke.Bounds;

        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);
    }

    [Fact]
    public void InkStrokeCollection_Add_InsertsStroke()
    {
        var collection = new InkStrokeCollection();
        var stroke = new InkStroke(new[] { Vector2.Zero });

        collection.Add(stroke);

        Assert.Single(collection);
    }

    [Fact]
    public void InkStrokeCollection_Remove_RemovesStroke()
    {
        var collection = new InkStrokeCollection();
        var stroke = new InkStroke(new[] { Vector2.Zero });
        collection.Add(stroke);

        collection.Remove(stroke);

        Assert.Empty(collection);
    }

    [Fact]
    public void InkStrokeCollection_Clear_RemovesAllStrokes()
    {
        var collection = new InkStrokeCollection();
        collection.Add(new InkStroke(new[] { Vector2.Zero }));
        collection.Add(new InkStroke(new[] { new Vector2(10, 10) }));

        collection.Clear();

        Assert.Empty(collection);
    }

    [Fact]
    public void InkStrokeCollection_GetBounds_ReturnsCombinedBounds()
    {
        var collection = new InkStrokeCollection();
        collection.Add(new InkStroke(new[] { new Vector2(0, 0), new Vector2(10, 10) }));
        collection.Add(new InkStroke(new[] { new Vector2(50, 50), new Vector2(60, 60) }));

        var bounds = collection.GetBounds();

        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);
    }

    [Fact]
    public void InkStrokeCollection_Changed_EventFiresOnAdd()
    {
        var collection = new InkStrokeCollection();
        var eventFired = false;
        collection.Changed += (s, e) => eventFired = true;

        collection.Add(new InkStroke(new[] { Vector2.Zero }));

        Assert.True(eventFired);
    }

    [Fact]
    public void InkStrokeCollection_Changed_EventFiresOnRemove()
    {
        var collection = new InkStrokeCollection();
        var stroke = new InkStroke(new[] { Vector2.Zero });
        collection.Add(stroke);
        var eventFired = false;
        collection.Changed += (s, e) => eventFired = true;

        collection.Remove(stroke);

        Assert.True(eventFired);
    }

    [Fact]
    public void InkStrokeCollection_Changed_EventFiresOnClear()
    {
        var collection = new InkStrokeCollection();
        collection.Add(new InkStroke(new[] { Vector2.Zero }));
        var eventFired = false;
        collection.Changed += (s, e) => eventFired = true;

        collection.Clear();

        Assert.True(eventFired);
    }
}
