using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public enum FillRule
{
    EvenOdd,
    Nonzero
}

public enum GeometryCombineMode
{
    Union,
    Intersect,
    Exclude,
    Xor
}

public sealed class GeometryFigure
{
    public GeometryFigure(IReadOnlyList<Vector2> points, bool isClosed)
    {
        Points = points;
        IsClosed = isClosed;
    }

    public IReadOnlyList<Vector2> Points { get; }

    public bool IsClosed { get; }
}

public abstract class Geometry : Freezable
{
    private Transform? _transform;

    public new Geometry Clone()
    {
        return (Geometry)base.Clone();
    }

    public new Geometry CloneCurrentValue()
    {
        return (Geometry)base.CloneCurrentValue();
    }

    public Transform? Transform
    {
        get => _transform;
        set
        {
            WritePreamble();
            if (ReferenceEquals(_transform, value))
            {
                return;
            }

            _transform = value;
            WritePostscript();
        }
    }

    public IReadOnlyList<GeometryFigure> GetFlattenedFigures(float tolerance = 1f)
    {
        var figures = new List<GeometryFigure>();
        CollectFigures(figures, MathF.Max(0.1f, tolerance));
        if (Transform == null)
        {
            return figures;
        }

        var matrix = Transform.ToMatrix();
        var transformed = new List<GeometryFigure>(figures.Count);
        foreach (var figure in figures)
        {
            var points = new Vector2[figure.Points.Count];
            for (var i = 0; i < figure.Points.Count; i++)
            {
                points[i] = Vector2.Transform(figure.Points[i], matrix);
            }

            transformed.Add(new GeometryFigure(points, figure.IsClosed));
        }

        return transformed;
    }

    protected override void CloneCore(Freezable source)
    {
        var typedSource = (Geometry)source;
        _transform = typedSource._transform?.Clone();
    }

    protected override bool FreezeCore(bool isChecking)
    {
        return FreezeValue(_transform, isChecking);
    }

    protected abstract void CollectFigures(List<GeometryFigure> figures, float tolerance);
}

public sealed class PathGeometry : Geometry
{
    private string? _data;
    private FillRule _fillRule = FillRule.EvenOdd;

    public PathGeometry()
    {
        Figures.SetOwner(this);
    }

    public PathGeometry(string data)
    {
        Figures.SetOwner(this);
        SetData(data);
    }

    public GeometryFigureCollection Figures { get; } = new();

    public FillRule FillRule
    {
        get => _fillRule;
        set
        {
            WritePreamble();
            if (_fillRule == value)
            {
                return;
            }

            _fillRule = value;
            WritePostscript();
        }
    }

    public string? Data
    {
        get => _data;
        set => SetData(value);
    }

    public static PathGeometry Parse(string data)
    {
        return PathMarkupParser.Parse(data);
    }

    public void SetData(string? data)
    {
        WritePreamble();
        _data = data;
        Figures.Clear();

        if (string.IsNullOrWhiteSpace(data))
        {
            WritePostscript();
            return;
        }

        var parsed = Parse(data);
        Figures.AddRange(parsed.Figures);
        _fillRule = parsed._fillRule;
        if (parsed.Transform != null)
        {
            Transform = parsed.Transform;
        }

        WritePostscript();
    }

    protected override Freezable CreateInstanceCore()
    {
        return new PathGeometry();
    }

    protected override void CloneCore(Freezable source)
    {
        base.CloneCore(source);
        var typedSource = (PathGeometry)source;
        _data = typedSource._data;
        _fillRule = typedSource._fillRule;
        Figures.Clear();
        for (var i = 0; i < typedSource.Figures.Count; i++)
        {
            var figure = typedSource.Figures[i];
            var points = new Vector2[figure.Points.Count];
            for (var pointIndex = 0; pointIndex < figure.Points.Count; pointIndex++)
            {
                points[pointIndex] = figure.Points[pointIndex];
            }

            Figures.Add(new GeometryFigure(points, figure.IsClosed));
        }
    }

    protected override bool FreezeCore(bool isChecking)
    {
        return base.FreezeCore(isChecking);
    }

    protected override void CollectFigures(List<GeometryFigure> figures, float tolerance)
    {
        figures.AddRange(Figures);
    }

    public sealed class GeometryFigureCollection : IList<GeometryFigure>
    {
        private readonly List<GeometryFigure> _items = new();
        private PathGeometry? _owner;

        internal void SetOwner(PathGeometry owner)
        {
            _owner = owner;
        }

        public GeometryFigure this[int index]
        {
            get => _items[index];
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                _owner?.WritePreamble();
                _items[index] = value;
                _owner?.WritePostscript();
            }
        }

        public int Count => _items.Count;

        public bool IsReadOnly => false;

        public void Add(GeometryFigure item)
        {
            ArgumentNullException.ThrowIfNull(item);
            _owner?.WritePreamble();
            _items.Add(item);
            _owner?.WritePostscript();
        }

        public void AddRange(IEnumerable<GeometryFigure> items)
        {
            _owner?.WritePreamble();
            _items.AddRange(items);
            _owner?.WritePostscript();
        }

        public void Clear()
        {
            _owner?.WritePreamble();
            if (_items.Count == 0)
            {
                return;
            }

            _items.Clear();
            _owner?.WritePostscript();
        }

        public bool Contains(GeometryFigure item)
        {
            return _items.Contains(item);
        }

        public void CopyTo(GeometryFigure[] array, int arrayIndex)
        {
            _items.CopyTo(array, arrayIndex);
        }

        public IEnumerator<GeometryFigure> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        public int IndexOf(GeometryFigure item)
        {
            return _items.IndexOf(item);
        }

        public void Insert(int index, GeometryFigure item)
        {
            ArgumentNullException.ThrowIfNull(item);
            _owner?.WritePreamble();
            _items.Insert(index, item);
            _owner?.WritePostscript();
        }

        public bool Remove(GeometryFigure item)
        {
            _owner?.WritePreamble();
            var removed = _items.Remove(item);
            if (removed)
            {
                _owner?.WritePostscript();
            }

            return removed;
        }

        public void RemoveAt(int index)
        {
            _owner?.WritePreamble();
            _items.RemoveAt(index);
            _owner?.WritePostscript();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _items.GetEnumerator();
        }
    }
}

public sealed class GeometryGroup : Geometry
{
    private FillRule _fillRule = FillRule.EvenOdd;

    public GeometryGroup()
    {
        Children = new GeometryCollection(this);
    }

    public GeometryCollection Children { get; }

    public FillRule FillRule
    {
        get => _fillRule;
        set
        {
            WritePreamble();
            if (_fillRule == value)
            {
                return;
            }

            _fillRule = value;
            WritePostscript();
        }
    }

    protected override Freezable CreateInstanceCore()
    {
        return new GeometryGroup();
    }

    protected override void CloneCore(Freezable source)
    {
        base.CloneCore(source);
        var typedSource = (GeometryGroup)source;
        _fillRule = typedSource._fillRule;
        Children.Clear();
        foreach (var child in typedSource.Children)
        {
            Children.Add(child.Clone());
        }
    }

    protected override bool FreezeCore(bool isChecking)
    {
        if (!base.FreezeCore(isChecking))
        {
            return false;
        }

        foreach (var child in Children)
        {
            if (!FreezeValue(child, isChecking))
            {
                return false;
            }
        }

        return true;
    }

    protected override void CollectFigures(List<GeometryFigure> figures, float tolerance)
    {
        foreach (var child in Children)
        {
            figures.AddRange(child.GetFlattenedFigures(tolerance));
        }
    }

    public sealed class GeometryCollection : IList<Geometry>
    {
        private readonly GeometryGroup _owner;
        private readonly List<Geometry> _items = new();

        public GeometryCollection(GeometryGroup owner)
        {
            _owner = owner;
        }

        public Geometry this[int index]
        {
            get => _items[index];
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                _owner.WritePreamble();
                _items[index] = value;
                _owner.WritePostscript();
            }
        }

        public int Count => _items.Count;

        public bool IsReadOnly => false;

        public void Add(Geometry item)
        {
            ArgumentNullException.ThrowIfNull(item);
            _owner.WritePreamble();
            _items.Add(item);
            _owner.WritePostscript();
        }

        public void Clear()
        {
            _owner.WritePreamble();
            if (_items.Count == 0)
            {
                return;
            }

            _items.Clear();
            _owner.WritePostscript();
        }

        public bool Contains(Geometry item)
        {
            return _items.Contains(item);
        }

        public void CopyTo(Geometry[] array, int arrayIndex)
        {
            _items.CopyTo(array, arrayIndex);
        }

        public IEnumerator<Geometry> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        public int IndexOf(Geometry item)
        {
            return _items.IndexOf(item);
        }

        public void Insert(int index, Geometry item)
        {
            ArgumentNullException.ThrowIfNull(item);
            _owner.WritePreamble();
            _items.Insert(index, item);
            _owner.WritePostscript();
        }

        public bool Remove(Geometry item)
        {
            _owner.WritePreamble();
            var removed = _items.Remove(item);
            if (removed)
            {
                _owner.WritePostscript();
            }

            return removed;
        }

        public void RemoveAt(int index)
        {
            _owner.WritePreamble();
            _items.RemoveAt(index);
            _owner.WritePostscript();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _items.GetEnumerator();
        }
    }
}

public sealed class CombinedGeometry : Geometry
{
    private GeometryCombineMode _geometryCombineMode = GeometryCombineMode.Union;
    private Geometry? _geometry1;
    private Geometry? _geometry2;

    public GeometryCombineMode GeometryCombineMode
    {
        get => _geometryCombineMode;
        set
        {
            WritePreamble();
            if (_geometryCombineMode == value)
            {
                return;
            }

            _geometryCombineMode = value;
            WritePostscript();
        }
    }

    public Geometry? Geometry1
    {
        get => _geometry1;
        set
        {
            WritePreamble();
            if (ReferenceEquals(_geometry1, value))
            {
                return;
            }

            _geometry1 = value;
            WritePostscript();
        }
    }

    public Geometry? Geometry2
    {
        get => _geometry2;
        set
        {
            WritePreamble();
            if (ReferenceEquals(_geometry2, value))
            {
                return;
            }

            _geometry2 = value;
            WritePostscript();
        }
    }

    protected override Freezable CreateInstanceCore()
    {
        return new CombinedGeometry();
    }

    protected override void CloneCore(Freezable source)
    {
        base.CloneCore(source);
        var typedSource = (CombinedGeometry)source;
        _geometryCombineMode = typedSource._geometryCombineMode;
        _geometry1 = typedSource._geometry1?.Clone();
        _geometry2 = typedSource._geometry2?.Clone();
    }

    protected override bool FreezeCore(bool isChecking)
    {
        if (!base.FreezeCore(isChecking))
        {
            return false;
        }

        return FreezeValue(_geometry1, isChecking) &&
               FreezeValue(_geometry2, isChecking);
    }

    protected override void CollectFigures(List<GeometryFigure> figures, float tolerance)
    {
        if (Geometry1 == null && Geometry2 == null)
        {
            return;
        }

        var first = Geometry1?.GetFlattenedFigures(tolerance) ?? Array.Empty<GeometryFigure>();
        var second = Geometry2?.GetFlattenedFigures(tolerance) ?? Array.Empty<GeometryFigure>();

        // The renderer is polygon/line based, so boolean region ops are approximated.
        switch (GeometryCombineMode)
        {
            case GeometryCombineMode.Union:
            case GeometryCombineMode.Xor:
                figures.AddRange(first);
                figures.AddRange(second);
                break;
            case GeometryCombineMode.Intersect:
                figures.AddRange(first.Count <= second.Count ? first : second);
                break;
            case GeometryCombineMode.Exclude:
                figures.AddRange(first);
                break;
        }
    }
}

internal static class GeometryParsers
{
    public static List<Vector2> ParsePointList(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<Vector2>();
        }

        var tokens = value
            .Split(new[] { ' ', ';', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        var points = new List<Vector2>(tokens.Length);
        foreach (var token in tokens)
        {
            points.Add(ParsePoint(token));
        }

        return points;
    }

    public static Vector2 ParsePoint(string token)
    {
        var parts = token.Split(',');
        if (parts.Length != 2)
        {
            throw new FormatException($"Point token '{token}' must use 'x,y'.");
        }

        return new Vector2(
            float.Parse(parts[0], CultureInfo.InvariantCulture),
            float.Parse(parts[1], CultureInfo.InvariantCulture));
    }
}
