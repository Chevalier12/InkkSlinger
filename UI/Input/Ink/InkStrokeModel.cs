using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public sealed class InkStroke
{
    private List<Vector2> _points;
    private RectangleF? _cachedBounds;

    public InkStroke(IEnumerable<Vector2> points)
    {
        _points = new List<Vector2>(points);
        Color = Color.Black;
        Thickness = 2f;
        Opacity = 1f;
    }

    public IReadOnlyList<Vector2> Points => _points;

    public Color Color { get; set; }

    public float Thickness { get; set; }

    public float Opacity { get; set; }

    public RectangleF Bounds
    {
        get
        {
            if (_cachedBounds.HasValue)
            {
                return _cachedBounds.Value;
            }

            if (_points.Count == 0)
            {
                _cachedBounds = RectangleF.Empty;
                return _cachedBounds.Value;
            }

            var minX = float.MaxValue;
            var minY = float.MaxValue;
            var maxX = float.MinValue;
            var maxY = float.MinValue;

            foreach (var point in _points)
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }

            var halfThickness = Thickness / 2f;
            _cachedBounds = new RectangleF(
                minX - halfThickness,
                minY - halfThickness,
                (maxX - minX) + Thickness,
                (maxY - minY) + Thickness);
            return _cachedBounds.Value;
        }
    }

    public void AddPoint(Vector2 point)
    {
        _points.Add(point);
        _cachedBounds = null;
    }

    public void ClearPoints()
    {
        _points.Clear();
        _cachedBounds = null;
    }
}

public sealed class InkStrokeCollection
{
    private readonly List<InkStroke> _strokes = new();
    private event EventHandler? StrokesChanged;

    public int Count => _strokes.Count;

    public InkStroke this[int index] => _strokes[index];

    public IReadOnlyList<InkStroke> Strokes => _strokes;

    public event EventHandler? Changed
    {
        add => StrokesChanged += value;
        remove => StrokesChanged -= value;
    }

    public void Add(InkStroke stroke)
    {
        _strokes.Add(stroke);
        OnStrokesChanged();
    }

    public void Remove(InkStroke stroke)
    {
        if (_strokes.Remove(stroke))
        {
            OnStrokesChanged();
        }
    }

    public void Clear()
    {
        if (_strokes.Count == 0)
        {
            return;
        }

        _strokes.Clear();
        OnStrokesChanged();
    }

    public RectangleF GetBounds()
    {
        if (_strokes.Count == 0)
        {
            return RectangleF.Empty;
        }

        var minX = float.MaxValue;
        var minY = float.MaxValue;
        var maxX = float.MinValue;
        var maxY = float.MinValue;

        foreach (var stroke in _strokes)
        {
            var bounds = stroke.Bounds;
            minX = Math.Min(minX, bounds.X);
            minY = Math.Min(minY, bounds.Y);
            maxX = Math.Max(maxX, bounds.X + bounds.Width);
            maxY = Math.Max(maxY, bounds.Y + bounds.Height);
        }

        return new RectangleF(minX, minY, maxX - minX, maxY - minY);
    }

    private void OnStrokesChanged()
    {
        StrokesChanged?.Invoke(this, EventArgs.Empty);
    }
}
