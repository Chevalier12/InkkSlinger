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
}
