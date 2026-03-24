using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

/// <summary>
/// A rendering primitive that draws ink strokes.
/// Hosted by <see cref="InkCanvas"/> for the complete inking experience.
/// Modeled after WPF <c>System.Windows.Controls.InkPresenter</c>.
/// </summary>
public class InkPresenter : Control
{
    private InkStrokeCollection? _attachedStrokes;

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
                    if (dependencyObject is not InkPresenter presenter)
                    {
                        return;
                    }

                    presenter.OnStrokesChanged(
                        args.OldValue as InkStrokeCollection,
                        args.NewValue as InkStrokeCollection);
                }));

    public InkStrokeCollection? Strokes
    {
        get => GetValue<InkStrokeCollection>(StrokesProperty);
        set => SetValue(StrokesProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        return availableSize;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        base.OnRender(spriteBatch);

        var strokes = Strokes;
        if (strokes == null || strokes.Count == 0)
        {
            return;
        }

        if (TryGetClipRect(out var clipRect))
        {
            UiDrawing.PushClip(spriteBatch, clipRect);
        }

        try
        {
            for (int s = 0; s < strokes.Count; s++)
            {
                var stroke = strokes[s];
                var points = stroke.Points;
                if (points.Count < 2)
                {
                    continue;
                }

                var attr = stroke.DrawingAttributes;
                var color = attr.Color;
                if (attr.Opacity < 1f)
                {
                    color = new Color(color.R, color.G, color.B, (byte)(color.A * attr.Opacity));
                }

                float thickness = attr.Width;

                // Draw as polyline segments for performance on hot paths
                UiDrawing.DrawPolyline(
                    spriteBatch,
                    points,
                    closed: false,
                    thickness,
                    color,
                    attr.Opacity);
            }
        }
        finally
        {
            if (TryGetClipRect(out _))
            {
                UiDrawing.PopClip(spriteBatch);
            }
        }
    }

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        clipRect = LayoutSlot;
        return true;
    }

    private void OnStrokesChanged(InkStrokeCollection? oldStrokes, InkStrokeCollection? newStrokes)
    {
        if (ReferenceEquals(_attachedStrokes, newStrokes))
        {
            return;
        }

        if (_attachedStrokes != null)
        {
            _attachedStrokes.Changed -= OnStrokesCollectionChanged;
        }

        _attachedStrokes = newStrokes;

        if (_attachedStrokes != null)
        {
            _attachedStrokes.Changed += OnStrokesCollectionChanged;
        }
    }

    private void OnStrokesCollectionChanged(object? sender, EventArgs e)
    {
        InvalidateArrange();
    }
}
