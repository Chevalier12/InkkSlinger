using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class GridView : UserControl
{
    private const float StackedLayoutBreakpoint = 760f;
    private const float InfoRailWidth = 320f;
    private const float StackedInfoRailHeight = 220f;
    private const float StackedWorkbenchMinHeight = 300f;
    private const float DefaultWorkbenchMinHeight = 360f;
    private static readonly Thickness DefaultWorkbenchMargin = new(0f, 0f, 0f, 12f);
    private static readonly Thickness StackedWorkbenchMargin = new(0f, 0f, 0f, 22f);

    private Grid? _contentGrid;
    private Border? _bodyBorder;
    private ScrollViewer? _infoScrollViewer;
    private Grid? _workbenchGrid;
    private Border? _canvasCard;
    private Border? _inspectorCard;
    private CheckBox? _showGridLinesCheckBox;
    private CheckBox? _wideCanvasCheckBox;
    private Grid? _sharedPrimaryGrid;
    private Grid? _sharedSecondaryGrid;
    private TextBlock? _layoutModeValueText;
    private TextBlock? _gridLinesValueText;
    private TextBlock? _columnMetricsText;
    private TextBlock? _rowMetricsText;
    private TextBlock? _sharedMetricsText;
    private bool _isStackedLayout;

    public GridView()
    {
        InitializeComponent();
        EnsureReferences();

        if (_showGridLinesCheckBox != null)
        {
            _showGridLinesCheckBox.Checked += HandleWorkbenchToggleChanged;
            _showGridLinesCheckBox.Unchecked += HandleWorkbenchToggleChanged;
        }

        if (_wideCanvasCheckBox != null)
        {
            _wideCanvasCheckBox.Checked += HandleWorkbenchToggleChanged;
            _wideCanvasCheckBox.Unchecked += HandleWorkbenchToggleChanged;
        }

        ApplyWorkbenchState();
        UpdateTelemetry();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        UpdateResponsiveLayout(availableSize.X);
        return base.MeasureOverride(availableSize);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        UpdateResponsiveLayout(finalSize.X);
        var arranged = base.ArrangeOverride(finalSize);
        UpdateTelemetry();
        return arranged;
    }

    private void HandleWorkbenchToggleChanged(object? sender, RoutedSimpleEventArgs args)
    {
        ApplyWorkbenchState();
    }

    private void EnsureReferences()
    {
        _contentGrid ??= this.FindName("GridViewContentGrid") as Grid;
        _bodyBorder ??= this.FindName("GridViewBodyBorder") as Border;
        _infoScrollViewer ??= this.FindName("GridViewInfoScrollViewer") as ScrollViewer;
        _workbenchGrid ??= this.FindName("LayoutWorkbenchGrid") as Grid;
        _canvasCard ??= this.FindName("CanvasCard") as Border;
        _inspectorCard ??= this.FindName("InspectorCard") as Border;
        _showGridLinesCheckBox ??= this.FindName("ShowGridLinesCheckBox") as CheckBox;
        _wideCanvasCheckBox ??= this.FindName("WideCanvasCheckBox") as CheckBox;
        _sharedPrimaryGrid ??= this.FindName("SharedSizePrimaryGrid") as Grid;
        _sharedSecondaryGrid ??= this.FindName("SharedSizeSecondaryGrid") as Grid;
        _layoutModeValueText ??= this.FindName("LayoutModeValueText") as TextBlock;
        _gridLinesValueText ??= this.FindName("GridLinesValueText") as TextBlock;
        _columnMetricsText ??= this.FindName("ColumnMetricsText") as TextBlock;
        _rowMetricsText ??= this.FindName("RowMetricsText") as TextBlock;
        _sharedMetricsText ??= this.FindName("SharedMetricsText") as TextBlock;
    }

    private void ApplyWorkbenchState()
    {
        EnsureReferences();

        if (_workbenchGrid == null || _canvasCard == null || _inspectorCard == null)
        {
            return;
        }

        _workbenchGrid.ShowGridLines = _showGridLinesCheckBox?.IsChecked == true;

        var wideCanvas = _wideCanvasCheckBox?.IsChecked == true;
        if (wideCanvas)
        {
            Grid.SetColumn(_canvasCard, 1);
            Grid.SetColumnSpan(_canvasCard, 2);

            Grid.SetColumn(_inspectorCard, 3);
            Grid.SetColumnSpan(_inspectorCard, 1);
        }
        else
        {
            Grid.SetColumn(_canvasCard, 1);
            Grid.SetColumnSpan(_canvasCard, 1);

            Grid.SetColumn(_inspectorCard, 2);
            Grid.SetColumnSpan(_inspectorCard, 2);
        }

        _workbenchGrid.InvalidateMeasure();
        _workbenchGrid.InvalidateArrange();
        UpdateTelemetry();
    }

    private void UpdateResponsiveLayout(float availableWidth)
    {
        EnsureReferences();

        if (_contentGrid == null ||
            _bodyBorder == null ||
            _infoScrollViewer == null ||
            _workbenchGrid == null ||
            _contentGrid.ColumnDefinitions.Count < 2 ||
            _contentGrid.RowDefinitions.Count < 2)
        {
            return;
        }

        if (availableWidth <= 0f)
        {
            return;
        }

        var shouldStack = availableWidth < StackedLayoutBreakpoint;
        if (_isStackedLayout == shouldStack)
        {
            return;
        }

        _isStackedLayout = shouldStack;

        if (shouldStack)
        {
            _contentGrid.ColumnDefinitions[1].Width = new GridLength(0f, GridUnitType.Pixel);
            _contentGrid.RowDefinitions[1].Height = GridLength.Auto;

            _bodyBorder.Margin = new Thickness(0f, 0f, 0f, 10f);
            Grid.SetRow(_bodyBorder, 0);
            Grid.SetColumn(_bodyBorder, 0);

            Grid.SetRow(_infoScrollViewer, 1);
            Grid.SetColumn(_infoScrollViewer, 0);
            _infoScrollViewer.Height = StackedInfoRailHeight;
            _workbenchGrid.MinHeight = StackedWorkbenchMinHeight;
            _workbenchGrid.Margin = StackedWorkbenchMargin;
        }
        else
        {
            _contentGrid.ColumnDefinitions[1].Width = new GridLength(InfoRailWidth, GridUnitType.Pixel);
            _contentGrid.RowDefinitions[1].Height = new GridLength(0f, GridUnitType.Pixel);

            _bodyBorder.Margin = new Thickness(0f, 0f, 10f, 0f);
            Grid.SetRow(_bodyBorder, 0);
            Grid.SetColumn(_bodyBorder, 0);

            Grid.SetRow(_infoScrollViewer, 0);
            Grid.SetColumn(_infoScrollViewer, 1);
            _infoScrollViewer.Height = float.NaN;
            _workbenchGrid.MinHeight = DefaultWorkbenchMinHeight;
            _workbenchGrid.Margin = DefaultWorkbenchMargin;
        }

        _contentGrid.InvalidateMeasure();
        _contentGrid.InvalidateArrange();
        _bodyBorder.InvalidateMeasure();
        _infoScrollViewer.InvalidateMeasure();
    }

    private void UpdateTelemetry()
    {
        EnsureReferences();

        if (_workbenchGrid == null)
        {
            return;
        }

        SetText(
            _layoutModeValueText,
            $"Layout mode: {(_isStackedLayout ? "Stacked info rail" : "Wide rail")}. Toggle state keeps the same Grid tree but repositions the catalog chrome around it.");
        SetText(
            _gridLinesValueText,
            $"Grid lines: {(_workbenchGrid.ShowGridLines ? "On" : "Off")}. Canvas emphasis: {(_wideCanvasCheckBox?.IsChecked == true ? "Nested canvas spans two columns" : "Inspector spans two columns")}." );
        SetText(_columnMetricsText, $"Columns: {FormatColumnMetrics(_workbenchGrid)}");
        SetText(_rowMetricsText, $"Rows: {FormatRowMetrics(_workbenchGrid)}");
        SetText(_sharedMetricsText, FormatSharedMetrics());
    }

    private static string FormatColumnMetrics(Grid grid)
    {
        var parts = new string[grid.ColumnDefinitions.Count];
        for (var i = 0; i < grid.ColumnDefinitions.Count; i++)
        {
            parts[i] = $"C{i}={grid.ColumnDefinitions[i].ActualWidth:0}";
        }

        return string.Join(" | ", parts);
    }

    private static string FormatRowMetrics(Grid grid)
    {
        var parts = new string[grid.RowDefinitions.Count];
        for (var i = 0; i < grid.RowDefinitions.Count; i++)
        {
            parts[i] = $"R{i}={grid.RowDefinitions[i].ActualHeight:0}";
        }

        return string.Join(" | ", parts);
    }

    private string FormatSharedMetrics()
    {
        if (_sharedPrimaryGrid == null || _sharedSecondaryGrid == null ||
            _sharedPrimaryGrid.ColumnDefinitions.Count < 3 ||
            _sharedSecondaryGrid.ColumnDefinitions.Count < 3)
        {
            return "Shared groups: unavailable";
        }

        return $"Shared groups: Label={_sharedPrimaryGrid.ColumnDefinitions[0].ActualWidth:0}/{_sharedSecondaryGrid.ColumnDefinitions[0].ActualWidth:0}, Meta={_sharedPrimaryGrid.ColumnDefinitions[2].ActualWidth:0}/{_sharedSecondaryGrid.ColumnDefinitions[2].ActualWidth:0}.";
    }

    private static void SetText(TextBlock? target, string value)
    {
        if (target == null || string.Equals(target.Text, value, StringComparison.Ordinal))
        {
            return;
        }

        target.Text = value;
    }
}




