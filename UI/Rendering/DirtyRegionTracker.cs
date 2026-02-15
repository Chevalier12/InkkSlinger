using System;
using System.Collections.Generic;

namespace InkkSlinger;

internal sealed class DirtyRegionTracker
{
    private readonly List<LayoutRect> _regions = new();
    private readonly int _maxRegionCount;
    private LayoutRect _viewport;
    private bool _hasViewport;

    public DirtyRegionTracker(int maxRegionCount = 12)
    {
        _maxRegionCount = Math.Max(1, maxRegionCount);
    }

    public bool IsFullFrameDirty { get; private set; }

    public int FullRedrawFallbackCount { get; private set; }

    public int RegionCount => _regions.Count;

    public IReadOnlyList<LayoutRect> Regions => _regions;

    public bool SetViewport(LayoutRect viewport)
    {
        var normalized = Normalize(viewport);
        if (_hasViewport && AreEqual(_viewport, normalized))
        {
            return false;
        }

        _hasViewport = true;
        _viewport = normalized;
        for (var i = _regions.Count - 1; i >= 0; i--)
        {
            _regions[i] = ClipToViewport(_regions[i]);
            if (!IsValid(_regions[i]))
            {
                _regions.RemoveAt(i);
            }
        }

        return true;
    }

    public void MarkFullFrameDirty(bool dueToFragmentation)
    {
        IsFullFrameDirty = true;
        _regions.Clear();
        if (dueToFragmentation)
        {
            FullRedrawFallbackCount++;
        }
    }

    public void Clear()
    {
        IsFullFrameDirty = false;
        _regions.Clear();
    }

    public void AddDirtyRegion(LayoutRect region)
    {
        if (IsFullFrameDirty)
        {
            return;
        }

        var candidate = Normalize(region);
        if (_hasViewport)
        {
            candidate = ClipToViewport(candidate);
        }

        if (!IsValid(candidate))
        {
            return;
        }

        for (var i = _regions.Count - 1; i >= 0; i--)
        {
            if (!IntersectsOrTouches(_regions[i], candidate))
            {
                continue;
            }

            candidate = Union(_regions[i], candidate);
            _regions.RemoveAt(i);
        }

        _regions.Add(candidate);
        if (_regions.Count > _maxRegionCount)
        {
            MarkFullFrameDirty(dueToFragmentation: true);
        }
    }

    public double GetDirtyAreaCoverage()
    {
        if (IsFullFrameDirty)
        {
            return 1d;
        }

        if (!_hasViewport || _viewport.Width <= 0f || _viewport.Height <= 0f)
        {
            return 0d;
        }

        var viewportArea = _viewport.Width * _viewport.Height;
        if (viewportArea <= 0f)
        {
            return 0d;
        }

        double dirtyArea = 0d;
        for (var i = 0; i < _regions.Count; i++)
        {
            var region = _regions[i];
            dirtyArea += region.Width * region.Height;
        }

        return Math.Clamp(dirtyArea / viewportArea, 0d, 1d);
    }

    private LayoutRect ClipToViewport(LayoutRect region)
    {
        var left = MathF.Max(_viewport.X, region.X);
        var top = MathF.Max(_viewport.Y, region.Y);
        var right = MathF.Min(_viewport.X + _viewport.Width, region.X + region.Width);
        var bottom = MathF.Min(_viewport.Y + _viewport.Height, region.Y + region.Height);
        return new LayoutRect(left, top, MathF.Max(0f, right - left), MathF.Max(0f, bottom - top));
    }

    private static LayoutRect Normalize(LayoutRect region)
    {
        var x = region.X;
        var y = region.Y;
        var width = region.Width;
        var height = region.Height;

        if (width < 0f)
        {
            x += width;
            width = -width;
        }

        if (height < 0f)
        {
            y += height;
            height = -height;
        }

        return new LayoutRect(x, y, width, height);
    }

    private static bool IsValid(LayoutRect region)
    {
        return region.Width > 0f && region.Height > 0f;
    }

    private static bool IntersectsOrTouches(LayoutRect left, LayoutRect right)
    {
        var leftRight = left.X + left.Width;
        var rightRight = right.X + right.Width;
        var leftBottom = left.Y + left.Height;
        var rightBottom = right.Y + right.Height;

        return left.X <= rightRight &&
               leftRight >= right.X &&
               left.Y <= rightBottom &&
               leftBottom >= right.Y;
    }

    private static LayoutRect Union(LayoutRect left, LayoutRect right)
    {
        var x = MathF.Min(left.X, right.X);
        var y = MathF.Min(left.Y, right.Y);
        var rightEdge = MathF.Max(left.X + left.Width, right.X + right.Width);
        var bottomEdge = MathF.Max(left.Y + left.Height, right.Y + right.Height);
        return new LayoutRect(x, y, MathF.Max(0f, rightEdge - x), MathF.Max(0f, bottomEdge - y));
    }

    private static bool AreEqual(LayoutRect left, LayoutRect right)
    {
        const float epsilon = 0.0001f;
        return MathF.Abs(left.X - right.X) <= epsilon &&
               MathF.Abs(left.Y - right.Y) <= epsilon &&
               MathF.Abs(left.Width - right.Width) <= epsilon &&
               MathF.Abs(left.Height - right.Height) <= epsilon;
    }
}
