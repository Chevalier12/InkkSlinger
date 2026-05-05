using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public partial class TreeView
{
    private sealed class VirtualizingTreeDataHost : Panel, IScrollTransformContent, IScrollViewerVirtualizedContent
    {
        private const float FallbackRowHeight = 22f;
        private readonly TreeView _owner;
        private IReadOnlyList<VisibleTreeDataEntry> _rows = Array.Empty<VisibleTreeDataEntry>();
        private readonly List<float> _rowHeights = new();
        private readonly List<bool> _rowHeightMeasured = new();
        private readonly List<float> _rowOffsets = new();
        private bool _rowOffsetsDirty = true;
        private int _firstRealizedIndex = -1;
        private int _lastRealizedIndex = -1;
        private bool _pendingDeferredOffsetRefresh;
        private bool _preserveRowMetricsForNextArrange;
        private Dictionary<int, float>? _preservedThumbSnapshotRowYByIndex;
        private float _averageRowHeight = FallbackRowHeight;
        private float _rowHeightTotal;
        private float _measuredRowHeightTotal;
        private int _measuredRowHeightCount;
        private float _estimatedExtentWidth;
        private readonly Dictionary<object, EstimatedRowWidthCacheEntry> _estimatedRowWidthCache = new();

        public VirtualizingTreeDataHost(TreeView owner)
        {
            _owner = owner;
            Background = Color.Transparent;
        }

        public bool OwnsHorizontalScrollOffset => true;

        public bool OwnsVerticalScrollOffset => true;

        public float AverageRowHeight => _averageRowHeight;

        public int FirstRealizedIndexForDiagnostics => _firstRealizedIndex;

        public int LastRealizedIndexForDiagnostics => _lastRealizedIndex;

        public float TotalExtentHeightForDiagnostics => GetTotalExtentHeight();

        public float MeasuredRowHeightAverageForDiagnostics => _measuredRowHeightCount > 0
            ? _measuredRowHeightTotal / _measuredRowHeightCount
            : FallbackRowHeight;

        public int MeasuredRowHeightCountForDiagnostics => _measuredRowHeightCount;

        public void SetRows(IReadOnlyList<VisibleTreeDataEntry> rows)
        {
            _rows = rows;
            _rowHeights.Clear();
            _rowHeightMeasured.Clear();
            _rowOffsets.Clear();
            _rowHeightTotal = 0f;
            _measuredRowHeightTotal = 0f;
            _measuredRowHeightCount = 0;
            _averageRowHeight = FallbackRowHeight;
            _rowOffsetsDirty = true;
            EnsureRowMetricStorage();
            _firstRealizedIndex = -1;
            _lastRealizedIndex = -1;
            _pendingDeferredOffsetRefresh = false;
            _estimatedExtentWidth = EstimateExtentWidth(rows);
            InvalidateMeasure();
            InvalidateArrange();
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            RealizeRows(availableSize.Y, invalidateMeasureForChildMutations: false, suppressLayoutInvalidations: true);
            var childConstraint = new Vector2(availableSize.X, float.PositiveInfinity);
            foreach (var child in Children)
            {
                if (child is FrameworkElement element)
                {
                    element.Measure(childConstraint);
                    if (child is TreeViewItem item && item.VirtualizedTreeRowIndex >= 0)
                    {
                        UpdateMeasuredRowMetric(item.VirtualizedTreeRowIndex, element.DesiredSize);
                    }
                }
            }

            var viewportWidth = float.IsFinite(availableSize.X) ? MathF.Max(0f, availableSize.X) : 0f;
            return new Vector2(MathF.Max(viewportWidth, _estimatedExtentWidth), GetTotalExtentHeight());
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            _pendingDeferredOffsetRefresh = false;
            RealizeRows(finalSize.Y, invalidateMeasureForChildMutations: false);
            ArrangeRealizedRows(finalSize);

            return finalSize;
        }

        private void ArrangeRealizedRows(Vector2 finalSize)
        {
            var childConstraint = new Vector2(finalSize.X, float.PositiveInfinity);
            foreach (var child in Children)
            {
                if (child is not TreeViewItem item || item.VirtualizedTreeRowIndex < 0)
                {
                    continue;
                }

                if (item.NeedsMeasure)
                {
                    item.Measure(childConstraint);
                    if (!_preserveRowMetricsForNextArrange)
                    {
                        UpdateMeasuredRowMetric(item.VirtualizedTreeRowIndex, item.DesiredSize);
                    }
                }

                var index = item.VirtualizedTreeRowIndex;
                var rowHeight = GetRowHeight(index);
                var y = _preservedThumbSnapshotRowYByIndex is not null &&
                        _preservedThumbSnapshotRowYByIndex.TryGetValue(index, out var preservedY)
                    ? _owner.ActiveScrollViewer.VerticalOffset + preservedY
                    : LayoutSlot.Y + GetRowOffset(index);
                var targetSlot = new LayoutRect(
                    LayoutSlot.X,
                    y,
                    finalSize.X,
                    rowHeight);
                if (!item.NeedsArrange && LayoutSlotsMatch(item.LayoutSlot, targetSlot))
                {
                    continue;
                }

                item.Arrange(targetSlot);
            }

                    _preserveRowMetricsForNextArrange = false;
        }

        private static bool LayoutSlotsMatch(LayoutRect actual, LayoutRect expected)
        {
            const float epsilon = 0.001f;
            return MathF.Abs(actual.X - expected.X) <= epsilon &&
                   MathF.Abs(actual.Y - expected.Y) <= epsilon &&
                   MathF.Abs(actual.Width - expected.Width) <= epsilon &&
                   MathF.Abs(actual.Height - expected.Height) <= epsilon;
        }

        public void RefreshForStableViewportOffsetChange()
        {
            RefreshForStableViewportOffsetChange(forceRealization: false);
        }

        public void RefreshPendingStableViewportOffsetChange()
        {
            if (!_pendingDeferredOffsetRefresh)
            {
                return;
            }

            _pendingDeferredOffsetRefresh = false;
            _preserveRowMetricsForNextArrange = true;
            RefreshForStableViewportOffsetChange(forceRealization: true);
        }

        private void RefreshForStableViewportOffsetChange(bool forceRealization)
        {
            if (!IsMeasureValidForTests ||
                !IsArrangeValidForTests ||
                LayoutSlot.Width <= 0f ||
                LayoutSlot.Height <= 0f)
            {
                InvalidateArrangeForDirectLayoutOnly();
                return;
            }

            var range = CalculateRealizedRange(LayoutSlot.Height);
            if (!forceRealization && !_owner.IsActiveScrollViewerThumbCaptured())
            {
                _preservedThumbSnapshotRowYByIndex = null;
            }

            var isThumbCaptured = _owner.IsActiveScrollViewerThumbCaptured();
            if (!forceRealization && isThumbCaptured && range.First > 0)
            {
                // During thumb drags, retarget existing rows as lightweight display snapshots.
                // The real containers are committed on pointer release to avoid rebuilding templates every drag frame.
                RetargetRealizedRowsForThumbDrag(range.First, range.Last);
                _pendingDeferredOffsetRefresh = true;
                UiRoot.Current?.NotifyDirectRenderInvalidation(this);
                return;
            }

            if (RealizeRows(LayoutSlot.Height, invalidateMeasureForChildMutations: false, suppressLayoutInvalidations: true))
            {
                ArrangeRealizedRows(new Vector2(LayoutSlot.Width, LayoutSlot.Height));
            }

            UiRoot.Current?.NotifyDirectRenderInvalidation(this);
        }

        private void RetargetRealizedRowsForThumbDrag(int first, int last)
        {
            if (Children.Count == 0 || first > last)
            {
                return;
            }

            _firstRealizedIndex = first;
            _lastRealizedIndex = last;
            _preservedThumbSnapshotRowYByIndex ??= [];
            _preservedThumbSnapshotRowYByIndex.Clear();

            var childConstraint = new Vector2(LayoutSlot.Width, float.PositiveInfinity);
            var rowIndex = first;
            foreach (var child in Children)
            {
                if (rowIndex > last || child is not TreeViewItem item)
                {
                    continue;
                }

                var row = _rows[rowIndex];
                item.ApplyVirtualizedDisplaySnapshot(
                    _owner.GetHierarchicalHeader(row.Item),
                    row.HasChildren,
                    row.IsExpanded,
                    _owner.IsHierarchicalDataItemSelected(row.Item),
                    row.Depth,
                    rowIndex);

                var rowHeight = GetRowHeight(rowIndex);
                var slot = new LayoutRect(
                    LayoutSlot.X,
                    LayoutSlot.Y + GetRowOffset(rowIndex),
                    LayoutSlot.Width,
                    rowHeight);
                item.Arrange(slot);
                _preservedThumbSnapshotRowYByIndex[rowIndex] = slot.Y - _owner.ActiveScrollViewer.VerticalOffset;
                rowIndex++;
            }
        }

        private bool RealizeRows(
            float viewportHeight,
            bool invalidateMeasureForChildMutations,
            bool suppressLayoutInvalidations = false)
        {
            var range = CalculateRealizedRange(viewportHeight);
            var first = range.First;
            var last = range.Last;

            if (first == _firstRealizedIndex &&
                last == _lastRealizedIndex &&
                HasCurrentRealizedRows(first, last))
            {
                return false;
            }

            _firstRealizedIndex = first;
            _lastRealizedIndex = last;
            using (suppressLayoutInvalidations
                       ? DeferChildMutationLayoutInvalidations()
                       : invalidateMeasureForChildMutations
                           ? DeferChildMutationInvalidations()
                           : DeferChildMutationArrangeInvalidations())
            {
                Dictionary<int, TreeViewItem>? realizedByRowIndex = null;
                for (var childIndex = Children.Count - 1; childIndex >= 0; childIndex--)
                {
                    var child = Children[childIndex];
                    if (child is TreeViewItem treeItem &&
                        ShouldKeepRealizedChild(treeItem, first, last))
                    {
                        if (!treeItem.HasVirtualizedDisplaySnapshot)
                        {
                            var keptRowIndex = treeItem.VirtualizedTreeRowIndex;
                            _owner.ApplyHierarchicalDataContainer(treeItem, _rows[keptRowIndex], keptRowIndex);
                            realizedByRowIndex ??= [];
                            realizedByRowIndex[keptRowIndex] = treeItem;
                        }

                        continue;
                    }

                    RemoveChildAt(childIndex);
                    if (child is TreeViewItem removedTreeItem)
                    {
                        _owner.RecycleHierarchicalDataContainer(removedTreeItem);
                    }
                }

                var targetChildIndex = 0;
                for (var rowIndex = first; rowIndex <= last; rowIndex++)
                {
                    if (realizedByRowIndex?.TryGetValue(rowIndex, out var realizedContainer) == true)
                    {
                        var currentIndex = IndexOfChild(realizedContainer);
                        if (currentIndex >= 0 && currentIndex != targetChildIndex)
                        {
                            MoveChildRange(currentIndex, targetChildIndex, 1);
                        }

                        targetChildIndex++;
                        continue;
                    }

                    var row = _rows[rowIndex];
                    var container = _owner.RealizeHierarchicalDataContainer(row, rowIndex);
                    var existingIndex = IndexOfChild(container);
                    if (existingIndex >= 0)
                    {
                        if (existingIndex != targetChildIndex)
                        {
                            MoveChildRange(existingIndex, targetChildIndex, 1);
                        }
                    }
                    else
                    {
                        InsertChild(targetChildIndex, container);
                    }

                    targetChildIndex++;
                }
            }

            return true;
        }

        private bool HasCurrentRealizedRows(int first, int last)
        {
            if (first > last)
            {
                return Children.Count == 0;
            }

            var expectedCount = last - first + 1;
            if (Children.Count != expectedCount)
            {
                return false;
            }

            foreach (var child in Children)
            {
                if (child is not TreeViewItem item ||
                    item.HasVirtualizedDisplaySnapshot ||
                    !ShouldKeepRealizedChild(item, first, last))
                {
                    return false;
                }
            }

            return true;
        }

        private (int First, int Last) CalculateRealizedRange(float viewportHeight)
        {
            var viewer = _owner.ActiveScrollViewer;
            var offset = MathF.Max(0f, viewer.VerticalOffset);
            var viewport = float.IsFinite(viewportHeight) && viewportHeight > 0f
                ? viewportHeight
                : MathF.Max(viewer.ViewportHeight, FallbackRowHeight);
            var cacheHeight = _averageRowHeight * 4f;
            var first = Math.Max(0, FindRowIndexAtOffset(MathF.Max(0f, offset - cacheHeight)));
            var last = Math.Min(_rows.Count - 1, FindRowIndexAtOffset(offset + viewport + cacheHeight));
            return (first, last);
        }

        public float GetRowOffset(int rowIndex)
        {
            EnsureRowOffsetsCurrent();
            return rowIndex <= 0 ? 0f : _rowOffsets[Math.Clamp(rowIndex, 0, _rowOffsets.Count - 1)];
        }

        public float GetRowHeight(int rowIndex)
        {
            EnsureRowMetricStorage();
            if ((uint)rowIndex >= (uint)_rowHeights.Count)
            {
                return _averageRowHeight;
            }

            return MathF.Max(1f, _rowHeights[rowIndex]);
        }

        public bool IsRowHeightMeasured(int rowIndex)
        {
            EnsureRowMetricStorage();
            return (uint)rowIndex < (uint)_rowHeightMeasured.Count && _rowHeightMeasured[rowIndex];
        }

        private void EnsureRowMetricStorage()
        {
            var changed = false;
            while (_rowHeights.Count < _rows.Count)
            {
                _rowHeights.Add(_averageRowHeight);
                _rowHeightMeasured.Add(false);
                _rowOffsets.Add(0f);
                _rowHeightTotal += _averageRowHeight;
                changed = true;
            }

            if (_rowHeights.Count > _rows.Count)
            {
                for (var i = _rows.Count; i < _rowHeights.Count; i++)
                {
                    _rowHeightTotal -= _rowHeights[i];
                    if (_rowHeightMeasured[i])
                    {
                        _measuredRowHeightTotal -= _rowHeights[i];
                        _measuredRowHeightCount--;
                    }
                }

                _rowHeights.RemoveRange(_rows.Count, _rowHeights.Count - _rows.Count);
                _rowHeightMeasured.RemoveRange(_rows.Count, _rowHeightMeasured.Count - _rows.Count);
                _rowOffsets.RemoveRange(_rows.Count, _rowOffsets.Count - _rows.Count);
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            RecalculateAverageRowHeight();

            _rowOffsetsDirty = true;
        }

        private void UpdateMeasuredRowMetric(int rowIndex, Vector2 desiredSize)
        {
            if ((uint)rowIndex >= (uint)_rowHeights.Count)
            {
                return;
            }

            var height = MathF.Max(1f, desiredSize.Y);
            var previousHeight = _rowHeights[rowIndex];
            var wasMeasured = _rowHeightMeasured[rowIndex];
            if (wasMeasured && AreClose(previousHeight, height))
            {
                return;
            }

            _rowHeights[rowIndex] = height;
            _rowHeightTotal += height - previousHeight;
            if (wasMeasured)
            {
                _measuredRowHeightTotal += height - previousHeight;
            }
            else
            {
                _rowHeightMeasured[rowIndex] = true;
                _measuredRowHeightTotal += height;
                _measuredRowHeightCount++;
            }

            RecalculateAverageRowHeight();

            _rowOffsetsDirty = true;
        }

        private void RecalculateAverageRowHeight()
        {
            _averageRowHeight = _measuredRowHeightCount > 0
                ? MathF.Max(1f, _measuredRowHeightTotal / _measuredRowHeightCount)
                : FallbackRowHeight;
        }

        private float EstimateExtentWidth(IReadOnlyList<VisibleTreeDataEntry> rows)
        {
            var maxWidth = 0f;
            for (var i = 0; i < rows.Count; i++)
            {
                maxWidth = MathF.Max(maxWidth, EstimateRowWidth(rows[i]));
            }

            return maxWidth;
        }

        private float EstimateRowWidth(VisibleTreeDataEntry row)
        {
            var header = _owner.GetHierarchicalHeader(row.Item);
            if (_estimatedRowWidthCache.TryGetValue(row.Item, out var cached) &&
                cached.Depth == row.Depth &&
                cached.HasChildren == row.HasChildren &&
                string.Equals(cached.Header, header, StringComparison.Ordinal))
            {
                return cached.Width;
            }

            float width;
            if (string.IsNullOrEmpty(header))
            {
                width = row.Depth * 16f + 26f;
            }
            else
            {
                var depthOffset = MathF.Max(0f, row.Depth) * 16f;
                var glyphAndPadding = row.HasChildren ? 20f : 10f;
                width = depthOffset + glyphAndPadding + UiTextRenderer.MeasureWidth(_owner, header, _owner.FontSize);
            }

            _estimatedRowWidthCache[row.Item] = new EstimatedRowWidthCacheEntry(header, row.Depth, row.HasChildren, width);
            return width;
        }

        private readonly record struct EstimatedRowWidthCacheEntry(string Header, int Depth, bool HasChildren, float Width);

        private void EnsureRowOffsetsCurrent()
        {
            EnsureRowMetricStorage();
            if (!_rowOffsetsDirty)
            {
                return;
            }

            var offset = 0f;
            for (var i = 0; i < _rowOffsets.Count; i++)
            {
                _rowOffsets[i] = offset;
                offset += MathF.Max(1f, _rowHeights[i]);
            }

            _rowOffsetsDirty = false;
        }

        private float GetTotalExtentHeight()
        {
            if (_rowHeights.Count == 0)
            {
                return 0f;
            }

            EnsureRowOffsetsCurrent();
            var last = _rowHeights.Count - 1;
            return _rowOffsets[last] + MathF.Max(1f, _rowHeights[last]);
        }

        private int FindRowIndexAtOffset(float offset)
        {
            EnsureRowOffsetsCurrent();
            if (_rowOffsets.Count == 0)
            {
                return -1;
            }

            var low = 0;
            var high = _rowOffsets.Count - 1;
            while (low <= high)
            {
                var mid = low + ((high - low) / 2);
                var rowStart = _rowOffsets[mid];
                var rowEnd = rowStart + GetRowHeight(mid);
                if (offset < rowStart)
                {
                    high = mid - 1;
                }
                else if (offset >= rowEnd)
                {
                    low = mid + 1;
                }
                else
                {
                    return mid;
                }
            }

            return Math.Clamp(low, 0, _rowOffsets.Count - 1);
        }

        private bool ShouldKeepRealizedChild(TreeViewItem item, int first, int last)
        {
            var rowIndex = item.VirtualizedTreeRowIndex;
            return rowIndex >= first &&
                   rowIndex <= last &&
                   rowIndex < _rows.Count &&
                   ReferenceEquals(item.VirtualizedTreeDataItem, _rows[rowIndex].Item);
        }

        private int IndexOfChild(UIElement child)
        {
            for (var index = 0; index < Children.Count; index++)
            {
                if (ReferenceEquals(Children[index], child))
                {
                    return index;
                }
            }

            return -1;
        }
    }

}
