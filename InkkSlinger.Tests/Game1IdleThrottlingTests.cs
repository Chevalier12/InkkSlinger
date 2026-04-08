using Xunit;

namespace InkkSlinger.Tests;

public sealed class InkkSlingerGameHostBehaviorTests
{
    [Fact]
    public void ActiveIdleFrame_UsesIdleThrottleDelay()
    {
        var delay = InkkSlingerGameHost.GetIdleThrottleDelayMilliseconds(isActive: true, shouldDrawUiThisFrame: false);

        Assert.Equal(8, delay);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void NonIdleOrInactiveFrames_DoNotThrottle(bool isActive, bool shouldDrawUiThisFrame)
    {
        var delay = InkkSlingerGameHost.GetIdleThrottleDelayMilliseconds(isActive, shouldDrawUiThisFrame);

        Assert.Equal(0, delay);
    }

    [Fact]
    public void CompositeTargetRecreate_ForcesDrawEvenWhenUpdateScheduledSkip()
    {
        var shouldDraw = InkkSlingerGameHost.ShouldDrawUiOnCurrentFrame(scheduledDraw: false, targetRecreated: true);

        Assert.True(shouldDraw);
    }

    [Fact]
    public void NoCompositeTargetRecreate_PreservesScheduledSkip()
    {
        var shouldDraw = InkkSlingerGameHost.ShouldDrawUiOnCurrentFrame(scheduledDraw: false, targetRecreated: false);

        Assert.False(shouldDraw);
    }

    [Fact]
    public void ScheduledDraw_RemainsTrueWithoutCompositeTargetRecreate()
    {
        var shouldDraw = InkkSlingerGameHost.ShouldDrawUiOnCurrentFrame(scheduledDraw: true, targetRecreated: false);

        Assert.True(shouldDraw);
    }

    [Fact]
    public void DisplayedFps_DoesNotUpdateBeforeRefreshInterval()
    {
        var shouldUpdate = InkkSlingerGameHost.TryComputeDisplayedFps(
            accumulatedFrameCount: 30,
            accumulatedElapsedSeconds: 0.05d,
            out var displayedFps);

        Assert.False(shouldUpdate);
        Assert.Equal(string.Empty, displayedFps);
    }

    [Fact]
    public void DisplayedFps_FormatsFramesPerSecondAfterRefreshInterval()
    {
        var shouldUpdate = InkkSlingerGameHost.TryComputeDisplayedFps(
            accumulatedFrameCount: 120,
            accumulatedElapsedSeconds: 2d,
            out var displayedFps);

        Assert.True(shouldUpdate);
        Assert.Equal("60.0", displayedFps);
    }

    [Fact]
    public void WindowTitle_BuildsWithDisplayedFpsAndHoveredElement()
    {
        var title = InkkSlingerGameHost.BuildWindowTitle(
            "InkkSlinger Controls Catalog",
            "60.0",
            "Button#HoverTarget");

        Assert.Equal("InkkSlinger Controls Catalog | App FPS: 60.0 | Hovered: Button#HoverTarget", title);
    }

    [Fact]
    public void WindowTitleParser_ExtractsAppFpsFromCurrentTitleFormat()
    {
        var displayedFps = InkkSlingerGameHost.ExtractDisplayedFpsFromWindowTitle(
            "InkkSlinger Controls Catalog | App FPS: 60.0 | Hovered: Button#HoverTarget");

        Assert.Equal("60.0", displayedFps);
    }

    [Fact]
    public void WindowTitleParser_ExtractsAppFpsFromLegacyTitleFormat()
    {
        var displayedFps = InkkSlingerGameHost.ExtractDisplayedFpsFromWindowTitle(
            "InkkSlinger Controls Catalog | FPS: 60.0 | Hovered: Button#HoverTarget");

        Assert.Equal("60.0", displayedFps);
    }

    [Fact]
    public void WindowTitleElementFormatter_UsesTypeAndName()
    {
        var button = new Button { Name = "HoverTarget" };

        var description = InkkSlingerGameHost.DescribeElementForWindowTitle(button);

        Assert.Equal("Button#HoverTarget", description);
    }

    [Fact]
    public void WindowTitleElementFormatter_UsesNullWhenNothingHovered()
    {
        var description = InkkSlingerGameHost.DescribeElementForWindowTitle(null);

        Assert.Equal("null", description);
    }
}
