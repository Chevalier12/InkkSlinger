using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public class UiRootTelemetryTests
{
    [Fact]
    public void MetricsSnapshot_TracksLayoutAndDrawSkipCounters()
    {
        AnimationManager.Current.ResetForTests();
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

    [Fact]
    public void VisualTreeMetricsSnapshot_AggregatesLayoutAndUpdateCounters()
    {
        var root = new StackPanel();
        root.AddChild(new Label { Text = "Header" });
        root.AddChild(new ProgressBar { IsIndeterminate = true });
        root.AddChild(new Border { Child = new Label { Text = "Body" } });

        var uiRoot = new UiRoot(root);
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 800, 600));

        var snapshot = uiRoot.GetVisualTreeMetricsSnapshot();

        Assert.True(snapshot.VisualCount >= 4);
        Assert.True(snapshot.FrameworkElementCount >= 4);
        Assert.True(snapshot.HighCostVisualCount >= 2);
        Assert.True(snapshot.MaxDepth >= 2);
        Assert.True(snapshot.MeasureCallCount >= 4);
        Assert.True(snapshot.ArrangeCallCount >= 4);
        Assert.Equal(1, snapshot.UpdateCallCount);
        Assert.True(snapshot.MeasureInvalidationCount >= 1);

        var perfSnapshot = uiRoot.GetPerformanceTelemetrySnapshotForTests();
        Assert.Equal(1, perfSnapshot.FrameUpdateParticipantCount);
    }

    [Fact]
    public void PassiveVisualTree_UpdatePhase_DoesNotWalkNonParticipantVisuals()
    {
        var root = new StackPanel();
        root.AddChild(new Label { Text = "Header" });
        root.AddChild(new Border { Child = new Label { Text = "Body" } });

        var uiRoot = new UiRoot(root);
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 800, 600));

        var snapshot = uiRoot.GetVisualTreeMetricsSnapshot();
        var perfSnapshot = uiRoot.GetPerformanceTelemetrySnapshotForTests();

        Assert.Equal(0, snapshot.UpdateCallCount);
        Assert.Equal(0, perfSnapshot.FrameUpdateParticipantCount);
    }

    [Fact]
    public void FrameUpdateParticipants_TrackVisualAttachAndDetach()
    {
        var root = new Panel();
        var progressBar = new ProgressBar
        {
            IsIndeterminate = true
        };
        root.AddChild(progressBar);

        var uiRoot = new UiRoot(root);
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 320, 180));
        Assert.Equal(1, uiRoot.GetPerformanceTelemetrySnapshotForTests().FrameUpdateParticipantCount);

        root.RemoveChild(progressBar);
        progressBar.IsIndeterminate = false;

        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(32), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 320, 180));
        Assert.Equal(0, uiRoot.GetPerformanceTelemetrySnapshotForTests().FrameUpdateParticipantCount);
    }

    [Fact]
    public void MetricsSnapshot_TracksRetainedTreeRebuildsAndStructureChanges()
    {
        var root = new Panel();
        var uiRoot = new UiRoot(root);

        var child = new Border();
        root.AddChild(child);

        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        child.InvalidateVisual();
        uiRoot.SynchronizeRetainedRenderListForTests();

        var snapshot = uiRoot.GetMetricsSnapshot();

        Assert.True(snapshot.VisualStructureChangeCount >= 1);
        Assert.True(snapshot.RetainedFullRebuildCount >= 1);
        Assert.True(snapshot.RetainedSubtreeSyncCount >= 1);
        Assert.True(snapshot.RetainedRenderNodeCount >= 2);
        Assert.True(snapshot.LastRetainedDirtyVisualCount >= 1);
    }
}
