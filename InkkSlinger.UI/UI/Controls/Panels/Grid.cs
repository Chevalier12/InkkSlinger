using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public enum GridUnitType
{
    Auto,
    Pixel,
    Star
}

public readonly struct GridLength
{
    public GridLength(float value, GridUnitType gridUnitType = GridUnitType.Pixel)
    {
        if (gridUnitType != GridUnitType.Auto && value < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        Value = gridUnitType == GridUnitType.Auto ? 1f : value;
        GridUnitType = gridUnitType;
    }

    public float Value { get; }

    public GridUnitType GridUnitType { get; }

    public bool IsAuto => GridUnitType == GridUnitType.Auto;

    public bool IsStar => GridUnitType == GridUnitType.Star;

    public bool IsPixel => GridUnitType == GridUnitType.Pixel;

    public static GridLength Auto => new(1f, GridUnitType.Auto);

    public static GridLength Star => new(1f, GridUnitType.Star);
}

internal static class SharedSizeGroupUtilities
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (char.IsDigit(trimmed[0]))
        {
            throw new ArgumentException("SharedSizeGroup cannot start with a digit.", nameof(value));
        }

        for (var i = 0; i < trimmed.Length; i++)
        {
            var current = trimmed[i];
            if (char.IsLetterOrDigit(current) || current == '_')
            {
                continue;
            }

            throw new ArgumentException(
                "SharedSizeGroup can contain only letters, digits, and underscore.",
                nameof(value));
        }

        return trimmed;
    }
}

public sealed class ColumnDefinition
{
    private GridLength _width = GridLength.Star;
    private float _minWidth;
    private float _maxWidth = float.PositiveInfinity;
    private string? _sharedSizeGroup;

    public event EventHandler? Changed;

    public GridLength Width
    {
        get => _width;
        set
        {
            if (_width.Equals(value))
            {
                return;
            }

            _width = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public float MinWidth
    {
        get => _minWidth;
        set
        {
            var clamped = value < 0f ? 0f : value;
            if (MathF.Abs(_minWidth - clamped) < 0.0001f)
            {
                return;
            }

            _minWidth = clamped;
            if (_maxWidth < _minWidth)
            {
                _maxWidth = _minWidth;
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public float MaxWidth
    {
        get => _maxWidth;
        set
        {
            var clamped = value <= 0f ? 0f : value;
            if (MathF.Abs(_maxWidth - clamped) < 0.0001f)
            {
                return;
            }

            _maxWidth = clamped;
            if (_minWidth > _maxWidth)
            {
                _minWidth = _maxWidth;
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public string? SharedSizeGroup
    {
        get => _sharedSizeGroup;
        set
        {
            var normalized = SharedSizeGroupUtilities.Normalize(value);
            if (string.Equals(_sharedSizeGroup, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _sharedSizeGroup = normalized;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public float ActualWidth { get; internal set; }
}

public sealed class RowDefinition
{
    private GridLength _height = GridLength.Star;
    private float _minHeight;
    private float _maxHeight = float.PositiveInfinity;
    private string? _sharedSizeGroup;

    public event EventHandler? Changed;

    public GridLength Height
    {
        get => _height;
        set
        {
            if (_height.Equals(value))
            {
                return;
            }

            _height = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public float MinHeight
    {
        get => _minHeight;
        set
        {
            var clamped = value < 0f ? 0f : value;
            if (MathF.Abs(_minHeight - clamped) < 0.0001f)
            {
                return;
            }

            _minHeight = clamped;
            if (_maxHeight < _minHeight)
            {
                _maxHeight = _minHeight;
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public float MaxHeight
    {
        get => _maxHeight;
        set
        {
            var clamped = value <= 0f ? 0f : value;
            if (MathF.Abs(_maxHeight - clamped) < 0.0001f)
            {
                return;
            }

            _maxHeight = clamped;
            if (_minHeight > _maxHeight)
            {
                _minHeight = _maxHeight;
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public string? SharedSizeGroup
    {
        get => _sharedSizeGroup;
        set
        {
            var normalized = SharedSizeGroupUtilities.Normalize(value);
            if (string.Equals(_sharedSizeGroup, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _sharedSizeGroup = normalized;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public float ActualHeight { get; internal set; }
}

public class Grid : Panel
{
    private const bool EnableGridHangDiagnostics = false;
    private static long _diagMeasureCallCount;
    private static long _measureOverrideElapsedTicks;
    private static long _diagMeasureChildCount;
    private static long _diagMeasureDeferredRowSpanChildCount;
    private static long _diagMeasureFirstPassChildCount;
    private static long _diagMeasureSecondPassChildCount;
    private static long _diagMeasureRemeasureCheckCount;
    private static long _diagMeasureRemeasureCount;
    private static long _diagMeasureRemeasureSkipCount;
    private static long _diagArrangeCallCount;
    private static long _arrangeOverrideElapsedTicks;
    private static long _diagArrangeChildCount;
    private static long _diagArrangeSkippedChildCount;
    private static long _diagPrepareChildLayoutMetadataCallCount;
    private static long _diagPrepareChildLayoutMetadataElapsedTicks;
    private static long _diagChildLayoutMetadataCacheRefreshCount;
    private static long _diagChildLayoutMetadataEntryRefreshCount;
    private static long _diagChildLayoutMetadataEntryReuseCount;
    private static long _diagChildLayoutMetadataFrameworkChildCount;
    private static long _diagChildLayoutMetadataInvalidationCount;
    private static long _diagMeasureChildCallCount;
    private static long _diagMeasureChildElapsedTicks;
    private static long _diagMeasureChildCacheHitCount;
    private static long _diagMeasureChildCacheMissCount;
    private static long _diagMeasureChildMissNeedsMeasureCount;
    private static long _diagMeasureChildMissInvalidCacheCount;
    private static long _diagMeasureChildMissReuseRejectedCount;
    private static long _diagMeasureChildNeedHottestElapsedTicks;
    private static string _diagMeasureChildNeedHottestPath = "none";
    private static long _diagMeasureChildRejectHottestElapsedTicks;
    private static string _diagMeasureChildRejectHottestPath = "none";
    private static long _diagResolveDefinitionSizesCallCount;
    private static long _diagResolveDefinitionSizesElapsedTicks;
    private static long _diagResolveDefinitionFiniteAvailableCount;
    private static long _diagResolveDefinitionInfiniteAvailableCount;
    private static long _diagResolveDefinitionNaNAvailableCount;
    private static long _diagApplyChildRequirementCallCount;
    private static long _diagApplyChildRequirementElapsedTicks;
    private static long _diagApplyChildRequirementChangedCount;
    private static long _diagApplyChildRequirementNoOpCount;
    private static long _diagApplyChildRequirementFiniteStarConstraintCount;
    private static long _diagNormalizeDefinitionOverflowCallCount;
    private static long _diagNormalizeDefinitionOverflowTriggeredCount;
    private static long _diagDistributeExtraSizeCallCount;
    private static long _diagReduceOverflowCallCount;
    private static long _diagSharedSizeScopeRefreshCallCount;
    private static long _diagSharedSizeScopeHitCount;
    private static long _diagSharedSizeScopeMissCount;
    private static long _diagSharedSizeScopeChangedCount;
    private static long _diagApplySharedSizesCallCount;
    private static long _diagApplySharedSizeDefinitionCount;
    private static long _diagPublishSharedSizesCallCount;
    private static long _diagPublishSharedSizeDefinitionCount;
    private static readonly ConditionalWeakTable<UIElement, SharedSizeScopeState> SharedSizeScopes = new();
    public static readonly DependencyProperty RowProperty =
        DependencyProperty.RegisterAttached(
            "Row",
            typeof(int),
            typeof(Grid),
            new FrameworkPropertyMetadata(
                0,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                OnCellPropertyChanged),
            static value => value is int i && i >= 0);

    public static readonly DependencyProperty ColumnProperty =
        DependencyProperty.RegisterAttached(
            "Column",
            typeof(int),
            typeof(Grid),
            new FrameworkPropertyMetadata(
                0,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                OnCellPropertyChanged),
            static value => value is int i && i >= 0);

    public static readonly DependencyProperty RowSpanProperty =
        DependencyProperty.RegisterAttached(
            "RowSpan",
            typeof(int),
            typeof(Grid),
            new FrameworkPropertyMetadata(
                1,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                OnCellPropertyChanged),
            static value => value is int i && i > 0);

    public static readonly DependencyProperty ColumnSpanProperty =
        DependencyProperty.RegisterAttached(
            "ColumnSpan",
            typeof(int),
            typeof(Grid),
            new FrameworkPropertyMetadata(
                1,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                OnCellPropertyChanged),
            static value => value is int i && i > 0);

    public static readonly DependencyProperty IsSharedSizeScopeProperty =
        DependencyProperty.RegisterAttached(
            "IsSharedSizeScope",
            typeof(bool),
            typeof(Grid),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.AffectsMeasure,
                OnIsSharedSizeScopeChanged));

    public static readonly DependencyProperty ShowGridLinesProperty =
        DependencyProperty.Register(
            nameof(ShowGridLines),
            typeof(bool),
            typeof(Grid),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly ObservableCollection<ColumnDefinition> _columnDefinitions = new();
    private readonly ObservableCollection<RowDefinition> _rowDefinitions = new();
    private readonly List<DefinitionSnapshot> _measureColumns = new();
    private readonly List<DefinitionSnapshot> _measureRows = new();
    private readonly List<DefinitionSnapshot> _arrangeColumns = new();
    private readonly List<DefinitionSnapshot> _arrangeRows = new();
    private float[] _measuredColumnSizes = Array.Empty<float>();
    private float[] _measuredRowSizes = Array.Empty<float>();
    private int _definitionInvalidationSuppressionDepth;
    private bool _hasSuppressedDefinitionChange;
    private float[] _columnOffsets = Array.Empty<float>();
    private float[] _rowOffsets = Array.Empty<float>();
    private ChildLayoutMetadata[] _childLayoutMetadataCache = Array.Empty<ChildLayoutMetadata>();
    private FirstPassMeasureRecord[] _firstPassMeasureRecords = Array.Empty<FirstPassMeasureRecord>();
    private CachedChildMeasureState[] _cachedChildMeasureStates = Array.Empty<CachedChildMeasureState>();
    private CachedChildArrangeState[] _cachedChildArrangeStates = Array.Empty<CachedChildArrangeState>();
    private bool _childLayoutMetadataDirty = true;
    private SharedSizeScopeState? _sharedSizeScopeState;
    private bool _isReconcilingDescendantMeasureInvalidation;

    public Grid()
    {
        _columnDefinitions.CollectionChanged += OnColumnDefinitionsChanged;
        _rowDefinitions.CollectionChanged += OnRowDefinitionsChanged;
    }

    public ObservableCollection<ColumnDefinition> ColumnDefinitions => _columnDefinitions;

    public ObservableCollection<RowDefinition> RowDefinitions => _rowDefinitions;

    public bool ShowGridLines
    {
        get => GetValue<bool>(ShowGridLinesProperty);
        set => SetValue(ShowGridLinesProperty, value);
    }

    public static int GetRow(UIElement element)
    {
        return element.GetValue<int>(RowProperty);
    }

    public static void SetRow(UIElement element, int value)
    {
        element.SetValue(RowProperty, value);
    }

    public static int GetColumn(UIElement element)
    {
        return element.GetValue<int>(ColumnProperty);
    }

    public static void SetColumn(UIElement element, int value)
    {
        element.SetValue(ColumnProperty, value);
    }

    public static int GetRowSpan(UIElement element)
    {
        return element.GetValue<int>(RowSpanProperty);
    }

    public static void SetRowSpan(UIElement element, int value)
    {
        element.SetValue(RowSpanProperty, value);
    }

    public static int GetColumnSpan(UIElement element)
    {
        return element.GetValue<int>(ColumnSpanProperty);
    }

    public static void SetColumnSpan(UIElement element, int value)
    {
        element.SetValue(ColumnSpanProperty, value);
    }

    public static bool GetIsSharedSizeScope(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue<bool>(IsSharedSizeScopeProperty);
    }

    public static void SetIsSharedSizeScope(UIElement element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(IsSharedSizeScopeProperty, value);
    }

    public override void AddChild(UIElement child)
    {
        InvalidateChildLayoutMetadataCache();
        base.AddChild(child);
    }

    public override void InsertChild(int index, UIElement child)
    {
        InvalidateChildLayoutMetadataCache();
        base.InsertChild(index, child);
    }

    public override bool RemoveChild(UIElement child)
    {
        InvalidateChildLayoutMetadataCache();
        return base.RemoveChild(child);
    }

    public override bool RemoveChildAt(int index)
    {
        InvalidateChildLayoutMetadataCache();
        return base.RemoveChildAt(index);
    }

    public override bool MoveChildRange(int oldIndex, int count, int newIndex)
    {
        InvalidateChildLayoutMetadataCache();
        return base.MoveChildRange(oldIndex, count, newIndex);
    }

    protected override void OnVisualParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        base.OnVisualParentChanged(oldParent, newParent);
        DetachFromSharedSizeScope();
        InvalidateMeasure();
    }

    protected override void OnLogicalParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        base.OnLogicalParentChanged(oldParent, newParent);
        DetachFromSharedSizeScope();
        InvalidateMeasure();
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        base.OnRender(spriteBatch);

        if (!ShowGridLines || LayoutSlot.Width <= 0f || LayoutSlot.Height <= 0f)
        {
            return;
        }

        var lineColor = new Color(120, 120, 120);
        UiDrawing.DrawRectStroke(spriteBatch, LayoutSlot, 1f, lineColor, Opacity);

        for (var i = 1; i < _columnOffsets.Length; i++)
        {
            var x = _columnOffsets[i];
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x, LayoutSlot.Y, 1f, LayoutSlot.Height), lineColor, Opacity);
        }

        for (var i = 1; i < _rowOffsets.Length; i++)
        {
            var y = _rowOffsets[i];
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(LayoutSlot.X, y, LayoutSlot.Width, 1f), lineColor, Opacity);
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var start = Stopwatch.GetTimestamp();
        var measuredChildCount = 0;
        var deferredRowSpanChildCount = 0;
        var firstPassMeasuredChildCount = 0;
        var secondPassMeasuredChildCount = 0;
        var remeasureCheckCount = 0;
        var remeasureCount = 0;
        var remeasureSkipCount = 0;
        IncrementAggregate(ref _diagMeasureCallCount);
        try
        {
        var columns = PrepareColumnSnapshots(_measureColumns);
        var rows = PrepareRowSnapshots(_measureRows);
        RefreshSharedSizeScopeState();
        ApplySharedSizes(columns, isColumnAxis: true);
        ApplySharedSizes(rows, isColumnAxis: false);
        var childLayoutMetadata = PrepareChildLayoutMetadata(rows, columns);
        measuredChildCount = childLayoutMetadata.Length;

        for (var childMetadataIndex = 0; childMetadataIndex < childLayoutMetadata.Length; childMetadataIndex++)
        {
            if (ShouldDeferRowSpanMeasurement(childLayoutMetadata[childMetadataIndex]))
            {
                deferredRowSpanChildCount++;
            }
        }

        ResolveDefinitionSizes(columns, availableSize.X);
        ResolveDefinitionSizes(rows, availableSize.Y);
        EnsureChildMeasureRecordCapacity(childLayoutMetadata.Length);

        for (var deferredSpanPass = 0; deferredSpanPass < 2; deferredSpanPass++)
        {
            var useWidthPriority = ShouldUseWidthPriorityFirstPass(childLayoutMetadata, deferredSpanPass, availableSize.X);
            for (var widthPriorityPass = 0; widthPriorityPass < (useWidthPriority ? 2 : 1); widthPriorityPass++)
            {
                var measuredAnyChild = false;
                for (var childMeasureIndex = 0; childMeasureIndex < childLayoutMetadata.Length; childMeasureIndex++)
                {
                    ref readonly var metadata = ref childLayoutMetadata[childMeasureIndex];
                    if (!ShouldMeasureInFirstPass(metadata, deferredSpanPass, widthPriorityPass, useWidthPriority))
                    {
                        continue;
                    }

                    var firstPassAvailable = ResolveFirstPassAvailableSize(metadata, rows, columns, availableSize);
                    var childDesiredSize = MeasureChildOrReuseCachedState(childMeasureIndex, metadata, firstPassAvailable);
                    firstPassMeasuredChildCount++;
                    measuredAnyChild = true;

                    _firstPassMeasureRecords[childMeasureIndex] = new FirstPassMeasureRecord(
                        metadata.Cell,
                        firstPassAvailable,
                        childDesiredSize,
                        metadata.HasExplicitWidth,
                        metadata.HasExplicitHeight,
                        float.IsPositiveInfinity(firstPassAvailable.X),
                        float.IsPositiveInfinity(firstPassAvailable.Y));

                    ApplyChildRequirement(
                        columns,
                        metadata.Cell.Column,
                        metadata.Cell.ColumnSpan,
                        childDesiredSize.X,
                        !float.IsInfinity(availableSize.X) && !float.IsNaN(availableSize.X));
                    ApplyChildRequirement(
                        rows,
                        metadata.Cell.Row,
                        metadata.Cell.RowSpan,
                        childDesiredSize.Y,
                        !float.IsInfinity(availableSize.Y) && !float.IsNaN(availableSize.Y));
                }

                if (useWidthPriority && widthPriorityPass == 0 && measuredAnyChild)
                {
                    FinalizeDefinitionSizes(columns, availableSize.X);
                    FinalizeDefinitionSizes(rows, availableSize.Y);
                }
            }
        }

        FinalizeDefinitionSizes(columns, availableSize.X);
        FinalizeDefinitionSizes(rows, availableSize.Y);

        for (var pass = 0; pass < 4; pass++)
        {
            var definitionsChanged = false;
            for (var deferredSpanPass = 0; deferredSpanPass < 2; deferredSpanPass++)
            {
                for (var childMeasureIndex = 0; childMeasureIndex < childLayoutMetadata.Length; childMeasureIndex++)
                {
                    ref readonly var metadata = ref childLayoutMetadata[childMeasureIndex];
                    if (ShouldDeferSpanMeasurement(metadata) != (deferredSpanPass == 1))
                    {
                        continue;
                    }

                    var childAvailable = new Vector2(
                        SumRange(columns, metadata.Cell.Column, metadata.Cell.ColumnSpan),
                        SumRange(rows, metadata.Cell.Row, metadata.Cell.RowSpan));
                    remeasureCheckCount++;

                    if (!ShouldReMeasureChild(_firstPassMeasureRecords[childMeasureIndex], childAvailable))
                    {
                        remeasureSkipCount++;
                        continue;
                    }

                    var childDesiredSize = MeasureChildOrReuseCachedState(childMeasureIndex, metadata, childAvailable);
                    remeasureCount++;
                    secondPassMeasuredChildCount++;
                    definitionsChanged |= ApplyChildRequirement(
                        columns,
                        metadata.Cell.Column,
                        metadata.Cell.ColumnSpan,
                        childDesiredSize.X,
                        !float.IsInfinity(availableSize.X) && !float.IsNaN(availableSize.X));
                    definitionsChanged |= ApplyChildRequirement(
                        rows,
                        metadata.Cell.Row,
                        metadata.Cell.RowSpan,
                        childDesiredSize.Y,
                        !float.IsInfinity(availableSize.Y) && !float.IsNaN(availableSize.Y));
                }
            }

            if (!definitionsChanged)
            {
                break;
            }

            FinalizeDefinitionSizes(columns, availableSize.X);
            FinalizeDefinitionSizes(rows, availableSize.Y);
        }

        PublishSharedSizes(columns, isColumnAxis: true);
        PublishSharedSizes(rows, isColumnAxis: false);

        CopySizesToBuffer(columns, ref _measuredColumnSizes);
        CopySizesToBuffer(rows, ref _measuredRowSizes);

        var gridDesiredSize = new Vector2(SumSizes(columns), SumSizes(rows));
        return gridDesiredSize;
        }
        finally
        {
            AddAggregate(ref _measureOverrideElapsedTicks, Stopwatch.GetTimestamp() - start);
            AddAggregate(ref _diagMeasureChildCount, measuredChildCount);
            AddAggregate(ref _diagMeasureDeferredRowSpanChildCount, deferredRowSpanChildCount);
            AddAggregate(ref _diagMeasureFirstPassChildCount, firstPassMeasuredChildCount);
            AddAggregate(ref _diagMeasureSecondPassChildCount, secondPassMeasuredChildCount);
            AddAggregate(ref _diagMeasureRemeasureCheckCount, remeasureCheckCount);
            AddAggregate(ref _diagMeasureRemeasureCount, remeasureCount);
            AddAggregate(ref _diagMeasureRemeasureSkipCount, remeasureSkipCount);
        }
    }

    protected override bool CanReuseMeasureForAvailableSizeChange(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
    {
        if (GetType() != typeof(Grid) || _childLayoutMetadataDirty || HasSharedSizeDefinitions())
        {
            return false;
        }

        if (HasFiniteStarAxisSizeChange(previousAvailableSize, nextAvailableSize))
        {
            return false;
        }

        var columns = PrepareColumnSnapshots(_arrangeColumns);
        var rows = PrepareRowSnapshots(_arrangeRows);
        var childLayoutMetadata = PrepareChildLayoutMetadata(rows, columns);
        if (!TrySimulateMeasureWithCachedChildren(childLayoutMetadata, nextAvailableSize, columns, rows, out var nextDesiredSize))
        {
            return false;
        }

        return AreSizesClose(nextDesiredSize, GetCurrentMeasuredDesiredSize()) &&
               AreDefinitionSizesClose(columns, _measuredColumnSizes) &&
               AreDefinitionSizesClose(rows, _measuredRowSizes);
    }

    protected override bool TryHandleMeasureInvalidation(UIElement origin, UIElement? source, string reason)
    {
        _ = source;
        _ = reason;

        if (ReferenceEquals(origin, this) ||
            _isReconcilingDescendantMeasureInvalidation ||
            NeedsMeasure ||
            HasSharedSizeDefinitions() ||
            !IsGridDescendant(origin))
        {
            return false;
        }

        var availableSize = PreviousAvailableSizeForTests;
        if (float.IsNaN(availableSize.X) || float.IsNaN(availableSize.Y))
        {
            return false;
        }

        _isReconcilingDescendantMeasureInvalidation = true;
        try
        {
            if (!TryReconcileDescendantMeasureInvalidation(availableSize, out var desiredSize))
            {
                return false;
            }

            if (!AreSizesClose(desiredSize, GetCurrentMeasuredDesiredSize()))
            {
                return false;
            }

            MarkMeasureValidAfterLocalReconciliation();
            InvalidateArrangeForDirectLayoutOnly();
            return true;
        }
        finally
        {
            _isReconcilingDescendantMeasureInvalidation = false;
        }
    }

    protected internal override bool ShouldSuppressMeasureInvalidationFromDescendantDuringMeasure(FrameworkElement descendant)
    {
        return _isReconcilingDescendantMeasureInvalidation && IsGridDescendant(descendant);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var start = Stopwatch.GetTimestamp();
        var arrangedChildCount = 0;
        var skippedChildCount = 0;
        IncrementAggregate(ref _diagArrangeCallCount);
        try
        {
            var columns = PrepareColumnSnapshots(_arrangeColumns);
            var rows = PrepareRowSnapshots(_arrangeRows);
            RefreshSharedSizeScopeState();
            ApplySharedSizes(columns, isColumnAxis: true);
            ApplySharedSizes(rows, isColumnAxis: false);

            ResolveDefinitionSizes(columns, finalSize.X, _measuredColumnSizes);
            ResolveDefinitionSizes(rows, finalSize.Y, _measuredRowSizes);
            SyncActualDefinitionSizes(columns, rows);

            FillOffsets(columns, LayoutSlot.X, ref _columnOffsets);
            FillOffsets(rows, LayoutSlot.Y, ref _rowOffsets);

            var childLayoutMetadata = PrepareChildLayoutMetadata(rows, columns);
            EnsureChildMeasureRecordCapacity(childLayoutMetadata.Length);

            foreach (ref readonly var metadata in childLayoutMetadata.AsSpan())
            {
                arrangedChildCount++;
                var cell = metadata.Cell;
                var x = _columnOffsets[cell.Column];
                var y = _rowOffsets[cell.Row];
                var width = SumRange(columns, cell.Column, cell.ColumnSpan);
                var height = SumRange(rows, cell.Row, cell.RowSpan);
                var childRect = new LayoutRect(x, y, width, height);
                var childArrangeIndex = arrangedChildCount - 1;
                if (CanReuseCachedArrange(childArrangeIndex, metadata.Child, childRect))
                {
                    continue;
                }

                if (TryTranslateCachedArrange(childArrangeIndex, metadata.Child, childRect))
                {
                    continue;
                }

                metadata.Child.Arrange(childRect);
                if (childArrangeIndex >= 0 && childArrangeIndex < _cachedChildArrangeStates.Length)
                {
                    _cachedChildArrangeStates[childArrangeIndex] = new CachedChildArrangeState(
                        metadata.Child,
                        childRect,
                        metadata.Child.DesiredSize,
                        true);
                }
            }

            skippedChildCount = CountNonFrameworkChildren();
            return finalSize;
        }
        finally
        {
            AddAggregate(ref _arrangeOverrideElapsedTicks, Stopwatch.GetTimestamp() - start);
            AddAggregate(ref _diagArrangeChildCount, arrangedChildCount);
            AddAggregate(ref _diagArrangeSkippedChildCount, skippedChildCount);
        }
    }

    private void SyncActualDefinitionSizes(
        IReadOnlyList<DefinitionSnapshot> arrangedColumns,
        IReadOnlyList<DefinitionSnapshot> arrangedRows)
    {
        for (var i = 0; i < _columnDefinitions.Count; i++)
        {
            _columnDefinitions[i].ActualWidth = i < arrangedColumns.Count
                ? arrangedColumns[i].Size
                : 0f;
        }

        for (var i = 0; i < _rowDefinitions.Count; i++)
        {
            _rowDefinitions[i].ActualHeight = i < arrangedRows.Count
                ? arrangedRows[i].Size
                : 0f;
        }
    }

    private static void OnCellPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element || element.VisualParent is not Grid grid)
        {
            return;
        }

        grid.InvalidateChildLayoutMetadataCache();
        grid.InvalidateMeasure();
    }

    private static void OnIsSharedSizeScopeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
        {
            return;
        }

        InvalidateSharedSizeScopeSubtree(element);
        if (element is FrameworkElement frameworkElement)
        {
            frameworkElement.InvalidateMeasure();
        }
    }

    private void OnColumnDefinitionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (ColumnDefinition definition in e.OldItems)
            {
                definition.Changed -= OnDefinitionChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (ColumnDefinition definition in e.NewItems)
            {
                definition.Changed += OnDefinitionChanged;
            }
        }

        DetachFromSharedSizeScope();
        InvalidateChildLayoutMetadataCache();
        InvalidateMeasure();
    }

    private void OnRowDefinitionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (RowDefinition definition in e.OldItems)
            {
                definition.Changed -= OnDefinitionChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (RowDefinition definition in e.NewItems)
            {
                definition.Changed += OnDefinitionChanged;
            }
        }

        DetachFromSharedSizeScope();
        InvalidateChildLayoutMetadataCache();
        InvalidateMeasure();
    }

    private void OnDefinitionChanged(object? sender, EventArgs e)
    {
        if (_definitionInvalidationSuppressionDepth > 0)
        {
            _hasSuppressedDefinitionChange = true;
            return;
        }

        DetachFromSharedSizeScope();
        InvalidateChildLayoutMetadataCache();
        InvalidateMeasure();
    }

    internal bool ApplySplitterColumnResize(int indexA, int indexB, float firstWidth, float secondWidth)
    {
        return ApplySplitterColumnResize(indexA, indexB, firstWidth, secondWidth, debugContext: null);
    }

    internal bool ApplySplitterColumnResize(int indexA, int indexB, float firstWidth, float secondWidth, string? debugContext)
    {
        if (indexA < 0 || indexA >= _columnDefinitions.Count ||
            indexB < 0 || indexB >= _columnDefinitions.Count)
        {
            return false;
        }

        var firstDefinition = _columnDefinitions[indexA];
        var secondDefinition = _columnDefinitions[indexB];

        BeginSuppressDefinitionInvalidation();
        try
        {
            ApplyResizedColumnLengths(firstDefinition, secondDefinition, firstWidth, secondWidth);
        }
        finally
        {
            EndSuppressDefinitionInvalidation();
        }

        return FinalizeSuppressedDefinitionResize(debugContext);
    }

    internal bool ApplySplitterRowResize(int indexA, int indexB, float firstHeight, float secondHeight)
    {
        return ApplySplitterRowResize(indexA, indexB, firstHeight, secondHeight, debugContext: null);
    }

    internal bool ApplySplitterRowResize(int indexA, int indexB, float firstHeight, float secondHeight, string? debugContext)
    {
        if (indexA < 0 || indexA >= _rowDefinitions.Count ||
            indexB < 0 || indexB >= _rowDefinitions.Count)
        {
            return false;
        }

        var firstDefinition = _rowDefinitions[indexA];
        var secondDefinition = _rowDefinitions[indexB];

        BeginSuppressDefinitionInvalidation();
        try
        {
            ApplyResizedRowLengths(firstDefinition, secondDefinition, firstHeight, secondHeight);
        }
        finally
        {
            EndSuppressDefinitionInvalidation();
        }

        return FinalizeSuppressedDefinitionResize(debugContext);
    }

    private static void ApplyResizedColumnLengths(
        ColumnDefinition firstDefinition,
        ColumnDefinition secondDefinition,
        float firstWidth,
        float secondWidth)
    {
        var firstWasStar = firstDefinition.Width.IsStar;
        var secondWasStar = secondDefinition.Width.IsStar;

        if (firstWasStar && secondWasStar)
        {
            ApplyResizedStarPair(
                firstWidth,
                secondWidth,
                firstDefinition.Width.Value,
                secondDefinition.Width.Value,
                static length => new GridLength(length, GridUnitType.Star),
                value => firstDefinition.Width = value,
                value => secondDefinition.Width = value);
            return;
        }

        if (firstWasStar)
        {
            firstDefinition.Width = GridLength.Star;
            secondDefinition.Width = new GridLength(secondWidth, GridUnitType.Pixel);
            return;
        }

        if (secondWasStar)
        {
            firstDefinition.Width = new GridLength(firstWidth, GridUnitType.Pixel);
            secondDefinition.Width = GridLength.Star;
            return;
        }

        firstDefinition.Width = new GridLength(firstWidth, GridUnitType.Pixel);
        secondDefinition.Width = new GridLength(secondWidth, GridUnitType.Pixel);
    }

    private static void ApplyResizedRowLengths(
        RowDefinition firstDefinition,
        RowDefinition secondDefinition,
        float firstHeight,
        float secondHeight)
    {
        var firstWasStar = firstDefinition.Height.IsStar;
        var secondWasStar = secondDefinition.Height.IsStar;

        if (firstWasStar && secondWasStar)
        {
            ApplyResizedStarPair(
                firstHeight,
                secondHeight,
                firstDefinition.Height.Value,
                secondDefinition.Height.Value,
                static length => new GridLength(length, GridUnitType.Star),
                value => firstDefinition.Height = value,
                value => secondDefinition.Height = value);
            return;
        }

        if (firstWasStar)
        {
            firstDefinition.Height = GridLength.Star;
            secondDefinition.Height = new GridLength(secondHeight, GridUnitType.Pixel);
            return;
        }

        if (secondWasStar)
        {
            firstDefinition.Height = new GridLength(firstHeight, GridUnitType.Pixel);
            secondDefinition.Height = GridLength.Star;
            return;
        }

        firstDefinition.Height = new GridLength(firstHeight, GridUnitType.Pixel);
        secondDefinition.Height = new GridLength(secondHeight, GridUnitType.Pixel);
    }

    private static void ApplyResizedStarPair(
        float firstSize,
        float secondSize,
        float firstWeight,
        float secondWeight,
        Func<float, GridLength> createStarLength,
        Action<GridLength> applyFirst,
        Action<GridLength> applySecond)
    {
        var totalSize = MathF.Max(0f, firstSize) + MathF.Max(0f, secondSize);
        if (totalSize <= 0.001f)
        {
            applyFirst(GridLength.Star);
            applySecond(GridLength.Star);
            return;
        }

        var totalWeight = MathF.Max(0.0001f, firstWeight + secondWeight);
        var firstRatio = MathF.Max(0f, firstSize) / totalSize;
        var secondRatio = MathF.Max(0f, secondSize) / totalSize;
        var adjustedFirstWeight = MathF.Max(0.0001f, totalWeight * firstRatio);
        var adjustedSecondWeight = MathF.Max(0.0001f, totalWeight * secondRatio);

        applyFirst(createStarLength(adjustedFirstWeight));
        applySecond(createStarLength(adjustedSecondWeight));
    }

    private void BeginSuppressDefinitionInvalidation()
    {
        _definitionInvalidationSuppressionDepth++;
    }

    private void EndSuppressDefinitionInvalidation()
    {
        _definitionInvalidationSuppressionDepth = Math.Max(0, _definitionInvalidationSuppressionDepth - 1);
    }

    private bool FinalizeSuppressedDefinitionResize(string? debugContext = null)
    {
        if (!_hasSuppressedDefinitionChange)
        {
            return false;
        }

        _hasSuppressedDefinitionChange = false;
        DetachFromSharedSizeScope();
        InvalidateChildLayoutMetadataCache();

        if (TryResolveSplitterResizeAsArrangeOnly())
        {
            InvalidateArrange();
            return true;
        }

        InvalidateMeasure();
        return true;
    }

    private bool TryResolveSplitterResizeAsArrangeOnly()
    {
        if (NeedsMeasure || !IsMeasureValidForTests || HasSharedSizeDefinitions())
        {
            return false;
        }

        var availableSize = PreviousAvailableSizeForTests;
        if (float.IsNaN(availableSize.X) || float.IsNaN(availableSize.Y))
        {
            return false;
        }

        var columns = PrepareColumnSnapshots(_measureColumns);
        var rows = PrepareRowSnapshots(_measureRows);
        var childLayoutMetadata = PrepareChildLayoutMetadata(rows, columns);
        if (!TrySimulateMeasureWithCachedChildren(childLayoutMetadata, availableSize, columns, rows, out var desiredSize))
        {
            return false;
        }

        if (!AreSizesClose(desiredSize, GetCurrentMeasuredDesiredSize()))
        {
            return false;
        }

        CopySizesToBuffer(columns, ref _measuredColumnSizes);
        CopySizesToBuffer(rows, ref _measuredRowSizes);
        return true;
    }

    private List<DefinitionSnapshot> PrepareColumnSnapshots(List<DefinitionSnapshot> target)
    {
        PrepareDefinitionSnapshots(target, _columnDefinitions.Count);
        if (_columnDefinitions.Count == 0)
        {
            target[0].Reset(GridLength.Star, 0f, float.PositiveInfinity, null);
            return target;
        }

        for (var i = 0; i < _columnDefinitions.Count; i++)
        {
            var definition = _columnDefinitions[i];
            var length = definition.Width.IsStar && !string.IsNullOrEmpty(definition.SharedSizeGroup)
                ? GridLength.Auto
                : definition.Width;
            target[i].Reset(
                length,
                definition.MinWidth,
                definition.MaxWidth,
                definition.SharedSizeGroup);
        }

        return target;
    }

    private List<DefinitionSnapshot> PrepareRowSnapshots(List<DefinitionSnapshot> target)
    {
        PrepareDefinitionSnapshots(target, _rowDefinitions.Count);
        if (_rowDefinitions.Count == 0)
        {
            target[0].Reset(GridLength.Star, 0f, float.PositiveInfinity, null);
            return target;
        }

        for (var i = 0; i < _rowDefinitions.Count; i++)
        {
            var definition = _rowDefinitions[i];
            var length = definition.Height.IsStar && !string.IsNullOrEmpty(definition.SharedSizeGroup)
                ? GridLength.Auto
                : definition.Height;
            target[i].Reset(
                length,
                definition.MinHeight,
                definition.MaxHeight,
                definition.SharedSizeGroup);
        }

        return target;
    }

    private static void ResolveDefinitionSizes(
        IReadOnlyList<DefinitionSnapshot> definitions,
        float available,
        IReadOnlyList<float>? measuredSizes = null)
    {
        var startTicks = Stopwatch.GetTimestamp();
        IncrementAggregate(ref _diagResolveDefinitionSizesCallCount);
        if (float.IsNaN(available))
        {
            IncrementAggregate(ref _diagResolveDefinitionNaNAvailableCount);
        }
        else if (float.IsInfinity(available))
        {
            IncrementAggregate(ref _diagResolveDefinitionInfiniteAvailableCount);
        }
        else
        {
            IncrementAggregate(ref _diagResolveDefinitionFiniteAvailableCount);
        }

        try
        {
        var fixedTotal = 0f;
        var starWeight = 0f;

        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            if (definition.Length.IsPixel)
            {
                definition.Size = Clamp(definition.Length.Value, definition.EffectiveMin, definition.Max);
                fixedTotal += definition.Size;
                continue;
            }

            if (definition.Length.IsAuto)
            {
                var measured = measuredSizes != null && i < measuredSizes.Count ? measuredSizes[i] : definition.Size;
                definition.Size = Clamp(measured, definition.EffectiveMin, definition.Max);
                fixedTotal += definition.Size;
                continue;
            }

            definition.Size = Clamp(definition.Size, definition.EffectiveMin, definition.Max);
            starWeight += MathF.Max(0.0001f, definition.Length.Value);
        }

        if (starWeight <= 0f)
        {
            return;
        }

        if (float.IsInfinity(available) || float.IsNaN(available))
        {
            return;
        }

        var remaining = MathF.Max(0f, available - fixedTotal);
        ResolveStarDefinitionSizes(definitions, remaining);

        NormalizeDefinitionOverflow(definitions, available);
        }
        finally
        {
            AddAggregate(ref _diagResolveDefinitionSizesElapsedTicks, Stopwatch.GetTimestamp() - startTicks);
        }
    }

    private static void ResolveStarDefinitionSizes(
        IReadOnlyList<DefinitionSnapshot> definitions,
        float available)
    {
        var remaining = available;
        var unresolved = new bool[definitions.Count];
        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            if (!definition.Length.IsStar)
            {
                continue;
            }

            if (definition.Length.Value <= 0f)
            {
                remaining -= definition.Size;
                continue;
            }

            unresolved[i] = true;
        }

        while (true)
        {
            var totalWeight = 0f;
            var unresolvedCount = 0;
            for (var i = 0; i < definitions.Count; i++)
            {
                if (!unresolved[i])
                {
                    continue;
                }

                var definition = definitions[i];
                totalWeight += definition.Length.Value;
                unresolvedCount++;
            }

            if (unresolvedCount == 0 || totalWeight <= 0f)
            {
                return;
            }

            var constrainedAny = false;
            for (var i = 0; i < definitions.Count; i++)
            {
                if (!unresolved[i])
                {
                    continue;
                }

                var definition = definitions[i];
                var minSize = Clamp(definition.Size, definition.EffectiveMin, definition.Max);
                var share = remaining * (definition.Length.Value / totalWeight);
                if (share + 0.01f < minSize)
                {
                    definition.Size = minSize;
                    remaining -= definition.Size;
                    unresolved[i] = false;
                    constrainedAny = true;
                    continue;
                }

                if (share > definition.Max + 0.01f)
                {
                    definition.Size = definition.Max;
                    remaining -= definition.Size;
                    unresolved[i] = false;
                    constrainedAny = true;
                }
            }

            if (constrainedAny)
            {
                continue;
            }

            for (var i = 0; i < definitions.Count; i++)
            {
                if (!unresolved[i])
                {
                    continue;
                }

                var definition = definitions[i];
                definition.Size = Clamp(
                    remaining * (definition.Length.Value / totalWeight),
                    definition.EffectiveMin,
                    definition.Max);
            }

            return;
        }
    }

    private static bool ApplyChildRequirement(
        IReadOnlyList<DefinitionSnapshot> definitions,
        int start,
        int span,
        float requiredSize,
        bool hasFiniteConstraint)
    {
        var startTicks = Stopwatch.GetTimestamp();
        var changed = false;
        IncrementAggregate(ref _diagApplyChildRequirementCallCount);

        try
        {
        if (requiredSize <= 0f || float.IsNaN(requiredSize) || float.IsInfinity(requiredSize))
        {
            return false;
        }

        ApplyContentRequirement(definitions, start, span, requiredSize, hasFiniteConstraint);

        var end = Math.Min(definitions.Count, start + span);
        var current = 0f;
        for (var i = start; i < end; i++)
        {
            current += definitions[i].Size;
        }

        var extra = requiredSize - current;
        if (extra <= 0f)
        {
            return false;
        }

        if (hasFiniteConstraint && HasDefinitionType(definitions, start, end, static definition => definition.Length.IsStar))
        {
            IncrementAggregate(ref _diagApplyChildRequirementFiniteStarConstraintCount);
            extra = DistributeExtraSize(definitions, start, end, extra, static definition => definition.Length.IsAuto, ref changed);
            return changed;
        }

        extra = DistributeExtraSize(definitions, start, end, extra, static definition => definition.Length.IsAuto, ref changed);
        extra = DistributeExtraSize(definitions, start, end, extra, static definition => !definition.Length.IsPixel, ref changed, useStarWeights: true);
        var fallback = definitions[end - 1];
        var fallbackPreviousSize = fallback.Size;
        fallback.Size = Clamp(fallback.Size + extra, fallback.EffectiveMin, fallback.Max);
        return changed || !AreFloatsClose(fallbackPreviousSize, fallback.Size);
        }
        finally
        {
            AddAggregate(ref _diagApplyChildRequirementElapsedTicks, Stopwatch.GetTimestamp() - startTicks);
            if (changed)
            {
                IncrementAggregate(ref _diagApplyChildRequirementChangedCount);
            }
            else
            {
                IncrementAggregate(ref _diagApplyChildRequirementNoOpCount);
            }
        }
    }

    private static void ApplyContentRequirement(
        IReadOnlyList<DefinitionSnapshot> definitions,
        int start,
        int span,
        float requiredSize,
        bool hasFiniteConstraint)
    {
        var end = Math.Min(definitions.Count, start + span);
        var current = 0f;
        for (var i = start; i < end; i++)
        {
            current += definitions[i].ContentSize;
        }

        var extra = requiredSize - current;
        if (extra <= 0f)
        {
            return;
        }

        if (hasFiniteConstraint && HasDefinitionType(definitions, start, end, static definition => definition.Length.IsStar))
        {
            extra = DistributeExtraContentSize(definitions, start, end, extra, static definition => definition.Length.IsAuto);
            if (extra <= 0.01f)
            {
                return;
            }
        }
        else
        {
            extra = DistributeExtraContentSize(definitions, start, end, extra, static definition => definition.Length.IsAuto);
            extra = DistributeExtraContentSize(definitions, start, end, extra, static definition => !definition.Length.IsPixel, useStarWeights: true);
            if (extra <= 0.01f)
            {
                return;
            }
        }

        definitions[end - 1].RecordContentSize(definitions[end - 1].ContentSize + extra);
    }

    private static void FinalizeDefinitionSizes(IReadOnlyList<DefinitionSnapshot> definitions, float available)
    {
        ResolveDefinitionSizes(definitions, available);
    }

    private static void NormalizeDefinitionOverflow(IReadOnlyList<DefinitionSnapshot> definitions, float available)
    {
        IncrementAggregate(ref _diagNormalizeDefinitionOverflowCallCount);
        if (float.IsInfinity(available) || float.IsNaN(available) || available < 0f)
        {
            return;
        }

        var total = SumSizes(definitions);
        var overflow = total - available;
        if (overflow <= 0f)
        {
            return;
        }

        IncrementAggregate(ref _diagNormalizeDefinitionOverflowTriggeredCount);
        overflow = ReduceOverflow(definitions, overflow, static definition => definition.Length.IsStar);
        overflow = ReduceOverflow(definitions, overflow, static definition => definition.Length.IsAuto);
        ReduceOverflow(definitions, overflow, static definition => definition.Length.IsPixel);
    }

    private static float ReduceOverflow(
        IReadOnlyList<DefinitionSnapshot> definitions,
        float overflow,
        Predicate<DefinitionSnapshot> match)
    {
        IncrementAggregate(ref _diagReduceOverflowCallCount);
        if (overflow <= 0f)
        {
            return 0f;
        }

        var shrinkableTotal = 0f;
        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            if (!match(definition))
            {
                continue;
            }

            shrinkableTotal += MathF.Max(0f, definition.Size - definition.EffectiveMin);
        }

        if (shrinkableTotal <= 0f)
        {
            return overflow;
        }

        var shrinkTarget = MathF.Min(overflow, shrinkableTotal);
        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            if (!match(definition))
            {
                continue;
            }

            var shrinkable = MathF.Max(0f, definition.Size - definition.EffectiveMin);
            if (shrinkable <= 0f)
            {
                continue;
            }

            var portion = shrinkable / shrinkableTotal;
            definition.Size = Clamp(definition.Size - (shrinkTarget * portion), definition.EffectiveMin, definition.Max);
        }

        return overflow - shrinkTarget;
    }

    private static void FillOffsets(IReadOnlyList<DefinitionSnapshot> definitions, float origin, ref float[] offsets)
    {
        if (offsets.Length != definitions.Count)
        {
            offsets = new float[definitions.Count];
        }

        var current = origin;
        for (var i = 0; i < definitions.Count; i++)
        {
            offsets[i] = current;
            current += definitions[i].Size;
        }
    }

    private static float SumSizes(IReadOnlyList<DefinitionSnapshot> definitions)
    {
        var total = 0f;
        for (var i = 0; i < definitions.Count; i++)
        {
            total += definitions[i].Size;
        }

        return total;
    }

    private static float SumRange(IReadOnlyList<DefinitionSnapshot> definitions, int start, int span)
    {
        var end = Math.Min(definitions.Count, start + span);
        var total = 0f;
        for (var i = start; i < end; i++)
        {
            total += definitions[i].Size;
        }

        return total;
    }

    private static bool RequiresAutoMeasurement(IReadOnlyList<DefinitionSnapshot> definitions, int start, int span)
    {
        var end = Math.Min(definitions.Count, start + span);
        for (var i = start; i < end; i++)
        {
            if (definitions[i].Length.IsAuto)
            {
                return true;
            }
        }

        return false;
    }

    private static void CopySizesToBuffer(IReadOnlyList<DefinitionSnapshot> definitions, ref float[] buffer)
    {
        if (buffer.Length != definitions.Count)
        {
            buffer = new float[definitions.Count];
        }

        for (var i = 0; i < definitions.Count; i++)
        {
            buffer[i] = definitions[i].Size;
        }
    }

    private static bool AreDefinitionSizesClose(IReadOnlyList<DefinitionSnapshot> definitions, float[] previousSizes)
    {
        if (definitions.Count != previousSizes.Length)
        {
            return false;
        }

        for (var i = 0; i < definitions.Count; i++)
        {
            if (!AreFloatsClose(definitions[i].Size, previousSizes[i]))
            {
                return false;
            }
        }

        return true;
    }

    private void EnsureChildMeasureRecordCapacity(int capacity)
    {
        if (_firstPassMeasureRecords.Length >= capacity)
        {
            if (_cachedChildMeasureStates.Length >= capacity &&
                _cachedChildArrangeStates.Length >= capacity)
            {
                return;
            }
        }

        if (_firstPassMeasureRecords.Length < capacity)
        {
            _firstPassMeasureRecords = new FirstPassMeasureRecord[capacity];
        }

        if (_cachedChildMeasureStates.Length < capacity)
        {
            _cachedChildMeasureStates = new CachedChildMeasureState[capacity];
        }

        if (_cachedChildArrangeStates.Length < capacity)
        {
            _cachedChildArrangeStates = new CachedChildArrangeState[capacity];
        }
    }

    private ChildLayoutMetadata[] PrepareChildLayoutMetadata(
        IReadOnlyList<DefinitionSnapshot> rows,
        IReadOnlyList<DefinitionSnapshot> columns)
    {
        var startTicks = Stopwatch.GetTimestamp();
        var refreshedEntryCount = 0;
        var reusedEntryCount = 0;
        IncrementAggregate(ref _diagPrepareChildLayoutMetadataCallCount);
        var frameworkChildCount = CountFrameworkChildren();
        AddAggregate(ref _diagChildLayoutMetadataFrameworkChildCount, frameworkChildCount);
        if (_childLayoutMetadataDirty || _childLayoutMetadataCache.Length != frameworkChildCount)
        {
            IncrementAggregate(ref _diagChildLayoutMetadataCacheRefreshCount);
        }

        if (_childLayoutMetadataCache.Length != frameworkChildCount)
        {
            _childLayoutMetadataCache = new ChildLayoutMetadata[frameworkChildCount];
            _childLayoutMetadataDirty = true;
        }

        var metadataIndex = 0;
        foreach (var child in Children)
        {
            if (child is not FrameworkElement frameworkChild)
            {
                continue;
            }

            ref var metadata = ref _childLayoutMetadataCache[metadataIndex];
            if (_childLayoutMetadataDirty || !ReferenceEquals(metadata.Child, frameworkChild))
            {
                refreshedEntryCount++;
                var cell = NormalizeCell(child, rows.Count, columns.Count);
                metadata = new ChildLayoutMetadata(
                    frameworkChild,
                    cell,
                    HasAutoDefinition(columns, cell.Column, cell.ColumnSpan),
                    HasAutoDefinition(rows, cell.Row, cell.RowSpan),
                    HasStarDefinition(columns, cell.Column, cell.ColumnSpan),
                    HasStarDefinition(rows, cell.Row, cell.RowSpan),
                    HasExplicitSize(frameworkChild.Width),
                    HasExplicitSize(frameworkChild.Height));
            }
            else
            {
                reusedEntryCount++;
                metadata = metadata with
                {
                    HasExplicitWidth = HasExplicitSize(frameworkChild.Width),
                    HasExplicitHeight = HasExplicitSize(frameworkChild.Height)
                };
            }

            metadataIndex++;
        }

        _childLayoutMetadataDirty = false;
        AddAggregate(ref _diagChildLayoutMetadataEntryRefreshCount, refreshedEntryCount);
        AddAggregate(ref _diagChildLayoutMetadataEntryReuseCount, reusedEntryCount);
        AddAggregate(ref _diagPrepareChildLayoutMetadataElapsedTicks, Stopwatch.GetTimestamp() - startTicks);
        return _childLayoutMetadataCache;
    }

    private static Vector2 ResolveFirstPassAvailableSize(
        in ChildLayoutMetadata metadata,
        IReadOnlyList<DefinitionSnapshot> rows,
        IReadOnlyList<DefinitionSnapshot> columns,
        Vector2 parentAvailableSize)
    {
        var width = SumRange(columns, metadata.Cell.Column, metadata.Cell.ColumnSpan);
        if (metadata.HasAutoWidth)
        {
            width = metadata.HasExplicitWidth
                ? ResolveExplicitMeasureAvailable(metadata.Child.Width, metadata.Child.Margin.Horizontal)
                : metadata.HasStarWidth ? width : float.PositiveInfinity;
        }
        else if ((float.IsInfinity(parentAvailableSize.X) || float.IsNaN(parentAvailableSize.X)) && metadata.HasStarWidth)
        {
            width = float.PositiveInfinity;
        }

        var height = SumRange(rows, metadata.Cell.Row, metadata.Cell.RowSpan);
        if (metadata.HasAutoHeight)
        {
            height = metadata.HasExplicitHeight
                ? ResolveExplicitMeasureAvailable(metadata.Child.Height, metadata.Child.Margin.Vertical)
                : metadata.HasStarHeight ? height : float.PositiveInfinity;
        }
        else if ((float.IsInfinity(parentAvailableSize.Y) || float.IsNaN(parentAvailableSize.Y)) && metadata.HasStarHeight)
        {
            height = float.PositiveInfinity;
        }

        return new Vector2(width, height);
    }

    private static float ResolveExplicitMeasureAvailable(float explicitSize, float margin)
    {
        if (float.IsNaN(explicitSize))
        {
            return float.PositiveInfinity;
        }

        return MathF.Max(0f, explicitSize + margin);
    }

    private static bool ShouldReMeasureChild(in FirstPassMeasureRecord firstPassRecord, Vector2 finalAvailableSize)
    {
        return ShouldReMeasureAxis(
                   firstPassRecord.AvailableSize.X,
                   finalAvailableSize.X,
                   firstPassRecord.DesiredSize.X,
                   firstPassRecord.HasExplicitWidth,
                   firstPassRecord.WidthWasUnconstrained) ||
               ShouldReMeasureAxis(
                   firstPassRecord.AvailableSize.Y,
                   finalAvailableSize.Y,
                   firstPassRecord.DesiredSize.Y,
                   firstPassRecord.HasExplicitHeight,
                   firstPassRecord.HeightWasUnconstrained);
    }

    private static bool ShouldUseWidthPriorityFirstPass(
        ReadOnlySpan<ChildLayoutMetadata> childLayoutMetadata,
        int deferredSpanPass,
        float availableWidth)
    {
        if (float.IsInfinity(availableWidth) || float.IsNaN(availableWidth))
        {
            return false;
        }

        var sawStarWidthChild = false;
        var sawNonStarWidthChild = false;
        for (var childMeasureIndex = 0; childMeasureIndex < childLayoutMetadata.Length; childMeasureIndex++)
        {
            ref readonly var metadata = ref childLayoutMetadata[childMeasureIndex];
            if (ShouldDeferSpanMeasurement(metadata) != (deferredSpanPass == 1))
            {
                continue;
            }

            if (metadata.HasStarWidth)
            {
                sawStarWidthChild = true;
            }
            else
            {
                sawNonStarWidthChild = true;
            }

            if (sawStarWidthChild && sawNonStarWidthChild)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShouldMeasureInFirstPass(
        in ChildLayoutMetadata metadata,
        int deferredSpanPass,
        int widthPriorityPass,
        bool useWidthPriority)
    {
        if (ShouldDeferSpanMeasurement(metadata) != (deferredSpanPass == 1))
        {
            return false;
        }

        if (!useWidthPriority)
        {
            return true;
        }

        return metadata.HasStarWidth == (widthPriorityPass == 1);
    }

    private static bool ShouldReMeasureAxis(
        float firstAvailable,
        float finalAvailable,
        float desired,
        bool hasExplicitSize,
        bool firstPassWasUnconstrained)
    {
        if (hasExplicitSize || AreFloatsClose(firstAvailable, finalAvailable))
        {
            return false;
        }

        if (firstPassWasUnconstrained)
        {
            return finalAvailable + 0.01f < desired;
        }

        if (desired < firstAvailable - 0.01f && finalAvailable + 0.01f >= desired)
        {
            return false;
        }

        return true;
    }

    private int CountFrameworkChildren()
    {
        var count = 0;
        foreach (var child in Children)
        {
            if (child is FrameworkElement)
            {
                count++;
            }
        }

        return count;
    }

    private int CountNonFrameworkChildren()
    {
        var count = 0;
        foreach (var child in Children)
        {
            if (child is not FrameworkElement)
            {
                count++;
            }
        }

        return count;
    }

    private static bool HasAutoDefinition(IReadOnlyList<DefinitionSnapshot> definitions, int start, int span)
    {
        var end = Math.Min(definitions.Count, start + span);
        for (var i = start; i < end; i++)
        {
            if (definitions[i].Length.IsAuto)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasStarDefinition(IReadOnlyList<DefinitionSnapshot> definitions, int start, int span)
    {
        var end = Math.Min(definitions.Count, start + span);
        for (var i = start; i < end; i++)
        {
            if (definitions[i].Length.IsStar)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasExplicitSize(float value)
    {
        return !float.IsNaN(value);
    }

    private static bool ShouldDeferRowSpanMeasurement(in ChildLayoutMetadata metadata)
    {
        return metadata.Cell.RowSpan > 1;
    }

    private static bool ShouldDeferSpanMeasurement(in ChildLayoutMetadata metadata)
    {
        return metadata.Cell.RowSpan > 1 || metadata.Cell.ColumnSpan > 1;
    }

    private void InvalidateChildLayoutMetadataCache()
    {
        IncrementAggregate(ref _diagChildLayoutMetadataInvalidationCount);
        _childLayoutMetadataDirty = true;
    }

    private Vector2 MeasureChildOrReuseCachedState(
        int childMeasureIndex,
        in ChildLayoutMetadata metadata,
        Vector2 availableSize)
    {
        var startTicks = Stopwatch.GetTimestamp();
        var missCategory = 0;
        IncrementAggregate(ref _diagMeasureChildCallCount);

        try
        {
        var hasCacheSlot = childMeasureIndex >= 0 && childMeasureIndex < _cachedChildMeasureStates.Length;
        var childNeedsMeasure = metadata.Child.NeedsMeasure;
        var hasValidCachedState = hasCacheSlot &&
            _cachedChildMeasureStates[childMeasureIndex].IsValid &&
            ReferenceEquals(_cachedChildMeasureStates[childMeasureIndex].Child, metadata.Child);

        if (!childNeedsMeasure &&
            hasCacheSlot &&
            CanReuseCachedMeasure(_cachedChildMeasureStates[childMeasureIndex], metadata.Child, availableSize))
        {
            IncrementAggregate(ref _diagMeasureChildCacheHitCount);
            return _cachedChildMeasureStates[childMeasureIndex].DesiredSize;
        }

        IncrementAggregate(ref _diagMeasureChildCacheMissCount);
        if (childNeedsMeasure)
        {
            IncrementAggregate(ref _diagMeasureChildMissNeedsMeasureCount);
            missCategory = 1;
        }
        else if (!hasValidCachedState)
        {
            IncrementAggregate(ref _diagMeasureChildMissInvalidCacheCount);
            missCategory = 2;
        }
        else
        {
            IncrementAggregate(ref _diagMeasureChildMissReuseRejectedCount);
            missCategory = 3;
        }

        metadata.Child.Measure(availableSize);
        if (childMeasureIndex >= 0 && childMeasureIndex < _cachedChildMeasureStates.Length)
        {
            _cachedChildMeasureStates[childMeasureIndex] = new CachedChildMeasureState(
                metadata.Child,
                availableSize,
                metadata.Child.DesiredSize,
                metadata.HasExplicitWidth,
                metadata.HasExplicitHeight,
                float.IsPositiveInfinity(availableSize.X),
                float.IsPositiveInfinity(availableSize.Y),
                true);
        }

        return metadata.Child.DesiredSize;
        }
        finally
        {
            var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            AddAggregate(ref _diagMeasureChildElapsedTicks, elapsedTicks);
            if (metadata.Child is FrameworkElement frameworkChild)
            {
                if (missCategory == 1 && elapsedTicks > Volatile.Read(ref _diagMeasureChildNeedHottestElapsedTicks))
                {
                    Interlocked.Exchange(ref _diagMeasureChildNeedHottestElapsedTicks, elapsedTicks);
                    _diagMeasureChildNeedHottestPath = BuildDiagnosticElementPath(frameworkChild);
                }
                else if (missCategory == 3 && elapsedTicks > Volatile.Read(ref _diagMeasureChildRejectHottestElapsedTicks))
                {
                    Interlocked.Exchange(ref _diagMeasureChildRejectHottestElapsedTicks, elapsedTicks);
                    _diagMeasureChildRejectHottestPath = BuildDiagnosticElementPath(frameworkChild);
                }
            }
        }
    }

    private static string BuildDiagnosticElementPath(FrameworkElement element)
    {
        const int maxSegments = 6;
        var segments = new List<string>(maxSegments);
        FrameworkElement? current = element;
        while (current != null && segments.Count < maxSegments)
        {
            segments.Add(string.IsNullOrEmpty(current.Name)
                ? $"{current.GetType().Name}#"
                : $"{current.GetType().Name}#{current.Name}");
            current = current.VisualParent as FrameworkElement;
        }

        segments.Reverse();
        return string.Join(" > ", segments);
    }

    private bool CanReuseCachedArrange(int childArrangeIndex, FrameworkElement child, LayoutRect finalRect)
    {
        if (child.NeedsMeasure ||
            child.NeedsArrange ||
            childArrangeIndex < 0 ||
            childArrangeIndex >= _cachedChildArrangeStates.Length)
        {
            return false;
        }

        var cachedState = _cachedChildArrangeStates[childArrangeIndex];
        return cachedState.IsValid &&
               ReferenceEquals(cachedState.Child, child) &&
               AreRectsClose(cachedState.FinalRect, finalRect) &&
               AreSizesClose(cachedState.DesiredSize, child.DesiredSize);
    }

    private bool TryTranslateCachedArrange(int childArrangeIndex, FrameworkElement child, LayoutRect finalRect)
    {
        if (child.NeedsMeasure ||
            child.NeedsArrange ||
            childArrangeIndex < 0 ||
            childArrangeIndex >= _cachedChildArrangeStates.Length)
        {
            return false;
        }

        var cachedState = _cachedChildArrangeStates[childArrangeIndex];
        if (!cachedState.IsValid ||
            !ReferenceEquals(cachedState.Child, child) ||
            !AreSizesClose(cachedState.DesiredSize, child.DesiredSize) ||
            !AreFloatsClose(cachedState.FinalRect.Width, finalRect.Width) ||
            !AreFloatsClose(cachedState.FinalRect.Height, finalRect.Height) ||
            (AreFloatsClose(cachedState.FinalRect.X, finalRect.X) &&
             AreFloatsClose(cachedState.FinalRect.Y, finalRect.Y)))
        {
            return false;
        }

        if (!child.TryTranslateArrangedSubtree(finalRect))
        {
            return false;
        }

        child.InvalidateVisual();
        _cachedChildArrangeStates[childArrangeIndex] = new CachedChildArrangeState(
            child,
            finalRect,
            child.DesiredSize,
            true);
        return true;
    }

    private static bool CanReuseCachedMeasure(
        in CachedChildMeasureState cachedState,
        FrameworkElement child,
        Vector2 availableSize)
    {
        if (!cachedState.IsValid || !ReferenceEquals(cachedState.Child, child))
        {
            return false;
        }

        return child.CanReuseMeasureForAvailableSizeChangeForParentLayout(
            cachedState.AvailableSize,
            availableSize);
    }

    private bool TrySimulateMeasureWithCachedChildren(
        ReadOnlySpan<ChildLayoutMetadata> childLayoutMetadata,
        Vector2 availableSize,
        List<DefinitionSnapshot> columns,
        List<DefinitionSnapshot> rows,
        out Vector2 desiredSize)
    {
        desiredSize = Vector2.Zero;
        var firstPassRecords = new FirstPassMeasureRecord[childLayoutMetadata.Length];

        ResolveDefinitionSizes(columns, availableSize.X);
        ResolveDefinitionSizes(rows, availableSize.Y);

        for (var deferredSpanPass = 0; deferredSpanPass < 2; deferredSpanPass++)
        {
            var useWidthPriority = ShouldUseWidthPriorityFirstPass(childLayoutMetadata, deferredSpanPass, availableSize.X);
            for (var widthPriorityPass = 0; widthPriorityPass < (useWidthPriority ? 2 : 1); widthPriorityPass++)
            {
                var measuredAnyChild = false;
                for (var childMeasureIndex = 0; childMeasureIndex < childLayoutMetadata.Length; childMeasureIndex++)
                {
                    ref readonly var metadata = ref childLayoutMetadata[childMeasureIndex];
                    if (!ShouldMeasureInFirstPass(metadata, deferredSpanPass, widthPriorityPass, useWidthPriority))
                    {
                        continue;
                    }

                    var firstPassAvailable = ResolveFirstPassAvailableSize(metadata, rows, columns, availableSize);
                    if (!CanReuseCachedChildMeasureState(childMeasureIndex, metadata.Child, firstPassAvailable, out var cachedDesiredSize))
                    {
                        return false;
                    }

                    firstPassRecords[childMeasureIndex] = new FirstPassMeasureRecord(
                        metadata.Cell,
                        firstPassAvailable,
                        cachedDesiredSize,
                        metadata.HasExplicitWidth,
                        metadata.HasExplicitHeight,
                        float.IsPositiveInfinity(firstPassAvailable.X),
                        float.IsPositiveInfinity(firstPassAvailable.Y));
                    measuredAnyChild = true;

                    ApplyChildRequirement(
                        columns,
                        metadata.Cell.Column,
                        metadata.Cell.ColumnSpan,
                        cachedDesiredSize.X,
                        !float.IsInfinity(availableSize.X) && !float.IsNaN(availableSize.X));
                    ApplyChildRequirement(
                        rows,
                        metadata.Cell.Row,
                        metadata.Cell.RowSpan,
                        cachedDesiredSize.Y,
                        !float.IsInfinity(availableSize.Y) && !float.IsNaN(availableSize.Y));
                }

                if (useWidthPriority && widthPriorityPass == 0 && measuredAnyChild)
                {
                    FinalizeDefinitionSizes(columns, availableSize.X);
                    FinalizeDefinitionSizes(rows, availableSize.Y);
                }
            }
        }

        FinalizeDefinitionSizes(columns, availableSize.X);
        FinalizeDefinitionSizes(rows, availableSize.Y);

        for (var pass = 0; pass < 4; pass++)
        {
            var definitionsChanged = false;
            for (var deferredSpanPass = 0; deferredSpanPass < 2; deferredSpanPass++)
            {
                for (var childMeasureIndex = 0; childMeasureIndex < childLayoutMetadata.Length; childMeasureIndex++)
                {
                    ref readonly var metadata = ref childLayoutMetadata[childMeasureIndex];
                    if (ShouldDeferSpanMeasurement(metadata) != (deferredSpanPass == 1))
                    {
                        continue;
                    }

                    var childAvailable = new Vector2(
                        SumRange(columns, metadata.Cell.Column, metadata.Cell.ColumnSpan),
                        SumRange(rows, metadata.Cell.Row, metadata.Cell.RowSpan));
                    if (!ShouldReMeasureChild(firstPassRecords[childMeasureIndex], childAvailable))
                    {
                        continue;
                    }

                    if (!CanReuseCachedChildMeasureState(childMeasureIndex, metadata.Child, childAvailable, out var cachedDesiredSize))
                    {
                        return false;
                    }

                    definitionsChanged |= ApplyChildRequirement(
                        columns,
                        metadata.Cell.Column,
                        metadata.Cell.ColumnSpan,
                        cachedDesiredSize.X,
                        !float.IsInfinity(availableSize.X) && !float.IsNaN(availableSize.X));
                    definitionsChanged |= ApplyChildRequirement(
                        rows,
                        metadata.Cell.Row,
                        metadata.Cell.RowSpan,
                        cachedDesiredSize.Y,
                        !float.IsInfinity(availableSize.Y) && !float.IsNaN(availableSize.Y));
                }
            }

            if (!definitionsChanged)
            {
                desiredSize = new Vector2(SumSizes(columns), SumSizes(rows));
                return true;
            }

            FinalizeDefinitionSizes(columns, availableSize.X);
            FinalizeDefinitionSizes(rows, availableSize.Y);
        }

        desiredSize = new Vector2(SumSizes(columns), SumSizes(rows));
        return true;
    }

    private bool CanReuseCachedChildMeasureState(
        int childMeasureIndex,
        FrameworkElement child,
        Vector2 availableSize,
        out Vector2 desiredSize)
    {
        desiredSize = Vector2.Zero;
        if (!child.NeedsMeasure &&
            child.IsMeasureValidForTests &&
            child.CanReuseMeasureForAvailableSizeChangeForParentLayout(child.PreviousAvailableSizeForTests, availableSize))
        {
            desiredSize = child.DesiredSize;
            if (childMeasureIndex >= 0 && childMeasureIndex < _cachedChildMeasureStates.Length)
            {
                _cachedChildMeasureStates[childMeasureIndex] = new CachedChildMeasureState(
                    child,
                    availableSize,
                    desiredSize,
                    !float.IsNaN(child.Width),
                    !float.IsNaN(child.Height),
                    float.IsPositiveInfinity(availableSize.X),
                    float.IsPositiveInfinity(availableSize.Y),
                    true);
            }

            return true;
        }

        if (child.NeedsMeasure || childMeasureIndex < 0 || childMeasureIndex >= _cachedChildMeasureStates.Length)
        {
            return false;
        }

        var cachedState = _cachedChildMeasureStates[childMeasureIndex];
        if (!cachedState.IsValid || !ReferenceEquals(cachedState.Child, child))
        {
            return false;
        }

        if (!child.CanReuseMeasureForAvailableSizeChangeForParentLayout(cachedState.AvailableSize, availableSize))
        {
            return false;
        }

        desiredSize = cachedState.DesiredSize;
        return true;
    }

    private bool TryReconcileDescendantMeasureInvalidation(Vector2 availableSize, out Vector2 desiredSize)
    {
        desiredSize = Vector2.Zero;

        var columns = PrepareColumnSnapshots(_measureColumns);
        var rows = PrepareRowSnapshots(_measureRows);
        var childLayoutMetadata = PrepareChildLayoutMetadata(rows, columns);

        ResolveDefinitionSizes(columns, availableSize.X);
        ResolveDefinitionSizes(rows, availableSize.Y);
        EnsureChildMeasureRecordCapacity(childLayoutMetadata.Length);

        for (var deferredSpanPass = 0; deferredSpanPass < 2; deferredSpanPass++)
        {
            var useWidthPriority = ShouldUseWidthPriorityFirstPass(childLayoutMetadata, deferredSpanPass, availableSize.X);
            for (var widthPriorityPass = 0; widthPriorityPass < (useWidthPriority ? 2 : 1); widthPriorityPass++)
            {
                var measuredAnyChild = false;
                for (var childMeasureIndex = 0; childMeasureIndex < childLayoutMetadata.Length; childMeasureIndex++)
                {
                    ref readonly var metadata = ref childLayoutMetadata[childMeasureIndex];
                    if (!ShouldMeasureInFirstPass(metadata, deferredSpanPass, widthPriorityPass, useWidthPriority))
                    {
                        continue;
                    }

                    var firstPassAvailable = ResolveFirstPassAvailableSize(metadata, rows, columns, availableSize);
                    var measuredDesiredSize = MeasureChildOrReuseCachedState(childMeasureIndex, metadata, firstPassAvailable);
                    if (metadata.Child.NeedsMeasure || !metadata.Child.IsMeasureValidForTests)
                    {
                        return false;
                    }

                    _firstPassMeasureRecords[childMeasureIndex] = new FirstPassMeasureRecord(
                        metadata.Cell,
                        firstPassAvailable,
                        measuredDesiredSize,
                        metadata.HasExplicitWidth,
                        metadata.HasExplicitHeight,
                        float.IsPositiveInfinity(firstPassAvailable.X),
                        float.IsPositiveInfinity(firstPassAvailable.Y));
                    measuredAnyChild = true;

                    ApplyChildRequirement(
                        columns,
                        metadata.Cell.Column,
                        metadata.Cell.ColumnSpan,
                        measuredDesiredSize.X,
                        !float.IsInfinity(availableSize.X) && !float.IsNaN(availableSize.X));
                    ApplyChildRequirement(
                        rows,
                        metadata.Cell.Row,
                        metadata.Cell.RowSpan,
                        measuredDesiredSize.Y,
                        !float.IsInfinity(availableSize.Y) && !float.IsNaN(availableSize.Y));
                }

                if (useWidthPriority && widthPriorityPass == 0 && measuredAnyChild)
                {
                    FinalizeDefinitionSizes(columns, availableSize.X);
                    FinalizeDefinitionSizes(rows, availableSize.Y);
                }
            }
        }

        FinalizeDefinitionSizes(columns, availableSize.X);
        FinalizeDefinitionSizes(rows, availableSize.Y);

        for (var pass = 0; pass < 4; pass++)
        {
            var definitionsChanged = false;
            for (var deferredSpanPass = 0; deferredSpanPass < 2; deferredSpanPass++)
            {
                for (var childMeasureIndex = 0; childMeasureIndex < childLayoutMetadata.Length; childMeasureIndex++)
                {
                    ref readonly var metadata = ref childLayoutMetadata[childMeasureIndex];
                    if (ShouldDeferSpanMeasurement(metadata) != (deferredSpanPass == 1))
                    {
                        continue;
                    }

                    var childAvailable = new Vector2(
                        SumRange(columns, metadata.Cell.Column, metadata.Cell.ColumnSpan),
                        SumRange(rows, metadata.Cell.Row, metadata.Cell.RowSpan));
                    if (!ShouldReMeasureChild(_firstPassMeasureRecords[childMeasureIndex], childAvailable))
                    {
                        continue;
                    }

                    var measuredDesiredSize = MeasureChildOrReuseCachedState(childMeasureIndex, metadata, childAvailable);
                    if (metadata.Child.NeedsMeasure || !metadata.Child.IsMeasureValidForTests)
                    {
                        return false;
                    }

                    definitionsChanged |= ApplyChildRequirement(
                        columns,
                        metadata.Cell.Column,
                        metadata.Cell.ColumnSpan,
                        measuredDesiredSize.X,
                        !float.IsInfinity(availableSize.X) && !float.IsNaN(availableSize.X));
                    definitionsChanged |= ApplyChildRequirement(
                        rows,
                        metadata.Cell.Row,
                        metadata.Cell.RowSpan,
                        measuredDesiredSize.Y,
                        !float.IsInfinity(availableSize.Y) && !float.IsNaN(availableSize.Y));
                }
            }

            if (!definitionsChanged)
            {
                desiredSize = new Vector2(SumSizes(columns), SumSizes(rows));
                CopySizesToBuffer(columns, ref _measuredColumnSizes);
                CopySizesToBuffer(rows, ref _measuredRowSizes);
                return true;
            }

            FinalizeDefinitionSizes(columns, availableSize.X);
            FinalizeDefinitionSizes(rows, availableSize.Y);
        }

        desiredSize = new Vector2(SumSizes(columns), SumSizes(rows));
        CopySizesToBuffer(columns, ref _measuredColumnSizes);
        CopySizesToBuffer(rows, ref _measuredRowSizes);
        return true;
    }

    private bool HasSharedSizeDefinitions()
    {
        for (var i = 0; i < _columnDefinitions.Count; i++)
        {
            if (!string.IsNullOrEmpty(_columnDefinitions[i].SharedSizeGroup))
            {
                return true;
            }
        }

        for (var i = 0; i < _rowDefinitions.Count; i++)
        {
            if (!string.IsNullOrEmpty(_rowDefinitions[i].SharedSizeGroup))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasFiniteStarAxisSizeChange(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
    {
        return HasFiniteStarDefinitionSizeChange(previousAvailableSize.X, nextAvailableSize.X, isColumnAxis: true) ||
               HasFiniteStarDefinitionSizeChange(previousAvailableSize.Y, nextAvailableSize.Y, isColumnAxis: false);
    }

    private bool HasFiniteStarDefinitionSizeChange(float previousAvailable, float nextAvailable, bool isColumnAxis)
    {
        if (!IsFiniteAvailableSizeChange(previousAvailable, nextAvailable))
        {
            return false;
        }

        if (isColumnAxis)
        {
            for (var i = 0; i < _columnDefinitions.Count; i++)
            {
                if (_columnDefinitions[i].Width.IsStar)
                {
                    return true;
                }
            }

            return false;
        }

        for (var i = 0; i < _rowDefinitions.Count; i++)
        {
            if (_rowDefinitions[i].Height.IsStar)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFiniteAvailableSizeChange(float previousAvailable, float nextAvailable)
    {
        return !float.IsNaN(previousAvailable) &&
               !float.IsNaN(nextAvailable) &&
               !float.IsInfinity(previousAvailable) &&
               !float.IsInfinity(nextAvailable) &&
               !AreFloatsClose(previousAvailable, nextAvailable);
    }

    private bool IsGridDescendant(UIElement element)
    {
        for (UIElement? current = element; current != null; current = current.GetInvalidationParent())
        {
            if (ReferenceEquals(current, this))
            {
                return !ReferenceEquals(element, this);
            }
        }

        return false;
    }

    private Vector2 GetCurrentMeasuredDesiredSize()
    {
        return new Vector2(
            MathF.Max(0f, DesiredSize.X - Margin.Horizontal),
            MathF.Max(0f, DesiredSize.Y - Margin.Vertical));
    }

    private static bool AreSizesClose(Vector2 first, Vector2 second)
    {
        return AreFloatsClose(first.X, second.X) && AreFloatsClose(first.Y, second.Y);
    }

    private static bool AreRectsClose(LayoutRect first, LayoutRect second)
    {
        return AreFloatsClose(first.X, second.X) &&
               AreFloatsClose(first.Y, second.Y) &&
               AreFloatsClose(first.Width, second.Width) &&
               AreFloatsClose(first.Height, second.Height);
    }

    private static bool AreFloatsClose(float first, float second)
    {
        if (float.IsNaN(first) || float.IsNaN(second))
        {
            return float.IsNaN(first) && float.IsNaN(second);
        }

        if (float.IsInfinity(first) || float.IsInfinity(second))
        {
            return float.IsPositiveInfinity(first) == float.IsPositiveInfinity(second) &&
                   float.IsNegativeInfinity(first) == float.IsNegativeInfinity(second);
        }

        return MathF.Abs(first - second) < 0.01f;
    }

    private static void PrepareDefinitionSnapshots(List<DefinitionSnapshot> target, int definitionCount)
    {
        var requiredCount = definitionCount == 0 ? 1 : definitionCount;
        while (target.Count < requiredCount)
        {
            target.Add(new DefinitionSnapshot(GridLength.Star, 0f, float.PositiveInfinity, null));
        }

        if (target.Count > requiredCount)
        {
            target.RemoveRange(requiredCount, target.Count - requiredCount);
        }
    }

    private static CellInfo NormalizeCell(UIElement child, int rowCount, int columnCount)
    {
        var row = CoerceIndex(GetRow(child), rowCount);
        var column = CoerceIndex(GetColumn(child), columnCount);
        var rowSpan = CoerceSpan(GetRowSpan(child), row, rowCount);
        var columnSpan = CoerceSpan(GetColumnSpan(child), column, columnCount);
        return new CellInfo(row, column, rowSpan, columnSpan);
    }

    private static int CoerceIndex(int requested, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        if (requested < 0)
        {
            return 0;
        }

        if (requested >= count)
        {
            return count - 1;
        }

        return requested;
    }

    private static int CoerceSpan(int requested, int startIndex, int count)
    {
        if (count <= 0)
        {
            return 1;
        }

        var maxSpan = count - startIndex;
        if (requested <= 0)
        {
            return 1;
        }

        if (requested > maxSpan)
        {
            return maxSpan;
        }

        return requested;
    }

    private static float Clamp(float value, float min, float max)
    {
        if (float.IsNaN(value))
        {
            return min;
        }

        if (float.IsPositiveInfinity(value))
        {
            return max;
        }

        if (float.IsNegativeInfinity(value))
        {
            return min;
        }

        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private void RefreshSharedSizeScopeState()
    {
        IncrementAggregate(ref _diagSharedSizeScopeRefreshCallCount);
        var scopeOwner = FindSharedSizeScopeOwner();
        if (scopeOwner == null)
        {
            IncrementAggregate(ref _diagSharedSizeScopeMissCount);
        }
        else
        {
            IncrementAggregate(ref _diagSharedSizeScopeHitCount);
        }

        var nextScopeState = scopeOwner != null
            ? SharedSizeScopes.GetValue(scopeOwner, static _ => new SharedSizeScopeState())
            : null;

        if (ReferenceEquals(_sharedSizeScopeState, nextScopeState))
        {
            return;
        }

        IncrementAggregate(ref _diagSharedSizeScopeChangedCount);
        _sharedSizeScopeState?.RemoveGrid(this);
        _sharedSizeScopeState = nextScopeState;
    }

    private void DetachFromSharedSizeScope()
    {
        _sharedSizeScopeState?.RemoveGrid(this);
        _sharedSizeScopeState = null;
    }

    private UIElement? FindSharedSizeScopeOwner()
    {
        for (UIElement? current = this; current != null; current = current.GetInvalidationParent())
        {
            if (GetIsSharedSizeScope(current))
            {
                return current;
            }
        }

        return null;
    }

    private void ApplySharedSizes(IReadOnlyList<DefinitionSnapshot> definitions, bool isColumnAxis)
    {
        IncrementAggregate(ref _diagApplySharedSizesCallCount);
        if (_sharedSizeScopeState == null)
        {
            return;
        }

        for (var i = 0; i < definitions.Count; i++)
        {
            var group = definitions[i].SharedSizeGroup;
            if (string.IsNullOrEmpty(group))
            {
                continue;
            }

            IncrementAggregate(ref _diagApplySharedSizeDefinitionCount);
            definitions[i].ApplySharedSize(_sharedSizeScopeState.GetSharedSize(group, isColumnAxis));
        }
    }

    private void PublishSharedSizes(IReadOnlyList<DefinitionSnapshot> definitions, bool isColumnAxis)
    {
        IncrementAggregate(ref _diagPublishSharedSizesCallCount);
        if (_sharedSizeScopeState == null)
        {
            return;
        }

        for (var i = 0; i < definitions.Count; i++)
        {
            if (!string.IsNullOrEmpty(definitions[i].SharedSizeGroup))
            {
                IncrementAggregate(ref _diagPublishSharedSizeDefinitionCount);
            }
        }

        _sharedSizeScopeState.Publish(this, definitions, isColumnAxis);
    }

    private static void InvalidateSharedSizeScopeSubtree(UIElement root)
    {
        if (root is Grid grid)
        {
            grid.DetachFromSharedSizeScope();
            grid.InvalidateMeasure();
        }

        foreach (var child in root.GetVisualChildren())
        {
            InvalidateSharedSizeScopeSubtree(child);
        }
    }

    private static bool HasDefinitionType(
        IReadOnlyList<DefinitionSnapshot> definitions,
        int start,
        int end,
        Predicate<DefinitionSnapshot> match)
    {
        for (var i = start; i < end; i++)
        {
            if (match(definitions[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static float DistributeExtraSize(
        IReadOnlyList<DefinitionSnapshot> definitions,
        int start,
        int end,
        float extra,
        Predicate<DefinitionSnapshot> match,
        ref bool changed,
        bool useStarWeights = false)
    {
        IncrementAggregate(ref _diagDistributeExtraSizeCallCount);
        if (extra <= 0f)
        {
            return 0f;
        }

        while (extra > 0.01f)
        {
            var candidateCount = 0;
            var totalWeight = 0f;
            for (var i = start; i < end; i++)
            {
                var definition = definitions[i];
                if (!match(definition) || definition.Size >= definition.Max - 0.01f)
                {
                    continue;
                }

                candidateCount++;
                totalWeight += useStarWeights && definition.Length.IsStar
                    ? MathF.Max(0.0001f, definition.Length.Value)
                    : 1f;
            }

            if (candidateCount == 0 || totalWeight <= 0f)
            {
                return extra;
            }

            var remainingExtra = extra;
            var remainingCount = candidateCount;
            var remainingWeight = totalWeight;
            var madeProgress = false;

            for (var i = start; i < end && remainingExtra > 0.01f; i++)
            {
                var definition = definitions[i];
                if (!match(definition) || definition.Size >= definition.Max - 0.01f)
                {
                    continue;
                }

                var weight = useStarWeights && definition.Length.IsStar
                    ? MathF.Max(0.0001f, definition.Length.Value)
                    : 1f;
                var share = useStarWeights
                    ? remainingExtra * (weight / remainingWeight)
                    : remainingExtra / remainingCount;
                var capacity = MathF.Max(0f, definition.Max - definition.Size);
                var added = MathF.Min(share, capacity);
                if (added > 0f)
                {
                    var previousSize = definition.Size;
                    definition.Size = Clamp(definition.Size + added, definition.EffectiveMin, definition.Max);
                    changed |= !AreFloatsClose(previousSize, definition.Size);
                    remainingExtra -= added;
                    madeProgress = true;
                }

                remainingCount--;
                remainingWeight -= weight;
            }

            extra = remainingExtra;
            if (!madeProgress)
            {
                return extra;
            }
        }

        return extra;
    }

    private static float DistributeExtraContentSize(
        IReadOnlyList<DefinitionSnapshot> definitions,
        int start,
        int end,
        float extra,
        Predicate<DefinitionSnapshot> match,
        bool useStarWeights = false)
    {
        if (extra <= 0f)
        {
            return 0f;
        }

        while (extra > 0.01f)
        {
            var candidateCount = 0;
            var totalWeight = 0f;
            for (var i = start; i < end; i++)
            {
                var definition = definitions[i];
                if (!match(definition) || definition.ContentSize >= definition.Max - 0.01f)
                {
                    continue;
                }

                candidateCount++;
                totalWeight += useStarWeights && definition.Length.IsStar
                    ? MathF.Max(0.0001f, definition.Length.Value)
                    : 1f;
            }

            if (candidateCount == 0 || totalWeight <= 0f)
            {
                return extra;
            }

            var remainingExtra = extra;
            var remainingCount = candidateCount;
            var remainingWeight = totalWeight;
            var madeProgress = false;

            for (var i = start; i < end && remainingExtra > 0.01f; i++)
            {
                var definition = definitions[i];
                if (!match(definition) || definition.ContentSize >= definition.Max - 0.01f)
                {
                    continue;
                }

                var weight = useStarWeights && definition.Length.IsStar
                    ? MathF.Max(0.0001f, definition.Length.Value)
                    : 1f;
                var share = useStarWeights
                    ? remainingExtra * (weight / remainingWeight)
                    : remainingExtra / remainingCount;
                var capacity = MathF.Max(0f, definition.Max - definition.ContentSize);
                var added = MathF.Min(share, capacity);
                if (added > 0f)
                {
                    definition.RecordContentSize(definition.ContentSize + added);
                    remainingExtra -= added;
                    madeProgress = true;
                }

                remainingCount--;
                remainingWeight -= weight;
            }

            extra = remainingExtra;
            if (!madeProgress)
            {
                return extra;
            }
        }

        return extra;
    }

    private sealed class DefinitionSnapshot
    {
        public DefinitionSnapshot(GridLength length, float min, float max, string? sharedSizeGroup)
        {
            Reset(length, min, max, sharedSizeGroup);
        }

        public GridLength Length { get; private set; }

        public float Min { get; private set; }

        public float Max { get; private set; }

        public string? SharedSizeGroup { get; private set; }

        public float SharedSize { get; private set; }

        public float ContentSize { get; private set; }

        public float EffectiveMin => MathF.Max(Min, SharedSize);

        public float Size { get; set; }

        public void Reset(GridLength length, float min, float max, string? sharedSizeGroup)
        {
            Length = length;
            Min = min;
            Max = max;
            SharedSizeGroup = sharedSizeGroup;
            SharedSize = 0f;
            Size = 0f;
            ContentSize = length.IsPixel
                ? Clamp(length.Value, min, max)
                : Clamp(min, min, max);
        }

        public void ApplySharedSize(float sharedSize)
        {
            SharedSize = MathF.Max(0f, sharedSize);
            if (Size < EffectiveMin)
            {
                Size = EffectiveMin;
            }
        }

        public void RecordContentSize(float size)
        {
            ContentSize = Clamp(MathF.Max(ContentSize, size), Min, Max);
        }
    }

    private readonly struct CellInfo
    {
        public CellInfo(int row, int column, int rowSpan, int columnSpan)
        {
            Row = row;
            Column = column;
            RowSpan = rowSpan;
            ColumnSpan = columnSpan;
        }

        public int Row { get; }

        public int Column { get; }

        public int RowSpan { get; }

        public int ColumnSpan { get; }
    }

    private readonly record struct ChildLayoutMetadata(
        FrameworkElement Child,
        CellInfo Cell,
        bool HasAutoWidth,
        bool HasAutoHeight,
        bool HasStarWidth,
        bool HasStarHeight,
        bool HasExplicitWidth,
        bool HasExplicitHeight);

    private readonly record struct FirstPassMeasureRecord(
        CellInfo Cell,
        Vector2 AvailableSize,
        Vector2 DesiredSize,
        bool HasExplicitWidth,
        bool HasExplicitHeight,
        bool WidthWasUnconstrained,
        bool HeightWasUnconstrained);

    private readonly record struct CachedChildMeasureState(
        FrameworkElement Child,
        Vector2 AvailableSize,
        Vector2 DesiredSize,
        bool HasExplicitWidth,
        bool HasExplicitHeight,
        bool WidthWasUnconstrained,
        bool HeightWasUnconstrained,
        bool IsValid);

    private readonly record struct CachedChildArrangeState(
        FrameworkElement Child,
        LayoutRect FinalRect,
        Vector2 DesiredSize,
        bool IsValid);

    internal GridRuntimeDiagnosticsSnapshot GetGridSnapshotForDiagnostics()
    {
        return new GridRuntimeDiagnosticsSnapshot(
            ShowGridLines,
            _columnDefinitions.Count,
            _rowDefinitions.Count,
            Children.Count,
            CountFrameworkChildren(),
            DesiredSize.X,
            DesiredSize.Y,
            RenderSize.X,
            RenderSize.Y,
            ActualWidth,
            ActualHeight,
            PreviousAvailableSizeForTests.X,
            PreviousAvailableSizeForTests.Y,
            _measuredColumnSizes.Length,
            _measuredRowSizes.Length,
            _columnOffsets.Length,
            _rowOffsets.Length,
            _childLayoutMetadataCache.Length,
            _firstPassMeasureRecords.Length,
            _cachedChildMeasureStates.Length,
            _childLayoutMetadataDirty,
            _sharedSizeScopeState is not null,
            GetIsSharedSizeScope(this),
            MeasureCallCount,
            MeasureWorkCount,
            ArrangeCallCount,
            ArrangeWorkCount,
            TicksToMilliseconds(MeasureElapsedTicksForTests),
            TicksToMilliseconds(MeasureExclusiveElapsedTicksForTests),
            TicksToMilliseconds(ArrangeElapsedTicksForTests),
            IsMeasureValidForTests,
            IsArrangeValidForTests,
            MeasureInvalidationCount,
            ArrangeInvalidationCount,
                RenderInvalidationCount);
    }

    internal new static GridTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return CreateTelemetrySnapshot(reset: false);
    }

    internal static GridTelemetrySnapshot GetTelemetrySnapshotForDiagnostics()
    {
        return GetAggregateTelemetrySnapshotForDiagnostics();
    }

    internal new static GridTelemetrySnapshot GetTelemetryAndReset()
    {
        return CreateTelemetrySnapshot(reset: true);
    }

    internal static GridTimingSnapshot GetTimingSnapshotForTests()
    {
        return new GridTimingSnapshot(
            ReadAggregate(ref _measureOverrideElapsedTicks),
            ReadAggregate(ref _arrangeOverrideElapsedTicks));
    }

    internal static void ResetTimingForTests()
    {
        ResetAggregate(ref _measureOverrideElapsedTicks);
        ResetAggregate(ref _arrangeOverrideElapsedTicks);
    }

    private static GridTelemetrySnapshot CreateTelemetrySnapshot(bool reset)
    {
        return new GridTelemetrySnapshot(
            ReadOrReset(ref _diagMeasureCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _measureOverrideElapsedTicks, reset)),
            ReadOrReset(ref _diagMeasureChildCount, reset),
            ReadOrReset(ref _diagMeasureDeferredRowSpanChildCount, reset),
            ReadOrReset(ref _diagMeasureFirstPassChildCount, reset),
            ReadOrReset(ref _diagMeasureSecondPassChildCount, reset),
            ReadOrReset(ref _diagMeasureRemeasureCheckCount, reset),
            ReadOrReset(ref _diagMeasureRemeasureCount, reset),
            ReadOrReset(ref _diagMeasureRemeasureSkipCount, reset),
            ReadOrReset(ref _diagArrangeCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _arrangeOverrideElapsedTicks, reset)),
            ReadOrReset(ref _diagArrangeChildCount, reset),
            ReadOrReset(ref _diagArrangeSkippedChildCount, reset),
            ReadOrReset(ref _diagPrepareChildLayoutMetadataCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagPrepareChildLayoutMetadataElapsedTicks, reset)),
            ReadOrReset(ref _diagChildLayoutMetadataCacheRefreshCount, reset),
            ReadOrReset(ref _diagChildLayoutMetadataEntryRefreshCount, reset),
            ReadOrReset(ref _diagChildLayoutMetadataEntryReuseCount, reset),
            ReadOrReset(ref _diagChildLayoutMetadataFrameworkChildCount, reset),
            ReadOrReset(ref _diagChildLayoutMetadataInvalidationCount, reset),
            ReadOrReset(ref _diagMeasureChildCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagMeasureChildElapsedTicks, reset)),
            ReadOrReset(ref _diagMeasureChildCacheHitCount, reset),
            ReadOrReset(ref _diagMeasureChildCacheMissCount, reset),
            ReadOrReset(ref _diagMeasureChildMissNeedsMeasureCount, reset),
            ReadOrReset(ref _diagMeasureChildMissInvalidCacheCount, reset),
            ReadOrReset(ref _diagMeasureChildMissReuseRejectedCount, reset),
            ReadOrResetString(ref _diagMeasureChildNeedHottestPath, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagMeasureChildNeedHottestElapsedTicks, reset)),
            ReadOrResetString(ref _diagMeasureChildRejectHottestPath, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagMeasureChildRejectHottestElapsedTicks, reset)),
            ReadOrReset(ref _diagResolveDefinitionSizesCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagResolveDefinitionSizesElapsedTicks, reset)),
            ReadOrReset(ref _diagResolveDefinitionFiniteAvailableCount, reset),
            ReadOrReset(ref _diagResolveDefinitionInfiniteAvailableCount, reset),
            ReadOrReset(ref _diagResolveDefinitionNaNAvailableCount, reset),
            ReadOrReset(ref _diagApplyChildRequirementCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagApplyChildRequirementElapsedTicks, reset)),
            ReadOrReset(ref _diagApplyChildRequirementChangedCount, reset),
            ReadOrReset(ref _diagApplyChildRequirementNoOpCount, reset),
            ReadOrReset(ref _diagApplyChildRequirementFiniteStarConstraintCount, reset),
            ReadOrReset(ref _diagNormalizeDefinitionOverflowCallCount, reset),
            ReadOrReset(ref _diagNormalizeDefinitionOverflowTriggeredCount, reset),
            ReadOrReset(ref _diagDistributeExtraSizeCallCount, reset),
            ReadOrReset(ref _diagReduceOverflowCallCount, reset),
            ReadOrReset(ref _diagSharedSizeScopeRefreshCallCount, reset),
            ReadOrReset(ref _diagSharedSizeScopeHitCount, reset),
            ReadOrReset(ref _diagSharedSizeScopeMissCount, reset),
            ReadOrReset(ref _diagSharedSizeScopeChangedCount, reset),
            ReadOrReset(ref _diagApplySharedSizesCallCount, reset),
            ReadOrReset(ref _diagApplySharedSizeDefinitionCount, reset),
            ReadOrReset(ref _diagPublishSharedSizesCallCount, reset),
            ReadOrReset(ref _diagPublishSharedSizeDefinitionCount, reset));
    }

    private static string ReadOrResetString(ref string value, bool reset)
    {
        var result = value;
        if (reset)
        {
            value = "none";
        }

        return result;
    }

    private static void IncrementAggregate(ref long counter)
    {
        Interlocked.Increment(ref counter);
    }

    private static void AddAggregate(ref long counter, long value)
    {
        Interlocked.Add(ref counter, value);
    }

    private static long ReadAggregate(ref long counter)
    {
        return Interlocked.Read(ref counter);
    }

    private static long ResetAggregate(ref long counter)
    {
        return Interlocked.Exchange(ref counter, 0L);
    }

    private static long ReadOrReset(ref long counter, bool reset)
    {
        return reset ? ResetAggregate(ref counter) : ReadAggregate(ref counter);
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }

    private sealed class SharedSizeScopeState
    {
        private readonly Dictionary<string, SharedAxisState> _columnGroups = new(StringComparer.Ordinal);
        private readonly Dictionary<string, SharedAxisState> _rowGroups = new(StringComparer.Ordinal);

        public float GetSharedSize(string sharedSizeGroup, bool isColumnAxis)
        {
            var registry = isColumnAxis ? _columnGroups : _rowGroups;
            if (!registry.TryGetValue(sharedSizeGroup, out var axisState))
            {
                return 0f;
            }

            axisState.RemoveStaleContributions(this);
            return axisState.MaxSize;
        }

        public void Publish(Grid grid, IReadOnlyList<DefinitionSnapshot> definitions, bool isColumnAxis)
        {
            var registry = isColumnAxis ? _columnGroups : _rowGroups;
            var contributions = new Dictionary<string, float>(StringComparer.Ordinal);
            for (var i = 0; i < definitions.Count; i++)
            {
                var group = definitions[i].SharedSizeGroup;
                if (string.IsNullOrEmpty(group))
                {
                    continue;
                }

                var size = definitions[i].Size;
                if (!definitions[i].Length.IsPixel)
                {
                    size = definitions[i].ContentSize;
                }
                if (contributions.TryGetValue(group, out var existing))
                {
                    contributions[group] = MathF.Max(existing, size);
                }
                else
                {
                    contributions[group] = size;
                }
            }

            foreach (var contribution in contributions)
            {
                if (!registry.TryGetValue(contribution.Key, out var axisState))
                {
                    axisState = new SharedAxisState();
                    registry[contribution.Key] = axisState;
                }

                axisState.RemoveStaleContributions(this);

                if (axisState.UpdateContribution(grid, contribution.Value))
                {
                    axisState.InvalidateMembers();
                }
            }

            foreach (var pair in registry)
            {
                pair.Value.RemoveStaleContributions(this);
                if (contributions.ContainsKey(pair.Key) || !pair.Value.ContainsContribution(grid))
                {
                    continue;
                }

                if (pair.Value.RemoveContribution(grid))
                {
                    pair.Value.InvalidateMembers();
                }
            }

            RemoveEmptyStates(registry);
        }

        public void RemoveGrid(Grid grid)
        {
            RemoveGridFromRegistry(grid, _columnGroups);
            RemoveGridFromRegistry(grid, _rowGroups);
            RemoveEmptyStates(_columnGroups);
            RemoveEmptyStates(_rowGroups);
        }

        private static void RemoveGridFromRegistry(Grid grid, Dictionary<string, SharedAxisState> registry)
        {
            foreach (var axisState in registry.Values)
            {
                if (axisState.RemoveContribution(grid))
                {
                    axisState.InvalidateMembers();
                }
            }
        }

        private static void RemoveEmptyStates(Dictionary<string, SharedAxisState> registry)
        {
            var emptyGroups = new List<string>();
            foreach (var pair in registry)
            {
                if (pair.Value.IsEmpty)
                {
                    emptyGroups.Add(pair.Key);
                }
            }

            for (var i = 0; i < emptyGroups.Count; i++)
            {
                registry.Remove(emptyGroups[i]);
            }
        }
    }

    private sealed class SharedAxisState
    {
        private readonly Dictionary<Grid, float> _contributions = new();

        public float MaxSize { get; private set; }

        public bool IsEmpty => _contributions.Count == 0;

        public bool ContainsContribution(Grid grid)
        {
            return _contributions.ContainsKey(grid);
        }

        public bool UpdateContribution(Grid grid, float size)
        {
            _contributions[grid] = size;
            return RecalculateMaxSize();
        }

        public bool RemoveContribution(Grid grid)
        {
            if (!_contributions.Remove(grid))
            {
                return false;
            }

            return RecalculateMaxSize();
        }

        public void InvalidateMembers()
        {
            foreach (var grid in _contributions.Keys)
            {
                grid.InvalidateMeasure();
                grid.InvalidateArrange();
            }
        }

        public void RemoveStaleContributions(SharedSizeScopeState owner)
        {
            if (_contributions.Count == 0)
            {
                return;
            }

            var staleGrids = new List<Grid>();
            foreach (var contribution in _contributions)
            {
                if (!ReferenceEquals(contribution.Key._sharedSizeScopeState, owner))
                {
                    staleGrids.Add(contribution.Key);
                }
            }

            if (staleGrids.Count == 0)
            {
                return;
            }

            for (var i = 0; i < staleGrids.Count; i++)
            {
                _contributions.Remove(staleGrids[i]);
            }

            RecalculateMaxSize();
        }

        private bool RecalculateMaxSize()
        {
            var nextMax = 0f;
            foreach (var contribution in _contributions.Values)
            {
                if (contribution > nextMax)
                {
                    nextMax = contribution;
                }
            }

            if (MathF.Abs(MaxSize - nextMax) < 0.01f)
            {
                return false;
            }

            MaxSize = nextMax;
            return true;
        }
    }
}


