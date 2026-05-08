namespace InkkSlinger;

internal enum RetainedCompositionCacheMode
{
    None,
    TransformStableLayer,
    Bitmap
}

internal readonly record struct RetainedCompositionCacheKey(
    int SubtreeContentVersion,
    int StructureVersion,
    bool HasBounds,
    LayoutRect Bounds,
    int DeviceWidth,
    int DeviceHeight);
