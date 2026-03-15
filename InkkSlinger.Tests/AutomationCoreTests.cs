using Xunit;

namespace InkkSlinger.Tests;

public sealed class AutomationCoreTests
{
    [Fact]
    public void GetPeer_ForElementInTree_ReturnsPeerWithStableRuntimeId()
    {
        var host = new Canvas();
        var button = new Button { Content = "Run" };
        host.AddChild(button);
        var uiRoot = new UiRoot(host);

        var first = uiRoot.Automation.GetPeer(button);
        var second = uiRoot.Automation.GetPeer(button);

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.True(first!.RuntimeId > 0);

        uiRoot.Shutdown();
    }

    [Fact]
    public void NameResolution_UsesAutomationPropertiesBeforeControlFallback()
    {
        var button = new Button { Content = "Fallback" };
        AutomationProperties.SetName(button, "ExplicitName");

        var host = new Canvas();
        host.AddChild(button);
        var uiRoot = new UiRoot(host);
        var peer = uiRoot.Automation.GetPeer(button);
        Assert.NotNull(peer);

        Assert.Equal("ExplicitName", peer.GetName());

        uiRoot.Shutdown();
    }
}
