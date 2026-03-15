using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public enum InkCanvasEditingMode
{
    Ink,
    EraseByStroke,
    EraseByPoint,
    None
}

public class InkCanvas : Control
{
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
                    if (dependencyObject is InkCanvas canvas)
                    {
                        canvas.OnStrokesChanged(args.OldValue as InkStrokeCollection, args.NewValue as InkStrokeCollection);
                    }
                }));

    public static readonly DependencyProperty EditingModeProperty =
        DependencyProperty.Register(
            nameof(EditingMode),
            typeof(InkCanvasEditingMode),
            typeof(InkCanvas),
            new FrameworkPropertyMetadata(InkCanvasEditingMode.Ink));

    public static readonly DependencyProperty DefaultDrawingAttributesProperty =
        DependencyProperty.Register(
            nameof(DefaultDrawingAttributes),
            typeof(InkDrawingAttributes),
            typeof(InkCanvas),
            new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty UseCustomCursorProperty =
        DependencyProperty.Register(
            nameof(UseCustomCursor),
            typeof(bool),
            typeof(InkCanvas),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty ActiveEditingModeProperty =
        DependencyProperty.Register(
            nameof(ActiveEditingMode),
            typeof(InkCanvasEditingMode),
            typeof(InkCanvas),
            new FrameworkPropertyMetadata(InkCanvasEditingMode.Ink));

    public static readonly RoutedEvent StrokeCollectedEvent = new(nameof(StrokeCollected), RoutingStrategy.Bubble);

    public InkStrokeCollection? Strokes
    {
        get => GetValue<InkStrokeCollection>(StrokesProperty);
        set => SetValue(StrokesProperty, value);
    }

    public InkCanvasEditingMode EditingMode
    {
        get => GetValue<InkCanvasEditingMode>(EditingModeProperty);
        set => SetValue(EditingModeProperty, value);
    }

    public InkDrawingAttributes? DefaultDrawingAttributes
    {
        get => GetValue<InkDrawingAttributes>(DefaultDrawingAttributesProperty);
        set => SetValue(DefaultDrawingAttributesProperty, value);
    }

    public bool UseCustomCursor
    {
        get => GetValue<bool>(UseCustomCursorProperty);
        set => SetValue(UseCustomCursorProperty, value);
    }

    public InkCanvasEditingMode ActiveEditingMode
    {
        get => GetValue<InkCanvasEditingMode>(ActiveEditingModeProperty);
        private set => SetValue(ActiveEditingModeProperty, value);
    }

    public event EventHandler<InkStrokeCollectedEventArgs> StrokeCollected
    {
        add => AddHandler(StrokeCollectedEvent, value);
        remove => RemoveHandler(StrokeCollectedEvent, value);
    }

    private InkStroke? _currentStroke;
    private bool _isCapturing;
    private InkStrokeCollection? _strokes;

    public InkCanvas()
    {
        Strokes = new InkStrokeCollection();
        DefaultDrawingAttributes = new InkDrawingAttributes();
        ActiveEditingMode = EditingMode;
    }

    private void OnStrokesChanged(InkStrokeCollection? oldStrokes, InkStrokeCollection? newStrokes)
    {
        if (oldStrokes != null)
        {
            oldStrokes.Changed -= OnStrokesCollectionChanged;
        }

        _strokes = newStrokes;

        if (newStrokes != null)
        {
            newStrokes.Changed += OnStrokesCollectionChanged;
        }
    }

    private void OnStrokesCollectionChanged(object? sender, EventArgs e)
    {
        InvalidateVisual();
    }

    public bool HandlePointerDownFromInput(Vector2 pointerPosition, bool extendSelection)
    {
        _ = extendSelection;

        if (!IsEnabled)
        {
            return false;
        }

        if (EditingMode == InkCanvasEditingMode.None)
        {
            return false;
        }

        ActiveEditingMode = EditingMode;

        // Start a new stroke
        var drawingAttributes = DefaultDrawingAttributes ?? new InkDrawingAttributes();
        _currentStroke = new InkStroke(new[] { pointerPosition })
        {
            Color = drawingAttributes.Color,
            Thickness = drawingAttributes.Thickness,
            Opacity = drawingAttributes.Opacity
        };

        _isCapturing = true;
        CapturePointer(this);
        InvalidateVisual();
        return true;
    }

    public bool HandlePointerMoveFromInput(Vector2 pointerPosition)
    {
        if (!_isCapturing || _currentStroke == null)
        {
            return false;
        }

        if (!IsEnabled || EditingMode == InkCanvasEditingMode.None)
        {
            return false;
        }

        // Add point to current stroke
        _currentStroke.AddPoint(pointerPosition);
        InvalidateVisual();
        return true;
    }

    public bool HandlePointerUpFromInput()
    {
        if (!_isCapturing)
        {
            return false;
        }

        if (_currentStroke != null && _currentStroke.Points.Count >= 2)
        {
            var strokes = Strokes;
            if (strokes != null)
            {
                strokes.Add(_currentStroke);
                RaiseRoutedEventInternal(
                    StrokeCollectedEvent,
                    new InkStrokeCollectedEventArgs(StrokeCollectedEvent, _currentStroke));
            }
        }

        _currentStroke = null;
        _isCapturing = false;
        ReleasePointerCapture(this);
        InvalidateVisual();
        return true;
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (args.Property == EditingModeProperty)
        {
            ActiveEditingMode = (InkCanvasEditingMode)args.NewValue;
        }
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        // Draw background
        UiDrawing.DrawFilledRect(spriteBatch, LayoutSlot, Background * Opacity);

        // Draw existing strokes
        var strokes = Strokes;
        if (strokes != null && strokes.Count > 0)
        {
            foreach (var stroke in strokes.Strokes)
            {
                DrawStroke(spriteBatch, stroke);
            }
        }

        // Draw current stroke being drawn
        if (_currentStroke != null)
        {
            DrawStroke(spriteBatch, _currentStroke);
        }
    }

    private void DrawStroke(SpriteBatch spriteBatch, InkStroke stroke)
    {
        var points = stroke.Points;
        if (points.Count < 2)
        {
            return;
        }

        var color = new Color(
            stroke.Color.R,
            stroke.Color.G,
            stroke.Color.B,
            (byte)(stroke.Color.A * stroke.Opacity * Opacity));

        // Draw as lines between points
        for (int i = 0; i < points.Count - 1; i++)
        {
            var p1 = points[i];
            var p2 = points[i + 1];
            UiDrawing.DrawLine(spriteBatch, p1, p2, color, stroke.Thickness);
        }
    }

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        clipRect = LayoutSlot;
        return true;
    }

    public void ClearStrokes()
    {
        var strokes = Strokes;
        strokes?.Clear();
    }
}

public sealed class InkDrawingAttributes
{
    public Color Color { get; set; } = Color.Black;
    public float Thickness { get; set; } = 2f;
    public float Opacity { get; set; } = 1f;
    public bool FitToCurve { get; set; } = true;
}

public sealed class InkStrokeCollectedEventArgs : RoutedEventArgs
{
    public InkStroke Stroke { get; }

    public InkStrokeCollectedEventArgs(RoutedEvent routedEvent, InkStroke stroke) : base(routedEvent)
    {
        Stroke = stroke;
    }
}
