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
}
