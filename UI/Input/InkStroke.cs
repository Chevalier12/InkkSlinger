using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

/// <summary>
/// Represents a single ink stroke as a sequence of points with associated drawing attributes.
/// Modeled after WPF <c>System.Windows.Ink.Stroke</c> at a practical parity level.
/// </summary>
public class InkStroke
{
    private readonly List<Vector2> _points;
    private LayoutRect _cachedBounds;
    private bool _boundsDirty;

    public InkStroke(IReadOnlyList<Vector2> points, InkDrawingAttributes? attributes = null)
    {
        if (points == null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        _points = new List<Vector2>(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            _points.Add(points[i]);
        }

        DrawingAttributes = attributes?.Clone() ?? new InkDrawingAttributes();
        _boundsDirty = true;
    }

    public InkDrawingAttributes DrawingAttributes { get; }

    public IReadOnlyList<Vector2> Points => _points;

    public int PointCount => _points.Count;

    /// <summary>
    /// Appends a single point to the stroke (used during live drawing).
    /// </summary>
    public void AddPoint(Vector2 point)
    {
        _points.Add(point);
        _boundsDirty = true;
    }

    /// <summary>
    /// Returns the axis-aligned bounding box of the stroke.
    /// The result is cached and invalidated when points change.
    /// </summary>
    public LayoutRect GetBounds()
    {
        if (!_boundsDirty)
        {
            return _cachedBounds;
        }

        if (_points.Count == 0)
        {
            _cachedBounds = default;
            _boundsDirty = false;
            return _cachedBounds;
        }

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        float halfWidth = DrawingAttributes.Width * 0.5f;
        float halfHeight = DrawingAttributes.Height * 0.5f;

        for (int i = 0; i < _points.Count; i++)
        {
            var p = _points[i];
            if (p.X - halfWidth < minX) minX = p.X - halfWidth;
            if (p.Y - halfHeight < minY) minY = p.Y - halfHeight;
            if (p.X + halfWidth > maxX) maxX = p.X + halfWidth;
            if (p.Y + halfHeight > maxY) maxY = p.Y + halfHeight;
        }

        _cachedBounds = new LayoutRect(minX, minY, maxX - minX, maxY - minY);
        _boundsDirty = false;
        return _cachedBounds;
    }
}
