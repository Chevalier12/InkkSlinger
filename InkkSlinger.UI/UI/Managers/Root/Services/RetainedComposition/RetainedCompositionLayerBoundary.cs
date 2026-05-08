namespace InkkSlinger;

internal static class RetainedCompositionLayerBoundary
{
    public static RetainedCompositionCacheMode ResolveCacheMode(UIElement visual)
    {
        return IsTransformStableLayer(visual)
            ? RetainedCompositionCacheMode.TransformStableLayer
            : visual.RetainedCompositionCacheMode;
    }

    public static bool IsTransformStableLayer(UIElement visual)
    {
        return TryGetTransformStableLayerViewport(visual, out _);
    }

    public static bool TryGetTransformStableLayerViewport(UIElement visual, out LayoutRect viewport)
    {
        if (IsTransformScrollContent(visual) &&
            TryGetDirectTransformScrollOwner(visual, out var owner) &&
            owner.TryGetContentViewportClipRect(out viewport))
        {
            return viewport.Width > 0f && viewport.Height > 0f;
        }

        viewport = default;
        return false;
    }

    private static bool IsTransformScrollContent(UIElement visual)
    {
        return visual is VirtualizingStackPanel ||
               visual is IScrollTransformContent && ScrollViewer.GetIsTransformContentLayerStable(visual);
    }

    private static bool TryGetDirectTransformScrollOwner(UIElement element, out ScrollViewer owner)
    {
        owner = null!;

        var visualOwner = element.VisualParent as ScrollViewer;
        if (visualOwner != null && ReferenceEquals(visualOwner.Content, element))
        {
            owner = visualOwner;
            return true;
        }

        var logicalOwner = element.LogicalParent as ScrollViewer;
        if (logicalOwner != null && ReferenceEquals(logicalOwner.Content, element))
        {
            owner = logicalOwner;
            return true;
        }

        return false;
    }
}
