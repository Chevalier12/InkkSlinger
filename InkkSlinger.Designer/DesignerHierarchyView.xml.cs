using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using InkkSlinger;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger.Designer;

public partial class DesignerHierarchyView : UserControl
{
    private static readonly Color WorkspaceBackground = new(24, 25, 27);
    private static readonly Color WorkspaceDotColor = new(46, 49, 54, 150);
    private static readonly Color NodeBackground = new(21, 22, 24);
    private static readonly Color NodeBorder = new(48, 51, 58);
    private static readonly Color NodeSelectedBackground = new(36, 38, 43);
    private static readonly Color NodeSelectedBorder = new(168, 88, 230);
    private static readonly Color NodeText = new(229, 231, 234);
    private static readonly Color NodeSelectedText = new(245, 238, 255);
    private static readonly Color NodeMutedGlyph = new(130, 136, 146);
    private static readonly Color Accent = new(198, 112, 224);
    private static readonly Color AccentDim = new(147, 91, 172);

    private const float NodeWidth = 208f;
    private const float NodeHeight = 60f;
    private const float NodeHorizontalPadding = 16f;
    private const float NodeAccentWidth = 3f;
    private const float NodeAccentHeight = 28f;
    private const float NodeCornerRadius = 8f;
    private const float PortDiameter = 9f;
    private const float PortRadius = PortDiameter / 2f;
    private const float MenuGlyphWidth = 12f;
    private const float NodeLabelWidth = NodeWidth - (NodeHorizontalPadding * 2f) - NodeAccentWidth - MenuGlyphWidth - 24f;
    private const float ColumnGap = 132f;
    private const float RowGap = 34f;
    private const float CanvasPadding = 28f;
    private const float MinimumWorkspaceWidth = 2400f;
    private const float MinimumWorkspaceHeight = 1200f;
    private const float MinimumZoom = 0.35f;
    private const float MaximumZoom = 2.5f;
    private const float WheelZoomStep = 1.12f;
    private IReadOnlyList<GraphNode> _lastGraphNodes = Array.Empty<GraphNode>();
    private IReadOnlyList<GraphConnector> _lastConnectors = Array.Empty<GraphConnector>();
    private readonly List<RealizedNode> _realizedNodes = new();
    private readonly List<RealizedConnector> _realizedConnectors = new();
    private readonly GraphLayerCanvas _graphLayer = new()
    {
        Name = "HierarchyGraphLayer"
    };
    private TextBlock? _emptyStateText;
    private DesignerShellViewModel? _viewModel;
    private float _zoom = 1f;
    private float _workspaceLogicalWidth = MinimumWorkspaceWidth;
    private float _workspaceLogicalHeight = MinimumWorkspaceHeight;
    private float _lastViewportWidth = -1f;
    private float _lastViewportHeight = -1f;
    private bool _hasAppliedZoomLayout;
    private HierarchyZoomLayout _lastAppliedZoomLayout;

    public DesignerHierarchyView()
    {
        InitializeComponent();
        HierarchyCanvas.AddChild(_graphLayer);
        HierarchyScrollViewer.LayoutUpdated += OnHierarchyScrollViewerLayoutUpdated;
        HierarchyScrollViewer.AddHandler<MouseWheelRoutedEventArgs>(
            UIElement.PreviewMouseWheelEvent,
            OnHierarchyScrollViewerPreviewMouseWheel,
            handledEventsToo: true);
        DependencyPropertyChanged += OnDependencyPropertyChanged;
        Dispatcher.EnqueueDeferred(RebuildGraph);
    }

    private void OnDependencyPropertyChanged(object? sender, DependencyPropertyChangedEventArgs args)
    {
        if (args.Property != DataContextProperty)
        {
            return;
        }

        AttachViewModel(DataContext as DesignerShellViewModel);
        RebuildGraph();
    }

    private void AttachViewModel(DesignerShellViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(DesignerShellViewModel.VisualTreeRoots))
        {
            RebuildGraph();
        }
    }

    private void RebuildGraph()
    {
        AttachViewModel(DataContext as DesignerShellViewModel);
        ClearCanvas();

        if (_viewModel == null || _viewModel.VisualTreeRoots.Count == 0)
        {
            AddEmptyState();
            return;
        }

        var graphNodes = new List<GraphNode>();
        var connectors = new List<GraphConnector>();
        var nextLeafY = CanvasPadding;
        foreach (var root in _viewModel.VisualTreeRoots)
        {
            LayoutNode(root, depth: 0, graphNodes, connectors, ref nextLeafY);
            nextLeafY += RowGap;
        }

        _lastGraphNodes = graphNodes;
        _lastConnectors = connectors;

        var width = graphNodes.Count == 0
            ? 720f
            : graphNodes.Max(static node => node.X + NodeWidth + CanvasPadding);
        var height = graphNodes.Count == 0
            ? 420f
            : graphNodes.Max(static node => node.Y + NodeHeight + CanvasPadding);
        SetWorkspaceSize(
            MathF.Max(MinimumWorkspaceWidth, width),
            MathF.Max(MinimumWorkspaceHeight, height));

        RealizeGraph();
    }

    private void RealizeGraph()
    {
        ClearGraphLayer();
        ApplyZoom();

        foreach (var connector in _lastConnectors)
        {
            var realized = CreateConnector(connector);
            _realizedConnectors.Add(realized);
            _graphLayer.AddChild(realized.Element);
        }

        foreach (var node in _lastGraphNodes)
        {
            var realized = CreateNode(node);
            _realizedNodes.Add(realized);
            _graphLayer.AddChild(realized.Button);
        }
    }

    private float LayoutNode(
        DesignerVisualTreeNodeViewModel node,
        int depth,
        List<GraphNode> graphNodes,
        List<GraphConnector> connectors,
        ref float nextLeafY)
    {
        var visibleChildren = node.Children;
        float y;
        if (visibleChildren.Count == 0)
        {
            y = nextLeafY;
            nextLeafY += NodeHeight + RowGap;
        }
        else
        {
            var childYs = new List<float>(visibleChildren.Count);
            foreach (var child in visibleChildren)
            {
                childYs.Add(LayoutNode(child, depth + 1, graphNodes, connectors, ref nextLeafY));
            }

            y = childYs.Sum() / childYs.Count;
        }

        var x = CanvasPadding + (depth * (NodeWidth + ColumnGap));
        var graphNode = new GraphNode(node, x, y);
        graphNodes.Add(graphNode);

        foreach (var child in visibleChildren)
        {
            var childX = CanvasPadding + ((depth + 1) * (NodeWidth + ColumnGap));
            var childY = graphNodes.First(graphNode => ReferenceEquals(graphNode.Source, child)).Y;
            connectors.Add(new GraphConnector(
                x + NodeWidth,
                y + (NodeHeight / 2f),
                childX,
                childY + (NodeHeight / 2f)));
        }

        return y;
    }

    private RealizedConnector CreateConnector(GraphConnector connector)
    {
        var path = new PathShape
        {
            Stroke = AccentDim,
            Stretch = Stretch.None,
            StrokeStartLineCap = StrokeLineCap.Round,
            StrokeEndLineCap = StrokeLineCap.Round
        };

        var realized = new RealizedConnector(connector, path);
        UpdateConnectorLayout(realized);
        return realized;
    }

    private RealizedNode CreateNode(GraphNode graphNode)
    {
        var source = graphNode.Source;
        var content = CreateNodeContent(source);
        var button = new Button
        {
            Padding = new Thickness(0f),
            Background = Color.Transparent,
            Foreground = NodeText,
            BorderBrush = Color.Transparent,
            BorderThickness = 0f,
            ClipToBounds = false,
            Command = _viewModel?.SelectVisualTreeNodeCommand,
            CommandParameter = source,
            Content = content.Root
        };

        var realized = new RealizedNode(graphNode, button, content);
        UpdateNodeLayout(realized);
        return realized;
    }

    private static NodeVisualContent CreateNodeContent(DesignerVisualTreeNodeViewModel source)
    {
        var root = new Canvas
        {
            ClipToBounds = false
        };

        var chrome = new Border
        {
            Background = new SolidColorBrush(source.IsSelected ? NodeSelectedBackground : NodeBackground),
            BorderBrush = new SolidColorBrush(source.IsSelected ? NodeSelectedBorder : NodeBorder),
            BorderThickness = new Thickness(1f),
            CornerRadius = new CornerRadius(NodeCornerRadius),
            ClipToBounds = true
        };
        root.AddChild(chrome);

        var leftPort = new Border
        {
            Background = new SolidColorBrush(Accent),
            CornerRadius = new CornerRadius(2f)
        };
        root.AddChild(leftPort);

        var label = new TextBlock
        {
            Text = source.Label,
            Foreground = source.IsSelected ? NodeSelectedText : NodeText,
            FontWeight = "Normal",
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ClipToBounds = true
        };
        root.AddChild(label);

        var menuGlyph = new MenuDotsCanvas();
        root.AddChild(menuGlyph);

        var outputPort = new EllipseShape
        {
            Fill = NodeBackground,
            Stroke = AccentDim,
            StrokeThickness = 1.5f
        };
        root.AddChild(outputPort);

        return new NodeVisualContent(root, chrome, leftPort, label, menuGlyph, outputPort);
    }

    private void AddEmptyState()
    {
        _lastGraphNodes = Array.Empty<GraphNode>();
        _lastConnectors = Array.Empty<GraphConnector>();
        SetWorkspaceSize(MinimumWorkspaceWidth, MinimumWorkspaceHeight);
        _emptyStateText = new TextBlock
        {
            Text = "Refresh the preview to build the hierarchy graph.",
            Foreground = new Color(199, 203, 211),
        };
        UpdateEmptyStateLayout();
        _graphLayer.AddChild(_emptyStateText);
    }

    private void ClearCanvas()
    {
        ClearGraphLayer();
    }

    private void ClearGraphLayer()
    {
        _emptyStateText = null;
        _realizedNodes.Clear();
        _realizedConnectors.Clear();
        while (_graphLayer.Children.Count > 0)
        {
            _graphLayer.RemoveChildAt(_graphLayer.Children.Count - 1);
        }
    }

    private void OnHierarchyScrollViewerPreviewMouseWheel(object? sender, MouseWheelRoutedEventArgs args)
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

        var pointerInViewport = GetPointerInHierarchyViewport(args.Position);
        var oldLayout = CalculateZoomLayout(_workspaceLogicalWidth, _workspaceLogicalHeight, oldZoom, GetHierarchyViewportSize());
        var logicalAnchorX = (HierarchyScrollViewer.HorizontalOffset + pointerInViewport.X - oldLayout.LeftOffset) / oldZoom;
        var logicalAnchorY = (HierarchyScrollViewer.VerticalOffset + pointerInViewport.Y - oldLayout.TopOffset) / oldZoom;

        _zoom = nextZoom;
        ApplyZoom();
        ScrollToZoomAnchor(logicalAnchorX, logicalAnchorY, pointerInViewport);
        args.Handled = true;
    }

    private void OnHierarchyScrollViewerLayoutUpdated(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        var viewport = GetHierarchyViewportSize();
        if (MathF.Abs(viewport.X - _lastViewportWidth) <= 0.5f &&
            MathF.Abs(viewport.Y - _lastViewportHeight) <= 0.5f)
        {
            return;
        }

        ApplyZoom();
    }

    private Vector2 GetPointerInHierarchyViewport(Vector2 pointerPosition)
    {
        var slot = HierarchyScrollViewer.LayoutSlot;
        var padding = HierarchyScrollViewer.Padding;
        return new Vector2(
            MathF.Max(0f, pointerPosition.X - slot.X - padding.Left),
            MathF.Max(0f, pointerPosition.Y - slot.Y - padding.Top));
    }

    private void ScrollToZoomAnchor(float logicalAnchorX, float logicalAnchorY, Vector2 pointerInViewport)
    {
        var layout = CalculateZoomLayout(_workspaceLogicalWidth, _workspaceLogicalHeight, _zoom, GetHierarchyViewportSize());
        HierarchyScrollViewer.ScrollToHorizontalOffset(MathF.Max(0f, layout.LeftOffset + (logicalAnchorX * _zoom) - pointerInViewport.X));
        HierarchyScrollViewer.ScrollToVerticalOffset(MathF.Max(0f, layout.TopOffset + (logicalAnchorY * _zoom) - pointerInViewport.Y));
    }

    private void SetWorkspaceSize(float logicalWidth, float logicalHeight)
    {
        _workspaceLogicalWidth = logicalWidth;
        _workspaceLogicalHeight = logicalHeight;
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        var viewport = GetHierarchyViewportSize();
        var layout = CalculateZoomLayout(_workspaceLogicalWidth, _workspaceLogicalHeight, _zoom, viewport);
        _lastViewportWidth = viewport.X;
        _lastViewportHeight = viewport.Y;
        if (_hasAppliedZoomLayout && AreZoomLayoutsClose(_lastAppliedZoomLayout, layout))
        {
            return;
        }

        _hasAppliedZoomLayout = true;
        _lastAppliedZoomLayout = layout;
        HierarchyCanvas.ApplyZoomLayout(layout);
        HierarchyScrollViewer.InvalidateScrollInfo();
        _graphLayer.Width = _workspaceLogicalWidth;
        _graphLayer.Height = _workspaceLogicalHeight;
        _graphLayer.SetZoomTransform(layout.Zoom, layout.LeftOffset, layout.TopOffset);
        Canvas.SetLeft(_graphLayer, 0f);
        Canvas.SetTop(_graphLayer, 0f);
    }

    private void UpdateGraphLayout()
    {
        foreach (var connector in _realizedConnectors)
        {
            UpdateConnectorLayout(connector);
        }

        foreach (var node in _realizedNodes)
        {
            UpdateNodeLayout(node);
        }

        UpdateEmptyStateLayout();
    }

    private static void UpdateConnectorLayout(RealizedConnector realized)
    {
        var connector = realized.Source;
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
        var data = CreateSmoothConnectorGeometry(
            new Vector2(fromX, fromY),
            new Vector2(fromX + controlX, fromY),
            new Vector2(toX - controlX, toY),
            new Vector2(toX, toY));

        realized.Element.Width = width;
        realized.Element.Height = height;
        realized.Element.Data = data;
        realized.Element.StrokeThickness = 2f;
        Canvas.SetLeft(realized.Element, left);
        Canvas.SetTop(realized.Element, top);
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

    private static void UpdateNodeLayout(RealizedNode realized)
    {
        var source = realized.Source.Source;
        realized.Button.Width = NodeWidth + PortRadius;
        realized.Button.Height = NodeHeight;
        realized.Button.Background = Color.Transparent;
        realized.Button.Foreground = source.IsSelected ? NodeSelectedText : NodeText;
        realized.Button.BorderBrush = Color.Transparent;
        realized.Button.BorderThickness = 0f;
        Canvas.SetLeft(realized.Button, realized.Source.X);
        Canvas.SetTop(realized.Button, realized.Source.Y);

        realized.Content.Root.Width = NodeWidth + PortRadius;
        realized.Content.Root.Height = NodeHeight;
        Canvas.SetLeft(realized.Content.Chrome, 0f);
        Canvas.SetTop(realized.Content.Chrome, 0f);
        realized.Content.Chrome.Width = NodeWidth;
        realized.Content.Chrome.Height = NodeHeight;
        realized.Content.Chrome.Background = new SolidColorBrush(source.IsSelected ? NodeSelectedBackground : NodeBackground);
        realized.Content.Chrome.BorderBrush = new SolidColorBrush(source.IsSelected ? NodeSelectedBorder : NodeBorder);
        realized.Content.LeftAccent.Width = NodeAccentWidth;
        realized.Content.LeftAccent.Height = NodeAccentHeight;
        Canvas.SetLeft(realized.Content.LeftAccent, 14f);
        Canvas.SetTop(realized.Content.LeftAccent, (NodeHeight - NodeAccentHeight) / 2f);
        realized.Content.Label.FontSize = 14f;
        realized.Content.Label.Width = NodeLabelWidth;
        realized.Content.Label.MaxWidth = NodeLabelWidth;
        realized.Content.Label.Height = 24f;
        realized.Content.Label.Foreground = source.IsSelected ? NodeSelectedText : NodeText;
        Canvas.SetLeft(realized.Content.Label, NodeHorizontalPadding + NodeAccentWidth + 12f);
        Canvas.SetTop(realized.Content.Label, (NodeHeight - 24f) / 2f);
        realized.Content.MenuGlyph.Width = MenuGlyphWidth;
        realized.Content.MenuGlyph.Height = NodeHeight;
        Canvas.SetLeft(realized.Content.MenuGlyph, NodeWidth - NodeHorizontalPadding - MenuGlyphWidth);
        Canvas.SetTop(realized.Content.MenuGlyph, 0f);
        UpdatePort(realized.Content.OutputPort, NodeWidth - PortRadius, (NodeHeight - PortDiameter) / 2f, source.IsSelected);
    }

    private static void UpdatePort(EllipseShape port, float left, float top, bool isSelected)
    {
        port.Width = PortDiameter;
        port.Height = PortDiameter;
        port.Fill = isSelected ? NodeSelectedBackground : NodeBackground;
        port.Stroke = isSelected ? Accent : AccentDim;
        Canvas.SetLeft(port, left);
        Canvas.SetTop(port, top);
    }

    private void UpdateEmptyStateLayout()
    {
        if (_emptyStateText == null)
        {
            return;
        }

        _emptyStateText.FontSize = 14f;
        Canvas.SetLeft(_emptyStateText, 28f);
        Canvas.SetTop(_emptyStateText, 28f);
    }

    private Vector2 GetHierarchyViewportSize()
    {
        var width = HierarchyScrollViewer.ViewportWidth;
        var height = HierarchyScrollViewer.ViewportHeight;
        if (width <= 0f || height <= 0f)
        {
            var slot = HierarchyScrollViewer.LayoutSlot;
            var padding = HierarchyScrollViewer.Padding;
            width = MathF.Max(0f, slot.Width - padding.Horizontal);
            height = MathF.Max(0f, slot.Height - padding.Vertical);
        }

        return new Vector2(width, height);
    }

    internal static HierarchyZoomLayout CalculateZoomLayout(
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
        return new HierarchyZoomLayout(
            safeZoom,
            logicalWidth,
            logicalHeight,
            canvasWidth,
            canvasHeight,
            MathF.Max(0f, (canvasWidth - scaledWidth) / 2f),
            MathF.Max(0f, (canvasHeight - scaledHeight) / 2f));
    }

    private static bool AreZoomLayoutsClose(HierarchyZoomLayout left, HierarchyZoomLayout right)
    {
        return AreLayoutValuesClose(left.Zoom, right.Zoom) &&
               AreLayoutValuesClose(left.LogicalWidth, right.LogicalWidth) &&
               AreLayoutValuesClose(left.LogicalHeight, right.LogicalHeight) &&
               AreLayoutValuesClose(left.CanvasWidth, right.CanvasWidth) &&
               AreLayoutValuesClose(left.CanvasHeight, right.CanvasHeight) &&
               AreLayoutValuesClose(left.LeftOffset, right.LeftOffset) &&
               AreLayoutValuesClose(left.TopOffset, right.TopOffset);
    }

    private static bool AreLayoutValuesClose(float left, float right)
    {
        return MathF.Abs(left - right) <= 0.01f;
    }

    private sealed record GraphNode(DesignerVisualTreeNodeViewModel Source, float X, float Y);

    private sealed record GraphConnector(float FromX, float FromY, float ToX, float ToY);

    private sealed record RealizedConnector(GraphConnector Source, PathShape Element);

    private sealed record RealizedNode(GraphNode Source, Button Button, NodeVisualContent Content);

    private sealed record NodeVisualContent(
        Canvas Root,
        Border Chrome,
        Border LeftAccent,
        TextBlock Label,
        MenuDotsCanvas MenuGlyph,
        EllipseShape OutputPort);

    private sealed class MenuDotsCanvas : Canvas
    {
        private const float DotSize = 2f;
        private const float DotGap = 4f;

        protected override void OnRender(SpriteBatch spriteBatch)
        {
            base.OnRender(spriteBatch);
            var slot = LayoutSlot;
            var x = slot.X + ((slot.Width - DotSize) / 2f);
            var startY = slot.Y + ((slot.Height - ((DotSize * 3f) + (DotGap * 2f))) / 2f);
            for (var i = 0; i < 3; i++)
            {
                UiDrawing.DrawFilledRect(
                    spriteBatch,
                    new LayoutRect(x, startY + (i * (DotSize + DotGap)), DotSize, DotSize),
                    NodeMutedGlyph,
                    Opacity);
            }
        }
    }

    internal sealed class GraphLayerCanvas : Canvas
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
            var startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            base.OnRender(spriteBatch);
            _renderCallCount++;
            _renderElapsedTicks += System.Diagnostics.Stopwatch.GetTimestamp() - startTicks;
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

        internal DesignerHierarchyGraphLayerRuntimeDiagnosticsSnapshot GetDesignerHierarchyGraphLayerSnapshotForDiagnostics()
        {
            return new DesignerHierarchyGraphLayerRuntimeDiagnosticsSnapshot(
                _zoom,
                MathF.Abs(_zoom - 1f) >= 0.0001f,
                LayoutSlot.X,
                LayoutSlot.Y,
                LayoutSlot.Width,
                LayoutSlot.Height,
                _renderCallCount,
                _renderElapsedTicks * 1000d / System.Diagnostics.Stopwatch.Frequency,
                _localRenderTransformCallCount,
                _localRenderTransformActiveCount);
        }
    }

    internal readonly record struct HierarchyZoomLayout(
        float Zoom,
        float LogicalWidth,
        float LogicalHeight,
        float CanvasWidth,
        float CanvasHeight,
        float LeftOffset,
        float TopOffset);
}

internal readonly record struct DesignerHierarchyGraphLayerRuntimeDiagnosticsSnapshot(
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

public sealed class DesignerHierarchyWorkspaceCanvas : Canvas, IScrollTransformContent, IScrollViewerMeasureConstraintProvider
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

    public DesignerHierarchyWorkspaceCanvas()
    {
        ScrollViewer.SetIsTransformContentLayerStable(this, true);
    }

    internal float LogicalExtentWidth => _logicalExtentWidth;

    internal float LogicalExtentHeight => _logicalExtentHeight;

    internal float TransformedExtentWidth => _transformedExtentWidth;

    internal float TransformedExtentHeight => _transformedExtentHeight;

    internal float ContentScale => _contentScale;

    internal void ApplyZoomLayout(DesignerHierarchyView.HierarchyZoomLayout layout)
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
