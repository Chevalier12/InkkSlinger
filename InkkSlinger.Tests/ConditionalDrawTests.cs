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
        AnimationManager.Current.ResetForTests();
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

    [Fact]
    public void HoldEndAnimation_DoesNotKeepAnimationActiveAfterSettling()
    {
        AnimationManager.Current.ResetForTests();
        try
        {
            var root = new Panel();
            var uiRoot = new UiRoot(root)
            {
                UseConditionalDrawScheduling = true,
                AlwaysDrawCompatibilityMode = false
            };

            var storyboard = new Storyboard();
            storyboard.Children.Add(new DoubleAnimation
            {
                TargetProperty = "Opacity",
                To = 0.35f,
                Duration = TimeSpan.FromMilliseconds(80),
                FillBehavior = FillBehavior.HoldEnd
            });

            storyboard.Begin(root);

            var viewport = new Viewport(0, 0, 1280, 720);
            uiRoot.Update(new GameTime(TimeSpan.Zero, TimeSpan.Zero), viewport);
            _ = uiRoot.ShouldDrawThisFrame(new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)), viewport);
            uiRoot.CompleteDrawStateForTests();
            uiRoot.ResetDirtyStateForTests();

            uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(16)), viewport);
            uiRoot.CompleteDrawStateForTests();
            uiRoot.ResetDirtyStateForTests();

            var shouldDraw = uiRoot.ShouldDrawThisFrame(new GameTime(TimeSpan.FromMilliseconds(216), TimeSpan.FromMilliseconds(16)), viewport);

            Assert.False(shouldDraw);
            Assert.True(Math.Abs(root.Opacity - 0.35f) < 0.001f);
            Assert.True((uiRoot.LastShouldDrawReasons & UiRedrawReason.AnimationActive) == 0);
        }
        finally
        {
            AnimationManager.Current.ResetForTests();
        }
    }
}
