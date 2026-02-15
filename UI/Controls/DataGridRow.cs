using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class DataGridRow : Control
{
    public static readonly DependencyProperty IsSelectedProperty =
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
    private readonly List<float> _columnWidths = new();

    public DataGridRow()
    {
        _rowHeader.SetVisualParent(this);
        _rowHeader.SetLogicalParent(this);
        _detailsPresenter.SetVisualParent(this);
        _detailsPresenter.SetLogicalParent(this);
    }

    public bool IsSelected
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

    internal DataGrid? Owner { get; private set; }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        yield return _rowHeader;
        foreach (var cell in _cells)
        {
            yield return cell;
        }

        yield return _detailsPresenter;
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

    internal void Configure(DataGrid owner, int rowIndex, object? item, IReadOnlyList<DataGridColumn> columns, IReadOnlyList<float> columnWidths)
    {
        Owner = owner;
        RowIndex = rowIndex;
        Item = item;

        _rowHeader.Text = (rowIndex + 1).ToString();
        _rowHeader.Font = owner.Font;
        _columnWidths.Clear();
        _columnWidths.AddRange(columnWidths);

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
            cell.ColumnIndex = i;
            cell.Value = owner.GetValueForCell(item, columns[i]);
            cell.Font = owner.Font;
            cell.Foreground = owner.Foreground;
        }

        InvalidateMeasure();
    }

    internal void UpdateSelectionState(DataGridSelectionUnit selectionUnit, int selectedRowIndex, int selectedColumnIndex)
    {
        IsSelected = selectedRowIndex == RowIndex;

        for (var i = 0; i < _cells.Count; i++)
        {
            var selected = selectionUnit == DataGridSelectionUnit.Cell
                ? selectedRowIndex == RowIndex && selectedColumnIndex == i
                : selectedRowIndex == RowIndex;
            _cells[i].IsSelected = selected;
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var rowHeaderWidth = Owner?.RowHeaderWidth ?? 0f;
        var rowHeight = Owner?.RowHeight ?? 24f;
        _rowHeader.Measure(new Vector2(rowHeaderWidth, rowHeight));

        for (var i = 0; i < _cells.Count; i++)
        {
            var width = i < _columnWidths.Count ? _columnWidths[i] : 100f;
            _cells[i].Measure(new Vector2(width, rowHeight));
        }

        var totalWidth = rowHeaderWidth;
        for (var i = 0; i < _columnWidths.Count; i++)
        {
            totalWidth += _columnWidths[i];
        }

        return new Vector2(totalWidth, rowHeight);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var x = LayoutSlot.X;
        var y = LayoutSlot.Y;
        var height = finalSize.Y;

        var rowHeaderWidth = Owner?.RowHeaderWidth ?? 0f;
        if (Owner?.ShowRowHeaders != true)
        {
            rowHeaderWidth = 0f;
        }

        _rowHeader.Arrange(new LayoutRect(x, y, rowHeaderWidth, height));
        x += rowHeaderWidth;

        for (var i = 0; i < _cells.Count; i++)
        {
            var width = i < _columnWidths.Count ? _columnWidths[i] : 100f;
            _cells[i].Arrange(new LayoutRect(x, y, width, height));
            x += width;
        }

        _detailsPresenter.Arrange(new LayoutRect(x, y, 0f, 0f));
        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var fill = IsSelected ? SelectedRowBackground : RowBackground;
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
}
