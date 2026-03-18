using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class BorderViewLayoutTests
{
    [Theory]
    [InlineData(900, 520)]
    [InlineData(1200, 520)]
    public void Footer_RemainsInsideProbeStage_AfterRealViewLayout(int width, int height)
    {
        var view = new BorderView();
        var uiRoot = new UiRoot(view);

        RunLayout(uiRoot, width, height);

        var footer = Assert.IsType<TextBlock>(FindDescendant(
            view,
            element => element is TextBlock textBlock &&
                       textBlock.Text == "The badge is intentionally translated beyond the frame. ClipToBounds crops it to the Border layout slot, and that clip is rectangular rather than rounded."));
        var stage = Assert.IsType<Grid>(footer.VisualParent);

        var stageBottom = stage.LayoutSlot.Y + stage.LayoutSlot.Height;
        var footerBottom = footer.LayoutSlot.Y + footer.LayoutSlot.Height;

        Assert.True(footerBottom <= stageBottom + 0.01f);
        Assert.Equal(footer.DesiredSize.Y, footer.ActualHeight, 0.01f);
        Assert.True(stage.ActualHeight >= 210f - 0.01f);
    }

    private static UIElement? FindDescendant(UIElement root, Predicate<UIElement> match)
    {
        if (match(root))
        {
            return root;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindDescendant(child, match);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs = 16)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }
}