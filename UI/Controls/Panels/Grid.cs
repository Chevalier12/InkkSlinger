using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
    private static long _measureOverrideElapsedTicks;
    private static long _arrangeOverrideElapsedTicks;
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
    private float[] _columnOffsets = Array.Empty<float>();
    private float[] _rowOffsets = Array.Empty<float>();
    private ChildLayoutMetadata[] _childLayoutMetadataCache = Array.Empty<ChildLayoutMetadata>();
    private FirstPassMeasureRecord[] _firstPassMeasureRecords = Array.Empty<FirstPassMeasureRecord>();
    private CachedChildMeasureState[] _cachedChildMeasureStates = Array.Empty<CachedChildMeasureState>();
    private bool _childLayoutMetadataDirty = true;
    private SharedSizeScopeState? _sharedSizeScopeState;

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
        try
        {
        var columns = PrepareColumnSnapshots(_measureColumns);
        var rows = PrepareRowSnapshots(_measureRows);
        RefreshSharedSizeScopeState();
        ApplySharedSizes(columns, isColumnAxis: true);
        ApplySharedSizes(rows, isColumnAxis: false);
        var childLayoutMetadata = PrepareChildLayoutMetadata(rows, columns);

        ResolveDefinitionSizes(columns, availableSize.X);
        ResolveDefinitionSizes(rows, availableSize.Y);
        EnsureChildMeasureRecordCapacity(childLayoutMetadata.Length);

        for (var childMeasureIndex = 0; childMeasureIndex < childLayoutMetadata.Length; childMeasureIndex++)
        {
            ref readonly var metadata = ref childLayoutMetadata[childMeasureIndex];
            var firstPassAvailable = ResolveFirstPassAvailableSize(metadata, rows, columns);
            var desiredSize = MeasureChildOrReuseCachedState(childMeasureIndex, metadata, firstPassAvailable);

            _firstPassMeasureRecords[childMeasureIndex] = new FirstPassMeasureRecord(
                metadata.Cell,
                firstPassAvailable,
                desiredSize,
                metadata.HasExplicitWidth,
                metadata.HasExplicitHeight,
                float.IsPositiveInfinity(firstPassAvailable.X),
                float.IsPositiveInfinity(firstPassAvailable.Y));

            ApplyChildRequirement(
                columns,
                metadata.Cell.Column,
                metadata.Cell.ColumnSpan,
                desiredSize.X,
                !float.IsInfinity(availableSize.X) && !float.IsNaN(availableSize.X));
            ApplyChildRequirement(
                rows,
                metadata.Cell.Row,
                metadata.Cell.RowSpan,
                desiredSize.Y,
                !float.IsInfinity(availableSize.Y) && !float.IsNaN(availableSize.Y));
        }

        FinalizeDefinitionSizes(columns, availableSize.X);
        FinalizeDefinitionSizes(rows, availableSize.Y);

        for (var pass = 0; pass < 4; pass++)
        {
            var definitionsChanged = false;
            for (var childMeasureIndex = 0; childMeasureIndex < childLayoutMetadata.Length; childMeasureIndex++)
            {
                ref readonly var metadata = ref childLayoutMetadata[childMeasureIndex];
                var childAvailable = new Vector2(
                    SumRange(columns, metadata.Cell.Column, metadata.Cell.ColumnSpan),
                    SumRange(rows, metadata.Cell.Row, metadata.Cell.RowSpan));

                if (!ShouldReMeasureChild(_firstPassMeasureRecords[childMeasureIndex], childAvailable))
                {
                    continue;
                }

                var desiredSize = MeasureChildOrReuseCachedState(childMeasureIndex, metadata, childAvailable);
                definitionsChanged |= ApplyChildRequirement(
                    columns,
                    metadata.Cell.Column,
                    metadata.Cell.ColumnSpan,
                    desiredSize.X,
                    !float.IsInfinity(availableSize.X) && !float.IsNaN(availableSize.X));
                definitionsChanged |= ApplyChildRequirement(
                    rows,
                    metadata.Cell.Row,
                    metadata.Cell.RowSpan,
                    desiredSize.Y,
                    !float.IsInfinity(availableSize.Y) && !float.IsNaN(availableSize.Y));
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

        return new Vector2(SumSizes(columns), SumSizes(rows));
        }
        finally
        {
            _measureOverrideElapsedTicks += Stopwatch.GetTimestamp() - start;
        }
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var start = Stopwatch.GetTimestamp();
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

        foreach (var child in Children)
        {
            if (child is not FrameworkElement frameworkChild)
            {
                continue;
            }

            var cell = NormalizeCell(child, rows.Count, columns.Count);
            var x = _columnOffsets[cell.Column];
            var y = _rowOffsets[cell.Row];
            var width = SumRange(columns, cell.Column, cell.ColumnSpan);
            var height = SumRange(rows, cell.Row, cell.RowSpan);
            frameworkChild.Arrange(new LayoutRect(x, y, width, height));
        }

        return finalSize;
        }
        finally
        {
            _arrangeOverrideElapsedTicks += Stopwatch.GetTimestamp() - start;
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
        DetachFromSharedSizeScope();
        InvalidateChildLayoutMetadataCache();
        InvalidateMeasure();
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
            target[i].Reset(
                definition.Width,
                definition.MinWidth,
                definition.MaxWidth,
                definition.Width.IsStar ? null : definition.SharedSizeGroup);
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
            target[i].Reset(
                definition.Height,
                definition.MinHeight,
                definition.MaxHeight,
                definition.Height.IsStar ? null : definition.SharedSizeGroup);
        }

        return target;
    }

    private static void ResolveDefinitionSizes(
        IReadOnlyList<DefinitionSnapshot> definitions,
        float available,
        IReadOnlyList<float>? measuredSizes = null)
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
        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            if (!definition.Length.IsStar)
            {
                continue;
            }

            var weight = MathF.Max(0.0001f, definition.Length.Value);
            var share = remaining * (weight / starWeight);
            definition.Size = Clamp(MathF.Max(definition.Size, share), definition.EffectiveMin, definition.Max);
        }

        NormalizeDefinitionOverflow(definitions, available);
    }

    private static bool ApplyChildRequirement(
        IReadOnlyList<DefinitionSnapshot> definitions,
        int start,
        int span,
        float requiredSize,
        bool hasFiniteConstraint)
    {
        if (requiredSize <= 0f || float.IsNaN(requiredSize) || float.IsInfinity(requiredSize))
        {
            return false;
        }

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

        var changed = false;
        if (hasFiniteConstraint && HasDefinitionType(definitions, start, end, static definition => definition.Length.IsStar))
        {
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

    private static void FinalizeDefinitionSizes(IReadOnlyList<DefinitionSnapshot> definitions, float available)
    {
        ResolveDefinitionSizes(definitions, available);
    }

    private static void NormalizeDefinitionOverflow(IReadOnlyList<DefinitionSnapshot> definitions, float available)
    {
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

        overflow = ReduceOverflow(definitions, overflow, static definition => definition.Length.IsStar);
        overflow = ReduceOverflow(definitions, overflow, static definition => definition.Length.IsAuto);
        ReduceOverflow(definitions, overflow, static definition => definition.Length.IsPixel);
    }

    private static float ReduceOverflow(
        IReadOnlyList<DefinitionSnapshot> definitions,
        float overflow,
        Predicate<DefinitionSnapshot> match)
    {
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

    private void EnsureChildMeasureRecordCapacity(int capacity)
    {
        if (_firstPassMeasureRecords.Length >= capacity)
        {
            if (_cachedChildMeasureStates.Length >= capacity)
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
    }

    private ChildLayoutMetadata[] PrepareChildLayoutMetadata(
        IReadOnlyList<DefinitionSnapshot> rows,
        IReadOnlyList<DefinitionSnapshot> columns)
    {
        var frameworkChildCount = CountFrameworkChildren();
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
                metadata = metadata with
                {
                    HasExplicitWidth = HasExplicitSize(frameworkChild.Width),
                    HasExplicitHeight = HasExplicitSize(frameworkChild.Height)
                };
            }

            metadataIndex++;
        }

        _childLayoutMetadataDirty = false;
        return _childLayoutMetadataCache;
    }

    private static Vector2 ResolveFirstPassAvailableSize(
        in ChildLayoutMetadata metadata,
        IReadOnlyList<DefinitionSnapshot> rows,
        IReadOnlyList<DefinitionSnapshot> columns)
    {
        var width = SumRange(columns, metadata.Cell.Column, metadata.Cell.ColumnSpan);
        if (metadata.HasAutoWidth)
        {
            width = metadata.HasExplicitWidth
                ? ResolveExplicitMeasureAvailable(metadata.Child.Width, metadata.Child.Margin.Horizontal)
                : metadata.HasStarWidth ? width : float.PositiveInfinity;
        }

        var height = SumRange(rows, metadata.Cell.Row, metadata.Cell.RowSpan);
        if (metadata.HasAutoHeight)
        {
            height = metadata.HasExplicitHeight
                ? ResolveExplicitMeasureAvailable(metadata.Child.Height, metadata.Child.Margin.Vertical)
                : metadata.HasStarHeight ? height : float.PositiveInfinity;
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

    private void InvalidateChildLayoutMetadataCache()
    {
        _childLayoutMetadataDirty = true;
    }

    private Vector2 MeasureChildOrReuseCachedState(
        int childMeasureIndex,
        in ChildLayoutMetadata metadata,
        Vector2 availableSize)
    {
        if (!metadata.Child.NeedsMeasure &&
            childMeasureIndex >= 0 &&
            childMeasureIndex < _cachedChildMeasureStates.Length &&
            CanReuseCachedMeasure(_cachedChildMeasureStates[childMeasureIndex], metadata.Child, availableSize))
        {
            return _cachedChildMeasureStates[childMeasureIndex].DesiredSize;
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

    private static bool AreSizesClose(Vector2 first, Vector2 second)
    {
        return AreFloatsClose(first.X, second.X) && AreFloatsClose(first.Y, second.Y);
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
        var scopeOwner = FindSharedSizeScopeOwner();
        var nextScopeState = scopeOwner != null
            ? SharedSizeScopes.GetValue(scopeOwner, static _ => new SharedSizeScopeState())
            : null;

        if (ReferenceEquals(_sharedSizeScopeState, nextScopeState))
        {
            return;
        }

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

            definitions[i].ApplySharedSize(_sharedSizeScopeState.GetSharedSize(group, isColumnAxis));
        }
    }

    private void PublishSharedSizes(IReadOnlyList<DefinitionSnapshot> definitions, bool isColumnAxis)
    {
        if (_sharedSizeScopeState == null)
        {
            return;
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
        }

        public void ApplySharedSize(float sharedSize)
        {
            SharedSize = MathF.Max(0f, sharedSize);
            if (Size < EffectiveMin)
            {
                Size = EffectiveMin;
            }
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

    internal static GridTimingSnapshot GetTimingSnapshotForTests()
    {
        return new GridTimingSnapshot(_measureOverrideElapsedTicks, _arrangeOverrideElapsedTicks);
    }

    internal static void ResetTimingForTests()
    {
        _measureOverrideElapsedTicks = 0;
        _arrangeOverrideElapsedTicks = 0;
    }

    private sealed class SharedSizeScopeState
    {
        private readonly Dictionary<string, SharedAxisState> _columnGroups = new(StringComparer.Ordinal);
        private readonly Dictionary<string, SharedAxisState> _rowGroups = new(StringComparer.Ordinal);

        public float GetSharedSize(string sharedSizeGroup, bool isColumnAxis)
        {
            var registry = isColumnAxis ? _columnGroups : _rowGroups;
            return registry.TryGetValue(sharedSizeGroup, out var axisState)
                ? axisState.MaxSize
                : 0f;
        }

        public void Publish(Grid grid, IReadOnlyList<DefinitionSnapshot> definitions, bool isColumnAxis)
        {
            var registry = isColumnAxis ? _columnGroups : _rowGroups;
            RemoveGridFromRegistry(grid, registry);

            var contributions = new Dictionary<string, float>(StringComparer.Ordinal);
            for (var i = 0; i < definitions.Count; i++)
            {
                var group = definitions[i].SharedSizeGroup;
                if (string.IsNullOrEmpty(group))
                {
                    continue;
                }

                var size = definitions[i].Size;
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

                if (axisState.UpdateContribution(grid, contribution.Value))
                {
                    axisState.InvalidateMembers();
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

internal readonly record struct GridTimingSnapshot(
    long MeasureOverrideElapsedTicks,
    long ArrangeOverrideElapsedTicks);
