using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class GridLayoutTests
{
    [Fact]
    public void Measure_WhenSecondPassMatchesFirstPass_DoesNotReMeasureChild()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120f) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48f) });

        var child = new FixedSizeElement(new Vector2(40f, 18f));
        grid.AddChild(child);

        grid.Measure(new Vector2(120f, 48f));

        Assert.Equal(1, child.MeasureCallCount);
        Assert.Equal(1, child.MeasureWorkCount);
    }

    [Fact]
    public void Measure_AutoSizedChildWithExplicitWidthAndHeight_IsMeasuredOnce()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var child = new FixedSizeElement(new Vector2(40f, 18f))
        {
            Width = 28f,
            Height = 18f,
            Margin = new Thickness(3f, 2f, 3f, 2f)
        };
        grid.AddChild(child);

        grid.Measure(new Vector2(120f, 48f));

        Assert.Equal(1, child.MeasureCallCount);
        Assert.Equal(1, child.MeasureWorkCount);
        Assert.Equal(34f, child.DesiredSize.X, 0.01f);
        Assert.Equal(22f, child.DesiredSize.Y, 0.01f);
    }

    [Fact]
    public void Measure_WhenFirstPassWasUnconstrainedAndFinalSizeFitsDesired_DoesNotReMeasureChild()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var child = new FixedSizeElement(new Vector2(40f, 18f));
        grid.AddChild(child);

        grid.Measure(new Vector2(120f, 48f));

        Assert.Equal(1, child.MeasureCallCount);
        Assert.Equal(1, child.MeasureWorkCount);
        Assert.Equal(40f, child.DesiredSize.X, 0.01f);
        Assert.Equal(18f, child.DesiredSize.Y, 0.01f);
    }

    [Fact]
    public void Measure_WhenFirstPassWasUnconstrainedAndFinalSizeIsTighter_StillReMeasuresChild()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var child = new FixedSizeElement(new Vector2(200f, 18f));
        Grid.SetColumnSpan(child, 2);
        grid.AddChild(child);

        grid.Measure(new Vector2(120f, 48f));

        Assert.Equal(2, child.MeasureCallCount);
        Assert.Equal(2, child.MeasureWorkCount);
        Assert.Equal(120f, child.DesiredSize.X, 0.01f);
    }

    [Fact]
    public void Arrange_MixedAutoAndStarDefinitions_PreservesActualSizes()
    {
        var grid = new Grid
        {
            Width = 240f,
            Height = 100f
        };
        var autoColumn = new ColumnDefinition { Width = GridLength.Auto };
        var starColumn = new ColumnDefinition { Width = GridLength.Star };
        var autoRow = new RowDefinition { Height = GridLength.Auto };
        var starRow = new RowDefinition { Height = GridLength.Star };
        grid.ColumnDefinitions.Add(autoColumn);
        grid.ColumnDefinitions.Add(starColumn);
        grid.RowDefinitions.Add(autoRow);
        grid.RowDefinitions.Add(starRow);

        var autoChild = new FixedSizeElement(new Vector2(60f, 20f));
        grid.AddChild(autoChild);

        var starChild = new FixedSizeElement(new Vector2(40f, 20f));
        Grid.SetColumn(starChild, 1);
        Grid.SetRow(starChild, 1);
        grid.AddChild(starChild);

        grid.Measure(new Vector2(240f, 100f));
        grid.Arrange(new LayoutRect(0f, 0f, 240f, 100f));

        Assert.Equal(60f, autoColumn.ActualWidth, 0.01f);
        Assert.Equal(180f, starColumn.ActualWidth, 0.01f);
        Assert.Equal(20f, autoRow.ActualHeight, 0.01f);
        Assert.Equal(80f, starRow.ActualHeight, 0.01f);
    }

    [Fact]
    public void Measure_AutoDefinitions_RespectMinAndMaxConstraints()
    {
        var grid = new Grid();
        var column = new ColumnDefinition
        {
            Width = GridLength.Auto,
            MinWidth = 50f,
            MaxWidth = 90f
        };
        var row = new RowDefinition
        {
            Height = GridLength.Auto,
            MinHeight = 20f,
            MaxHeight = 40f
        };
        grid.ColumnDefinitions.Add(column);
        grid.RowDefinitions.Add(row);

        var child = new FixedSizeElement(new Vector2(120f, 80f));
        grid.AddChild(child);

        grid.Measure(new Vector2(300f, 200f));
        grid.Arrange(new LayoutRect(0f, 0f, 300f, 200f));

        Assert.Equal(90f, column.ActualWidth, 0.01f);
        Assert.Equal(40f, row.ActualHeight, 0.01f);
    }

    [Fact]
    public void Measure_NestedCalendarLikeGrids_ReduceDayCellRemeasure()
    {
        var root = new Grid
        {
            Width = 320f,
            Height = 260f
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });

        var title = new FixedSizeElement(new Vector2(120f, 24f));
        root.AddChild(title);

        var calendarHost = new Grid();
        calendarHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        calendarHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        calendarHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
        Grid.SetRow(calendarHost, 1);
        root.AddChild(calendarHost);

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        calendarHost.AddChild(header);
        Grid.SetRow(header, 0);

        var previous = new FixedSizeElement(new Vector2(28f, 18f))
        {
            Width = 28f,
            Margin = new Thickness(0f, 0f, 6f, 4f)
        };
        header.AddChild(previous);

        var month = new FixedSizeElement(new Vector2(47f, 18f));
        Grid.SetColumn(month, 1);
        header.AddChild(month);

        var next = new FixedSizeElement(new Vector2(28f, 18f))
        {
            Width = 28f,
            Margin = new Thickness(6f, 0f, 0f, 4f)
        };
        Grid.SetColumn(next, 2);
        header.AddChild(next);

        var weekDays = new UniformGrid { Columns = 7 };
        for (var i = 0; i < 7; i++)
        {
            weekDays.AddChild(new FixedSizeElement(new Vector2(8f, 14f)));
        }

        calendarHost.AddChild(weekDays);
        Grid.SetRow(weekDays, 1);

        var days = new UniformGrid
        {
            Rows = 6,
            Columns = 7
        };
        var probeCell = new FixedSizeElement(new Vector2(14f, 16f));
        days.AddChild(probeCell);
        for (var i = 1; i < 42; i++)
        {
            days.AddChild(new FixedSizeElement(new Vector2(14f, 16f)));
        }

        calendarHost.AddChild(days);
        Grid.SetRow(days, 2);

        root.Measure(new Vector2(320f, 260f));
        root.Arrange(new LayoutRect(0f, 0f, 320f, 260f));

        Assert.InRange(previous.MeasureWorkCount, 1, 3);
        Assert.InRange(month.MeasureWorkCount, 1, 4);
        Assert.InRange(probeCell.MeasureWorkCount, 1, 4);
        Assert.True(days.DesiredSize.Y > 0f);
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
