using Xunit;

namespace InkkSlinger.Tests;

public sealed class AutomationPatternExpandCollapseTests
{
    [Fact]
    public void ExpanderPeer_ExposesExpandCollapsePattern()
    {
        var host = new Canvas();
        var expander = new Expander { IsExpanded = false };
        host.AddChild(expander);
        var uiRoot = new UiRoot(host);
        var peer = uiRoot.Automation.GetPeer(expander);
        Assert.NotNull(peer);

        Assert.True(peer.TryGetPattern(AutomationPatternType.ExpandCollapse, out var pattern));
        var expandCollapse = Assert.IsAssignableFrom<IExpandCollapseProvider>(pattern);

        expandCollapse.Expand();
        Assert.True(expander.IsExpanded);

        expandCollapse.Collapse();
        Assert.False(expander.IsExpanded);

        uiRoot.Shutdown();
    }
}
