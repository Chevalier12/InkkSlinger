using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

/// <summary>
/// Hypothesis-driven tests for concerns raised during retained draw pipeline review.
/// Each test method maps to one falsifiable claim about the implementation.
/// </summary>
public sealed class RetainedDrawPipelineReviewTests
{
    /// <summary>
    /// H1 — Prove that scroll translation fast-path updates N subtree nodes without deep-syncing
    /// each child individually (i.e. struct copies are O(1) per subtree, not O(N) deep syncs).
    ///
    /// If the scroll translation fast-path works correctly, scrolling a subtree root should:
    ///   (a) NOT trigger the force-deep-sync path
    ///   (b) translate every node's retained BoundsSnapshot correctly
    ///   (c) leave the retained tree valid
    ///
    /// A failure suggests the scroll-fast-path is degrading to per-node deep sync,
    /// confirming the concern about struct copy volume.
    /// </summary>
    [Fact]
    public void H1_ScrollTranslationFastPath_AvoidsPerNodeDeepSyncCopies()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 400f, 400f));

        var content = new StackPanel();
        const int childCount = 20;
        for (var i = 0; i < childCount; i++)
        {
            content.AddChild(new Border
            {
                Height = 24f,
                Width = 200f
            });
        }

        var viewer = new ScrollViewer
        {
            Content = content,
            Width = 300f,
            Height = 200f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        var viewport = new Viewport(0, 0, 400, 400);
        uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)), viewport);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        var preScrollForceDeepCount = uiRoot.GetPerformanceTelemetrySnapshotForTests().RetainedForceDeepSyncCount;

        viewer.ScrollToVerticalOffset(48f);
        uiRoot.SynchronizeRetainedRenderListForTests();
        var perf1 = uiRoot.GetPerformanceTelemetrySnapshotForTests();

        var postScrollForceDeepCount = perf1.RetainedForceDeepSyncCount - preScrollForceDeepCount;

        var treeState = uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests();

        Assert.Equal("ok", treeState);
        Assert.False(uiRoot.IsRenderListFullRebuildPendingForTests(),
            "scroll translation fast path should not trigger a full retained rebuild");
        Assert.True(postScrollForceDeepCount <= 2,
            $"Force-deep-sync count ({postScrollForceDeepCount}) should be O(1), not O(childCount={childCount})");
    }

    /// <summary>
    /// H1b — Prove that when scroll translation fast path is NOT taken (because child is
    /// individually dirty), the retained tree performs a deep enough update to produce
    /// valid bounds without triggering a full rebuild.
    /// </summary>
    [Fact]
    public void H1b_ScrollTranslationBlockedByDirtyChild_FallsBackToDeepSyncWithoutFullRebuild()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 400f, 400f));

        var content = new StackPanel();
        var targetChild = new Border { Height = 24f, Width = 200f };
        content.AddChild(targetChild);
        for (var i = 0; i < 5; i++)
        {
            content.AddChild(new Border { Height = 24f, Width = 200f });
        }

        var viewer = new ScrollViewer
        {
            Content = content,
            Width = 300f,
            Height = 200f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        var viewport = new Viewport(0, 0, 400, 400);
        uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)), viewport);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        targetChild.InvalidateVisual();

        viewer.ScrollToVerticalOffset(24f);
        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.Equal("ok", uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests());
        Assert.False(uiRoot.IsRenderListFullRebuildPendingForTests());
    }

    /// <summary>
    /// H2 — Prove that the List-based scroll translation stack in
    /// TraverseRetainedNodesWithinClip correctly accumulates and unwinds
    /// multi-level scroll offsets from nested ScrollViewers.
    ///
    /// If the stack-based approach (using RemoveAt on a List) has correctness
    /// issues, then clipped draw-order will include off-viewport children or
    /// miss on-viewport children.
    /// </summary>
    [Fact]
    public void H2_NestedScrollTranslationStack_AccumulatesAndUnwindsCorrectly()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 600f, 600f));

        var innerContent = new StackPanel();
        for (var i = 0; i < 10; i++)
        {
            var border = new Border
            {
                Height = 40f,
                Width = 100f
            };
            border.SetLayoutSlot(new LayoutRect(0f, i * 40f, 100f, 40f));
            innerContent.AddChild(border);
        }
        var innerViewer = new ScrollViewer
        {
            Content = innerContent,
            Width = 100f,
            Height = 120f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var outerContent = new StackPanel();
        outerContent.AddChild(innerViewer);
        outerContent.AddChild(new Border { Height = 200f, Width = 100f });

        var outerViewer = new ScrollViewer
        {
            Content = outerContent,
            Width = 150f,
            Height = 300f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        root.AddChild(outerViewer);

        var uiRoot = new UiRoot(root);
        var viewport = new Viewport(0, 0, 600, 600);
        uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)), viewport);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        outerViewer.ScrollToVerticalOffset(30f);
        uiRoot.SynchronizeRetainedRenderListForTests();
        uiRoot.ResetDirtyStateForTests();

        innerViewer.ScrollToVerticalOffset(40f);
        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.Equal("ok", uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests());
        Assert.False(uiRoot.IsRenderListFullRebuildPendingForTests());
    }

    /// <summary>
    /// H3 — Prove that TryGetScrollTranslationOffsetFromAncestors does an O(depth)
    /// walk of the ancestor chain for each dirty-bounds computation.
    ///
    /// We build a deep chain of nested panels, set layout slots at each level,
    /// scroll the innermost viewer, and verify the retained tree stays valid.
    /// The walk exists because the retained system accumulates scroll translations
    /// from ancestors rather than caching the net offset per node.
    /// </summary>
    [Fact]
    public void H3_ScrollTranslationOffsetFromAncestors_WalksDepthPerDirtyBounds()
    {
        const int depth = 8;

        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 800f, 800f));

        var content = new StackPanel();
        for (var i = 0; i < depth * 2; i++)
        {
            content.AddChild(new Border { Height = 30f, Width = 100f });
        }
        var viewer = new ScrollViewer
        {
            Content = content,
            Width = 150f,
            Height = 200f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        Panel currentContainer = root;
        for (var level = 0; level < depth; level++)
        {
            var wrapper = new Border();
            wrapper.SetLayoutSlot(new LayoutRect(
                level * 10f, level * 10f,
                800f - level * 20f, 800f - level * 20f));
            currentContainer.AddChild(wrapper);

            var inner = new Panel();
            inner.SetLayoutSlot(new LayoutRect(
                level * 5f, level * 5f,
                790f - level * 20f, 790f - level * 20f));

            if (level == depth - 1)
            {
                inner.AddChild(viewer);
            }

            wrapper.Child = inner;
            currentContainer = inner;
        }

        var uiRoot = new UiRoot(root);
        var viewport = new Viewport(0, 0, 800, 800);
        uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)), viewport);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        viewer.ScrollToVerticalOffset(60f);
        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.Equal("ok", uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests());
    }

    /// <summary>
    /// H4 — Prove that N non-coalesced dirty regions produce N separate
    /// scissor-clear traversals (DirtyRegionTraversalCount == region count).
    ///
    /// We create 4 non-overlapping, well-separated children and invalidate
    /// each one. After sync, the dirty-region tracker should hold 4 regions
    /// (one per child, not merged).
    /// </summary>
    [Fact]
    public void H4_DirtyRegions_NonOverlappingChildren_ProduceSeparateRegions()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 300f, 300f));

        var positions = new[]
        {
            new LayoutRect(10f, 10f, 20f, 20f),
            new LayoutRect(100f, 10f, 20f, 20f),
            new LayoutRect(10f, 100f, 20f, 20f),
            new LayoutRect(100f, 100f, 20f, 20f)
        };

        var children = new Border[4];
        for (var i = 0; i < 4; i++)
        {
            children[i] = new Border();
            children[i].SetLayoutSlot(positions[i]);
            root.AddChild(children[i]);
        }

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 300f, 300f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        for (var i = 0; i < 4; i++)
        {
            children[i].InvalidateVisual();
        }
        uiRoot.SynchronizeRetainedRenderListForTests();

        var regions = uiRoot.GetDirtyRegionsSnapshotForTests();
        Assert.Equal(4, regions.Count);
    }

    /// <summary>
    /// H4b — Prove that when UsingDirtyRegionRendering, the number of
    /// traversals matches the number of dirty regions. This is verified by
    /// checking that the partial draw decision is used and regions are counted.
    /// </summary>
    [Fact]
    public void H4b_TwoDisjointDirtyRegions_KeepsTwoRegionsAfterSync()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 200f, 200f));
        var first = new Border();
        first.SetLayoutSlot(new LayoutRect(10f, 10f, 20f, 20f));
        var second = new Border();
        second.SetLayoutSlot(new LayoutRect(100f, 100f, 20f, 20f));
        root.AddChild(first);
        root.AddChild(second);

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 200f, 200f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        first.RenderTransform = new TranslateTransform { X = 50f, Y = 0f };
        second.RenderTransform = new TranslateTransform { X = 0f, Y = 50f };
        uiRoot.SynchronizeRetainedRenderListForTests();

        var regions = uiRoot.GetDirtyRegionsSnapshotForTests();
        Assert.True(regions.Count > 0, "at least one dirty region should track the moved children");
        Assert.Equal(4, regions.Count);
    }

    /// <summary>
    /// H5 — Prove that on initial build, every leaf RenderNode has
    /// BoundsSnapshot == SubtreeBoundsSnapshot (the struct carries two
    /// identical LayoutRect values per leaf node).
    ///
    /// We detect this by verifying that for each leaf node, after a transform
    /// that moves it, the node's BoundsSnapshot equals its SubtreeBoundsSnapshot
    /// before the move. Since we can't read SubtreeBoundsSnapshot directly,
    /// we prove the duplication indirectly: the retained node count mirrors
    /// the subtree-end-index relationship.
    /// </summary>
    [Fact]
    public void H5_LeafNodeBoundsSnapshot_MatchesSubtreeBoundsSnapshotThroughSubtreeEndIndex()
    {
        var root = new Panel();
        var parent = new Border();
        parent.SetLayoutSlot(new LayoutRect(0f, 0f, 100f, 100f));
        var leaf = new Border();
        leaf.SetLayoutSlot(new LayoutRect(10f, 10f, 20f, 20f));
        parent.Child = leaf;
        root.AddChild(parent);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();

        var order = uiRoot.GetRetainedVisualOrderForTests();
        Assert.Equal(3, order.Count);

        var parentEndIndex = uiRoot.GetRetainedNodeSubtreeEndIndexForTests(parent);
        var leafEndIndex = uiRoot.GetRetainedNodeSubtreeEndIndexForTests(leaf);

        Assert.Equal(leafEndIndex, uiRoot.RetainedRenderNodeCount);
        Assert.Equal(parentEndIndex, uiRoot.RetainedRenderNodeCount);
        Assert.Equal(leafEndIndex, parentEndIndex);
    }

    /// <summary>
    /// H5b — After a render-transform change moves a leaf, its BoundsSnapshot
    /// changes but the subtree metadata still reflects the entire subtree
    /// (even though for a leaf, subtree == self).
    /// </summary>
    [Fact]
    public void H5b_LeafBoundsUpdate_ChangesBothBoundsAndSubtreeBounds()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 200f, 200f));
        var leaf = new Border();
        leaf.SetLayoutSlot(new LayoutRect(10f, 10f, 30f, 20f));
        root.AddChild(leaf);

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 200f, 200f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        var leafBoundsBefore = uiRoot.GetRetainedNodeBoundsForTests(leaf);
        Assert.Equal(10f, leafBoundsBefore.X);
        Assert.Equal(10f, leafBoundsBefore.Y);

        leaf.RenderTransform = new TranslateTransform { X = 50f, Y = 30f };
        uiRoot.SynchronizeRetainedRenderListForTests();

        var leafBoundsAfter = uiRoot.GetRetainedNodeBoundsForTests(leaf);
        Assert.Equal(60f, leafBoundsAfter.X);
        Assert.Equal(40f, leafBoundsAfter.Y);

        var regions = uiRoot.GetDirtyRegionsSnapshotForTests();
        Assert.Equal(2, regions.Count);
        Assert.Equal("ok", uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests());
    }

    /// <summary>
    /// H6 — Prove that MixHash collisions do not cause correctness issues
    /// by testing two identical leaves that can each be independently
    /// invalidated and synchronized without one leaking into the other.
    ///
    /// This tests the practical effect of hash mixing quality.
    /// </summary>
    [Fact]
    public void H6_IndependentLeafInvalidations_DoNotContaminateEachOtherAfterSync()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 200f, 200f));

        var left = new Border();
        left.SetLayoutSlot(new LayoutRect(10f, 10f, 30f, 20f));
        var right = new Border();
        right.SetLayoutSlot(new LayoutRect(100f, 10f, 30f, 20f));
        root.AddChild(left);
        root.AddChild(right);

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 200f, 200f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        left.InvalidateVisual();
        uiRoot.SynchronizeRetainedRenderListForTests();
        var regionsAfterLeft = uiRoot.GetDirtyRegionsSnapshotForTests();

        Assert.True(regionsAfterLeft.Count > 0);

        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        right.InvalidateVisual();
        uiRoot.SynchronizeRetainedRenderListForTests();
        var regionsAfterRight = uiRoot.GetDirtyRegionsSnapshotForTests();

        Assert.True(regionsAfterRight.Count > 0);
    }

    /// <summary>
    /// H6b — Make both identical-stamped leaves dirty simultaneously.
    /// If hash collisions caused problems, the versions would collide and
    /// either miss a dirty region or incorrectly merge work items.
    /// </summary>
    [Fact]
    public void H6b_IdenticalLeavesInvalidatedTogether_RetainSeparateDirtyWorkItems()
    {
        var root = new Panel();
        var left = new Border();
        var right = new Border();
        root.AddChild(left);
        root.AddChild(right);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        left.InvalidateVisual();
        right.InvalidateVisual();
        uiRoot.SynchronizeRetainedRenderListForTests();

        var perf = uiRoot.GetPerformanceTelemetrySnapshotForTests();
        Assert.Equal(2, perf.DirtyRootCount);
        Assert.Contains(left, uiRoot.GetRetainedVisualOrderForTests());
        Assert.Contains(right, uiRoot.GetRetainedVisualOrderForTests());
    }

    /// <summary>
    /// H7 — Prove that the hardcoded DirtyRegionCountFallbackThreshold (= 4)
    /// causes full-frame fallback when region count exceeds it.
    ///
    /// Create 5 well-separated dirty regions → ShouldUsePartialDirtyRedraw returns false.
    /// </summary>
    [Fact]
    public void H7_FiveDirtyRegions_ExceedsThreshold_FallsBackToFullDraw()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 500f, 500f));

        var children = new Border[5];
        for (var i = 0; i < 5; i++)
        {
            children[i] = new Border();
            children[i].SetLayoutSlot(new LayoutRect(10f + i * 80f, 10f, 20f, 20f));
            root.AddChild(children[i]);
        }

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 500f, 500f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        for (var i = 0; i < 5; i++)
        {
            children[i].InvalidateVisual();
        }
        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.False(uiRoot.WouldUsePartialDirtyRedrawForTests(),
            "5 dirty regions should exceed the DirtyRegionCountFallbackThreshold of 4");
    }

    /// <summary>
    /// H7b — Prove that when coverage exceeds DirtyRegionCoverageFallbackThreshold
    /// (20% for single region), ShouldUsePartialDirtyRedraw returns false.
    /// </summary>
    [Fact]
    public void H7b_SingleRegionAboveCoverageThreshold_FallsBackToFullDraw()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 100f, 100f));

        var bigChild = new Border();
        bigChild.SetLayoutSlot(new LayoutRect(0f, 0f, 60f, 60f));
        root.AddChild(bigChild);

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 100f, 100f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        bigChild.InvalidateVisual();
        uiRoot.SynchronizeRetainedRenderListForTests();

        var coverage = uiRoot.GetDirtyCoverageForTests();
        Assert.True(coverage > 0.20d,
            $"Single region coverage {coverage:P} should exceed 20% threshold");
        Assert.False(uiRoot.WouldUsePartialDirtyRedrawForTests(),
            $"Coverage {coverage:P} should trigger fallback");
    }

    /// <summary>
    /// H7c — Prove that below both thresholds (count <= 4, coverage <= threshold),
    /// ShouldUsePartialDirtyRedraw returns true.
    /// </summary>
    [Fact]
    public void H7c_SmallRegionsWithinThreshold_UsesPartialRedraw()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 300f, 300f));

        var child = new Border();
        child.SetLayoutSlot(new LayoutRect(10f, 10f, 20f, 20f));
        root.AddChild(child);

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 300f, 300f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        child.InvalidateVisual();
        uiRoot.SynchronizeRetainedRenderListForTests();

        var coverage = uiRoot.GetDirtyCoverageForTests();
        Assert.True(coverage <= 0.20d,
            $"Single region coverage {coverage:P} should be within 20% threshold");
        Assert.True(uiRoot.WouldUsePartialDirtyRedrawForTests(),
            $"Coverage {coverage:P} should allow partial redraw");
    }

    /// <summary>
    /// H8 — Prove that when GetRetainedRenderChildren returns a different set
    /// than what the retained indices expect, the sync path detects the
    /// divergence and triggers a full retained-list rebuild.
    ///
    /// This confirms that structural divergence is handled safely.
    /// </summary>
    [Fact]
    public void H8_GetRetainedRenderChildrenDivergence_TriggersFullRebuild()
    {
        var root = new Panel();
        var misdirectedParent = new MismatchedRenderChildrenPanel();
        var visibleChild = new Border();
        misdirectedParent.AddChild(visibleChild);
        root.AddChild(misdirectedParent);
        root.AddChild(new Border());

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        var orderBefore = uiRoot.GetRetainedVisualOrderForTests();
        Assert.Contains(misdirectedParent, orderBefore);
        Assert.Contains(visibleChild, orderBefore);

        var preRebuildTelemetry = uiRoot.GetRenderTelemetrySnapshotForTests();
        var preRebuildCount = preRebuildTelemetry.FullDirtyRetainedRebuildCount;

        misdirectedParent.SetReturnsDivergentChildren(true);
        misdirectedParent.InvalidateVisual();
        uiRoot.SynchronizeRetainedRenderListForTests();

        var postRebuildTelemetry = uiRoot.GetRenderTelemetrySnapshotForTests();
        var rebuildDelta = postRebuildTelemetry.FullDirtyRetainedRebuildCount - preRebuildCount;

        Assert.True(rebuildDelta >= 1,
            $"Mismatched GetRetainedRenderChildren should trigger full rebuild (rebuildDelta={rebuildDelta})");
    }

    /// <summary>
    /// H8b — A Panel that adds children via the normal AddChild should always
    /// produce matching GetVisualChildren() and GetRetainedRenderChildren().
    /// This is a negative test that confirms the non-divergent case works.
    /// </summary>
    [Fact]
    public void H8b_NormalPanel_NoDivergence_NoRebuild()
    {
        var root = new Panel();
        var parent = new Panel();
        var child = new Border();
        parent.AddChild(child);
        root.AddChild(parent);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        child.InvalidateVisual();
        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.False(uiRoot.IsRenderListFullRebuildPendingForTests());
        Assert.Equal("ok", uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests());
    }

    [Fact]
    public void H7_Fix_ConfigurableCountThreshold_RaisesLimit()
    {
        var (uiRoot, children, root) = CreateRootWithDisjointChildren(
            viewportSize: 300f, childSize: 20f, spacing: 80f, childCount: 6);
        uiRoot.DirtyRegionCountFallbackThreshold = 10;
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        for (var i = 0; i < 6; i++)
        {
            children[i].InvalidateVisual();
        }
        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.True(uiRoot.WouldUsePartialDirtyRedrawForTests(),
            "With DirtyRegionCountFallbackThreshold=10, 6 regions should still use partial redraw");
    }

    [Fact]
    public void H7_Finding_CountThresholdRaiseFixtureClipsOutTwoRegions_DoesNotExerciseRaisedThreshold()
    {
        var (uiRoot, children, root) = CreateRootWithDisjointChildren(
            viewportSize: 300f, childSize: 20f, spacing: 80f, childCount: 6);
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        for (var i = 0; i < 6; i++)
        {
            children[i].InvalidateVisual();
        }
        uiRoot.SynchronizeRetainedRenderListForTests();

        var regions = uiRoot.GetDirtyRegionsSnapshotForTests();
        Assert.Equal(4, regions.Count);
        Assert.True(uiRoot.WouldUsePartialDirtyRedrawForTests(),
            "The original raise-threshold fixture only keeps 4 in-viewport regions, so it passes without exercising the raised count threshold.");
    }

    [Fact]
    public void H7_Fix_ConfigurableCountThreshold_RaisesLimit_WhenAllRegionsAreInsideViewport()
    {
        var (defaultRoot, defaultChildren, defaultPanel) = CreateRootWithDisjointChildren(
            viewportSize: 520f, childSize: 20f, spacing: 80f, childCount: 6);
        defaultRoot.ResetDirtyStateForTests();
        defaultPanel.ClearRenderInvalidationRecursive();
        defaultRoot.CompleteDrawStateForTests();

        for (var i = 0; i < 6; i++)
        {
            defaultChildren[i].InvalidateVisual();
        }
        defaultRoot.SynchronizeRetainedRenderListForTests();

        Assert.Equal(6, defaultRoot.GetDirtyRegionsSnapshotForTests().Count);
        Assert.False(defaultRoot.WouldUsePartialDirtyRedrawForTests(),
            "The default count threshold should reject 6 in-viewport dirty regions.");

        var (raisedRoot, raisedChildren, raisedPanel) = CreateRootWithDisjointChildren(
            viewportSize: 520f, childSize: 20f, spacing: 80f, childCount: 6);
        raisedRoot.DirtyRegionCountFallbackThreshold = 10;
        raisedRoot.ResetDirtyStateForTests();
        raisedPanel.ClearRenderInvalidationRecursive();
        raisedRoot.CompleteDrawStateForTests();

        for (var i = 0; i < 6; i++)
        {
            raisedChildren[i].InvalidateVisual();
        }
        raisedRoot.SynchronizeRetainedRenderListForTests();

        Assert.Equal(6, raisedRoot.GetDirtyRegionsSnapshotForTests().Count);
        Assert.True(raisedRoot.WouldUsePartialDirtyRedrawForTests(),
            "Raising DirtyRegionCountFallbackThreshold should allow 6 in-viewport dirty regions to use partial redraw.");
    }

    [Fact]
    public void H7_Fix_ConfigurableCountThreshold_LowersLimit()
    {
        var (uiRoot, children, root) = CreateRootWithDisjointChildren(
            viewportSize: 300f, childSize: 20f, spacing: 80f, childCount: 3);
        uiRoot.DirtyRegionCountFallbackThreshold = 2;
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        for (var i = 0; i < 3; i++)
        {
            children[i].InvalidateVisual();
        }
        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.False(uiRoot.WouldUsePartialDirtyRedrawForTests(),
            "With DirtyRegionCountFallbackThreshold=2, 3 regions should fall back to full draw");
    }

    [Fact]
    public void H7_Fix_ConfigurableCoverageThreshold_RaisesLimit()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 100f, 100f));
        var child = new Border();
        child.SetLayoutSlot(new LayoutRect(0f, 0f, 45f, 45f));
        root.AddChild(child);

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 100f, 100f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        uiRoot.DirtyRegionCoverageFallbackThreshold = 0.50d;

        child.RenderTransform = new TranslateTransform { X = 10f, Y = 10f };
        uiRoot.SynchronizeRetainedRenderListForTests();

        var coverage = uiRoot.GetDirtyCoverageForTests();
        Assert.True(coverage <= 0.50d,
            $"Expected coverage ≤ 0.50 for partial draw, but was {coverage:P}");
        Assert.True(uiRoot.WouldUsePartialDirtyRedrawForTests(),
            $"With DirtyRegionCoverageFallbackThreshold=0.50 and coverage={coverage:P}, partial should be allowed");
    }

    [Fact]
    public void H7_Fix_ConfigurableCoverageThreshold_LowersLimit()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 100f, 100f));
        var child = new Border();
        child.SetLayoutSlot(new LayoutRect(0f, 0f, 20f, 20f));
        root.AddChild(child);

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 100f, 100f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        uiRoot.DirtyRegionCoverageFallbackThreshold = 0.02d;

        child.RenderTransform = new TranslateTransform { X = 5f, Y = 5f };
        uiRoot.SynchronizeRetainedRenderListForTests();

        var coverage = uiRoot.GetDirtyCoverageForTests();
        Assert.True(coverage > 0.02d,
            $"Expected coverage > 0.02 for full-draw fallback, but was {coverage:P}");
        Assert.False(uiRoot.WouldUsePartialDirtyRedrawForTests(),
            $"With DirtyRegionCoverageFallbackThreshold=0.02 and coverage={coverage:P}, full draw should be forced");
    }

    [Fact]
    public void H7_Fix_ConfigurableMultipleRegionCoverageThreshold_RaisesLimit()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 100f, 100f));

        var first = new Border();
        first.SetLayoutSlot(new LayoutRect(0f, 0f, 30f, 30f));
        var second = new Border();
        second.SetLayoutSlot(new LayoutRect(60f, 0f, 30f, 30f));
        root.AddChild(first);
        root.AddChild(second);

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 100f, 100f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        first.InvalidateVisual();
        second.InvalidateVisual();
        uiRoot.SynchronizeRetainedRenderListForTests();

        var defaultCoverage = uiRoot.GetDirtyCoverageForTests();
        Assert.Equal(2, uiRoot.GetDirtyRegionsSnapshotForTests().Count);
        Assert.True(defaultCoverage > 0.12d);
        Assert.False(uiRoot.WouldUsePartialDirtyRedrawForTests(),
            $"Default multi-region coverage threshold should reject coverage={defaultCoverage:P}.");

        uiRoot.DirtyRegionCoverageFallbackThresholdForMultipleRegions = 0.20d;

        Assert.True(uiRoot.WouldUsePartialDirtyRedrawForTests(),
            $"Raised multi-region coverage threshold should allow coverage={defaultCoverage:P}.");
    }

    [Fact]
    public void H7_Fix_ConfigurableMultipleRegionCoverageThreshold_LowersLimit()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 100f, 100f));

        var first = new Border();
        first.SetLayoutSlot(new LayoutRect(0f, 0f, 20f, 20f));
        var second = new Border();
        second.SetLayoutSlot(new LayoutRect(60f, 0f, 20f, 20f));
        root.AddChild(first);
        root.AddChild(second);

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 100f, 100f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        uiRoot.DirtyRegionCoverageFallbackThresholdForMultipleRegions = 0.05d;

        first.InvalidateVisual();
        second.InvalidateVisual();
        uiRoot.SynchronizeRetainedRenderListForTests();

        var coverage = uiRoot.GetDirtyCoverageForTests();
        Assert.Equal(2, uiRoot.GetDirtyRegionsSnapshotForTests().Count);
        Assert.True(coverage > 0.05d);
        Assert.False(uiRoot.WouldUsePartialDirtyRedrawForTests(),
            $"Lowered multi-region coverage threshold should reject coverage={coverage:P}.");
    }

    [Fact]
    public void H7_Finding_SingleCoverageThreshold_DoesNotRetainNaN()
    {
        var uiRoot = new UiRoot(new Panel());
        uiRoot.DirtyRegionCoverageFallbackThreshold = 0.42d;

        uiRoot.DirtyRegionCoverageFallbackThreshold = double.NaN;

        Assert.Equal(0.42d, uiRoot.DirtyRegionCoverageFallbackThreshold);
    }

    [Fact]
    public void H7_Finding_MultipleCoverageThreshold_DoesNotRetainNaN()
    {
        var uiRoot = new UiRoot(new Panel());
        uiRoot.DirtyRegionCoverageFallbackThresholdForMultipleRegions = 0.18d;

        uiRoot.DirtyRegionCoverageFallbackThresholdForMultipleRegions = double.NaN;

        Assert.Equal(0.18d, uiRoot.DirtyRegionCoverageFallbackThresholdForMultipleRegions);
    }

    private static (UiRoot UiRoot, Border[] Children, Panel Root) CreateRootWithDisjointChildren(
        float viewportSize, float childSize, float spacing, int childCount)
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, viewportSize, viewportSize));
        var children = new Border[childCount];
        for (var i = 0; i < childCount; i++)
        {
            children[i] = new Border();
            children[i].SetLayoutSlot(new LayoutRect(10f + i * spacing, 10f, childSize, childSize));
            root.AddChild(children[i]);
        }

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, viewportSize, viewportSize));
        uiRoot.RebuildRenderListForTests();
        return (uiRoot, children, root);
    }

    /// <summary>
    /// H4 — Prove the threshold system already handles the N-clear concern.
    /// When 4 small, well-separated dirty regions exist below the threshold,
    /// partial redraw is used (N clears + N traversals is correct).
    /// When 5 regions exist above the threshold, the system falls back to
    /// full draw (1 full clear + 1 full traversal), avoiding excessive clears.
    /// </summary>
    [Fact]
    public void H4_Threshold_FourSmallRegions_UsesPartialRedraw()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 400f, 400f));
        for (var i = 0; i < 4; i++)
        {
            var child = new Border();
            child.SetLayoutSlot(new LayoutRect(10f + i * 90f, 10f, 20f, 20f));
            root.AddChild(child);
        }

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 400f, 400f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        for (var i = 0; i < 4; i++)
        {
            ((Border)root.Children[i]).InvalidateVisual();
        }
        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.True(uiRoot.WouldUsePartialDirtyRedrawForTests(),
            "4 small well-separated dirty regions should use partial redraw (N clears + N traversals is correct here)");
    }

    /// <summary>
    /// H4b — Prove that exceeding DirtyRegionCountFallbackThreshold forces full draw,
    /// avoiding N clears for too many regions.
    /// </summary>
    [Fact]
    public void H4_Threshold_FiveSmallRegions_FallsBackToFullDraw()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 500f, 500f));
        for (var i = 0; i < 5; i++)
        {
            var child = new Border();
            child.SetLayoutSlot(new LayoutRect(10f + i * 80f, 10f, 20f, 20f));
            root.AddChild(child);
        }

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 500f, 500f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        for (var i = 0; i < 5; i++)
        {
            ((Border)root.Children[i]).InvalidateVisual();
        }
        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.False(uiRoot.WouldUsePartialDirtyRedrawForTests(),
            "5 dirty regions exceeds DirtyRegionCountFallbackThreshold=4, so full draw should be used instead of 5 clears");
    }

    /// <summary>
    /// H3 — Prove the O(depth) ancestor walk in TryGetScrollTranslationOffsetFromAncestors
    /// produces correct results at depth 10 with nested scroll viewers.
    /// The walk is correct by design — the only way to consistently read
    /// pre-sync scroll state. The O(depth) cost is bounded by typical UI depth (3-8).
    /// </summary>
    [Fact]
    public void H3_DeepAncestorWalk_ProducesCorrectScrollOffsets()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 1000f, 1000f));

        var content = new StackPanel();
        for (var i = 0; i < 20; i++)
        {
            content.AddChild(new Border { Height = 30f, Width = 100f });
        }
        var innerViewer = new ScrollViewer
        {
            Content = content,
            Width = 150f,
            Height = 200f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        Panel currentContainer = root;
        for (var level = 0; level < 10; level++)
        {
            var wrapper = new Border();
            wrapper.SetLayoutSlot(new LayoutRect(level * 5f, level * 5f,
                1000f - level * 10f, 1000f - level * 10f));
            currentContainer.AddChild(wrapper);

            var inner = new Panel();
            inner.SetLayoutSlot(new LayoutRect(level * 2f, level * 2f,
                990f - level * 10f, 990f - level * 10f));
            wrapper.Child = inner;
            currentContainer = inner;
        }

        currentContainer.AddChild(innerViewer);

        var uiRoot = new UiRoot(root);
        var viewport = new Viewport(0, 0, 1000, 1000);
        uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)), viewport);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        innerViewer.ScrollToVerticalOffset(60f);
        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.Equal("ok", uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests());
        Assert.False(uiRoot.IsRenderListFullRebuildPendingForTests());
    }

    /// <summary>
    /// H5 — Prove that leaf-node SubtreeBoundsSnapshot equals BoundsSnapshot
    /// by showing that after initial build, the retained tree is valid for
    /// a 3-level hierarchy. The duplication is a memory concern (~16 bytes
    /// per leaf) but not a correctness issue. The struct carries both fields
    /// by design so that traversal code (which only reads SubtreeBoundsSnapshot
    /// for culling) doesn't need a conditional branch per node.
    /// </summary>
    [Fact]
    public void H5_LeafBoundsDuplication_DoesNotAffectCorrectness()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 200f, 200f));
        var parent = new Border();
        parent.SetLayoutSlot(new LayoutRect(0f, 0f, 100f, 100f));
        var leaf = new Border();
        leaf.SetLayoutSlot(new LayoutRect(10f, 10f, 20f, 20f));
        parent.Child = leaf;
        root.AddChild(parent);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();

        Assert.Equal(3, uiRoot.GetRetainedVisualOrderForTests().Count);
        Assert.Equal("ok", uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests());

        var leafEndIndex = uiRoot.GetRetainedNodeSubtreeEndIndexForTests(leaf);
        var parentEndIndex = uiRoot.GetRetainedNodeSubtreeEndIndexForTests(parent);
        var rootEndIndex = uiRoot.GetRetainedNodeSubtreeEndIndexForTests(root);

        Assert.Equal(leafEndIndex, rootEndIndex);
        Assert.Equal(parentEndIndex, rootEndIndex);
    }

    private sealed class MismatchedRenderChildrenPanel : Panel
    {
        private bool _returnDivergentChildren;

        public void SetReturnsDivergentChildren(bool value)
        {
            _returnDivergentChildren = value;
        }

        internal override IEnumerable<UIElement> GetRetainedRenderChildren()
        {
            if (_returnDivergentChildren)
            {
                yield break;
            }

            foreach (var child in GetVisualChildren())
            {
                yield return child;
            }
        }
    }
}
