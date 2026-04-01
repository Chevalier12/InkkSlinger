using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

/// <summary>
/// A canvas control that supports ink (pen/stylus/mouse) input for drawing strokes.
/// Hosts an <see cref="InkPresenter"/> internally and manages pointer capture lifecycle.
/// Modeled after WPF <c>System.Windows.Controls.InkCanvas</c>.
/// </summary>
public class InkCanvas : Control
{
    private readonly InkPresenter _presenter;
    private InkStroke? _activeStroke;
    private readonly List<Vector2> _activePoints = new();

    public static readonly DependencyProperty StrokesProperty =
        DependencyProperty.Register(
            nameof(Strokes),
            typeof(InkStrokeCollection),
            typeof(InkCanvas),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is not InkCanvas canvas)
                    {
                        return;
                    }

                    canvas._presenter.Strokes = args.NewValue as InkStrokeCollection;
                }));

    public static readonly DependencyProperty DefaultDrawingAttributesProperty =
        DependencyProperty.Register(
            nameof(DefaultDrawingAttributes),
            typeof(InkDrawingAttributes),
            typeof(InkCanvas),
            new FrameworkPropertyMetadata(null));

    public InkCanvas()
    {
        _presenter = new InkPresenter();
        AddChild(_presenter);

        // Ensure we have a default stroke collection
        var strokes = new InkStrokeCollection();
        Strokes = strokes;
    }

    /// <summary>
    /// The collection of strokes managed by this canvas.
    /// </summary>
    public InkStrokeCollection? Strokes
    {
        get => GetValue<InkStrokeCollection>(StrokesProperty);
        set => SetValue(StrokesProperty, value);
    }

    /// <summary>
    /// The default drawing attributes applied to new strokes.
    /// </summary>
    public InkDrawingAttributes? DefaultDrawingAttributes
    {
        get => GetValue<InkDrawingAttributes>(DefaultDrawingAttributesProperty);
        set => SetValue(DefaultDrawingAttributesProperty, value);
    }

    /// <summary>
    /// True while a stroke is being actively drawn (between pointer down and pointer up).
    /// </summary>
    public bool IsDrawing => _activeStroke != null;

    /// <summary>
    /// Internal presenter used for rendering. Exposed for testing.
    /// </summary>
    internal InkPresenter Presenter => _presenter;

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        _presenter.Measure(availableSize);
        return availableSize;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        _presenter.Arrange(new LayoutRect(LayoutSlot.X, LayoutSlot.Y, finalSize.X, finalSize.Y));
        return finalSize;
    }

    /// <summary>
    /// Called by the input pipeline when the pointer is pressed over this canvas.
    /// </summary>
    internal bool HandlePointerDownFromInput(Vector2 pointerPosition)
    {
        if (!IsEnabled || !IsHitTestVisible)
        {
            return false;
        }

        // Only handle if pointer is within our layout slot
        if (!LayoutSlot.Contains(pointerPosition))
        {
            return false;
        }

        BeginStroke(pointerPosition);
        return true;
    }

    /// <summary>
    /// Called by the input pipeline when a captured pointer moves.
    /// </summary>
    internal void HandlePointerMoveFromInput(Vector2 pointerPosition)
    {
        if (_activeStroke == null)
        {
            return;
        }

        ContinueStroke(pointerPosition);
    }

    /// <summary>
    /// Called by the input pipeline when the captured pointer is released.
    /// </summary>
    internal void HandlePointerUpFromInput()
    {
        if (_activeStroke == null)
        {
            return;
        }

        EndStroke();
    }

    private void BeginStroke(Vector2 position)
    {
        var strokes = Strokes;
        if (strokes == null)
        {
            return;
        }

        _activePoints.Clear();
        _activePoints.Add(position);

        var attrs = DefaultDrawingAttributes?.Clone() ?? new InkDrawingAttributes();
        _activeStroke = new InkStroke(_activePoints, attrs);
        strokes.Add(_activeStroke);
    }

    private void ContinueStroke(Vector2 position)
    {
        if (_activeStroke == null)
        {
            return;
        }

        _activePoints.Add(position);
        _activeStroke.AddPoint(position);
        _presenter.InvalidateArrange();
    }

    private void EndStroke()
    {
        _activeStroke = null;
        _activePoints.Clear();
    }
}
