using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class BorderRenderCacheTests
{
    [Fact]
    public void RoundedGeometryCache_ReusesCachedPolygons_ForStableLayout()
    {
        var border = new Border
        {
            Width = 120f,
            Height = 48f,
            BorderThickness = new Thickness(2f),
            CornerRadius = new CornerRadius(8f)
        };

        border.Measure(new Vector2(120f, 48f));
        border.Arrange(new LayoutRect(10f, 20f, 120f, 48f));

        Border.ResetRoundedGeometryCacheBuildCountForTests();

        border.BuildRoundedGeometryCacheForTests();
        border.BuildRoundedGeometryCacheForTests();

        Assert.Equal(1, Border.GetRoundedGeometryCacheBuildCountForTests());
    }

    [Fact]
    public void RoundedGeometryCache_Rebuilds_WhenLayoutChanges()
    {
        var border = new Border
        {
            Width = 120f,
            Height = 48f,
            BorderThickness = new Thickness(2f),
            CornerRadius = new CornerRadius(8f)
        };

        border.Measure(new Vector2(120f, 48f));
        border.Arrange(new LayoutRect(10f, 20f, 120f, 48f));

        Border.ResetRoundedGeometryCacheBuildCountForTests();

        border.BuildRoundedGeometryCacheForTests();
        border.CornerRadius = new CornerRadius(10f);
        border.BuildRoundedGeometryCacheForTests();

        Assert.Equal(2, Border.GetRoundedGeometryCacheBuildCountForTests());
    }

    [Fact]
    public void RenderStateCache_ReusesResolvedBrushAndThicknessState_ForStableInputs()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.CornflowerBlue),
            BorderBrush = new SolidColorBrush(Color.White),
            BorderThickness = new Thickness(2f)
        };

        Border.ResetRenderStateCacheBuildCountForTests();

        border.ResolveRenderStateForTests();
        border.ResolveRenderStateForTests();

        Assert.Equal(1, Border.GetRenderStateCacheBuildCountForTests());

        border.BorderThickness = new Thickness(3f);
        border.ResolveRenderStateForTests();

        Assert.Equal(2, Border.GetRenderStateCacheBuildCountForTests());
    }

    [Fact]
    public void TelemetrySnapshots_ReportLayoutCacheAndBrushActivity()
    {
        _ = Border.GetTelemetryAndReset();

        var backgroundBrush = new SolidColorBrush(Color.CornflowerBlue);
        var borderBrush = new SolidColorBrush(Color.White);
        var border = new Border
        {
            Background = backgroundBrush,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(2f),
            Padding = new Thickness(3f),
            CornerRadius = new CornerRadius(8f),
            Child = new TextBlock { Text = "Telemetry" }
        };

        border.Measure(new Vector2(120f, 48f));
        border.Arrange(new LayoutRect(10f, 20f, 120f, 48f));
        border.ResolveRenderStateForTests();
        border.ResolveRenderStateForTests();
        border.BuildRoundedGeometryCacheForTests();
        border.BuildRoundedGeometryCacheForTests();

        border.BorderThickness = new Thickness(4f);
        border.CornerRadius = new CornerRadius(10f);
        backgroundBrush.Color = Color.OrangeRed;
        borderBrush.Color = Color.DarkSlateBlue;

        border.ResolveRenderStateForTests();
        border.BuildRoundedGeometryCacheForTests();

        var runtime = border.GetBorderSnapshotForDiagnostics();
        Assert.True(runtime.HasChild);
        Assert.True(runtime.MeasureOverrideCallCount > 0);
        Assert.True(runtime.MeasureOverrideChildMeasureCount > 0);
        Assert.True(runtime.ArrangeOverrideCallCount > 0);
        Assert.True(runtime.ArrangeOverrideChildArrangeCount > 0);
        Assert.True(runtime.RenderStateCacheHitCount > 0);
        Assert.True(runtime.RenderStateCacheMissCount > 0);
        Assert.True(runtime.OuterRadiiCacheHitCount > 0);
        Assert.True(runtime.OuterRadiiCacheMissCount > 0);
        Assert.True(runtime.RoundedGeometryCacheHitCount > 0);
        Assert.True(runtime.RoundedGeometryCacheMissCount > 0);
        Assert.True(runtime.BackgroundBrushChangeCount > 0);
        Assert.True(runtime.BackgroundBrushAttachCount > 0);
        Assert.True(runtime.BackgroundBrushMutationCount > 0);
        Assert.True(runtime.BorderBrushChangeCount > 0);
        Assert.True(runtime.BorderBrushAttachCount > 0);
        Assert.True(runtime.BorderBrushMutationCount > 0);
        Assert.True(runtime.BorderThicknessPropertyChangeCount > 0);
        Assert.True(runtime.CornerRadiusPropertyChangeCount > 0);
        Assert.True(runtime.RenderStateInvalidationCount > 0);
        Assert.True(runtime.OuterRadiiInvalidationCount > 0);
        Assert.True(runtime.RoundedGeometryBuildPointCount > 0);
        
        var aggregate = Border.GetTelemetryAndReset();
        Assert.True(aggregate.MeasureOverrideCallCount > 0);
        Assert.True(aggregate.ArrangeOverrideCallCount > 0);
        Assert.True(aggregate.RenderStateCacheHitCount > 0);
        Assert.True(aggregate.RenderStateCacheMissCount > 0);
        Assert.True(aggregate.OuterRadiiCacheHitCount > 0);
        Assert.True(aggregate.OuterRadiiCacheMissCount > 0);
        Assert.True(aggregate.RoundedGeometryCacheHitCount > 0);
        Assert.True(aggregate.RoundedGeometryCacheMissCount > 0);
        Assert.True(aggregate.BackgroundBrushMutationCount > 0);
        Assert.True(aggregate.BorderBrushMutationCount > 0);
        Assert.True(aggregate.BorderThicknessPropertyChangeCount > 0);
        Assert.True(aggregate.CornerRadiusPropertyChangeCount > 0);
        Assert.True(aggregate.RoundedGeometryBuildPointCount > 0);

        var cleared = Border.GetTelemetryAndReset();
        Assert.Equal(0, cleared.MeasureOverrideCallCount);
        Assert.Equal(0, cleared.RenderStateCacheMissCount);
        Assert.Equal(0, cleared.RoundedGeometryCacheMissCount);
        Assert.Equal(0, cleared.BackgroundBrushMutationCount);
    }
}
