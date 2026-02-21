using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class PaintShellView : UserControl
{
    private SelectionOverlayAdorner? _selectionAdorner;
    private ResizeHandlesOverlayAdorner? _handlesAdorner;

    public PaintShellView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "PaintShellView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
        InitializeShellData();
        AttachSelectionAdorners();
    }

    public void SetFont(SpriteFont? font)
    {
        if (font == null)
        {
            return;
        }

        ApplyFontRecursive(this, font);
    }

    private void InitializeShellData()
    {
        if (LayersGrid != null)
        {
            LayersGrid.AddItems(new object[]
            {
                new LayerRow { Name = "Ink", Visible = "Yes", Opacity = "100%" },
                new LayerRow { Name = "Highlights", Visible = "Yes", Opacity = "75%" },
                new LayerRow { Name = "Sketch", Visible = "No", Opacity = "40%" }
            });
        }

        if (HistoryGrid != null)
        {
            HistoryGrid.AddItems(new object[]
            {
                new HistoryRow { Action = "Create Layer", Time = "10:01" },
                new HistoryRow { Action = "Brush Stroke", Time = "10:03" },
                new HistoryRow { Action = "Move Selection", Time = "10:05" }
            });
        }

        SetStatus("Paint shell initialized.");
    }

    private void AttachSelectionAdorners()
    {
        if (CanvasAdornerRoot == null || SelectedShape == null)
        {
            return;
        }

        var layer = CanvasAdornerRoot.AdornerLayer;
        _selectionAdorner = new SelectionOverlayAdorner(SelectedShape)
        {
            TrackingMode = AdornerTrackingMode.RenderBounds,
            Inset = 2f
        };
        layer.AddAdorner(_selectionAdorner);

        _handlesAdorner = new ResizeHandlesOverlayAdorner(SelectedShape)
        {
            TrackingMode = AdornerTrackingMode.RenderBounds,
            HandleSize = 10f
        };
        _handlesAdorner.HandleDragDelta += OnHandleDragDelta;
        layer.AddAdorner(_handlesAdorner);
    }

    private void OnHandleDragDelta(object? sender, HandleDragDeltaEventArgs args)
    {
        if (SelectedShape == null)
        {
            return;
        }

        var width = MathF.Max(30f, SelectedShape.Width + args.HorizontalChange);
        var height = MathF.Max(20f, SelectedShape.Height + args.VerticalChange);
        SelectedShape.Width = width;
        SelectedShape.Height = height;

        SetStatus($"Resize {args.Handle}: {width:0}x{height:0}");
    }

    private void OnFileNewClick(object? sender, RoutedSimpleEventArgs args) => SetStatus("File > New");
    private void OnFileOpenClick(object? sender, RoutedSimpleEventArgs args) => SetStatus("File > Open");
    private void OnFileSaveClick(object? sender, RoutedSimpleEventArgs args) => SetStatus("File > Save");
    private void OnFileExitClick(object? sender, RoutedSimpleEventArgs args) => SetStatus("File > Exit (demo)");
    private void OnEditUndoClick(object? sender, RoutedSimpleEventArgs args) => SetStatus("Edit > Undo");
    private void OnEditRedoClick(object? sender, RoutedSimpleEventArgs args) => SetStatus("Edit > Redo");
    private void OnViewZoomInClick(object? sender, RoutedSimpleEventArgs args) => SetStatus("View > Zoom In");
    private void OnViewZoomOutClick(object? sender, RoutedSimpleEventArgs args) => SetStatus("View > Zoom Out");
    private void OnToolSelectClick(object? sender, RoutedSimpleEventArgs args) => SetStatus("Tool: Select");
    private void OnToolBrushClick(object? sender, RoutedSimpleEventArgs args) => SetStatus("Tool: Brush");
    private void OnToolEraserClick(object? sender, RoutedSimpleEventArgs args) => SetStatus("Tool: Eraser");
    private void OnShapeRectangleClick(object? sender, RoutedSimpleEventArgs args) => SetStatus("Shape: Rectangle");
    private void OnShapeEllipseClick(object? sender, RoutedSimpleEventArgs args) => SetStatus("Shape: Ellipse");
    private void OnShapeLineClick(object? sender, RoutedSimpleEventArgs args) => SetStatus("Shape: Line");
    private void OnZoomPlusClick(object? sender, RoutedSimpleEventArgs args) => SetStatus("Zoom +");
    private void OnZoomMinusClick(object? sender, RoutedSimpleEventArgs args) => SetStatus("Zoom -");

    private void SetStatus(string text)
    {
        if (StatusTextItem != null)
        {
            StatusTextItem.Content = text;
        }
    }

    private static void ApplyFontRecursive(UIElement? element, SpriteFont font)
    {
        if (element == null)
        {
            return;
        }

        if (element is TextBlock textBlock)
        {
            textBlock.Font = font;
        }

        if (element is Button button)
        {
            button.Font = font;
        }

        if (element is DataGrid dataGrid)
        {
            dataGrid.Font = font;
        }

        foreach (var child in element.GetVisualChildren())
        {
            ApplyFontRecursive(child, font);
        }
    }

    private sealed class LayerRow
    {
        public string Name { get; set; } = string.Empty;

        public string Visible { get; set; } = string.Empty;

        public string Opacity { get; set; } = string.Empty;
    }

    private sealed class HistoryRow
    {
        public string Action { get; set; } = string.Empty;

        public string Time { get; set; } = string.Empty;
    }

    private sealed class SelectionOverlayAdorner : AnchoredAdorner
    {
        public SelectionOverlayAdorner(UIElement adornedElement)
            : base(adornedElement)
        {
        }

        public float Inset { get; set; }

        public Color Stroke { get; set; } = new(117, 190, 255);

        public Color Fill { get; set; } = new(117, 190, 255, 30);

        public float StrokeThickness { get; set; } = 1f;

        protected override LayoutRect GetAnchorRect(LayoutRect bounds)
        {
            return new LayoutRect(
                bounds.X - Inset,
                bounds.Y - Inset,
                bounds.Width + (Inset * 2f),
                bounds.Height + (Inset * 2f));
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

    private sealed class ResizeHandlesOverlayAdorner : HandlesAdornerBase
    {
        public ResizeHandlesOverlayAdorner(UIElement adornedElement)
            : base(adornedElement)
        {
        }

        protected override HandleSet Handles => HandleSet.Corners;

        protected override void OnRenderAdorner(SpriteBatch spriteBatch, LayoutRect rect)
        {
            UiDrawing.DrawRectStroke(spriteBatch, rect, 1f, Stroke, Opacity);
        }

        protected override (float HorizontalChange, float VerticalChange) TransformHandleDragDelta(
            HandleKind handle,
            float horizontalChange,
            float verticalChange)
        {
            if (handle is HandleKind.TopLeft or HandleKind.TopRight)
            {
                verticalChange = -verticalChange;
            }

            if (handle is HandleKind.TopLeft or HandleKind.BottomLeft)
            {
                horizontalChange = -horizontalChange;
            }

            return (horizontalChange, verticalChange);
        }
    }
}
