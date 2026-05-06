using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace InkkSlinger.Tests;

internal static class RetainedRenderingAssert
{
    public static void AssertRetainedDrawOrderMatchesImmediateTraversal(UiRoot uiRoot, LayoutRect clip)
    {
        var retained = uiRoot.GetRetainedDrawOrderForClipForTests(clip);
        var root = uiRoot.GetRetainedVisualOrderForTests().FirstOrDefault();
        Assert.NotNull(root);

        var immediate = new List<UIElement>();
        AppendImmediateDrawOrder(root, clip, parentVisible: true, immediate);

        Assert.Equal(immediate, retained);
    }

    private static void AppendImmediateDrawOrder(
        UIElement visual,
        LayoutRect clip,
        bool parentVisible,
        List<UIElement> order)
    {
        var effectivelyVisible = parentVisible && visual.IsVisible;
        if (!effectivelyVisible)
        {
            return;
        }

        if (!visual.TryGetRenderBoundsInRootSpace(out var bounds) || Intersects(bounds, clip))
        {
            order.Add(visual);
        }

        foreach (var child in visual.GetRetainedRenderChildren())
        {
            AppendImmediateDrawOrder(child, clip, effectivelyVisible, order);
        }
    }

    private static bool Intersects(LayoutRect left, LayoutRect right)
    {
        return left.X < right.X + right.Width &&
               left.X + left.Width > right.X &&
               left.Y < right.Y + right.Height &&
               left.Y + left.Height > right.Y;
    }
}
