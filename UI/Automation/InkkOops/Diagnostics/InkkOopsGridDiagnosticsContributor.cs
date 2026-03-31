namespace InkkSlinger;

public sealed class InkkOopsGridDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 50;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        if (element is not Grid grid)
        {
            return;
        }

        var timing = Grid.GetTimingSnapshotForTests();
        builder.Add("desired", $"{grid.DesiredSize.X:0.##},{grid.DesiredSize.Y:0.##}");
        builder.Add("actual", $"{grid.ActualWidth:0.##},{grid.ActualHeight:0.##}");
        builder.Add("previousAvailable", $"{grid.PreviousAvailableSizeForTests.X:0.##},{grid.PreviousAvailableSizeForTests.Y:0.##}");
        builder.Add("measureCalls", grid.MeasureCallCount);
        builder.Add("measureWork", grid.MeasureWorkCount);
        builder.Add("arrangeCalls", grid.ArrangeCallCount);
        builder.Add("arrangeWork", grid.ArrangeWorkCount);
        builder.Add("measureValid", grid.IsMeasureValidForTests);
        builder.Add("arrangeValid", grid.IsArrangeValidForTests);
        builder.Add("measureInvalidations", grid.MeasureInvalidationCount);
        builder.Add("arrangeInvalidations", grid.ArrangeInvalidationCount);
        builder.Add("renderInvalidations", grid.RenderInvalidationCount);
        builder.Add("measureMs", FormatMilliseconds(grid.MeasureElapsedTicksForTests));
        builder.Add("arrangeMs", FormatMilliseconds(grid.ArrangeElapsedTicksForTests));
        builder.Add("gridGlobalMeasureOverrideMs", FormatMilliseconds(timing.MeasureOverrideElapsedTicks));
        builder.Add("gridGlobalArrangeOverrideMs", FormatMilliseconds(timing.ArrangeOverrideElapsedTicks));
        builder.Add("columns", BuildColumnSummary(grid));
        builder.Add("rows", BuildRowSummary(grid));
        builder.Add("children", BuildChildrenSummary(grid));
    }

    private static string BuildColumnSummary(Grid grid)
    {
        if (grid.ColumnDefinitions.Count == 0)
        {
            return "implicit:*";
        }

        var parts = new string[grid.ColumnDefinitions.Count];
        for (var i = 0; i < grid.ColumnDefinitions.Count; i++)
        {
            var definition = grid.ColumnDefinitions[i];
            var sharedSizeGroup = definition.SharedSizeGroup ?? "-";
            parts[i] = $"{i}:{FormatGridLength(definition.Width)},min={definition.MinWidth:0.##},max={definition.MaxWidth:0.##},actual={definition.ActualWidth:0.##},shared={sharedSizeGroup}";
        }

        return string.Join(" | ", parts);
    }

    private static string BuildRowSummary(Grid grid)
    {
        if (grid.RowDefinitions.Count == 0)
        {
            return "implicit:*";
        }

        var parts = new string[grid.RowDefinitions.Count];
        for (var i = 0; i < grid.RowDefinitions.Count; i++)
        {
            var definition = grid.RowDefinitions[i];
            var sharedSizeGroup = definition.SharedSizeGroup ?? "-";
            parts[i] = $"{i}:{FormatGridLength(definition.Height)},min={definition.MinHeight:0.##},max={definition.MaxHeight:0.##},actual={definition.ActualHeight:0.##},shared={sharedSizeGroup}";
        }

        return string.Join(" | ", parts);
    }

    private static string BuildChildrenSummary(Grid grid)
    {
        if (grid.Children.Count == 0)
        {
            return "none";
        }

        var parts = new string[grid.Children.Count];
        for (var i = 0; i < grid.Children.Count; i++)
        {
            var child = grid.Children[i];
            var row = Grid.GetRow(child);
            var column = Grid.GetColumn(child);
            var rowSpan = Grid.GetRowSpan(child);
            var columnSpan = Grid.GetColumnSpan(child);
            if (child is FrameworkElement frameworkChild)
            {
                var invalidation = frameworkChild.InvalidationDiagnosticsForTests;
                parts[i] = $"{i}:{DescribeElement(child)}@r{row}c{column}rs{rowSpan}cs{columnSpan},slot={FormatRect(frameworkChild.LayoutSlot)},desired={frameworkChild.DesiredSize.X:0.##},{frameworkChild.DesiredSize.Y:0.##},actual={frameworkChild.ActualWidth:0.##},{frameworkChild.ActualHeight:0.##},prevAvail={frameworkChild.PreviousAvailableSizeForTests.X:0.##},{frameworkChild.PreviousAvailableSizeForTests.Y:0.##},mc={frameworkChild.MeasureCallCount}/{frameworkChild.MeasureWorkCount},ac={frameworkChild.ArrangeCallCount}/{frameworkChild.ArrangeWorkCount},mi={frameworkChild.MeasureInvalidationCount},ai={frameworkChild.ArrangeInvalidationCount},mlast={invalidation.LastMeasureInvalidationSummary},mtop={invalidation.TopMeasureInvalidationSources}";
            }
            else
            {
                parts[i] = $"{i}:{DescribeElement(child)}@r{row}c{column}rs{rowSpan}cs{columnSpan}";
            }
        }

        return string.Join(" | ", parts);
    }

    private static string FormatGridLength(GridLength length)
    {
        if (length.IsAuto)
        {
            return "Auto";
        }

        if (length.IsStar)
        {
            return length.Value == 1f ? "*" : $"{length.Value:0.##}*";
        }

        return $"{length.Value:0.##}";
    }

    private static string FormatMilliseconds(long ticks)
    {
        return $"{ticks * 1000d / System.Diagnostics.Stopwatch.Frequency:0.###}";
    }

    private static string FormatRect(LayoutRect rect)
    {
        return $"{rect.X:0.##},{rect.Y:0.##},{rect.Width:0.##},{rect.Height:0.##}";
    }

    private static string DescribeElement(UIElement element)
    {
        var name = element is FrameworkElement frameworkElement ? frameworkElement.Name : string.Empty;
        return string.IsNullOrWhiteSpace(name)
            ? element.GetType().Name
            : $"{element.GetType().Name}#{name}";
    }
}
