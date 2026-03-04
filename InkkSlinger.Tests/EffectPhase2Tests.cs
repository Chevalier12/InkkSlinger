using System;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class EffectPhase2Tests
{
    [Fact]
    public void LoadFromString_BorderEffect_DropShadowEffectLoads()
    {
        const string xaml = """
                            <Panel>
                              <Border>
                                <Border.Effect>
                                  <DropShadowEffect Color="#FF8C00" ShadowDepth="3" BlurRadius="12" Opacity="0.5" />
                                </Border.Effect>
                              </Border>
                            </Panel>
                            """;

        var root = (Panel)XamlLoader.LoadFromString(xaml);
        var border = Assert.IsType<Border>(Assert.Single(root.Children));
        var effect = Assert.IsType<DropShadowEffect>(border.Effect);
        Assert.Equal(3f, effect.ShadowDepth, 3);
        Assert.Equal(12f, effect.BlurRadius, 3);
        Assert.Equal(0.5f, effect.Opacity, 3);
    }

    [Fact]
    public void SettingEffect_InvalidatesRenderOnly()
    {
        var border = new Border();

        Assert.False(border.NeedsMeasure);
        Assert.False(border.NeedsArrange);
        Assert.False(border.NeedsRender);

        border.Effect = new DropShadowEffect
        {
            BlurRadius = 8f,
            Opacity = 0.4f
        };

        Assert.False(border.NeedsMeasure);
        Assert.False(border.NeedsArrange);
        Assert.True(border.NeedsRender);
    }

    [Fact]
    public void MutatingDropShadowEffect_InvalidatesRenderOnly()
    {
        var border = new Border
        {
            Effect = new DropShadowEffect
            {
                BlurRadius = 2f,
                Opacity = 0.2f
            }
        };

        border.ClearRenderInvalidationRecursive();
        Assert.False(border.NeedsMeasure);
        Assert.False(border.NeedsArrange);
        Assert.False(border.NeedsRender);

        var effect = Assert.IsType<DropShadowEffect>(border.Effect);
        effect.BlurRadius = 9f;

        Assert.False(border.NeedsMeasure);
        Assert.False(border.NeedsArrange);
        Assert.True(border.NeedsRender);
    }

    [Fact]
    public void FrozenDropShadowEffect_MutationThrows()
    {
        var effect = new DropShadowEffect
        {
            BlurRadius = 2f,
            Opacity = 0.2f
        };
        effect.Freeze();

        Assert.Throws<InvalidOperationException>(() => effect.BlurRadius = 9f);
    }
}
