using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using InkkSlinger.UI.Telemetry;

namespace InkkSlinger;

public class UniformGrid : Panel
{
    private static long _measureOverrideElapsedTicks;
    private static long _arrangeOverrideElapsedTicks;
    private static long _measureChildrenCacheElapsedTicks;
    private static long _measureDimensionResolutionElapsedTicks;
    private static long _measureAggregateCheckElapsedTicks;
    private static long _measureChildLoopElapsedTicks;
    private static long _measureChildMeasureElapsedTicks;
    private static int _measureChildrenCacheRefreshCount;
    private static int _measureAggregateReuseHitCount;
    private static int _measureAggregateReuseMissCount;
    private static int _measureChildReuseCount;
    private static int _measureChildMeasureCount;
    private readonly List<FrameworkElement> _measurableChildren = new();
    private readonly List<ChildMeasureState> _cachedChildMeasureStates = new();
    private bool _measurableChildrenDirty = true;
    private bool _hasCachedAggregateMeasureState;
    private AggregateMeasureState _cachedAggregateMeasureState;

    public static readonly DependencyProperty RowsProperty =
        DependencyProperty.Register(
            nameof(Rows),
            typeof(int),
            typeof(UniformGrid),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange),
            static value => value is int rows && rows >= 0);

    public static readonly DependencyProperty ColumnsProperty =
        DependencyProperty.Register(
            nameof(Columns),
            typeof(int),
            typeof(UniformGrid),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange),
            static value => value is int columns && columns >= 0);

    public static readonly DependencyProperty FirstColumnProperty =
        DependencyProperty.Register(
            nameof(FirstColumn),
            typeof(int),
            typeof(UniformGrid),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange),
            static value => value is int firstColumn && firstColumn >= 0);

    public int Rows
    {
        get => GetValue<int>(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    public int Columns
    {
        get => GetValue<int>(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    public int FirstColumn
    {
        get => GetValue<int>(FirstColumnProperty);
        set => SetValue(FirstColumnProperty, value);
    }

    public override void AddChild(UIElement child)
    {
        base.AddChild(child);
        InvalidateChildMeasureCache();
    }

    public override void InsertChild(int index, UIElement child)
    {
        base.InsertChild(index, child);
        InvalidateChildMeasureCache();
    }

    public override bool RemoveChild(UIElement child)
    {
        var removed = base.RemoveChild(child);
        if (removed)
        {
            InvalidateChildMeasureCache();
        }

        return removed;
    }

    public override bool RemoveChildAt(int index)
    {
        var removed = base.RemoveChildAt(index);
        if (removed)
        {
            InvalidateChildMeasureCache();
        }

        return removed;
    }

    public override bool MoveChildRange(int oldIndex, int count, int newIndex)
    {
        var moved = base.MoveChildRange(oldIndex, count, newIndex);
        if (moved)
        {
            InvalidateChildMeasureCache();
        }

        return moved;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            var phaseStart = Stopwatch.GetTimestamp();
            var measurableChildren = GetMeasurableChildren();
            _measureChildrenCacheElapsedTicks += Stopwatch.GetTimestamp() - phaseStart;
            var childCount = measurableChildren.Count;
            if (childCount == 0)
            {
                return Vector2.Zero;
            }

            phaseStart = Stopwatch.GetTimestamp();
            var (rows, columns) = ResolveGridDimensions(childCount);
            _measureDimensionResolutionElapsedTicks += Stopwatch.GetTimestamp() - phaseStart;
            if (rows <= 0 || columns <= 0)
            {
                return Vector2.Zero;
            }

            var cellAvailable = new Vector2(
                columns > 0 ? availableSize.X / columns : availableSize.X,
                rows > 0 ? availableSize.Y / rows : availableSize.Y);

            phaseStart = Stopwatch.GetTimestamp();
            if (CanReuseAggregateMeasure(measurableChildren, rows, columns))
            {
                _measureAggregateCheckElapsedTicks += Stopwatch.GetTimestamp() - phaseStart;
                _measureAggregateReuseHitCount++;
                return _cachedAggregateMeasureState.DesiredSize;
            }
            _measureAggregateCheckElapsedTicks += Stopwatch.GetTimestamp() - phaseStart;
            _measureAggregateReuseMissCount++;

            var maxChildWidth = 0f;
            var maxChildHeight = 0f;
            var allChildrenAvailableIndependent = true;
            EnsureChildMeasureStateCapacity(childCount);
            phaseStart = Stopwatch.GetTimestamp();
            for (var i = 0; i < measurableChildren.Count; i++)
            {
                var frameworkChild = measurableChildren[i];
                var childMeasureStart = Stopwatch.GetTimestamp();
                var desiredSize = MeasureChildOrReuseCachedState(frameworkChild, cellAvailable, out var childState, out var reusedCachedState);
                _measureChildMeasureElapsedTicks += Stopwatch.GetTimestamp() - childMeasureStart;
                if (reusedCachedState)
                {
                    _measureChildReuseCount++;
                }
                else
                {
                    _measureChildMeasureCount++;
                }

                _cachedChildMeasureStates[i] = childState;
                maxChildWidth = MathF.Max(maxChildWidth, desiredSize.X);
                maxChildHeight = MathF.Max(maxChildHeight, desiredSize.Y);
                allChildrenAvailableIndependent &= childState.CanReuseForAnyAvailableSize;
            }
            _measureChildLoopElapsedTicks += Stopwatch.GetTimestamp() - phaseStart;

            var desired = new Vector2(maxChildWidth * columns, maxChildHeight * rows);
            _cachedAggregateMeasureState = new AggregateMeasureState(rows, columns, desired, allChildrenAvailableIndependent);
            _hasCachedAggregateMeasureState = true;
            return desired;
        }
        finally
        {
            _measureOverrideElapsedTicks += Stopwatch.GetTimestamp() - start;
        }
    }

    protected override bool CanReuseMeasureForAvailableSizeChange(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
    {
        _ = previousAvailableSize;
        _ = nextAvailableSize;
        return _hasCachedAggregateMeasureState &&
               _cachedAggregateMeasureState.AllChildrenAvailableIndependent;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            var measurableChildren = GetMeasurableChildren();
            var childCount = measurableChildren.Count;
            if (childCount == 0)
            {
                return finalSize;
            }

            var (rows, columns) = ResolveGridDimensions(childCount);
            if (rows <= 0 || columns <= 0)
            {
                return finalSize;
            }

            var cellWidth = finalSize.X / columns;
            var cellHeight = finalSize.Y / rows;
            var startIndex = Math.Min(FirstColumn, columns - 1);

            for (var visualIndex = 0; visualIndex < measurableChildren.Count; visualIndex++)
            {
                var frameworkChild = measurableChildren[visualIndex];
                var offsetIndex = visualIndex + startIndex;
                var row = offsetIndex / columns;
                var column = offsetIndex % columns;

                frameworkChild.Arrange(new LayoutRect(
                    LayoutSlot.X + (column * cellWidth),
                    LayoutSlot.Y + (row * cellHeight),
                    cellWidth,
                    cellHeight));
            }

            return finalSize;
        }
        finally
        {
            _arrangeOverrideElapsedTicks += Stopwatch.GetTimestamp() - start;
        }
    }

    private IReadOnlyList<FrameworkElement> GetMeasurableChildren()
    {
        if (!_measurableChildrenDirty)
        {
            return _measurableChildren;
        }

        _measureChildrenCacheRefreshCount++;
        _measurableChildren.Clear();
        foreach (var child in Children)
        {
            if (child is FrameworkElement frameworkChild)
            {
                _measurableChildren.Add(frameworkChild);
            }
        }

        _cachedChildMeasureStates.Clear();
        _measurableChildrenDirty = false;
        _hasCachedAggregateMeasureState = false;
        return _measurableChildren;
    }

    private (int Rows, int Columns) ResolveGridDimensions(int childCount)
    {
        var rows = Rows;
        var columns = Columns;

        if (rows == 0 && columns == 0)
        {
            columns = (int)MathF.Ceiling(MathF.Sqrt(childCount));
            rows = (int)MathF.Ceiling(childCount / (float)columns);
        }
        else if (rows == 0)
        {
            rows = (int)MathF.Ceiling((childCount + Math.Min(FirstColumn, columns)) / (float)columns);
        }
        else if (columns == 0)
        {
            columns = (int)MathF.Ceiling(childCount / (float)rows);
        }

        rows = Math.Max(1, rows);
        columns = Math.Max(1, columns);
        return (rows, columns);
    }

    private Vector2 MeasureChildOrReuseCachedState(
        FrameworkElement child,
        Vector2 availableSize,
        out ChildMeasureState childState,
        out bool reusedCachedState)
    {
        childState = ChildMeasureState.Invalid;
        reusedCachedState = false;
        if (!child.NeedsMeasure &&
            TryGetCachedChildMeasureState(child, out var cachedState) &&
            CanReuseCachedMeasure(cachedState, availableSize))
        {
            childState = cachedState;
            reusedCachedState = true;
            return cachedState.DesiredSize;
        }

        child.Measure(availableSize);
        var desiredSize = child.DesiredSize;
        childState = new ChildMeasureState(
            IsValid: true,
            availableSize,
            desiredSize,
            HasExplicitDimension(child.Width),
            HasExplicitDimension(child.Height),
            HasAvailableIndependentDesiredSize(child));
        return desiredSize;
    }

    private bool CanReuseAggregateMeasure(IReadOnlyList<FrameworkElement> measurableChildren, int rows, int columns)
    {
        if (!_hasCachedAggregateMeasureState ||
            _cachedAggregateMeasureState.Rows != rows ||
            _cachedAggregateMeasureState.Columns != columns ||
            !_cachedAggregateMeasureState.AllChildrenAvailableIndependent ||
            measurableChildren.Count != _cachedChildMeasureStates.Count)
        {
            return false;
        }

        for (var i = 0; i < measurableChildren.Count; i++)
        {
            if (measurableChildren[i].NeedsMeasure || !_cachedChildMeasureStates[i].IsValid)
            {
                return false;
            }
        }

        return true;
    }

    private bool TryGetCachedChildMeasureState(FrameworkElement child, out ChildMeasureState cachedState)
    {
        for (var i = 0; i < _measurableChildren.Count; i++)
        {
            if (!ReferenceEquals(_measurableChildren[i], child))
            {
                continue;
            }

            if (i < _cachedChildMeasureStates.Count)
            {
                cachedState = _cachedChildMeasureStates[i];
                return cachedState.IsValid;
            }

            break;
        }

        cachedState = ChildMeasureState.Invalid;
        return false;
    }

    private static bool CanReuseCachedMeasure(ChildMeasureState cachedState, Vector2 availableSize)
    {
        if (SizesMatch(cachedState.AvailableSize, availableSize) || cachedState.CanReuseForAnyAvailableSize)
        {
            return true;
        }

        return false;
    }

    private static bool HasAvailableIndependentDesiredSize(FrameworkElement child)
    {
        return child switch
        {
            Button button => button.HasAvailableIndependentDesiredSizeForUniformGrid(),
            TextBlock textBlock => textBlock.HasAvailableIndependentDesiredSizeForUniformGrid(),
            _ => false
        };
    }

    private static bool SizesMatch(Vector2 left, Vector2 right)
    {
        return DimensionMatches(left.X, right.X) &&
               DimensionMatches(left.Y, right.Y);
    }

    private static bool DimensionMatches(float left, float right)
    {
        if (float.IsNaN(left) && float.IsNaN(right))
        {
            return true;
        }

        if (float.IsInfinity(left) || float.IsInfinity(right))
        {
            return float.IsPositiveInfinity(left) == float.IsPositiveInfinity(right) &&
                   float.IsNegativeInfinity(left) == float.IsNegativeInfinity(right);
        }

        return MathF.Abs(left - right) < 0.01f;
    }

    private static bool HasExplicitDimension(float value)
    {
        return !float.IsNaN(value);
    }

    private void EnsureChildMeasureStateCapacity(int childCount)
    {
        while (_cachedChildMeasureStates.Count < childCount)
        {
            _cachedChildMeasureStates.Add(ChildMeasureState.Invalid);
        }
    }

    private void InvalidateChildMeasureCache()
    {
        _measurableChildrenDirty = true;
        _measurableChildren.Clear();
        _cachedChildMeasureStates.Clear();
        _hasCachedAggregateMeasureState = false;
    }

    internal static UniformGridTimingSnapshot GetTimingSnapshotForTests()
    {
        return new UniformGridTimingSnapshot(
            _measureOverrideElapsedTicks,
            _arrangeOverrideElapsedTicks,
            _measureChildrenCacheElapsedTicks,
            _measureDimensionResolutionElapsedTicks,
            _measureAggregateCheckElapsedTicks,
            _measureChildLoopElapsedTicks,
            _measureChildMeasureElapsedTicks,
            _measureChildrenCacheRefreshCount,
            _measureAggregateReuseHitCount,
            _measureAggregateReuseMissCount,
            _measureChildReuseCount,
            _measureChildMeasureCount);
    }

    internal static void ResetTimingForTests()
    {
        _measureOverrideElapsedTicks = 0;
        _arrangeOverrideElapsedTicks = 0;
        _measureChildrenCacheElapsedTicks = 0;
        _measureDimensionResolutionElapsedTicks = 0;
        _measureAggregateCheckElapsedTicks = 0;
        _measureChildLoopElapsedTicks = 0;
        _measureChildMeasureElapsedTicks = 0;
        _measureChildrenCacheRefreshCount = 0;
        _measureAggregateReuseHitCount = 0;
        _measureAggregateReuseMissCount = 0;
        _measureChildReuseCount = 0;
        _measureChildMeasureCount = 0;
    }

    private readonly record struct AggregateMeasureState(
        int Rows,
        int Columns,
        Vector2 DesiredSize,
        bool AllChildrenAvailableIndependent);

    private readonly record struct ChildMeasureState(
        bool IsValid,
        Vector2 AvailableSize,
        Vector2 DesiredSize,
        bool HasExplicitWidth,
        bool HasExplicitHeight,
        bool HasAvailableIndependentDesiredSize)
    {
        public static ChildMeasureState Invalid => new(
            false,
            Vector2.Zero,
            Vector2.Zero,
            false,
            false,
            false);

        public bool CanReuseForAnyAvailableSize =>
            (HasExplicitWidth || HasAvailableIndependentDesiredSize) &&
            (HasExplicitHeight || HasAvailableIndependentDesiredSize);
    }
}
