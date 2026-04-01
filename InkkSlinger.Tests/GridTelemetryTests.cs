using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class GridTelemetryTests
{
    [Fact]
    public void GetTelemetryAndReset_ReportsGridLayoutAndCachePaths()
    {
        var host = new Canvas
        {
            Width = 480f,
            Height = 320f
        };
        Grid.SetIsSharedSizeScope(host, true);

        var grid = new Grid
        {
            Width = 320f,
            Height = 220f
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "SharedColumn" });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, SharedSizeGroup = "SharedRow" });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });

        var anchor = new Border { Width = 80f, Height = 28f };
        Grid.SetColumn(anchor, 0);
        Grid.SetRow(anchor, 0);

        var spanning = new Border { Width = 72f, Height = 140f };
        Grid.SetColumn(spanning, 1);
        Grid.SetRow(spanning, 0);
        Grid.SetRowSpan(spanning, 2);

        grid.AddChild(anchor);
        grid.AddChild(spanning);
        host.AddChild(grid);

        var uiRoot = new UiRoot(host);

        _ = Grid.GetTelemetryAndReset();
        RunLayout(uiRoot);

        var first = Grid.GetTelemetryAndReset();
        Assert.True(first.MeasureCallCount > 0);
        Assert.True(first.ArrangeCallCount > 0);
        Assert.True(first.MeasureChildCount >= 2);
        Assert.True(first.MeasureDeferredRowSpanChildCount > 0);
        Assert.True(first.MeasureFirstPassChildCount >= 2);
        Assert.True(first.PrepareChildLayoutMetadataCallCount > 0);
        Assert.True(first.ChildLayoutMetadataCacheRefreshCount > 0);
        Assert.True(first.ChildLayoutMetadataEntryRefreshCount > 0);
        Assert.True(first.MeasureChildCacheMissCount > 0);
        Assert.True(first.ResolveDefinitionSizesCallCount > 0);
        Assert.True(first.ApplyChildRequirementCallCount > 0);
        Assert.True(first.SharedSizeScopeRefreshCallCount > 0);
        Assert.True(first.SharedSizeScopeHitCount > 0);
        Assert.True(first.ApplySharedSizesCallCount > 0);
        Assert.True(first.PublishSharedSizesCallCount > 0);

        grid.InvalidateMeasure();
        RunLayout(uiRoot);

        var second = Grid.GetTelemetryAndReset();
        Assert.True(second.MeasureCallCount > 0);
        Assert.True(second.MeasureChildCallCount > 0);
        Assert.True(second.ChildLayoutMetadataEntryReuseCount > 0);

        var cleared = Grid.GetTelemetryAndReset();
        Assert.Equal(0, cleared.MeasureCallCount);
        Assert.Equal(0, cleared.ArrangeCallCount);
        Assert.Equal(0, cleared.MeasureChildCallCount);
    }

    [Fact]
    public void GetGridSnapshotForDiagnostics_ReportsRuntimeStateOnly()
    {
        var host = new Canvas
        {
            Width = 480f,
            Height = 320f
        };
        Grid.SetIsSharedSizeScope(host, true);

        var grid = new Grid
        {
            Width = 320f,
            Height = 220f,
            ShowGridLines = true,
            Name = "TelemetryGrid"
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "SharedColumn" });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, SharedSizeGroup = "SharedRow" });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });

        var child = new Border { Width = 80f, Height = 28f };
        Grid.SetColumn(child, 0);
        Grid.SetRow(child, 0);
        grid.AddChild(child);
        host.AddChild(grid);

        var uiRoot = new UiRoot(host);

        _ = Grid.GetTelemetryAndReset();
        RunLayout(uiRoot);

        var snapshot = grid.GetGridSnapshotForDiagnostics();
        Assert.True(snapshot.ShowGridLines);
        Assert.Equal(2, snapshot.ColumnDefinitionCount);
        Assert.Equal(2, snapshot.RowDefinitionCount);
        Assert.Equal(1, snapshot.ChildCount);
        Assert.Equal(1, snapshot.FrameworkChildCount);
        Assert.Equal(2, snapshot.MeasuredColumnCount);
        Assert.Equal(2, snapshot.MeasuredRowCount);
        Assert.Equal(1, snapshot.ChildLayoutMetadataCacheCount);
        Assert.False(snapshot.IsChildLayoutMetadataDirty);
        Assert.True(snapshot.HasSharedSizeScope);
        Assert.True(snapshot.MeasureCallCount > 0);
        Assert.True(snapshot.ArrangeCallCount > 0);

        var telemetry = Grid.GetAggregateTelemetrySnapshotForDiagnostics();
        Assert.True(telemetry.MeasureCallCount > 0);
        Assert.True(telemetry.PrepareChildLayoutMetadataCallCount > 0);
        Assert.True(telemetry.ApplySharedSizesCallCount > 0);
    }

    private static void RunLayout(UiRoot uiRoot)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 640, 360));
    }
}