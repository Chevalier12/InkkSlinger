using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class DataGrid : ItemsControl
{
    public static readonly DependencyProperty FontProperty =
        DependencyProperty.Register(
            nameof(Font),
            typeof(SpriteFont),
            typeof(DataGrid),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(DataGrid),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(DataGrid),
            new FrameworkPropertyMetadata(new Color(20, 30, 45), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(DataGrid),
            new FrameworkPropertyMetadata(new Color(69, 99, 132), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(DataGrid),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float v && v >= 0f ? v : 0f));

    public static readonly DependencyProperty RowHeightProperty =
        DependencyProperty.Register(
            nameof(RowHeight),
            typeof(float),
            typeof(DataGrid),
            new FrameworkPropertyMetadata(
                26f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                coerceValueCallback: static (_, value) => value is float v && v >= 16f ? v : 16f));

    public static readonly DependencyProperty ColumnHeaderHeightProperty =
        DependencyProperty.Register(
            nameof(ColumnHeaderHeight),
            typeof(float),
            typeof(DataGrid),
            new FrameworkPropertyMetadata(
                28f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                coerceValueCallback: static (_, value) => value is float v && v >= 16f ? v : 16f));

    public static readonly DependencyProperty RowHeaderWidthProperty =
        DependencyProperty.Register(
            nameof(RowHeaderWidth),
            typeof(float),
            typeof(DataGrid),
            new FrameworkPropertyMetadata(
                42f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                coerceValueCallback: static (_, value) => value is float v && v >= 0f ? v : 0f));

    public static readonly DependencyProperty ShowRowHeadersProperty =
        DependencyProperty.Register(
            nameof(ShowRowHeaders),
            typeof(bool),
            typeof(DataGrid),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty SelectionUnitProperty =
        DependencyProperty.Register(
            nameof(SelectionUnit),
            typeof(DataGridSelectionUnit),
            typeof(DataGrid),
            new FrameworkPropertyMetadata(DataGridSelectionUnit.FullRow, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedRowIndexProperty =
        DependencyProperty.Register(
            nameof(SelectedRowIndex),
            typeof(int),
            typeof(DataGrid),
            new FrameworkPropertyMetadata(
                -1,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is DataGrid grid)
                    {
                        grid.UpdateSelectionVisuals();
                    }
                }));

    public static readonly DependencyProperty SelectedColumnIndexProperty =
        DependencyProperty.Register(
            nameof(SelectedColumnIndex),
            typeof(int),
            typeof(DataGrid),
            new FrameworkPropertyMetadata(
                -1,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is DataGrid grid)
                    {
                        grid.UpdateSelectionVisuals();
                    }
                }));

    public static readonly DependencyProperty EnableRowVirtualizationProperty =
        DependencyProperty.Register(
            nameof(EnableRowVirtualization),
            typeof(bool),
            typeof(DataGrid),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is DataGrid grid && args.NewValue is bool enabled)
                    {
                        grid._rowsHost.IsVirtualizing = enabled;
                    }
                }));

    public static readonly DependencyProperty CanUserSortColumnsProperty =
        DependencyProperty.Register(
            nameof(CanUserSortColumns),
            typeof(bool),
            typeof(DataGrid),
            new FrameworkPropertyMetadata(true));

    private readonly ObservableCollection<DataGridColumn> _columns = new();
    private readonly List<DataGridColumn> _resolvedColumns = new();
    private readonly List<float> _columnWidths = new();
    private readonly List<DataGridColumnHeader> _columnHeaders = new();
    private readonly ScrollViewer _scrollViewer;
    private readonly VirtualizingStackPanel _rowsHost;
    private readonly DataGridRowHeader _cornerHeader;

    public DataGrid()
    {

        _rowsHost = new VirtualizingStackPanel
        {
            Orientation = Orientation.Vertical,
            IsVirtualizing = true,
            CacheLength = 1f,
            CacheLengthUnit = VirtualizationCacheLengthUnit.Page
        };

        _scrollViewer = new ScrollViewer
        {
            Content = _rowsHost,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            LineScrollAmount = 24f,
            BorderThickness = 0f,
            Background = Color.Transparent
        };

        _cornerHeader = new DataGridRowHeader
        {
            Text = string.Empty
        };

        _scrollViewer.SetVisualParent(this);
        _scrollViewer.SetLogicalParent(this);
        _cornerHeader.SetVisualParent(this);
        _cornerHeader.SetLogicalParent(this);

        _columns.CollectionChanged += OnColumnsChanged;
        _scrollViewer.DependencyPropertyChanged += OnScrollViewerDependencyPropertyChanged;
    }

    public event EventHandler<DataGridSortingEventArgs>? Sorting;

    public ObservableCollection<DataGridColumn> Columns => _columns;

    public SpriteFont? Font
    {
        get => GetValue<SpriteFont>(FontProperty);
        set => SetValue(FontProperty, value);
    }

    public Color Foreground
    {
        get => GetValue<Color>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Color BorderBrush
    {
        get => GetValue<Color>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public float BorderThickness
    {
        get => GetValue<float>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public float RowHeight
    {
        get => GetValue<float>(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    public float ColumnHeaderHeight
    {
        get => GetValue<float>(ColumnHeaderHeightProperty);
        set => SetValue(ColumnHeaderHeightProperty, value);
    }

    public float RowHeaderWidth
    {
        get => GetValue<float>(RowHeaderWidthProperty);
        set => SetValue(RowHeaderWidthProperty, value);
    }

    public bool ShowRowHeaders
    {
        get => GetValue<bool>(ShowRowHeadersProperty);
        set => SetValue(ShowRowHeadersProperty, value);
    }

    public DataGridSelectionUnit SelectionUnit
    {
        get => GetValue<DataGridSelectionUnit>(SelectionUnitProperty);
        set => SetValue(SelectionUnitProperty, value);
    }

    public int SelectedRowIndex
    {
        get => GetValue<int>(SelectedRowIndexProperty);
        set => SetValue(SelectedRowIndexProperty, value);
    }

    public int SelectedColumnIndex
    {
        get => GetValue<int>(SelectedColumnIndexProperty);
        set => SetValue(SelectedColumnIndexProperty, value);
    }

    public bool EnableRowVirtualization
    {
        get => GetValue<bool>(EnableRowVirtualizationProperty);
        set => SetValue(EnableRowVirtualizationProperty, value);
    }

    public bool CanUserSortColumns
    {
        get => GetValue<bool>(CanUserSortColumnsProperty);
        set => SetValue(CanUserSortColumnsProperty, value);
    }

    internal int RealizedRowCountForTesting => _rowsHost.RealizedChildrenCount;

    internal ScrollViewer ScrollViewerForTesting => _scrollViewer;

    protected override bool IncludeGeneratedChildrenInVisualTree => false;

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        yield return _cornerHeader;
        foreach (var header in _columnHeaders)
        {
            yield return header;
        }

        yield return _scrollViewer;
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        yield return _cornerHeader;
        foreach (var header in _columnHeaders)
        {
            yield return header;
        }

        yield return _scrollViewer;
    }

    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return item is DataGridRow;
    }

    protected override UIElement CreateContainerForItemOverride(object item)
    {
        return new DataGridRow();
    }

    protected override void PrepareContainerForItemOverride(UIElement element, object item, int index)
    {
        base.PrepareContainerForItemOverride(element, item, index);

        if (element is not DataGridRow row)
        {
            return;
        }

        row.Height = RowHeight;
        row.Configure(this, index, item, _resolvedColumns, _columnWidths);
    }

    protected override void OnItemsChanged()
    {
        base.OnItemsChanged();
        RefreshAutoColumnsIfNeeded();
        RebuildHeaders();
        SyncRowsHost();
        UpdateSelectionVisuals();
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (args.Property == RowHeightProperty)
        {
            foreach (var row in GetRows())
            {
                row.Height = RowHeight;
            }

            InvalidateMeasure();
        }

        if (args.Property == ShowRowHeadersProperty ||
            args.Property == RowHeaderWidthProperty ||
            args.Property == ColumnHeaderHeightProperty)
        {
            InvalidateArrange();
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        RefreshAutoColumnsIfNeeded();
        EnsureColumnWidths();

        var border = BorderThickness;
        var innerWidth = MathF.Max(0f, availableSize.X - (border * 2f));
        var innerHeight = MathF.Max(0f, availableSize.Y - (border * 2f));

        var rowHeaderWidth = ShowRowHeaders ? RowHeaderWidth : 0f;
        var headerHeight = ColumnHeaderHeight;
        var headersWidth = GetColumnsTotalWidth();

        _cornerHeader.Font = Font;
        _cornerHeader.Measure(new Vector2(rowHeaderWidth, headerHeight));

        foreach (var header in _columnHeaders)
        {
            var width = _columnWidths[header.ColumnIndex];
            header.Measure(new Vector2(width, headerHeight));
        }

        var scrollWidth = MathF.Max(0f, innerWidth);
        var scrollHeight = MathF.Max(0f, innerHeight - headerHeight);
        _scrollViewer.Measure(new Vector2(scrollWidth, scrollHeight));

        var desiredWidth = MathF.Max(rowHeaderWidth + headersWidth, _scrollViewer.DesiredSize.X) + (border * 2f);
        var desiredHeight = headerHeight + _scrollViewer.DesiredSize.Y + (border * 2f);

        return new Vector2(desiredWidth, desiredHeight);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var border = BorderThickness;
        var innerX = LayoutSlot.X + border;
        var innerY = LayoutSlot.Y + border;
        var innerWidth = MathF.Max(0f, finalSize.X - (border * 2f));
        var innerHeight = MathF.Max(0f, finalSize.Y - (border * 2f));

        var rowHeaderWidth = ShowRowHeaders ? RowHeaderWidth : 0f;
        var headerHeight = ColumnHeaderHeight;
        var scrollY = innerY + headerHeight;
        var scrollHeight = MathF.Max(0f, innerHeight - headerHeight);

        _cornerHeader.IsVisible = ShowRowHeaders;
        _cornerHeader.Arrange(new LayoutRect(innerX, innerY, rowHeaderWidth, headerHeight));

        var headersX = innerX + rowHeaderWidth - _scrollViewer.HorizontalOffset;
        for (var i = 0; i < _columnHeaders.Count; i++)
        {
            var width = _columnWidths[i];
            _columnHeaders[i].Arrange(new LayoutRect(headersX, innerY, width, headerHeight));
            headersX += width;
        }

        _scrollViewer.Arrange(new LayoutRect(innerX, scrollY, innerWidth, scrollHeight));
        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        UiDrawing.DrawFilledRect(spriteBatch, LayoutSlot, Background, Opacity);

        if (BorderThickness > 0f)
        {
            UiDrawing.DrawRectStroke(spriteBatch, LayoutSlot, BorderThickness, BorderBrush, Opacity);
        }
    }

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        clipRect = LayoutSlot;
        return true;
    }


    internal void NotifyRowPressed(DataGridRow row, int cellIndex)
    {
        var targetColumn = SelectionUnit == DataGridSelectionUnit.Cell ? Math.Max(0, cellIndex) : -1;
        Select(row.RowIndex, targetColumn);
    }

    internal object? GetValueForCell(object? item, DataGridColumn column)
    {
        if (item == null)
        {
            return null;
        }

        var path = column.BindingPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return item;
        }

        var current = item;
        foreach (var segment in path.Split('.'))
        {
            if (current == null)
            {
                return null;
            }

            var property = current.GetType().GetProperty(segment, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property == null)
            {
                return null;
            }

            current = property.GetValue(current);
        }

        return current;
    }

    private void Select(int rowIndex, int columnIndex)
    {
        SelectedRowIndex = rowIndex;
        SelectedColumnIndex = SelectionUnit == DataGridSelectionUnit.Cell ? columnIndex : -1;
        UpdateSelectionVisuals();
    }

    private void UpdateSelectionVisuals()
    {
        foreach (var row in GetRows())
        {
            row.UpdateSelectionState(SelectionUnit, SelectedRowIndex, SelectedColumnIndex);
        }
    }

    private void EnsureRowVisible(int rowIndex)
    {
        var rowTop = rowIndex * RowHeight;
        var rowBottom = rowTop + RowHeight;
        var viewportTop = _scrollViewer.VerticalOffset;
        var viewportBottom = viewportTop + _scrollViewer.ViewportHeight;

        if (rowTop < viewportTop)
        {
            _scrollViewer.ScrollToVerticalOffset(rowTop);
        }
        else if (rowBottom > viewportBottom)
        {
            _scrollViewer.ScrollToVerticalOffset(MathF.Max(0f, rowBottom - _scrollViewer.ViewportHeight));
        }
    }

    private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshAutoColumnsIfNeeded();
        RebuildHeaders();
        SyncRowsHost();
        InvalidateMeasure();
    }

    private void RefreshAutoColumnsIfNeeded()
    {
        _resolvedColumns.Clear();
        if (_columns.Count > 0)
        {
            foreach (var column in _columns)
            {
                _resolvedColumns.Add(column);
            }
        }
        else if (Items.Count > 0)
        {
            var sample = Items[0];
            if (sample != null)
            {
                foreach (var property in sample.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    _resolvedColumns.Add(new DataGridColumn
                    {
                        Header = property.Name,
                        BindingPath = property.Name,
                        Width = float.NaN
                    });
                }
            }
        }

        if (_resolvedColumns.Count == 0)
        {
            _resolvedColumns.Add(new DataGridColumn { Header = "Value", BindingPath = string.Empty, Width = float.NaN });
        }

        EnsureColumnWidths();
    }

    private void EnsureColumnWidths()
    {
        _columnWidths.Clear();
        foreach (var column in _resolvedColumns)
        {
            _columnWidths.Add(column.GetResolvedWidth());
        }
    }

    private void RebuildHeaders()
    {
        foreach (var header in _columnHeaders)
        {
            header.Click -= OnColumnHeaderClick;
            header.SetVisualParent(null);
            header.SetLogicalParent(null);
        }

        _columnHeaders.Clear();

        for (var i = 0; i < _resolvedColumns.Count; i++)
        {
            var column = _resolvedColumns[i];
            var header = new DataGridColumnHeader
            {
                ColumnIndex = i,
                Text = string.IsNullOrWhiteSpace(column.Header) ? column.BindingPath : column.Header,
                Font = Font,
                SortDirection = column.SortDirection
            };
            header.Click += OnColumnHeaderClick;
            header.SetVisualParent(this);
            header.SetLogicalParent(this);
            _columnHeaders.Add(header);
        }
    }

    private void SyncRowsHost()
    {
        while (_rowsHost.Children.Count > 0)
        {
            _rowsHost.RemoveChild(_rowsHost.Children[^1]);
        }

        var rows = GetRows();
        for (var i = 0; i < rows.Count; i++)
        {
            rows[i].Configure(this, i, Items[i], _resolvedColumns, _columnWidths);
            rows[i].UpdateSelectionState(SelectionUnit, SelectedRowIndex, SelectedColumnIndex);
            if (!ReferenceEquals(rows[i].VisualParent, _rowsHost))
            {
                rows[i].SetVisualParent(null);
                rows[i].SetLogicalParent(null);
                _rowsHost.AddChild(rows[i]);
            }
        }

        _rowsHost.IsVirtualizing = EnableRowVirtualization;
    }

    private IReadOnlyList<DataGridRow> GetRows()
    {
        var rows = new List<DataGridRow>();
        foreach (var container in ItemContainers)
        {
            if (container is DataGridRow row)
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    private float GetColumnsTotalWidth()
    {
        var width = 0f;
        foreach (var columnWidth in _columnWidths)
        {
            width += columnWidth;
        }

        return width;
    }

    private void OnColumnHeaderClick(object? sender, RoutedSimpleEventArgs e)
    {
        if (sender is not DataGridColumnHeader header || !CanUserSortColumns)
        {
            return;
        }

        var index = header.ColumnIndex;
        if (index < 0 || index >= _resolvedColumns.Count)
        {
            return;
        }

        var column = _resolvedColumns[index];
        if (!column.CanUserSort)
        {
            return;
        }

        column.SortDirection = column.SortDirection switch
        {
            DataGridSortDirection.None => DataGridSortDirection.Ascending,
            DataGridSortDirection.Ascending => DataGridSortDirection.Descending,
            _ => DataGridSortDirection.None
        };

        header.SortDirection = column.SortDirection;
        Sorting?.Invoke(this, new DataGridSortingEventArgs(column, index));
    }

    private void OnScrollViewerDependencyPropertyChanged(object? sender, DependencyPropertyChangedEventArgs args)
    {
        if (args.Property == ScrollViewer.HorizontalOffsetProperty)
        {
            InvalidateArrange();
        }
    }
}
