using Xunit;

namespace InkkSlinger.Tests;

public sealed class AutomationPatternSelectionTests
{
    [Fact]
    public void ListBoxPeer_ExposesSelectionPattern()
    {
        var host = new Canvas();
        var listBox = new ListBox();
        listBox.Items.Add("One");
        listBox.Items.Add("Two");
        listBox.SelectedIndex = 1;
        host.AddChild(listBox);

        var uiRoot = new UiRoot(host);
        var listPeer = uiRoot.Automation.GetPeer(listBox);
        Assert.NotNull(listPeer);

        Assert.True(listPeer.TryGetPattern(AutomationPatternType.Selection, out var selectionPattern));
        var selectionProvider = Assert.IsAssignableFrom<ISelectionProvider>(selectionPattern);
        var selected = selectionProvider.GetSelection();

        Assert.Single(selected);

        uiRoot.Shutdown();
    }

    [Fact]
    public void ListBoxItemPeer_ExposesSelectionItemPattern()
    {
        var host = new Canvas();
        var listBox = new ListBox();
        listBox.Items.Add("One");
        listBox.Items.Add("Two");
        host.AddChild(listBox);

        var uiRoot = new UiRoot(host);
        listBox.SelectedIndex = 0;
        var firstContainer = listBox.GetItemContainersForPresenter()[0];
        var peer = uiRoot.Automation.GetPeer(firstContainer);
        Assert.NotNull(peer);

        Assert.True(peer.TryGetPattern(AutomationPatternType.SelectionItem, out var selectionItem));
        var selectionItemProvider = Assert.IsAssignableFrom<ISelectionItemProvider>(selectionItem);
        Assert.True(selectionItemProvider.IsSelected);

        uiRoot.Shutdown();
    }
}
