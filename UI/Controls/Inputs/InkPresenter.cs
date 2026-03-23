using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class InkPresenter : Control
{
    public static readonly DependencyProperty StrokesProperty =
        DependencyProperty.Register(
            nameof(Strokes),
            typeof(InkStrokeCollection),
            typeof(InkPresenter),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is InkPresenter presenter)
                    {
                        presenter.OnStrokesChanged(args.OldValue as InkStrokeCollection, args.NewValue as InkStrokeCollection);
                    }
                }));

    public static readonly DependencyProperty StrokeColorProperty =
        DependencyProperty.Register(
            nameof(StrokeColor),
            typeof(Color),
            typeof(InkPresenter),
            new FrameworkPropertyMetadata(Color.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(
            nameof(StrokeThickness),
            typeof(float),
            typeof(InkPresenter),
            new FrameworkPropertyMetadata(2f, FrameworkPropertyMetadataOptions.AffectsRender));

    public InkStrokeCollection? Strokes
    {
        get => GetValue<InkStrokeCollection>(StrokesProperty);
        set => SetValue(StrokesProperty, value);
    }

    public Color StrokeColor
    {
        get => GetValue<Color>(StrokeColorProperty);
        set => SetValue(StrokeColorProperty, value);
    }

    public float StrokeThickness
    {
        get => GetValue<float>(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public InkPresenter()
    {
        Strokes = new InkStrokeCollection();
    }

    private void OnStrokesChanged(InkStrokeCollection? oldStrokes, InkStrokeCollection? newStrokes)
    {
        if (oldStrokes != null)
        {
            oldStrokes.Changed -= OnStrokesCollectionChanged;
        }

        if (newStrokes != null)
        {
            newStrokes.Changed += OnStrokesCollectionChanged;
        }
    }

    private void OnStrokesCollectionChanged(object? sender, EventArgs e)
    {
        InvalidateVisual();
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var strokes = Strokes;
        if (strokes == null || strokes.Count == 0)
        {
            return;
        }

        var color = StrokeColor * Opacity;
        var thickness = StrokeThickness;

        foreach (var stroke in strokes.Strokes)
        {
            DrawStroke(spriteBatch, stroke, color, thickness);
        }
    }

    private void DrawStroke(SpriteBatch spriteBatch, InkStroke stroke, Color color, float thickness)
    {
        var points = stroke.Points;
        if (points.Count < 2)
        {
            return;
        }

        var strokeColor = new Color(
            color.R,
            color.G,
            color.B,
            (byte)(color.A * stroke.Opacity));

        // Draw as lines between points
        for (int i = 0; i < points.Count - 1; i++)
        {
            var p1 = points[i];
            var p2 = points[i + 1];
            DrawLine(spriteBatch, p1, p2, strokeColor, thickness);
        }
    }

    private void DrawLine(SpriteBatch spriteBatch, Vector2 p1, Vector2 p2, Color color, float thickness)
    {
        UiDrawing.DrawLine(spriteBatch, p1, p2, thickness, color);
    }

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        clipRect = LayoutSlot;
        return true;
    }
}
