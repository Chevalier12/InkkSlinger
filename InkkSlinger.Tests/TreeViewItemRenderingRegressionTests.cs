using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class TreeViewItemRenderingRegressionTests
{
    [Fact]
    public void HeaderText_ShouldContributeToDesiredWidth_WhenFontIsNotExplicitlySet()
    {
        var item = new TreeViewItem
        {
            Header = "Root Node"
        };

        item.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));

        Assert.True(
            item.DesiredSize.X > 20f,
            $"Expected header text width to contribute to desired width when Font is null. DesiredWidth={item.DesiredSize.X:0.###}");
    }

    [Fact]
    public void VirtualizedDisplaySnapshot_UsesSnapshotSelectionForRendering()
    {
        var item = new TreeViewItem
        {
            Header = "Selected container",
            IsSelected = true
        };

        item.ApplyVirtualizedDisplaySnapshot(
            "Different row",
            hasChildren: false,
            isExpanded: false,
            isSelected: false,
            depth: 0,
            rowIndex: 12);

        Assert.False(item.IsSelectedForRenderDiagnostics);

        item.ApplyVirtualizedDisplaySnapshot(
            "Selected row",
            hasChildren: false,
            isExpanded: false,
            isSelected: true,
            depth: 0,
            rowIndex: 13);

        Assert.True(item.IsSelectedForRenderDiagnostics);

        item.ClearVirtualizedDisplaySnapshot();

        Assert.True(item.IsSelectedForRenderDiagnostics);
    }
}
