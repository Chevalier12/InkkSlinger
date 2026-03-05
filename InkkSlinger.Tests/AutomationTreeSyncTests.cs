using Xunit;

namespace InkkSlinger.Tests;

public sealed class AutomationTreeSyncTests
{
    [Fact]
    public void AddAndRemoveChild_RebuildsPeerTree()
    {
        var host = new Canvas();
        var uiRoot = new UiRoot(host);
        var button = new Button();

        Assert.Null(uiRoot.Automation.GetPeer(button));

        host.AddChild(button);
        var addedPeer = uiRoot.Automation.GetPeer(button);
        Assert.NotNull(addedPeer);

        host.RemoveChild(button);
        Assert.Null(uiRoot.Automation.GetPeer(button));

        uiRoot.Shutdown();
    }
}
