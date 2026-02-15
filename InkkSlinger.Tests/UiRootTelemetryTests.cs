using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public class UiRootTelemetryTests
{
    [Fact]
    public void MetricsSnapshot_TracksLayoutAndDrawSkipCounters()
    {
        var root = new Panel();
        var uiRoot = new UiRoot(root);
        var viewport = new Viewport(0, 0, 1280, 720);

        uiRoot.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)), viewport);
        _ = uiRoot.ShouldDrawThisFrame(new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)), viewport);

        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();

        uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(32), TimeSpan.FromMilliseconds(16)), viewport);
        _ = uiRoot.ShouldDrawThisFrame(new GameTime(TimeSpan.FromMilliseconds(32), TimeSpan.FromMilliseconds(16)), viewport);

        var snapshot = uiRoot.GetMetricsSnapshot();
        Assert.True(snapshot.LayoutExecutedFrameCount >= 1);
        Assert.True(snapshot.LayoutSkippedFrameCount >= 1);
        Assert.True(snapshot.DrawSkippedFrameCount >= 1);
    }

    [Fact]
    public void MetricsSnapshot_ReflectsRolloutToggleStates()
    {
        var root = new Panel();
        var uiRoot = new UiRoot(root)
        {
            UseRetainedRenderList = false,
            UseDirtyRegionRendering = false,
            UseConditionalDrawScheduling = false
        };

        var snapshot = uiRoot.GetMetricsSnapshot();
        Assert.False(snapshot.UseRetainedRenderList);
        Assert.False(snapshot.UseDirtyRegionRendering);
        Assert.False(snapshot.UseConditionalDrawScheduling);
    }

    [Fact]
    public void DisablingDirtyRegionRendering_SkipsDirtyRegionAccumulation()
    {
        var root = new Panel();
        var child = new Border();
        root.AddChild(child);
        child.SetLayoutSlot(new LayoutRect(10f, 10f, 20f, 20f));

        var uiRoot = new UiRoot(root)
        {
            UseDirtyRegionRendering = false
        };
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();

        child.InvalidateVisual();

        Assert.Empty(uiRoot.GetDirtyRegionsSnapshotForTests());
        Assert.False(uiRoot.IsFullDirtyForTests());
    }
}
