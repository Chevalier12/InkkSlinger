using Xunit;

namespace InkkSlinger.Tests;

public sealed class StyleFreezableClonePolicyTests
{
    [Fact]
    public void StyleSetter_WithFrozenTransform_SharesInstanceAcrossTargets()
    {
        var shared = new ScaleTransform { ScaleX = 1f, ScaleY = 1f };
        shared.Freeze();

        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(UIElement.RenderTransformProperty, shared));

        var first = new Button { Style = style };
        var second = new Button { Style = style };

        Assert.Same(shared, first.RenderTransform);
        Assert.Same(shared, second.RenderTransform);
        Assert.Same(first.RenderTransform, second.RenderTransform);
    }

    [Fact]
    public void StyleSetter_WithUnfrozenTransform_ClonesPerTarget()
    {
        var shared = new ScaleTransform { ScaleX = 1f, ScaleY = 1f };

        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(UIElement.RenderTransformProperty, shared));

        var first = new Button { Style = style };
        var second = new Button { Style = style };

        Assert.NotSame(shared, first.RenderTransform);
        Assert.NotSame(shared, second.RenderTransform);
        Assert.NotSame(first.RenderTransform, second.RenderTransform);
    }

    [Fact]
    public void StyleSetter_WithFrozenEffect_SharesInstanceAcrossTargets()
    {
        var sharedEffect = new DropShadowEffect
        {
            BlurRadius = 4f,
            Opacity = 0.3f,
            ShadowDepth = 2f
        };
        sharedEffect.Freeze();

        var style = new Style(typeof(Border));
        style.Setters.Add(new Setter(UIElement.EffectProperty, sharedEffect));

        var first = new Border { Style = style };
        var second = new Border { Style = style };

        Assert.Same(sharedEffect, first.Effect);
        Assert.Same(sharedEffect, second.Effect);
        Assert.Same(first.Effect, second.Effect);
    }

    [Fact]
    public void StyleSetter_WithUnfrozenEffect_ClonesPerTarget()
    {
        var sharedEffect = new DropShadowEffect
        {
            BlurRadius = 4f,
            Opacity = 0.3f,
            ShadowDepth = 2f
        };

        var style = new Style(typeof(Border));
        style.Setters.Add(new Setter(UIElement.EffectProperty, sharedEffect));

        var first = new Border { Style = style };
        var second = new Border { Style = style };

        Assert.NotSame(sharedEffect, first.Effect);
        Assert.NotSame(sharedEffect, second.Effect);
        Assert.NotSame(first.Effect, second.Effect);
    }
}
