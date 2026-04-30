using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class DataGridRow : Control
{
    public new static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(
            nameof(IsSelected),
            typeof(bool),
            typeof(DataGridRow),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RowBackgroundProperty =
        DependencyProperty.Register(
            nameof(RowBackground),
            typeof(Color),
            typeof(DataGridRow),
            new FrameworkPropertyMetadata(new Color(24, 34, 49), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedRowBackgroundProperty =
        DependencyProperty.Register(
            nameof(SelectedRowBackground),
            typeof(Color),
            typeof(DataGridRow),
            new FrameworkPropertyMetadata(new Color(44, 74, 112), FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly List<DataGridCell> _cells = new();
    private readonly DataGridRowHeader _rowHeader = new();
    private readonly DataGridDetailsPresenter _detailsPresenter = new();
    private readonly List<DataGridColumnState> _columns = new();
    private readonly DataGridRowHeaderLaneCoordinator _rowHeaderLaneCoordinator = new();

    public DataGridRow()
    {
        _rowHeader.SetVisualParent(this);
        _rowHeader.SetLogicalParent(this);
        _detailsPresenter.SetVisualParent(this);
        _detailsPresenter.SetLogicalParent(this);
    }

    public new bool IsSelected
    {
        get => GetValue<bool>(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public Color RowBackground
    {
        get => GetValue<Color>(RowBackgroundProperty);
        set => SetValue(RowBackgroundProperty, value);
    }

    public Color SelectedRowBackground
    {
        get => GetValue<Color>(SelectedRowBackgroundProperty);
        set => SetValue(SelectedRowBackgroundProperty, value);
    }

    internal int RowIndex { get; set; }

    internal object? Item { get; private set; }

    internal IReadOnlyList<DataGridCell> Cells => _cells;

    internal DataGridRowHeader RowHeaderForTesting => _rowHeader;

    internal DataGrid? Owner { get; private set; }

    internal DataGridDetailsPresenter DetailsPresenterForTesting => _detailsPresenter;

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        yield return _rowHeader;
        foreach (var cell in _cells)
        {
            yield return cell;
        }

        yield return _detailsPresenter;
    }

    internal override int GetVisualChildCountForTraversal()
    {
        return _cells.Count + 2;
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        if (index == 0)
        {
            return _rowHeader;
        }

        if (index == _cells.Count + 1)
        {
            return _detailsPresenter;
        }

        var cellIndex = index - 1;
        if ((uint)cellIndex < (uint)_cells.Count)
        {
            return _cells[cellIndex];
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        yield return _rowHeader;
        foreach (var cell in _cells)
        {
            yield return cell;
        }

        yield return _detailsPresenter;
    }

    internal void Configure(
        DataGrid owner,
        DataGridRowState rowState,
        IReadOnlyList<DataGridColumnState> columns,
        bool showRowHeader,
        bool showHorizontalGridLines,
        bool showVerticalGridLines,
        Color horizontalGridLineBrush,
        Color verticalGridLineBrush)
    {
        var previousColumnCount = _cells.Count;
        var previousStateCount = _columns.Count;
        Owner = owner;
        RowIndex = rowState.RowIndex;
        Item = rowState.Item;

        _rowHeaderLaneCoordinator.ConfigureHeader(_rowHeader, owner, rowState, showRowHeader);
        _columns.Clear();
        _columns.AddRange(columns);
        SyncDetailsPresenter(owner, rowState.Item, rowState.AreDetailsVisible);

        while (_cells.Count > columns.Count)
        {
            var index = _cells.Count - 1;
            var removed = _cells[index];
            removed.SetVisualParent(null);
            removed.SetLogicalParent(null);
            _cells.RemoveAt(index);
        }

        while (_cells.Count < columns.Count)
        {
            var cell = new DataGridCell();
            cell.SetVisualParent(this);
            cell.SetLogicalParent(this);
            _cells.Add(cell);
        }

        for (var i = 0; i < columns.Count; i++)
        {
            var cell = _cells[i];
            cell.BindState(
                owner,
                rowState,
                columns[i],
                showHorizontalGridLines,
                showVerticalGridLines,
                horizontalGridLineBrush,
                verticalGridLineBrush);
        }

        if (previousColumnCount != _cells.Count || previousStateCount != _columns.Count)
        {
            InvalidateMeasure();
        }
        else
        {
            InvalidateVisual();
        }
    }

    internal void UpdateSelectionState(
        DataGridSelectionUnit selectionUnit,
        IReadOnlySet<int> selectedRowIndices,
        IReadOnlySet<long> selectedCellKeys,
        bool allCellsSelected,
        int currentRowIndex,
        int currentColumnIndex,
        bool areDetailsVisible)
    {
        var isRowSelected = selectedRowIndices.Contains(RowIndex);
        var previousDetailsVisible = _detailsPresenter.IsVisibleDetails;
        var previousDetailsContent = _detailsPresenter.Content;
        var previousDetailsTemplate = _detailsPresenter.ContentTemplate;
        IsSelected = isRowSelected;
        SyncDetailsPresenter(Owner, Item, areDetailsVisible);
        var detailsLayoutChanged = previousDetailsVisible != _detailsPresenter.IsVisibleDetails ||
                                   !ReferenceEquals(previousDetailsContent, _detailsPresenter.Content) ||
                                   !ReferenceEquals(previousDetailsTemplate, _detailsPresenter.ContentTemplate);

        for (var i = 0; i < _cells.Count; i++)
        {
            var isCellSelected = selectedCellKeys.Contains(CreateCellSelectionKey(RowIndex, i));
            var selected = selectionUnit switch
            {
                DataGridSelectionUnit.Cell => allCellsSelected || isCellSelected,
                DataGridSelectionUnit.CellOrRowHeader => allCellsSelected || isRowSelected || isCellSelected,
                _ => isRowSelected
            };
            var currentCellSelected = currentRowIndex == RowIndex && currentColumnIndex == i &&
                                      (selectionUnit == DataGridSelectionUnit.Cell || selectionUnit == DataGridSelectionUnit.CellOrRowHeader);
            _cells[i].IsSelected = selected || currentCellSelected;
        }

        if (detailsLayoutChanged)
        {
            InvalidateMeasure();
        }
    }

    internal void ReorderColumns(
        DataGrid owner,
        DataGridRowState rowState,
        IReadOnlyList<DataGridColumnState> columns,
        bool showRowHeader,
        bool showHorizontalGridLines,
        bool showVerticalGridLines,
        Color horizontalGridLineBrush,
        Color verticalGridLineBrush)
    {
        if (_cells.Count != columns.Count || _columns.Count != columns.Count)
        {
            Configure(owner, rowState, columns, showRowHeader, showHorizontalGridLines, showVerticalGridLines, horizontalGridLineBrush, verticalGridLineBrush);
            return;
        }

        var cellsByColumn = new Dictionary<DataGridColumn, DataGridCell>(columns.Count);
        for (var i = 0; i < _cells.Count; i++)
        {
            var column = _cells[i].ColumnState?.Column;
            if (column == null || !cellsByColumn.TryAdd(column, _cells[i]))
            {
                Configure(owner, rowState, columns, showRowHeader, showHorizontalGridLines, showVerticalGridLines, horizontalGridLineBrush, verticalGridLineBrush);
                return;
            }
        }

        var reorderedCells = new List<DataGridCell>(columns.Count);
        var orderChanged = false;
        for (var i = 0; i < columns.Count; i++)
        {
            if (!cellsByColumn.TryGetValue(columns[i].Column, out var cell))
            {
                Configure(owner, rowState, columns, showRowHeader, showHorizontalGridLines, showVerticalGridLines, horizontalGridLineBrush, verticalGridLineBrush);
                return;
            }

            reorderedCells.Add(cell);
            if (!ReferenceEquals(_cells[i], cell))
            {
                orderChanged = true;
            }
        }

        Owner = owner;
        RowIndex = rowState.RowIndex;
        Item = rowState.Item;
        _rowHeaderLaneCoordinator.ConfigureHeader(_rowHeader, owner, rowState, showRowHeader);
        _columns.Clear();
        _columns.AddRange(columns);
        SyncDetailsPresenter(owner, rowState.Item, rowState.AreDetailsVisible);

        if (orderChanged)
        {
            _cells.Clear();
            _cells.AddRange(reorderedCells);
        }

        for (var i = 0; i < columns.Count; i++)
        {
            _cells[i].BindState(
                owner,
                rowState,
                columns[i],
                showHorizontalGridLines,
                showVerticalGridLines,
                horizontalGridLineBrush,
                verticalGridLineBrush,
                refreshContent: false);
        }

        if (orderChanged)
        {
            InvalidateArrange();
        }

        InvalidateVisual();
    }

    private static long CreateCellSelectionKey(int rowIndex, int columnIndex)
    {
        return ((long)rowIndex << 32) | (uint)columnIndex;
    }

    internal void RefreshCellContents()
    {
        for (var i = 0; i < _cells.Count; i++)
        {
            _cells[i].RefreshContentFromState();
        }
    }

    public override void InvalidateMeasure()
    {
        base.InvalidateMeasure();
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var rowHeaderWidth = _rowHeaderLaneCoordinator.ResolveWidth(Owner);
        var rowHeight = Owner?.EffectiveRowHeightForLayout ?? 24f;
        _rowHeaderLaneCoordinator.MeasureHeader(_rowHeader, rowHeaderWidth, rowHeight);

        for (var i = 0; i < _cells.Count; i++)
        {
            var width = i < _columns.Count ? _columns[i].Width : 100f;
            _cells[i].Measure(new Vector2(width, rowHeight));
        }

        _detailsPresenter.Measure(new Vector2(MathF.Max(0f, availableSize.X - rowHeaderWidth), float.PositiveInfinity));
        var detailsHeight = _detailsPresenter.IsVisibleDetails ? _detailsPresenter.DesiredSize.Y : 0f;

        var totalWidth = rowHeaderWidth;
        for (var i = 0; i < _columns.Count; i++)
        {
            totalWidth += _columns[i].Width;
        }

        return new Vector2(totalWidth, rowHeight + detailsHeight);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var x = LayoutSlot.X;
        var y = LayoutSlot.Y;
        var rowHeaderWidth = _rowHeaderLaneCoordinator.ResolveWidth(Owner);
        var detailsHeight = _detailsPresenter.IsVisibleDetails ? _detailsPresenter.DesiredSize.Y : 0f;
        var rowHeight = MathF.Max(0f, finalSize.Y - detailsHeight);
        var horizontalOffset = Owner?.HorizontalOffsetForTesting ?? 0f;


        _rowHeaderLaneCoordinator.ArrangeHeader(_rowHeader, x + horizontalOffset, y, rowHeaderWidth, rowHeight);

        var frozenCount = Math.Clamp(Owner?.FrozenColumnCount ?? 0, 0, _columns.Count);
        var runningFrozenX = x + rowHeaderWidth + horizontalOffset;
        var runningScrollableX = x + rowHeaderWidth;
        for (var i = 0; i < _cells.Count; i++)
        {
            var width = i < _columns.Count ? _columns[i].Width : 100f;
            var cellX = i < frozenCount ? runningFrozenX : runningScrollableX;
            _cells[i].Arrange(new LayoutRect(cellX, y, width, rowHeight));
            if (i < frozenCount)
            {
                runningFrozenX += width;
            }

            runningScrollableX += width;
        }

        _detailsPresenter.Arrange(new LayoutRect(
            x + rowHeaderWidth + horizontalOffset,
            y + rowHeight,
            MathF.Max(0f, finalSize.X - rowHeaderWidth),
            detailsHeight));
        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var hasStyleDrivenBackground = GetValueSource(BackgroundProperty) != DependencyPropertyValueSource.Default;
        var fill = hasStyleDrivenBackground
            ? Background
            : (IsSelected ? SelectedRowBackground : RowBackground);
        UiDrawing.DrawFilledRect(spriteBatch, LayoutSlot, fill, Opacity);
    }


    private int ResolveCellIndex(Vector2 point)
    {
        for (var i = 0; i < _cells.Count; i++)
        {
            var slot = _cells[i].LayoutSlot;
            if (point.X >= slot.X && point.X <= slot.X + slot.Width && point.Y >= slot.Y && point.Y <= slot.Y + slot.Height)
            {
                return i;
            }
        }

        return _cells.Count > 0 ? 0 : -1;
    }

    internal int ResolveCellIndexAtPoint(Vector2 point)
    {
        return ResolveCellIndex(point);
    }

    internal bool IsPointInRowHeader(Vector2 point)
    {
        var slot = _rowHeader.LayoutSlot;
        return point.X >= slot.X && point.X <= slot.X + slot.Width && point.Y >= slot.Y && point.Y <= slot.Y + slot.Height;
    }

    private void SyncDetailsPresenter(DataGrid? owner, object? item, bool areDetailsVisible)
    {
        var template = owner?.RowDetailsTemplate;
        var canShowDetails = areDetailsVisible && template != null;
        _detailsPresenter.ContentTemplate = template;
        _detailsPresenter.Content = canShowDetails ? item : null;
        _detailsPresenter.IsVisibleDetails = canShowDetails;
        _detailsPresenter.IsVisible = canShowDetails;
    }
}
