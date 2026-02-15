using Xunit;

namespace InkkSlinger.Tests;

public class RenderCachePolicyTests
{
    [Fact]
    public void TextBox_IsCacheable_WhenStable()
    {
        var policy = new DefaultRenderCachePolicy();
        var textBox = new TextBox();
        var context = new RenderCachePolicyContext(
            IsEffectivelyVisible: true,
            HasBoundsSnapshot: true,
            BoundsSnapshot: new LayoutRect(0f, 0f, 300f, 80f),
            HasTransformState: false,
            HasClipState: true,
            RenderStateStepCount: 1,
            RenderStateSignature: 101,
            SubtreeVisualCount: 1,
            SubtreeHighCostVisualCount: 1,
            SubtreeRenderVersionStamp: textBox.RenderCacheRenderVersion,
            SubtreeLayoutVersionStamp: textBox.RenderCacheLayoutVersion);

        Assert.True(policy.CanCache(textBox, context));
    }

    [Fact]
    public void TextBox_IsNotCacheable_WhenFocused()
    {
        var policy = new DefaultRenderCachePolicy();
        var textBox = new TextBox();
        textBox.SetValue(TextBox.IsFocusedProperty, true);
        var context = new RenderCachePolicyContext(
            IsEffectivelyVisible: true,
            HasBoundsSnapshot: true,
            BoundsSnapshot: new LayoutRect(0f, 0f, 300f, 80f),
            HasTransformState: false,
            HasClipState: true,
            RenderStateStepCount: 1,
            RenderStateSignature: 101,
            SubtreeVisualCount: 1,
            SubtreeHighCostVisualCount: 1,
            SubtreeRenderVersionStamp: textBox.RenderCacheRenderVersion,
            SubtreeLayoutVersionStamp: textBox.RenderCacheLayoutVersion);

        Assert.False(policy.CanCache(textBox, context));
    }

    [Fact]
    public void ShouldRebuildCache_WhenVersionsChanged()
    {
        var policy = new DefaultRenderCachePolicy();
        var textBox = new TextBox();
        var context = new RenderCachePolicyContext(
            IsEffectivelyVisible: true,
            HasBoundsSnapshot: true,
            BoundsSnapshot: new LayoutRect(0f, 0f, 300f, 80f),
            HasTransformState: false,
            HasClipState: true,
            RenderStateStepCount: 1,
            RenderStateSignature: 101,
            SubtreeVisualCount: 1,
            SubtreeHighCostVisualCount: 1,
            SubtreeRenderVersionStamp: textBox.RenderCacheRenderVersion,
            SubtreeLayoutVersionStamp: textBox.RenderCacheLayoutVersion);

        var currentSnapshot = new RenderCacheSnapshot(
            Bounds: context.BoundsSnapshot,
            RenderVersionStamp: context.SubtreeRenderVersionStamp,
            LayoutVersionStamp: context.SubtreeLayoutVersionStamp,
            RenderStateSignature: context.RenderStateSignature);
        Assert.False(policy.ShouldRebuildCache(textBox, context, currentSnapshot));

        textBox.InvalidateVisual();
        var updatedContext = context with { SubtreeRenderVersionStamp = textBox.RenderCacheRenderVersion };
        Assert.True(policy.ShouldRebuildCache(textBox, updatedContext, currentSnapshot));
    }

    [Fact]
    public void LargeStaticPanel_IsCacheable_ButPanelWithChildrenIsNot()
    {
        var policy = new DefaultRenderCachePolicy();
        var staticPanel = new Panel();
        var largeContext = new RenderCachePolicyContext(
            IsEffectivelyVisible: true,
            HasBoundsSnapshot: true,
            BoundsSnapshot: new LayoutRect(0f, 0f, 400f, 400f),
            HasTransformState: false,
            HasClipState: false,
            RenderStateStepCount: 0,
            RenderStateSignature: 5,
            SubtreeVisualCount: 1,
            SubtreeHighCostVisualCount: 0,
            SubtreeRenderVersionStamp: staticPanel.RenderCacheRenderVersion,
            SubtreeLayoutVersionStamp: staticPanel.RenderCacheLayoutVersion);

        Assert.True(policy.CanCache(staticPanel, largeContext));

        staticPanel.AddChild(new Border());
        Assert.False(policy.CanCache(staticPanel, largeContext));
    }

    [Fact]
    public void TransformedStaticSubtree_IsCacheable()
    {
        var policy = new DefaultRenderCachePolicy();
        var container = new Panel();
        var context = new RenderCachePolicyContext(
            IsEffectivelyVisible: true,
            HasBoundsSnapshot: true,
            BoundsSnapshot: new LayoutRect(0f, 0f, 240f, 140f),
            HasTransformState: true,
            HasClipState: false,
            RenderStateStepCount: 2,
            RenderStateSignature: 42,
            SubtreeVisualCount: 3,
            SubtreeHighCostVisualCount: 0,
            SubtreeRenderVersionStamp: container.RenderCacheRenderVersion,
            SubtreeLayoutVersionStamp: container.RenderCacheLayoutVersion);

        Assert.True(policy.CanCache(container, context));
    }

    [Fact]
    public void HighCostTextSubtree_IsCacheable_WithoutTransform()
    {
        var policy = new DefaultRenderCachePolicy();
        var panel = new Panel();
        var context = new RenderCachePolicyContext(
            IsEffectivelyVisible: true,
            HasBoundsSnapshot: true,
            BoundsSnapshot: new LayoutRect(0f, 0f, 220f, 140f),
            HasTransformState: false,
            HasClipState: true,
            RenderStateStepCount: 1,
            RenderStateSignature: 64,
            SubtreeVisualCount: 4,
            SubtreeHighCostVisualCount: 2,
            SubtreeRenderVersionStamp: panel.RenderCacheRenderVersion,
            SubtreeLayoutVersionStamp: panel.RenderCacheLayoutVersion);

        Assert.True(policy.CanCache(panel, context));
    }
}
