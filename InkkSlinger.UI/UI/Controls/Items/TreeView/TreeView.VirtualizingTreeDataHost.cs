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
        private readonly List<float> _rowOffsets = new();
        private bool _rowOffsetsDirty = true;
        private int _firstRealizedIndex = -1;
        private int _lastRealizedIndex = -1;
        private bool _pendingDeferredOffsetRefresh;
        private float _averageRowHeight = FallbackRowHeight;
        private float _rowHeightTotal;
        private float _estimatedExtentWidth;

        public VirtualizingTreeDataHost(TreeView owner)
        {
            _owner = owner;
            Background = Color.Transparent;
        }

        public bool OwnsHorizontalScrollOffset => true;

        public bool OwnsVerticalScrollOffset => true;

        public float AverageRowHeight => _averageRowHeight;

        public void SetRows(IReadOnlyList<VisibleTreeDataEntry> rows)
        {
            _rows = rows;
            _rowHeights.Clear();
            _rowOffsets.Clear();
            _rowHeightTotal = 0f;
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
            RealizeRows(availableSize.Y, invalidateMeasureForChildMutations: true);
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
            RealizeRows(finalSize.Y, invalidateMeasureForChildMutations: false);
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
                    UpdateMeasuredRowMetric(item.VirtualizedTreeRowIndex, item.DesiredSize);
                }

                var index = item.VirtualizedTreeRowIndex;
                var rowHeight = GetRowHeight(index);
                item.Arrange(new LayoutRect(
                    LayoutSlot.X,
                    LayoutSlot.Y + GetRowOffset(index),
                    finalSize.X,
                    rowHeight));
            }

            return finalSize;
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
            if (!forceRealization &&
                _owner.IsActiveScrollViewerThumbCaptured() &&
                range.First > 0)
            {
                // During thumb drags, retarget the existing rows as lightweight display snapshots.
                // The real containers are committed on pointer release to avoid rebuilding templates every drag frame.
                RetargetRealizedRowsForThumbDrag(range.First, range.Last);
                _pendingDeferredOffsetRefresh = true;
                UiRoot.Current?.NotifyDirectRenderInvalidation(this);
                return;
            }

            if (RealizeRows(LayoutSlot.Height, invalidateMeasureForChildMutations: false, suppressLayoutInvalidations: true))
            {
                InvalidateArrangeForDirectLayoutOnly(invalidateRender: false);
                Arrange(LayoutSlot);
            }

            UiRoot.Current?.NotifyDirectRenderInvalidation(this);
        }

        private void RetargetRealizedRowsForThumbDrag(int first, int last)
        {
            if (Children.Count == 0 || first > last)
            {
                return;
            }

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

                if (item.NeedsMeasure)
                {
                    item.Measure(childConstraint);
                }

                var rowHeight = GetRowHeight(rowIndex);
                item.Arrange(new LayoutRect(
                    LayoutSlot.X,
                    LayoutSlot.Y + GetRowOffset(rowIndex),
                    LayoutSlot.Width,
                    rowHeight));
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
                        }

                        continue;
                    }

                    RemoveChildAt(childIndex);
                    if (child is TreeViewItem removedTreeItem)
                    {
                        _owner.RecycleHierarchicalDataContainer(removedTreeItem);
                    }
                }

                for (var rowIndex = first; rowIndex <= last; rowIndex++)
                {
                    var row = _rows[rowIndex];
                    var container = _owner.RealizeHierarchicalDataContainer(row, rowIndex);
                    if (IndexOfChild(container) < 0)
                    {
                        InsertChild(Children.Count, container);
                    }
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
            var cacheHeight = MathF.Max(viewport, _averageRowHeight * 4f);
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

        private void EnsureRowMetricStorage()
        {
            var changed = false;
            while (_rowHeights.Count < _rows.Count)
            {
                _rowHeights.Add(_averageRowHeight);
                _rowOffsets.Add(0f);
                _rowHeightTotal += _averageRowHeight;
                changed = true;
            }

            if (_rowHeights.Count > _rows.Count)
            {
                for (var i = _rows.Count; i < _rowHeights.Count; i++)
                {
                    _rowHeightTotal -= _rowHeights[i];
                }

                _rowHeights.RemoveRange(_rows.Count, _rowHeights.Count - _rows.Count);
                _rowOffsets.RemoveRange(_rows.Count, _rowOffsets.Count - _rows.Count);
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            if (_rowHeights.Count == 0)
            {
                _rowHeightTotal = 0f;
            }
            else
            {
                _averageRowHeight = _rowHeightTotal / _rowHeights.Count;
            }

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
            if (AreClose(previousHeight, height))
            {
                return;
            }

            _rowHeights[rowIndex] = height;
            _rowHeightTotal += height - previousHeight;
            if (_rowHeights.Count > 0)
            {
                _averageRowHeight = _rowHeightTotal / _rowHeights.Count;
            }

            _rowOffsetsDirty = true;
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
            if (string.IsNullOrEmpty(header))
            {
                return row.Depth * 16f + 26f;
            }

            var depthOffset = MathF.Max(0f, row.Depth) * 16f;
            var glyphAndPadding = row.HasChildren ? 20f : 10f;
            return depthOffset + glyphAndPadding + UiTextRenderer.MeasureWidth(_owner, header, _owner.FontSize);
        }

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
