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
    public void Arrange_ShrinkingGridWidths_KeepsWrappedTextConstrainedToCurrentStarColumn()
    {
        var grid = new Grid
        {
            Height = 260f
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var badge = new FixedSizeElement(new Vector2(52f, 20f));
        grid.AddChild(badge);

        var text = new TextBlock
        {
            Text = "Wrapping should continue to honor the active star column width even after the same Grid has already been measured and arranged at a wider size.",
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetColumn(text, 1);
        grid.AddChild(text);

        var widths = new[] { 320f, 260f, 220f, 180f, 150f, 128f, 112f, 96f };
        foreach (var width in widths)
        {
            MeasureArrangeAndUpdate(grid, width, 260f);

            var textColumnWidth = width - badge.DesiredSize.X;
            var expectedLayout = TextLayout.LayoutForElement(text.Text, text, text.FontSize, textColumnWidth, TextWrapping.Wrap);

            Assert.Equal(textColumnWidth, grid.ColumnDefinitions[1].ActualWidth, 0.01f);
            Assert.Equal(textColumnWidth, text.LayoutSlot.Width, 0.01f);
            Assert.True(text.DesiredSize.X <= textColumnWidth + 0.01f);
            Assert.Equal(expectedLayout.Size.Y, text.DesiredSize.Y, 3);
            Assert.Equal(expectedLayout.Size.Y, text.ActualHeight, 3);
        }
    }

    [Fact]
    public void Arrange_ShrinkingSharedSizeGridWidths_RemeasuresWrappedStarColumnContent()
    {
        var scopeHost = new StackPanel();
        Grid.SetIsSharedSizeScope(scopeHost, true);

        var primary = new Grid();
        primary.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "Label" });
        primary.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        primary.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "Meta" });
        primary.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        scopeHost.AddChild(primary);

        var primaryLabel = new TextBlock { Text = "Owner", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0f, 0f, 10f, 0f) };
        var primaryValue = new TextBlock { Text = "Layout crew", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0f, 0f, 10f, 0f) };
        var primaryMeta = new TextBlock { Text = "Ready", TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(primaryValue, 1);
        Grid.SetColumn(primaryMeta, 2);
        primary.AddChild(primaryLabel);
        primary.AddChild(primaryValue);
        primary.AddChild(primaryMeta);

        var secondary = new Grid();
        secondary.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "Label" });
        secondary.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        secondary.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "Meta" });
        secondary.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        scopeHost.AddChild(secondary);

        var secondaryLabel = new TextBlock { Text = "Longer dependency label", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0f, 0f, 10f, 0f) };
        var secondaryValue = new TextBlock
        {
            Text = "Shared groups synchronize both label and badge columns while the middle star lane still needs to rewrap as the host narrows.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0f, 0f, 10f, 0f),
            VerticalAlignment = VerticalAlignment.Top
        };
        var secondaryMeta = new TextBlock { Text = "Pinned", TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(secondaryValue, 1);
        Grid.SetColumn(secondaryMeta, 2);
        secondary.AddChild(secondaryLabel);
        secondary.AddChild(secondaryValue);
        secondary.AddChild(secondaryMeta);

        var widths = new[] { 420f, 360f, 320f, 280f, 250f, 220f, 196f };
        foreach (var width in widths)
        {
            MeasureArrangeAndUpdate(scopeHost, width, 220f);

            var textAvailableWidth = secondary.ColumnDefinitions[1].ActualWidth - secondaryValue.Margin.Horizontal;

            var expectedLayout = TextLayout.LayoutForElement(
                secondaryValue.Text,
                secondaryValue,
                secondaryValue.FontSize,
                textAvailableWidth,
                TextWrapping.Wrap);

            Assert.Equal(textAvailableWidth, secondaryValue.LayoutSlot.Width, 0.01f);
            Assert.Equal(expectedLayout.Size.Y, secondaryValue.DesiredSize.Y, 3);
            Assert.Equal(expectedLayout.Size.Y, secondaryValue.ActualHeight, 3);
        }
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
    public void Measure_StarColumn_WithInfiniteAvailableWidth_UsesContentSize()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var child = new FixedSizeElement(new Vector2(80f, 20f));
        grid.AddChild(child);

        grid.Measure(new Vector2(float.PositiveInfinity, 120f));

        Assert.Equal(80f, grid.DesiredSize.X, 0.01f);
        Assert.Equal(20f, grid.DesiredSize.Y, 0.01f);
    }

    [Fact]
    public void Arrange_SharedSizeScope_TreatsStarSharedColumnAsAutoForSizing()
    {
        var root = new StackPanel();
        Grid.SetIsSharedSizeScope(root, true);

        var firstGrid = new Grid();
        var firstSharedColumn = new ColumnDefinition
        {
            Width = GridLength.Star,
            SharedSizeGroup = "Label"
        };
        firstGrid.ColumnDefinitions.Add(firstSharedColumn);
        firstGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        firstGrid.AddChild(new FixedSizeElement(new Vector2(56f, 18f)));

        var secondGrid = new Grid();
        var secondSharedColumn = new ColumnDefinition
        {
            Width = GridLength.Auto,
            SharedSizeGroup = "Label"
        };
        secondGrid.ColumnDefinitions.Add(secondSharedColumn);
        secondGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        secondGrid.AddChild(new FixedSizeElement(new Vector2(112f, 18f)));

        root.AddChild(firstGrid);
        root.AddChild(secondGrid);

        root.Measure(new Vector2(320f, 240f));
        root.Arrange(new LayoutRect(0f, 0f, 320f, 240f));
        root.UpdateLayout();

        Assert.Equal(112f, firstSharedColumn.ActualWidth, 0.01f);
        Assert.Equal(112f, secondSharedColumn.ActualWidth, 0.01f);
    }

    [Fact]
    public void Arrange_LaterAutoColumnGrowth_DoesNotCollapseFollowingStarColumnAfterEarlierColumnSpanMeasurement()
    {
        var grid = new Grid
        {
            Width = 577f,
            Height = 120f
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var leadingAuto = new FixedSizeElement(new Vector2(123f, 24f));
        grid.AddChild(leadingAuto);

        var earlySpan = new FixedSizeElement(new Vector2(307f, 24f));
        Grid.SetColumnSpan(earlySpan, 2);
        grid.AddChild(earlySpan);

        var laterAuto = new FixedSizeElement(new Vector2(269f, 24f));
        Grid.SetColumn(laterAuto, 1);
        grid.AddChild(laterAuto);

        var starColumnChild = new FixedSizeElement(new Vector2(26f, 24f));
        Grid.SetColumn(starColumnChild, 2);
        grid.AddChild(starColumnChild);

        var trailingAuto = new FixedSizeElement(new Vector2(74f, 24f));
        Grid.SetColumn(trailingAuto, 3);
        grid.AddChild(trailingAuto);

        grid.Measure(new Vector2(577f, 120f));
        grid.Arrange(new LayoutRect(0f, 0f, 577f, 120f));

        Assert.Equal(123f, grid.ColumnDefinitions[0].ActualWidth, 0.01f);
        Assert.Equal(269f, grid.ColumnDefinitions[1].ActualWidth, 0.01f);
        Assert.Equal(111f, grid.ColumnDefinitions[2].ActualWidth, 0.01f);
        Assert.Equal(74f, grid.ColumnDefinitions[3].ActualWidth, 0.01f);
        Assert.True(starColumnChild.ActualWidth > 100f);
    }

    [Fact]
    public void Arrange_WeightedStarColumns_DistributeProportionally()
    {
        var grid = new Grid();
        var first = new ColumnDefinition { Width = new GridLength(1f, GridUnitType.Star) };
        var second = new ColumnDefinition { Width = new GridLength(2f, GridUnitType.Star) };
        var third = new ColumnDefinition { Width = new GridLength(1f, GridUnitType.Star) };
        grid.ColumnDefinitions.Add(first);
        grid.ColumnDefinitions.Add(second);
        grid.ColumnDefinitions.Add(third);
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        MeasureArrangeAndUpdate(grid, 400f, 80f);

        Assert.Equal(100f, first.ActualWidth, 0.01f);
        Assert.Equal(200f, second.ActualWidth, 0.01f);
        Assert.Equal(100f, third.ActualWidth, 0.01f);
    }

    [Fact]
    public void Arrange_WeightedStarRows_DistributeProportionally()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80f) });
        var first = new RowDefinition { Height = new GridLength(1f, GridUnitType.Star) };
        var second = new RowDefinition { Height = new GridLength(3f, GridUnitType.Star) };
        grid.RowDefinitions.Add(first);
        grid.RowDefinitions.Add(second);

        MeasureArrangeAndUpdate(grid, 120f, 200f);

        Assert.Equal(50f, first.ActualHeight, 0.01f);
        Assert.Equal(150f, second.ActualHeight, 0.01f);
    }

    [Fact]
    public void Arrange_StarColumns_RespectMinimumWidths()
    {
        var grid = new Grid();
        var first = new ColumnDefinition
        {
            Width = GridLength.Star,
            MinWidth = 90f
        };
        var second = new ColumnDefinition { Width = GridLength.Star };
        grid.ColumnDefinitions.Add(first);
        grid.ColumnDefinitions.Add(second);
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        MeasureArrangeAndUpdate(grid, 120f, 60f);

        Assert.Equal(90f, first.ActualWidth, 0.01f);
        Assert.Equal(30f, second.ActualWidth, 0.01f);
    }

    [Fact]
    public void Measure_InfiniteHeightStarRow_UsesContentSize()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });

        var child = new FixedSizeElement(new Vector2(20f, 80f));
        grid.AddChild(child);

        grid.Measure(new Vector2(120f, float.PositiveInfinity));

        Assert.Equal(20f, grid.DesiredSize.X, 0.01f);
        Assert.Equal(80f, grid.DesiredSize.Y, 0.01f);
    }

    [Fact]
    public void Arrange_RowAndColumnOutOfRange_AreCoercedToLastDefinitions()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40f) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60f) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10f) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30f) });

        var child = new FixedSizeElement(new Vector2(10f, 10f));
        Grid.SetColumn(child, 99);
        Grid.SetRow(child, 99);
        grid.AddChild(child);

        MeasureArrangeAndUpdate(grid, 100f, 40f);

        Assert.Equal(40f, child.LayoutSlot.X, 0.01f);
        Assert.Equal(10f, child.LayoutSlot.Y, 0.01f);
        Assert.Equal(60f, child.LayoutSlot.Width, 0.01f);
        Assert.Equal(30f, child.LayoutSlot.Height, 0.01f);
    }

    [Fact]
    public void Arrange_ColumnSpanBeyondAvailableColumns_IsClamped()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10f) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20f) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30f) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(15f) });

        var child = new FixedSizeElement(new Vector2(10f, 10f));
        Grid.SetColumn(child, 1);
        Grid.SetColumnSpan(child, 99);
        grid.AddChild(child);

        MeasureArrangeAndUpdate(grid, 60f, 15f);

        Assert.Equal(10f, child.LayoutSlot.X, 0.01f);
        Assert.Equal(50f, child.LayoutSlot.Width, 0.01f);
    }

    [Fact]
    public void Arrange_RowSpanBeyondAvailableRows_IsClamped()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20f) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10f) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20f) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30f) });

        var child = new FixedSizeElement(new Vector2(10f, 10f));
        Grid.SetRow(child, 1);
        Grid.SetRowSpan(child, 99);
        grid.AddChild(child);

        MeasureArrangeAndUpdate(grid, 20f, 60f);

        Assert.Equal(10f, child.LayoutSlot.Y, 0.01f);
        Assert.Equal(50f, child.LayoutSlot.Height, 0.01f);
    }

    [Fact]
    public void Arrange_ChangingAttachedColumn_RepositionsChildOnUpdateLayout()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50f) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70f) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20f) });

        var child = new FixedSizeElement(new Vector2(10f, 10f));
        grid.AddChild(child);

        MeasureArrangeAndUpdate(grid, 120f, 20f);
        Assert.Equal(0f, child.LayoutSlot.X, 0.01f);

        Grid.SetColumn(child, 1);
        grid.UpdateLayout();

        Assert.Equal(50f, child.LayoutSlot.X, 0.01f);
        Assert.Equal(70f, child.LayoutSlot.Width, 0.01f);
    }

    [Fact]
    public void Arrange_ChangingColumnDefinitionWidth_RepositionsSecondColumnChild()
    {
        var grid = new Grid();
        var first = new ColumnDefinition { Width = new GridLength(50f) };
        grid.ColumnDefinitions.Add(first);
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70f) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20f) });

        var child = new FixedSizeElement(new Vector2(10f, 10f));
        Grid.SetColumn(child, 1);
        grid.AddChild(child);

        MeasureArrangeAndUpdate(grid, 120f, 20f);
        Assert.Equal(50f, child.LayoutSlot.X, 0.01f);

        first.Width = new GridLength(80f);
        grid.UpdateLayout();

        Assert.Equal(80f, child.LayoutSlot.X, 0.01f);
    }

    [Fact]
    public void Arrange_AddingWiderAutoChild_ExpandsAutoColumnOnUpdateLayout()
    {
        var grid = new Grid();
        var auto = new ColumnDefinition { Width = GridLength.Auto };
        grid.ColumnDefinitions.Add(auto);
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        grid.AddChild(new FixedSizeElement(new Vector2(40f, 10f)));

        MeasureArrangeAndUpdate(grid, 200f, 40f);
        Assert.Equal(40f, auto.ActualWidth, 0.01f);

        grid.AddChild(new FixedSizeElement(new Vector2(90f, 10f)));
        grid.UpdateLayout();

        Assert.Equal(90f, auto.ActualWidth, 0.01f);
    }

    [Fact]
    public void Arrange_RemovingWiderAutoChild_ShrinksAutoColumnOnUpdateLayout()
    {
        var grid = new Grid();
        var auto = new ColumnDefinition { Width = GridLength.Auto };
        grid.ColumnDefinitions.Add(auto);
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        grid.AddChild(new FixedSizeElement(new Vector2(40f, 10f)));
        var wider = new FixedSizeElement(new Vector2(90f, 10f));
        grid.AddChild(wider);

        MeasureArrangeAndUpdate(grid, 200f, 40f);
        Assert.Equal(90f, auto.ActualWidth, 0.01f);

        Assert.True(grid.RemoveChild(wider));
        grid.UpdateLayout();

        Assert.Equal(40f, auto.ActualWidth, 0.01f);
    }

    [Fact]
    public void Arrange_SharedSizeScope_Disabled_LeavesColumnsIndependent()
    {
        var root = new StackPanel();

        var firstGrid = CreateSharedWidthGrid(56f);
        var secondGrid = CreateSharedWidthGrid(112f);

        root.AddChild(firstGrid.Grid);
        root.AddChild(secondGrid.Grid);

        MeasureArrangeAndUpdate(root, 320f, 240f);

        Assert.Equal(56f, firstGrid.SharedColumn.ActualWidth, 0.01f);
        Assert.Equal(112f, secondGrid.SharedColumn.ActualWidth, 0.01f);
    }

    [Fact]
    public void Arrange_SharedSizeScope_TreatsStarSharedRowAsAutoForSizing()
    {
        var root = new StackPanel();
        Grid.SetIsSharedSizeScope(root, true);

        var firstGrid = new Grid();
        firstGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80f) });
        var firstSharedRow = new RowDefinition
        {
            Height = GridLength.Star,
            SharedSizeGroup = "Details"
        };
        firstGrid.RowDefinitions.Add(firstSharedRow);
        firstGrid.AddChild(new FixedSizeElement(new Vector2(40f, 22f)));

        var secondGrid = new Grid();
        secondGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80f) });
        var secondSharedRow = new RowDefinition
        {
            Height = GridLength.Auto,
            SharedSizeGroup = "Details"
        };
        secondGrid.RowDefinitions.Add(secondSharedRow);
        secondGrid.AddChild(new FixedSizeElement(new Vector2(40f, 44f)));

        root.AddChild(firstGrid);
        root.AddChild(secondGrid);

        MeasureArrangeAndUpdate(root, 320f, 240f);

        Assert.Equal(44f, firstSharedRow.ActualHeight, 0.01f);
        Assert.Equal(44f, secondSharedRow.ActualHeight, 0.01f);
    }

    [Fact]
    public void Arrange_NestedSharedSizeScopes_IsolateGroupsByNearestScope()
    {
        var root = new StackPanel();
        Grid.SetIsSharedSizeScope(root, true);

        var outerGrid = CreateSharedWidthGrid(120f);
        root.AddChild(outerGrid.Grid);

        var innerScope = new StackPanel();
        Grid.SetIsSharedSizeScope(innerScope, true);
        var innerFirst = CreateSharedWidthGrid(40f);
        var innerSecond = CreateSharedWidthGrid(60f);
        innerScope.AddChild(innerFirst.Grid);
        innerScope.AddChild(innerSecond.Grid);
        root.AddChild(innerScope);

        MeasureArrangeAndUpdate(root, 320f, 240f);

        Assert.Equal(120f, outerGrid.SharedColumn.ActualWidth, 0.01f);
        Assert.Equal(60f, innerFirst.SharedColumn.ActualWidth, 0.01f);
        Assert.Equal(60f, innerSecond.SharedColumn.ActualWidth, 0.01f);
    }

    [Fact]
    public void Arrange_RemovingLargestSharedWidthContributor_ShrinksRemainingMembers()
    {
        var root = new StackPanel();
        Grid.SetIsSharedSizeScope(root, true);

        var firstGrid = CreateSharedWidthGrid(56f);
        var secondGrid = CreateSharedWidthGrid(112f);
        root.AddChild(firstGrid.Grid);
        root.AddChild(secondGrid.Grid);

        MeasureArrangeAndUpdate(root, 320f, 240f);
        Assert.Equal(112f, firstGrid.SharedColumn.ActualWidth, 0.01f);

        Assert.True(root.RemoveChild(secondGrid.Grid));
        MeasureArrangeAndUpdate(root, 320f, 240f);

        Assert.Equal(56f, firstGrid.SharedColumn.ActualWidth, 0.01f);
    }

    [Fact]
    public void Arrange_RemovingLargestSharedHeightContributor_ShrinksRemainingMembers()
    {
        var root = new StackPanel();
        Grid.SetIsSharedSizeScope(root, true);

        var firstGrid = CreateSharedHeightGrid(22f);
        var secondGrid = CreateSharedHeightGrid(44f);
        root.AddChild(firstGrid.Grid);
        root.AddChild(secondGrid.Grid);

        MeasureArrangeAndUpdate(root, 320f, 240f);
        Assert.Equal(44f, firstGrid.SharedRow.ActualHeight, 0.01f);

        Assert.True(root.RemoveChild(secondGrid.Grid));
        MeasureArrangeAndUpdate(root, 320f, 240f);

        Assert.Equal(22f, firstGrid.SharedRow.ActualHeight, 0.01f);
    }

    [Fact]
    public void Arrange_ChangingSharedWidthContributor_ReflowsSiblingGrid()
    {
        var root = new StackPanel();
        Grid.SetIsSharedSizeScope(root, true);

        var firstGrid = new Grid();
        var firstShared = new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "Label" };
        firstGrid.ColumnDefinitions.Add(firstShared);
        firstGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var mutable = new MutableSizeElement(new Vector2(40f, 18f));
        firstGrid.AddChild(mutable);

        var secondGrid = new Grid();
        var secondShared = new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "Label" };
        secondGrid.ColumnDefinitions.Add(secondShared);
        secondGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        secondGrid.AddChild(new FixedSizeElement(new Vector2(70f, 18f)));

        root.AddChild(firstGrid);
        root.AddChild(secondGrid);

        MeasureArrangeAndUpdate(root, 320f, 240f);
        Assert.Equal(70f, firstShared.ActualWidth, 0.01f);

        mutable.SetDesiredSize(new Vector2(120f, 18f));
        MeasureArrangeAndUpdate(root, 320f, 240f);

        Assert.Equal(120f, firstShared.ActualWidth, 0.01f);
        Assert.Equal(120f, secondShared.ActualWidth, 0.01f);
    }

    [Fact]
    public void SharedSizeGroup_WhitespaceIsTrimmed_AndWhitespaceOnlyBecomesNull()
    {
        var column = new ColumnDefinition();
        var row = new RowDefinition();

        column.SharedSizeGroup = " Label_01 ";
        row.SharedSizeGroup = "   ";

        Assert.Equal("Label_01", column.SharedSizeGroup);
        Assert.Null(row.SharedSizeGroup);
    }

    [Fact]
    public void SharedSizeGroup_IdentifierWithDigitsAndUnderscore_IsAccepted()
    {
        var column = new ColumnDefinition();
        var row = new RowDefinition();

        column.SharedSizeGroup = "Group_01";
        row.SharedSizeGroup = "Details_2026";

        Assert.Equal("Group_01", column.SharedSizeGroup);
        Assert.Equal("Details_2026", row.SharedSizeGroup);
    }

    [Fact]
    public void ColumnDefinition_MinAndMaxWidth_CoerceEachOther()
    {
        var column = new ColumnDefinition { MaxWidth = 20f };

        column.MinWidth = 30f;
        Assert.Equal(30f, column.MinWidth, 0.01f);
        Assert.Equal(30f, column.MaxWidth, 0.01f);

        column.MaxWidth = 10f;
        Assert.Equal(10f, column.MinWidth, 0.01f);
        Assert.Equal(10f, column.MaxWidth, 0.01f);
    }

    [Fact]
    public void RowDefinition_MinAndMaxHeight_CoerceEachOther()
    {
        var row = new RowDefinition { MaxHeight = 20f };

        row.MinHeight = 30f;
        Assert.Equal(30f, row.MinHeight, 0.01f);
        Assert.Equal(30f, row.MaxHeight, 0.01f);

        row.MaxHeight = 10f;
        Assert.Equal(10f, row.MinHeight, 0.01f);
        Assert.Equal(10f, row.MaxHeight, 0.01f);
    }

    [Fact]
    public void ColumnDefinition_MaxWidth_ZeroOrNegative_ClampsToZero()
    {
        var column = new ColumnDefinition();

        column.MaxWidth = -5f;
        Assert.Equal(0f, column.MaxWidth, 0.01f);

        column.MaxWidth = 0f;
        Assert.Equal(0f, column.MaxWidth, 0.01f);
    }

    [Fact]
    public void RowDefinition_MaxHeight_ZeroOrNegative_ClampsToZero()
    {
        var row = new RowDefinition();

        row.MaxHeight = -5f;
        Assert.Equal(0f, row.MaxHeight, 0.01f);

        row.MaxHeight = 0f;
        Assert.Equal(0f, row.MaxHeight, 0.01f);
    }

    [Fact]
    public void Measure_PixelDefinitions_RespectMinAndMaxConstraints()
    {
        var grid = new Grid();
        var first = new ColumnDefinition
        {
            Width = new GridLength(20f),
            MinWidth = 50f
        };
        var second = new ColumnDefinition
        {
            Width = new GridLength(200f),
            MaxWidth = 80f
        };
        var row = new RowDefinition
        {
            Height = new GridLength(100f),
            MaxHeight = 40f
        };
        grid.ColumnDefinitions.Add(first);
        grid.ColumnDefinitions.Add(second);
        grid.RowDefinitions.Add(row);

        MeasureArrangeAndUpdate(grid, 300f, 100f);

        Assert.Equal(50f, first.ActualWidth, 0.01f);
        Assert.Equal(80f, second.ActualWidth, 0.01f);
        Assert.Equal(40f, row.ActualHeight, 0.01f);
    }

    [Fact]
    public void Arrange_ShowGridLines_DoesNotChangeDefinitionSizes()
    {
        var first = CreateTwoColumnGrid(showGridLines: false);
        var second = CreateTwoColumnGrid(showGridLines: true);

        MeasureArrangeAndUpdate(first, 200f, 40f);
        MeasureArrangeAndUpdate(second, 200f, 40f);

        Assert.Equal(first.ColumnDefinitions[0].ActualWidth, second.ColumnDefinitions[0].ActualWidth, 0.01f);
        Assert.Equal(first.ColumnDefinitions[1].ActualWidth, second.ColumnDefinitions[1].ActualWidth, 0.01f);
        Assert.Equal(first.RowDefinitions[0].ActualHeight, second.RowDefinitions[0].ActualHeight, 0.01f);
    }

    [Fact]
    public void Arrange_SpanningChildAcrossAllColumns_UsesTotalResolvedWidth()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10f) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20f) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30f) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10f) });

        var child = new FixedSizeElement(new Vector2(5f, 5f));
        Grid.SetColumnSpan(child, 3);
        grid.AddChild(child);

        MeasureArrangeAndUpdate(grid, 60f, 10f);

        Assert.Equal(60f, child.LayoutSlot.Width, 0.01f);
    }

    [Fact]
    public void Arrange_SpanningChildAcrossAllRows_UsesTotalResolvedHeight()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10f) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10f) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20f) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30f) });

        var child = new FixedSizeElement(new Vector2(5f, 5f));
        Grid.SetRowSpan(child, 3);
        grid.AddChild(child);

        MeasureArrangeAndUpdate(grid, 10f, 60f);

        Assert.Equal(60f, child.LayoutSlot.Height, 0.01f);
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

    private static Grid CreateTwoColumnGrid(bool showGridLines)
    {
        var grid = new Grid
        {
            ShowGridLines = showGridLines
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.AddChild(new FixedSizeElement(new Vector2(40f, 18f)));

        var trailing = new FixedSizeElement(new Vector2(20f, 18f));
        Grid.SetColumn(trailing, 1);
        grid.AddChild(trailing);
        return grid;
    }

    private static void MeasureArrangeAndUpdate(FrameworkElement element, float width, float height)
    {
        element.Measure(new Vector2(width, height));
        element.Arrange(new LayoutRect(0f, 0f, width, height));
        element.UpdateLayout();
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

    private sealed class MutableSizeElement : FrameworkElement
    {
        private Vector2 _desiredSize;

        public MutableSizeElement(Vector2 desiredSize)
        {
            _desiredSize = desiredSize;
        }

        public void SetDesiredSize(Vector2 desiredSize)
        {
            _desiredSize = desiredSize;
            InvalidateMeasure();
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
