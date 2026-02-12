using System;
using System.Collections.Generic;

namespace InkkSlinger;

internal sealed class DirtyRegionTracker
{
    private readonly int _maxRegionCount;
    private readonly List<LayoutRect> _regions = new();
    private LayoutRect _viewportBounds;
    private bool _hasViewport;
    private bool _isFullFrameDirty;
    private int _fullFrameFallbackCount;

    public DirtyRegionTracker(int maxRegionCount = 32)
    {
        _maxRegionCount = Math.Max(1, maxRegionCount);
    }

    public bool IsFullFrameDirty => _isFullFrameDirty;

    public int RegionCount => _regions.Count;

    public IReadOnlyList<LayoutRect> Regions => _regions;

    public int FullFrameFallbackCount => _fullFrameFallbackCount;

    public void SetViewport(LayoutRect viewportBounds)
    {
        _viewportBounds = viewportBounds;
        _hasViewport = true;
    }

    public void MarkFullFrameDirty()
    {
        _isFullFrameDirty = true;
        _regions.Clear();
    }

    public void AddDirtyRegion(LayoutRect region)
    {
        if (_isFullFrameDirty)
        {
            return;
        }

        if (TryNormalizeRegion(region, out var normalized) == false)
        {
            return;
        }

        for (var i = 0; i < _regions.Count; i++)
        {
            var existing = _regions[i];
            if (!IntersectsOrTouches(existing, normalized))
            {
                continue;
            }

            _regions[i] = Union(existing, normalized);
            CollapseFrom(i);
            return;
        }

        _regions.Add(normalized);
        if (_regions.Count > _maxRegionCount)
        {
            _fullFrameFallbackCount++;
            MarkFullFrameDirty();
        }
    }

    public void Clear()
    {
        _isFullFrameDirty = false;
        _regions.Clear();
    }

    internal static bool Intersects(LayoutRect left, LayoutRect right)
    {
        return left.X < right.X + right.Width &&
               left.X + left.Width > right.X &&
               left.Y < right.Y + right.Height &&
               left.Y + left.Height > right.Y;
    }

    internal static LayoutRect Union(LayoutRect left, LayoutRect right)
    {
        var x1 = MathF.Min(left.X, right.X);
        var y1 = MathF.Min(left.Y, right.Y);
        var x2 = MathF.Max(left.X + left.Width, right.X + right.Width);
        var y2 = MathF.Max(left.Y + left.Height, right.Y + right.Height);
        return new LayoutRect(x1, y1, MathF.Max(0f, x2 - x1), MathF.Max(0f, y2 - y1));
    }

    private bool TryNormalizeRegion(LayoutRect region, out LayoutRect normalized)
    {
        var width = MathF.Max(0f, region.Width);
        var height = MathF.Max(0f, region.Height);
        if (width <= 0f || height <= 0f)
        {
            normalized = default;
            return false;
        }

        var candidate = new LayoutRect(region.X, region.Y, width, height);
        if (!_hasViewport)
        {
            normalized = candidate;
            return true;
        }

        if (!TryIntersect(candidate, _viewportBounds, out normalized))
        {
            return false;
        }

        return true;
    }

    private void CollapseFrom(int index)
    {
        var current = _regions[index];
        for (var i = _regions.Count - 1; i > index; i--)
        {
            if (!IntersectsOrTouches(current, _regions[i]))
            {
                continue;
            }

            current = Union(current, _regions[i]);
            _regions[index] = current;
            _regions.RemoveAt(i);
        }
    }

    private static bool TryIntersect(LayoutRect left, LayoutRect right, out LayoutRect intersection)
    {
        var x1 = MathF.Max(left.X, right.X);
        var y1 = MathF.Max(left.Y, right.Y);
        var x2 = MathF.Min(left.X + left.Width, right.X + right.Width);
        var y2 = MathF.Min(left.Y + left.Height, right.Y + right.Height);

        if (x2 <= x1 || y2 <= y1)
        {
            intersection = default;
            return false;
        }

        intersection = new LayoutRect(x1, y1, x2 - x1, y2 - y1);
        return true;
    }

    private static bool IntersectsOrTouches(LayoutRect left, LayoutRect right)
    {
        var horizontalTouch = left.X <= right.X + right.Width &&
                              left.X + left.Width >= right.X;
        var verticalTouch = left.Y <= right.Y + right.Height &&
                            left.Y + left.Height >= right.Y;
        return horizontalTouch && verticalTouch;
    }
}
