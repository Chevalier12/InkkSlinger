using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class UniformGridLayoutTests
{
    [Fact]
    public void Measure_WhenCellSizeGrowsForPlainTextButton_ReusesChildMeasure()
    {
        var grid = new UniformGrid
        {
            Rows = 1,
            Columns = 1
        };

        var child = new Button
        {
            Text = "13"
        };
        grid.AddChild(child);

        grid.Measure(new Vector2(24f, 24f));
        grid.Measure(new Vector2(96f, 48f));

        Assert.Equal(1, child.MeasureCallCount);
        Assert.Equal(1, child.MeasureWorkCount);
    }

    [Fact]
    public void Measure_WhenCellSizeGrowsForNoWrapTextBlock_ReusesChildMeasure()
    {
        var grid = new UniformGrid
        {
            Rows = 1,
            Columns = 1
        };

        var child = new TextBlock
        {
            Text = "Mon"
        };
        grid.AddChild(child);

        grid.Measure(new Vector2(20f, 20f));
        grid.Measure(new Vector2(100f, 40f));

        Assert.Equal(1, child.MeasureCallCount);
        Assert.Equal(1, child.MeasureWorkCount);
    }

    [Fact]
    public void Measure_WhenCellSizeGrowsForWrappedTextChild_StillReMeasures()
    {
        var grid = new UniformGrid
        {
            Rows = 1,
            Columns = 1
        };

        var child = new TextBlock
        {
            Text = "calendar layout attribution",
            TextWrapping = TextWrapping.Wrap
        };
        grid.AddChild(child);

        grid.Measure(new Vector2(40f, 30f));
        grid.Measure(new Vector2(120f, 60f));

        Assert.Equal(2, child.MeasureCallCount);
        Assert.Equal(2, child.MeasureWorkCount);
    }

    [Fact]
    public void Measure_WhenChildHasExplicitSize_ReusesChildMeasureAcrossCellChanges()
    {
        var grid = new UniformGrid
        {
            Rows = 1,
            Columns = 1
        };

        var child = new FixedSizeElement(new Vector2(18f, 12f))
        {
            Width = 18f,
            Height = 12f
        };
        grid.AddChild(child);

        grid.Measure(new Vector2(24f, 24f));
        grid.Measure(new Vector2(120f, 60f));

        Assert.Equal(1, child.MeasureCallCount);
        Assert.Equal(1, child.MeasureWorkCount);
    }

    [Fact]
    public void Measure_WhenAllChildrenAreAvailableIndependent_ReusesUniformGridMeasureAcrossCellChanges()
    {
        var grid = new UniformGrid
        {
            Rows = 1,
            Columns = 7
        };

        for (var i = 0; i < 7; i++)
        {
            grid.AddChild(new TextBlock { Text = "Mon" });
        }

        grid.Measure(new Vector2(140f, 24f));
        grid.Measure(new Vector2(700f, 48f));

        Assert.Equal(2, grid.MeasureCallCount);
        Assert.Equal(1, grid.MeasureWorkCount);
    }

    private sealed class FixedSizeElement : FrameworkElement
    {
        private readonly Vector2 _desiredSize;

        public FixedSizeElement(Vector2 desiredSize)
        {
            _desiredSize = desiredSize;
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            return new Vector2(
                MathF.Min(_desiredSize.X, availableSize.X),
                MathF.Min(_desiredSize.Y, availableSize.Y));
        }
    }
}
