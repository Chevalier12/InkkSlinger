using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class RenderSurfaceTests
{
    [Fact]
    public void Measure_WithPixelSurface_UsesSurfaceSize()
    {
        var renderSurface = new RenderSurface();

        renderSurface.Present(ImageSource.FromPixels(64, 32));
        renderSurface.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));

        Assert.Equal(64f, renderSurface.DesiredSize.X);
        Assert.Equal(32f, renderSurface.DesiredSize.Y);
    }

    [Fact]
    public void Present_SameSurfaceReference_InvalidatesRenderWithoutMeasure()
    {
        var renderSurface = new RenderSurface();
        var surface = ImageSource.FromPixels(64, 32);

        renderSurface.Present(surface);
        renderSurface.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
        renderSurface.Arrange(new LayoutRect(0f, 0f, 64f, 32f));
        renderSurface.ClearMeasureInvalidation();
        renderSurface.ClearArrangeInvalidation();
        renderSurface.ClearRenderInvalidationShallow();

        renderSurface.Present(surface);

        Assert.False(renderSurface.NeedsMeasure);
        Assert.False(renderSurface.NeedsArrange);
        Assert.True(renderSurface.NeedsRender);
    }

    [Fact]
    public void Present_NewSurface_InvalidatesMeasureAndUpdatesDesiredSize()
    {
        var renderSurface = new RenderSurface();

        renderSurface.Present(ImageSource.FromPixels(64, 32));
        renderSurface.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
        renderSurface.Arrange(new LayoutRect(0f, 0f, 64f, 32f));
        renderSurface.ClearMeasureInvalidation();
        renderSurface.ClearArrangeInvalidation();
        renderSurface.ClearRenderInvalidationShallow();

        renderSurface.Present(ImageSource.FromPixels(128, 48));

        Assert.True(renderSurface.NeedsMeasure);
        Assert.True(renderSurface.NeedsArrange);
        Assert.True(renderSurface.NeedsRender);

        renderSurface.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));

        Assert.Equal(128f, renderSurface.DesiredSize.X);
        Assert.Equal(48f, renderSurface.DesiredSize.Y);
    }

    [Fact]
    public void RefreshSurface_WithExistingSurface_InvalidatesRenderWithoutMeasure()
    {
        var renderSurface = new RenderSurface();

        renderSurface.Present(ImageSource.FromPixels(64, 32));
        renderSurface.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
        renderSurface.Arrange(new LayoutRect(0f, 0f, 64f, 32f));
        renderSurface.ClearMeasureInvalidation();
        renderSurface.ClearArrangeInvalidation();
        renderSurface.ClearRenderInvalidationShallow();

        renderSurface.RefreshSurface();

        Assert.False(renderSurface.NeedsMeasure);
        Assert.False(renderSurface.NeedsArrange);
        Assert.True(renderSurface.NeedsRender);
    }

    [Fact]
    public void ApplyRenderInvalidationCleanup_WhenNoDirtyRootsRemain_ClearsStaleRenderFlags()
    {
        var root = new Panel();
        var renderSurface = new RenderSurface();
        root.AddChild(renderSurface);

        var uiRoot = new UiRoot(root);
        var viewport = new Viewport(0, 0, 800, 600);

        renderSurface.Present(ImageSource.FromPixels(64, 32));
        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            viewport);

        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        Assert.True(renderSurface.NeedsRender);

        uiRoot.ApplyRenderInvalidationCleanupForTests();

        Assert.False(renderSurface.NeedsRender);
    }

    [Fact]
    public void RefreshSurface_AfterCleanupResetsVisualDirtyFlag_SchedulesDraw()
    {
        var root = new Panel();
        var renderSurface = new RenderSurface();
        root.AddChild(renderSurface);

        var uiRoot = new UiRoot(root);
        var viewport = new Viewport(0, 0, 800, 600);

        renderSurface.Present(ImageSource.FromPixels(64, 32));
        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            viewport);

        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        uiRoot.ApplyRenderInvalidationCleanupForTests();

        renderSurface.RefreshSurface();
        var shouldDraw = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            viewport);

        Assert.True(shouldDraw);
        Assert.True((uiRoot.LastShouldDrawReasons & UiRedrawReason.RenderInvalidated) != 0);
    }
}
