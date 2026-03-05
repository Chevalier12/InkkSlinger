using Xunit;

namespace InkkSlinger.Tests;

public sealed class AutomationFocusEventTests
{
    [Fact]
    public void FocusChanged_IsCoalescedPerFrame()
    {
        var host = new Canvas();
        var first = new TextBox();
        var second = new TextBox();
        host.AddChild(first);
        host.AddChild(second);

        var uiRoot = new UiRoot(host);
        uiRoot.Automation.BeginFrame();
        uiRoot.Automation.NotifyFocusChanged(first, second);
        uiRoot.Automation.NotifyFocusChanged(first, second);
        uiRoot.Automation.EndFrameAndFlush();

        var events = uiRoot.Automation.GetAndClearEventLogForTests();
        Assert.Single(events);
        Assert.Equal(AutomationEventType.FocusChanged, events[0].EventType);

        uiRoot.Shutdown();
    }
}
