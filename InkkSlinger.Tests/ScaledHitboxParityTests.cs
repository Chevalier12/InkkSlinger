using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ScaledHitboxParityTests
{
    [Fact]
    public void HitTest_StackPanelScaledButton_UsesScaledVisualBounds()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 400f, 400f));

        var stack = new StackPanel();
        stack.SetLayoutSlot(new LayoutRect(0f, 0f, 220f, 300f));
        root.AddChild(stack);

        for (var i = 0; i < 24; i++)
        {
            var button = new Button
            {
                Text = $"Item {i}",
                Height = 40f,
                Margin = new Thickness(0f, 0f, 0f, 4f)
            };

            stack.AddChild(button);
        }

        var target = Assert.IsType<Button>(stack.Children[1]);
        target.RenderTransformOrigin = new Vector2(0.5f, 0.5f);
        target.RenderTransform = new ScaleTransform { ScaleX = 1.3f, ScaleY = 1.3f };

        // Emulate arranged slots as in vertical stack with spacing.
        var y = 0f;
        foreach (var child in stack.Children)
        {
            child.SetLayoutSlot(new LayoutRect(0f, y, 220f, 40f));
            y += 44f;
        }

        // Point is just below original target slot (ends at y=84), but still within scaled visual bounds.
        var probe = new Vector2(110f, 86f);
        var slot = target.LayoutSlot;
        Assert.True(probe.Y > slot.Y + slot.Height);

        var hit = VisualTreeHelper.HitTest(root, probe);
        Assert.True(IsTargetOrDescendant(hit, target));
    }

    private static bool IsTargetOrDescendant(UIElement? hit, UIElement target)
    {
        for (var current = hit; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (ReferenceEquals(current, target))
            {
                return true;
            }
        }

        return false;
    }
}
