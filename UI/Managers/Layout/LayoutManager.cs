using Microsoft.Xna.Framework;

namespace InkkSlinger;

public sealed class LayoutManager
{
    private readonly FrameworkElement _root;

    public LayoutManager(FrameworkElement root)
    {
        _root = root;
    }

    public void UpdateLayout(Vector2 viewportSize)
    {
        const int maxPasses = 8;
        for (var pass = 0; pass < maxPasses; pass++)
        {
            _root.Measure(viewportSize);
            _root.Arrange(new LayoutRect(0f, 0f, viewportSize.X, viewportSize.Y));
            _root.UpdateLayout();
            if (!HasInvalidLayout(_root))
            {
                return;
            }
        }
    }

    private static bool HasInvalidLayout(FrameworkElement element)
    {
        if (element.NeedsMeasure || element.NeedsArrange)
        {
            return true;
        }

        foreach (var child in element.GetVisualChildren())
        {
            if (child is FrameworkElement frameworkChild && HasInvalidLayout(frameworkChild))
            {
                return true;
            }
        }

        return false;
    }
}
