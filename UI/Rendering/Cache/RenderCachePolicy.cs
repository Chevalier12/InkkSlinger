using System;

namespace InkkSlinger;

internal interface IRenderCachePolicy
{
    bool CanCache(UIElement visual, in RenderCachePolicyContext context);

    bool ShouldRebuildCache(
        UIElement visual,
        in RenderCachePolicyContext context,
        in RenderCacheSnapshot currentSnapshot);

    bool TryGetCacheBounds(UIElement visual, in RenderCachePolicyContext context, out LayoutRect bounds);
}

internal readonly record struct RenderCachePolicyContext(
    bool IsEffectivelyVisible,
    bool HasBoundsSnapshot,
    LayoutRect BoundsSnapshot,
    bool HasTransformState,
    bool HasClipState,
    int RenderStateStepCount,
    int RenderStateSignature,
    int SubtreeVisualCount,
    int SubtreeHighCostVisualCount,
    int SubtreeRenderVersionStamp,
    int SubtreeLayoutVersionStamp);

internal readonly record struct RenderCacheSnapshot(
    LayoutRect Bounds,
    int RenderVersionStamp,
    int LayoutVersionStamp,
    int RenderStateSignature);

internal sealed class DefaultRenderCachePolicy : IRenderCachePolicy
{
    private const float MinStaticPanelArea = 160f * 160f;
    private const float MinTransformedSubtreeArea = 96f * 96f;
    private const float MinHighCostSubtreeArea = 110f * 110f;

    public bool CanCache(UIElement visual, in RenderCachePolicyContext context)
    {
        if (!context.IsEffectivelyVisible || !context.HasBoundsSnapshot)
        {
            return false;
        }

        var area = context.BoundsSnapshot.Width * context.BoundsSnapshot.Height;
        if (area <= 0f)
        {
            return false;
        }

        if (visual is TextBox textBox)
        {
            return textBox.IsRenderCacheStable;
        }

        if (visual is PasswordBox passwordBox)
        {
            return passwordBox.IsRenderCacheStable;
        }

        if (visual is RichTextBox richTextBox)
        {
            return richTextBox.IsRenderCacheStable;
        }

        if (visual is TextBlock)
        {
            return true;
        }

        if (context.HasTransformState)
        {
            return context.SubtreeVisualCount >= 2 && area >= MinTransformedSubtreeArea;
        }

        if (context.SubtreeHighCostVisualCount > 0 &&
            context.SubtreeVisualCount >= 2 &&
            area >= MinHighCostSubtreeArea)
        {
            return true;
        }

        if (visual is Panel panel)
        {
            if (panel.Children.Count != 0)
            {
                return false;
            }

            return area >= MinStaticPanelArea;
        }

        if (visual is Shape)
        {
            return area >= MinHighCostSubtreeArea;
        }

        return false;
    }

    public bool ShouldRebuildCache(
        UIElement visual,
        in RenderCachePolicyContext context,
        in RenderCacheSnapshot currentSnapshot)
    {
        if (!context.HasBoundsSnapshot)
        {
            return true;
        }

        if (!AreRectsEqual(currentSnapshot.Bounds, context.BoundsSnapshot))
        {
            return true;
        }

        if (currentSnapshot.RenderStateSignature != context.RenderStateSignature)
        {
            return true;
        }

        if (currentSnapshot.RenderVersionStamp != context.SubtreeRenderVersionStamp)
        {
            return true;
        }

        return currentSnapshot.LayoutVersionStamp != context.SubtreeLayoutVersionStamp;
    }

    public bool TryGetCacheBounds(UIElement visual, in RenderCachePolicyContext context, out LayoutRect bounds)
    {
        _ = visual;
        bounds = context.BoundsSnapshot;
        return context.HasBoundsSnapshot;
    }

    private static bool AreRectsEqual(LayoutRect left, LayoutRect right)
    {
        const float epsilon = 0.0001f;
        return MathF.Abs(left.X - right.X) <= epsilon &&
               MathF.Abs(left.Y - right.Y) <= epsilon &&
               MathF.Abs(left.Width - right.Width) <= epsilon &&
               MathF.Abs(left.Height - right.Height) <= epsilon;
    }
}
