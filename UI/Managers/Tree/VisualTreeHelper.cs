using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Linq;
using Microsoft.Xna.Framework;
using InkkSlinger.UI.Telemetry;

namespace InkkSlinger;

public static class VisualTreeHelper
{
    private static readonly bool EnableHitTestTrace = false;
    private static int _itemsPresenterNeighborProbeCount;
    private static int _itemsPresenterFullFallbackCount;
    private static int _legacyEnumerableFallbackCount;
    private static int _monotonicPanelFastPathCount;
    private static int _simpleSlotHitCount;
    private static int _transformedBoundsHitCount;
    private static int _clipRejectCount;
    private static int _visibilityRejectCount;
    private static int _panelTraversalCount;
    private static int _visualTraversalZSortCount;
    private static readonly ConditionalWeakTable<Panel, PanelMonotonicCacheEntry> PanelMonotonicCache = new();
    public static UIElement? HitTest(UIElement root, Vector2 position)
    {
        return HitTestCore(
            root,
            position,
            0f,
            0f,
            collector: null,
            depth: 0,
            ancestorTransformToRoot: Matrix.Identity,
            hasAncestorTransformToRoot: false,
            rootToAncestorInverse: Matrix.Identity,
            hasRootToAncestorInverse: false,
                hasClipInAncestry: false,
                acceptancePredicate: null);
    }

            public static UIElement? HitTest(UIElement root, Vector2 position, Func<UIElement, bool> acceptancePredicate)
            {
            return HitTestCore(
                root,
                position,
                0f,
                0f,
                collector: null,
                depth: 0,
                ancestorTransformToRoot: Matrix.Identity,
                hasAncestorTransformToRoot: false,
                rootToAncestorInverse: Matrix.Identity,
                hasRootToAncestorInverse: false,
                hasClipInAncestry: false,
                acceptancePredicate);
    }

    public static UIElement? HitTest(UIElement root, Vector2 position, out HitTestMetrics metrics)
    {
        var collector = new HitTestMetricsCollector();
        var hitTestStart = Stopwatch.GetTimestamp();
        var hit = HitTestCore(
            root,
            position,
            0f,
            0f,
            collector,
            depth: 0,
            ancestorTransformToRoot: Matrix.Identity,
            hasAncestorTransformToRoot: false,
            rootToAncestorInverse: Matrix.Identity,
            hasRootToAncestorInverse: false,
            hasClipInAncestry: false,
            acceptancePredicate: null);
        var totalMs = Stopwatch.GetElapsedTime(hitTestStart).TotalMilliseconds;
        metrics = collector.ToMetrics(totalMs);
        return hit;
    }

    public static UIElement? HitTest(UIElement root, Vector2 position, Func<UIElement, bool> acceptancePredicate, out HitTestMetrics metrics)
    {
        var collector = new HitTestMetricsCollector();
        var hitTestStart = Stopwatch.GetTimestamp();
        var hit = HitTestCore(
            root,
            position,
            0f,
            0f,
            collector,
            depth: 0,
            ancestorTransformToRoot: Matrix.Identity,
            hasAncestorTransformToRoot: false,
            rootToAncestorInverse: Matrix.Identity,
            hasRootToAncestorInverse: false,
            hasClipInAncestry: false,
            acceptancePredicate);
        var totalMs = Stopwatch.GetElapsedTime(hitTestStart).TotalMilliseconds;
        metrics = collector.ToMetrics(totalMs);
        return hit;
    }

    private static UIElement? HitTestCore(
        UIElement root,
        Vector2 position,
        float accumulatedHorizontalOffset,
        float accumulatedVerticalOffset,
        HitTestMetricsCollector? collector,
        int depth,
        Matrix ancestorTransformToRoot,
        bool hasAncestorTransformToRoot,
        Matrix rootToAncestorInverse,
        bool hasRootToAncestorInverse,
        bool hasClipInAncestry,
        Func<UIElement, bool>? acceptancePredicate)
    {
        var nodeStart = collector?.StartNode(root, depth) ?? 0L;
        try
        {
            var hasLocalTransform = root.TryGetLocalRenderTransformSnapshot(out var localTransform, out var localInverseTransform);
            var hasLocalClip = root.TryGetLocalClipSnapshot(out var localClipRect);
            var hasTransformInChain = hasRootToAncestorInverse || hasLocalTransform;
            var hasClipInChain = hasClipInAncestry || hasLocalClip;
            var hitTestStart = EnableHitTestTrace ? Stopwatch.GetTimestamp() : 0L;

            var currentRootToThisInverse = rootToAncestorInverse;
            var hasCurrentRootToThisInverse = hasRootToAncestorInverse;
            if (hasLocalTransform)
            {
                currentRootToThisInverse = hasRootToAncestorInverse
                    ? rootToAncestorInverse * localInverseTransform
                    : localInverseTransform;
                hasCurrentRootToThisInverse = true;
            }

            var nextAncestorTransformToRoot = ancestorTransformToRoot;
            var hasNextAncestorTransformToRoot = hasAncestorTransformToRoot;
            if (hasLocalTransform)
            {
                nextAncestorTransformToRoot = hasAncestorTransformToRoot
                    ? localTransform * ancestorTransformToRoot
                    : localTransform;
                hasNextAncestorTransformToRoot = true;
            }

            if (!root.IsVisible || !root.IsEnabled || !root.IsHitTestVisible)
            {
                collector?.RecordReject("HiddenOrDisabled");
                _visibilityRejectCount++;
                return null;
            }

            if (hasLocalClip)
            {
                var clipRect = hasNextAncestorTransformToRoot
                    ? TransformRect(localClipRect, nextAncestorTransformToRoot)
                    : localClipRect;
                if (!ContainsPoint(clipRect, position))
                {
                    collector?.RecordReject("Clip");
                    _clipRejectCount++;
                    return null;
                }
            }

            var isWithinSelfBounds = true;
            if (root is FrameworkElement frameworkElement)
            {
                var canOverflowToChildren = root is Panel || root is ItemsPresenter || root.GetVisualChildCountForTraversal() > 0;
                var canUseSimpleSlotHit = !hasTransformInChain && !hasClipInChain;
                if (canUseSimpleSlotHit)
                {
                    _simpleSlotHitCount++;
                    collector?.RecordTraversal("SimpleSlotBounds");
                    isWithinSelfBounds = FastBoundsHit(frameworkElement, position, accumulatedHorizontalOffset, accumulatedVerticalOffset);
                }
                else
                {
                    _transformedBoundsHitCount++;
                    collector?.RecordTraversal("TransformedBounds");
                    var probePoint = position;
                    if (MathF.Abs(accumulatedHorizontalOffset) > 0.01f ||
                        MathF.Abs(accumulatedVerticalOffset) > 0.01f)
                    {
                        probePoint = new Vector2(
                            position.X + accumulatedHorizontalOffset,
                            position.Y + accumulatedVerticalOffset);
                    }

                    var transformedBounds = frameworkElement.LayoutSlot;
                    if (hasNextAncestorTransformToRoot)
                    {
                        transformedBounds = TransformRect(transformedBounds, nextAncestorTransformToRoot);
                    }

                    isWithinSelfBounds = ContainsPoint(transformedBounds, probePoint);
                }

                if (!isWithinSelfBounds && !canOverflowToChildren)
                {
                    collector?.RecordReject("OutsideSelfBounds");
                    return null;
                }
            }
            else if (!(isWithinSelfBounds = root.HitTest(position)))
            {
                collector?.RecordReject("LeafHitTestMiss");
                return null;
            }

            var nextHorizontalOffset = accumulatedHorizontalOffset;
            var nextVerticalOffset = accumulatedVerticalOffset;
            if (root is ScrollViewer scrollViewerForOffset)
            {
                nextHorizontalOffset += scrollViewerForOffset.HorizontalOffset;
                nextVerticalOffset += scrollViewerForOffset.VerticalOffset;
            }

            // Hot path: avoid per-node allocations and sorting (ItemsPresenter can have thousands of children).
            if (root is Panel panel)
            {
                _panelTraversalCount++;
                collector?.RecordTraversal("Panel");
                var ordered = panel.GetChildrenOrderedByZIndex();
                if (ordered.Count >= 16 &&
                    TryHitTestMonotonicVerticalPanelChildren(
                        panel,
                        ordered,
                        position,
                        nextHorizontalOffset,
                        nextVerticalOffset,
                        collector,
                        depth,
                        nextAncestorTransformToRoot,
                        hasNextAncestorTransformToRoot,
                        currentRootToThisInverse,
                        hasCurrentRootToThisInverse,
                        hasClipInChain,
                        out var indexedHit))
                {
                    return indexedHit;
                }

                for (var i = ordered.Count - 1; i >= 0; i--)
                {
                    var child = ordered[i];
                    if (!ShouldTraverseChildSubtree(
                            root,
                            child,
                            position,
                            accumulatedHorizontalOffset,
                            accumulatedVerticalOffset,
                            nextHorizontalOffset,
                            nextVerticalOffset,
                            nextAncestorTransformToRoot,
                            hasNextAncestorTransformToRoot,
                            currentRootToThisInverse,
                            hasCurrentRootToThisInverse,
                            hasClipInChain,
                            collector))
                    {
                        continue;
                    }

                    var hit = HitTestCore(
                        child,
                        position,
                        ResolveChildHorizontalOffset(root, child, accumulatedHorizontalOffset, nextHorizontalOffset),
                        ResolveChildVerticalOffset(root, child, accumulatedVerticalOffset, nextVerticalOffset),
                        collector,
                        depth + 1,
                        nextAncestorTransformToRoot,
                        hasNextAncestorTransformToRoot,
                        currentRootToThisInverse,
                        hasCurrentRootToThisInverse,
                        hasClipInChain,
                        acceptancePredicate);
                    if (hit != null)
                    {
                        return hit;
                    }
                }

                return AcceptHitCandidate(root, isWithinSelfBounds, acceptancePredicate);
            }

            if (isWithinSelfBounds &&
                root is ItemsPresenter itemsPresenter &&
                itemsPresenter.TryGetItemContainersForHitTest(out var itemContainers) &&
                itemContainers.Count > 0)
            {
                collector?.RecordTraversal("ItemsPresenter");
                var probeX = position.X + nextHorizontalOffset;
                var probeY = position.Y + nextVerticalOffset;
                var presenterSlot = itemsPresenter.LayoutSlot;
                if (probeY < presenterSlot.Y ||
                    probeY > presenterSlot.Y + presenterSlot.Height ||
                    probeX < presenterSlot.X ||
                    probeX > presenterSlot.X + presenterSlot.Width)
                {
                    return AcceptHitCandidate(root, isWithinSelfBounds, acceptancePredicate);
                }

                var relativeY = probeY - presenterSlot.Y;
                var averageHeight = itemsPresenter.DesiredSize.Y / itemContainers.Count;
                if (!IsFinitePositive(averageHeight))
                {
                    averageHeight = 24f;
                }

                var candidate = (int)(relativeY / averageHeight);
                candidate = Math.Clamp(candidate, 0, itemContainers.Count - 1);

                candidate = FindCandidateIndexByY(itemContainers, probeY, candidate, isMonotonicByY: true);
                candidate = RefineIndexByLayoutSlot(itemContainers, probeY, candidate);

                var hit = HitTestCore(
                    itemContainers[candidate],
                    position,
                    nextHorizontalOffset,
                    nextVerticalOffset,
                    collector,
                    depth + 1,
                    nextAncestorTransformToRoot,
                    hasNextAncestorTransformToRoot,
                    currentRootToThisInverse,
                    hasCurrentRootToThisInverse,
                    hasClipInChain,
                    acceptancePredicate);
                if (hit != null)
                {
                    if (EnableHitTestTrace)
                    {
                        var ms = Stopwatch.GetElapsedTime(hitTestStart).TotalMilliseconds;
                        Console.WriteLine(
                            $"[HitTest.ItemsPresenter] t={Environment.TickCount64} root={root.GetType().Name} items={itemContainers.Count} " +
                            $"pos=({position.X:0.#},{position.Y:0.#}) candidate={candidate} mode=candidate hit={hit.GetType().Name} ms={ms:0.###}");
                    }

                    return hit;
                }

                var scanned = 0;
                var searchLeft = true;
                var searchRight = true;
                var left = candidate - 1;
                var right = candidate + 1;
                while (left >= 0 || right < itemContainers.Count)
                {
                    if (searchLeft && left >= 0)
                    {
                        if (TryGetVerticalRange(itemContainers[left], out _, out var leftBottom) &&
                            probeY > leftBottom)
                        {
                            searchLeft = false;
                        }
                        else
                        {
                            scanned++;
                            _itemsPresenterNeighborProbeCount++;
                            hit = HitTestCore(
                                itemContainers[left],
                                position,
                                nextHorizontalOffset,
                                nextVerticalOffset,
                                collector,
                                depth + 1,
                                nextAncestorTransformToRoot,
                                hasNextAncestorTransformToRoot,
                                currentRootToThisInverse,
                                hasCurrentRootToThisInverse,
                                hasClipInChain,
                                acceptancePredicate);
                            if (hit != null)
                            {
                                if (EnableHitTestTrace)
                                {
                                    var ms = Stopwatch.GetElapsedTime(hitTestStart).TotalMilliseconds;
                                    Console.WriteLine(
                                        $"[HitTest.ItemsPresenter] t={Environment.TickCount64} root={root.GetType().Name} items={itemContainers.Count} " +
                                        $"pos=({position.X:0.#},{position.Y:0.#}) candidate={candidate} mode=fallback scanned={scanned} hit={hit.GetType().Name} ms={ms:0.###}");
                                }

                                return hit;
                            }

                            left--;
                        }
                    }
                    else
                    {
                        searchLeft = false;
                    }

                    if (searchRight && right < itemContainers.Count)
                    {
                        if (TryGetVerticalRange(itemContainers[right], out var rightTop, out _) &&
                            probeY < rightTop)
                        {
                            searchRight = false;
                        }
                        else
                        {
                            scanned++;
                            _itemsPresenterNeighborProbeCount++;
                            hit = HitTestCore(
                                itemContainers[right],
                                position,
                                nextHorizontalOffset,
                                nextVerticalOffset,
                                collector,
                                depth + 1,
                                nextAncestorTransformToRoot,
                                hasNextAncestorTransformToRoot,
                                currentRootToThisInverse,
                                hasCurrentRootToThisInverse,
                                hasClipInChain,
                                acceptancePredicate);
                            if (hit != null)
                            {
                                if (EnableHitTestTrace)
                                {
                                    var ms = Stopwatch.GetElapsedTime(hitTestStart).TotalMilliseconds;
                                    Console.WriteLine(
                                        $"[HitTest.ItemsPresenter] t={Environment.TickCount64} root={root.GetType().Name} items={itemContainers.Count} " +
                                        $"pos=({position.X:0.#},{position.Y:0.#}) candidate={candidate} mode=fallback scanned={scanned} hit={hit.GetType().Name} ms={ms:0.###}");
                                }

                                return hit;
                            }

                            right++;
                        }
                    }
                    else
                    {
                        searchRight = false;
                    }

                    if (!searchLeft && !searchRight)
                    {
                        break;
                    }
                }

                for (var i = 0; i <= left; i++)
                {
                    _itemsPresenterFullFallbackCount++;
                    scanned++;
                    hit = HitTestCore(
                        itemContainers[i],
                        position,
                        nextHorizontalOffset,
                        nextVerticalOffset,
                        collector,
                        depth + 1,
                        nextAncestorTransformToRoot,
                        hasNextAncestorTransformToRoot,
                        currentRootToThisInverse,
                        hasCurrentRootToThisInverse,
                        hasClipInChain,
                        acceptancePredicate);
                    if (hit != null)
                    {
                        if (EnableHitTestTrace)
                        {
                            var ms = Stopwatch.GetElapsedTime(hitTestStart).TotalMilliseconds;
                            Console.WriteLine(
                                $"[HitTest.ItemsPresenter] t={Environment.TickCount64} root={root.GetType().Name} items={itemContainers.Count} " +
                                $"pos=({position.X:0.#},{position.Y:0.#}) candidate={candidate} mode=full-fallback scanned={scanned} hit={hit.GetType().Name} ms={ms:0.###}");
                        }

                        return hit;
                    }
                }

                for (var i = right; i < itemContainers.Count; i++)
                {
                    _itemsPresenterFullFallbackCount++;
                    scanned++;
                    hit = HitTestCore(
                        itemContainers[i],
                        position,
                        nextHorizontalOffset,
                        nextVerticalOffset,
                        collector,
                        depth + 1,
                        nextAncestorTransformToRoot,
                        hasNextAncestorTransformToRoot,
                        currentRootToThisInverse,
                        hasCurrentRootToThisInverse,
                        hasClipInChain,
                        acceptancePredicate);
                    if (hit != null)
                    {
                        if (EnableHitTestTrace)
                        {
                            var ms = Stopwatch.GetElapsedTime(hitTestStart).TotalMilliseconds;
                            Console.WriteLine(
                                $"[HitTest.ItemsPresenter] t={Environment.TickCount64} root={root.GetType().Name} items={itemContainers.Count} " +
                                $"pos=({position.X:0.#},{position.Y:0.#}) candidate={candidate} mode=full-fallback scanned={scanned} hit={hit.GetType().Name} ms={ms:0.###}");
                        }

                        return hit;
                    }
                }

                if (EnableHitTestTrace)
                {
                    var ms = Stopwatch.GetElapsedTime(hitTestStart).TotalMilliseconds;
                    Console.WriteLine(
                        $"[HitTest.ItemsPresenter] t={Environment.TickCount64} root={root.GetType().Name} items={itemContainers.Count} " +
                        $"pos=({position.X:0.#},{position.Y:0.#}) candidate={candidate} mode=fallback scanned={scanned} hit=root ms={ms:0.###}");
                }

                return AcceptHitCandidate(root, isWithinSelfBounds, acceptancePredicate);
            }

            var traversalChildCount = root.GetVisualChildCountForTraversal();
            if (traversalChildCount >= 0)
            {
                if (traversalChildCount == 0)
                {
                    return AcceptHitCandidate(root, isWithinSelfBounds, acceptancePredicate);
                }

                var minZ = int.MaxValue;
                var maxZ = int.MinValue;
                for (var i = 0; i < traversalChildCount; i++)
                {
                    var child = root.GetVisualChildAtForTraversal(i);
                    var z = Panel.GetZIndex(child);
                    minZ = Math.Min(minZ, z);
                    maxZ = Math.Max(maxZ, z);
                }

                if (minZ != maxZ)
                {
                    _visualTraversalZSortCount++;
                    collector?.RecordTraversal("VisualZSort");
                    var orderedIndices = ListPool<TraversalIndexEntry>.Rent();
                    try
                    {
                        for (var i = 0; i < traversalChildCount; i++)
                        {
                            orderedIndices.Add(new TraversalIndexEntry(i, Panel.GetZIndex(root.GetVisualChildAtForTraversal(i))));
                        }

                        orderedIndices.Sort(static (left, right) => CompareTraversalEntries(left, right));
                        for (var i = 0; i < orderedIndices.Count; i++)
                        {
                            var child = root.GetVisualChildAtForTraversal(orderedIndices[i].Index);
                            if (!ShouldTraverseChildSubtree(
                                    root,
                                    child,
                                    position,
                                    accumulatedHorizontalOffset,
                                    accumulatedVerticalOffset,
                                    nextHorizontalOffset,
                                    nextVerticalOffset,
                                    nextAncestorTransformToRoot,
                                    hasNextAncestorTransformToRoot,
                                    currentRootToThisInverse,
                                    hasCurrentRootToThisInverse,
                                    hasClipInChain,
                                    collector))
                            {
                                continue;
                            }

                            var hit = HitTestCore(
                                child,
                                position,
                                ResolveChildHorizontalOffset(root, child, accumulatedHorizontalOffset, nextHorizontalOffset),
                                ResolveChildVerticalOffset(root, child, accumulatedVerticalOffset, nextVerticalOffset),
                                collector,
                                depth + 1,
                                nextAncestorTransformToRoot,
                                hasNextAncestorTransformToRoot,
                                currentRootToThisInverse,
                                hasCurrentRootToThisInverse,
                                hasClipInChain,
                                acceptancePredicate);
                            if (hit != null)
                            {
                                return hit;
                            }
                        }

                        return AcceptHitCandidate(root, isWithinSelfBounds, acceptancePredicate);
                    }
                    finally
                    {
                        ListPool<TraversalIndexEntry>.Return(orderedIndices);
                    }
                }

                for (var i = traversalChildCount - 1; i >= 0; i--)
                {
                    var child = root.GetVisualChildAtForTraversal(i);
                    if (!ShouldTraverseChildSubtree(
                            root,
                            child,
                            position,
                            accumulatedHorizontalOffset,
                            accumulatedVerticalOffset,
                            nextHorizontalOffset,
                            nextVerticalOffset,
                            nextAncestorTransformToRoot,
                            hasNextAncestorTransformToRoot,
                            currentRootToThisInverse,
                            hasCurrentRootToThisInverse,
                            hasClipInChain,
                            collector))
                    {
                        continue;
                    }

                    var hit = HitTestCore(
                        child,
                        position,
                        ResolveChildHorizontalOffset(root, child, accumulatedHorizontalOffset, nextHorizontalOffset),
                        ResolveChildVerticalOffset(root, child, accumulatedVerticalOffset, nextVerticalOffset),
                        collector,
                        depth + 1,
                        nextAncestorTransformToRoot,
                        hasNextAncestorTransformToRoot,
                        currentRootToThisInverse,
                        hasCurrentRootToThisInverse,
                        hasClipInChain,
                        acceptancePredicate);
                    if (hit != null)
                    {
                        return hit;
                    }
                }

                return AcceptHitCandidate(root, isWithinSelfBounds, acceptancePredicate);
            }

            _legacyEnumerableFallbackCount++;
            collector?.RecordTraversal("LegacyEnumerableFallback");
            var childBuffer = ListPool<UIElement>.Rent();
            try
            {
                var minZ = int.MaxValue;
                var maxZ = int.MinValue;

                foreach (var child in root.GetVisualChildren())
                {
                    childBuffer.Add(child);
                    var z = Panel.GetZIndex(child);
                    minZ = Math.Min(minZ, z);
                    maxZ = Math.Max(maxZ, z);
                }

                if (childBuffer.Count == 0)
                {
                    return AcceptHitCandidate(root, isWithinSelfBounds, acceptancePredicate);
                }

                if (minZ != maxZ)
                {
                    childBuffer.Sort(static (left, right) => CompareVisualChildrenByZIndex(left, right));
                    for (var i = 0; i < childBuffer.Count; i++)
                    {
                        var child = childBuffer[i];
                        var hit = HitTestCore(
                            child,
                            position,
                            ResolveChildHorizontalOffset(root, child, accumulatedHorizontalOffset, nextHorizontalOffset),
                            ResolveChildVerticalOffset(root, child, accumulatedVerticalOffset, nextVerticalOffset),
                            collector,
                            depth + 1,
                            nextAncestorTransformToRoot,
                            hasNextAncestorTransformToRoot,
                            currentRootToThisInverse,
                            hasCurrentRootToThisInverse,
                            hasClipInChain,
                            acceptancePredicate);
                        if (hit != null)
                        {
                            return hit;
                        }
                    }

                    return AcceptHitCandidate(root, isWithinSelfBounds, acceptancePredicate);
                }

                for (var i = childBuffer.Count - 1; i >= 0; i--)
                {
                    var child = childBuffer[i];
                    if (!ShouldTraverseChildSubtree(
                            root,
                            child,
                            position,
                            accumulatedHorizontalOffset,
                            accumulatedVerticalOffset,
                            nextHorizontalOffset,
                            nextVerticalOffset,
                            nextAncestorTransformToRoot,
                            hasNextAncestorTransformToRoot,
                            currentRootToThisInverse,
                            hasCurrentRootToThisInverse,
                            hasClipInChain,
                            collector))
                    {
                        continue;
                    }

                    var hit = HitTestCore(
                        child,
                        position,
                        ResolveChildHorizontalOffset(root, child, accumulatedHorizontalOffset, nextHorizontalOffset),
                        ResolveChildVerticalOffset(root, child, accumulatedVerticalOffset, nextVerticalOffset),
                        collector,
                        depth + 1,
                        nextAncestorTransformToRoot,
                        hasNextAncestorTransformToRoot,
                        currentRootToThisInverse,
                        hasCurrentRootToThisInverse,
                        hasClipInChain,
                        acceptancePredicate);
                    if (hit != null)
                    {
                        return hit;
                    }
                }
            }
            finally
            {
                ListPool<UIElement>.Return(childBuffer);
            }

            return AcceptHitCandidate(root, isWithinSelfBounds, acceptancePredicate);
        }
        finally
        {
            collector?.EndNode(root, depth, nodeStart);
        }
    }

    private static bool TryHitTestMonotonicVerticalPanelChildren(
        Panel panel,
        IReadOnlyList<UIElement> children,
        Vector2 position,
        float accumulatedHorizontalOffset,
        float accumulatedVerticalOffset,
        HitTestMetricsCollector? collector,
        int depth,
        Matrix ancestorTransformToRoot,
        bool hasAncestorTransformToRoot,
        Matrix rootToAncestorInverse,
        bool hasRootToAncestorInverse,
        bool hasClipInAncestry,
        out UIElement? hit)
    {
        hit = null;
        if (children.Count == 0)
        {
            return false;
        }

        var isMonotonicByY = IsMonotonicByY(panel, children);
        if (!isMonotonicByY)
        {
            return false;
        }

        _monotonicPanelFastPathCount++;
        var probeY = position.Y + accumulatedVerticalOffset;
        var averageHeight = EstimateAverageItemHeight(children);
        if (!IsFinitePositive(averageHeight))
        {
            averageHeight = 24f;
        }

        var originY = 0f;
        if (TryGetVerticalRange(children[0], out var firstTop, out _))
        {
            originY = firstTop;
        }

        var candidate = (int)((probeY - originY) / averageHeight);
        candidate = Math.Clamp(candidate, 0, children.Count - 1);
        candidate = FindCandidateIndexByY(children, probeY, candidate, isMonotonicByY: true);
        candidate = RefineIndexByLayoutSlot(children, probeY, candidate);

        hit = HitTestCore(
            children[candidate],
            position,
            accumulatedHorizontalOffset,
            accumulatedVerticalOffset,
            collector,
            depth + 1,
            ancestorTransformToRoot,
            hasAncestorTransformToRoot,
            rootToAncestorInverse,
            hasRootToAncestorInverse,
            hasClipInAncestry,
            acceptancePredicate: null);
        if (hit != null)
        {
            return true;
        }

        var searchLower = true;
        var searchUpper = true;
        var lower = candidate - 1;
        var upper = candidate + 1;
        while (lower >= 0 || upper < children.Count)
        {
            if (searchLower && lower >= 0)
            {
                if (TryGetVerticalRange(children[lower], out _, out var lowerBottom) && probeY > lowerBottom)
                {
                    searchLower = false;
                }
                else
                {
                    hit = HitTestCore(
                        children[lower],
                        position,
                        accumulatedHorizontalOffset,
                        accumulatedVerticalOffset,
                        collector,
                        depth + 1,
                        ancestorTransformToRoot,
                        hasAncestorTransformToRoot,
                        rootToAncestorInverse,
                        hasRootToAncestorInverse,
                        hasClipInAncestry,
                        acceptancePredicate: null);
                    if (hit != null)
                    {
                        return true;
                    }

                    lower--;
                }
            }
            else
            {
                searchLower = false;
            }

            if (searchUpper && upper < children.Count)
            {
                if (TryGetVerticalRange(children[upper], out var upperTop, out _) && probeY < upperTop)
                {
                    searchUpper = false;
                }
                else
                {
                    hit = HitTestCore(
                        children[upper],
                        position,
                        accumulatedHorizontalOffset,
                        accumulatedVerticalOffset,
                        collector,
                        depth + 1,
                        ancestorTransformToRoot,
                        hasAncestorTransformToRoot,
                        rootToAncestorInverse,
                        hasRootToAncestorInverse,
                        hasClipInAncestry,
                        acceptancePredicate: null);
                    if (hit != null)
                    {
                        return true;
                    }

                    upper++;
                }
            }
            else
            {
                searchUpper = false;
            }

            if (!searchLower && !searchUpper)
            {
                break;
            }
        }

        return false;
    }

    private static bool ShouldTraverseChildSubtree(
        UIElement parent,
        UIElement child,
        Vector2 position,
        float accumulatedHorizontalOffset,
        float accumulatedVerticalOffset,
        float nextHorizontalOffset,
        float nextVerticalOffset,
        Matrix ancestorTransformToRoot,
        bool hasAncestorTransformToRoot,
        Matrix rootToAncestorInverse,
        bool hasRootToAncestorInverse,
        bool hasClipInAncestry,
        HitTestMetricsCollector? collector)
    {
        if (!child.IsVisible || !child.IsEnabled || !child.IsHitTestVisible)
        {
            collector?.RecordReject("ChildHiddenOrDisabled");
            return false;
        }

        if (child is not FrameworkElement frameworkChild)
        {
            return true;
        }

        var childHorizontalOffset = ResolveChildHorizontalOffset(parent, child, accumulatedHorizontalOffset, nextHorizontalOffset);
        var childVerticalOffset = ResolveChildVerticalOffset(parent, child, accumulatedVerticalOffset, nextVerticalOffset);
        var hasLocalTransform = child.TryGetLocalRenderTransformSnapshot(out var localTransform, out _);
        var hasLocalClip = child.TryGetLocalClipSnapshot(out var localClipRect);
        var hasTransformInChain = hasRootToAncestorInverse || hasLocalTransform;
        var hasClipInChain = hasClipInAncestry || hasLocalClip;

        if (hasLocalClip)
        {
            var childTransformToRoot = ancestorTransformToRoot;
            var hasChildTransformToRoot = hasAncestorTransformToRoot;
            if (hasLocalTransform)
            {
                childTransformToRoot = hasAncestorTransformToRoot
                    ? localTransform * ancestorTransformToRoot
                    : localTransform;
                hasChildTransformToRoot = true;
            }

            var clipRect = hasChildTransformToRoot
                ? TransformRect(localClipRect, childTransformToRoot)
                : localClipRect;
            if (!ContainsPoint(clipRect, position))
            {
                collector?.RecordReject("ChildClip");
                return false;
            }
        }

        var canUseSimpleSlotHit = !hasTransformInChain && !hasClipInChain;
        var isWithinChildBounds = canUseSimpleSlotHit
            ? FastBoundsHit(frameworkChild, position, childHorizontalOffset, childVerticalOffset)
            : ContainsPoint(
                hasLocalTransform && hasAncestorTransformToRoot
                    ? TransformRect(frameworkChild.LayoutSlot, localTransform * ancestorTransformToRoot)
                    : hasLocalTransform
                        ? TransformRect(frameworkChild.LayoutSlot, localTransform)
                        : hasAncestorTransformToRoot
                            ? TransformRect(frameworkChild.LayoutSlot, ancestorTransformToRoot)
                            : frameworkChild.LayoutSlot,
                canUseSimpleSlotHit
                    ? position
                    : new Vector2(position.X + childHorizontalOffset, position.Y + childVerticalOffset));

        if (isWithinChildBounds)
        {
            return true;
        }

        if (MayDescendantsOverflowOutsideBounds(child))
        {
            return true;
        }

        collector?.RecordReject("ChildOutsideSelfBounds");
        return false;
    }

    private static UIElement? AcceptHitCandidate(UIElement element, bool isWithinSelfBounds, Func<UIElement, bool>? acceptancePredicate)
    {
        if (!isWithinSelfBounds)
        {
            return null;
        }

        if (acceptancePredicate == null || acceptancePredicate(element))
        {
            return element;
        }

        return null;
    }

    private static bool MayDescendantsOverflowOutsideBounds(UIElement element)
    {
        if (element is Popup or ContextMenu or AdornerLayer or Adorner)
        {
            return true;
        }

        if (element.TryGetLocalRenderTransformSnapshot(out _, out _))
        {
            return true;
        }

        var childCount = element.GetVisualChildCountForTraversal();
        if (childCount <= 0)
        {
            return false;
        }

        for (var i = 0; i < childCount; i++)
        {
            var child = element.GetVisualChildAtForTraversal(i);
            if (child.TryGetLocalRenderTransformSnapshot(out _, out _))
            {
                return true;
            }

            if (child is Popup or ContextMenu or AdornerLayer or Adorner)
            {
                return true;
            }
        }

        return false;
    }

    private static float EstimateAverageItemHeight(IReadOnlyList<UIElement> children)
    {
        if (children.Count == 0)
        {
            return 0f;
        }

        var firstIndex = -1;
        var lastIndex = -1;
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i] is FrameworkElement)
            {
                firstIndex = i;
                break;
            }
        }

        for (var i = children.Count - 1; i >= 0; i--)
        {
            if (children[i] is FrameworkElement)
            {
                lastIndex = i;
                break;
            }
        }

        if (firstIndex < 0 || lastIndex < 0 || firstIndex > lastIndex)
        {
            return 0f;
        }

        if (children[firstIndex] is not FrameworkElement first || children[lastIndex] is not FrameworkElement last)
        {
            return 0f;
        }

        var span = MathF.Max(0.0001f, (last.LayoutSlot.Y + last.LayoutSlot.Height) - first.LayoutSlot.Y);
        var count = Math.Max(1, lastIndex - firstIndex + 1);
        return span / count;
    }

    private static bool IsFinitePositive(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }

    private static int RefineIndexByLayoutSlot(IReadOnlyList<UIElement> containers, float y, int candidate)
    {
        if (containers[candidate] is not FrameworkElement current)
        {
            return candidate;
        }

        var slot = current.LayoutSlot;
        if (y < slot.Y)
        {
            var index = candidate;
            for (var i = 0; i < 64 && index > 0; i++)
            {
                index--;
                if (containers[index] is FrameworkElement element && y >= element.LayoutSlot.Y)
                {
                    return index;
                }
            }

            return candidate;
        }

        if (y > slot.Y + slot.Height)
        {
            var index = candidate;
            for (var i = 0; i < 64 && index < containers.Count - 1; i++)
            {
                index++;
                if (containers[index] is FrameworkElement element &&
                    y < element.LayoutSlot.Y + element.LayoutSlot.Height)
                {
                    return index;
                }
            }
        }

        return candidate;
    }

    private static int FindCandidateIndexByY(IReadOnlyList<UIElement> containers, float y, int guess, bool isMonotonicByY)
    {
        if (containers.Count == 0)
        {
            return 0;
        }

        guess = Math.Clamp(guess, 0, containers.Count - 1);
        if (!isMonotonicByY)
        {
            return guess;
        }

        var low = 0;
        var high = containers.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            if (!TryGetVerticalRange(containers[middle], out var top, out var bottom))
            {
                return guess;
            }

            if (y < top)
            {
                high = middle - 1;
                continue;
            }

            if (y > bottom)
            {
                low = middle + 1;
                continue;
            }

            return middle;
        }

        return Math.Clamp(low, 0, containers.Count - 1);
    }

    private static bool IsMonotonicByY(Panel panel, IReadOnlyList<UIElement> containers)
    {
        var cache = PanelMonotonicCache.GetOrCreateValue(panel);
        var layoutVersion = panel.LayoutVersionStamp;
        if (cache.LayoutVersion == layoutVersion && cache.ChildCount == containers.Count)
        {
            return cache.IsMonotonic;
        }

        var isMonotonic = IsMonotonicByY(containers);
        cache.LayoutVersion = layoutVersion;
        cache.ChildCount = containers.Count;
        cache.IsMonotonic = isMonotonic;
        return isMonotonic;
    }

    private static bool IsMonotonicByY(IReadOnlyList<UIElement> containers)
    {
        var lastTop = float.NegativeInfinity;
        for (var i = 0; i < containers.Count; i++)
        {
            if (!TryGetVerticalRange(containers[i], out var top, out _))
            {
                return false;
            }

            if (top < lastTop)
            {
                return false;
            }

            lastTop = top;
        }

        return true;
    }

    private static bool TryGetVerticalRange(UIElement element, out float top, out float bottom)
    {
        if (element is FrameworkElement frameworkElement)
        {
            var slot = frameworkElement.LayoutSlot;
            top = slot.Y;
            bottom = slot.Y + slot.Height;
            return true;
        }

        top = 0f;
        bottom = 0f;
        return false;
    }

    private static int CompareTraversalEntries(TraversalIndexEntry left, TraversalIndexEntry right)
    {
        var zIndexComparison = right.ZIndex.CompareTo(left.ZIndex);
        if (zIndexComparison != 0)
        {
            return zIndexComparison;
        }

        return right.Index.CompareTo(left.Index);
    }

    private static int CompareVisualChildrenByZIndex(UIElement left, UIElement right)
    {
        var zIndexComparison = Panel.GetZIndex(right).CompareTo(Panel.GetZIndex(left));
        if (zIndexComparison != 0)
        {
            return zIndexComparison;
        }

        return 0;
    }

    private static LayoutRect TransformRect(LayoutRect rect, Matrix transform)
    {
        if (transform == Matrix.Identity)
        {
            return rect;
        }

        var topLeft = Vector2.Transform(new Vector2(rect.X, rect.Y), transform);
        var topRight = Vector2.Transform(new Vector2(rect.X + rect.Width, rect.Y), transform);
        var bottomLeft = Vector2.Transform(new Vector2(rect.X, rect.Y + rect.Height), transform);
        var bottomRight = Vector2.Transform(new Vector2(rect.X + rect.Width, rect.Y + rect.Height), transform);

        var minX = MathF.Min(MathF.Min(topLeft.X, topRight.X), MathF.Min(bottomLeft.X, bottomRight.X));
        var minY = MathF.Min(MathF.Min(topLeft.Y, topRight.Y), MathF.Min(bottomLeft.Y, bottomRight.Y));
        var maxX = MathF.Max(MathF.Max(topLeft.X, topRight.X), MathF.Max(bottomLeft.X, bottomRight.X));
        var maxY = MathF.Max(MathF.Max(topLeft.Y, topRight.Y), MathF.Max(bottomLeft.Y, bottomRight.Y));

        return new LayoutRect(minX, minY, maxX - minX, maxY - minY);
    }

    private static bool ContainsPoint(LayoutRect rect, Vector2 point)
    {
        if (rect.Width < 0f || rect.Height < 0f)
        {
            return false;
        }

        return point.X >= rect.X &&
               point.X <= rect.X + rect.Width &&
               point.Y >= rect.Y &&
               point.Y <= rect.Y + rect.Height;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ResolveChildHorizontalOffset(
        UIElement parent,
        UIElement child,
        float currentHorizontalOffset,
        float scrolledHorizontalOffset)
    {
        return ShouldApplyScrollViewerOffsetToChild(parent, child)
            ? scrolledHorizontalOffset
            : currentHorizontalOffset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ResolveChildVerticalOffset(
        UIElement parent,
        UIElement child,
        float currentVerticalOffset,
        float scrolledVerticalOffset)
    {
        return ShouldApplyScrollViewerOffsetToChild(parent, child)
            ? scrolledVerticalOffset
            : currentVerticalOffset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ShouldApplyScrollViewerOffsetToChild(UIElement parent, UIElement child)
    {
        if (parent is not ScrollViewer)
        {
            return true;
        }

        if (child is ScrollBar)
        {
            return false;
        }

        return !UsesTransformBasedScrollHitTesting(child);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool UsesTransformBasedScrollHitTesting(UIElement element)
    {
        if (element is IScrollTransformContent)
        {
            return true;
        }

        return element is Panel panel && ScrollViewer.GetUseTransformContentScrolling(panel);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool FastBoundsHit(
        FrameworkElement element,
        Vector2 pointerPosition,
        float accumulatedHorizontalOffset,
        float accumulatedVerticalOffset)
    {
        if (!element.IsVisible || !element.IsEnabled || !element.IsHitTestVisible)
        {
            return false;
        }

        var slot = element.LayoutSlot;
        var probeX = pointerPosition.X + accumulatedHorizontalOffset;
        var probeY = pointerPosition.Y + accumulatedVerticalOffset;
        return probeX >= slot.X &&
               probeX <= slot.X + slot.Width &&
               probeY >= slot.Y &&
               probeY <= slot.Y + slot.Height;
    }

    internal static (int NeighborProbes, int FullFallbackScans) GetItemsPresenterFallbackStatsForTests()
    {
        return (_itemsPresenterNeighborProbeCount, _itemsPresenterFullFallbackCount);
    }

    internal static HitTestInstrumentationSnapshot GetInstrumentationSnapshotForTests()
    {
        return new HitTestInstrumentationSnapshot(
            _itemsPresenterNeighborProbeCount,
            _itemsPresenterFullFallbackCount,
            _legacyEnumerableFallbackCount,
            _monotonicPanelFastPathCount,
            _simpleSlotHitCount,
            _transformedBoundsHitCount,
            _clipRejectCount,
            _visibilityRejectCount,
            _panelTraversalCount,
            _visualTraversalZSortCount);
    }

    internal static void ResetInstrumentationForTests()
    {
        _itemsPresenterNeighborProbeCount = 0;
        _itemsPresenterFullFallbackCount = 0;
        _legacyEnumerableFallbackCount = 0;
        _monotonicPanelFastPathCount = 0;
        _simpleSlotHitCount = 0;
        _transformedBoundsHitCount = 0;
        _clipRejectCount = 0;
        _visibilityRejectCount = 0;
        _panelTraversalCount = 0;
        _visualTraversalZSortCount = 0;
    }

    private static class ListPool<T>
    {
        private const int MaxPoolSize = 64;

        [ThreadStatic]
        private static Stack<List<T>>? _pool;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<T> Rent()
        {
            var pool = _pool;
            if (pool != null && pool.Count > 0)
            {
                return pool.Pop();
            }

            return new List<T>(8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return(List<T> list)
        {
            list.Clear();

            _pool ??= new Stack<List<T>>();
            if (_pool.Count < MaxPoolSize)
            {
                _pool.Push(list);
            }
        }
    }

    private sealed class PanelMonotonicCacheEntry
    {
        public int LayoutVersion;
        public int ChildCount;
        public bool IsMonotonic;
    }

    private readonly record struct TraversalIndexEntry(int Index, int ZIndex);
}

public readonly record struct HitTestMetrics(
    int NodesVisited,
    int MaxDepth,
    double TotalMilliseconds,
    string TopLevelSubtreeSummary,
    string HottestTypeSummary,
    string HottestNodeSummary,
    string TraversalSummary,
    string RejectSummary);

internal sealed class HitTestMetricsCollector
{
    private readonly Dictionary<string, (int Count, long Ticks)> _byType = new();
    private readonly Dictionary<string, long> _topLevelSubtreeTicks = new();
    private readonly Dictionary<string, int> _traversalCounts = new();
    private readonly Dictionary<string, int> _rejectCounts = new();
    private int _nodesVisited;
    private int _maxDepth;
    private long _hottestNodeTicks;
    private string _hottestNodeSummary = "none";

    internal long StartNode(UIElement element, int depth)
    {
        _nodesVisited++;
        _maxDepth = Math.Max(_maxDepth, depth);
        return Stopwatch.GetTimestamp();
    }

    internal void EndNode(UIElement element, int depth, long startTicks)
    {
        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        var key = element.GetType().Name;
        if (_byType.TryGetValue(key, out var entry))
        {
            _byType[key] = (entry.Count + 1, entry.Ticks + elapsedTicks);
        }
        else
        {
            _byType[key] = (1, elapsedTicks);
        }

        var elementSummary = DescribeElement(element);
        if (elapsedTicks > _hottestNodeTicks)
        {
            _hottestNodeTicks = elapsedTicks;
            _hottestNodeSummary = $"{elementSummary}@depth{depth}:{TicksToMs(elapsedTicks):0.###}ms";
        }

        if (depth == 1)
        {
            if (_topLevelSubtreeTicks.TryGetValue(elementSummary, out var ticks))
            {
                _topLevelSubtreeTicks[elementSummary] = ticks + elapsedTicks;
            }
            else
            {
                _topLevelSubtreeTicks[elementSummary] = elapsedTicks;
            }
        }
    }

    internal void RecordTraversal(string kind)
    {
        if (_traversalCounts.TryGetValue(kind, out var count))
        {
            _traversalCounts[kind] = count + 1;
        }
        else
        {
            _traversalCounts[kind] = 1;
        }
    }

    internal void RecordReject(string reason)
    {
        if (_rejectCounts.TryGetValue(reason, out var count))
        {
            _rejectCounts[reason] = count + 1;
        }
        else
        {
            _rejectCounts[reason] = 1;
        }
    }

    internal HitTestMetrics ToMetrics(double totalMs)
    {
        var topLevelSummary = SummarizeTicks(_topLevelSubtreeTicks, limit: 3);
        var hottestTypesSummary = SummarizeTypes(limit: 3);
        var traversalSummary = SummarizeCounts(_traversalCounts, limit: 6);
        var rejectSummary = SummarizeCounts(_rejectCounts, limit: 6);
        return new HitTestMetrics(
            _nodesVisited,
            _maxDepth,
            totalMs,
            topLevelSummary,
            hottestTypesSummary,
            _hottestNodeSummary,
            traversalSummary,
            rejectSummary);
    }

    private string SummarizeTypes(int limit)
    {
        if (_byType.Count == 0)
        {
            return "none";
        }

        var entries = _byType
            .OrderByDescending(static kvp => kvp.Value.Ticks)
            .Take(limit)
            .Select(kvp =>
                $"{kvp.Key}(n={kvp.Value.Count},ms={TicksToMs(kvp.Value.Ticks):0.###})");
        return string.Join(", ", entries);
    }

    private static string SummarizeTicks(Dictionary<string, long> ticksByKey, int limit)
    {
        if (ticksByKey.Count == 0)
        {
            return "none";
        }

        var entries = ticksByKey
            .OrderByDescending(static kvp => kvp.Value)
            .Take(limit)
            .Select(kvp => $"{kvp.Key}={TicksToMs(kvp.Value):0.###}ms");
        return string.Join(", ", entries);
    }

    private static string SummarizeCounts(Dictionary<string, int> countsByKey, int limit)
    {
        if (countsByKey.Count == 0)
        {
            return "none";
        }

        var entries = countsByKey
            .OrderByDescending(static kvp => kvp.Value)
            .Take(limit)
            .Select(kvp => $"{kvp.Key}={kvp.Value}");
        return string.Join(", ", entries);
    }

    private static string DescribeElement(UIElement element)
    {
        if (element is FrameworkElement frameworkElement && !string.IsNullOrWhiteSpace(frameworkElement.Name))
        {
            return $"{element.GetType().Name}#{frameworkElement.Name}";
        }

        if (element is Button button && button.Content is string buttonText && !string.IsNullOrWhiteSpace(buttonText))
        {
            return $"{element.GetType().Name}[{buttonText}]";
        }

        if (element is CalendarDayButton dayButton && !string.IsNullOrWhiteSpace(dayButton.DayText))
        {
            return $"{element.GetType().Name}[{dayButton.DayText}]";
        }

        return element.GetType().Name;
    }

    private static double TicksToMs(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }
}
