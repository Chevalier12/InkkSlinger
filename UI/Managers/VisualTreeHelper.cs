using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public static class VisualTreeHelper
{
    private static readonly bool EnableHitTestTrace = false;
    private static int _itemsPresenterNeighborProbeCount;
    private static int _itemsPresenterFullFallbackCount;
    public static UIElement? HitTest(UIElement root, Vector2 position)
    {
        return HitTestCore(root, position, 0f, 0f);
    }

    private static UIElement? HitTestCore(UIElement root, Vector2 position, float accumulatedHorizontalOffset, float accumulatedVerticalOffset)
    {
        var hitTestStart = EnableHitTestTrace ? Stopwatch.GetTimestamp() : 0L;
        if (!root.HitTest(position))
        {
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
            var ordered = panel.GetChildrenOrderedByZIndex();
            for (var i = ordered.Count - 1; i >= 0; i--)
            {
                var hit = HitTestCore(ordered[i], position, nextHorizontalOffset, nextVerticalOffset);
                if (hit != null)
                {
                    return hit;
                }
            }

            return root;
        }

        if (root is ItemsPresenter itemsPresenter &&
            itemsPresenter.TryGetItemContainersForHitTest(out var itemContainers) &&
            itemContainers.Count > 0)
        {
            var probeX = position.X + nextHorizontalOffset;
            var probeY = position.Y + nextVerticalOffset;
            var presenterSlot = itemsPresenter.LayoutSlot;
            if (probeY < presenterSlot.Y ||
                probeY > presenterSlot.Y + presenterSlot.Height ||
                probeX < presenterSlot.X ||
                probeX > presenterSlot.X + presenterSlot.Width)
            {
                return root;
            }

            // Items are laid out vertically in order; use an approximate index to avoid scanning the full list.
            var relativeY = probeY - presenterSlot.Y;
            var averageHeight = itemsPresenter.DesiredSize.Y / itemContainers.Count;
            if (!IsFinitePositive(averageHeight))
            {
                averageHeight = 24f;
            }

            var candidate = (int)(relativeY / averageHeight);
            candidate = Math.Clamp(candidate, 0, itemContainers.Count - 1);

            candidate = FindCandidateIndexByY(itemContainers, probeY, candidate);
            candidate = RefineIndexByLayoutSlot(itemContainers, probeY, candidate);

            var hit = HitTestCore(itemContainers[candidate], position, nextHorizontalOffset, nextVerticalOffset);
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

            // Fallback: probe nearest neighbors around the predicted index and prune by Y-range when possible.
            var scanned = 0;
            var monotonicByY = IsMonotonicByY(itemContainers);
            var searchLeft = true;
            var searchRight = true;
            var left = candidate - 1;
            var right = candidate + 1;
            while (left >= 0 || right < itemContainers.Count)
            {
                if (searchLeft && left >= 0)
                {
                    if (monotonicByY &&
                        TryGetVerticalRange(itemContainers[left], out _, out var leftBottom) &&
                        probeY > leftBottom)
                    {
                        searchLeft = false;
                    }
                    else
                    {
                        scanned++;
                        _itemsPresenterNeighborProbeCount++;
                        hit = HitTestCore(itemContainers[left], position, nextHorizontalOffset, nextVerticalOffset);
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
                    if (monotonicByY &&
                        TryGetVerticalRange(itemContainers[right], out var rightTop, out _) &&
                        probeY < rightTop)
                    {
                        searchRight = false;
                    }
                    else
                    {
                        scanned++;
                        _itemsPresenterNeighborProbeCount++;
                        hit = HitTestCore(itemContainers[right], position, nextHorizontalOffset, nextVerticalOffset);
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

            for (var i = 0; i < itemContainers.Count; i++)
            {
                if (i == candidate)
                {
                    continue;
                }

                _itemsPresenterFullFallbackCount++;
                scanned++;
                hit = HitTestCore(itemContainers[i], position, nextHorizontalOffset, nextVerticalOffset);
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
            return root;
        }

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
                return root;
            }

        if (minZ != maxZ)
        {
            // Only sort when ZIndex differs. Most trees have all-zero ZIndex and sorting becomes the dominant cost.
            childBuffer.Sort(static (a, b) => Panel.GetZIndex(b).CompareTo(Panel.GetZIndex(a)));
            for (var i = 0; i < childBuffer.Count; i++)
            {
                var hit = HitTestCore(childBuffer[i], position, nextHorizontalOffset, nextVerticalOffset);
                if (hit != null)
                {
                    return hit;
                    }
                }

                return root;
            }

            // Common case: no ZIndex variance. Iterate in reverse draw order so later children win.
            for (var i = childBuffer.Count - 1; i >= 0; i--)
            {
                var hit = HitTestCore(childBuffer[i], position, nextHorizontalOffset, nextVerticalOffset);
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

        return root;
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

    private static int FindCandidateIndexByY(IReadOnlyList<UIElement> containers, float y, int guess)
    {
        if (containers.Count == 0)
        {
            return 0;
        }

        guess = Math.Clamp(guess, 0, containers.Count - 1);
        if (!IsMonotonicByY(containers))
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

    internal static (int NeighborProbes, int FullFallbackScans) GetItemsPresenterFallbackStatsForTests()
    {
        return (_itemsPresenterNeighborProbeCount, _itemsPresenterFullFallbackCount);
    }

    internal static void ResetInstrumentationForTests()
    {
        _itemsPresenterNeighborProbeCount = 0;
        _itemsPresenterFullFallbackCount = 0;
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
}
