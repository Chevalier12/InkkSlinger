using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public static class VisualTreeHelper
{
    public static UIElement? HitTest(UIElement root, Vector2 position)
    {
        if (!root.HitTest(position))
        {
            return null;
        }

        var children = new List<UIElement>();
        foreach (var child in root.GetVisualChildren())
        {
            children.Add(child);
        }

        children.Sort(static (a, b) => Panel.GetZIndex(b).CompareTo(Panel.GetZIndex(a)));

        foreach (var child in children)
        {
            var hit = HitTest(child, position);
            if (hit != null)
            {
                return hit;
            }
        }

        return root;
    }
}
