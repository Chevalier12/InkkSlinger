using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class UIElementInvalidationDiagnosticsTests
{
    [Fact]
    public void ChildMeasurePropertyChange_TracksDirectAndPropagatedOrigins()
    {
        var root = new Panel();
        var parent = new StackPanel
        {
            Name = "ParentStack"
        };
        var child = new TextBlock
        {
            Name = "StageMetricsText",
            Text = "alpha",
            TextWrapping = TextWrapping.Wrap
        };

        parent.AddChild(child);
        root.AddChild(parent);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 640, 480, 16);

        var beforeChild = child.InvalidationDiagnosticsForTests;
        var beforeParent = parent.InvalidationDiagnosticsForTests;

        child.Text = "alpha beta gamma delta epsilon zeta";

        var afterChild = child.InvalidationDiagnosticsForTests;
        var afterParent = parent.InvalidationDiagnosticsForTests;

        Assert.Equal(beforeChild.DirectMeasureInvalidationCount + 1, afterChild.DirectMeasureInvalidationCount);
        Assert.Equal(beforeChild.PropagatedMeasureInvalidationCount, afterChild.PropagatedMeasureInvalidationCount);
        Assert.Contains("property:Text@TextBlock#StageMetricsText", afterChild.LastMeasureInvalidationSummary, StringComparison.Ordinal);
        Assert.Contains("property:Text@TextBlock#StageMetricsText", afterChild.TopMeasureInvalidationSources, StringComparison.Ordinal);

        Assert.Equal(beforeParent.DirectMeasureInvalidationCount, afterParent.DirectMeasureInvalidationCount);
        Assert.Equal(beforeParent.PropagatedMeasureInvalidationCount + 1, afterParent.PropagatedMeasureInvalidationCount);
        Assert.Contains("property:Text@TextBlock#StageMetricsText", afterParent.LastMeasureInvalidationSummary, StringComparison.Ordinal);
        Assert.Contains("property:Text@TextBlock#StageMetricsText", afterParent.TopMeasureInvalidationSources, StringComparison.Ordinal);
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }
}