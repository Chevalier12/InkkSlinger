namespace InkkSlinger;

public sealed class InkkOopsBorderDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 40;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        if (element is not Border border)
        {
            return;
        }

        var runtime = border.GetBorderSnapshotForDiagnostics();

        builder.Add("desired", $"{border.DesiredSize.X:0.##},{border.DesiredSize.Y:0.##}");
        builder.Add("previousAvailable", $"{border.PreviousAvailableSizeForTests.X:0.##},{border.PreviousAvailableSizeForTests.Y:0.##}");
        builder.Add("measureCalls", border.MeasureCallCount);
        builder.Add("measureWork", border.MeasureWorkCount);
        builder.Add("arrangeCalls", border.ArrangeCallCount);
        builder.Add("arrangeWork", border.ArrangeWorkCount);
        builder.Add("measureValid", border.IsMeasureValidForTests);
        builder.Add("arrangeValid", border.IsArrangeValidForTests);
        builder.Add("hasVisibleBackground", runtime.HasVisibleBackground);
        builder.Add("hasVisibleBorder", runtime.HasVisibleBorder);
        builder.Add("hasRenderStateCache", runtime.HasRenderStateCache);
        builder.Add("hasOuterRadiiCache", runtime.HasOuterRadiiCache);
        builder.Add("hasRoundedGeometryCache", runtime.HasRoundedGeometryCache);
        builder.Add("runtimeMeasureOverrideCalls", runtime.MeasureOverrideCallCount);
        builder.Add("runtimeMeasureOverrideMs", $"{runtime.MeasureOverrideMilliseconds:0.###}");
        builder.Add("runtimeArrangeOverrideCalls", runtime.ArrangeOverrideCallCount);
        builder.Add("runtimeArrangeOverrideMs", $"{runtime.ArrangeOverrideMilliseconds:0.###}");
        builder.Add("runtimeRenderCalls", runtime.RenderCallCount);
        builder.Add("runtimeRenderMs", $"{runtime.RenderMilliseconds:0.###}");
        builder.Add("runtimeRectangularPath", runtime.RenderRectangularPathCount);
        builder.Add("runtimeRoundedPath", runtime.RenderRoundedPathCount);
        builder.Add("runtimeTextureCacheHits", runtime.RenderTextureCacheHitCount);
        builder.Add("runtimeTextureCacheMisses", runtime.RenderTextureCacheMissCount);
        builder.Add("runtimeTextureBuilds", runtime.TextureBuildCount);
        builder.Add("runtimeTextureBuildMs", $"{runtime.TextureBuildMilliseconds:0.###}");
        builder.Add("runtimeRenderStateCacheHits", runtime.RenderStateCacheHitCount);
        builder.Add("runtimeRenderStateCacheMisses", runtime.RenderStateCacheMissCount);
        builder.Add("runtimeOuterRadiiCacheHits", runtime.OuterRadiiCacheHitCount);
        builder.Add("runtimeOuterRadiiCacheMisses", runtime.OuterRadiiCacheMissCount);
        builder.Add("runtimeRoundedGeometryCacheHits", runtime.RoundedGeometryCacheHitCount);
        builder.Add("runtimeRoundedGeometryCacheMisses", runtime.RoundedGeometryCacheMissCount);
        builder.Add("runtimeBackgroundBrushChanges", runtime.BackgroundBrushChangeCount);
        builder.Add("runtimeBackgroundBrushMutations", runtime.BackgroundBrushMutationCount);
        builder.Add("runtimeBorderBrushChanges", runtime.BorderBrushChangeCount);
        builder.Add("runtimeBorderBrushMutations", runtime.BorderBrushMutationCount);
        builder.Add("runtimeBorderThicknessChanges", runtime.BorderThicknessPropertyChangeCount);
        builder.Add("runtimeCornerRadiusChanges", runtime.CornerRadiusPropertyChangeCount);
        builder.Add("runtimeRoundedFillPoints", runtime.RoundedFillPolygonPointCount);
    }
}
