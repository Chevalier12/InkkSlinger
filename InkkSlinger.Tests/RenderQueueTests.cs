using Xunit;

namespace InkkSlinger.Tests;

public class RenderQueueTests
{
    [Fact]
    public void RetainedRenderList_PreservesVisualTraversalOrder()
    {
        var root = new Panel();
        var lowZBorder = new Border();
        var lowZChild = new Border();
        var highZBorder = new Border();
        lowZBorder.Child = lowZChild;
        Panel.SetZIndex(lowZBorder, 0);
        Panel.SetZIndex(highZBorder, 10);
        root.AddChild(lowZBorder);
        root.AddChild(highZBorder);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();

        var order = uiRoot.GetRetainedVisualOrderForTests();
        Assert.Collection(
            order,
            visual => Assert.Same(root, visual),
            visual => Assert.Same(lowZBorder, visual),
            visual => Assert.Same(lowZChild, visual),
            visual => Assert.Same(highZBorder, visual));
    }

    [Fact]
    public void OpacityInvalidation_UpdatesCompositionMetadataWithoutDirtyRenderQueue()
    {
        var root = new Panel();
        var target = new Border();
        target.Name = "metadataTarget";
        root.AddChild(target);
        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.GetTelemetryAndReset();

        target.Opacity = 0.5f;
        target.Opacity = 0.25f;

        var queue = uiRoot.GetDirtyRenderQueueSnapshotForTests();
        Assert.Empty(queue);

        uiRoot.SynchronizeRetainedRenderListForTests();

        var retained = uiRoot.GetRetainedRenderControllerTelemetrySnapshotForTests();
        Assert.Equal(1, retained.OpacityMetadataUpdateCount);
        Assert.Equal(0, retained.ContentInvalidationCount);
        Assert.Equal(0, retained.CompositionMetadataUpdateMissCount);
        Assert.Equal("Border#metadataTarget", retained.LastCompositionMetadataUpdateSource);
    }

    [Fact]
    public void StructureChange_RebuildIncludesNewVisual()
    {
        var root = new Panel();
        var first = new Border();
        root.AddChild(first);
        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();

        var second = new Border();
        root.AddChild(second);
        uiRoot.RebuildRenderListForTests();

        var order = uiRoot.GetRetainedVisualOrderForTests();
        Assert.Collection(
            order,
            visual => Assert.Same(root, visual),
            visual => Assert.Same(first, visual),
            visual => Assert.Same(second, visual));
    }
}
