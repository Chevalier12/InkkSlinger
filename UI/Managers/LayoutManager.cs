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
        _root.Measure(viewportSize);
        _root.Arrange(new LayoutRect(0f, 0f, viewportSize.X, viewportSize.Y));
        _root.UpdateLayout();
    }
}
