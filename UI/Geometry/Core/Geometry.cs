using System;
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

public abstract class Geometry
{
    public Transform? Transform { get; set; }

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

    protected abstract void CollectFigures(List<GeometryFigure> figures, float tolerance);
}

public sealed class PathGeometry : Geometry
{
    public PathGeometry()
    {
    }

    public PathGeometry(string data)
    {
        SetData(data);
    }

    public List<GeometryFigure> Figures { get; } = new();

    public FillRule FillRule { get; set; } = FillRule.EvenOdd;

    public string? Data
    {
        get => _data;
        set => SetData(value);
    }

    private string? _data;

    public static PathGeometry Parse(string data)
    {
        return PathMarkupParser.Parse(data);
    }

    public void SetData(string? data)
    {
        _data = data;
        Figures.Clear();

        if (string.IsNullOrWhiteSpace(data))
        {
            return;
        }

        var parsed = Parse(data);
        Figures.AddRange(parsed.Figures);
        FillRule = parsed.FillRule;
        if (parsed.Transform != null)
        {
            Transform = parsed.Transform;
        }
    }

    protected override void CollectFigures(List<GeometryFigure> figures, float tolerance)
    {
        figures.AddRange(Figures);
    }
}

public sealed class GeometryGroup : Geometry
{
    public List<Geometry> Children { get; } = new();

    public FillRule FillRule { get; set; } = FillRule.EvenOdd;

    protected override void CollectFigures(List<GeometryFigure> figures, float tolerance)
    {
        foreach (var child in Children)
        {
            if (child == null)
            {
                continue;
            }

            figures.AddRange(child.GetFlattenedFigures(tolerance));
        }
    }
}

public sealed class CombinedGeometry : Geometry
{
    public GeometryCombineMode GeometryCombineMode { get; set; } = GeometryCombineMode.Union;

    public Geometry? Geometry1 { get; set; }

    public Geometry? Geometry2 { get; set; }

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
