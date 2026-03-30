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
    public void Measure_AutoAndStarSpan_WithFiniteConstraint_AvoidsSecondMeasure()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var child = new FixedSizeElement(new Vector2(200f, 18f));
        Grid.SetColumnSpan(child, 2);
        grid.AddChild(child);

        grid.Measure(new Vector2(120f, 48f));

        Assert.Equal(1, child.MeasureCallCount);
        Assert.Equal(1, child.MeasureWorkCount);
        Assert.Equal(120f, child.DesiredSize.X, 0.01f);
    }

    [Fact]
    public void Measure_AutoAndStarSpan_UsesFiniteFirstPassConstraint()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var child = new RecordingMeasureElement(new Vector2(320f, 18f));
        Grid.SetColumnSpan(child, 2);
        grid.AddChild(child);

        grid.Measure(new Vector2(120f, 48f));

        Assert.False(float.IsPositiveInfinity(child.FirstAvailableSize.X));
        Assert.Equal(120f, child.FirstAvailableSize.X, 0.01f);
    }

    [Fact]
    public void UpdateLayout_WhenChildInvalidatesAncestorDuringArrange_RevisitsAutoRow()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });

        var footer = new LateGrowingElement();
        grid.AddChild(footer);

        var body = new FixedSizeElement(new Vector2(40f, 200f));
        Grid.SetRow(body, 1);
        grid.AddChild(body);

        grid.Measure(new Vector2(640f, 360f));

        grid.Arrange(new LayoutRect(0f, 0f, 80f, 360f));
        grid.UpdateLayout();

        Assert.Equal(60f, footer.DesiredSize.Y, 0.01f);
        Assert.Equal(60f, footer.ActualHeight, 0.01f);
        Assert.Equal(60f, grid.RowDefinitions[0].ActualHeight, 0.01f);
        Assert.True(footer.ArrangeInvalidatedParent);
    }

    [Fact]
    public void Arrange_NarrowerThanMeasuredWidth_RemeasuresWrappedTextForAutoRowHeight()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var footer = new TextBlock
        {
            Text = "The badge is intentionally translated beyond the frame. ClipToBounds crops it to the Border layout slot, and that clip is rectangular rather than rounded.",
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top
        };
        grid.AddChild(footer);

        grid.Measure(new Vector2(320f, 200f));
        var wideDesiredHeight = footer.DesiredSize.Y;

        grid.Arrange(new LayoutRect(0f, 0f, 220f, 200f));

        Assert.True(footer.DesiredSize.Y > wideDesiredHeight + 0.01f);
        Assert.Equal(footer.DesiredSize.Y, footer.ActualHeight, 0.01f);
    }

    [Fact]
    public void Arrange_WhenMeasuredHeightWasInfinite_StillRemeasuresForFiniteWidthShrink()
    {
        var text = new TextBlock
        {
            Text = "The hosted grid is stretched to behave like a real workspace surface rather than a thumbnail. Regression tests still guard header sorting, viewport width, and bottom-of-list scrolling.",
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top
        };

        text.Measure(new Vector2(668f, float.PositiveInfinity));
        var wideDesiredHeight = text.DesiredSize.Y;

        text.Arrange(new LayoutRect(0f, 0f, 420f, 38f));

        Assert.True(text.MeasureCallCount >= 2);
        Assert.True(text.DesiredSize.Y > wideDesiredHeight + 0.01f);
        Assert.Equal(38f, text.ActualHeight, 0.01f);
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
    public void Arrange_LaterAutoRowGrowth_DoesNotCollapseFollowingStarRowAfterEarlierRowSpanMeasurement()
    {
        var grid = new Grid
        {
            Width = 500f,
            Height = 577f
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150f) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new FixedSizeElement(new Vector2(200f, 123f));
        grid.AddChild(header);

        var spanningStatus = new FixedSizeElement(new Vector2(120f, 307f));
        Grid.SetColumn(spanningStatus, 1);
        Grid.SetRow(spanningStatus, 0);
        Grid.SetRowSpan(spanningStatus, 2);
        grid.AddChild(spanningStatus);

        var tallAutoRow = new FixedSizeElement(new Vector2(100f, 269f));
        Grid.SetRow(tallAutoRow, 1);
        grid.AddChild(tallAutoRow);

        var starRow = new FixedSizeElement(new Vector2(100f, 26f));
        Grid.SetRow(starRow, 2);
        grid.AddChild(starRow);

        var footer = new FixedSizeElement(new Vector2(100f, 74f));
        Grid.SetRow(footer, 3);
        Grid.SetColumnSpan(footer, 2);
        grid.AddChild(footer);

        grid.Measure(new Vector2(500f, 577f));
        grid.Arrange(new LayoutRect(0f, 0f, 500f, 577f));

        Assert.Equal(123f, grid.RowDefinitions[0].ActualHeight, 0.01f);
        Assert.Equal(269f, grid.RowDefinitions[1].ActualHeight, 0.01f);
        Assert.Equal(111f, grid.RowDefinitions[2].ActualHeight, 0.01f);
        Assert.Equal(74f, grid.RowDefinitions[3].ActualHeight, 0.01f);
        Assert.True(starRow.ActualHeight > 100f);
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
    public void Measure_GridMaxWidth_ConstrainsWrappedChildBeforeHeightIsCalculated()
    {
        var grid = new Grid
        {
            MaxWidth = 220f
        };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var text = new TextBlock
        {
            Text = "The badge is intentionally translated beyond the frame. ClipToBounds crops it to the Border layout slot, and that clip is rectangular rather than rounded.",
            TextWrapping = TextWrapping.Wrap
        };
        grid.AddChild(text);

        var expectedLayout = TextLayout.LayoutForElement(text.Text, text, text.FontSize, 220f, TextWrapping.Wrap);

        grid.Measure(new Vector2(900f, 260f));

        Assert.True(grid.DesiredSize.X <= 220f + 0.01f);
        Assert.Equal(expectedLayout.Size.Y, text.DesiredSize.Y, 3);
        Assert.Equal(expectedLayout.Size.Y, grid.DesiredSize.Y, 3);
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

    [Fact]
    public void Arrange_SharedSizeScope_SynchronizesColumnWidthsAcrossSiblingGrids()
    {
        var root = new StackPanel();
        Grid.SetIsSharedSizeScope(root, true);

        var firstGrid = CreateSharedWidthGrid(56f);
        var secondGrid = CreateSharedWidthGrid(112f);

        root.AddChild(firstGrid.Grid);
        root.AddChild(secondGrid.Grid);

        root.Measure(new Vector2(320f, 240f));
        root.Arrange(new LayoutRect(0f, 0f, 320f, 240f));
        root.UpdateLayout();

        Assert.Equal(112f, firstGrid.SharedColumn.ActualWidth, 0.01f);
        Assert.Equal(112f, secondGrid.SharedColumn.ActualWidth, 0.01f);
    }

    [Fact]
    public void Arrange_SharedSizeScope_SynchronizesRowHeightsAcrossSiblingGrids()
    {
        var root = new StackPanel();
        Grid.SetIsSharedSizeScope(root, true);

        var firstGrid = CreateSharedHeightGrid(22f);
        var secondGrid = CreateSharedHeightGrid(44f);

        root.AddChild(firstGrid.Grid);
        root.AddChild(secondGrid.Grid);

        root.Measure(new Vector2(320f, 240f));
        root.Arrange(new LayoutRect(0f, 0f, 320f, 240f));
        root.UpdateLayout();

        Assert.Equal(44f, firstGrid.SharedRow.ActualHeight, 0.01f);
        Assert.Equal(44f, secondGrid.SharedRow.ActualHeight, 0.01f);
    }

    [Fact]
    public void SharedSizeGroup_InvalidIdentifier_ThrowsArgumentException()
    {
        var column = new ColumnDefinition();
        var row = new RowDefinition();

        Assert.Throws<ArgumentException>(() => column.SharedSizeGroup = "123Bad");
        Assert.Throws<ArgumentException>(() => row.SharedSizeGroup = "Bad-Group");
    }

    private static (Grid Grid, ColumnDefinition SharedColumn) CreateSharedWidthGrid(float labelWidth)
    {
        var grid = new Grid();
        var sharedColumn = new ColumnDefinition
        {
            Width = GridLength.Auto,
            SharedSizeGroup = "Label"
        };

        grid.ColumnDefinitions.Add(sharedColumn);
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        grid.AddChild(new FixedSizeElement(new Vector2(labelWidth, 18f)));

        var value = new FixedSizeElement(new Vector2(40f, 18f));
        Grid.SetColumn(value, 1);
        grid.AddChild(value);

        return (grid, sharedColumn);
    }

    private static (Grid Grid, RowDefinition SharedRow) CreateSharedHeightGrid(float rowHeight)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80f) });

        var sharedRow = new RowDefinition
        {
            Height = GridLength.Auto,
            SharedSizeGroup = "Details"
        };

        grid.RowDefinitions.Add(sharedRow);
        grid.AddChild(new FixedSizeElement(new Vector2(40f, rowHeight)));

        return (grid, sharedRow);
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

    private sealed class RecordingMeasureElement : FrameworkElement
    {
        private readonly Vector2 _desiredSize;
        private bool _capturedFirstMeasure;

        public RecordingMeasureElement(Vector2 desiredSize)
        {
            _desiredSize = desiredSize;
        }

        public Vector2 FirstAvailableSize { get; private set; }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            if (!_capturedFirstMeasure)
            {
                FirstAvailableSize = availableSize;
                _capturedFirstMeasure = true;
            }

            return new Vector2(
                MathF.Min(_desiredSize.X, availableSize.X),
                MathF.Min(_desiredSize.Y, availableSize.Y));
        }
    }

    private sealed class LateGrowingElement : FrameworkElement
    {
        private float _desiredHeight = 20f;

        public bool ArrangeInvalidatedParent { get; private set; }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            return new Vector2(MathF.Min(40f, availableSize.X), _desiredHeight);
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            if (finalSize.X < 100f && _desiredHeight < 60f)
            {
                _desiredHeight = 60f;
                ArrangeInvalidatedParent = true;
                InvalidateMeasure();
                if (VisualParent is FrameworkElement parent)
                {
                    parent.InvalidateMeasure();
                }
            }

            return finalSize;
        }
    }
}
