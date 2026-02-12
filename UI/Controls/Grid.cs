using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
    private float[] _measuredColumnSizes = Array.Empty<float>();
    private float[] _measuredRowSizes = Array.Empty<float>();

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

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var columns = BuildColumns();
        var rows = BuildRows();

        ResolveDefinitionSizes(columns, availableSize.X);
        ResolveDefinitionSizes(rows, availableSize.Y);

        foreach (var child in Children)
        {
            if (child is not FrameworkElement frameworkChild)
            {
                continue;
            }

            frameworkChild.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));

            var cell = NormalizeCell(child, rows.Count, columns.Count);
            ApplyChildRequirement(columns, cell.Column, cell.ColumnSpan, frameworkChild.DesiredSize.X);
            ApplyChildRequirement(rows, cell.Row, cell.RowSpan, frameworkChild.DesiredSize.Y);
        }

        FinalizeDefinitionSizes(columns, availableSize.X);
        FinalizeDefinitionSizes(rows, availableSize.Y);

        foreach (var child in Children)
        {
            if (child is not FrameworkElement frameworkChild)
            {
                continue;
            }

            var cell = NormalizeCell(child, rows.Count, columns.Count);
            var childAvailable = new Vector2(
                SumRange(columns, cell.Column, cell.ColumnSpan),
                SumRange(rows, cell.Row, cell.RowSpan));
            frameworkChild.Measure(childAvailable);
        }

        _measuredColumnSizes = ExtractSizes(columns);
        _measuredRowSizes = ExtractSizes(rows);

        return new Vector2(SumSizes(columns), SumSizes(rows));
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var columns = BuildColumns();
        var rows = BuildRows();

        ResolveDefinitionSizes(columns, finalSize.X, _measuredColumnSizes);
        ResolveDefinitionSizes(rows, finalSize.Y, _measuredRowSizes);
        SyncActualDefinitionSizes(columns, rows);

        var columnOffsets = BuildOffsets(columns, LayoutSlot.X);
        var rowOffsets = BuildOffsets(rows, LayoutSlot.Y);

        foreach (var child in Children)
        {
            if (child is not FrameworkElement frameworkChild)
            {
                continue;
            }

            var cell = NormalizeCell(child, rows.Count, columns.Count);
            var x = columnOffsets[cell.Column];
            var y = rowOffsets[cell.Row];
            var width = SumRange(columns, cell.Column, cell.ColumnSpan);
            var height = SumRange(rows, cell.Row, cell.RowSpan);
            frameworkChild.Arrange(new LayoutRect(x, y, width, height));
        }

        return finalSize;
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

        InvalidateMeasure();
    }

    private void OnDefinitionChanged(object? sender, EventArgs e)
    {
        InvalidateMeasure();
    }

    private List<DefinitionSnapshot> BuildColumns()
    {
        var columns = new List<DefinitionSnapshot>();
        if (_columnDefinitions.Count == 0)
        {
            columns.Add(new DefinitionSnapshot(GridLength.Star, 0f, float.PositiveInfinity));
            return columns;
        }

        foreach (var definition in _columnDefinitions)
        {
            columns.Add(new DefinitionSnapshot(definition.Width, definition.MinWidth, definition.MaxWidth));
        }

        return columns;
    }

    private List<DefinitionSnapshot> BuildRows()
    {
        var rows = new List<DefinitionSnapshot>();
        if (_rowDefinitions.Count == 0)
        {
            rows.Add(new DefinitionSnapshot(GridLength.Star, 0f, float.PositiveInfinity));
            return rows;
        }

        foreach (var definition in _rowDefinitions)
        {
            rows.Add(new DefinitionSnapshot(definition.Height, definition.MinHeight, definition.MaxHeight));
        }

        return rows;
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

        var autoTargets = new List<int>();
        var nonPixelTargets = new List<int>();
        for (var i = start; i < end; i++)
        {
            if (!definitions[i].Length.IsPixel)
            {
                nonPixelTargets.Add(i);
            }

            if (definitions[i].Length.IsAuto)
            {
                autoTargets.Add(i);
            }
        }

        var targets = autoTargets.Count > 0 ? autoTargets : nonPixelTargets;
        if (targets.Count == 0)
        {
            targets.Add(end - 1);
        }

        var addPerTarget = extra / targets.Count;
        foreach (var index in targets)
        {
            var definition = definitions[index];
            definition.Size = Clamp(definition.Size + addPerTarget, definition.Min, definition.Max);
        }
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

    private static float[] BuildOffsets(IReadOnlyList<DefinitionSnapshot> definitions, float origin)
    {
        var offsets = new float[definitions.Count];
        var current = origin;
        for (var i = 0; i < definitions.Count; i++)
        {
            offsets[i] = current;
            current += definitions[i].Size;
        }

        return offsets;
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

    private static float[] ExtractSizes(IReadOnlyList<DefinitionSnapshot> definitions)
    {
        var result = new float[definitions.Count];
        for (var i = 0; i < definitions.Count; i++)
        {
            result[i] = definitions[i].Size;
        }

        return result;
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
            Length = length;
            Min = min;
            Max = max;
        }

        public GridLength Length { get; }

        public float Min { get; }

        public float Max { get; }

        public float Size { get; set; }
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
}
