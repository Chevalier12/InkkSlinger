using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using Microsoft.Xna.Framework;

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

public sealed class ColumnDefinition
{
    private GridLength _width = GridLength.Star;
    private float _minWidth;
    private float _maxWidth = float.PositiveInfinity;

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

    public float ActualWidth { get; internal set; }
}

public sealed class RowDefinition
{
    private GridLength _height = GridLength.Star;
    private float _minHeight;
    private float _maxHeight = float.PositiveInfinity;

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

    public float ActualHeight { get; internal set; }
}

public class Grid : Panel
{
    private static long _measureOverrideElapsedTicks;
    private static long _arrangeOverrideElapsedTicks;
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

    public Grid()
    {
        _columnDefinitions.CollectionChanged += OnColumnDefinitionsChanged;
        _rowDefinitions.CollectionChanged += OnRowDefinitionsChanged;
    }

    public ObservableCollection<ColumnDefinition> ColumnDefinitions => _columnDefinitions;

    public ObservableCollection<RowDefinition> RowDefinitions => _rowDefinitions;

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

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
        var columns = PrepareColumnSnapshots(_measureColumns);
        var rows = PrepareRowSnapshots(_measureRows);
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
                metadata.HasAutoWidth,
                metadata.HasAutoHeight,
                metadata.HasExplicitWidth,
                metadata.HasExplicitHeight,
                float.IsPositiveInfinity(firstPassAvailable.X),
                float.IsPositiveInfinity(firstPassAvailable.Y));

            ApplyChildRequirement(columns, metadata.Cell.Column, metadata.Cell.ColumnSpan, desiredSize.X);
            ApplyChildRequirement(rows, metadata.Cell.Row, metadata.Cell.RowSpan, desiredSize.Y);
        }

        FinalizeDefinitionSizes(columns, availableSize.X);
        FinalizeDefinitionSizes(rows, availableSize.Y);

        for (var childMeasureIndex = 0; childMeasureIndex < childLayoutMetadata.Length; childMeasureIndex++)
        {
            ref readonly var metadata = ref childLayoutMetadata[childMeasureIndex];
            var childAvailable = new Vector2(
                SumRange(columns, metadata.Cell.Column, metadata.Cell.ColumnSpan),
                SumRange(rows, metadata.Cell.Row, metadata.Cell.RowSpan));

            if (ShouldReMeasureChild(_firstPassMeasureRecords[childMeasureIndex], childAvailable))
            {
                MeasureChildOrReuseCachedState(childMeasureIndex, metadata, childAvailable);
            }
        }

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

        InvalidateChildLayoutMetadataCache();
        InvalidateMeasure();
    }

    private void OnDefinitionChanged(object? sender, EventArgs e)
    {
        InvalidateChildLayoutMetadataCache();
        InvalidateMeasure();
    }

    private List<DefinitionSnapshot> PrepareColumnSnapshots(List<DefinitionSnapshot> target)
    {
        PrepareDefinitionSnapshots(target, _columnDefinitions.Count);
        if (_columnDefinitions.Count == 0)
        {
            target[0].Reset(GridLength.Star, 0f, float.PositiveInfinity);
            return target;
        }

        for (var i = 0; i < _columnDefinitions.Count; i++)
        {
            var definition = _columnDefinitions[i];
            target[i].Reset(definition.Width, definition.MinWidth, definition.MaxWidth);
        }

        return target;
    }

    private List<DefinitionSnapshot> PrepareRowSnapshots(List<DefinitionSnapshot> target)
    {
        PrepareDefinitionSnapshots(target, _rowDefinitions.Count);
        if (_rowDefinitions.Count == 0)
        {
            target[0].Reset(GridLength.Star, 0f, float.PositiveInfinity);
            return target;
        }

        for (var i = 0; i < _rowDefinitions.Count; i++)
        {
            var definition = _rowDefinitions[i];
            target[i].Reset(definition.Height, definition.MinHeight, definition.MaxHeight);
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
                definition.Size = Clamp(definition.Length.Value, definition.Min, definition.Max);
                fixedTotal += definition.Size;
                continue;
            }

            if (definition.Length.IsAuto)
            {
                var measured = measuredSizes != null && i < measuredSizes.Count ? measuredSizes[i] : definition.Size;
                definition.Size = Clamp(measured, definition.Min, definition.Max);
                fixedTotal += definition.Size;
                continue;
            }

            definition.Size = Clamp(definition.Size, definition.Min, definition.Max);
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
            definition.Size = Clamp(MathF.Max(definition.Size, share), definition.Min, definition.Max);
        }

        NormalizeDefinitionOverflow(definitions, available);
    }

    private static void ApplyChildRequirement(
        IReadOnlyList<DefinitionSnapshot> definitions,
        int start,
        int span,
        float requiredSize)
    {
        if (requiredSize <= 0f || float.IsNaN(requiredSize) || float.IsInfinity(requiredSize))
        {
            return;
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
            return;
        }

        var autoTargetCount = 0;
        var nonPixelTargetCount = 0;
        for (var i = start; i < end; i++)
        {
            if (!definitions[i].Length.IsPixel)
            {
                nonPixelTargetCount++;
            }

            if (definitions[i].Length.IsAuto)
            {
                autoTargetCount++;
            }
        }

        if (autoTargetCount > 0)
        {
            var addPerTarget = extra / autoTargetCount;
            for (var i = start; i < end; i++)
            {
                var definition = definitions[i];
                if (!definition.Length.IsAuto)
                {
                    continue;
                }

                definition.Size = Clamp(definition.Size + addPerTarget, definition.Min, definition.Max);
            }

            return;
        }

        if (nonPixelTargetCount > 0)
        {
            var addPerTarget = extra / nonPixelTargetCount;
            for (var i = start; i < end; i++)
            {
                var definition = definitions[i];
                if (definition.Length.IsPixel)
                {
                    continue;
                }

                definition.Size = Clamp(definition.Size + addPerTarget, definition.Min, definition.Max);
            }

            return;
        }

        var fallback = definitions[end - 1];
        fallback.Size = Clamp(fallback.Size + extra, fallback.Min, fallback.Max);
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

            shrinkableTotal += MathF.Max(0f, definition.Size - definition.Min);
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

            var shrinkable = MathF.Max(0f, definition.Size - definition.Min);
            if (shrinkable <= 0f)
            {
                continue;
            }

            var portion = shrinkable / shrinkableTotal;
            definition.Size = Clamp(definition.Size - (shrinkTarget * portion), definition.Min, definition.Max);
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
                : float.PositiveInfinity;
        }

        var height = SumRange(rows, metadata.Cell.Row, metadata.Cell.RowSpan);
        if (metadata.HasAutoHeight)
        {
            height = metadata.HasExplicitHeight
                ? ResolveExplicitMeasureAvailable(metadata.Child.Height, metadata.Child.Margin.Vertical)
                : float.PositiveInfinity;
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
        if (AreSizesClose(firstPassRecord.AvailableSize, finalAvailableSize))
        {
            return false;
        }

        var widthRequiresRemeasure = ShouldReMeasureAxis(
            firstPassRecord.AvailableSize.X,
            finalAvailableSize.X,
            firstPassRecord.DesiredSize.X,
            firstPassRecord.HasExplicitWidth,
            firstPassRecord.WidthWasUnconstrained);

        if (widthRequiresRemeasure)
        {
            return true;
        }

        return ShouldReMeasureAxis(
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

        return !ShouldReMeasureAxis(
                   cachedState.AvailableSize.X,
                   availableSize.X,
                   cachedState.DesiredSize.X,
                   cachedState.HasExplicitWidth,
                   cachedState.WidthWasUnconstrained)
               && !ShouldReMeasureAxis(
                   cachedState.AvailableSize.Y,
                   availableSize.Y,
                   cachedState.DesiredSize.Y,
                   cachedState.HasExplicitHeight,
                   cachedState.HeightWasUnconstrained);
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
            target.Add(new DefinitionSnapshot(GridLength.Star, 0f, float.PositiveInfinity));
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

    private sealed class DefinitionSnapshot
    {
        public DefinitionSnapshot(GridLength length, float min, float max)
        {
            Reset(length, min, max);
        }

        public GridLength Length { get; private set; }

        public float Min { get; private set; }

        public float Max { get; private set; }

        public float Size { get; set; }

        public void Reset(GridLength length, float min, float max)
        {
            Length = length;
            Min = min;
            Max = max;
            Size = 0f;
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
        bool HasExplicitWidth,
        bool HasExplicitHeight);

    private readonly record struct FirstPassMeasureRecord(
        CellInfo Cell,
        Vector2 AvailableSize,
        Vector2 DesiredSize,
        bool HasAutoWidth,
        bool HasAutoHeight,
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
}

internal readonly record struct GridTimingSnapshot(
    long MeasureOverrideElapsedTicks,
    long ArrangeOverrideElapsedTicks);
