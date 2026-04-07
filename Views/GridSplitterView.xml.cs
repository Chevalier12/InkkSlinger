using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class GridSplitterView : UserControl
{
    private static bool EnableLiveGridSplitterHitchDiagnostics => Debugger.IsAttached;
    private static readonly string LiveGridSplitterHitchDiagnosticsArtifactPath = Path.Combine(AppContext.BaseDirectory, "artifacts", "diagnostics", "gridsplitter-live-hitch-latest.txt");
    private const float StackedLayoutBreakpoint = 880f;
    private const float InfoRailWidth = 340f;
    private const float StackedInfoRailHeight = 280f;
    private const float DefaultWorkbenchMinHeight = 320f;
    private const float StackedWorkbenchMinHeight = 280f;
    private static readonly Thickness DefaultWorkbenchMargin = new(0f, 0f, 0f, 12f);
    private static readonly Thickness StackedWorkbenchMargin = new(0f, 0f, 0f, 18f);

    private GridSplitterPreset _currentPreset = GridSplitterPreset.Balanced;
    private bool _isStackedLayout;

    private Grid? _contentGrid;
    private Border? _bodyBorder;
    private Grid? _rootGrid;
    private ScrollViewer? _infoScrollViewer;
    private ScrollViewer? _workbenchScrollViewer;
    private Grid? _primaryEditorGrid;
    private Grid? _horizontalWorkbenchGrid;
    private Grid? _alignmentLeftGrid;
    private Grid? _alignmentCenterGrid;
    private Grid? _alignmentRightGrid;
    private Grid? _explicitPreviousCurrentGrid;
    private Grid? _explicitPreviousNextGrid;
    private Grid? _explicitCurrentNextGrid;
    private Grid? _autoColumnGrid;
    private Grid? _autoRowGrid;
    private CheckBox? _showGridLinesCheckBox;
    private CheckBox? _wideCenterCheckBox;
    private CheckBox? _precisionIncrementsCheckBox;
    private GridSplitter? _navigationSplitter;
    private GridSplitter? _inspectorSplitter;
    private GridSplitter? _timelineSplitter;
    private GridSplitter? _alignmentLeftSplitter;
    private GridSplitter? _alignmentCenterSplitter;
    private GridSplitter? _alignmentRightSplitter;
    private GridSplitter? _explicitPreviousCurrentSplitter;
    private GridSplitter? _explicitPreviousNextSplitter;
    private GridSplitter? _explicitCurrentNextSplitter;
    private GridSplitter? _autoColumnSplitter;
    private GridSplitter? _autoRowSplitter;
    private TextBlock? _presetNarrativeText;
    private TextBlock? _horizontalSummaryText;
    private TextBlock? _behaviorSummaryText;
    private TextBlock? _autoDirectionSummaryText;
    private TextBlock? _primaryPairSummaryText;
    private TextBlock? _incrementSummaryText;
    private TextBlock? _interactionSummaryText;
    private TextBlock? _layoutModeValueText;
    private TextBlock? _primaryMetricsText;
    private TextBlock? _secondaryMetricsText;
    private TextBlock? _behaviorMetricsText;
    private TextBlock? _autoDirectionMetricsText;
    private Grid? _primaryCanvasRootGrid;
    private Grid? _primaryCanvasHeaderGrid;
    private Border? _primaryCanvasHintBorder;
    private Grid? _primaryCanvasHintGrid;
    private TextBlock? _primaryCanvasHintText;
    private Border? _primaryCanvasHintHotkeyBorder;
    private TextBlock? _primaryCanvasHintHotkeyText;
    private Border? _primaryCanvasHintMinWidthBorder;
    private TextBlock? _primaryCanvasHintMinWidthText;
    private Grid? _primaryCanvasLowerGrid;
    private Border? _primaryCanvasLowerLeftPanel;
    private Border? _primaryCanvasLowerRightPanel;
    private bool _telemetryUpdateQueued;
    private long _liveDiagSequence;
    private bool _dragDiagActive;
    private float _dragDiagLastCenterWidth = float.NaN;
    private double _dragDiagPeakLayoutMs;
    private double _dragDiagPeakMeasureWorkMs;
    private double _dragDiagPeakArrangeWorkMs;
    private double _dragDiagPeakDrawTreeMs;
    private long _dragDiagPeakFrameworkMeasureWork;
    private long _dragDiagPeakFrameworkArrangeWork;
    private readonly Dictionary<FrameworkElement, DragLayoutElementState> _dragDiagPreviousElementStates = new();

    public GridSplitterView()
    {
        InitializeComponent();
        EnsureReferences();
        WireEvents();
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
        EmitLiveGridSplitterDiagnostics();
        return arranged;
    }

    private void WireEvents()
    {
        WireToggle(_showGridLinesCheckBox);
        WireToggle(_wideCenterCheckBox);
        WireToggle(_precisionIncrementsCheckBox);

        AttachButton("BalancedPresetButton", (_, _) => ApplyPreset(GridSplitterPreset.Balanced));
        AttachButton("CanvasPresetButton", (_, _) => ApplyPreset(GridSplitterPreset.CanvasFocus));
        AttachButton("InspectorPresetButton", (_, _) => ApplyPreset(GridSplitterPreset.InspectorFocus));
        AttachButton("KeyboardPresetButton", (_, _) => ApplyPreset(GridSplitterPreset.KeyboardLab));
        AttachButton("ResetPrimaryLayoutButton", (_, _) => ApplyWorkbenchState());
    }

    private void WireToggle(CheckBox? checkBox)
    {
        if (checkBox == null)
        {
            return;
        }

        checkBox.Checked += HandleWorkbenchOptionChanged;
        checkBox.Unchecked += HandleWorkbenchOptionChanged;
    }

    private void AttachButton(string name, EventHandler<RoutedSimpleEventArgs> handler)
    {
        if (this.FindName(name) is Button button)
        {
            button.Click += handler;
        }
    }

    private void HandleWorkbenchOptionChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        ApplyWorkbenchState();
    }

    private void ApplyPreset(GridSplitterPreset preset)
    {
        _currentPreset = preset;
        ApplyWorkbenchState();
    }

    private void EnsureReferences()
    {
        _contentGrid ??= this.FindName("GridSplitterViewContentGrid") as Grid;
        _bodyBorder ??= this.FindName("GridSplitterViewBodyBorder") as Border;
        _rootGrid ??= this.FindName("GridSplitterViewRootGrid") as Grid;
        _infoScrollViewer ??= this.FindName("GridSplitterViewInfoScrollViewer") as ScrollViewer;
        _workbenchScrollViewer ??= this.FindName("GridSplitterWorkbenchScrollViewer") as ScrollViewer;
        _primaryEditorGrid ??= this.FindName("PrimaryEditorGrid") as Grid;
        _horizontalWorkbenchGrid ??= this.FindName("HorizontalWorkbenchGrid") as Grid;
        _alignmentLeftGrid ??= this.FindName("AlignmentLeftGrid") as Grid;
        _alignmentCenterGrid ??= this.FindName("AlignmentCenterGrid") as Grid;
        _alignmentRightGrid ??= this.FindName("AlignmentRightGrid") as Grid;
        _explicitPreviousCurrentGrid ??= this.FindName("ExplicitPreviousCurrentGrid") as Grid;
        _explicitPreviousNextGrid ??= this.FindName("ExplicitPreviousNextGrid") as Grid;
        _explicitCurrentNextGrid ??= this.FindName("ExplicitCurrentNextGrid") as Grid;
        _autoColumnGrid ??= this.FindName("AutoColumnGrid") as Grid;
        _autoRowGrid ??= this.FindName("AutoRowGrid") as Grid;
        _showGridLinesCheckBox ??= this.FindName("ShowGridLinesCheckBox") as CheckBox;
        _wideCenterCheckBox ??= this.FindName("WideCenterCheckBox") as CheckBox;
        _precisionIncrementsCheckBox ??= this.FindName("PrecisionIncrementsCheckBox") as CheckBox;
        _navigationSplitter ??= this.FindName("NavigationSplitter") as GridSplitter;
        _inspectorSplitter ??= this.FindName("InspectorSplitter") as GridSplitter;
        _timelineSplitter ??= this.FindName("TimelineSplitter") as GridSplitter;
        _primaryCanvasRootGrid ??= this.FindName("PrimaryCanvasRootGrid") as Grid;
        _primaryCanvasHeaderGrid ??= this.FindName("PrimaryCanvasHeaderGrid") as Grid;
        _primaryCanvasHintBorder ??= this.FindName("PrimaryCanvasHintBorder") as Border;
        _primaryCanvasHintGrid ??= this.FindName("PrimaryCanvasHintGrid") as Grid;
        _primaryCanvasHintText ??= this.FindName("PrimaryCanvasHintText") as TextBlock;
        _primaryCanvasHintHotkeyBorder ??= this.FindName("PrimaryCanvasHintHotkeyBorder") as Border;
        _primaryCanvasHintHotkeyText ??= this.FindName("PrimaryCanvasHintHotkeyText") as TextBlock;
        _primaryCanvasHintMinWidthBorder ??= this.FindName("PrimaryCanvasHintMinWidthBorder") as Border;
        _primaryCanvasHintMinWidthText ??= this.FindName("PrimaryCanvasHintMinWidthText") as TextBlock;
        _primaryCanvasLowerGrid ??= this.FindName("PrimaryCanvasLowerGrid") as Grid;
        _primaryCanvasLowerLeftPanel ??= this.FindName("PrimaryCanvasLowerLeftPanel") as Border;
        _primaryCanvasLowerRightPanel ??= this.FindName("PrimaryCanvasLowerRightPanel") as Border;
        _alignmentLeftSplitter ??= this.FindName("AlignmentLeftSplitter") as GridSplitter;
        _alignmentCenterSplitter ??= this.FindName("AlignmentCenterSplitter") as GridSplitter;
        _alignmentRightSplitter ??= this.FindName("AlignmentRightSplitter") as GridSplitter;
        _explicitPreviousCurrentSplitter ??= this.FindName("ExplicitPreviousCurrentSplitter") as GridSplitter;
        _explicitPreviousNextSplitter ??= this.FindName("ExplicitPreviousNextSplitter") as GridSplitter;
        _explicitCurrentNextSplitter ??= this.FindName("ExplicitCurrentNextSplitter") as GridSplitter;
        _autoColumnSplitter ??= this.FindName("AutoColumnSplitter") as GridSplitter;
        _autoRowSplitter ??= this.FindName("AutoRowSplitter") as GridSplitter;
        _presetNarrativeText ??= this.FindName("PresetNarrativeText") as TextBlock;
        _horizontalSummaryText ??= this.FindName("HorizontalSummaryText") as TextBlock;
        _behaviorSummaryText ??= this.FindName("BehaviorSummaryText") as TextBlock;
        _autoDirectionSummaryText ??= this.FindName("AutoDirectionSummaryText") as TextBlock;
        _primaryPairSummaryText ??= this.FindName("PrimaryPairSummaryText") as TextBlock;
        _incrementSummaryText ??= this.FindName("IncrementSummaryText") as TextBlock;
        _interactionSummaryText ??= this.FindName("InteractionSummaryText") as TextBlock;
        _layoutModeValueText ??= this.FindName("LayoutModeValueText") as TextBlock;
        _primaryMetricsText ??= this.FindName("PrimaryMetricsText") as TextBlock;
        _secondaryMetricsText ??= this.FindName("SecondaryMetricsText") as TextBlock;
        _behaviorMetricsText ??= this.FindName("BehaviorMetricsText") as TextBlock;
        _autoDirectionMetricsText ??= this.FindName("AutoDirectionMetricsText") as TextBlock;
    }

    private void ApplyWorkbenchState()
    {
        EnsureReferences();

        if (_primaryEditorGrid == null || _horizontalWorkbenchGrid == null)
        {
            return;
        }

        var preset = GetPresetDefinition(_currentPreset);
        var wideCenter = _wideCenterCheckBox?.IsChecked == true;
        var precisionMode = _precisionIncrementsCheckBox?.IsChecked == true;

        var leftWidth = preset.LeftWidth;
        var centerWidth = preset.CenterWidth;
        var rightWidth = preset.RightWidth;
        if (wideCenter)
        {
            leftWidth = MathF.Max(120f, leftWidth - 32f);
            rightWidth = MathF.Max(140f, rightWidth - 32f);
            centerWidth += 64f;
        }

        SetColumnWidth(_primaryEditorGrid, 0, leftWidth);
        SetColumnWidth(_primaryEditorGrid, 2, centerWidth);
        SetColumnWidth(_primaryEditorGrid, 4, rightWidth);
        SetRowHeight(_horizontalWorkbenchGrid, 0, preset.TopHeight);
        SetRowHeight(_horizontalWorkbenchGrid, 2, preset.BottomHeight);

        var dragIncrement = precisionMode ? 2f : preset.DragIncrement;
        var keyboardIncrement = precisionMode ? 8f : preset.KeyboardIncrement;
        ApplyIncrements(_navigationSplitter, dragIncrement, keyboardIncrement);
        ApplyIncrements(_inspectorSplitter, dragIncrement, keyboardIncrement);
        ApplyIncrements(_timelineSplitter, precisionMode ? 2f : MathF.Max(4f, preset.DragIncrement), precisionMode ? 8f : MathF.Max(12f, preset.KeyboardIncrement - 6f));
        ApplyIncrements(_alignmentLeftSplitter, 1f, 8f);
        ApplyIncrements(_alignmentCenterSplitter, 1f, 8f);
        ApplyIncrements(_alignmentRightSplitter, 1f, 8f);
        ApplyIncrements(_explicitPreviousCurrentSplitter, 1f, 8f);
        ApplyIncrements(_explicitPreviousNextSplitter, 1f, 8f);
        ApplyIncrements(_explicitCurrentNextSplitter, 1f, 8f);
        ApplyIncrements(_autoColumnSplitter, precisionMode ? 2f : 4f, precisionMode ? 8f : 18f);
        ApplyIncrements(_autoRowSplitter, precisionMode ? 2f : 4f, precisionMode ? 8f : 18f);

        ApplyGridLineState(_showGridLinesCheckBox?.IsChecked == true);

        SetText(
            _presetNarrativeText,
            wideCenter
                ? preset.Narrative + " Wider center mode steals a little space from both side rails so the middle pane behaves like a more dominant design canvas."
                : preset.Narrative);
        SetText(_horizontalSummaryText, preset.HorizontalSummary);
        SetText(_behaviorSummaryText, precisionMode ? preset.BehaviorSummary + " Precision increments are on, so the main rails move in smaller steps for easier inspection." : preset.BehaviorSummary);
        SetText(_autoDirectionSummaryText, preset.AutoSummary);
        SetText(_primaryPairSummaryText, $"Navigator: {DescribeColumnPair(_primaryEditorGrid, _navigationSplitter)}. Inspector: {DescribeColumnPair(_primaryEditorGrid, _inspectorSplitter)}.");
        SetText(_incrementSummaryText, $"Main rails drag by {dragIncrement:0}px and arrow keys move by {keyboardIncrement:0}px. Row rail uses {(_timelineSplitter?.DragIncrement ?? 0f):0}px drag and {(_timelineSplitter?.KeyboardIncrement ?? 0f):0}px key steps.");
        SetText(_interactionSummaryText, BuildInteractionSummary());

        _primaryEditorGrid.InvalidateMeasure();
        _primaryEditorGrid.InvalidateArrange();
        _horizontalWorkbenchGrid.InvalidateMeasure();
        _horizontalWorkbenchGrid.InvalidateArrange();

        UpdateTelemetry();
    }

    private void ApplyGridLineState(bool showGridLines)
    {
        SetShowGridLines(_primaryEditorGrid, showGridLines);
        SetShowGridLines(_horizontalWorkbenchGrid, showGridLines);
        SetShowGridLines(_alignmentLeftGrid, showGridLines);
        SetShowGridLines(_alignmentCenterGrid, showGridLines);
        SetShowGridLines(_alignmentRightGrid, showGridLines);
        SetShowGridLines(_explicitPreviousCurrentGrid, showGridLines);
        SetShowGridLines(_explicitPreviousNextGrid, showGridLines);
        SetShowGridLines(_explicitCurrentNextGrid, showGridLines);
        SetShowGridLines(_autoColumnGrid, showGridLines);
        SetShowGridLines(_autoRowGrid, showGridLines);
    }

    private void UpdateResponsiveLayout(float availableWidth)
    {
        EnsureReferences();

        if (_contentGrid == null ||
            _bodyBorder == null ||
            _infoScrollViewer == null ||
            _workbenchScrollViewer == null ||
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
            _workbenchScrollViewer.MinHeight = StackedWorkbenchMinHeight;
            _workbenchScrollViewer.Margin = StackedWorkbenchMargin;
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
            _workbenchScrollViewer.MinHeight = DefaultWorkbenchMinHeight;
            _workbenchScrollViewer.Margin = DefaultWorkbenchMargin;
        }

        _contentGrid.InvalidateMeasure();
        _contentGrid.InvalidateArrange();
        _bodyBorder.InvalidateMeasure();
        _infoScrollViewer.InvalidateMeasure();
        QueueTelemetryUpdate();
    }

    private void UpdateTelemetry()
    {
        EnsureReferences();

        SetText(
            _layoutModeValueText,
            $"Layout: {(_isStackedLayout ? "Stacked info rail" : "Wide side rail")}. Preset: {GetPresetLabel(_currentPreset)}. Grid lines: {(_showGridLinesCheckBox?.IsChecked == true ? "On" : "Off")}. Wider center: {(_wideCenterCheckBox?.IsChecked == true ? "On" : "Off")}. Precision increments: {(_precisionIncrementsCheckBox?.IsChecked == true ? "On" : "Off")}.");

        SetText(
            _primaryMetricsText,
            $"Primary shell columns: {FormatColumnMetrics(_primaryEditorGrid)}. Navigator splitter: {DescribeSplitterState(_navigationSplitter)}. Inspector splitter: {DescribeSplitterState(_inspectorSplitter)}.");

        SetText(
            _secondaryMetricsText,
            $"Row workbench: {FormatRowMetrics(_horizontalWorkbenchGrid)}. Timeline splitter: {DescribeSplitterState(_timelineSplitter)}.");

        SetText(
            _behaviorMetricsText,
            $"Alignment lab pairs: Left={DescribeColumnPair(_alignmentLeftGrid, _alignmentLeftSplitter)}, Center={DescribeColumnPair(_alignmentCenterGrid, _alignmentCenterSplitter)}, Right={DescribeColumnPair(_alignmentRightGrid, _alignmentRightSplitter)}. Explicit pairs: Prev+Cur={DescribeColumnPair(_explicitPreviousCurrentGrid, _explicitPreviousCurrentSplitter)}, Prev+Next={DescribeColumnPair(_explicitPreviousNextGrid, _explicitPreviousNextSplitter)}, Cur+Next={DescribeColumnPair(_explicitCurrentNextGrid, _explicitCurrentNextSplitter)}.");

        SetText(
            _autoDirectionMetricsText,
            $"Auto direction: tall sample -> {ResolveExpectedDirection(_autoColumnSplitter)}, wide sample -> {ResolveExpectedDirection(_autoRowSplitter)}. Tall sample columns: {FormatColumnMetrics(_autoColumnGrid)}. Wide sample rows: {FormatRowMetrics(_autoRowGrid)}.");
    }

    private void QueueTelemetryUpdate()
    {
        if (_telemetryUpdateQueued)
        {
            return;
        }

        _telemetryUpdateQueued = true;
        Dispatcher.EnqueueDeferred(FlushQueuedTelemetryUpdate);
    }

    private void FlushQueuedTelemetryUpdate()
    {
        _telemetryUpdateQueued = false;
        if (!IsAnyPrimaryWorkbenchSplitterDragging())
        {
            UpdateTelemetry();
        }
    }

    private bool IsAnyPrimaryWorkbenchSplitterDragging()
    {
        return _navigationSplitter?.IsDragging == true ||
               _inspectorSplitter?.IsDragging == true ||
               _timelineSplitter?.IsDragging == true;
    }

    private string BuildInteractionSummary()
    {
        return $"Navigator hover={FormatBool(_navigationSplitter?.IsMouseOver)}, drag={FormatBool(_navigationSplitter?.IsDragging)}. Inspector hover={FormatBool(_inspectorSplitter?.IsMouseOver)}, drag={FormatBool(_inspectorSplitter?.IsDragging)}. Timeline hover={FormatBool(_timelineSplitter?.IsMouseOver)}, drag={FormatBool(_timelineSplitter?.IsDragging)}.";
    }

    private static string FormatBool(bool? value)
    {
        return value == true ? "Yes" : "No";
    }

    private static string FormatColumnMetrics(Grid? grid)
    {
        if (grid == null || grid.ColumnDefinitions.Count == 0)
        {
            return "Unavailable";
        }

        var parts = new string[grid.ColumnDefinitions.Count];
        for (var i = 0; i < grid.ColumnDefinitions.Count; i++)
        {
            parts[i] = $"C{i}={grid.ColumnDefinitions[i].ActualWidth:0}";
        }

        return string.Join(" | ", parts);
    }

    private static string FormatRowMetrics(Grid? grid)
    {
        if (grid == null || grid.RowDefinitions.Count == 0)
        {
            return "Unavailable";
        }

        var parts = new string[grid.RowDefinitions.Count];
        for (var i = 0; i < grid.RowDefinitions.Count; i++)
        {
            parts[i] = $"R{i}={grid.RowDefinitions[i].ActualHeight:0}";
        }

        return string.Join(" | ", parts);
    }

    private static string DescribeSplitterState(GridSplitter? splitter)
    {
        if (splitter == null)
        {
            return "Unavailable";
        }

        return $"Dir={splitter.ResizeDirection}, Behavior={splitter.ResizeBehavior}, Drag={splitter.DragIncrement:0}, Key={splitter.KeyboardIncrement:0}, Hover={FormatBool(splitter.IsMouseOver)}, Dragging={FormatBool(splitter.IsDragging)}";
    }

    private static string DescribeColumnPair(Grid? grid, GridSplitter? splitter)
    {
        if (grid == null || splitter == null || grid.ColumnDefinitions.Count < 2)
        {
            return "Unavailable";
        }

        var count = grid.ColumnDefinitions.Count;
        var current = ClampIndex(Grid.GetColumn(splitter), count);
        if (!TryResolvePair(current, count, splitter.HorizontalAlignment, splitter.ResizeBehavior, out var indexA, out var indexB))
        {
            return "Unresolved";
        }

        return $"C{indexA}/C{indexB}";
    }

    private static GridResizeDirection ResolveExpectedDirection(GridSplitter? splitter)
    {
        if (splitter == null)
        {
            return GridResizeDirection.Auto;
        }

        if (splitter.ResizeDirection != GridResizeDirection.Auto)
        {
            return splitter.ResizeDirection;
        }

        var width = float.IsNaN(splitter.Width) ? MathF.Max(0f, splitter.RenderSize.X) : splitter.Width;
        var height = float.IsNaN(splitter.Height) ? MathF.Max(0f, splitter.RenderSize.Y) : splitter.Height;
        if (width <= 0f && height <= 0f)
        {
            return splitter.HorizontalAlignment == HorizontalAlignment.Stretch
                ? GridResizeDirection.Columns
                : GridResizeDirection.Rows;
        }

        return width <= height ? GridResizeDirection.Columns : GridResizeDirection.Rows;
    }

    private static bool TryResolvePair(
        int currentIndex,
        int count,
        HorizontalAlignment alignment,
        GridResizeBehavior behavior,
        out int indexA,
        out int indexB)
    {
        if (behavior == GridResizeBehavior.BasedOnAlignment)
        {
            behavior = alignment switch
            {
                HorizontalAlignment.Left => GridResizeBehavior.PreviousAndCurrent,
                HorizontalAlignment.Right => GridResizeBehavior.CurrentAndNext,
                _ => GridResizeBehavior.PreviousAndNext
            };
        }

        indexA = -1;
        indexB = -1;
        switch (behavior)
        {
            case GridResizeBehavior.CurrentAndNext:
                indexA = currentIndex;
                indexB = currentIndex + 1;
                break;
            case GridResizeBehavior.PreviousAndCurrent:
                indexA = currentIndex - 1;
                indexB = currentIndex;
                break;
            case GridResizeBehavior.PreviousAndNext:
                indexA = currentIndex - 1;
                indexB = currentIndex + 1;
                break;
        }

        return indexA >= 0 && indexB >= 0 && indexA < count && indexB < count && indexA != indexB;
    }

    private static int ClampIndex(int index, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        if (index < 0)
        {
            return 0;
        }

        return index >= count ? count - 1 : index;
    }

    private static void SetShowGridLines(Grid? grid, bool value)
    {
        if (grid != null)
        {
            grid.ShowGridLines = value;
        }
    }

    private static void SetColumnWidth(Grid grid, int index, float value)
    {
        if (index < 0 || index >= grid.ColumnDefinitions.Count)
        {
            return;
        }

        var definition = grid.ColumnDefinitions[index];
        var clamped = ClampToRange(value, definition.MinWidth, definition.MaxWidth);
        if (definition.Width.IsPixel && MathF.Abs(definition.Width.Value - clamped) < 0.01f)
        {
            return;
        }

        definition.Width = new GridLength(clamped, GridUnitType.Pixel);
    }

    private static void SetRowHeight(Grid grid, int index, float value)
    {
        if (index < 0 || index >= grid.RowDefinitions.Count)
        {
            return;
        }

        var definition = grid.RowDefinitions[index];
        var clamped = ClampToRange(value, definition.MinHeight, definition.MaxHeight);
        if (definition.Height.IsPixel && MathF.Abs(definition.Height.Value - clamped) < 0.01f)
        {
            return;
        }

        definition.Height = new GridLength(clamped, GridUnitType.Pixel);
    }

    private static float ClampToRange(float value, float min, float max)
    {
        var upper = float.IsPositiveInfinity(max) ? value : MathF.Min(value, max);
        return MathF.Max(min, upper);
    }

    private static void ApplyIncrements(GridSplitter? splitter, float dragIncrement, float keyboardIncrement)
    {
        if (splitter == null)
        {
            return;
        }

        splitter.DragIncrement = dragIncrement;
        splitter.KeyboardIncrement = keyboardIncrement;
    }

    private static string GetPresetLabel(GridSplitterPreset preset)
    {
        return preset switch
        {
            GridSplitterPreset.Balanced => "Balanced",
            GridSplitterPreset.CanvasFocus => "Canvas Focus",
            GridSplitterPreset.InspectorFocus => "Inspector Focus",
            GridSplitterPreset.KeyboardLab => "Keyboard Lab",
            _ => "Balanced"
        };
    }

    private static PresetDefinition GetPresetDefinition(GridSplitterPreset preset)
    {
        return preset switch
        {
            GridSplitterPreset.CanvasFocus => new PresetDefinition(
                152f,
                458f,
                170f,
                138f,
                178f,
                5f,
                22f,
                "Canvas Focus favors the center workspace so the shell feels like a design surface with two supporting rails instead of three equal regions.",
                "The row sample keeps the diagnostics area secondary so the top workspace dominates like a preview or timeline surface.",
                "Use this preset to see how overlay and dedicated-lane splitters behave when the middle pane is expected to absorb most of the viewport.",
                "Auto direction still stays declarative here: the tall sample reads like a column splitter and the wide sample reads like a row splitter."),
            GridSplitterPreset.InspectorFocus => new PresetDefinition(
                164f,
                312f,
                304f,
                176f,
                136f,
                5f,
                22f,
                "Inspector Focus widens the right rail so property sheets, diagnostics, or metadata take priority without removing the center canvas.",
                "The row sample leaves more height above the splitter so the diagnostics rail feels like an intentional second workspace instead of a footer.",
                "This preset is useful for validation because the explicit behavior lab now sits beside a shell with a more aggressive edge rail allocation.",
                "Auto direction remains geometry-driven, so only the surrounding pane allocation changes."),
            GridSplitterPreset.KeyboardLab => new PresetDefinition(
                176f,
                356f,
                248f,
                148f,
                166f,
                3f,
                14f,
                "Keyboard Lab starts closer to equal partitions and uses smaller default increments so arrow-key resizing is easier to inspect step by step.",
                "The row sample keeps enough space on both sides of the splitter that each Up or Down nudge is visually obvious.",
                "This preset is the cleanest place to compare BasedOnAlignment and explicit behaviors while also watching the live increment telemetry update.",
                "Auto direction does not need a special preset, but this one keeps the overall surface calmer while you test key input."),
            _ => new PresetDefinition(
                180f,
                380f,
                220f,
                150f,
                166f,
                4f,
                24f,
                "Balanced keeps the three-pane shell readable, with two dedicated splitter lanes and enough center width to resemble a typical WPF editor or designer host.",
                "The row sample splits a top workspace and bottom diagnostics lane with an explicit Rows splitter and visible minimum heights on both panes.",
                "The lab below moves from alignment-driven overlay splitters to explicit behavior selection so each pairing strategy is visible in one column.",
                "Auto direction compares tall versus wide splitter geometry without changing the surrounding markup.")
        };
    }

    private static void SetText(TextBlock? target, string value)
    {
        if (target == null || string.Equals(target.Text, value, StringComparison.Ordinal))
        {
            return;
        }

        target.Text = value;
    }

    private void EmitLiveGridSplitterDiagnostics()
    {
        if (!EnableLiveGridSplitterHitchDiagnostics)
        {
            return;
        }

        EnsureReferences();
        var nav = _navigationSplitter;
        var uiRoot = UiRoot.Current;
        if (nav == null || uiRoot == null)
        {
            return;
        }

        if (!nav.IsDragging)
        {
            if (_dragDiagActive)
            {
                var dragEndLine = $"[GridSplitterHitchDiag] phase=drag-end peakLayoutMs={_dragDiagPeakLayoutMs:0.###} peakMeasureMs={_dragDiagPeakMeasureWorkMs:0.###} peakArrangeMs={_dragDiagPeakArrangeWorkMs:0.###} peakDrawTreeMs={_dragDiagPeakDrawTreeMs:0.###} peakFrameworkMeasureWork={_dragDiagPeakFrameworkMeasureWork} peakFrameworkArrangeWork={_dragDiagPeakFrameworkArrangeWork}";
                Debug.WriteLine(dragEndLine);
                AppendLiveGridSplitterDiagnosticArtifactLine(dragEndLine);
                _dragDiagActive = false;
            }

            return;
        }

        var perf = uiRoot.GetPerformanceTelemetrySnapshotForTests();
        var render = uiRoot.GetRenderTelemetrySnapshotForTests();
        var tree = uiRoot.GetVisualTreeWorkMetricsSnapshotForTests();
        var pointer = uiRoot.GetPointerMoveTelemetrySnapshotForTests();
        var renderInvalidation = uiRoot.GetRenderInvalidationDebugSnapshotForTests();
        var textTiming = UiTextRenderer.GetTimingSnapshotForTests();
        var borderTelemetry = Border.GetAggregateTelemetrySnapshotForDiagnostics();
        var frameTiming = FrameworkElement.GetFrameTimingSnapshotForTests();
        var panelTelemetry = Panel.GetTelemetryAndReset();
        var gridTelemetry = Grid.GetAggregateTelemetrySnapshotForDiagnostics();
        var stackPanelTelemetry = StackPanel.GetAggregateTelemetrySnapshotForDiagnostics();
        var scrollViewerTelemetry = ScrollViewer.GetAggregateTelemetrySnapshotForDiagnostics();
        var textBlockTelemetry = TextBlock.GetAggregateTelemetrySnapshotForDiagnostics();
        var centerWidth = GetCenterWidth();
        var widthDelta = float.IsNaN(_dragDiagLastCenterWidth) ? 0f : centerWidth - _dragDiagLastCenterWidth;

        if (!_dragDiagActive)
        {
            _dragDiagActive = true;
            _dragDiagPeakLayoutMs = 0d;
            _dragDiagPeakMeasureWorkMs = 0d;
            _dragDiagPeakArrangeWorkMs = 0d;
            _dragDiagPeakDrawTreeMs = 0d;
            _dragDiagPeakFrameworkMeasureWork = 0;
            _dragDiagPeakFrameworkArrangeWork = 0;
            uiRoot.ClearDirtyBoundsEventTraceForTests();
            FrameworkElement.ResetFrameTimingForTests();
            UiTextRenderer.ResetTimingForTests();
            _ = Grid.GetTelemetryAndReset();
            _ = StackPanel.GetTelemetryAndReset();
            _ = ScrollViewer.GetTelemetryAndReset();
            _ = TextBlock.GetTelemetryAndReset();
            _ = Border.GetTelemetryAndReset();
            _ = Panel.GetTelemetryAndReset();
            _dragDiagPreviousElementStates.Clear();
            ResetLiveGridSplitterDiagnosticArtifact();
            var dragBeginLine = $"[GridSplitterHitchDiag] phase=drag-begin artifact={LiveGridSplitterHitchDiagnosticsArtifactPath}";
            Debug.WriteLine(dragBeginLine);
            AppendLiveGridSplitterDiagnosticArtifactLine(dragBeginLine);
        }

        _dragDiagLastCenterWidth = centerWidth;
        _dragDiagPeakLayoutMs = Math.Max(_dragDiagPeakLayoutMs, perf.LayoutPhaseMilliseconds);
        _dragDiagPeakMeasureWorkMs = Math.Max(_dragDiagPeakMeasureWorkMs, perf.LayoutMeasureWorkMilliseconds);
        _dragDiagPeakArrangeWorkMs = Math.Max(_dragDiagPeakArrangeWorkMs, perf.LayoutArrangeWorkMilliseconds);
        _dragDiagPeakDrawTreeMs = Math.Max(_dragDiagPeakDrawTreeMs, render.DrawVisualTreeMilliseconds);
        _dragDiagPeakFrameworkMeasureWork = Math.Max(_dragDiagPeakFrameworkMeasureWork, tree.MeasureWorkCount);
        _dragDiagPeakFrameworkArrangeWork = Math.Max(_dragDiagPeakFrameworkArrangeWork, tree.ArrangeWorkCount);

        var branchSummary = $"nav={FormatElementWork(_primaryEditorGrid, this.FindName("PrimaryNavigationPane") as FrameworkElement)} canvas={FormatElementWork(_primaryEditorGrid, this.FindName("PrimaryCanvasPane") as FrameworkElement)} insp={FormatElementWork(_primaryEditorGrid, this.FindName("PrimaryInspectorPane") as FrameworkElement)}";
        var canvasSummary = $"header={FormatElementWork(_primaryCanvasRootGrid, _primaryCanvasHeaderGrid)} hint={FormatElementWork(_primaryCanvasRootGrid, _primaryCanvasHintBorder)} lower={FormatElementWork(_primaryCanvasRootGrid, _primaryCanvasLowerGrid)} left={FormatElementWork(_primaryCanvasLowerGrid, _primaryCanvasLowerLeftPanel)} right={FormatElementWork(_primaryCanvasLowerGrid, _primaryCanvasLowerRightPanel)}";
        var canvasRootSummary = FormatGridState(_primaryCanvasRootGrid);
        var hintGridSummary = FormatGridState(_primaryCanvasHintGrid);
        var hintChildrenSummary = $"lead={FormatTextLayoutState(_primaryCanvasHintText)} hotkeyChip={FormatElementState(_primaryCanvasHintHotkeyBorder)} hotkeyText={FormatTextLayoutState(_primaryCanvasHintHotkeyText)} minChip={FormatElementState(_primaryCanvasHintMinWidthBorder)} minText={FormatTextLayoutState(_primaryCanvasHintMinWidthText)}";
        var rawDirtyCoverage = uiRoot.GetDirtyCoverageForTests();
        var settleFramesRemaining = uiRoot.GetFullRedrawSettleFramesRemainingForTests();
        var wouldUsePartial = uiRoot.WouldUsePartialDirtyRedrawForTests();
        var dirtyRegionSummary = uiRoot.GetDirtyRegionSummaryForTests();
        var dirtyQueueSummary = uiRoot.GetDirtyRenderQueueSummaryForTests();
        var syncedDirtyRootSummary = uiRoot.GetLastSynchronizedDirtyRootSummaryForTests();
        var dirtyBoundsTrace = SummarizeDirtyBoundsTrace(uiRoot.GetDirtyBoundsEventTraceForTests(), maxEntries: 24);
        var compactDirtyBoundsTrace = SummarizeDirtyBoundsTrace(uiRoot.GetDirtyBoundsEventTraceForTests(), maxEntries: 8);
        var drawnVisualSummary = SummarizeLargestDrawnVisuals(uiRoot, renderInvalidation.DirtyBounds, maxEntries: 8);
        var invalidationPathSummary = SummarizeInvalidationPath(renderInvalidation);
        var suspectChainSummary = SummarizeInvalidationSuspectChain(
            _primaryEditorGrid,
            _workbenchScrollViewer,
            _bodyBorder,
            _contentGrid,
            _rootGrid,
            this);
        var layoutDeltaSummary = SummarizeLayoutDeltaDiagnostics(this, maxEntries: 5);
        var summaryLine =
            $"[GridSplitterHitchDiag] seq={++_liveDiagSequence} centerWidth={centerWidth:0.##} widthDelta={widthDelta:0.##} prevDrawMs={uiRoot.LastDrawMs:0.###} prevDrawTreeMs={render.DrawVisualTreeMilliseconds:0.###} updateMs={uiRoot.LastUpdateMs:0.###} measureMs={perf.LayoutMeasureWorkMilliseconds:0.###} prevPartial={FormatBool(uiRoot.LastDrawUsedPartialRedraw)} currRawDirtyPct={rawDirtyCoverage:0.###} currWouldPartial={FormatBool(wouldUsePartial)} fullDirty={FormatBool(uiRoot.IsFullDirtyForTests())} settle={settleFramesRemaining} dirtyRects={uiRoot.GetDirtyRegionsSnapshotForTests().Count} dirtySrc={renderInvalidation.EffectiveSourceType}#{renderInvalidation.EffectiveSourceName}|via={renderInvalidation.EffectiveSourceResolution} retained={renderInvalidation.RetainedSyncSourceType}#{renderInvalidation.RetainedSyncSourceName}|via={renderInvalidation.RetainedSyncSourceResolution} dirtyVisual={renderInvalidation.DirtyBoundsVisualType}#{renderInvalidation.DirtyBoundsVisualName}|via={renderInvalidation.DirtyBoundsSourceResolution}|hint={FormatBool(renderInvalidation.DirtyBoundsUsedHint)} dirtyBounds={FormatRect(renderInvalidation.DirtyBounds)} workbenchViewer={FormatScrollViewerState(_workbenchScrollViewer)} dirtyTrace={compactDirtyBoundsTrace}";
        var detailedLine =
            $"[GridSplitterHitchDiag] seq={_liveDiagSequence} drag=True centerWidth={centerWidth:0.##} widthDelta={widthDelta:0.##} requestedDelta={nav.GetGridSplitterSnapshotForDiagnostics().LastRequestedDelta:0.##} snappedDelta={nav.GetGridSplitterSnapshotForDiagnostics().LastSnappedDelta:0.##} appliedDelta={nav.GetGridSplitterSnapshotForDiagnostics().LastAppliedDelta:0.##} " +
            $"layoutMs={perf.LayoutPhaseMilliseconds:0.###} measureMs={perf.LayoutMeasureWorkMilliseconds:0.###} arrangeMs={perf.LayoutArrangeWorkMilliseconds:0.###} updateMs={uiRoot.LastUpdateMs:0.###} updateFps={FormatFramesPerSecond(uiRoot.LastUpdateMs)} drawMs={uiRoot.LastDrawMs:0.###} drawFps={FormatFramesPerSecond(uiRoot.LastDrawMs)} drawTreeMs={render.DrawVisualTreeMilliseconds:0.###} retainedTrav={perf.RetainedTraversalCount} dirtyRoots={perf.DirtyRootCount} dirtyRects={uiRoot.GetDirtyRegionsSnapshotForTests().Count} fullDirty={uiRoot.IsFullDirtyForTests()} dirtySrc={renderInvalidation.EffectiveSourceType}#{renderInvalidation.EffectiveSourceName} shouldDraw={uiRoot.LastShouldDrawReasons} drawReasons={uiRoot.LastDrawReasons} " +
            $"retained[nodes={render.RetainedNodesVisited}/{render.RetainedNodesDrawn},clips={render.ClipPushCount},restarts={render.SpriteBatchRestartCount},partial={FormatBool(uiRoot.LastDrawUsedPartialRedraw)},dirtyPct={uiRoot.LastDirtyAreaPercentage:0.###},rawDirtyPct={rawDirtyCoverage:0.###},wouldPartial={FormatBool(wouldUsePartial)},settle={settleFramesRemaining}] " +
            $"drawPhases[clear={render.DrawClearMilliseconds:0.###},begin={render.DrawInitialBatchBeginMilliseconds:0.###},tree={render.DrawVisualTreeMilliseconds:0.###},cursor={render.DrawCursorMilliseconds:0.###},end={render.DrawFinalBatchEndMilliseconds:0.###},cleanup={render.DrawCleanupMilliseconds:0.###}] " +
            $"textDraw[calls={textTiming.DrawStringCallCount},hottestMs={textTiming.HottestDrawStringMilliseconds:0.###},hottestText={SanitizeDiagnosticText(textTiming.HottestDrawStringText)},hottestTypo={SanitizeDiagnosticText(textTiming.HottestDrawStringTypography)}] " +
            $"measureHotspots[{SummarizeMeasureHotspots(frameTiming, gridTelemetry, stackPanelTelemetry, scrollViewerTelemetry, textBlockTelemetry, borderTelemetry, panelTelemetry, textTiming)}] " +
            $"borderDraw[renders={borderTelemetry.RenderCallCount},rounded={borderTelemetry.RenderRoundedPathCount},rect={borderTelemetry.RenderRectangularPathCount},cacheHit={borderTelemetry.RenderTextureCacheHitCount},cacheMiss={borderTelemetry.RenderTextureCacheMissCount},cacheReject={borderTelemetry.RenderTextureCacheRejectedAreaCount},texBuilds={borderTelemetry.TextureBuildCount},texBuildMs={borderTelemetry.TextureBuildMilliseconds:0.###},texPixels={borderTelemetry.TextureBuildPixelCount},geomHits={borderTelemetry.RoundedGeometryCacheHitCount},geomMiss={borderTelemetry.RoundedGeometryCacheMissCount},geomPts={borderTelemetry.RoundedGeometryBuildPointCount}] " +
            $"invalidationPath[{invalidationPathSummary}] " +
            $"suspectChain[{suspectChainSummary}] " +
            $"dirtyBounds={renderInvalidation.DirtyBoundsVisualType}#{renderInvalidation.DirtyBoundsVisualName}|hint={FormatBool(renderInvalidation.DirtyBoundsUsedHint)}|rect={FormatRect(renderInvalidation.DirtyBounds)} " +
            $"dirtyRegions={dirtyRegionSummary} dirtyQueue={dirtyQueueSummary} syncedDirtyRoots={syncedDirtyRootSummary} dirtyTrace={dirtyBoundsTrace} " +
            $"drawnVisuals={drawnVisualSummary} " +
            $"pointerPath={pointer.PointerResolvePath} hitTests={pointer.HitTestCount} routeMs={pointer.PointerRouteMilliseconds:0.###} moveHandlerMs={pointer.PointerMoveHandlerMilliseconds:0.###} " +
            $"hottestMeasure={perf.HottestLayoutMeasureElementType}#{perf.HottestLayoutMeasureElementName}:{perf.HottestLayoutMeasureElementMilliseconds:0.###} hottestArrange={perf.HottestLayoutArrangeElementType}#{perf.HottestLayoutArrangeElementName}:{perf.HottestLayoutArrangeElementMilliseconds:0.###} " +
            $"frameworkWork[m={tree.MeasureWorkCount},a={tree.ArrangeWorkCount}] grid={FormatGridState(_primaryEditorGrid)} workbenchViewer={FormatScrollViewerState(_workbenchScrollViewer)} canvasRoot={canvasRootSummary} canvasHint={FormatTextLayoutState(_primaryCanvasHintText)} hintGrid={hintGridSummary} hintChildren[{hintChildrenSummary}] branches[{branchSummary}] canvasSections[{canvasSummary}] layoutDelta[{layoutDeltaSummary}]";

        Debug.WriteLine(summaryLine);
        AppendLiveGridSplitterDiagnosticArtifactLine(detailedLine);
        uiRoot.ClearDirtyBoundsEventTraceForTests();
        FrameworkElement.ResetFrameTimingForTests();
        UiTextRenderer.ResetTimingForTests();
        _ = Grid.GetTelemetryAndReset();
        _ = StackPanel.GetTelemetryAndReset();
        _ = ScrollViewer.GetTelemetryAndReset();
        _ = TextBlock.GetTelemetryAndReset();
        _ = Border.GetTelemetryAndReset();
        _ = Panel.GetTelemetryAndReset();
    }

    private static string SanitizeDiagnosticText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "none";
        }

        return value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("|", "/", StringComparison.Ordinal);
    }

    private static string SummarizeMeasureHotspots(
        FrameworkLayoutTimingSnapshot frameTiming,
        GridTelemetrySnapshot gridTelemetry,
        StackPanelTelemetrySnapshot stackPanelTelemetry,
        ScrollViewerTelemetrySnapshot scrollViewerTelemetry,
        TextBlockTelemetrySnapshot textBlockTelemetry,
        BorderTelemetrySnapshot borderTelemetry,
        PanelTelemetrySnapshot panelTelemetry,
        UiTextRendererTimingSnapshot textTiming)
    {
         return $"frame={SanitizeDiagnosticText(frameTiming.HottestMeasureElementPath)}:{TicksToMilliseconds(frameTiming.HottestMeasureElapsedTicks):0.###}," +
             $"grid={gridTelemetry.MeasureMilliseconds:0.###}/child={gridTelemetry.MeasureChildMilliseconds:0.###}/hit={gridTelemetry.MeasureChildCacheHitCount}/miss={gridTelemetry.MeasureChildCacheMissCount}/need={gridTelemetry.MeasureChildMissNeedsMeasureCount}/needHot={SanitizeDiagnosticText(gridTelemetry.MeasureChildNeedHottestPath)}:{gridTelemetry.MeasureChildNeedHottestMilliseconds:0.###}/cold={gridTelemetry.MeasureChildMissInvalidCacheCount}/reject={gridTelemetry.MeasureChildMissReuseRejectedCount}/rejectHot={SanitizeDiagnosticText(gridTelemetry.MeasureChildRejectHottestPath)}:{gridTelemetry.MeasureChildRejectHottestMilliseconds:0.###}/defs={gridTelemetry.ResolveDefinitionSizesMilliseconds:0.###}/apply={gridTelemetry.ApplyChildRequirementMilliseconds:0.###}/meta={gridTelemetry.PrepareChildLayoutMetadataMilliseconds:0.###}/remeasure={gridTelemetry.MeasureRemeasureCount}," +
               $"stack={stackPanelTelemetry.MeasureMilliseconds:0.###}," +
               $"scroll={scrollViewerTelemetry.MeasureOverrideMilliseconds:0.###}/bars={scrollViewerTelemetry.ResolveBarsAndMeasureContentMilliseconds:0.###}/content={scrollViewerTelemetry.MeasureContentMilliseconds:0.###}/remeasure={scrollViewerTelemetry.ResolveBarsAndMeasureContentRemeasurePathCount}," +
               $"text={textBlockTelemetry.MeasureOverrideMilliseconds:0.###}/layout={textBlockTelemetry.ResolveLayoutMilliseconds:0.###}/intrinsic={textBlockTelemetry.ResolveIntrinsicNoWrapTextSizeMilliseconds:0.###}/widthHot={textTiming.HottestMeasureWidthMilliseconds:0.###}," +
               $"border={borderTelemetry.MeasureOverrideMilliseconds:0.###}," +
               $"panel={panelTelemetry.MeasureMilliseconds:0.###}";
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks <= 0L
            ? 0d
            : ticks * 1000d / Stopwatch.Frequency;
    }

    private static string SummarizeLargestDrawnVisuals(UiRoot uiRoot, LayoutRect clipRect, int maxEntries)
    {
        var drawnVisuals = uiRoot.GetRetainedDrawOrderForClipForTests(clipRect);
        if (drawnVisuals.Count == 0)
        {
            return "none";
        }

        var ranked = new List<(float Area, UIElement Visual, LayoutRect Bounds)>(drawnVisuals.Count);
        foreach (var visual in drawnVisuals)
        {
            var bounds = uiRoot.GetRetainedNodeBoundsForTests(visual);
            ranked.Add((bounds.Width * bounds.Height, visual, bounds));
        }

        ranked.Sort(static (left, right) => right.Area.CompareTo(left.Area));
        var count = Math.Min(maxEntries, ranked.Count);
        var parts = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var item = ranked[i];
            var name = item.Visual is FrameworkElement frameworkElement ? frameworkElement.Name : string.Empty;
            parts.Add($"{item.Visual.GetType().Name}#{name}:{FormatRect(item.Bounds)}");
        }

        return string.Join(" || ", parts);
    }

    private static void ResetLiveGridSplitterDiagnosticArtifact()
    {
        try
        {
            var artifactDirectory = Path.GetDirectoryName(LiveGridSplitterHitchDiagnosticsArtifactPath);
            if (!string.IsNullOrWhiteSpace(artifactDirectory))
            {
                Directory.CreateDirectory(artifactDirectory);
            }

            File.WriteAllText(
                LiveGridSplitterHitchDiagnosticsArtifactPath,
                $"# GridSplitter live hitch diagnostics{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private static void AppendLiveGridSplitterDiagnosticArtifactLine(string line)
    {
        try
        {
            var artifactDirectory = Path.GetDirectoryName(LiveGridSplitterHitchDiagnosticsArtifactPath);
            if (!string.IsNullOrWhiteSpace(artifactDirectory))
            {
                Directory.CreateDirectory(artifactDirectory);
            }

            File.AppendAllText(
                LiveGridSplitterHitchDiagnosticsArtifactPath,
                line + Environment.NewLine);
        }
        catch
        {
        }
    }

    private static string SummarizeDirtyBoundsTrace(IReadOnlyList<string> entries, int maxEntries)
    {
        if (entries.Count == 0)
        {
            return "none";
        }

        var summaries = new List<string>(Math.Min(entries.Count, maxEntries));
        string? previous = null;
        var repeatCount = 0;

        void FlushPrevious()
        {
            if (previous == null)
            {
                return;
            }

            summaries.Add(repeatCount > 1
                ? $"{previous} x{repeatCount}"
                : previous);
        }

        for (var i = 0; i < entries.Count; i++)
        {
            var current = entries[i];
            if (string.Equals(current, previous, StringComparison.Ordinal))
            {
                repeatCount++;
                continue;
            }

            FlushPrevious();
            if (summaries.Count >= maxEntries)
            {
                break;
            }

            previous = current;
            repeatCount = 1;
        }

        if (summaries.Count < maxEntries)
        {
            FlushPrevious();
        }

        if (entries.Count > maxEntries)
        {
            summaries.Add($"... ({entries.Count - maxEntries} more)");
        }

        return string.Join(" || ", summaries);
    }

    private float GetCenterWidth()
    {
        return _primaryEditorGrid != null && _primaryEditorGrid.ColumnDefinitions.Count > 2
            ? _primaryEditorGrid.ColumnDefinitions[2].ActualWidth
            : 0f;
    }

    private static string FormatGridState(Grid? grid)
    {
        if (grid == null)
        {
            return "none";
        }

        var runtime = grid.GetGridSnapshotForDiagnostics();
        return $"{grid.Name}|cols={FormatColumns(grid)} rows={FormatRows(grid)} mw={runtime.MeasureWorkCount} aw={runtime.ArrangeWorkCount} mi={runtime.MeasureInvalidationCount} ai={runtime.ArrangeInvalidationCount}";
    }

    private static string FormatScrollViewerState(ScrollViewer? viewer)
    {
        if (viewer == null)
        {
            return "none";
        }

        var runtime = viewer.GetScrollViewerSnapshotForDiagnostics();
        return $"{viewer.Name}|vp={viewer.ViewportWidth:0.##}x{viewer.ViewportHeight:0.##} ext={viewer.ExtentWidth:0.##}x{viewer.ExtentHeight:0.##} off={viewer.HorizontalOffset:0.##},{viewer.VerticalOffset:0.##} barsM={runtime.ResolveBarsAndMeasureContentCallCount} contentM={runtime.MeasureContentCallCount} setOffsets={runtime.SetOffsetsCallCount}/{runtime.SetOffsetsNoOpCount}";
    }

    private static string FormatFramesPerSecond(double milliseconds)
    {
        if (milliseconds <= 0d)
        {
            return "inf";
        }

        return (1000d / milliseconds).ToString("0.#");
    }

    private static string FormatTextState(TextBlock? text)
    {
        if (text == null)
        {
            return "none";
        }

        var runtime = text.GetTextBlockSnapshotForDiagnostics();
        return $"{text.Name}|slotW={text.LayoutSlot.Width:0.##} desired={text.DesiredSize.X:0.##}x{text.DesiredSize.Y:0.##} actual={text.ActualWidth:0.##}x{text.ActualHeight:0.##} layoutCalls={runtime.ResolveLayoutCallCount} hits={runtime.ResolveLayoutCacheHitCount} misses={runtime.ResolveLayoutCacheMissCount}";
    }

    private static string FormatTextLayoutState(TextBlock? text)
    {
        if (text == null)
        {
            return "none";
        }

        var availableWidth = text.TextWrapping == TextWrapping.Wrap
            ? Math.Max(0f, text.LayoutSlot.Width)
            : float.PositiveInfinity;
        var layout = TextLayout.LayoutForElement(
            text.Text,
            text,
            text.FontSize,
            availableWidth,
            text.TextWrapping);

        return $"{FormatTextState(text)} lines={layout.Lines.Count} layout={layout.Size.X:0.##}x{layout.Size.Y:0.##}";
    }

    private static string FormatElementState(FrameworkElement? element)
    {
        if (element == null)
        {
            return "none";
        }

        var snap = element.GetFrameworkElementSnapshotForDiagnostics();
        return $"{element.Name}|slot={element.LayoutSlot.Width:0.##}x{element.LayoutSlot.Height:0.##} desired={snap.DesiredSize.X:0.##}x{snap.DesiredSize.Y:0.##} actual={element.ActualWidth:0.##}x{element.ActualHeight:0.##}";
    }

    private static string FormatElementWork(FrameworkElement? root, FrameworkElement? element)
    {
        if (root == null || element == null)
        {
            return "none";
        }

        var rootSnap = root.GetFrameworkElementSnapshotForDiagnostics();
        var snap = element.GetFrameworkElementSnapshotForDiagnostics();
        return $"{element.Name}|mw={snap.MeasureWorkCount - rootSnap.MeasureWorkCount} aw={snap.ArrangeWorkCount - rootSnap.ArrangeWorkCount} mi={snap.InvalidateMeasureCallCount} ai={snap.InvalidateArrangeCallCount} slot={element.LayoutSlot.Width:0.##}x{element.LayoutSlot.Height:0.##}";
    }

    private string SummarizeInvalidationPath(UiRenderInvalidationDebugSnapshot snapshot)
    {
        return $"requested={FormatVisualIdentity(snapshot.RequestedSourceType, snapshot.RequestedSourceName)}|last={SanitizeDiagnosticText(snapshot.RequestedSourceSummary)} effective={FormatVisualIdentity(snapshot.EffectiveSourceType, snapshot.EffectiveSourceName)}|via={snapshot.EffectiveSourceResolution}|last={SanitizeDiagnosticText(snapshot.EffectiveSourceSummary)} clip={FormatVisualIdentity(snapshot.ClipPromotionAncestorType, snapshot.ClipPromotionAncestorName)} retained={FormatVisualIdentity(snapshot.RetainedSyncSourceType, snapshot.RetainedSyncSourceName)}|via={snapshot.RetainedSyncSourceResolution}|last={SanitizeDiagnosticText(snapshot.RetainedSyncSourceSummary)} dirty={FormatVisualIdentity(snapshot.DirtyBoundsVisualType, snapshot.DirtyBoundsVisualName)}|via={snapshot.DirtyBoundsSourceResolution}|last={SanitizeDiagnosticText(snapshot.DirtyBoundsVisualSummary)}";
    }

    private static string SummarizeInvalidationSuspectChain(params FrameworkElement?[] elements)
    {
        var parts = new List<string>(elements.Length);
        foreach (var element in elements)
        {
            if (element == null)
            {
                continue;
            }

            var snapshot = element.GetFrameworkElementSnapshotForDiagnostics();
            var invalidation = snapshot.Invalidation;
            parts.Add(
                $"{DescribeDiagnosticElement(element)}|mi={snapshot.InvalidateMeasureCallCount}/ai={snapshot.InvalidateArrangeCallCount}/ri={snapshot.InvalidateVisualCallCount}" +
                $"|m={invalidation.DirectMeasureInvalidationCount}+{invalidation.PropagatedMeasureInvalidationCount}:{SanitizeDiagnosticText(invalidation.LastMeasureInvalidationSummary)}" +
                $"|a={invalidation.DirectArrangeInvalidationCount}+{invalidation.PropagatedArrangeInvalidationCount}:{SanitizeDiagnosticText(invalidation.LastArrangeInvalidationSummary)}" +
                $"|r={invalidation.DirectRenderInvalidationCount}+{invalidation.PropagatedRenderInvalidationCount}:{SanitizeDiagnosticText(invalidation.LastRenderInvalidationSummary)}");
        }

        return parts.Count == 0
            ? "none"
            : string.Join(" || ", parts);
    }

    private string SummarizeLayoutDeltaDiagnostics(FrameworkElement root, int maxEntries)
    {
        var currentStates = new Dictionary<FrameworkElement, DragLayoutElementState>(ReferenceEqualityComparer<FrameworkElement>.Instance);
        var desiredDeltas = new List<DragLayoutDelta>();
        var slotDeltas = new List<DragLayoutDelta>();
        var invalidationDeltas = new List<DragLayoutDelta>();
        var workDeltas = new List<DragLayoutDelta>();

        CollectDragLayoutStates(root, DescribeDiagnosticElement(root), currentStates, desiredDeltas, slotDeltas, invalidationDeltas, workDeltas);

        _dragDiagPreviousElementStates.Clear();
        foreach (var pair in currentStates)
        {
            _dragDiagPreviousElementStates[pair.Key] = pair.Value;
        }

        var workbenchViewerState = _workbenchScrollViewer?.GetFrameworkElementSnapshotForDiagnostics();
        var workbenchContentState = _workbenchScrollViewer?.Content as FrameworkElement;
        var workbenchContentSnapshot = workbenchContentState?.GetFrameworkElementSnapshotForDiagnostics();
        var rootState = root.GetFrameworkElementSnapshotForDiagnostics();
        var extentSummary = $"viewerExt={FormatOptionalTransition(_workbenchScrollViewer, currentStates, state => new Vector2(_workbenchScrollViewer?.ViewportWidth ?? 0f, _workbenchScrollViewer?.ExtentHeight ?? 0f), state => state.ViewerExtent)} viewerDesired={FormatOptionalTransition(_workbenchScrollViewer, currentStates, state => workbenchViewerState?.DesiredSize ?? Vector2.Zero, state => state.DesiredSize)} contentDesired={FormatOptionalTransition(workbenchContentState, currentStates, state => workbenchContentSnapshot?.DesiredSize ?? Vector2.Zero, state => state.DesiredSize)} rootDesired={FormatOptionalTransition(root, currentStates, _ => rootState.DesiredSize, state => state.DesiredSize)}";

        return $"extent={extentSummary}; desired={FormatTopLayoutDeltas(desiredDeltas, maxEntries)}; slot={FormatTopLayoutDeltas(slotDeltas, maxEntries)}; invalid={FormatTopLayoutDeltas(invalidationDeltas, maxEntries)}; work={FormatTopLayoutDeltas(workDeltas, maxEntries)}";
    }

    private void CollectDragLayoutStates(
        FrameworkElement element,
        string path,
        Dictionary<FrameworkElement, DragLayoutElementState> currentStates,
        List<DragLayoutDelta> desiredDeltas,
        List<DragLayoutDelta> slotDeltas,
        List<DragLayoutDelta> invalidationDeltas,
        List<DragLayoutDelta> workDeltas)
    {
        var snapshot = element.GetFrameworkElementSnapshotForDiagnostics();
        var currentState = new DragLayoutElementState(
            path,
            snapshot.DesiredSize,
            snapshot.Slot,
            snapshot.RenderSize,
            snapshot.MeasureWorkCount,
            snapshot.ArrangeWorkCount,
            snapshot.InvalidateMeasureCallCount,
            snapshot.InvalidateArrangeCallCount,
            snapshot.Invalidation,
            element == _workbenchScrollViewer
                ? new Vector2(_workbenchScrollViewer?.ViewportWidth ?? 0f, _workbenchScrollViewer?.ExtentHeight ?? 0f)
                : new Vector2(float.NaN, float.NaN));
        currentStates[element] = currentState;

        if (_dragDiagPreviousElementStates.TryGetValue(element, out var previousState))
        {
            var desiredMagnitude = MathF.Abs(currentState.DesiredSize.Y - previousState.DesiredSize.Y) + MathF.Abs(currentState.DesiredSize.X - previousState.DesiredSize.X);
            if (desiredMagnitude > 0.01f)
            {
                desiredDeltas.Add(CreateDragLayoutDelta(currentState, previousState, desiredMagnitude));
            }

            var slotMagnitude = MathF.Abs(currentState.Slot.Height - previousState.Slot.Height) + MathF.Abs(currentState.Slot.Width - previousState.Slot.Width);
            if (slotMagnitude > 0.01f)
            {
                slotDeltas.Add(CreateDragLayoutDelta(currentState, previousState, slotMagnitude));
            }

            var invalidationMagnitude = (currentState.InvalidateMeasureCalls - previousState.InvalidateMeasureCalls) +
                                        (currentState.InvalidateArrangeCalls - previousState.InvalidateArrangeCalls);
            if (invalidationMagnitude > 0)
            {
                invalidationDeltas.Add(CreateDragLayoutDelta(currentState, previousState, invalidationMagnitude));
            }

            var workMagnitude = (currentState.MeasureWorkCount - previousState.MeasureWorkCount) +
                                (currentState.ArrangeWorkCount - previousState.ArrangeWorkCount);
            if (workMagnitude > 0)
            {
                workDeltas.Add(CreateDragLayoutDelta(currentState, previousState, workMagnitude));
            }
        }

        var childIndex = 0;
        foreach (var child in element.GetVisualChildren())
        {
            if (child is not FrameworkElement frameworkChild)
            {
                childIndex++;
                continue;
            }

            CollectDragLayoutStates(
                frameworkChild,
                $"{path} > {DescribeDiagnosticElement(frameworkChild, childIndex)}",
                currentStates,
                desiredDeltas,
                slotDeltas,
                invalidationDeltas,
                workDeltas);
            childIndex++;
        }
    }

    private static DragLayoutDelta CreateDragLayoutDelta(DragLayoutElementState currentState, DragLayoutElementState previousState, float magnitude)
    {
        return new DragLayoutDelta(previousState, currentState, magnitude);
    }

    private static string FormatTopLayoutDeltas(List<DragLayoutDelta> deltas, int maxEntries)
    {
        if (deltas.Count == 0)
        {
            return "none";
        }

        deltas.Sort(static (left, right) => right.Magnitude.CompareTo(left.Magnitude));
        var count = Math.Min(maxEntries, deltas.Count);
        var parts = new string[count];
        for (var i = 0; i < count; i++)
        {
            var delta = deltas[i];
            parts[i] = $"{delta.Current.Path}|desired={FormatSize(delta.Previous.DesiredSize)}->{FormatSize(delta.Current.DesiredSize)}|slot={FormatRect(delta.Previous.Slot)}->{FormatRect(delta.Current.Slot)}|mw=+{delta.Current.MeasureWorkCount - delta.Previous.MeasureWorkCount}|aw=+{delta.Current.ArrangeWorkCount - delta.Previous.ArrangeWorkCount}|mi=+{delta.Current.InvalidateMeasureCalls - delta.Previous.InvalidateMeasureCalls}|ai=+{delta.Current.InvalidateArrangeCalls - delta.Previous.InvalidateArrangeCalls}|mlast={SanitizeDiagnosticText(delta.Current.Invalidation.LastMeasureInvalidationSummary)}|alast={SanitizeDiagnosticText(delta.Current.Invalidation.LastArrangeInvalidationSummary)}";
        }

        return string.Join(" || ", parts);
    }

    private static string DescribeDiagnosticElement(FrameworkElement element, int? childIndex = null)
    {
        var typeName = element.GetType().Name;
        if (!string.IsNullOrEmpty(element.Name))
        {
            return $"{typeName}#{element.Name}";
        }

        return childIndex.HasValue
            ? $"{typeName}@{childIndex.Value}"
            : typeName;
    }

    private static string FormatVisualIdentity(string type, string name)
    {
        if (string.Equals(type, "none", StringComparison.OrdinalIgnoreCase))
        {
            return "none";
        }

        return string.IsNullOrEmpty(name)
            ? type
            : $"{type}#{name}";
    }

    private string FormatOptionalTransition<TElement>(TElement? element, Dictionary<FrameworkElement, DragLayoutElementState> currentStates, Func<DragLayoutElementState, Vector2> currentSelector, Func<DragLayoutElementState, Vector2> previousSelector)
        where TElement : FrameworkElement
    {
        if (element == null)
        {
            return "none";
        }

        if (!currentStates.TryGetValue(element, out var currentState))
        {
            return "none";
        }

        if (_dragDiagPreviousElementStates.TryGetValue(element, out var previousState))
        {
            return $"{FormatSize(previousSelector(previousState))}->{FormatSize(currentSelector(currentState))}";
        }

        return $"baseline->{FormatSize(currentSelector(currentState))}";
    }

    private static string FormatSize(Vector2 size)
    {
        return float.IsNaN(size.X) || float.IsNaN(size.Y)
            ? "none"
            : $"{size.X:0.##}x{size.Y:0.##}";
    }

    private static string FormatColumns(Grid grid)
    {
        var parts = new string[grid.ColumnDefinitions.Count];
        for (var i = 0; i < grid.ColumnDefinitions.Count; i++)
        {
            parts[i] = grid.ColumnDefinitions[i].ActualWidth.ToString("0.#");
        }

        return string.Join(",", parts);
    }

    private static string FormatRows(Grid grid)
    {
        var parts = new string[grid.RowDefinitions.Count];
        for (var i = 0; i < grid.RowDefinitions.Count; i++)
        {
            parts[i] = grid.RowDefinitions[i].ActualHeight.ToString("0.#");
        }

        return string.Join(",", parts);
    }

    private static string FormatRect(LayoutRect rect)
    {
        return $"{rect.X:0.##},{rect.Y:0.##},{rect.Width:0.##},{rect.Height:0.##}";
    }

    private readonly record struct DragLayoutElementState(
        string Path,
        Vector2 DesiredSize,
        LayoutRect Slot,
        Vector2 RenderSize,
        long MeasureWorkCount,
        long ArrangeWorkCount,
        long InvalidateMeasureCalls,
        long InvalidateArrangeCalls,
        UIElementInvalidationDiagnosticsSnapshot Invalidation,
        Vector2 ViewerExtent);

    private readonly record struct DragLayoutDelta(
        DragLayoutElementState Previous,
        DragLayoutElementState Current,
        float Magnitude);

    private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
        where T : class
    {
        public static ReferenceEqualityComparer<T> Instance { get; } = new();

        public bool Equals(T? x, T? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(T obj)
        {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }

    private enum GridSplitterPreset
    {
        Balanced,
        CanvasFocus,
        InspectorFocus,
        KeyboardLab
    }

    private readonly record struct PresetDefinition(
        float LeftWidth,
        float CenterWidth,
        float RightWidth,
        float TopHeight,
        float BottomHeight,
        float DragIncrement,
        float KeyboardIncrement,
        string Narrative,
        string HorizontalSummary,
        string BehaviorSummary,
        string AutoSummary);
}




