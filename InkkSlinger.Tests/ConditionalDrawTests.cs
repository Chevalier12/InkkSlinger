using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public class ConditionalDrawTests
{
    [Fact]
    public void FirstFrame_IsForcedToDraw()
    {
        var root = new Panel();
        var uiRoot = new UiRoot(root);

        var shouldDraw = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 1280, 720));

        Assert.True(shouldDraw);
        Assert.NotEqual(UiRedrawReason.None, uiRoot.LastShouldDrawReasons);
    }

    [Fact]
    public void IdleScene_SkipsDraw_AfterInitialFrameAndStateClear()
    {
        var root = new Panel();
        var uiRoot = new UiRoot(root);

        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 1280, 720));

        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        // No new reasons: this frame should skip.
        var shouldDraw = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.FromMilliseconds(32), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 1280, 720));

        Assert.False(shouldDraw);
        Assert.Equal(1, uiRoot.DrawSkippedFrameCount);
    }

    [Fact]
    public void RunningAnimations_ForceDraw()
    {
        var root = new Panel();
        var uiRoot = new UiRoot(root)
        {
            AlwaysDrawCompatibilityMode = false
        };

        uiRoot.SetAnimationActiveForTests(true);

        var shouldDraw = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 1280, 720));

        Assert.True(shouldDraw);
        Assert.True((uiRoot.LastShouldDrawReasons & UiRedrawReason.AnimationActive) != 0);
    }

    [Fact]
    public void AlwaysDrawCompatibilityMode_ForcesDraw()
    {
        var root = new Panel();
        var uiRoot = new UiRoot(root)
        {
            AlwaysDrawCompatibilityMode = true
        };

        var shouldDraw = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 1280, 720));

        Assert.True(shouldDraw);
    }

    [Fact]
    public void DisablingConditionalDrawScheduling_ForcesDrawEvenWhenIdle()
    {
        var root = new Panel();
        var uiRoot = new UiRoot(root)
        {
            UseConditionalDrawScheduling = false
        };

        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 1280, 720));

        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        var shouldDraw = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.FromMilliseconds(32), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 1280, 720));

        Assert.True(shouldDraw);
        Assert.Equal(0, uiRoot.DrawSkippedFrameCount);
    }
}
