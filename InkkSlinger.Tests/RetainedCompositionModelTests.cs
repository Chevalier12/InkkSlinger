using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class RetainedCompositionModelTests
{
    [Fact]
    public void Graph_BuildsPreorderWithParentChildAndSubtreeRanges()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 200f, 200f));

        var first = new Border { Name = "first" };
        first.SetLayoutSlot(new LayoutRect(10f, 10f, 50f, 50f));
        var firstChild = new Border { Name = "firstChild" };
        firstChild.SetLayoutSlot(new LayoutRect(15f, 15f, 10f, 10f));
        first.Child = firstChild;

        var second = new Border { Name = "second" };
        second.SetLayoutSlot(new LayoutRect(100f, 20f, 20f, 20f));

        root.AddChild(first);
        root.AddChild(second);

        var graph = BuildGraph(root);

        Assert.Equal(new UIElement[] { root, first, firstChild, second }, GetVisuals(graph));
        AssertNode(graph.Nodes[0], parentIndex: -1, firstChildIndex: 1, childCount: 2, subtreeStart: 0, subtreeEnd: 4);
        AssertNode(graph.Nodes[1], parentIndex: 0, firstChildIndex: 2, childCount: 1, subtreeStart: 1, subtreeEnd: 3);
        AssertNode(graph.Nodes[2], parentIndex: 1, firstChildIndex: -1, childCount: 0, subtreeStart: 2, subtreeEnd: 3);
        AssertNode(graph.Nodes[3], parentIndex: 0, firstChildIndex: -1, childCount: 0, subtreeStart: 3, subtreeEnd: 4);
        Assert.Equal(new[] { 1, 3 }, GetImmediateChildRootIndices(graph, 0));
    }

    [Fact]
    public void Graph_CapturesBoundsAndMetadataWithoutDrawRecords()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 200f, 200f));
        var child = new Border
        {
            Name = "metadataChild",
            ClipToBounds = true,
            Opacity = 0.5f,
            RenderTransform = new TranslateTransform { X = 25f, Y = 30f }
        };
        child.SetLayoutSlot(new LayoutRect(10f, 10f, 40f, 20f));
        root.AddChild(child);

        var graph = BuildGraph(root);
        var node = graph.Nodes[1];

        Assert.True(node.HasBounds);
        Assert.Equal(new LayoutRect(35f, 40f, 40f, 20f), node.Bounds);
        Assert.True(node.HasSubtreeBounds);
        Assert.Equal(node.Bounds, node.SubtreeBounds);
        Assert.True(node.HasLocalTransform);
        Assert.True(node.HasLocalClip);
        Assert.Equal(new LayoutRect(10f, 10f, 40f, 20f), node.LocalClip);
        Assert.Equal(0.5f, node.Opacity);
        Assert.True(node.IsEffectivelyVisible);

        Assert.DoesNotContain("Texture", string.Join('|', GetPropertyNames(node)));
        Assert.DoesNotContain("RenderTarget", string.Join('|', GetPropertyNames(node)));
        Assert.DoesNotContain("Commands", string.Join('|', GetPropertyNames(node)));
    }

    [Fact]
    public void MetadataChanges_UpdateCompositionNodeWithoutChangingSelfDrawRecord()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 200f, 200f));
        var child = CreateRecordedBorder();
        root.AddChild(child);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();

        var before = uiRoot.GetCompositionGraphForTests().Nodes[1];
        uiRoot.GetTelemetryAndReset();

        child.RenderTransform = new TranslateTransform { X = 15f, Y = 5f };
        uiRoot.NotifyDirectRenderInvalidation(child, RenderInvalidationKind.Transform);
        uiRoot.SynchronizeRetainedRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();

        var afterTransform = uiRoot.GetCompositionGraphForTests().Nodes[1];
        Assert.Equal(before.ContentVersion, afterTransform.ContentVersion);
        Assert.NotEqual(before.MetadataVersion, afterTransform.MetadataVersion);
        Assert.Equal(0, uiRoot.GetRetainedRenderControllerTelemetrySnapshotForTests().VisualRecordRebuildCount);

        uiRoot.GetTelemetryAndReset();
        child.Opacity = 0.25f;
        uiRoot.NotifyDirectRenderInvalidation(child, RenderInvalidationKind.Opacity);
        uiRoot.SynchronizeRetainedRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();

        var afterOpacity = uiRoot.GetCompositionGraphForTests().Nodes[1];
        Assert.Equal(before.ContentVersion, afterOpacity.ContentVersion);
        Assert.NotEqual(afterTransform.MetadataVersion, afterOpacity.MetadataVersion);
        Assert.Equal(0, uiRoot.GetRetainedRenderControllerTelemetrySnapshotForTests().VisualRecordRebuildCount);

        uiRoot.GetTelemetryAndReset();
        child.ClipToBounds = true;
        uiRoot.NotifyDirectRenderInvalidation(child, RenderInvalidationKind.Clip);
        uiRoot.SynchronizeRetainedRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();

        var afterClip = uiRoot.GetCompositionGraphForTests().Nodes[1];
        Assert.Equal(before.ContentVersion, afterClip.ContentVersion);
        Assert.NotEqual(afterOpacity.MetadataVersion, afterClip.MetadataVersion);
        Assert.Equal(0, uiRoot.GetRetainedRenderControllerTelemetrySnapshotForTests().VisualRecordRebuildCount);
    }

    private static RetainedCompositionGraph BuildGraph(UIElement root)
    {
        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        return uiRoot.GetCompositionGraphForTests();
    }

    private static Border CreateRecordedBorder()
    {
        var border = new Border
        {
            Name = "recordedMetadataChild",
            Background = new SolidColorBrush(Color.Red),
            BorderBrush = new SolidColorBrush(Color.White),
            BorderThickness = new Thickness(1f)
        };
        border.SetLayoutSlot(new LayoutRect(10f, 10f, 20f, 20f));
        return border;
    }

    private static UIElement[] GetVisuals(RetainedCompositionGraph graph)
    {
        var visuals = new UIElement[graph.NodeCount];
        for (var i = 0; i < graph.NodeCount; i++)
        {
            visuals[i] = graph.Nodes[i].Visual;
        }

        return visuals;
    }

    private static int[] GetImmediateChildRootIndices(RetainedCompositionGraph graph, int parentIndex)
    {
        var parent = graph.Nodes[parentIndex];
        var childIndices = new List<int>(parent.ChildCount);
        for (var childIndex = parent.FirstChildIndex; childIndex >= 0 && childIndex < parent.SubtreeEndIndexExclusive;)
        {
            childIndices.Add(childIndex);
            childIndex = graph.Nodes[childIndex].SubtreeEndIndexExclusive;
        }

        return childIndices.ToArray();
    }

    private static IEnumerable<string> GetPropertyNames(RetainedCompositionNode node)
    {
        _ = node;
        foreach (var property in typeof(RetainedCompositionNode).GetProperties())
        {
            yield return property.Name;
        }
    }

    private static void AssertNode(
        RetainedCompositionNode node,
        int parentIndex,
        int firstChildIndex,
        int childCount,
        int subtreeStart,
        int subtreeEnd)
    {
        Assert.Equal(parentIndex, node.ParentIndex);
        Assert.Equal(firstChildIndex, node.FirstChildIndex);
        Assert.Equal(childCount, node.ChildCount);
        Assert.Equal(subtreeStart, node.SubtreeStartIndex);
        Assert.Equal(subtreeEnd, node.SubtreeEndIndexExclusive);
    }
}
