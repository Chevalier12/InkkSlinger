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

        var runtime = grid.GetGridSnapshotForDiagnostics();
        var telemetry = Grid.GetAggregateTelemetrySnapshotForDiagnostics();
        builder.Add("desired", $"{runtime.DesiredWidth:0.##},{runtime.DesiredHeight:0.##}");
        builder.Add("actual", $"{runtime.ActualWidth:0.##},{runtime.ActualHeight:0.##}");
        builder.Add("renderSize", $"{runtime.RenderWidth:0.##},{runtime.RenderHeight:0.##}");
        builder.Add("previousAvailable", $"{runtime.PreviousAvailableWidth:0.##},{runtime.PreviousAvailableHeight:0.##}");
        builder.Add("measureCalls", runtime.MeasureCallCount);
        builder.Add("measureWork", runtime.MeasureWorkCount);
        builder.Add("arrangeCalls", runtime.ArrangeCallCount);
        builder.Add("arrangeWork", runtime.ArrangeWorkCount);
        builder.Add("measureValid", runtime.IsMeasureValid);
        builder.Add("arrangeValid", runtime.IsArrangeValid);
        builder.Add("measureInvalidations", runtime.MeasureInvalidationCount);
        builder.Add("arrangeInvalidations", runtime.ArrangeInvalidationCount);
        builder.Add("renderInvalidations", runtime.RenderInvalidationCount);
        builder.Add("measureMs", FormatMilliseconds(runtime.MeasureMilliseconds));
        builder.Add("measureExclusiveMs", FormatMilliseconds(runtime.MeasureExclusiveMilliseconds));
        builder.Add("arrangeMs", FormatMilliseconds(runtime.ArrangeMilliseconds));
        builder.Add("showGridLines", runtime.ShowGridLines);
        builder.Add("gridRuntimeColumnDefinitions", runtime.ColumnDefinitionCount);
        builder.Add("gridRuntimeRowDefinitions", runtime.RowDefinitionCount);
        builder.Add("gridRuntimeChildren", runtime.ChildCount);
        builder.Add("gridRuntimeFrameworkChildren", runtime.FrameworkChildCount);
        builder.Add("gridRuntimeMeasuredColumns", runtime.MeasuredColumnCount);
        builder.Add("gridRuntimeMeasuredRows", runtime.MeasuredRowCount);
        builder.Add("gridRuntimeMetadataEntries", runtime.ChildLayoutMetadataCacheCount);
        builder.Add("gridRuntimeMetadataDirty", runtime.IsChildLayoutMetadataDirty);
        builder.Add("gridRuntimeSharedScopeAttached", runtime.HasSharedSizeScope);
        builder.Add("gridRuntimeSharedScopeOwner", runtime.IsSharedSizeScopeOwner);
        builder.Add("gridGlobalMeasureCalls", telemetry.MeasureCallCount);
        builder.Add("gridGlobalMeasureMs", FormatMilliseconds(telemetry.MeasureMilliseconds));
        builder.Add("gridGlobalMeasureChildren", telemetry.MeasureChildCount);
        builder.Add("gridGlobalDeferredRowSpanChildren", telemetry.MeasureDeferredRowSpanChildCount);
        builder.Add("gridGlobalFirstPassChildren", telemetry.MeasureFirstPassChildCount);
        builder.Add("gridGlobalSecondPassChildren", telemetry.MeasureSecondPassChildCount);
        builder.Add("gridGlobalRemeasureChecks", telemetry.MeasureRemeasureCheckCount);
        builder.Add("gridGlobalRemeasures", telemetry.MeasureRemeasureCount);
        builder.Add("gridGlobalRemeasureSkips", telemetry.MeasureRemeasureSkipCount);
        builder.Add("gridGlobalArrangeCalls", telemetry.ArrangeCallCount);
        builder.Add("gridGlobalArrangeMs", FormatMilliseconds(telemetry.ArrangeMilliseconds));
        builder.Add("gridGlobalArrangedChildren", telemetry.ArrangeChildCount);
        builder.Add("gridGlobalArrangeSkippedChildren", telemetry.ArrangeSkippedChildCount);
        builder.Add("gridGlobalPrepareMetadataCalls", telemetry.PrepareChildLayoutMetadataCallCount);
        builder.Add("gridGlobalPrepareMetadataMs", FormatMilliseconds(telemetry.PrepareChildLayoutMetadataMilliseconds));
        builder.Add("gridGlobalMetadataCacheRefreshes", telemetry.ChildLayoutMetadataCacheRefreshCount);
        builder.Add("gridGlobalMetadataEntryRefreshes", telemetry.ChildLayoutMetadataEntryRefreshCount);
        builder.Add("gridGlobalMetadataEntryReuses", telemetry.ChildLayoutMetadataEntryReuseCount);
        builder.Add("gridGlobalMetadataInvalidations", telemetry.ChildLayoutMetadataInvalidationCount);
        builder.Add("gridGlobalMeasureChildCalls", telemetry.MeasureChildCallCount);
        builder.Add("gridGlobalMeasureChildMs", FormatMilliseconds(telemetry.MeasureChildMilliseconds));
        builder.Add("gridGlobalMeasureChildCacheHits", telemetry.MeasureChildCacheHitCount);
        builder.Add("gridGlobalMeasureChildCacheMisses", telemetry.MeasureChildCacheMissCount);
        builder.Add("gridGlobalResolveDefinitionCalls", telemetry.ResolveDefinitionSizesCallCount);
        builder.Add("gridGlobalResolveDefinitionMs", FormatMilliseconds(telemetry.ResolveDefinitionSizesMilliseconds));
        builder.Add("gridGlobalResolveFiniteAvailable", telemetry.ResolveDefinitionFiniteAvailableCount);
        builder.Add("gridGlobalResolveInfiniteAvailable", telemetry.ResolveDefinitionInfiniteAvailableCount);
        builder.Add("gridGlobalResolveNaNAvailable", telemetry.ResolveDefinitionNaNAvailableCount);
        builder.Add("gridGlobalApplyRequirementCalls", telemetry.ApplyChildRequirementCallCount);
        builder.Add("gridGlobalApplyRequirementMs", FormatMilliseconds(telemetry.ApplyChildRequirementMilliseconds));
        builder.Add("gridGlobalApplyRequirementChanges", telemetry.ApplyChildRequirementChangedCount);
        builder.Add("gridGlobalApplyRequirementNoOps", telemetry.ApplyChildRequirementNoOpCount);
        builder.Add("gridGlobalFiniteStarConstraintUses", telemetry.ApplyChildRequirementFiniteStarConstraintCount);
        builder.Add("gridGlobalOverflowChecks", telemetry.NormalizeDefinitionOverflowCallCount);
        builder.Add("gridGlobalOverflowTriggers", telemetry.NormalizeDefinitionOverflowTriggeredCount);
        builder.Add("gridGlobalDistributeExtraCalls", telemetry.DistributeExtraSizeCallCount);
        builder.Add("gridGlobalReduceOverflowCalls", telemetry.ReduceOverflowCallCount);
        builder.Add("gridGlobalSharedScopeRefreshes", telemetry.SharedSizeScopeRefreshCallCount);
        builder.Add("gridGlobalSharedScopeHits", telemetry.SharedSizeScopeHitCount);
        builder.Add("gridGlobalSharedScopeMisses", telemetry.SharedSizeScopeMissCount);
        builder.Add("gridGlobalSharedScopeChanges", telemetry.SharedSizeScopeChangedCount);
        builder.Add("gridGlobalApplySharedSizesCalls", telemetry.ApplySharedSizesCallCount);
        builder.Add("gridGlobalApplySharedSizeDefinitions", telemetry.ApplySharedSizeDefinitionCount);
        builder.Add("gridGlobalPublishSharedSizesCalls", telemetry.PublishSharedSizesCallCount);
        builder.Add("gridGlobalPublishSharedSizeDefinitions", telemetry.PublishSharedSizeDefinitionCount);
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

    private static string FormatMilliseconds(double milliseconds)
    {
        return milliseconds.ToString("0.###");
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
