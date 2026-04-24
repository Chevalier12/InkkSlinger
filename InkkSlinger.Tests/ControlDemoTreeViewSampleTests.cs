using System.Linq;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlDemoTreeViewSampleTests
{
    [Fact]
    public void TreeViewSample_ShouldIncludeNestedHierarchy()
    {
        var element = ControlDemoSupport.BuildSampleElement("TreeView");
        var treeView = Assert.IsType<TreeView>(element);

        var roots = treeView.GetItemContainersForPresenter().OfType<TreeViewItem>().ToArray();
        var root = Assert.Single(roots);
        Assert.Equal("Root", root.Header);
        Assert.True(root.IsExpanded);

        var levelOneChildren = root.GetChildTreeItems();
        Assert.Equal(2, levelOneChildren.Count);
        Assert.Equal("Documents", levelOneChildren[0].Header);
        Assert.True(levelOneChildren[0].IsExpanded);
        Assert.Equal("Media", levelOneChildren[1].Header);

        var levelTwoChildren = levelOneChildren[0].GetChildTreeItems();
        Assert.Equal(2, levelTwoChildren.Count);
        Assert.Equal("Invoices", levelTwoChildren[0].Header);
        Assert.Equal("Reports", levelTwoChildren[1].Header);
    }
}
