using System;
using System.Collections.Generic;

namespace InkkSlinger;

internal sealed class DirtyRegionTracker
{
    private readonly List<LayoutRect> _regions = new();
    private readonly int _maxRegionCount;
    private LayoutRect _viewport;
    private bool _hasViewport;
    private LayoutRect _dirtyBoundsEnvelope;
    private bool _hasDirtyBoundsEnvelope;
    private double _dirtyArea;
    private double _viewportArea;

    public DirtyRegionTracker(int maxRegionCount = 12)
    {
        _maxRegionCount = Math.Max(1, maxRegionCount);
    }

    public bool IsFullFrameDirty { get; private set; }

    public int FullRedrawFallbackCount { get; private set; }

    public int RegionCount => _regions.Count;

    public IReadOnlyList<LayoutRect> Regions => _regions;

    public bool TryGetDirtyBoundsEnvelope(out LayoutRect boundsEnvelope)
    {
        boundsEnvelope = _dirtyBoundsEnvelope;
        return _hasDirtyBoundsEnvelope;
    }

    public bool SetViewport(LayoutRect viewport)
    {
        var normalized = Normalize(viewport);
        if (_hasViewport && AreEqual(_viewport, normalized))
        {
            return false;
        }

        _hasViewport = true;
        _viewport = normalized;
        _viewportArea = Math.Max(0d, normalized.Width * normalized.Height);
        if (IsFullFrameDirty || _regions.Count == 0)
        {
            if (!IsFullFrameDirty)
            {
                ResetTrackedRegionState();
            }

            return true;
        }

        var existingRegions = _regions.ToArray();
        _regions.Clear();
        ResetTrackedRegionState();
        for (var i = 0; i < existingRegions.Length; i++)
        {
            var clippedRegion = ClipToViewport(existingRegions[i]);
            if (!IsValid(clippedRegion))
            {
                continue;
            }

            AddDirtyRegionCore(clippedRegion);
        }

        return true;
    }

    public void MarkFullFrameDirty(bool dueToFragmentation)
    {
        IsFullFrameDirty = true;
        _regions.Clear();
        ResetTrackedRegionState();
        if (dueToFragmentation)
        {
            FullRedrawFallbackCount++;
        }
    }

    public void Clear()
    {
        IsFullFrameDirty = false;
        _regions.Clear();
        ResetTrackedRegionState();
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

        AddDirtyRegionCore(candidate);
    }

    public double GetDirtyAreaCoverage()
    {
        if (IsFullFrameDirty)
        {
            return 1d;
        }

        if (!_hasViewport || _viewportArea <= 0d)
        {
            return 0d;
        }

        return Math.Clamp(_dirtyArea / _viewportArea, 0d, 1d);
    }

    private void AddDirtyRegionCore(LayoutRect candidate)
    {
        for (var i = _regions.Count - 1; i >= 0; i--)
        {
            var existingRegion = _regions[i];
            if (Contains(existingRegion, candidate))
            {
                return;
            }

            if (!IntersectsOrTouches(existingRegion, candidate))
            {
                continue;
            }

            candidate = Union(existingRegion, candidate);
            _dirtyArea -= GetArea(existingRegion);
            _regions.RemoveAt(i);
        }

        _regions.Add(candidate);
        _dirtyArea += GetArea(candidate);
        if (_hasDirtyBoundsEnvelope)
        {
            _dirtyBoundsEnvelope = Union(_dirtyBoundsEnvelope, candidate);
        }
        else
        {
            _dirtyBoundsEnvelope = candidate;
            _hasDirtyBoundsEnvelope = true;
        }

        if (_regions.Count > _maxRegionCount)
        {
            MarkFullFrameDirty(dueToFragmentation: true);
        }
    }

    private void ResetTrackedRegionState()
    {
        _dirtyArea = 0d;
        _dirtyBoundsEnvelope = default;
        _hasDirtyBoundsEnvelope = false;
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

        var right = x + width;
        var bottom = y + height;
        var snappedX = MathF.Floor(x);
        var snappedY = MathF.Floor(y);
        var snappedRight = MathF.Ceiling(right);
        var snappedBottom = MathF.Ceiling(bottom);
        return new LayoutRect(
            snappedX,
            snappedY,
            MathF.Max(0f, snappedRight - snappedX),
            MathF.Max(0f, snappedBottom - snappedY));
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

    private static bool Contains(LayoutRect outer, LayoutRect inner)
    {
        return inner.X >= outer.X &&
               inner.Y >= outer.Y &&
               inner.X + inner.Width <= outer.X + outer.Width &&
               inner.Y + inner.Height <= outer.Y + outer.Height;
    }

    private static LayoutRect Union(LayoutRect left, LayoutRect right)
    {
        var x = MathF.Min(left.X, right.X);
        var y = MathF.Min(left.Y, right.Y);
        var rightEdge = MathF.Max(left.X + left.Width, right.X + right.Width);
        var bottomEdge = MathF.Max(left.Y + left.Height, right.Y + right.Height);
        return new LayoutRect(x, y, MathF.Max(0f, rightEdge - x), MathF.Max(0f, bottomEdge - y));
    }

    private static double GetArea(LayoutRect rect)
    {
        return rect.Width * rect.Height;
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
