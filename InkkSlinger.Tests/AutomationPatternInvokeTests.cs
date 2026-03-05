using Xunit;

namespace InkkSlinger.Tests;

public sealed class AutomationPatternInvokeTests
{
    [Fact]
    public void ButtonPeer_ExposesInvokePattern_AndRaisesInvokeEvent()
    {
        var host = new Canvas();
        var button = new Button();
        host.AddChild(button);
        var uiRoot = new UiRoot(host);
        var peer = uiRoot.Automation.GetPeer(button);
        Assert.NotNull(peer);

        Assert.True(peer.TryGetPattern(AutomationPatternType.Invoke, out var provider));
        var invoke = Assert.IsAssignableFrom<IInvokeProvider>(provider);

        uiRoot.Automation.BeginFrame();
        invoke.Invoke();
        uiRoot.Automation.EndFrameAndFlush();

        var events = uiRoot.Automation.GetAndClearEventLogForTests();
        Assert.Contains(events, entry => entry.EventType == AutomationEventType.Invoke);

        uiRoot.Shutdown();
    }

    [Fact]
    public void MenuItemPeer_ExposesInvokePattern()
    {
        var host = new Canvas();
        var menuItem = new MenuItem { Header = "Open" };
        host.AddChild(menuItem);
        var uiRoot = new UiRoot(host);
        var peer = uiRoot.Automation.GetPeer(menuItem);
        Assert.NotNull(peer);

        Assert.True(peer.TryGetPattern(AutomationPatternType.Invoke, out var provider));
        Assert.IsAssignableFrom<IInvokeProvider>(provider);

        uiRoot.Shutdown();
    }
}
