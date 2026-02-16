using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public abstract class Shape : FrameworkElement
{
    public static readonly DependencyProperty FillProperty =
        DependencyProperty.Register(
            nameof(Fill),
            typeof(Color),
            typeof(Shape),
            new FrameworkPropertyMetadata(new Color(0, 0, 0, 0), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty =
        DependencyProperty.Register(
            nameof(Stroke),
            typeof(Color),
            typeof(Shape),
            new FrameworkPropertyMetadata(new Color(0, 0, 0, 0), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(
            nameof(StrokeThickness),
            typeof(float),
            typeof(Shape),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float f && f >= 0f ? f : 0f));

    public static readonly DependencyProperty StretchProperty =
        DependencyProperty.Register(
            nameof(Stretch),
            typeof(Stretch),
            typeof(Shape),
            new FrameworkPropertyMetadata(
                Stretch.Fill,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public Color Fill
    {
        get => GetValue<Color>(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public Color Stroke
    {
        get => GetValue<Color>(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public float StrokeThickness
    {
        get => GetValue<float>(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public Stretch Stretch
    {
        get => GetValue<Stretch>(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    protected abstract Geometry? DefiningGeometry { get; }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var geometry = DefiningGeometry;
        if (geometry == null)
        {
            return Vector2.Zero;
        }

        var bounds = GetBounds(geometry.GetFlattenedFigures());
        if (bounds.Width <= 0f && bounds.Height <= 0f)
        {
            return new Vector2(StrokeThickness, StrokeThickness);
        }

        return new Vector2(
            bounds.Width + StrokeThickness,
            bounds.Height + StrokeThickness);
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        base.OnRender(spriteBatch);
        RenderGeometry(spriteBatch, DefiningGeometry);
    }

    protected void RenderGeometry(SpriteBatch spriteBatch, Geometry? geometry)
    {
        if (geometry == null)
        {
            return;
        }

        var figures = geometry.GetFlattenedFigures();
        if (figures.Count == 0)
        {
            return;
        }

        var transformed = TransformFiguresToLayout(figures, LayoutSlot, Stretch);
        foreach (var figure in transformed)
        {
            if (figure.IsClosed && Fill.A > 0 && figure.Points.Count >= 3)
            {
                UiDrawing.DrawFilledPolygon(spriteBatch, figure.Points, Fill, Opacity);
            }

            if (Stroke.A > 0 && StrokeThickness > 0f && figure.Points.Count >= 2)
            {
                UiDrawing.DrawPolyline(spriteBatch, figure.Points, figure.IsClosed, StrokeThickness, Stroke, Opacity);
            }
        }
    }

    private static IReadOnlyList<GeometryFigure> TransformFiguresToLayout(
        IReadOnlyList<GeometryFigure> figures,
        LayoutRect slot,
        Stretch stretch)
    {
        var bounds = GetBounds(figures);
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return figures;
        }

        var scaleX = 1f;
        var scaleY = 1f;
        if (stretch != Stretch.None)
        {
            scaleX = slot.Width / bounds.Width;
            scaleY = slot.Height / bounds.Height;

            if (stretch == Stretch.Uniform)
            {
                var uniform = MathF.Min(scaleX, scaleY);
                scaleX = uniform;
                scaleY = uniform;
            }
            else if (stretch == Stretch.UniformToFill)
            {
                var uniform = MathF.Max(scaleX, scaleY);
                scaleX = uniform;
                scaleY = uniform;
            }
        }

        var transformed = new List<GeometryFigure>(figures.Count);
        var scaledWidth = bounds.Width * scaleX;
        var scaledHeight = bounds.Height * scaleY;
        var offsetX = slot.X + ((slot.Width - scaledWidth) / 2f);
        var offsetY = slot.Y + ((slot.Height - scaledHeight) / 2f);
        foreach (var figure in figures)
        {
            var points = new Vector2[figure.Points.Count];
            for (var i = 0; i < figure.Points.Count; i++)
            {
                var point = figure.Points[i];
                var normalizedX = point.X - bounds.X;
                var normalizedY = point.Y - bounds.Y;
                points[i] = new Vector2(
                    offsetX + (normalizedX * scaleX),
                    offsetY + (normalizedY * scaleY));
            }

            transformed.Add(new GeometryFigure(points, figure.IsClosed));
        }

        return transformed;
    }

    private static LayoutRect GetBounds(IReadOnlyList<GeometryFigure> figures)
    {
        var minX = float.PositiveInfinity;
        var minY = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var maxY = float.NegativeInfinity;
        foreach (var figure in figures)
        {
            foreach (var point in figure.Points)
            {
                minX = MathF.Min(minX, point.X);
                minY = MathF.Min(minY, point.Y);
                maxX = MathF.Max(maxX, point.X);
                maxY = MathF.Max(maxY, point.Y);
            }
        }

        if (float.IsPositiveInfinity(minX))
        {
            return new LayoutRect(0f, 0f, 0f, 0f);
        }

        return new LayoutRect(minX, minY, maxX - minX, maxY - minY);
    }
}

public class RectangleShape : Shape
{
    public static readonly DependencyProperty RadiusXProperty =
        DependencyProperty.Register(
            nameof(RadiusX),
            typeof(float),
            typeof(RectangleShape),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RadiusYProperty =
        DependencyProperty.Register(
            nameof(RadiusY),
            typeof(float),
            typeof(RectangleShape),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

    protected override Geometry? DefiningGeometry => null;

    public float RadiusX
    {
        get => GetValue<float>(RadiusXProperty);
        set => SetValue(RadiusXProperty, value);
    }

    public float RadiusY
    {
        get => GetValue<float>(RadiusYProperty);
        set => SetValue(RadiusYProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        return new Vector2(0f, 0f);
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var slot = LayoutSlot;
        if (Fill.A > 0)
        {
            UiDrawing.DrawFilledRect(spriteBatch, slot, Fill, Opacity);
        }

        if (Stroke.A > 0 && StrokeThickness > 0f)
        {
            UiDrawing.DrawRectStroke(spriteBatch, slot, StrokeThickness, Stroke, Opacity);
        }
    }
}

public class EllipseShape : Shape
{
    protected override Geometry? DefiningGeometry => null;

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var slot = LayoutSlot;
        var center = new Vector2(slot.X + (slot.Width / 2f), slot.Y + (slot.Height / 2f));
        var rx = MathF.Max(0f, slot.Width / 2f);
        var ry = MathF.Max(0f, slot.Height / 2f);
        if (rx <= 0f || ry <= 0f)
        {
            return;
        }

        const int segments = 48;
        var points = new Vector2[segments];
        for (var i = 0; i < segments; i++)
        {
            var angle = (MathF.PI * 2f * i) / segments;
            points[i] = new Vector2(
                center.X + (MathF.Cos(angle) * rx),
                center.Y + (MathF.Sin(angle) * ry));
        }

        if (Fill.A > 0)
        {
            UiDrawing.DrawFilledPolygon(spriteBatch, points, Fill, Opacity);
        }

        if (Stroke.A > 0 && StrokeThickness > 0f)
        {
            UiDrawing.DrawPolyline(spriteBatch, points, closed: true, StrokeThickness, Stroke, Opacity);
        }
    }
}

public class LineShape : Shape
{
    public static readonly DependencyProperty X1Property =
        DependencyProperty.Register(nameof(X1), typeof(float), typeof(LineShape), new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty Y1Property =
        DependencyProperty.Register(nameof(Y1), typeof(float), typeof(LineShape), new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty X2Property =
        DependencyProperty.Register(nameof(X2), typeof(float), typeof(LineShape), new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty Y2Property =
        DependencyProperty.Register(nameof(Y2), typeof(float), typeof(LineShape), new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

    protected override Geometry? DefiningGeometry => null;

    public float X1
    {
        get => GetValue<float>(X1Property);
        set => SetValue(X1Property, value);
    }

    public float Y1
    {
        get => GetValue<float>(Y1Property);
        set => SetValue(Y1Property, value);
    }

    public float X2
    {
        get => GetValue<float>(X2Property);
        set => SetValue(X2Property, value);
    }

    public float Y2
    {
        get => GetValue<float>(Y2Property);
        set => SetValue(Y2Property, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        return new Vector2(MathF.Abs(X2 - X1), MathF.Abs(Y2 - Y1));
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        if (Stroke.A == 0 || StrokeThickness <= 0f)
        {
            return;
        }

        var slot = LayoutSlot;
        var minX = MathF.Min(X1, X2);
        var minY = MathF.Min(Y1, Y2);
        var width = MathF.Max(0.001f, MathF.Abs(X2 - X1));
        var height = MathF.Max(0.001f, MathF.Abs(Y2 - Y1));
        var start = new Vector2(
            slot.X + (((X1 - minX) / width) * slot.Width),
            slot.Y + (((Y1 - minY) / height) * slot.Height));
        var end = new Vector2(
            slot.X + (((X2 - minX) / width) * slot.Width),
            slot.Y + (((Y2 - minY) / height) * slot.Height));
        UiDrawing.DrawLine(spriteBatch, start, end, StrokeThickness, Stroke, Opacity);
    }
}

public class PolylineShape : Shape
{
    public static readonly DependencyProperty PointsProperty =
        DependencyProperty.Register(
            nameof(Points),
            typeof(string),
            typeof(PolylineShape),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly PathGeometry _geometry = new();

    protected override Geometry? DefiningGeometry
    {
        get
        {
            var points = GeometryParsers.ParsePointList(Points);
            _geometry.Figures.Clear();
            if (points.Count >= 2)
            {
                _geometry.Figures.Add(new GeometryFigure(points.ToArray(), isClosed: false));
            }

            return _geometry;
        }
    }

    public string Points
    {
        get => GetValue<string>(PointsProperty) ?? string.Empty;
        set => SetValue(PointsProperty, value);
    }
}

public class PolygonShape : Shape
{
    public static readonly DependencyProperty PointsProperty =
        DependencyProperty.Register(
            nameof(Points),
            typeof(string),
            typeof(PolygonShape),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly PathGeometry _geometry = new();

    protected override Geometry? DefiningGeometry
    {
        get
        {
            var points = GeometryParsers.ParsePointList(Points);
            _geometry.Figures.Clear();
            if (points.Count >= 3)
            {
                _geometry.Figures.Add(new GeometryFigure(points.ToArray(), isClosed: true));
            }

            return _geometry;
        }
    }

    public string Points
    {
        get => GetValue<string>(PointsProperty) ?? string.Empty;
        set => SetValue(PointsProperty, value);
    }
}

public class PathShape : Shape
{
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(
            nameof(Data),
            typeof(Geometry),
            typeof(PathShape),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    protected override Geometry? DefiningGeometry => Data;

    public Geometry? Data
    {
        get => GetValue<Geometry>(DataProperty);
        set => SetValue(DataProperty, value);
    }
}
