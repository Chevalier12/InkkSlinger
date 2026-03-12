using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class UserControlLayoutTests
{
    [Fact]
    public void UserControl_WithoutTemplate_ArrangesContentOnceWithinChromeInset()
    {
        var content = new FixedSizeElement();
        var control = new UserControl
        {
            BorderThickness = new Thickness(3f, 5f, 7f, 11f),
            Padding = new Thickness(13f, 17f, 19f, 23f),
            Content = content
        };

        control.Measure(new Vector2(200f, 120f));
        control.Arrange(new LayoutRect(0f, 0f, 200f, 120f));

        Assert.Equal(1, content.ArrangeCallCount);
        Assert.Equal(new LayoutRect(16f, 22f, 158f, 64f), content.LayoutSlot);
    }

    private sealed class FixedSizeElement : FrameworkElement
    {
        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            return new Vector2(
                MathF.Min(40f, availableSize.X),
                MathF.Min(20f, availableSize.Y));
        }
    }
}
