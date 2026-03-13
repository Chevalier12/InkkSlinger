using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
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

    [Fact]
    public void DropShadowEffect_ExpandsRenderBoundsInRootSpace()
    {
        var border = new Border
        {
            Width = 40f,
            Height = 20f,
            Effect = new DropShadowEffect
            {
                Color = Color.Orange,
                ShadowDepth = 3f,
                BlurRadius = 12f,
                Opacity = 0.5f
            }
        };

        border.Measure(new Vector2(100f, 100f));
        border.Arrange(new LayoutRect(10f, 20f, 40f, 20f));

        Assert.True(border.TryGetRenderBoundsInRootSpace(out var bounds));
        Assert.Equal(-2f, bounds.X, 3);
        Assert.Equal(11f, bounds.Y, 3);
        Assert.Equal(64f, bounds.Width, 3);
        Assert.Equal(44f, bounds.Height, 3);
    }

    [Fact]
    public void DrawSelf_RendersEffectBeforeControlContent()
    {
        var effect = new ProbeEffect();
        var visual = new ProbeVisual
        {
            Effect = effect
        };

        visual.DrawSelf(null!);

        Assert.Equal(1, effect.RenderCallCount);
        Assert.Equal(1, visual.RenderCallCount);
        Assert.Equal("effect,content", string.Join(',', visual.RenderSteps));
    }

    private sealed class ProbeVisual : UIElement
    {
        public int RenderCallCount { get; private set; }

        public List<string> RenderSteps { get; } = new();

        protected override void OnRender(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch)
        {
            RenderCallCount++;
            RenderSteps.Add("content");
        }
    }

    private sealed class ProbeEffect : Effect
    {
        public int RenderCallCount { get; private set; }

        protected override Freezable CreateInstanceCore()
        {
            return new ProbeEffect();
        }

        protected override void CloneCore(Freezable source)
        {
        }

        internal override void Render(UIElement element, Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch, float elementOpacity)
        {
            RenderCallCount++;
            if (element is ProbeVisual probe)
            {
                probe.RenderSteps.Add("effect");
            }
        }

        internal override LayoutRect GetRenderBounds(UIElement element)
        {
            return element.LayoutSlot;
        }
    }
}
