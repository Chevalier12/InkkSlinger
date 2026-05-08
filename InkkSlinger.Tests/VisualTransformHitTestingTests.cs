using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class VisualTransformHitTestingTests
{
    [Fact]
    public void HitTest_UsesAncestorTransformForChildGeometry()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 300f, 300f));

        var transformedParent = new TestTransformPanel();
        transformedParent.SetLayoutSlot(new LayoutRect(0f, 0f, 120f, 120f));
        transformedParent.ConfigureTransform(new Vector2(-40f, 0f));

        var child = new Border();
        child.SetLayoutSlot(new LayoutRect(80f, 10f, 20f, 20f));
        transformedParent.AddChild(child);
        root.AddChild(transformedParent);

        var hit = VisualTreeHelper.HitTest(root, new Vector2(50f, 20f));
        Assert.Same(child, hit);
    }

    [Fact]
    public void HitTest_RejectsPointOutsideAncestorClip_WhenTransformed()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 300f, 300f));

        var transformedParent = new TestTransformPanel();
        transformedParent.SetLayoutSlot(new LayoutRect(0f, 0f, 120f, 120f));
        transformedParent.ConfigureTransform(new Vector2(-40f, 0f));
        transformedParent.ConfigureClip(new LayoutRect(0f, 0f, 30f, 120f), isEnabled: true);

        var child = new Border();
        child.SetLayoutSlot(new LayoutRect(80f, 10f, 20f, 20f));
        transformedParent.AddChild(child);
        root.AddChild(transformedParent);

        var hit = VisualTreeHelper.HitTest(root, new Vector2(50f, 20f));
        Assert.NotSame(child, hit);
    }

    [Fact]
    public void HitTest_ComposesNestedAncestorTransforms_ForZoomedScrolledContent()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 3000f, 3000f));

        var scrollContent = new TestTransformPanel();
        scrollContent.SetLayoutSlot(new LayoutRect(0f, 0f, 3000f, 3000f));
        scrollContent.ConfigureTransform(new Vector2(0f, -663f));

        var zoomLayer = new TestTransformPanel();
        zoomLayer.SetLayoutSlot(new LayoutRect(299f, 68f, 2400f, 3030f));
        zoomLayer.ConfigureTransform(CreateScaleAround(new Vector2(299f, 68f), 0.457f));

        var offscreenAfterScroll = new Border();
        offscreenAfterScroll.SetLayoutSlot(new LayoutRect(1687f, 1083f, 213f, 60f));

        var visibleAfterScroll = new Border();
        visibleAfterScroll.SetLayoutSlot(new LayoutRect(2027f, 2540f, 213f, 60f));

        zoomLayer.AddChild(offscreenAfterScroll);
        zoomLayer.AddChild(visibleAfterScroll);
        scrollContent.AddChild(zoomLayer);
        root.AddChild(scrollContent);

        var visibleCenter = TransformPoint(
            GetCenter(visibleAfterScroll.LayoutSlot),
            zoomLayer.TransformForTests * scrollContent.TransformForTests);

        var hit = VisualTreeHelper.HitTest(root, visibleCenter);

        Assert.Same(visibleAfterScroll, hit);
        Assert.False(offscreenAfterScroll.HitTest(visibleCenter));
    }

    [Fact]
    public void HitTest_ComposesScrollViewerTransformWithNestedZoomLayer()
    {
        var root = new Panel();
        var workspace = new Canvas
        {
            Width = 3000f,
            Height = 3030f
        };
        var zoomLayer = new TestTransformCanvas
        {
            Width = 2400f,
            Height = 3030f
        };
        Canvas.SetLeft(zoomLayer, 299f);
        Canvas.SetTop(zoomLayer, 68f);

        var offscreenAfterScroll = new Border
        {
            Width = 213f,
            Height = 60f
        };
        Canvas.SetLeft(offscreenAfterScroll, 1388f);
        Canvas.SetTop(offscreenAfterScroll, 1015f);

        var visibleAfterScroll = new Border
        {
            Width = 213f,
            Height = 60f
        };
        Canvas.SetLeft(visibleAfterScroll, 1728f);
        Canvas.SetTop(visibleAfterScroll, 2472f);

        zoomLayer.AddChild(offscreenAfterScroll);
        zoomLayer.AddChild(visibleAfterScroll);
        workspace.AddChild(zoomLayer);

        var viewer = new ScrollViewer
        {
            Width = 1280f,
            Height = 820f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = workspace
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 1280, 820, 16);

        zoomLayer.ConfigureTransform(CreateScaleAround(new Vector2(zoomLayer.LayoutSlot.X, zoomLayer.LayoutSlot.Y), 0.457f));
        viewer.ScrollToVerticalOffset(663f);
        RunLayout(uiRoot, 1280, 820, 32);

        Assert.True(visibleAfterScroll.TryGetRenderBoundsInRootSpace(out var visibleBounds));
        var probe = GetCenter(visibleBounds);
        var hit = VisualTreeHelper.HitTest(root, probe, out var metrics);

        Assert.True(
            ReferenceEquals(visibleAfterScroll, hit),
            $"expected visible child; actual={Describe(hit)} probe={probe} visibleBounds={Format(visibleBounds)} " +
            $"offscreenHit={offscreenAfterScroll.HitTest(probe)} visibleHit={visibleAfterScroll.HitTest(probe)} " +
            $"viewerOffset={viewer.VerticalOffset:0.###} metrics={metrics}");
        Assert.False(offscreenAfterScroll.HitTest(probe));
    }

    [Fact]
    public void HitTest_HierarchyLabNode57_DoesNotResolveNode20AfterScrolledZoom()
    {
        var view = new HierarchyLabView();
        var root = new Panel();
        root.AddChild(view);
        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 1280, 820, 16);

        var scrollViewer = Assert.IsType<ScrollViewer>(view.FindName("HierarchyLabScrollViewer"));
        var graphLayer = Assert.IsType<HierarchyLabGraphLayerCanvas>(view.FindName("HierarchyLabGraphLayer"));
        var node20 = Assert.IsType<Button>(view.FindName("HierarchyLabNode20"));
        var node57 = Assert.IsType<Button>(view.FindName("HierarchyLabNode57"));

        SetHierarchyLabZoom(view, 0.457f);
        RunLayout(uiRoot, 1280, 820, 32);
        scrollViewer.ScrollToVerticalOffset(560f);
        RunLayout(uiRoot, 1280, 820, 48);

        Assert.True(node57.TryGetRenderBoundsInRootSpace(out var node57Bounds));
        var probe = GetCenter(node57Bounds);

        var hit = VisualTreeHelper.HitTest(root, probe, out var metrics);

        Assert.True(
            IsDescendantOrSelf(node57, hit),
            $"expected Node57; actual={Describe(hit)} probe={probe} node57Bounds={Format(node57Bounds)} " +
            $"viewerOffset={scrollViewer.VerticalOffset:0.###} extent={scrollViewer.ExtentHeight:0.###} viewport={scrollViewer.ViewportHeight:0.###} " +
            $"node20Hit={node20.HitTest(probe)} node57Hit={node57.HitTest(probe)} metrics={metrics}");
        Assert.False(node20.HitTest(probe));
    }

    [Fact]
    public void HitTest_HierarchyLabNode62_AtBottomMaximumZoom_DoesNotResolveNode52()
    {
        var view = new HierarchyLabView();
        var root = new Panel();
        root.AddChild(view);
        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 1280, 820, 16);

        var scrollViewer = Assert.IsType<ScrollViewer>(view.FindName("HierarchyLabScrollViewer"));
        var node52 = Assert.IsType<Button>(view.FindName("HierarchyLabNode52"));
        var node62 = Assert.IsType<Button>(view.FindName("HierarchyLabNode62"));

        SetHierarchyLabZoom(view, 2.5f);
        RunLayout(uiRoot, 1280, 820, 32);
        Assert.True(node62.TryGetRenderBoundsInRootSpace(out var unscrolledNode62Bounds));
        scrollViewer.ScrollToHorizontalOffset(MathF.Max(0f, GetCenter(unscrolledNode62Bounds).X - 600f));
        scrollViewer.ScrollToVerticalOffset(scrollViewer.ScrollableHeight);
        RunLayout(uiRoot, 1280, 820, 48);

        Assert.True(node62.TryGetRenderBoundsInRootSpace(out var node62Bounds));
        var probe = GetCenter(node62Bounds);

        var hit = VisualTreeHelper.HitTest(root, probe, out var metrics);

        Assert.True(
            IsDescendantOrSelf(node62, hit),
            $"expected Node62; actual={Describe(hit)} probe={probe} node62Bounds={Format(node62Bounds)} " +
            $"viewerOffset={scrollViewer.VerticalOffset:0.###} extent={scrollViewer.ExtentHeight:0.###} viewport={scrollViewer.ViewportHeight:0.###} " +
            $"node52Hit={node52.HitTest(probe)} node62Hit={node62.HitTest(probe)} metrics={metrics}");
        Assert.False(node52.HitTest(probe));
    }

    [Fact]
    public void HitTest_AppStyledHierarchyLabNode62_AtBottomMaximumZoom_DoesNotResolveNode52()
    {
        var previousResources = UiApplication.Current.Resources.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value);
        var previousMergedDictionaries = UiApplication.Current.Resources.MergedDictionaries.ToList();
        try
        {
            TestApplicationResources.LoadDemoAppResources();
            var view = new HierarchyLabView();
            var root = new Panel();
            root.AddChild(view);
            var uiRoot = new UiRoot(root);
            RunLayout(uiRoot, 1280, 820, 16);

            var scrollViewer = Assert.IsType<ScrollViewer>(view.FindName("HierarchyLabScrollViewer"));
            var node52 = Assert.IsType<Button>(view.FindName("HierarchyLabNode52"));
            var node62 = Assert.IsType<Button>(view.FindName("HierarchyLabNode62"));

            SetHierarchyLabZoom(view, 2.5f);
            RunLayout(uiRoot, 1280, 820, 32);
            Assert.True(node62.TryGetRenderBoundsInRootSpace(out var unscrolledNode62Bounds));
            scrollViewer.ScrollToHorizontalOffset(MathF.Max(0f, GetCenter(unscrolledNode62Bounds).X - 600f));
            scrollViewer.ScrollToVerticalOffset(scrollViewer.ScrollableHeight);
            RunLayout(uiRoot, 1280, 820, 48);

            Assert.True(node62.TryGetRenderBoundsInRootSpace(out var node62Bounds));
            var probe = GetCenter(node62Bounds);

            uiRoot.RunInputDeltaForTests(CreatePointerMoveDelta(Vector2.Zero, probe));
            var hovered = uiRoot.GetHoveredElementForDiagnostics();
            var hit = VisualTreeHelper.HitTest(root, probe, out var metrics);

            Assert.True(
                IsDescendantOrSelf(node62, hit),
                $"expected hit Node62; actual={Describe(hit)} probe={probe} node62Bounds={Format(node62Bounds)} " +
                $"viewerOffset={scrollViewer.VerticalOffset:0.###} node52Hit={node52.HitTest(probe)} node62Hit={node62.HitTest(probe)} metrics={metrics}");
            Assert.True(
                IsDescendantOrSelf(node62, hovered),
                $"expected hover Node62; actual={Describe(hovered)} probe={probe} node62Bounds={Format(node62Bounds)} " +
                $"path={uiRoot.LastPointerResolvePathForDiagnostics} node52Hit={node52.HitTest(probe)} node62Hit={node62.HitTest(probe)}");
            Assert.False(node52.IsMouseOver);
            Assert.True(node62.IsMouseOver);
        }
        finally
        {
            TestApplicationResources.Restore(previousResources, previousMergedDictionaries);
        }
    }

    [Fact]
    public void HitTest_ControlsCatalogHierarchyLabNode62_AtBottomMaximumZoom_DoesNotResolveNode52()
    {
        var previousResources = UiApplication.Current.Resources.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value);
        var previousMergedDictionaries = UiApplication.Current.Resources.MergedDictionaries.ToList();
        try
        {
            TestApplicationResources.LoadDemoAppResources();
            var catalog = new ControlsCatalogView();
            catalog.ShowControl("HierarchyLab");

            var root = new Panel();
            root.AddChild(catalog);
            var uiRoot = new UiRoot(root);
            RunLayout(uiRoot, 1280, 820, 16);

            var previewHost = Assert.IsType<ContentControl>(catalog.FindName("PreviewHost"));
            var view = Assert.IsType<HierarchyLabView>(previewHost.Content);
            var scrollViewer = Assert.IsType<ScrollViewer>(view.FindName("HierarchyLabScrollViewer"));
            var node52 = Assert.IsType<Button>(view.FindName("HierarchyLabNode52"));
            var node62 = Assert.IsType<Button>(view.FindName("HierarchyLabNode62"));

            SetHierarchyLabZoom(view, 2.5f);
            RunLayout(uiRoot, 1280, 820, 32);
            Assert.True(node62.TryGetRenderBoundsInRootSpace(out var unscrolledNode62Bounds));
            scrollViewer.ScrollToHorizontalOffset(MathF.Max(0f, GetCenter(unscrolledNode62Bounds).X - 995.575f));
            scrollViewer.ScrollToVerticalOffset(scrollViewer.ScrollableHeight);
            RunLayout(uiRoot, 1280, 820, 48);

            Assert.True(node62.TryGetRenderBoundsInRootSpace(out var node62Bounds));
            var probe = GetCenter(node62Bounds);

            uiRoot.RunInputDeltaForTests(CreatePointerMoveDelta(Vector2.Zero, probe));
            var hovered = uiRoot.GetHoveredElementForDiagnostics();
            var hit = VisualTreeHelper.HitTest(root, probe, out var metrics);

            Assert.True(
                IsDescendantOrSelf(node62, hit),
                $"expected hit Node62; actual={Describe(hit)} probe={probe} node62Bounds={Format(node62Bounds)} " +
                $"node52Bounds={FormatRootBounds(node52)} viewerOffset={scrollViewer.VerticalOffset:0.###} " +
                $"node52Hit={node52.HitTest(probe)} node62Hit={node62.HitTest(probe)} metrics={metrics}");
            Assert.True(
                IsDescendantOrSelf(node62, hovered),
                $"expected hover Node62; actual={Describe(hovered)} probe={probe} path={uiRoot.LastPointerResolvePathForDiagnostics} " +
                $"node52Hit={node52.HitTest(probe)} node62Hit={node62.HitTest(probe)}");
            Assert.False(node52.IsMouseOver);
            Assert.True(node62.IsMouseOver);
        }
        finally
        {
            TestApplicationResources.Restore(previousResources, previousMergedDictionaries);
        }
    }

    [Fact]
    public void HitTest_HierarchyLabPathToGraphLayer_DoesNotRewalkGraphLayerTransformPerChild()
    {
        var view = new HierarchyLabView();
        var root = new Panel();
        root.AddChild(view);
        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 1280, 820, 16);

        var graphLayer = Assert.IsType<HierarchyLabGraphLayerCanvas>(view.FindName("HierarchyLabGraphLayer"));
        var rootNode = Assert.IsType<Button>(view.FindName("HierarchyLabRootNode"));
        var node01 = Assert.IsType<Button>(view.FindName("HierarchyLabNode01"));

        Assert.True(rootNode.TryGetRenderBoundsInRootSpace(out var rootNodeBounds));
        Assert.True(node01.TryGetRenderBoundsInRootSpace(out var node01Bounds));
        Assert.True(graphLayer.TryGetRenderBoundsInRootSpace(out var graphLayerBounds));

        var pathProbe = new Vector2(
            (rootNodeBounds.X + node01Bounds.X + node01Bounds.Width) / 2f,
            (rootNodeBounds.Y + node01Bounds.Y) / 2f);
        var graphProbe = GetCenter(graphLayerBounds);

        var before = graphLayer.GetHierarchyLabGraphLayerSnapshotForDiagnostics().LocalRenderTransformCallCount;
        UIElement? pathHit = null;
        UIElement? graphHit = null;
        for (var i = 0; i < 20; i++)
        {
            pathHit = VisualTreeHelper.HitTest(root, pathProbe);
            graphHit = VisualTreeHelper.HitTest(root, graphProbe);
        }

        var after = graphLayer.GetHierarchyLabGraphLayerSnapshotForDiagnostics().LocalRenderTransformCallCount;
        var transformCalls = after - before;

        Assert.IsType<PathShape>(pathHit);
        Assert.Same(graphLayer, graphHit);
        Assert.True(
            transformCalls <= 80,
            $"expected one graph-layer transform query per transformed hit-test traversal; actual={transformCalls} " +
            $"pathProbe={pathProbe} graphProbe={graphProbe} pathHit={Describe(pathHit)} graphHit={Describe(graphHit)}");
    }

    [Fact]
    public void ScrollLayoutMutation_HierarchyLabNode62_AtBottomMaximumZoom_DoesNotKeepNode52Hover()
    {
        var view = new HierarchyLabView();
        var root = new Panel();
        root.AddChild(view);
        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 1280, 820, 16);

        var scrollViewer = Assert.IsType<ScrollViewer>(view.FindName("HierarchyLabScrollViewer"));
        var node52 = Assert.IsType<Button>(view.FindName("HierarchyLabNode52"));
        var node62 = Assert.IsType<Button>(view.FindName("HierarchyLabNode62"));

        SetHierarchyLabZoom(view, 2.5f);
        RunLayout(uiRoot, 1280, 820, 32);
        Assert.True(node52.TryGetRenderBoundsInRootSpace(out var unscrolledNode52Bounds));
        Assert.True(node62.TryGetRenderBoundsInRootSpace(out var unscrolledNode62Bounds));
        var stationaryPointer = new Vector2(600f, 660f);
        var node62HorizontalOffset = MathF.Max(0f, GetCenter(unscrolledNode62Bounds).X - stationaryPointer.X);
        scrollViewer.ScrollToHorizontalOffset(MathF.Max(0f, GetCenter(unscrolledNode52Bounds).X - stationaryPointer.X));
        scrollViewer.ScrollToVerticalOffset(MathF.Max(0f, GetCenter(unscrolledNode52Bounds).Y - stationaryPointer.Y));
        RunLayout(uiRoot, 1280, 820, 40);
        Assert.True(node52.TryGetRenderBoundsInRootSpace(out unscrolledNode52Bounds));
        var node52Probe = GetCenter(unscrolledNode52Bounds);
        uiRoot.RunInputDeltaForTests(CreatePointerMoveDelta(Vector2.Zero, node52Probe));
        Assert.True(IsDescendantOrSelf(node52, uiRoot.GetHoveredElementForDiagnostics()));

        scrollViewer.ScrollToHorizontalOffset(node62HorizontalOffset);
        scrollViewer.ScrollToVerticalOffset(scrollViewer.ScrollableHeight);
        RunLayout(uiRoot, 1280, 820, 48);

        Assert.True(node62.TryGetRenderBoundsInRootSpace(out var node62Bounds));
        var node62Probe = GetCenter(node62Bounds);

        var hovered = uiRoot.GetHoveredElementForDiagnostics();
        Assert.True(
            IsDescendantOrSelf(node62, hovered),
            $"expected Node62 hover; actual={Describe(hovered)} path={uiRoot.LastPointerResolvePathForDiagnostics} " +
            $"node62Probe={node62Probe} node62Bounds={Format(node62Bounds)} node52Hit={node52.HitTest(node62Probe)} node62Hit={node62.HitTest(node62Probe)}");
    }

    private sealed class TestTransformPanel : Panel
    {
        private bool _hasClip;
        private LayoutRect _clipRect;
        private bool _hasTransform;
        private Matrix _transform = Matrix.Identity;
        private Matrix _inverseTransform = Matrix.Identity;

        public Matrix TransformForTests => _transform;

        public void ConfigureClip(LayoutRect clipRect, bool isEnabled)
        {
            _clipRect = clipRect;
            _hasClip = isEnabled;
            InvalidateVisual();
        }

        public void ConfigureTransform(Vector2 translation)
        {
            _hasTransform = translation != Vector2.Zero;
            _transform = _hasTransform
                ? Matrix.CreateTranslation(translation.X, translation.Y, 0f)
                : Matrix.Identity;
            _inverseTransform = _hasTransform
                ? Matrix.CreateTranslation(-translation.X, -translation.Y, 0f)
                : Matrix.Identity;
            InvalidateVisual();
        }

        public void ConfigureTransform(Matrix transform)
        {
            _transform = transform;
            _hasTransform = transform != Matrix.Identity;
            _inverseTransform = Matrix.Invert(transform);
            InvalidateVisual();
        }

        protected override bool TryGetClipRect(out LayoutRect clipRect)
        {
            clipRect = _clipRect;
            return _hasClip;
        }

        protected override bool TryGetLocalRenderTransform(out Matrix transform, out Matrix inverseTransform)
        {
            transform = _transform;
            inverseTransform = _inverseTransform;
            return _hasTransform;
        }
    }

    private sealed class TestTransformCanvas : Canvas
    {
        private bool _hasTransform;
        private Matrix _transform = Matrix.Identity;
        private Matrix _inverseTransform = Matrix.Identity;

        public void ConfigureTransform(Matrix transform)
        {
            _transform = transform;
            _hasTransform = transform != Matrix.Identity;
            _inverseTransform = Matrix.Invert(transform);
            InvalidateVisual();
        }

        protected override bool TryGetLocalRenderTransform(out Matrix transform, out Matrix inverseTransform)
        {
            transform = _transform;
            inverseTransform = _inverseTransform;
            return _hasTransform;
        }
    }

    private static Matrix CreateScaleAround(Vector2 origin, float scale)
    {
        return Matrix.CreateTranslation(-origin.X, -origin.Y, 0f) *
               Matrix.CreateScale(scale, scale, 1f) *
               Matrix.CreateTranslation(origin.X, origin.Y, 0f);
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width * 0.5f), rect.Y + (rect.Height * 0.5f));
    }

    private static Vector2 TransformPoint(Vector2 point, Matrix transform)
    {
        return Vector2.Transform(point, transform);
    }

    private static string Describe(UIElement? element)
    {
        return element is FrameworkElement frameworkElement
            ? $"{element.GetType().Name}#{frameworkElement.Name}"
            : element?.GetType().Name ?? "null";
    }

    private static string Format(LayoutRect rect)
    {
        return $"{rect.X:0.###},{rect.Y:0.###},{rect.Width:0.###},{rect.Height:0.###}";
    }

    private static string FormatRootBounds(UIElement element)
    {
        return element.TryGetRenderBoundsInRootSpace(out var bounds) ? Format(bounds) : "none";
    }

    private static bool IsDescendantOrSelf(UIElement ancestor, UIElement? candidate)
    {
        for (var current = candidate; current != null; current = current.VisualParent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static InputDelta CreatePointerMoveDelta(Vector2 previous, Vector2 current)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, previous),
            Current = new InputSnapshot(default, default, current),
            PressedKeys = Array.Empty<Microsoft.Xna.Framework.Input.Keys>(),
            ReleasedKeys = Array.Empty<Microsoft.Xna.Framework.Input.Keys>(),
            TextInput = Array.Empty<char>(),
            PointerMoved = true,
            WheelDelta = 0,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void SetHierarchyLabZoom(HierarchyLabView view, float zoom)
    {
        var field = typeof(HierarchyLabView).GetField("_zoom", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(view, zoom);

        var method = typeof(HierarchyLabView).GetMethod("ApplyZoom", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(view, null);
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }
}
