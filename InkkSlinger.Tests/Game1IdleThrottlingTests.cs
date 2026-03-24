using Xunit;

namespace InkkSlinger.Tests;

public sealed class Game1IdleThrottlingTests
{
    [Fact]
    public void ActiveIdleFrame_UsesIdleThrottleDelay()
    {
        var delay = Game1.GetIdleThrottleDelayMilliseconds(isActive: true, shouldDrawUiThisFrame: false);

        Assert.Equal(8, delay);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void NonIdleOrInactiveFrames_DoNotThrottle(bool isActive, bool shouldDrawUiThisFrame)
    {
        var delay = Game1.GetIdleThrottleDelayMilliseconds(isActive, shouldDrawUiThisFrame);

        Assert.Equal(0, delay);
    }

    [Fact]
    public void CompositeTargetRecreate_ForcesDrawEvenWhenUpdateScheduledSkip()
    {
        var shouldDraw = Game1.ShouldDrawUiOnCurrentFrame(scheduledDraw: false, targetRecreated: true);

        Assert.True(shouldDraw);
    }

    [Fact]
    public void NoCompositeTargetRecreate_PreservesScheduledSkip()
    {
        var shouldDraw = Game1.ShouldDrawUiOnCurrentFrame(scheduledDraw: false, targetRecreated: false);

        Assert.False(shouldDraw);
    }

    [Fact]
    public void ScheduledDraw_RemainsTrueWithoutCompositeTargetRecreate()
    {
        var shouldDraw = Game1.ShouldDrawUiOnCurrentFrame(scheduledDraw: true, targetRecreated: false);

        Assert.True(shouldDraw);
    }

    [Fact]
    public void FpsTitle_DoesNotUpdateBeforeOneSecond()
    {
        var shouldUpdate = Game1.TryBuildFpsWindowTitle(
            "InkkSlinger Controls Catalog",
            accumulatedFrameCount: 30,
            accumulatedElapsedSeconds: 0.5d,
            out var title);

        Assert.False(shouldUpdate);
        Assert.Equal("InkkSlinger Controls Catalog", title);
    }

    [Fact]
    public void FpsTitle_FormatsFramesPerSecondAfterOneSecond()
    {
        var shouldUpdate = Game1.TryBuildFpsWindowTitle(
            "InkkSlinger Controls Catalog",
            accumulatedFrameCount: 120,
            accumulatedElapsedSeconds: 2d,
            out var title);

        Assert.True(shouldUpdate);
        Assert.Equal("InkkSlinger Controls Catalog | FPS: 60.0", title);
    }
}
