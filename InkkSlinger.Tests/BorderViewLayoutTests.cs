using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class BorderViewLayoutTests
{
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
