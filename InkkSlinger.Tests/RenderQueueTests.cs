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
    public void RenderInvalidation_QueuesDirtyNodeOnce()
    {
        var root = new Panel();
        var target = new Border();
        root.AddChild(target);
        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();

        target.Opacity = 0.5f;
        target.Opacity = 0.25f;

        var queue = uiRoot.GetDirtyRenderQueueSnapshotForTests();
        Assert.Single(queue);
        Assert.Same(target, queue[0]);
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
