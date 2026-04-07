using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class GridSplitterView : UserControl
{
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
    public GridSplitterView()
    {
        InitializeComponent();
        EnsureReferences();
        WireEvents();
        ApplyWorkbenchState();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        UpdateResponsiveLayout(availableSize.X);
        return base.MeasureOverride(availableSize);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        UpdateResponsiveLayout(finalSize.X);
        return base.ArrangeOverride(finalSize);
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
        _primaryEditorGrid.InvalidateMeasure();
        _primaryEditorGrid.InvalidateArrange();
        _horizontalWorkbenchGrid.InvalidateMeasure();
        _horizontalWorkbenchGrid.InvalidateArrange();
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
                "The row sample keeps the lower lane secondary so the top workspace dominates like a preview or timeline surface.",
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
                "Inspector Focus widens the right rail so property sheets, supporting tools, or metadata take priority without removing the center canvas.",
                "The row sample leaves more height above the splitter so the secondary rail feels like an intentional second workspace instead of a footer.",
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
                "This preset is the cleanest place to compare BasedOnAlignment and explicit behaviors while also watching the live increment settings update.",
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
                "The row sample splits a top workspace and bottom utility lane with an explicit Rows splitter and visible minimum heights on both panes.",
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




