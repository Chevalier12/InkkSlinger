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
}
