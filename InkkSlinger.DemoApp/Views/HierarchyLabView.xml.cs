using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class HierarchyLabView : UserControl
{
    private static readonly Color WorkspaceBackground = new(24, 25, 27);
    private static readonly Color WorkspaceDotColor = new(46, 49, 54, 150);
    private static readonly Color NodeBackground = new(21, 22, 24);
    private static readonly Color NodeBorder = new(48, 51, 58);
    private static readonly Color NodeText = new(229, 231, 234);
    private static readonly Color Accent = new(198, 112, 224);
    private static readonly Color AccentDim = new(147, 91, 172);

    private const int NodeCount = 63;
    private const float NodeWidth = 208f;
    private const float NodeHeight = 60f;
    private const float ColumnGap = 132f;
    private const float RowGap = 34f;
    private const float CanvasPadding = 28f;
    private const float MinimumWorkspaceWidth = 2400f;
    private const float MinimumWorkspaceHeight = 1200f;
    private const float MinimumZoom = 0.35f;
    private const float MaximumZoom = 2.5f;
    private const float WheelZoomStep = 1.12f;

    private readonly List<LabNode> _nodes = new();
    private readonly List<LabConnector> _connectors = new();
    private readonly HierarchyLabGraphLayerCanvas _graphLayer = new()
    {
        Name = "HierarchyLabGraphLayer"
    };

    private float _zoom = MinimumZoom;
    private float _workspaceLogicalWidth = MinimumWorkspaceWidth;
    private float _workspaceLogicalHeight = MinimumWorkspaceHeight;
    private float _lastViewportWidth = -1f;
    private float _lastViewportHeight = -1f;

    public HierarchyLabView()
    {
        InitializeComponent();
        HierarchyLabWorkspace.AddChild(_graphLayer);
        HierarchyLabScrollViewer.LayoutUpdated += OnScrollViewerLayoutUpdated;
        HierarchyLabScrollViewer.AddHandler<MouseWheelRoutedEventArgs>(
            UIElement.PreviewMouseWheelEvent,
            OnPreviewMouseWheel,
            handledEventsToo: true);
        BuildGraph();
    }

    private void BuildGraph()
    {
        _nodes.Clear();
        _connectors.Clear();

        var byId = Enumerable.Range(0, NodeCount)
            .Select(static id => new LabNode(id, $"Node {id:D2}", 0f, 0f))
            .ToArray();

        var leafY = CanvasPadding;
        LayoutNode(id: 0, depth: 0, byId, ref leafY);

        foreach (var node in byId)
        {
            _nodes.Add(node);
            var left = (node.Id * 2) + 1;
            var right = left + 1;
            if (left < byId.Length)
            {
                _connectors.Add(CreateConnector(node, byId[left]));
            }

            if (right < byId.Length)
            {
                _connectors.Add(CreateConnector(node, byId[right]));
            }
        }

        _workspaceLogicalWidth = MathF.Max(MinimumWorkspaceWidth, _nodes.Max(static node => node.X + NodeWidth + CanvasPadding));
        _workspaceLogicalHeight = MathF.Max(MinimumWorkspaceHeight, _nodes.Max(static node => node.Y + NodeHeight + CanvasPadding));
        RealizeGraph();
    }

    private static float LayoutNode(int id, int depth, LabNode[] nodes, ref float leafY)
    {
        var left = (id * 2) + 1;
        var right = left + 1;
        float y;
        if (left >= nodes.Length)
        {
            y = leafY;
            leafY += NodeHeight + RowGap;
        }
        else
        {
            var leftY = LayoutNode(left, depth + 1, nodes, ref leafY);
            var rightY = right < nodes.Length
                ? LayoutNode(right, depth + 1, nodes, ref leafY)
                : leftY;
            y = (leftY + rightY) / 2f;
        }

        nodes[id] = nodes[id] with
        {
            X = CanvasPadding + (depth * (NodeWidth + ColumnGap)),
            Y = y
        };
        return y;
    }

    private static LabConnector CreateConnector(LabNode parent, LabNode child)
    {
        return new LabConnector(
            parent.X + NodeWidth,
            parent.Y + (NodeHeight / 2f),
            child.X,
            child.Y + (NodeHeight / 2f));
    }

    private void RealizeGraph()
    {
        while (_graphLayer.Children.Count > 0)
        {
            _graphLayer.RemoveChildAt(_graphLayer.Children.Count - 1);
        }

        ApplyZoom();

        foreach (var connector in _connectors)
        {
            _graphLayer.AddChild(CreateConnectorElement(connector));
        }

        foreach (var node in _nodes)
        {
            _graphLayer.AddChild(CreateNodeElement(node));
        }
    }

    private static PathShape CreateConnectorElement(LabConnector connector)
    {
        var path = new PathShape
        {
            Stroke = AccentDim,
            Stretch = Stretch.None,
            StrokeStartLineCap = StrokeLineCap.Round,
            StrokeEndLineCap = StrokeLineCap.Round,
            StrokeThickness = 2f
        };

        var verticalPad = 6f;
        var left = MathF.Min(connector.FromX, connector.ToX);
        var top = MathF.Min(connector.FromY, connector.ToY) - verticalPad;
        var width = MathF.Max(1f, MathF.Abs(connector.ToX - connector.FromX));
        var height = MathF.Max(12f, MathF.Abs(connector.ToY - connector.FromY) + (verticalPad * 2f));
        var fromX = connector.FromX - left;
        var fromY = connector.FromY - top;
        var toX = connector.ToX - left;
        var toY = connector.ToY - top;
        var controlX = MathF.Max(44f, MathF.Abs(toX - fromX) * 0.42f);

        path.Width = width;
        path.Height = height;
        path.Data = CreateSmoothConnectorGeometry(
            new Vector2(fromX, fromY),
            new Vector2(fromX + controlX, fromY),
            new Vector2(toX - controlX, toY),
            new Vector2(toX, toY));
        Canvas.SetLeft(path, left);
        Canvas.SetTop(path, top);
        return path;
    }

    private static PathGeometry CreateSmoothConnectorGeometry(
        Vector2 start,
        Vector2 control1,
        Vector2 control2,
        Vector2 end)
    {
        const int segments = 56;
        var points = new Vector2[segments + 1];
        points[0] = start;
        for (var i = 1; i <= segments; i++)
        {
            var t = i / (float)segments;
            var u = 1f - t;
            points[i] =
                (u * u * u * start) +
                (3f * u * u * t * control1) +
                (3f * u * t * t * control2) +
                (t * t * t * end);
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(new GeometryFigure(points, isClosed: false));
        return geometry;
    }

    private static Button CreateNodeElement(LabNode node)
    {
        var root = new Canvas
        {
            Width = NodeWidth + 5f,
            Height = NodeHeight,
            ClipToBounds = false
        };

        var chrome = new Border
        {
            Width = NodeWidth,
            Height = NodeHeight,
            Background = new SolidColorBrush(NodeBackground),
            BorderBrush = new SolidColorBrush(NodeBorder),
            BorderThickness = new Thickness(1f),
            CornerRadius = new CornerRadius(8f),
            ClipToBounds = true
        };
        root.AddChild(chrome);

        var accent = new Border
        {
            Width = 3f,
            Height = 28f,
            Background = new SolidColorBrush(Accent),
            CornerRadius = new CornerRadius(2f)
        };
        Canvas.SetLeft(accent, 14f);
        Canvas.SetTop(accent, 16f);
        root.AddChild(accent);

        var label = new TextBlock
        {
            Text = node.Label,
            Foreground = NodeText,
            FontSize = 14f,
            Width = 150f,
            Height = 24f,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Canvas.SetLeft(label, 31f);
        Canvas.SetTop(label, 18f);
        root.AddChild(label);

        var port = new EllipseShape
        {
            Width = 9f,
            Height = 9f,
            Fill = NodeBackground,
            Stroke = AccentDim,
            StrokeThickness = 1.5f
        };
        Canvas.SetLeft(port, NodeWidth - 4.5f);
        Canvas.SetTop(port, 25.5f);
        root.AddChild(port);

        var button = new Button
        {
            Name = node.Id == 0 ? "HierarchyLabRootNode" : $"HierarchyLabNode{node.Id:D2}",
            Width = NodeWidth + 5f,
            Height = NodeHeight,
            Padding = new Thickness(0f),
            Background = Color.Transparent,
            Foreground = NodeText,
            BorderBrush = Color.Transparent,
            BorderThickness = 0f,
            ClipToBounds = false,
            Content = root
        };
        Canvas.SetLeft(button, node.X);
        Canvas.SetTop(button, node.Y);
        return button;
    }

    private void OnPreviewMouseWheel(object? sender, MouseWheelRoutedEventArgs args)
    {
        _ = sender;
        if (args.Delta == 0)
        {
            return;
        }

        var oldZoom = _zoom;
        var steps = Math.Clamp(args.Delta / 120f, -6f, 6f);
        var nextZoom = Math.Clamp(oldZoom * MathF.Pow(WheelZoomStep, steps), MinimumZoom, MaximumZoom);
        if (MathF.Abs(nextZoom - oldZoom) < 0.001f)
        {
            return;
        }

        var pointerInViewport = GetPointerInViewport(args.Position);
        var oldLayout = CalculateZoomLayout(_workspaceLogicalWidth, _workspaceLogicalHeight, oldZoom, GetViewportSize());
        var logicalAnchorX = (HierarchyLabScrollViewer.HorizontalOffset + pointerInViewport.X - oldLayout.LeftOffset) / oldZoom;
        var logicalAnchorY = (HierarchyLabScrollViewer.VerticalOffset + pointerInViewport.Y - oldLayout.TopOffset) / oldZoom;

        _zoom = nextZoom;
        ApplyZoom();
        ScrollToZoomAnchor(logicalAnchorX, logicalAnchorY, pointerInViewport);
        args.Handled = true;
    }

    private void OnScrollViewerLayoutUpdated(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        var viewport = GetViewportSize();
        if (MathF.Abs(viewport.X - _lastViewportWidth) <= 0.5f &&
            MathF.Abs(viewport.Y - _lastViewportHeight) <= 0.5f)
        {
            return;
        }

        ApplyZoom();
    }

    private Vector2 GetPointerInViewport(Vector2 pointerPosition)
    {
        var slot = HierarchyLabScrollViewer.LayoutSlot;
        var padding = HierarchyLabScrollViewer.Padding;
        return new Vector2(
            MathF.Max(0f, pointerPosition.X - slot.X - padding.Left),
            MathF.Max(0f, pointerPosition.Y - slot.Y - padding.Top));
    }

    private void ScrollToZoomAnchor(float logicalAnchorX, float logicalAnchorY, Vector2 pointerInViewport)
    {
        var layout = CalculateZoomLayout(_workspaceLogicalWidth, _workspaceLogicalHeight, _zoom, GetViewportSize());
        HierarchyLabScrollViewer.ScrollToHorizontalOffset(MathF.Max(0f, layout.LeftOffset + (logicalAnchorX * _zoom) - pointerInViewport.X));
        HierarchyLabScrollViewer.ScrollToVerticalOffset(MathF.Max(0f, layout.TopOffset + (logicalAnchorY * _zoom) - pointerInViewport.Y));
    }

    private void ApplyZoom()
    {
        var viewport = GetViewportSize();
        var layout = CalculateZoomLayout(_workspaceLogicalWidth, _workspaceLogicalHeight, _zoom, viewport);
        _lastViewportWidth = viewport.X;
        _lastViewportHeight = viewport.Y;
        HierarchyLabWorkspace.ApplyZoomLayout(layout);
        HierarchyLabScrollViewer.InvalidateScrollInfo();
        _graphLayer.Width = _workspaceLogicalWidth;
        _graphLayer.Height = _workspaceLogicalHeight;
        _graphLayer.SetZoomTransform(layout.Zoom, layout.LeftOffset, layout.TopOffset);
        Canvas.SetLeft(_graphLayer, 0f);
        Canvas.SetTop(_graphLayer, 0f);
    }

    private Vector2 GetViewportSize()
    {
        var width = HierarchyLabScrollViewer.ViewportWidth;
        var height = HierarchyLabScrollViewer.ViewportHeight;
        if (width <= 0f || height <= 0f)
        {
            var slot = HierarchyLabScrollViewer.LayoutSlot;
            var padding = HierarchyLabScrollViewer.Padding;
            width = MathF.Max(0f, slot.Width - padding.Horizontal);
            height = MathF.Max(0f, slot.Height - padding.Vertical);
        }

        return new Vector2(width, height);
    }

    internal static HierarchyLabZoomLayout CalculateZoomLayout(
        float logicalWidth,
        float logicalHeight,
        float zoom,
        Vector2 viewportSize)
    {
        var safeZoom = Math.Clamp(zoom, MinimumZoom, MaximumZoom);
        var scaledWidth = MathF.Max(0f, logicalWidth * safeZoom);
        var scaledHeight = MathF.Max(0f, logicalHeight * safeZoom);
        var viewportWidth = MathF.Max(0f, viewportSize.X);
        var viewportHeight = MathF.Max(0f, viewportSize.Y);
        var canvasWidth = MathF.Max(scaledWidth, viewportWidth);
        var canvasHeight = MathF.Max(scaledHeight, viewportHeight);
        return new HierarchyLabZoomLayout(
            safeZoom,
            logicalWidth,
            logicalHeight,
            canvasWidth,
            canvasHeight,
            MathF.Max(0f, (canvasWidth - scaledWidth) / 2f),
            MathF.Max(0f, (canvasHeight - scaledHeight) / 2f));
    }

    private readonly record struct LabNode(int Id, string Label, float X, float Y);

    private readonly record struct LabConnector(float FromX, float FromY, float ToX, float ToY);
}

public readonly record struct HierarchyLabZoomLayout(
    float Zoom,
    float LogicalWidth,
    float LogicalHeight,
    float CanvasWidth,
    float CanvasHeight,
    float LeftOffset,
    float TopOffset);

public readonly record struct HierarchyLabGraphLayerRuntimeDiagnosticsSnapshot(
    float Zoom,
    bool IsTransformActive,
    float LayoutSlotX,
    float LayoutSlotY,
    float LayoutSlotWidth,
    float LayoutSlotHeight,
    int RenderCallCount,
    double RenderMilliseconds,
    int LocalRenderTransformCallCount,
    int LocalRenderTransformActiveCount);

public sealed class HierarchyLabGraphLayerCanvas : Canvas
{
    private float _zoom = 1f;
    private float _visualOffsetX;
    private float _visualOffsetY;
    private int _renderCallCount;
    private long _renderElapsedTicks;
    private int _localRenderTransformCallCount;
    private int _localRenderTransformActiveCount;

    public float Zoom
    {
        get => _zoom;
        set
        {
            var coerced = MathF.Max(0.001f, value);
            if (MathF.Abs(_zoom - coerced) < 0.0001f)
            {
                return;
            }

            _zoom = coerced;
            InvalidateTransformMetadata();
        }
    }

    public void SetZoomTransform(float zoom, float visualOffsetX, float visualOffsetY)
    {
        var coercedZoom = MathF.Max(0.001f, zoom);
        var changed =
            MathF.Abs(_zoom - coercedZoom) >= 0.0001f ||
            MathF.Abs(_visualOffsetX - visualOffsetX) >= 0.01f ||
            MathF.Abs(_visualOffsetY - visualOffsetY) >= 0.01f;
        if (!changed)
        {
            return;
        }

        _zoom = coercedZoom;
        _visualOffsetX = visualOffsetX;
        _visualOffsetY = visualOffsetY;
        InvalidateTransformMetadata();
    }

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        clipRect = default;
        return false;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var startTicks = Stopwatch.GetTimestamp();
        base.OnRender(spriteBatch);
        _renderCallCount++;
        _renderElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
    }

    protected override bool TryGetLocalRenderTransform(out Matrix transform, out Matrix inverseTransform)
    {
        _localRenderTransformCallCount++;
        if (MathF.Abs(_zoom - 1f) < 0.0001f &&
            MathF.Abs(_visualOffsetX) < 0.01f &&
            MathF.Abs(_visualOffsetY) < 0.01f)
        {
            transform = Matrix.Identity;
            inverseTransform = Matrix.Identity;
            return false;
        }

        var originX = LayoutSlot.X;
        var originY = LayoutSlot.Y;
        transform =
            Matrix.CreateTranslation(-originX, -originY, 0f) *
            Matrix.CreateScale(_zoom, _zoom, 1f) *
            Matrix.CreateTranslation(originX + _visualOffsetX, originY + _visualOffsetY, 0f);

        var inverseZoom = 1f / _zoom;
        inverseTransform =
            Matrix.CreateTranslation(-(originX + _visualOffsetX), -(originY + _visualOffsetY), 0f) *
            Matrix.CreateScale(inverseZoom, inverseZoom, 1f) *
            Matrix.CreateTranslation(originX, originY, 0f);
        _localRenderTransformActiveCount++;
        return true;
    }

    private void InvalidateTransformMetadata()
    {
        var uiRoot = UiRoot.Current;
        if (uiRoot == null)
        {
            InvalidateVisual();
            return;
        }

        uiRoot.NotifyDirectRenderInvalidation(this, RenderInvalidationKind.Transform);
    }

    internal HierarchyLabGraphLayerRuntimeDiagnosticsSnapshot GetHierarchyLabGraphLayerSnapshotForDiagnostics()
    {
        return new HierarchyLabGraphLayerRuntimeDiagnosticsSnapshot(
            _zoom,
            MathF.Abs(_zoom - 1f) >= 0.0001f,
            LayoutSlot.X,
            LayoutSlot.Y,
            LayoutSlot.Width,
            LayoutSlot.Height,
            _renderCallCount,
            _renderElapsedTicks * 1000d / Stopwatch.Frequency,
            _localRenderTransformCallCount,
            _localRenderTransformActiveCount);
    }
}

public sealed class HierarchyLabWorkspaceCanvas : Canvas, IScrollTransformContent, IScrollViewerMeasureConstraintProvider
{
    private const float DotSpacing = 16f;
    private const float DotSize = 1f;
    private static readonly Color WorkspaceBackground = new(24, 25, 27);
    private static readonly Color WorkspaceDotColor = new(46, 49, 54, 150);
    private float _logicalExtentWidth;
    private float _logicalExtentHeight;
    private float _transformedExtentWidth;
    private float _transformedExtentHeight;
    private float _contentScale = 1f;

    public HierarchyLabWorkspaceCanvas()
    {
        ScrollViewer.SetIsTransformContentLayerStable(this, true);
    }

    internal float LogicalExtentWidth => _logicalExtentWidth;

    internal float LogicalExtentHeight => _logicalExtentHeight;

    internal float TransformedExtentWidth => _transformedExtentWidth;

    internal float TransformedExtentHeight => _transformedExtentHeight;

    internal float ContentScale => _contentScale;

    internal void ApplyZoomLayout(HierarchyLabZoomLayout layout)
    {
        var nextLogicalWidth = MathF.Max(0f, layout.LogicalWidth);
        var nextLogicalHeight = MathF.Max(0f, layout.LogicalHeight);
        var logicalExtentChanged =
            !AreClose(_logicalExtentWidth, nextLogicalWidth) ||
            !AreClose(_logicalExtentHeight, nextLogicalHeight);
        if (logicalExtentChanged)
        {
            _logicalExtentWidth = nextLogicalWidth;
            _logicalExtentHeight = nextLogicalHeight;
            InvalidateMeasure();
        }

        _transformedExtentWidth = MathF.Max(0f, layout.CanvasWidth);
        _transformedExtentHeight = MathF.Max(0f, layout.CanvasHeight);
        _contentScale = MathF.Max(0.001f, layout.Zoom);
    }

    Vector2 IScrollViewerMeasureConstraintProvider.GetScrollViewerMeasureConstraint(
        float viewportWidth,
        float viewportHeight,
        bool canScrollHorizontally,
        bool canScrollVertically)
    {
        return new Vector2(
            canScrollHorizontally ? float.PositiveInfinity : MathF.Max(0f, viewportWidth),
            canScrollVertically ? float.PositiveInfinity : MathF.Max(0f, viewportHeight));
    }

    bool IScrollTransformContent.TryGetScrollTransformContentMetrics(out ScrollTransformContentMetrics metrics)
    {
        metrics = new ScrollTransformContentMetrics(
            new Vector2(_logicalExtentWidth, _logicalExtentHeight),
            new Vector2(_contentScale, _contentScale),
            ResolveTransformOffset());
        return true;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        _ = availableSize;
        return new Vector2(_logicalExtentWidth, _logicalExtentHeight);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        foreach (var child in Children)
        {
            if (child is not FrameworkElement frameworkChild)
            {
                continue;
            }

            var left = Canvas.GetLeft(frameworkChild);
            var top = Canvas.GetTop(frameworkChild);
            var width = float.IsNaN(frameworkChild.Width)
                ? frameworkChild.DesiredSize.X
                : frameworkChild.Width;
            var height = float.IsNaN(frameworkChild.Height)
                ? frameworkChild.DesiredSize.Y
                : frameworkChild.Height;

            frameworkChild.Arrange(new LayoutRect(
                LayoutSlot.X + ResolveOffset(left),
                LayoutSlot.Y + ResolveOffset(top),
                MathF.Max(0f, width),
                MathF.Max(0f, height)));
        }

        return finalSize;
    }

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        clipRect = default;
        return false;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        base.OnRender(spriteBatch);
        var slot = LayoutSlot;
        UiDrawing.DrawFilledRect(spriteBatch, slot, WorkspaceBackground, Opacity);

        var startX = MathF.Floor(slot.X / DotSpacing) * DotSpacing;
        var startY = MathF.Floor(slot.Y / DotSpacing) * DotSpacing;
        for (var y = startY; y <= slot.Y + slot.Height; y += DotSpacing)
        {
            for (var x = startX; x <= slot.X + slot.Width; x += DotSpacing)
            {
                UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x, y, DotSize, DotSize), WorkspaceDotColor, Opacity);
            }
        }
    }

    private static float ResolveOffset(float value)
    {
        return float.IsNaN(value) ? 0f : value;
    }

    private Vector2 ResolveTransformOffset()
    {
        return new Vector2(
            MathF.Max(0f, (_transformedExtentWidth - (_logicalExtentWidth * _contentScale)) / 2f),
            MathF.Max(0f, (_transformedExtentHeight - (_logicalExtentHeight * _contentScale)) / 2f));
    }

    private static bool AreClose(float left, float right)
    {
        return MathF.Abs(left - right) <= 0.01f;
    }
}
