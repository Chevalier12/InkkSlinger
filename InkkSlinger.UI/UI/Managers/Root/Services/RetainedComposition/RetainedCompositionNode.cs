using Microsoft.Xna.Framework;

namespace InkkSlinger;

internal readonly record struct RetainedCompositionNode(
    UIElement Visual,
    int ParentIndex,
    int FirstChildIndex,
    int ChildCount,
    int SubtreeStartIndex,
    int SubtreeEndIndexExclusive,
    int Depth,
    int DrawOrderIndex,
    bool HasBounds,
    LayoutRect Bounds,
    bool HasSubtreeBounds,
    LayoutRect SubtreeBounds,
    bool HasLocalTransform,
    Matrix LocalTransform,
    bool HasLocalClip,
    LayoutRect LocalClip,
    float Opacity,
    bool IsEffectivelyVisible,
    int ContentVersion,
    int MetadataVersion,
    RetainedCompositionCacheMode CacheMode,
    RetainedCompositionCacheKey CacheKey);
