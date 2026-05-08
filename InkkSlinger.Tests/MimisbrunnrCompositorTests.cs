using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class MimisbrunnrCompositorTests
{
    [Fact]
    public void Compositor_UsesTransformStackForMetadataOnlyPanWithoutRerecordingContent()
    {
        var root = CreateRoot();
        var parent = new Panel { Name = "translatedParent" };
        parent.SetLayoutSlot(new LayoutRect(0f, 0f, 80f, 80f));
        var child = CreateRecordingElement("translatedChild", new LayoutRect(10f, 10f, 20f, 20f), Color.Red);
        parent.AddChild(child);
        root.AddChild(parent);

        var uiRoot = BuildAndRecord(root);
        uiRoot.GetTelemetryAndReset();

        parent.RenderTransform = new TranslateTransform { X = 100f, Y = 0f };
        uiRoot.SynchronizeRetainedRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();

        var oldClipOrder = uiRoot.GetRetainedDrawOrderForClipForTests(new LayoutRect(10f, 10f, 20f, 20f));
        var translatedClipOrder = uiRoot.GetRetainedDrawOrderForClipForTests(new LayoutRect(110f, 10f, 20f, 20f));
        var telemetry = uiRoot.GetRetainedRenderControllerTelemetrySnapshotForTests();

        Assert.DoesNotContain(child, oldClipOrder);
        Assert.Contains(child, translatedClipOrder);
        Assert.Equal(0, telemetry.VisualRecordRebuildCount);
        Assert.Equal(0, telemetry.VisualRecordReuseCount);
        Assert.Equal(1, telemetry.TransformMetadataUpdateCount);
        Assert.True(telemetry.CompositionTransformPushCount >= 1);
    }

    [Fact]
    public void Compositor_MaintainsNestedClipAndOpacityStacks()
    {
        var root = CreateRoot();
        var outer = new Panel
        {
            Name = "outer",
            ClipToBounds = true,
            Opacity = 0.5f
        };
        outer.SetLayoutSlot(new LayoutRect(10f, 10f, 100f, 100f));
        var inner = new Panel
        {
            Name = "inner",
            ClipToBounds = true,
            Opacity = 0.5f
        };
        inner.SetLayoutSlot(new LayoutRect(20f, 20f, 50f, 50f));
        var child = CreateRecordingElement("inside", new LayoutRect(25f, 25f, 10f, 10f), Color.Blue);
        inner.AddChild(child);
        outer.AddChild(inner);
        root.AddChild(outer);

        var uiRoot = BuildAndRecord(root);
        uiRoot.GetTelemetryAndReset();

        var metrics = uiRoot.GetRetainedTraversalMetricsForClipForTests(new LayoutRect(25f, 25f, 10f, 10f));
        var telemetry = uiRoot.GetRetainedRenderControllerTelemetrySnapshotForTests();

        Assert.Equal(2, metrics.LocalClipPushCount);
        Assert.True(telemetry.CompositionOpacityPushCount >= 2);
        Assert.Contains(child, uiRoot.GetRetainedDrawOrderForClipForTests(new LayoutRect(25f, 25f, 10f, 10f)));
    }

    [Fact]
    public void Compositor_CullsSubtreesOutsideDirtyRegionClip()
    {
        var root = CreateRoot();
        var left = new Panel { Name = "left" };
        left.SetLayoutSlot(new LayoutRect(0f, 0f, 80f, 80f));
        var leftChild = CreateRecordingElement("leftChild", new LayoutRect(10f, 10f, 20f, 20f), Color.Red);
        left.AddChild(leftChild);

        var right = new Panel { Name = "right" };
        right.SetLayoutSlot(new LayoutRect(160f, 0f, 80f, 80f));
        var rightChild = CreateRecordingElement("rightChild", new LayoutRect(170f, 10f, 20f, 20f), Color.Green);
        right.AddChild(rightChild);

        root.AddChild(left);
        root.AddChild(right);

        var uiRoot = BuildAndRecord(root);
        uiRoot.GetTelemetryAndReset();

        var order = uiRoot.GetRetainedDrawOrderForClipForTests(new LayoutRect(160f, 0f, 80f, 80f));
        var metrics = uiRoot.GetRetainedTraversalMetricsForClipForTests(new LayoutRect(160f, 0f, 80f, 80f));
        var telemetry = uiRoot.GetRetainedRenderControllerTelemetrySnapshotForTests();

        Assert.DoesNotContain(leftChild, order);
        Assert.Contains(rightChild, order);
        Assert.True(metrics.NodesVisited < uiRoot.GetCompositionGraphForTests().NodeCount);
        Assert.True(telemetry.CompositionSubtreeCullCount >= 1);
    }

    [Fact]
    public void Compositor_DryRunClassifiesUnsupportedRecordsAsExplicitFallbacks()
    {
        var root = CreateRoot();
        var textBlock = new TextBlock
        {
            Text = "unsupported text",
            Foreground = Color.White
        };
        textBlock.SetLayoutSlot(new LayoutRect(10f, 10f, 120f, 20f));
        textBlock.Arrange(textBlock.LayoutSlot);
        root.AddChild(textBlock);

        var uiRoot = BuildAndRecord(root);
        uiRoot.GetTelemetryAndReset();

        _ = uiRoot.GetRetainedTraversalMetricsForClipForTests(new LayoutRect(0f, 0f, 200f, 200f));
        var telemetry = uiRoot.GetRetainedRenderControllerTelemetrySnapshotForTests();

        Assert.True(telemetry.CommandReplayFallbackCount >= 1);
        Assert.Equal(1, telemetry.UnsupportedCommandFallbackCount);
    }

    [Fact]
    public void ExplicitBitmapCacheMode_IsInternalBoundaryAfterNormalCompositorTraversal()
    {
        var root = CreateRoot();
        var cached = new Border
        {
            Name = "cachedBoundary",
            Background = new SolidColorBrush(Color.Red),
            RetainedCompositionCacheMode = RetainedCompositionCacheMode.Bitmap
        };
        cached.SetLayoutSlot(new LayoutRect(10f, 10f, 30f, 30f));
        root.AddChild(cached);

        var uiRoot = BuildAndRecord(root);
        var before = uiRoot.GetCompositionGraphForTests().Nodes[1];
        uiRoot.GetTelemetryAndReset();

        var order = uiRoot.GetRetainedDrawOrderForClipForTests(new LayoutRect(0f, 0f, 100f, 100f));
        var telemetry = uiRoot.GetRetainedRenderControllerTelemetrySnapshotForTests();

        Assert.Contains(cached, order);
        Assert.Equal(RetainedCompositionCacheMode.Bitmap, before.CacheMode);
        Assert.Equal(1, telemetry.CacheModeBoundaryCount);
        Assert.Equal(1, telemetry.BitmapCacheBoundaryCount);
        Assert.Equal(1, telemetry.DeferredBitmapCacheBoundaryCount);
        Assert.True(telemetry.CommandReplayCount >= 1);
    }

    [Fact]
    public void ExplicitBitmapCacheKey_TracksContentAndBoundsButNotTransformMetadata()
    {
        var root = CreateRoot();
        var cached = new Border
        {
            Name = "cachedKey",
            Background = new SolidColorBrush(Color.Red),
            RetainedCompositionCacheMode = RetainedCompositionCacheMode.Bitmap
        };
        cached.SetLayoutSlot(new LayoutRect(10f, 10f, 30f, 30f));
        root.AddChild(cached);

        var uiRoot = BuildAndRecord(root);
        var before = uiRoot.GetCompositionGraphForTests().Nodes[1];

        cached.RenderTransform = new TranslateTransform { X = 50f, Y = 0f };
        uiRoot.NotifyDirectRenderInvalidation(cached, RenderInvalidationKind.Transform);
        uiRoot.SynchronizeRetainedRenderListForTests();
        var afterTransform = uiRoot.GetCompositionGraphForTests().Nodes[1];

        Assert.NotEqual(before.MetadataVersion, afterTransform.MetadataVersion);
        Assert.Equal(before.CacheKey, afterTransform.CacheKey);
        cached.ClearRenderInvalidationRecursive();

        cached.Background = new SolidColorBrush(Color.Blue);
        uiRoot.NotifyDirectRenderInvalidation(cached, RenderInvalidationKind.Content);
        uiRoot.SynchronizeRetainedRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();
        var afterContent = uiRoot.GetCompositionGraphForTests().Nodes[1];

        Assert.NotEqual(before.CacheKey.SubtreeContentVersion, afterContent.CacheKey.SubtreeContentVersion);
        Assert.Equal(before.CacheKey.StructureVersion, afterContent.CacheKey.StructureVersion);
        cached.ClearRenderInvalidationRecursive();

        cached.SetLayoutSlot(new LayoutRect(10f, 10f, 40f, 30f));
        uiRoot.NotifyDirectRenderInvalidation(cached, RenderInvalidationKind.Bounds);
        uiRoot.SynchronizeRetainedRenderListForTests();
        var afterBounds = uiRoot.GetCompositionGraphForTests().Nodes[1];

        Assert.NotEqual(afterContent.CacheKey.Bounds, afterBounds.CacheKey.Bounds);
        Assert.Equal(0, afterBounds.CacheKey.DeviceWidth);
        Assert.Equal(0, afterBounds.CacheKey.DeviceHeight);
    }

    private static UiRoot BuildAndRecord(UIElement root)
    {
        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();
        return uiRoot;
    }

    private static Panel CreateRoot()
    {
        var root = new Panel { Name = "root" };
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 300f, 200f));
        return root;
    }

    private static RecordingElement CreateRecordingElement(string name, LayoutRect slot, Color color)
    {
        var element = new RecordingElement(name, color);
        element.SetLayoutSlot(slot);
        return element;
    }

    private sealed class RecordingElement : UIElement
    {
        private readonly Color _color;

        public RecordingElement(string name, Color color)
        {
            Name = name;
            _color = color;
        }

        public string Name { get; }

        internal override void RecordVisual(VisualRecordingContext context)
        {
            context.DrawFilledRect(new LayoutRect(0f, 0f, LayoutSlot.Width, LayoutSlot.Height), _color);
        }
    }
}
