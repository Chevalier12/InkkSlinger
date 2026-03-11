using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public class DataGrid : ItemsControl
{
    public new static readonly DependencyProperty FontProperty = Control.FontProperty;
    public new static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(nameof(Foreground), typeof(Color), typeof(DataGrid), new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));
    public new static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(nameof(Background), typeof(Color), typeof(DataGrid), new FrameworkPropertyMetadata(new Color(20, 30, 45), FrameworkPropertyMetadataOptions.AffectsRender));
    public new static readonly DependencyProperty BorderBrushProperty = DependencyProperty.Register(nameof(BorderBrush), typeof(Color), typeof(DataGrid), new FrameworkPropertyMetadata(new Color(69, 99, 132), FrameworkPropertyMetadataOptions.AffectsRender));
    public new static readonly DependencyProperty BorderThicknessProperty = DependencyProperty.Register(nameof(BorderThickness), typeof(float), typeof(DataGrid), new FrameworkPropertyMetadata(1f, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender, coerceValueCallback: static (_, value) => value is float v && v >= 0f ? v : 0f));
    public static readonly DependencyProperty RowBackgroundProperty = DependencyProperty.Register(nameof(RowBackground), typeof(Color), typeof(DataGrid), new FrameworkPropertyMetadata(new Color(24, 34, 49), FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty AlternatingRowBackgroundProperty = DependencyProperty.Register(nameof(AlternatingRowBackground), typeof(Color), typeof(DataGrid), new FrameworkPropertyMetadata(new Color(24, 34, 49), FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty HorizontalGridLinesBrushProperty = DependencyProperty.Register(nameof(HorizontalGridLinesBrush), typeof(Color), typeof(DataGrid), new FrameworkPropertyMetadata(new Color(69, 99, 132), FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty VerticalGridLinesBrushProperty = DependencyProperty.Register(nameof(VerticalGridLinesBrush), typeof(Color), typeof(DataGrid), new FrameworkPropertyMetadata(new Color(69, 99, 132), FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty GridLinesVisibilityProperty = DependencyProperty.Register(nameof(GridLinesVisibility), typeof(DataGridGridLinesVisibility), typeof(DataGrid), new FrameworkPropertyMetadata(DataGridGridLinesVisibility.None, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty HeadersVisibilityProperty = DependencyProperty.Register(nameof(HeadersVisibility), typeof(DataGridHeadersVisibility), typeof(DataGrid), new FrameworkPropertyMetadata(DataGridHeadersVisibility.All, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));
    public static readonly DependencyProperty RowHeightProperty = DependencyProperty.Register(nameof(RowHeight), typeof(float), typeof(DataGrid), new FrameworkPropertyMetadata(26f, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange, coerceValueCallback: static (_, value) => value is float v && v >= 16f ? v : 16f));
    public static readonly DependencyProperty ColumnHeaderHeightProperty = DependencyProperty.Register(nameof(ColumnHeaderHeight), typeof(float), typeof(DataGrid), new FrameworkPropertyMetadata(28f, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange, coerceValueCallback: static (_, value) => value is float v && v >= 16f ? v : 16f));
    public static readonly DependencyProperty RowHeaderWidthProperty = DependencyProperty.Register(nameof(RowHeaderWidth), typeof(float), typeof(DataGrid), new FrameworkPropertyMetadata(42f, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange, coerceValueCallback: static (_, value) => value is float v && v >= 0f ? v : 0f));
    public static readonly DependencyProperty ShowRowHeadersProperty = DependencyProperty.Register(nameof(ShowRowHeaders), typeof(bool), typeof(DataGrid), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));
    public static readonly DependencyProperty SelectionUnitProperty = DependencyProperty.Register(nameof(SelectionUnit), typeof(DataGridSelectionUnit), typeof(DataGrid), new FrameworkPropertyMetadata(DataGridSelectionUnit.FullRow, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty SelectedRowIndexProperty = DependencyProperty.Register(nameof(SelectedRowIndex), typeof(int), typeof(DataGrid), new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender, propertyChangedCallback: static (dependencyObject, _) => { if (dependencyObject is DataGrid grid) { grid.UpdateSelectionVisuals(); } }));
    public static readonly DependencyProperty SelectedColumnIndexProperty = DependencyProperty.Register(nameof(SelectedColumnIndex), typeof(int), typeof(DataGrid), new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender, propertyChangedCallback: static (dependencyObject, _) => { if (dependencyObject is DataGrid grid) { grid.UpdateSelectionVisuals(); } }));
    public static readonly DependencyProperty EnableRowVirtualizationProperty = DependencyProperty.Register(nameof(EnableRowVirtualization), typeof(bool), typeof(DataGrid), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange, propertyChangedCallback: static (dependencyObject, args) =>
    {
        if (dependencyObject is DataGrid grid && args.NewValue is bool enabled)
        {
            grid._rowsPresenter.RowsHost.IsVirtualizing = enabled;
            grid.SyncRowsHost();
        }
    }));
    public static readonly DependencyProperty CanUserSortColumnsProperty = DependencyProperty.Register(nameof(CanUserSortColumns), typeof(bool), typeof(DataGrid), new FrameworkPropertyMetadata(true));
    public static readonly DependencyProperty RowDetailsTemplateProperty = DependencyProperty.Register(nameof(RowDetailsTemplate), typeof(DataTemplate), typeof(DataGrid), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));
    public static readonly DependencyProperty RowDetailsVisibilityModeProperty = DependencyProperty.Register(nameof(RowDetailsVisibilityMode), typeof(DataGridRowDetailsVisibilityMode), typeof(DataGrid), new FrameworkPropertyMetadata(DataGridRowDetailsVisibilityMode.Collapsed, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange, propertyChangedCallback: static (dependencyObject, _) => { if (dependencyObject is DataGrid grid) { grid.RefreshRowDetailsState(); grid.SyncRowsHost(); } }));
    public static readonly DependencyProperty CanUserResizeColumnsProperty = DependencyProperty.Register(nameof(CanUserResizeColumns), typeof(bool), typeof(DataGrid), new FrameworkPropertyMetadata(true));
    public static readonly DependencyProperty CanUserReorderColumnsProperty = DependencyProperty.Register(nameof(CanUserReorderColumns), typeof(bool), typeof(DataGrid), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty FrozenColumnCountProperty = DependencyProperty.Register(nameof(FrozenColumnCount), typeof(int), typeof(DataGrid), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender, coerceValueCallback: static (_, value) => value is int count && count >= 0 ? count : 0));

    private const float HeaderResizeGripWidth = 6f;
    private const float HeaderDragThreshold = 4f;

    private readonly ObservableCollection<DataGridColumn> _columns = new();
    private readonly DataGridState _state = new();
    private readonly DataGridColumnHeadersPresenter _headersPresenter = new();
    private readonly DataGridRowsPresenter _rowsPresenter;
    private readonly Dictionary<string, Func<object?, object?>> _valueReaderCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<INotifyPropertyChanged, int> _observedItems = new();
    private readonly DataGridRowHeader _cornerHeader;
    private DataGridColumnState[]? _displayColumnsCache;
    private long _lastPointerPressedTimestamp;
    private int _lastPointerPressedRowIndex = -1;
    private int _lastPointerPressedColumnIndex = -1;
    private bool _isSynchronizingSelectionProperties;
    private bool _isFocusedFromInput;
    private bool _isResizingColumn;
    private bool _isPotentialReorder;
    private bool _isDraggingColumn;
    private bool _syncGridChromeDeferredQueued;
    private bool _pendingDeferredChromeInvalidateMeasure;
    private bool _columnStatesDirty = true;
    private int _dragColumnDisplayIndex = -1;
    private float _dragStartPointerX;
    private float _resizeStartPointerX;
    private float _resizeStartWidth;
    private int _resizeColumnDisplayIndex = -1;
    private const long PointerEditRepeatWindowMs = 500;

    public DataGrid()
    {
        Focusable = true;
        _rowsPresenter = new DataGridRowsPresenter(this);
        _cornerHeader = new DataGridRowHeader { Text = string.Empty };
        _cornerHeader.SetVisualParent(this);
        _cornerHeader.SetLogicalParent(this);
        _columns.CollectionChanged += OnColumnsChanged;
        _rowsPresenter.ScrollViewer.DependencyPropertyChanged += OnScrollViewerDependencyPropertyChanged;
    }

    public event EventHandler<DataGridSortingEventArgs>? Sorting;
    public event EventHandler<DataGridBeginningEditEventArgs>? BeginningEdit;
    public event EventHandler<DataGridPreparingCellForEditEventArgs>? PreparingCellForEdit;
    public event EventHandler<DataGridCellEditEndingEventArgs>? CellEditEnding;
    public event EventHandler<DataGridRowEditEndingEventArgs>? RowEditEnding;

    public ObservableCollection<DataGridColumn> Columns => _columns;
    public new SpriteFont? Font { get => GetValue<SpriteFont>(FontProperty); set => SetValue(FontProperty, value); }
    public new Color Foreground { get => GetValue<Color>(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public new Color Background { get => GetValue<Color>(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
    public new Color BorderBrush { get => GetValue<Color>(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }
    public new float BorderThickness { get => GetValue<float>(BorderThicknessProperty); set => SetValue(BorderThicknessProperty, value); }
    public Color RowBackground { get => GetValue<Color>(RowBackgroundProperty); set => SetValue(RowBackgroundProperty, value); }
    public Color AlternatingRowBackground { get => GetValue<Color>(AlternatingRowBackgroundProperty); set => SetValue(AlternatingRowBackgroundProperty, value); }
    public Color HorizontalGridLinesBrush { get => GetValue<Color>(HorizontalGridLinesBrushProperty); set => SetValue(HorizontalGridLinesBrushProperty, value); }
    public Color VerticalGridLinesBrush { get => GetValue<Color>(VerticalGridLinesBrushProperty); set => SetValue(VerticalGridLinesBrushProperty, value); }
    public DataGridGridLinesVisibility GridLinesVisibility { get => GetValue<DataGridGridLinesVisibility>(GridLinesVisibilityProperty); set => SetValue(GridLinesVisibilityProperty, value); }
    public DataGridHeadersVisibility HeadersVisibility { get => GetValue<DataGridHeadersVisibility>(HeadersVisibilityProperty); set => SetValue(HeadersVisibilityProperty, value); }
    public float RowHeight { get => GetValue<float>(RowHeightProperty); set => SetValue(RowHeightProperty, value); }
    public float ColumnHeaderHeight { get => GetValue<float>(ColumnHeaderHeightProperty); set => SetValue(ColumnHeaderHeightProperty, value); }
    public float RowHeaderWidth { get => GetValue<float>(RowHeaderWidthProperty); set => SetValue(RowHeaderWidthProperty, value); }
    public bool ShowRowHeaders { get => GetValue<bool>(ShowRowHeadersProperty); set => SetValue(ShowRowHeadersProperty, value); }
    public DataGridSelectionUnit SelectionUnit { get => GetValue<DataGridSelectionUnit>(SelectionUnitProperty); set => SetValue(SelectionUnitProperty, value); }
    public int SelectedRowIndex { get => GetValue<int>(SelectedRowIndexProperty); set => SetValue(SelectedRowIndexProperty, value); }
    public int SelectedColumnIndex { get => GetValue<int>(SelectedColumnIndexProperty); set => SetValue(SelectedColumnIndexProperty, value); }
    public bool EnableRowVirtualization { get => GetValue<bool>(EnableRowVirtualizationProperty); set => SetValue(EnableRowVirtualizationProperty, value); }
    public bool CanUserSortColumns { get => GetValue<bool>(CanUserSortColumnsProperty); set => SetValue(CanUserSortColumnsProperty, value); }
    public DataTemplate? RowDetailsTemplate { get => GetValue<DataTemplate>(RowDetailsTemplateProperty); set => SetValue(RowDetailsTemplateProperty, value); }
    public DataGridRowDetailsVisibilityMode RowDetailsVisibilityMode { get => GetValue<DataGridRowDetailsVisibilityMode>(RowDetailsVisibilityModeProperty); set => SetValue(RowDetailsVisibilityModeProperty, value); }
    public bool CanUserResizeColumns { get => GetValue<bool>(CanUserResizeColumnsProperty); set => SetValue(CanUserResizeColumnsProperty, value); }
    public bool CanUserReorderColumns { get => GetValue<bool>(CanUserReorderColumnsProperty); set => SetValue(CanUserReorderColumnsProperty, value); }
    public int FrozenColumnCount { get => GetValue<int>(FrozenColumnCountProperty); set => SetValue(FrozenColumnCountProperty, value); }

    internal int RealizedRowCountForTesting => _rowsPresenter.RowsHost.RealizedChildrenCount;
    internal ScrollViewer ScrollViewerForTesting => _rowsPresenter.ScrollViewer;
    internal VirtualizingStackPanel RowsHostForTesting => _rowsPresenter.RowsHost;
    internal IReadOnlyList<DataGridRow> RowsForTesting => _rowsPresenter.GetRows(GetItemContainersForPresenter());
    internal bool ColumnHeadersVisibleForTesting => ColumnHeadersVisibleForLayout;
    internal bool RowHeadersVisibleForTesting => RowHeadersVisibleForLayout;
    internal bool HorizontalGridLinesVisibleForTesting => HorizontalGridLinesVisibleForLayout;
    internal bool VerticalGridLinesVisibleForTesting => VerticalGridLinesVisibleForLayout;
    internal IReadOnlyList<DataGridColumnHeader> ColumnHeadersForTesting => _headersPresenter.Headers;
    internal IReadOnlyList<DataGridColumnState> DisplayColumnsForTesting => GetDisplayColumns();
    internal int CurrentRowIndexForTesting => _state.Selection.CurrentRowIndex;
    internal int CurrentColumnIndexForTesting => _state.Selection.CurrentColumnIndex;
    internal int EditingRowIndexForTesting => _state.Edit.EditingRowIndex;
    internal int EditingColumnIndexForTesting => _state.Edit.EditingColumnIndex;
    internal float HorizontalOffsetForTesting => _rowsPresenter.ScrollViewer.HorizontalOffset;
    internal float EffectiveRowHeightForLayout => GetEffectiveRowHeight();

    protected override bool IncludeGeneratedChildrenInVisualTree => false;
    protected override bool CanReconcileProjectedContainersOnReset => true;

    public override void InvalidateMeasure()
    {
        base.InvalidateMeasure();
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        yield return _cornerHeader;
        foreach (var header in _headersPresenter.Headers) { yield return header; }
        yield return _rowsPresenter.ScrollViewer;
    }

    internal override int GetVisualChildCountForTraversal()
    {
        return _headersPresenter.Headers.Count + 2;
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        if (index == 0)
        {
            return _cornerHeader;
        }

        if (index == _headersPresenter.Headers.Count + 1)
        {
            return _rowsPresenter.ScrollViewer;
        }

        var headerIndex = index - 1;
        if ((uint)headerIndex < (uint)_headersPresenter.Headers.Count)
        {
            return _headersPresenter.Headers[headerIndex];
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        yield return _cornerHeader;
        foreach (var header in _headersPresenter.Headers) { yield return header; }
        yield return _rowsPresenter.ScrollViewer;
    }

    protected override bool IsItemItsOwnContainerOverride(object item) => item is DataGridRow;
    protected override UIElement CreateContainerForItemOverride(object item) => new DataGridRow();

    protected override void PrepareContainerForItemOverride(UIElement element, object item, int index)
    {
        base.PrepareContainerForItemOverride(element, item, index);
    }

    protected override void OnItemsChanged()
    {
        base.OnItemsChanged();
        RefreshGridState(refreshColumns: true);
    }

    protected override void OnItemsIncrementalChanged(NotifyCollectionChangedEventArgs e)
    {
        _ = e;
        RefreshGridState(refreshColumns: _columns.Count == 0);
    }

    protected override void OnItemsResetReconciled()
    {
        RefreshGridState(refreshColumns: false, invalidateMeasure: false);
    }

    protected override bool ShouldInvalidateMeasureOnItemsResetReconciled => false;

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);
        if (args.Property == FontProperty ||
            args.Property == FontSizeProperty ||
            args.Property == RowHeightProperty ||
            args.Property == ShowRowHeadersProperty ||
            args.Property == RowHeaderWidthProperty ||
            args.Property == ColumnHeaderHeightProperty ||
            args.Property == HeadersVisibilityProperty ||
            args.Property == RowDetailsTemplateProperty)
        {
            RequestDeferredSyncGridChrome(invalidateMeasure: true);
        }
        else if (args.Property == ForegroundProperty ||
                 args.Property == RowBackgroundProperty ||
                 args.Property == AlternatingRowBackgroundProperty ||
                 args.Property == GridLinesVisibilityProperty ||
                 args.Property == HorizontalGridLinesBrushProperty ||
                 args.Property == VerticalGridLinesBrushProperty ||
                 args.Property == FrozenColumnCountProperty)
        {
            RequestDeferredSyncGridChrome(invalidateMeasure: false);
        }

        if (args.Property == SelectionUnitProperty)
        {
            SyncSelectionStateFromProperties();
            UpdateSelectionVisuals();
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        EnsureColumnStatesCurrent();
        var border = BorderThickness;
        var innerWidth = MathF.Max(0f, availableSize.X - (border * 2f));
        var innerHeight = MathF.Max(0f, availableSize.Y - (border * 2f));
        var rowHeaderWidth = RowHeadersVisibleForLayout ? RowHeaderWidth : 0f;
        var headerHeight = ColumnHeadersVisibleForLayout ? GetEffectiveColumnHeaderHeight() : 0f;
        _cornerHeader.Font = Font;
        _cornerHeader.Measure(new Vector2(rowHeaderWidth, headerHeight));
        _headersPresenter.MeasureHeaders(GetDisplayColumns(), headerHeight);
        _rowsPresenter.ScrollViewer.Measure(new Vector2(innerWidth, MathF.Max(0f, innerHeight - headerHeight)));
        var desired = new Vector2(
            MathF.Max(rowHeaderWidth + GetColumnsTotalWidth(), _rowsPresenter.ScrollViewer.DesiredSize.X) + (border * 2f),
            headerHeight + _rowsPresenter.ScrollViewer.DesiredSize.Y + (border * 2f));
        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var border = BorderThickness;
        var innerX = LayoutSlot.X + border;
        var innerY = LayoutSlot.Y + border;
        var innerWidth = MathF.Max(0f, finalSize.X - (border * 2f));
        var innerHeight = MathF.Max(0f, finalSize.Y - (border * 2f));
        var rowHeaderWidth = RowHeadersVisibleForLayout ? RowHeaderWidth : 0f;
        var headerHeight = ColumnHeadersVisibleForLayout ? GetEffectiveColumnHeaderHeight() : 0f;
        _cornerHeader.IsVisible = RowHeadersVisibleForLayout && ColumnHeadersVisibleForLayout;
        _cornerHeader.Arrange(new LayoutRect(innerX, innerY, rowHeaderWidth, headerHeight));
        _rowsPresenter.ScrollViewer.Arrange(new LayoutRect(innerX, innerY + headerHeight, innerWidth, MathF.Max(0f, innerHeight - headerHeight)));
        _headersPresenter.ArrangeHeaders(this, innerX, innerY, rowHeaderWidth, headerHeight, Math.Clamp(FrozenColumnCount, 0, _state.Columns.Count), _rowsPresenter.ScrollViewer.HorizontalOffset, ColumnHeadersVisibleForLayout);
        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        UiDrawing.DrawFilledRect(spriteBatch, LayoutSlot, Background, Opacity);
        if (BorderThickness > 0f) { UiDrawing.DrawRectStroke(spriteBatch, LayoutSlot, BorderThickness, BorderBrush, Opacity); }
    }

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        clipRect = LayoutSlot;
        return true;
    }

    public bool BeginEdit() => BeginEdit(DataGridEditTriggerSource.Keyboard, null);
    public bool CommitEdit() => CommitEditInternal(DataGridEditAction.Commit, moveAfterCommit: false, Keys.None, ModifierKeys.None);
    public bool CancelEdit() => CommitEditInternal(DataGridEditAction.Cancel, moveAfterCommit: false, Keys.None, ModifierKeys.None);

    internal bool HandleKeyDownFromInput(Keys key, ModifierKeys modifiers)
    {
        if (!IsEnabled || !_isFocusedFromInput) { return false; }
        EnsureCurrentCellInitialized();

        if (_state.Edit.IsEditing)
        {
            if (key == Keys.Escape) { return CancelEdit(); }
            if (key == Keys.Enter) { return CommitEditInternal(DataGridEditAction.Commit, moveAfterCommit: true, Keys.Enter, modifiers); }
            if (key == Keys.Tab) { return CommitEditInternal(DataGridEditAction.Commit, moveAfterCommit: true, Keys.Tab, modifiers); }
            return ForwardEditorKeyInput(key, modifiers);
        }

        return key switch
        {
            Keys.F2 => BeginEdit(DataGridEditTriggerSource.Keyboard, null),
            Keys.Left => MoveCurrentCellByColumn(-1),
            Keys.Right => MoveCurrentCellByColumn(1),
            Keys.Up => MoveCurrentCellByRow(-1),
            Keys.Down => MoveCurrentCellByRow(1),
            Keys.Home when (modifiers & ModifierKeys.Control) != 0 => MoveCurrentCellTo(0, 0),
            Keys.Home => MoveCurrentCellTo(_state.Selection.CurrentRowIndex, 0),
            Keys.End when (modifiers & ModifierKeys.Control) != 0 => MoveCurrentCellTo(_state.Rows.Count - 1, Math.Max(0, GetDisplayColumns().Count - 1)),
            Keys.End => MoveCurrentCellTo(_state.Selection.CurrentRowIndex, Math.Max(0, GetDisplayColumns().Count - 1)),
            Keys.PageUp => MoveCurrentCellByRow(-GetViewportRowStep()),
            Keys.PageDown => MoveCurrentCellByRow(GetViewportRowStep()),
            Keys.Tab => MoveCurrentCellByTab((modifiers & ModifierKeys.Shift) != 0 ? -1 : 1),
            Keys.Enter => MoveCurrentCellByRow(1),
            _ => false
        };
    }

    internal bool HandleTextInputFromInput(char character)
    {
        if (!IsEnabled || !_isFocusedFromInput) { return false; }
        if (_state.Edit.IsEditing) { return ForwardEditorTextInput(character); }
        if (char.IsControl(character)) { return false; }
        return BeginEdit(DataGridEditTriggerSource.TextInput, null) && ForwardEditorTextInput(character);
    }

    internal bool ShouldRetainFocusForInputTarget(UIElement target)
    {
        return _state.Edit.IsEditing && IsWithinActiveEditor(target);
    }

    internal void SetFocusedFromInput(bool isFocused)
    {
        _isFocusedFromInput = isFocused;
        if (_state.Edit.ActiveEditorElement is TextBox textBox) { textBox.SetFocusedFromInput(isFocused); }
        if (!isFocused && _state.Edit.IsEditing)
        {
            var focusedElement = FocusManager.GetFocusedElement();
            if (focusedElement == null || !IsWithinActiveEditor(focusedElement))
            {
                _ = CommitEdit();
            }
        }
    }

    internal bool HandlePointerDownFromInput(UIElement target, Vector2 pointerPosition, out bool capturePointer)
    {
        capturePointer = false;
        if (!TryResolveGridTarget(target, out var row, out var cell, out var header)) { return false; }
        if (header != null)
        {
            ResetPointerEditTracking();
            var displayIndex = header.ColumnIndex;
            if (TryStartHeaderResize(displayIndex, pointerPosition)) { capturePointer = true; return true; }
            if (CanUserReorderColumns && CanDisplayColumnReorder(displayIndex))
            {
                _isPotentialReorder = true; _isDraggingColumn = false; _dragColumnDisplayIndex = displayIndex; _dragStartPointerX = pointerPosition.X; capturePointer = true; return true;
            }

            return false;
        }

        if (row == null) { return false; }
        var columnIndex = cell?.ColumnIndex ?? row.ResolveCellIndexAtPoint(pointerPosition);
        var isRepeatedPress = IsRepeatedPointerPress(row.RowIndex, columnIndex);
        Select(row.RowIndex, columnIndex, preserveCurrentWhenFullRow: false);
        RecordPointerPress(row.RowIndex, columnIndex);
        if (isRepeatedPress && IsEditableDisplayColumn(columnIndex))
        {
            _ = BeginEdit(DataGridEditTriggerSource.Pointer, null);
        }

        return true;
    }

    internal bool HandlePointerMoveFromInput(Vector2 pointerPosition)
    {
        if (_isResizingColumn) { SetDisplayColumnWidth(_resizeColumnDisplayIndex, MathF.Max(0f, _resizeStartWidth + (pointerPosition.X - _resizeStartPointerX))); return true; }
        if (_isPotentialReorder && !_isDraggingColumn && MathF.Abs(pointerPosition.X - _dragStartPointerX) >= HeaderDragThreshold) { _isDraggingColumn = true; }
        return _isDraggingColumn;
    }

    internal bool HandlePointerUpFromInput(Vector2 pointerPosition)
    {
        if (_isResizingColumn) { _isResizingColumn = false; _resizeColumnDisplayIndex = -1; return true; }
        if (!_isPotentialReorder) { return false; }
        var sourceDisplayIndex = _dragColumnDisplayIndex;
        var dragged = _isDraggingColumn;
        ResetHeaderPointerState();
        if (!dragged) { SortByDisplayColumnIndex(sourceDisplayIndex); return true; }
        var targetDisplayIndex = ResolveHeaderDisplayIndexAtPointer(pointerPosition);
        if (targetDisplayIndex >= 0 && targetDisplayIndex != sourceDisplayIndex) { MoveDisplayColumn(sourceDisplayIndex, targetDisplayIndex); }
        return true;
    }

    internal object? ResolveCellContent(object? item, DataGridColumn column)
    {
        if (item == null) { return null; }
        if (string.IsNullOrWhiteSpace(column.BindingPath)) { return item; }
        if (!_valueReaderCache.TryGetValue(column.BindingPath, out var reader)) { reader = BuildValueReader(column.BindingPath, item.GetType()); _valueReaderCache[column.BindingPath] = reader; }
        return reader(item);
    }

    internal bool RowHeadersVisibleForLayout => ShowRowHeaders && (HeadersVisibility == DataGridHeadersVisibility.Row || HeadersVisibility == DataGridHeadersVisibility.All);
    internal bool ColumnHeadersVisibleForLayout => HeadersVisibility == DataGridHeadersVisibility.Column || HeadersVisibility == DataGridHeadersVisibility.All;
    internal bool HorizontalGridLinesVisibleForLayout => GridLinesVisibility == DataGridGridLinesVisibility.Horizontal || GridLinesVisibility == DataGridGridLinesVisibility.All;
    internal bool VerticalGridLinesVisibleForLayout => GridLinesVisibility == DataGridGridLinesVisibility.Vertical || GridLinesVisibility == DataGridGridLinesVisibility.All;
    internal void NotifyRowPressed(DataGridRow row, int cellIndex) => Select(row.RowIndex, cellIndex, preserveCurrentWhenFullRow: false);

    private bool BeginEdit(DataGridEditTriggerSource triggerSource, char? initialText)
    {
        EnsureCurrentCellInitialized();
        if (_state.Edit.IsEditing) { return true; }
        var rowIndex = _state.Selection.CurrentRowIndex;
        var columnIndex = _state.Selection.CurrentColumnIndex;
        if (!TryGetCell(rowIndex, columnIndex, out var row, out var cell, out var columnState) || columnState.Column.IsReadOnly) { return false; }

        var beginningArgs = new DataGridBeginningEditEventArgs(row.Item, rowIndex, columnState.Column, columnIndex, triggerSource);
        BeginningEdit?.Invoke(this, beginningArgs);
        if (beginningArgs.Cancel) { return false; }

        var editorElement = columnState.Column.CreateEditingElement(row.Item, this);
        if (editorElement == null) { return false; }

        _state.Edit.EditingRowIndex = rowIndex;
        _state.Edit.EditingColumnIndex = columnIndex;
        _state.Edit.ActiveEditorElement = editorElement;
        _state.Edit.TriggerSource = triggerSource;
        _state.Edit.OriginalValue = ResolveCellContent(row.Item, columnState.Column);
        cell.BeginEdit(editorElement);
        if (editorElement is TextBox textBox)
        {
            var displayedTypography = cell.GetDisplayedTypography();
            textBox.Font = displayedTypography.Font;
            textBox.FontSize = displayedTypography.FontSize;
            textBox.FontFamily = displayedTypography.FontFamily;
            textBox.FontWeight = displayedTypography.FontWeight;
            textBox.Foreground = displayedTypography.Foreground;
            textBox.SetFocusedFromInput(_isFocusedFromInput);
        }

        PreparingCellForEdit?.Invoke(this, new DataGridPreparingCellForEditEventArgs(row.Item, rowIndex, columnState.Column, columnIndex, editorElement));
        return true;
    }

    private bool CommitEditInternal(DataGridEditAction action, bool moveAfterCommit, Keys navigationKey, ModifierKeys modifiers)
    {
        if (!_state.Edit.IsEditing) { return false; }
        var rowIndex = _state.Edit.EditingRowIndex;
        var columnIndex = _state.Edit.EditingColumnIndex;
        if (!TryGetCell(rowIndex, columnIndex, out var row, out var cell, out var columnState)) { EndEditSession(null); return false; }

        var editor = _state.Edit.ActiveEditorElement!;
        var cellArgs = new DataGridCellEditEndingEventArgs(row.Item, rowIndex, columnState.Column, columnIndex, editor, action);
        CellEditEnding?.Invoke(this, cellArgs);
        if (cellArgs.Cancel) { return false; }
        var rowArgs = new DataGridRowEditEndingEventArgs(row.Item, rowIndex, action);
        RowEditEnding?.Invoke(this, rowArgs);
        if (rowArgs.Cancel) { return false; }

        var commitSucceeded = action == DataGridEditAction.Cancel || TryCommitEditorValue(editor, row.Item, columnState.Column);
        if (!commitSucceeded) { return false; }

        cell.EndEdit();
        cell.RefreshContentFromState();
        EndEditSession(cell);

        if (moveAfterCommit && action == DataGridEditAction.Commit)
        {
            if (navigationKey == Keys.Enter) { _ = MoveCurrentCellByRow(1); }
            else if (navigationKey == Keys.Tab) { _ = MoveCurrentCellByTab((modifiers & ModifierKeys.Shift) != 0 ? -1 : 1); }
        }

        return true;
    }

    private bool TryCommitEditorValue(UIElement editor, object? item, DataGridColumn column)
    {
        if (editor is TextBox textBox)
        {
            Validation.ClearErrors(textBox, this);
            if (!TryStageEditedValue(item, column.BindingPath, textBox.Text, out var rollbackSnapshot, out var validationMessage))
            {
                Validation.SetErrors(textBox, this, [new ValidationError(null, this, validationMessage ?? "Invalid cell value.")]);
                textBox.SetFocusedFromInput(_isFocusedFromInput);
                return false;
            }

            if (BindingGroup != null && !BindingGroup.CommitEdit())
            {
                rollbackSnapshot.Restore();
                return false;
            }
        }

        return true;
    }

    private void EndEditSession(DataGridCell? cell)
    {
        if (_state.Edit.ActiveEditorElement is TextBox textBox) { textBox.SetFocusedFromInput(false); }
        cell?.EndEdit();
        _state.Edit.Reset();
        SyncRowsHost();
    }

    private bool ForwardEditorTextInput(char character) => _state.Edit.ActiveEditorElement is TextBox textBox && textBox.HandleTextInputFromInput(character);
    private bool ForwardEditorKeyInput(Keys key, ModifierKeys modifiers) => _state.Edit.ActiveEditorElement is TextBox textBox && textBox.HandleKeyDownFromInput(key, modifiers);

    private bool MoveCurrentCellByColumn(int delta) => MoveCurrentCellTo(_state.Selection.CurrentRowIndex, Math.Clamp(_state.Selection.CurrentColumnIndex + delta, 0, Math.Max(0, GetDisplayColumns().Count - 1)));
    private bool MoveCurrentCellByRow(int delta) => MoveCurrentCellTo(Math.Clamp(_state.Selection.CurrentRowIndex + delta, 0, Math.Max(0, _state.Rows.Count - 1)), _state.Selection.CurrentColumnIndex);

    private bool MoveCurrentCellByTab(int delta)
    {
        var columnCount = GetDisplayColumns().Count;
        if (columnCount == 0 || _state.Rows.Count == 0) { return false; }
        var flatIndex = Math.Clamp((_state.Selection.CurrentRowIndex * columnCount) + _state.Selection.CurrentColumnIndex + delta, 0, (_state.Rows.Count * columnCount) - 1);
        return MoveCurrentCellTo(flatIndex / columnCount, flatIndex % columnCount);
    }

    private bool MoveCurrentCellTo(int rowIndex, int displayColumnIndex)
    {
        if (_state.Rows.Count == 0 || _state.Columns.Count == 0) { return false; }
        Select(Math.Clamp(rowIndex, 0, _state.Rows.Count - 1), Math.Clamp(displayColumnIndex, 0, _state.Columns.Count - 1), preserveCurrentWhenFullRow: false);
        return true;
    }

    private void Select(int rowIndex, int displayColumnIndex, bool preserveCurrentWhenFullRow)
    {
        var clampedRow = rowIndex >= 0 && rowIndex < _state.Rows.Count ? rowIndex : -1;
        var clampedColumn = displayColumnIndex >= 0 && displayColumnIndex < _state.Columns.Count ? displayColumnIndex : -1;
        _state.Selection.SelectedRowIndex = clampedRow;
        _state.Selection.CurrentRowIndex = clampedRow;
        _state.Selection.CurrentColumnIndex = clampedColumn;
        _state.Selection.SelectedColumnIndex = SelectionUnit == DataGridSelectionUnit.Cell ? clampedColumn : -1;
        if (SelectionUnit != DataGridSelectionUnit.Cell && preserveCurrentWhenFullRow && _state.Selection.CurrentColumnIndex < 0) { _state.Selection.CurrentColumnIndex = Math.Max(0, clampedColumn); }
        RefreshRowDetailsState();
        SyncSelectionPropertiesFromState();
        UpdateSelectionVisuals();
        EnsureCurrentCellVisible();
    }

    private void EnsureCurrentCellInitialized()
    {
        if (_state.Rows.Count == 0 || _state.Columns.Count == 0) { return; }
        if (_state.Selection.CurrentRowIndex < 0 || _state.Selection.CurrentRowIndex >= _state.Rows.Count) { _state.Selection.CurrentRowIndex = SelectedRowIndex >= 0 ? SelectedRowIndex : 0; }
        if (_state.Selection.CurrentColumnIndex < 0 || _state.Selection.CurrentColumnIndex >= _state.Columns.Count) { _state.Selection.CurrentColumnIndex = SelectedColumnIndex >= 0 ? SelectedColumnIndex : 0; }
    }

    private void EnsureCurrentCellVisible()
    {
        if (_state.Selection.CurrentRowIndex < 0 || _state.Selection.CurrentColumnIndex < 0) { return; }
        EnsureRowVisible(_state.Selection.CurrentRowIndex);
        EnsureColumnVisible(_state.Selection.CurrentColumnIndex);
    }

    private void EnsureRowVisible(int rowIndex)
    {
        if (TryGetRealizedRowViewportDelta(rowIndex, out var delta))
        {
            if (delta < 0f)
            {
                _rowsPresenter.ScrollViewer.ScrollToVerticalOffset(MathF.Max(0f, _rowsPresenter.ScrollViewer.VerticalOffset + delta));
            }
            else if (delta > 0f)
            {
                _rowsPresenter.ScrollViewer.ScrollToVerticalOffset(_rowsPresenter.ScrollViewer.VerticalOffset + delta);
            }

            return;
        }

        var rowHeight = GetEffectiveRowHeight();
        var rowTop = rowIndex * rowHeight;
        var rowBottom = rowTop + rowHeight;
        var viewportTop = _rowsPresenter.ScrollViewer.VerticalOffset;
        var viewportBottom = viewportTop + _rowsPresenter.ScrollViewer.ViewportHeight;
        if (rowTop < viewportTop) { _rowsPresenter.ScrollViewer.ScrollToVerticalOffset(rowTop); }
        else if (rowBottom > viewportBottom) { _rowsPresenter.ScrollViewer.ScrollToVerticalOffset(MathF.Max(0f, rowBottom - _rowsPresenter.ScrollViewer.ViewportHeight)); }
    }

    private void EnsureColumnVisible(int displayColumnIndex)
    {
        var displayColumns = GetDisplayColumns();
        if (displayColumnIndex < 0 || displayColumnIndex >= displayColumns.Count || displayColumnIndex < FrozenColumnCount) { return; }
        var cellLeft = 0f;
        for (var i = 0; i < displayColumnIndex; i++) { cellLeft += displayColumns[i].Width; }
        var cellRight = cellLeft + displayColumns[displayColumnIndex].Width;
        var frozenWidth = 0f;
        for (var i = 0; i < Math.Min(FrozenColumnCount, displayColumns.Count); i++) { frozenWidth += displayColumns[i].Width; }
        var viewportLeft = _rowsPresenter.ScrollViewer.HorizontalOffset + frozenWidth;
        var viewportRight = _rowsPresenter.ScrollViewer.HorizontalOffset + _rowsPresenter.ScrollViewer.ViewportWidth;
        if (cellLeft < viewportLeft) { _rowsPresenter.ScrollViewer.ScrollToHorizontalOffset(MathF.Max(0f, cellLeft - frozenWidth)); }
        else if (cellRight > viewportRight) { _rowsPresenter.ScrollViewer.ScrollToHorizontalOffset(MathF.Max(0f, cellRight - _rowsPresenter.ScrollViewer.ViewportWidth)); }
    }

    private int GetViewportRowStep()
    {
        var rowHeight = MathF.Max(1f, GetEffectiveRowHeight());
        return Math.Max(1, (int)MathF.Floor(_rowsPresenter.ScrollViewer.ViewportHeight / rowHeight));
    }

    private void RefreshGridState(bool refreshColumns, bool invalidateMeasure = true)
    {
        if (refreshColumns)
        {
            RefreshColumnStates();
        }
        else
        {
            EnsureColumnStatesCurrent();
            SyncColumnStateSortDirectionsFromView();
        }
        RefreshRowStates();
        RefreshRowDetailsState();
        SyncSelectionStateFromProperties();
        _headersPresenter.SyncHeaders(this, GetDisplayColumns(), OnColumnHeaderClick);
        SyncRowsHost(invalidateMeasure: invalidateMeasure);
        SyncObservedItems();
        if (invalidateMeasure)
        {
            InvalidateMeasure();
        }
        InvalidateArrange();
        InvalidateVisual();
    }

    private void RefreshColumnStates()
    {
        _valueReaderCache.Clear();
        _displayColumnsCache = null;
        var existingSort = _state.Columns.ToDictionary(static item => item.Column, static item => item.SortDirection);
        _state.Columns.Clear();
        if (_columns.Count > 0)
        {
            _state.AutoGeneratedItemType = null;
            for (var i = 0; i < _columns.Count; i++)
            {
                var column = _columns[i];
                _state.Columns.Add(new DataGridColumnState { Column = column, DisplayIndex = column.DisplayIndex >= 0 ? column.DisplayIndex : i, Width = column.GetResolvedWidth(), SortDirection = existingSort.TryGetValue(column, out var sortDirection) ? sortDirection : column.SortDirection });
            }
        }
        else
        {
            BuildAutoGeneratedColumns();
        }

        NormalizeDisplayIndexes();
        SyncColumnStateSortDirectionsFromView();
        _columnStatesDirty = false;
    }

    private void BuildAutoGeneratedColumns()
    {
        var sample = ResolveSampleItem();
        if (sample != null)
        {
            _state.AutoGeneratedItemType = sample.GetType();
            var properties = sample.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            for (var i = 0; i < properties.Length; i++)
            {
                var generatedColumn = new DataGridColumn { Header = properties[i].Name, BindingPath = properties[i].Name, Width = float.NaN, DisplayIndex = i };
                _state.Columns.Add(new DataGridColumnState { Column = generatedColumn, DisplayIndex = i, Width = generatedColumn.GetResolvedWidth(), SortDirection = DataGridSortDirection.None });
            }
        }

        if (_state.Columns.Count == 0)
        {
            var fallbackColumn = new DataGridColumn { Header = "Value", BindingPath = string.Empty, Width = float.NaN, DisplayIndex = 0 };
            _state.Columns.Add(new DataGridColumnState { Column = fallbackColumn, DisplayIndex = 0, Width = fallbackColumn.GetResolvedWidth(), SortDirection = DataGridSortDirection.None });
        }
    }

    private void NormalizeDisplayIndexes()
    {
        var used = new HashSet<int>();
        foreach (var state in _state.Columns.OrderBy(static item => item.DisplayIndex).ThenBy(item => _state.Columns.IndexOf(item)))
        {
            var candidate = state.DisplayIndex;
            if (candidate < 0 || !used.Add(candidate))
            {
                candidate = 0;
                while (!used.Add(candidate)) { candidate++; }
            }

            state.DisplayIndex = candidate;
            state.Column.DisplayIndex = candidate;
            state.Width = state.Column.GetResolvedWidth();
        }

        _displayColumnsCache = null;
    }

    private void RefreshRowStates()
    {
        _state.Rows.Clear();
        var itemContainers = GetItemContainersForPresenter();
        for (var i = 0; i < itemContainers.Count; i++) { _state.Rows.Add(new DataGridRowState { RowIndex = i, Item = ItemFromContainer(itemContainers[i]) }); }
    }

    private void RefreshRowDetailsState()
    {
        for (var i = 0; i < _state.Rows.Count; i++)
        {
            _state.Rows[i].AreDetailsVisible = RowDetailsVisibilityMode switch
            {
                DataGridRowDetailsVisibilityMode.Visible => true,
                DataGridRowDetailsVisibilityMode.VisibleWhenSelected => _state.Selection.SelectedRowIndex == i,
                _ => false
            };
        }
    }

    private void SyncGridChrome(bool invalidateMeasure = true)
    {
        _cornerHeader.Font = Font;
        _headersPresenter.SyncHeaders(this, GetDisplayColumns(), OnColumnHeaderClick);
        SyncRowsHost(invalidateMeasure: invalidateMeasure);
        if (invalidateMeasure)
        {
            InvalidateMeasure();
        }
        InvalidateArrange();
        InvalidateVisual();
    }


    private void RequestDeferredSyncGridChrome(bool invalidateMeasure = true)
    {
        _pendingDeferredChromeInvalidateMeasure |= invalidateMeasure;
        if (_syncGridChromeDeferredQueued)
        {
            return;
        }

        _syncGridChromeDeferredQueued = true;
        Dispatcher.EnqueueDeferred(() =>
        {
            _syncGridChromeDeferredQueued = false;
            var applyInvalidateMeasure = _pendingDeferredChromeInvalidateMeasure;
            _pendingDeferredChromeInvalidateMeasure = false;
            SyncGridChrome(applyInvalidateMeasure);
        });
    }

    private void SyncRowsHost(int startIndex = 0, bool invalidateMeasure = true) => _rowsPresenter.SyncRows(this, _state.Rows, GetDisplayColumns(), startIndex, invalidateMeasure);

    private IReadOnlyList<DataGridColumnState> GetDisplayColumns()
    {
        EnsureColumnStatesCurrent();
        return _displayColumnsCache ??= _state.Columns
            .OrderBy(static item => item.DisplayIndex)
            .ToArray();
    }

    private void UpdateSelectionVisuals()
    {
        SyncSelectionStateFromProperties();
        var rows = RowsForTesting;
        for (var i = 0; i < rows.Count; i++) { rows[i].UpdateSelectionState(SelectionUnit, _state.Selection.SelectedRowIndex, _state.Selection.SelectedColumnIndex, _state.Selection.CurrentRowIndex, _state.Selection.CurrentColumnIndex, i < _state.Rows.Count && _state.Rows[i].AreDetailsVisible); }
    }

    private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        _valueReaderCache.Clear();
        _columnStatesDirty = true;
        RefreshGridState(refreshColumns: true);
    }

    private void OnColumnHeaderClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = e;
        if (sender is DataGridColumnHeader header) { SortByDisplayColumnIndex(header.ColumnIndex); }
    }

    private void SortByDisplayColumnIndex(int displayIndex)
    {
        if (!CanUserSortColumns) { return; }
        var displayColumns = GetDisplayColumns();
        if (displayIndex < 0 || displayIndex >= displayColumns.Count) { return; }
        var columnState = displayColumns[displayIndex];
        if (!columnState.Column.CanUserSort) { return; }
        var nextSortDirection = columnState.SortDirection switch
        {
            DataGridSortDirection.None => DataGridSortDirection.Ascending,
            DataGridSortDirection.Ascending => DataGridSortDirection.Descending,
            _ => DataGridSortDirection.None
        };
        ApplySortToItemsSourceView(columnState, nextSortDirection);
        SyncColumnStateSortDirectionsFromView();
        SyncHeaderSortDirections();
        Sorting?.Invoke(this, new DataGridSortingEventArgs(columnState.Column, displayIndex));
    }

    private void ApplySortToItemsSourceView(DataGridColumnState columnState, DataGridSortDirection nextSortDirection)
    {
        var view = ItemsSourceView;
        if (view == null) { return; }
        if (view is CollectionView collectionView)
        {
            using (collectionView.DeferRefresh())
            {
                for (var i = view.SortDescriptions.Count - 1; i >= 0; i--)
                {
                    view.SortDescriptions.RemoveAt(i);
                }

                var deferredBindingPath = columnState.Column.BindingPath ?? string.Empty;
                if (nextSortDirection == DataGridSortDirection.Ascending)
                {
                    view.SortDescriptions.Add(new SortDescription(deferredBindingPath, ListSortDirection.Ascending));
                }
                else if (nextSortDirection == DataGridSortDirection.Descending)
                {
                    view.SortDescriptions.Add(new SortDescription(deferredBindingPath, ListSortDirection.Descending));
                }
            }
        }
        else
        {
            for (var i = view.SortDescriptions.Count - 1; i >= 0; i--)
            {
                view.SortDescriptions.RemoveAt(i);
            }

            var fallbackBindingPath = columnState.Column.BindingPath ?? string.Empty;
            if (nextSortDirection == DataGridSortDirection.Ascending)
            {
                view.SortDescriptions.Add(new SortDescription(fallbackBindingPath, ListSortDirection.Ascending));
            }
            else if (nextSortDirection == DataGridSortDirection.Descending)
            {
                view.SortDescriptions.Add(new SortDescription(fallbackBindingPath, ListSortDirection.Descending));
            }

            view.Refresh();
        }
    }

    private void SyncColumnStateSortDirectionsFromView()
    {
        var view = ItemsSourceView;
        for (var i = 0; i < _state.Columns.Count; i++) { _state.Columns[i].SortDirection = ResolveColumnSortDirection(view, _state.Columns[i].Column.BindingPath); _state.Columns[i].Column.SortDirection = _state.Columns[i].SortDirection; }
    }

    private static DataGridSortDirection ResolveColumnSortDirection(ICollectionView? view, string? bindingPath)
    {
        if (view == null) { return DataGridSortDirection.None; }
        var normalizedBindingPath = bindingPath ?? string.Empty;
        for (var i = view.SortDescriptions.Count - 1; i >= 0; i--)
        {
            var description = view.SortDescriptions[i];
            if (!string.Equals(description.PropertyName ?? string.Empty, normalizedBindingPath, StringComparison.OrdinalIgnoreCase)) { continue; }
            return description.Direction == ListSortDirection.Descending ? DataGridSortDirection.Descending : DataGridSortDirection.Ascending;
        }

        return DataGridSortDirection.None;
    }

    private void SyncHeaderSortDirections()
    {
        var displayColumns = GetDisplayColumns();
        var count = Math.Min(_headersPresenter.Headers.Count, displayColumns.Count);
        for (var i = 0; i < count; i++) { _headersPresenter.Headers[i].SortDirection = displayColumns[i].SortDirection; }
    }

    private void OnScrollViewerDependencyPropertyChanged(object? sender, DependencyPropertyChangedEventArgs args)
    {
        _ = sender;
        if (args.Property == ScrollViewer.HorizontalOffsetProperty ||
            args.Property == ScrollViewer.ViewportWidthProperty ||
            args.Property == ScrollViewer.ViewportHeightProperty)
        {
            InvalidateArrange();
        }
    }

    private float GetEffectiveRowHeight() => MathF.Max(RowHeight, FontStashTextRenderer.GetLineHeight(Font, FontSize) + 8f);
    private float GetEffectiveColumnHeaderHeight() => MathF.Max(ColumnHeaderHeight, FontStashTextRenderer.GetLineHeight(Font, FontSize) + 10f);

    private float GetColumnsTotalWidth()
    {
        var width = 0f;
        var displayColumns = GetDisplayColumns();
        for (var i = 0; i < displayColumns.Count; i++) { width += displayColumns[i].Width; }
        return width;
    }

    private object? ResolveSampleItem()
    {
        var itemContainers = GetItemContainersForPresenter();
        if (itemContainers.Count > 0) { return ItemFromContainer(itemContainers[0]); }
        if (ItemsSourceView != null) { foreach (var item in ItemsSourceView) { if (item != null) { return item; } } }
        return Items.Count > 0 ? Items[0] : null;
    }

    private void SyncSelectionStateFromProperties()
    {
        if (_isSynchronizingSelectionProperties) { return; }
        var rowCount = _state.Rows.Count;
        var columnCount = _state.Columns.Count;
        var selectedRowIndex = SelectedRowIndex >= 0 && SelectedRowIndex < rowCount ? SelectedRowIndex : -1;
        var selectedColumnIndex = SelectionUnit == DataGridSelectionUnit.Cell && SelectedColumnIndex >= 0 && SelectedColumnIndex < columnCount ? SelectedColumnIndex : -1;
        _state.Selection.SelectedRowIndex = selectedRowIndex;
        _state.Selection.SelectedColumnIndex = selectedColumnIndex;
        if (_state.Selection.CurrentRowIndex < 0 || _state.Selection.CurrentRowIndex >= rowCount) { _state.Selection.CurrentRowIndex = selectedRowIndex >= 0 ? selectedRowIndex : (rowCount > 0 ? 0 : -1); }
        if (_state.Selection.CurrentColumnIndex < 0 || _state.Selection.CurrentColumnIndex >= columnCount) { _state.Selection.CurrentColumnIndex = selectedColumnIndex >= 0 ? selectedColumnIndex : (columnCount > 0 ? 0 : -1); }
        RefreshRowDetailsState();
        if (SelectedRowIndex != selectedRowIndex || SelectedColumnIndex != selectedColumnIndex) { SyncSelectionPropertiesFromState(); }
    }

    private void SyncSelectionPropertiesFromState()
    {
        _isSynchronizingSelectionProperties = true;
        try
        {
            if (SelectedRowIndex != _state.Selection.SelectedRowIndex) { SelectedRowIndex = _state.Selection.SelectedRowIndex; }
            var selectedColumnIndex = SelectionUnit == DataGridSelectionUnit.Cell ? _state.Selection.SelectedColumnIndex : -1;
            if (SelectedColumnIndex != selectedColumnIndex) { SelectedColumnIndex = selectedColumnIndex; }
        }
        finally
        {
            _isSynchronizingSelectionProperties = false;
        }
    }

    private void SyncObservedItems()
    {
        var nextCounts = new Dictionary<INotifyPropertyChanged, int>();
        for (var i = 0; i < _state.Rows.Count; i++)
        {
            if (_state.Rows[i].Item is not INotifyPropertyChanged observed) { continue; }
            nextCounts.TryGetValue(observed, out var count);
            nextCounts[observed] = count + 1;
        }

        foreach (var entry in _observedItems) { if (!nextCounts.ContainsKey(entry.Key)) { entry.Key.PropertyChanged -= OnObservedItemPropertyChanged; } }
        foreach (var entry in nextCounts) { if (!_observedItems.ContainsKey(entry.Key)) { entry.Key.PropertyChanged += OnObservedItemPropertyChanged; } }
        _observedItems.Clear();
        foreach (var entry in nextCounts) { _observedItems[entry.Key] = entry.Value; }
    }

    private void OnObservedItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = e;
        if (sender == null) { return; }
        var rows = RowsForTesting;
        for (var i = 0; i < rows.Count; i++) { if (ReferenceEquals(rows[i].Item, sender)) { rows[i].RefreshCellContents(); } }
    }

    private bool TryGetCell(int rowIndex, int columnIndex, out DataGridRow row, out DataGridCell cell, out DataGridColumnState columnState)
    {
        row = null!;
        cell = null!;
        columnState = null!;
        var rows = RowsForTesting;
        var displayColumns = GetDisplayColumns();
        if (rowIndex < 0 || rowIndex >= rows.Count || columnIndex < 0 || columnIndex >= displayColumns.Count) { return false; }
        row = rows[rowIndex];
        if (columnIndex >= row.Cells.Count) { return false; }
        cell = row.Cells[columnIndex];
        columnState = displayColumns[columnIndex];
        return true;
    }

    private bool TryGetRealizedRowViewportDelta(int rowIndex, out float delta)
    {
        delta = 0f;
        var rowsHost = _rowsPresenter.RowsHost;
        if (rowIndex < rowsHost.FirstRealizedIndex || rowIndex > rowsHost.LastRealizedIndex)
        {
            return false;
        }

        var itemContainers = GetItemContainersForPresenter();
        if (rowIndex < 0 || rowIndex >= itemContainers.Count || itemContainers[rowIndex] is not DataGridRow row)
        {
            return false;
        }

        if (!_rowsPresenter.ScrollViewer.TryGetContentViewportClipRect(out var viewportRect))
        {
            return false;
        }

        var rowTop = row.LayoutSlot.Y;
        var rowBottom = row.LayoutSlot.Y + row.LayoutSlot.Height;
        var viewportTop = viewportRect.Y + _rowsPresenter.ScrollViewer.VerticalOffset;
        var viewportBottom = viewportTop + viewportRect.Height;
        if (rowTop < viewportTop)
        {
            delta = rowTop - viewportTop;
            return true;
        }

        if (rowBottom > viewportBottom)
        {
            delta = rowBottom - viewportBottom;
            return true;
        }

        return true;
    }

    private bool TryResolveGridTarget(UIElement target, out DataGridRow? row, out DataGridCell? cell, out DataGridColumnHeader? header)
    {
        row = null; cell = null; header = null;
        for (var current = target; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is DataGridColumnHeader currentHeader && ReferenceEquals(currentHeader.Owner, this)) { header = currentHeader; return true; }
            if (current is DataGridCell currentCell) { cell = currentCell; }
            if (current is DataGridRow currentRow && ReferenceEquals(currentRow.Owner, this)) { row = currentRow; return true; }
            if (ReferenceEquals(current, this)) { return row != null || cell != null || header != null; }
        }

        return false;
    }

    private bool IsWithinActiveEditor(UIElement target)
    {
        var editor = _state.Edit.ActiveEditorElement;
        if (editor == null)
        {
            return false;
        }

        for (var current = target; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (ReferenceEquals(current, editor))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryStartHeaderResize(int displayIndex, Vector2 pointerPosition)
    {
        if (!CanUserResizeColumns || displayIndex < 0 || displayIndex >= _headersPresenter.Headers.Count) { return false; }
        var column = GetDisplayColumns()[displayIndex].Column;
        if (!column.CanUserResize) { return false; }
        var slot = _headersPresenter.Headers[displayIndex].LayoutSlot;
        if (pointerPosition.X < slot.X + slot.Width - HeaderResizeGripWidth || pointerPosition.X > slot.X + slot.Width + 1f) { return false; }
        _isResizingColumn = true; _resizeColumnDisplayIndex = displayIndex; _resizeStartPointerX = pointerPosition.X; _resizeStartWidth = GetDisplayColumns()[displayIndex].Width; ResetHeaderDragState(); return true;
    }

    private bool CanDisplayColumnReorder(int displayIndex)
    {
        var displayColumns = GetDisplayColumns();
        return displayIndex >= 0 && displayIndex < displayColumns.Count && displayColumns[displayIndex].Column.CanUserReorder;
    }

    private void SetDisplayColumnWidth(int displayIndex, float width)
    {
        var displayColumns = GetDisplayColumns();
        if (displayIndex < 0 || displayIndex >= displayColumns.Count) { return; }
        var state = displayColumns[displayIndex];
        var resolvedWidth = MathF.Max(state.Column.MinWidth, width);
        state.Width = resolvedWidth;
        state.Column.Width = resolvedWidth;
        SyncGridChrome();
    }

    private int ResolveHeaderDisplayIndexAtPointer(Vector2 pointerPosition)
    {
        for (var i = 0; i < _headersPresenter.Headers.Count; i++) { if (_headersPresenter.Headers[i].HitTest(pointerPosition)) { return i; } }
        return -1;
    }

    private void MoveDisplayColumn(int sourceDisplayIndex, int targetDisplayIndex)
    {
        var displayColumns = GetDisplayColumns().ToList();
        if (sourceDisplayIndex < 0 || sourceDisplayIndex >= displayColumns.Count || targetDisplayIndex < 0 || targetDisplayIndex >= displayColumns.Count) { return; }
        var moved = displayColumns[sourceDisplayIndex];
        displayColumns.RemoveAt(sourceDisplayIndex);
        displayColumns.Insert(targetDisplayIndex, moved);
        for (var i = 0; i < displayColumns.Count; i++) { displayColumns[i].DisplayIndex = i; displayColumns[i].Column.DisplayIndex = i; }
        _displayColumnsCache = null;
        _headersPresenter.SyncHeaders(this, GetDisplayColumns(), OnColumnHeaderClick);
        SyncRowsHost();
        InvalidateArrange();
        InvalidateVisual();
    }

    private void EnsureColumnStatesCurrent()
    {
        if (_columnStatesDirty)
        {
            RefreshColumnStates();
        }
    }

    private void ResetHeaderPointerState()
    {
        _isResizingColumn = false;
        _resizeColumnDisplayIndex = -1;
        ResetHeaderDragState();
    }

    private void ResetHeaderDragState()
    {
        _isPotentialReorder = false;
        _isDraggingColumn = false;
        _dragColumnDisplayIndex = -1;
        _dragStartPointerX = 0f;
    }

    private bool IsEditableDisplayColumn(int displayColumnIndex)
    {
        var displayColumns = GetDisplayColumns();
        return displayColumnIndex >= 0 &&
               displayColumnIndex < displayColumns.Count &&
               !displayColumns[displayColumnIndex].Column.IsReadOnly;
    }

    private bool IsRepeatedPointerPress(int rowIndex, int columnIndex)
    {
        if (_lastPointerPressedRowIndex != rowIndex || _lastPointerPressedColumnIndex != columnIndex)
        {
            return false;
        }

        var elapsed = Stopwatch.GetElapsedTime(_lastPointerPressedTimestamp).TotalMilliseconds;
        return elapsed <= PointerEditRepeatWindowMs;
    }

    private void RecordPointerPress(int rowIndex, int columnIndex)
    {
        _lastPointerPressedRowIndex = rowIndex;
        _lastPointerPressedColumnIndex = columnIndex;
        _lastPointerPressedTimestamp = Stopwatch.GetTimestamp();
    }

    private void ResetPointerEditTracking()
    {
        _lastPointerPressedRowIndex = -1;
        _lastPointerPressedColumnIndex = -1;
        _lastPointerPressedTimestamp = 0L;
    }

    private static Func<object?, object?> BuildValueReader(string path, Type runtimeType)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) { return static instance => instance; }
        var instance = Expression.Parameter(typeof(object), "instance");
        Expression current = Expression.Convert(instance, runtimeType);
        var currentType = runtimeType;
        for (var i = 0; i < segments.Length; i++)
        {
            var property = currentType.GetProperty(segments[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property == null) { return static _ => null; }
            var propertyAccess = Expression.Property(current, property);
            current = !currentType.IsValueType ? Expression.Condition(Expression.ReferenceEqual(current, Expression.Constant(null, currentType)), Expression.Default(property.PropertyType), propertyAccess) : propertyAccess;
            currentType = property.PropertyType;
        }

        return Expression.Lambda<Func<object?, object?>>(Expression.Convert(current, typeof(object)), instance).Compile();
    }

    private static bool TryStageEditedValue(object? item, string path, string text, out DataGridEditRollbackSnapshot rollbackSnapshot, out string? validationMessage)
    {
        rollbackSnapshot = DataGridEditRollbackSnapshot.Empty;
        validationMessage = null;
        if (item == null || string.IsNullOrWhiteSpace(path))
        {
            validationMessage = "The edited cell does not map to a writable binding path.";
            return false;
        }

        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        object? current = item;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (current == null)
            {
                validationMessage = "The edited cell binding path resolved to null before the target property.";
                return false;
            }

            var navigation = current.GetType().GetProperty(segments[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (navigation == null)
            {
                validationMessage = $"Property '{segments[i]}' was not found while resolving the edited cell path.";
                return false;
            }

            current = navigation.GetValue(current);
        }

        if (current == null)
        {
            validationMessage = "The edited cell binding path resolved to a null target.";
            return false;
        }

        var leaf = current.GetType().GetProperty(segments[^1], BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (leaf == null || !leaf.CanWrite)
        {
            validationMessage = $"Property '{segments[^1]}' is not writable.";
            return false;
        }

        if (!TryConvertEditorText(text, leaf.PropertyType, out var converted))
        {
            validationMessage = $"'{text}' is not a valid value for {leaf.PropertyType.Name}.";
            return false;
        }

        rollbackSnapshot = new DataGridEditRollbackSnapshot(current, leaf, leaf.GetValue(current));
        leaf.SetValue(current, converted);
        return true;
    }

    private static bool TryConvertEditorText(string text, Type propertyType, out object? converted)
    {
        var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        try
        {
            if (targetType == typeof(string))
            {
                converted = text;
                return true;
            }

            if (string.IsNullOrEmpty(text) && Nullable.GetUnderlyingType(propertyType) != null)
            {
                converted = null;
                return true;
            }

            if (targetType.IsEnum)
            {
                converted = Enum.Parse(targetType, text, ignoreCase: true);
                return true;
            }

            converted = Convert.ChangeType(text, targetType);
            return true;
        }
        catch
        {
            converted = null;
            return false;
        }
    }

    private readonly record struct DataGridEditRollbackSnapshot(object Target, PropertyInfo Property, object? OriginalValue)
    {
        public static DataGridEditRollbackSnapshot Empty => default;

        public void Restore()
        {
            if (Target == null || Property == null)
            {
                return;
            }

            Property.SetValue(Target, OriginalValue);
        }
    }
}
