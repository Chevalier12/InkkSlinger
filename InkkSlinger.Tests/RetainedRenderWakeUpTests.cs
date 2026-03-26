using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class RetainedRenderWakeUpTests
{
    [Fact]
    public void DirtyVisual_WhenRootStateWasCleared_ReschedulesItself()
    {
        var root = new Panel();
        var target = new Border();
        root.AddChild(target);

        var uiRoot = new UiRoot(root);
        var viewport = new Viewport(0, 0, 800, 600);

        target.InvalidateVisual();
        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            viewport);

        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        Assert.False(uiRoot.HasPendingRenderInvalidation);

        target.InvalidateVisual();
        var shouldDraw = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            viewport);

        Assert.True(shouldDraw);
        Assert.True((uiRoot.LastShouldDrawReasons & UiRedrawReason.RenderInvalidated) != 0);
        Assert.True(target.NeedsRender);
    }

    [Fact]
    public void SiblingInvalidation_WakesFrame_WhenAnotherVisualIsStuckDirty()
    {
        var root = new Panel();
        var target = new Border();
        var sibling = new Border();
        root.AddChild(target);
        root.AddChild(sibling);

        var uiRoot = new UiRoot(root);
        var viewport = new Viewport(0, 0, 800, 600);

        target.InvalidateVisual();
        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            viewport);

        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        Assert.False(uiRoot.HasPendingRenderInvalidation);

        sibling.InvalidateVisual();
        var shouldDraw = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            viewport);

        Assert.True(shouldDraw);
        Assert.True((uiRoot.LastShouldDrawReasons & UiRedrawReason.RenderInvalidated) != 0);
    }

    [Fact]
    public void DirtyVisual_RequeuesItself_EvenWhenSiblingAlreadyQueued()
    {
        var root = new Panel();
        var target = new Border();
        var sibling = new Border();
        root.AddChild(target);
        root.AddChild(sibling);

        var uiRoot = new UiRoot(root);
        var viewport = new Viewport(0, 0, 800, 600);

        target.InvalidateVisual();
        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            viewport);

        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        Assert.Empty(uiRoot.GetDirtyRenderQueueSnapshotForTests());

        sibling.InvalidateVisual();
        target.InvalidateVisual();

        var dirtyQueue = uiRoot.GetDirtyRenderQueueSnapshotForTests();

        Assert.Contains(sibling, dirtyQueue);
        Assert.Contains(target, dirtyQueue);
    }
}
