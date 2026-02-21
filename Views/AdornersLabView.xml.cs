using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class AdornersLabView : UserControl
{
    private SpriteFont? _currentFont;
    private Border? _selectedShape;
    private LabSelectionAdorner? _selectionAdorner;
    private LabResizeHandlesAdorner? _handlesAdorner;
    private LabBadgeAdorner? _badgeAdorner;
    private AdornerTrackingMode _trackingMode = AdornerTrackingMode.RenderBounds;
    private HandleSet _activeHandleSet = HandleSet.Corners;
    private int _styleIndex;
    private readonly Random _random = new(1337);

    public AdornersLabView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "AdornersLabView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
        _selectedShape = ShapeAlpha;
        AttachAdorners();
        ApplySelectionVisuals();
        ApplyStylePreset(0);
        SetStatus("Adorners Lab loaded. Selected: Alpha.");
    }

    public void SetFont(SpriteFont? font)
    {
        if (font == null)
        {
            return;
        }

        _currentFont = font;
        ApplyFontRecursive(this, font);
    }

    private void OnSelectAlphaClick(object? sender, RoutedSimpleEventArgs args)
    {
        SelectShape(ShapeAlpha, "Alpha");
    }

    private void OnSelectBetaClick(object? sender, RoutedSimpleEventArgs args)
    {
        SelectShape(ShapeBeta, "Beta");
    }

    private void OnSelectGammaClick(object? sender, RoutedSimpleEventArgs args)
    {
        SelectShape(ShapeGamma, "Gamma");
    }

    private void OnToggleHandlesModeClick(object? sender, RoutedSimpleEventArgs args)
    {
        _activeHandleSet = _activeHandleSet == HandleSet.Corners ? HandleSet.All : HandleSet.Corners;
        if (_handlesAdorner != null)
        {
            _handlesAdorner.ActiveHandles = _activeHandleSet;
        }

        SetStatus($"Handle mode: {_activeHandleSet}.");
    }

    private void OnToggleTrackingModeClick(object? sender, RoutedSimpleEventArgs args)
    {
        _trackingMode = _trackingMode == AdornerTrackingMode.RenderBounds
            ? AdornerTrackingMode.LayoutSlot
            : AdornerTrackingMode.RenderBounds;

        if (_selectionAdorner != null)
        {
            _selectionAdorner.TrackingMode = _trackingMode;
        }

        if (_handlesAdorner != null)
        {
            _handlesAdorner.TrackingMode = _trackingMode;
        }

        if (_badgeAdorner != null)
        {
            _badgeAdorner.TrackingMode = _trackingMode;
        }

        SetStatus($"Tracking mode: {_trackingMode}.");
    }

    private void OnCycleStyleClick(object? sender, RoutedSimpleEventArgs args)
    {
        _styleIndex = (_styleIndex + 1) % 4;
        ApplyStylePreset(_styleIndex);
        SetStatus($"Style preset: {_styleIndex + 1}/4.");
    }

    private void OnChaosNudgeClick(object? sender, RoutedSimpleEventArgs args)
    {
        if (_selectedShape == null)
        {
            return;
        }

        var dx = _random.Next(-120, 121);
        var dy = _random.Next(-90, 91);
        var dw = _random.Next(-70, 121);
        var dh = _random.Next(-60, 111);

        NudgeSelected(dx, dy);
        ResizeSelected(dw, dh, keepRightEdge: false, keepBottomEdge: false);
        SetStatus($"Chaos nudge: dx={dx}, dy={dy}, dw={dw}, dh={dh}.");
    }

    private void OnReattachClick(object? sender, RoutedSimpleEventArgs args)
    {
        AttachAdorners();
        SetStatus("Adorners reattached.");
    }

    private void OnClearClick(object? sender, RoutedSimpleEventArgs args)
    {
        if (LabAdornerRoot == null)
        {
            return;
        }

        LabAdornerRoot.AdornerLayer.ClearAllAdorners();
        _selectionAdorner = null;
        _handlesAdorner = null;
        _badgeAdorner = null;
        SetStatus("All adorners cleared.");
    }

    private void OnScrollLeftClick(object? sender, RoutedSimpleEventArgs args)
    {
        if (LabScrollViewer == null)
        {
            return;
        }

        LabScrollViewer.ScrollToHorizontalOffset(LabScrollViewer.HorizontalOffset - 220f);
        SetStatus($"HorizontalOffset={LabScrollViewer.HorizontalOffset:0}.");
    }

    private void OnScrollRightClick(object? sender, RoutedSimpleEventArgs args)
    {
        if (LabScrollViewer == null)
        {
            return;
        }

        LabScrollViewer.ScrollToHorizontalOffset(LabScrollViewer.HorizontalOffset + 220f);
        SetStatus($"HorizontalOffset={LabScrollViewer.HorizontalOffset:0}.");
    }

    private void OnNudgeUpClick(object? sender, RoutedSimpleEventArgs args)
    {
        NudgeSelected(0f, -20f);
    }

    private void OnNudgeDownClick(object? sender, RoutedSimpleEventArgs args)
    {
        NudgeSelected(0f, 20f);
    }

    private void OnNudgeLeftClick(object? sender, RoutedSimpleEventArgs args)
    {
        NudgeSelected(-24f, 0f);
    }

    private void OnNudgeRightClick(object? sender, RoutedSimpleEventArgs args)
    {
        NudgeSelected(24f, 0f);
    }

    private void OnHandleDragDelta(object? sender, HandleDragDeltaEventArgs args)
    {
        if (_selectedShape == null)
        {
            return;
        }

        var affectsWidth = args.Handle is
            HandleKind.TopLeft or HandleKind.Left or HandleKind.BottomLeft or
            HandleKind.TopRight or HandleKind.Right or HandleKind.BottomRight;
        var affectsHeight = args.Handle is
            HandleKind.TopLeft or HandleKind.Top or HandleKind.TopRight or
            HandleKind.BottomLeft or HandleKind.Bottom or HandleKind.BottomRight;
        var keepRightEdge = args.Handle is HandleKind.TopLeft or HandleKind.Left or HandleKind.BottomLeft;
        var keepBottomEdge = args.Handle is HandleKind.TopLeft or HandleKind.Top or HandleKind.TopRight;

        ResizeSelected(
            affectsWidth ? args.HorizontalChange : 0f,
            affectsHeight ? args.VerticalChange : 0f,
            keepRightEdge,
            keepBottomEdge);
        SetStatus($"Resize {args.Handle}: {_selectedShape.Width:0}x{_selectedShape.Height:0}.");
    }

    private void SelectShape(Border? shape, string name)
    {
        if (shape == null)
        {
            return;
        }

        _selectedShape = shape;
        AttachAdorners();
        ApplySelectionVisuals();
        SetStatus($"Selected: {name}.");
    }

    private void AttachAdorners()
    {
        if (LabAdornerRoot == null || _selectedShape == null)
        {
            return;
        }

        var layer = LabAdornerRoot.AdornerLayer;
        layer.ClearAllAdorners();

        _selectionAdorner = new LabSelectionAdorner(_selectedShape)
        {
            TrackingMode = _trackingMode,
            Inset = 3f
        };
        layer.AddAdorner(_selectionAdorner);

        _handlesAdorner = new LabResizeHandlesAdorner(_selectedShape)
        {
            TrackingMode = _trackingMode,
            ActiveHandles = _activeHandleSet
        };
        _handlesAdorner.HandleDragDelta += OnHandleDragDelta;
        layer.AddAdorner(_handlesAdorner);

        _badgeAdorner = new LabBadgeAdorner(_selectedShape)
        {
            TrackingMode = _trackingMode
        };
        layer.AddAdorner(_badgeAdorner);

        ApplyStylePreset(_styleIndex);
    }

    private void ApplySelectionVisuals()
    {
        ApplyShapeBorder(ShapeAlpha, ReferenceEquals(_selectedShape, ShapeAlpha));
        ApplyShapeBorder(ShapeBeta, ReferenceEquals(_selectedShape, ShapeBeta));
        ApplyShapeBorder(ShapeGamma, ReferenceEquals(_selectedShape, ShapeGamma));
        ApplyShapeBorder(ShapeDelta, ReferenceEquals(_selectedShape, ShapeDelta));
    }

    private static void ApplyShapeBorder(Border? shape, bool selected)
    {
        if (shape == null)
        {
            return;
        }

        shape.BorderThickness = selected ? new Thickness(3f) : new Thickness(2f);
    }

    private void ApplyStylePreset(int index)
    {
        if (_selectionAdorner == null || _handlesAdorner == null || _badgeAdorner == null)
        {
            return;
        }

        switch (index)
        {
            case 0:
                _selectionAdorner.Fill = new Color(90, 170, 240, 42);
                _selectionAdorner.Stroke = new Color(143, 214, 255);
                _selectionAdorner.StrokeThickness = 1f;
                _handlesAdorner.HandleSize = 10f;
                _handlesAdorner.HandleFill = new Color(84, 84, 84);
                _handlesAdorner.HandleStroke = new Color(198, 198, 198);
                _handlesAdorner.Stroke = new Color(143, 214, 255);
                _handlesAdorner.StrokeThickness = 1f;
                _badgeAdorner.Fill = new Color(255, 214, 105);
                _badgeAdorner.Stroke = new Color(63, 43, 7);
                break;
            case 1:
                _selectionAdorner.Fill = new Color(255, 95, 160, 44);
                _selectionAdorner.Stroke = new Color(255, 174, 220);
                _selectionAdorner.StrokeThickness = 2f;
                _handlesAdorner.HandleSize = 12f;
                _handlesAdorner.HandleFill = new Color(255, 111, 178);
                _handlesAdorner.HandleStroke = new Color(76, 13, 48);
                _handlesAdorner.Stroke = new Color(255, 174, 220);
                _handlesAdorner.StrokeThickness = 2f;
                _badgeAdorner.Fill = new Color(144, 238, 144);
                _badgeAdorner.Stroke = new Color(18, 68, 32);
                break;
            case 2:
                _selectionAdorner.Fill = new Color(116, 255, 190, 40);
                _selectionAdorner.Stroke = new Color(184, 255, 219);
                _selectionAdorner.StrokeThickness = 1f;
                _handlesAdorner.HandleSize = 14f;
                _handlesAdorner.HandleFill = new Color(43, 129, 90);
                _handlesAdorner.HandleStroke = new Color(202, 255, 229);
                _handlesAdorner.Stroke = new Color(184, 255, 219);
                _handlesAdorner.StrokeThickness = 1f;
                _badgeAdorner.Fill = new Color(124, 199, 255);
                _badgeAdorner.Stroke = new Color(16, 54, 84);
                break;
            default:
                _selectionAdorner.Fill = new Color(255, 194, 122, 40);
                _selectionAdorner.Stroke = new Color(255, 228, 189);
                _selectionAdorner.StrokeThickness = 3f;
                _handlesAdorner.HandleSize = 16f;
                _handlesAdorner.HandleFill = new Color(228, 166, 90);
                _handlesAdorner.HandleStroke = new Color(79, 45, 8);
                _handlesAdorner.Stroke = new Color(255, 228, 189);
                _handlesAdorner.StrokeThickness = 2f;
                _badgeAdorner.Fill = new Color(255, 137, 137);
                _badgeAdorner.Stroke = new Color(92, 20, 20);
                break;
        }
    }

    private void NudgeSelected(float dx, float dy)
    {
        if (_selectedShape == null)
        {
            return;
        }

        var left = Canvas.GetLeft(_selectedShape) + dx;
        var top = Canvas.GetTop(_selectedShape) + dy;
        Canvas.SetLeft(_selectedShape, left);
        Canvas.SetTop(_selectedShape, top);
        SetStatus($"Position: x={left:0}, y={top:0}.");
    }

    private void ResizeSelected(float widthDelta, float heightDelta, bool keepRightEdge, bool keepBottomEdge)
    {
        if (_selectedShape == null)
        {
            return;
        }

        var minWidth = 40f;
        var minHeight = 30f;
        var oldWidth = _selectedShape.Width;
        var oldHeight = _selectedShape.Height;
        var newWidth = MathF.Max(minWidth, oldWidth + widthDelta);
        var newHeight = MathF.Max(minHeight, oldHeight + heightDelta);
        var appliedWidthDelta = newWidth - oldWidth;
        var appliedHeightDelta = newHeight - oldHeight;

        _selectedShape.Width = newWidth;
        _selectedShape.Height = newHeight;

        if (keepRightEdge)
        {
            Canvas.SetLeft(_selectedShape, Canvas.GetLeft(_selectedShape) - appliedWidthDelta);
        }

        if (keepBottomEdge)
        {
            Canvas.SetTop(_selectedShape, Canvas.GetTop(_selectedShape) - appliedHeightDelta);
        }
    }

    private void SetStatus(string text)
    {
        if (LabStatusLabel != null)
        {
            LabStatusLabel.Text = text;
        }
    }

    private static void ApplyFontRecursive(UIElement? element, SpriteFont font)
    {
        if (element == null)
        {
            return;
        }

        if (element is Label label)
        {
            label.Font = font;
        }

        if (element is TextBlock textBlock)
        {
            textBlock.Font = font;
        }

        if (element is Button button)
        {
            button.Font = font;
        }

        foreach (var child in element.GetVisualChildren())
        {
            ApplyFontRecursive(child, font);
        }
    }

    private sealed class LabSelectionAdorner : AnchoredAdorner
    {
        public LabSelectionAdorner(UIElement adornedElement)
            : base(adornedElement)
        {
        }

        public float Inset { get; set; } = 2f;

        public Color Stroke { get; set; } = new(143, 214, 255);

        public Color Fill { get; set; } = new(90, 170, 240, 42);

        public float StrokeThickness { get; set; } = 1f;

        protected override LayoutRect GetAnchorRect(LayoutRect targetBounds)
        {
            return new LayoutRect(
                targetBounds.X - Inset,
                targetBounds.Y - Inset,
                targetBounds.Width + (Inset * 2f),
                targetBounds.Height + (Inset * 2f));
        }

        protected override void OnRenderAdorner(SpriteBatch spriteBatch, LayoutRect rect)
        {
            if (Fill.A > 0)
            {
                UiDrawing.DrawFilledRect(spriteBatch, rect, Fill, Opacity);
            }

            if (StrokeThickness > 0f)
            {
                UiDrawing.DrawRectStroke(spriteBatch, rect, StrokeThickness, Stroke, Opacity);
            }
        }
    }

    private sealed class LabResizeHandlesAdorner : HandlesAdornerBase
    {
        public LabResizeHandlesAdorner(UIElement adornedElement)
            : base(adornedElement)
        {
        }

        public HandleSet ActiveHandles { get; set; } = HandleSet.Corners;

        protected override HandleSet Handles => ActiveHandles;

        protected override void OnRenderAdorner(SpriteBatch spriteBatch, LayoutRect rect)
        {
            if (StrokeThickness > 0f)
            {
                UiDrawing.DrawRectStroke(spriteBatch, rect, StrokeThickness, Stroke, Opacity);
            }
        }

        protected override (float HorizontalChange, float VerticalChange) TransformHandleDragDelta(
            HandleKind handle,
            float horizontalChange,
            float verticalChange)
        {
            if (handle is HandleKind.TopLeft or HandleKind.Top or HandleKind.TopRight)
            {
                verticalChange = -verticalChange;
            }

            if (handle is HandleKind.TopLeft or HandleKind.Left or HandleKind.BottomLeft)
            {
                horizontalChange = -horizontalChange;
            }

            return (horizontalChange, verticalChange);
        }
    }

    private sealed class LabBadgeAdorner : AnchoredAdorner
    {
        public LabBadgeAdorner(UIElement adornedElement)
            : base(adornedElement)
        {
        }

        public float Size { get; set; } = 14f;

        public float OffsetX { get; set; } = 10f;

        public float OffsetY { get; set; } = -10f;

        public Color Fill { get; set; } = new(255, 214, 105);

        public Color Stroke { get; set; } = new(63, 43, 7);

        protected override LayoutRect GetAnchorRect(LayoutRect targetBounds)
        {
            return new LayoutRect(
                targetBounds.X + targetBounds.Width + OffsetX,
                targetBounds.Y + OffsetY,
                Size,
                Size);
        }

        protected override void OnRenderAdorner(SpriteBatch spriteBatch, LayoutRect rect)
        {
            UiDrawing.DrawFilledRect(spriteBatch, rect, Fill, Opacity);
            UiDrawing.DrawRectStroke(spriteBatch, rect, 1f, Stroke, Opacity);
        }
    }
}
