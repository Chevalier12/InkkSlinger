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
    public void DisplayedFps_DoesNotUpdateBeforeRefreshInterval()
    {
        var shouldUpdate = Game1.TryComputeDisplayedFps(
            accumulatedFrameCount: 30,
            accumulatedElapsedSeconds: 0.05d,
            out var displayedFps);

        Assert.False(shouldUpdate);
        Assert.Equal(string.Empty, displayedFps);
    }

    [Fact]
    public void DisplayedFps_FormatsFramesPerSecondAfterRefreshInterval()
    {
        var shouldUpdate = Game1.TryComputeDisplayedFps(
            accumulatedFrameCount: 120,
            accumulatedElapsedSeconds: 2d,
            out var displayedFps);

        Assert.True(shouldUpdate);
        Assert.Equal("60.0", displayedFps);
    }

    [Fact]
    public void WindowTitle_BuildsWithDisplayedFpsAndHoveredElement()
    {
        var title = Game1.BuildWindowTitle(
            "InkkSlinger Controls Catalog",
            "60.0",
            "Button#HoverTarget");

        Assert.Equal("InkkSlinger Controls Catalog | App FPS: 60.0 | Hovered: Button#HoverTarget", title);
    }

    [Fact]
    public void WindowTitleParser_ExtractsAppFpsFromCurrentTitleFormat()
    {
        var displayedFps = Game1.ExtractDisplayedFpsFromWindowTitle(
            "InkkSlinger Controls Catalog | App FPS: 60.0 | Hovered: Button#HoverTarget");

        Assert.Equal("60.0", displayedFps);
    }

    [Fact]
    public void WindowTitleParser_ExtractsAppFpsFromLegacyTitleFormat()
    {
        var displayedFps = Game1.ExtractDisplayedFpsFromWindowTitle(
            "InkkSlinger Controls Catalog | FPS: 60.0 | Hovered: Button#HoverTarget");

        Assert.Equal("60.0", displayedFps);
    }

    [Fact]
    public void WindowTitleElementFormatter_UsesTypeAndName()
    {
        var button = new Button { Name = "HoverTarget" };

        var description = Game1.DescribeElementForWindowTitle(button);

        Assert.Equal("Button#HoverTarget", description);
    }

    [Fact]
    public void WindowTitleElementFormatter_UsesNullWhenNothingHovered()
    {
        var description = Game1.DescribeElementForWindowTitle(null);

        Assert.Equal("null", description);
    }
}
