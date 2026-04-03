using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class VirtualizingStackPanelView : UserControl
{
    private const int DirectItemCount = 160;
    private readonly List<WorkbenchPanelItem> _panelItems = CreatePanelItems();
    private readonly ObservableCollection<WorkbenchTemplateItem> _templateItems = CreateTemplateItems();

    private ComboBox? _orientationComboBox;
    private ComboBox? _virtualizationModeComboBox;
    private ComboBox? _cacheUnitComboBox;
    private CheckBox? _isVirtualizingCheckBox;
    private CheckBox? _variableHeightsCheckBox;
    private CheckBox? _denseCardsCheckBox;
    private Slider? _cacheLengthSlider;
    private Slider? _targetIndexSlider;
    private ScrollViewer? _workbenchScrollViewer;
    private VirtualizingStackPanel? _workbenchPanel;
    private ListBox? _templateListBox;
    private TextBlock? _cacheLengthValueText;
    private TextBlock? _targetIndexValueText;
    private TextBlock? _workbenchNarrativeText;
    private TextBlock? _workbenchMetricsInlineText;
    private TextBlock? _workbenchActionText;
    private TextBlock? _workbenchTargetSummaryText;
    private TextBlock? _panelStateText;
    private TextBlock? _realizationStateText;
    private TextBlock? _offsetStateText;
    private TextBlock? _extentStateText;
    private TextBlock? _visualTreeStateText;
    private TextBlock? _targetStateText;
    private TextBlock? _templateListSummaryText;
    private TextBlock? _templateSelectionSummaryText;
    private TextBlock? _observationText;
    private TextBlock? _selectedTemplateItemText;

    private string _lastWorkbenchAction = "Initial state builds a production-sized direct child collection and starts near the middle target so realized-range changes are visible immediately.";
    private string _lastTemplateAction = "The templated ListBox sample uses VirtualizingStackPanel as its ItemsPanelTemplate root.";
    private bool _suppressEvents;

    public VirtualizingStackPanelView()
    {
        InitializeComponent();
        DataContext = this;

        EnsureReferences();
        PopulateChoiceControls();
        ConfigureTemplateList();
        WireEvents();
        ApplyInitialState();
        RebuildWorkbenchPanel(resetOffsets: false);
        UpdateAllReadouts();
    }

    public IEnumerable TemplateItems => _templateItems;

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        UpdateAllReadouts();
        return arranged;
    }

    private void EnsureReferences()
    {
        _orientationComboBox ??= this.FindName("OrientationComboBox") as ComboBox;
        _virtualizationModeComboBox ??= this.FindName("VirtualizationModeComboBox") as ComboBox;
        _cacheUnitComboBox ??= this.FindName("CacheUnitComboBox") as ComboBox;
        _isVirtualizingCheckBox ??= this.FindName("IsVirtualizingCheckBox") as CheckBox;
        _variableHeightsCheckBox ??= this.FindName("VariableHeightsCheckBox") as CheckBox;
        _denseCardsCheckBox ??= this.FindName("DenseCardsCheckBox") as CheckBox;
        _cacheLengthSlider ??= this.FindName("CacheLengthSlider") as Slider;
        _targetIndexSlider ??= this.FindName("TargetIndexSlider") as Slider;
        _workbenchScrollViewer ??= this.FindName("WorkbenchScrollViewer") as ScrollViewer;
        _workbenchPanel ??= this.FindName("WorkbenchPanel") as VirtualizingStackPanel;
        _templateListBox ??= this.FindName("TemplateListBox") as ListBox;
        _cacheLengthValueText ??= this.FindName("CacheLengthValueText") as TextBlock;
        _targetIndexValueText ??= this.FindName("TargetIndexValueText") as TextBlock;
        _workbenchNarrativeText ??= this.FindName("WorkbenchNarrativeText") as TextBlock;
        _workbenchMetricsInlineText ??= this.FindName("WorkbenchMetricsInlineText") as TextBlock;
        _workbenchActionText ??= this.FindName("WorkbenchActionText") as TextBlock;
        _workbenchTargetSummaryText ??= this.FindName("WorkbenchTargetSummaryText") as TextBlock;
        _panelStateText ??= this.FindName("PanelStateText") as TextBlock;
        _realizationStateText ??= this.FindName("RealizationStateText") as TextBlock;
        _offsetStateText ??= this.FindName("OffsetStateText") as TextBlock;
        _extentStateText ??= this.FindName("ExtentStateText") as TextBlock;
        _visualTreeStateText ??= this.FindName("VisualTreeStateText") as TextBlock;
        _targetStateText ??= this.FindName("TargetStateText") as TextBlock;
        _templateListSummaryText ??= this.FindName("TemplateListSummaryText") as TextBlock;
        _templateSelectionSummaryText ??= this.FindName("TemplateSelectionSummaryText") as TextBlock;
        _observationText ??= this.FindName("ObservationText") as TextBlock;
        _selectedTemplateItemText ??= this.FindName("SelectedTemplateItemText") as TextBlock;
        AttachButton("LineBackButton", OnLineBackClicked);
        AttachButton("LineForwardButton", OnLineForwardClicked);
        AttachButton("PageBackButton", OnPageBackClicked);
        AttachButton("PageForwardButton", OnPageForwardClicked);
        AttachButton("JumpStartButton", OnJumpStartClicked);
        AttachButton("JumpMiddleButton", OnJumpMiddleClicked);
        AttachButton("JumpEndButton", OnJumpEndClicked);
        AttachButton("RevealTargetButton", OnRevealTargetClicked);
        AttachButton("ListJumpStartButton", OnListJumpStartClicked);
        AttachButton("ListJumpMiddleButton", OnListJumpMiddleClicked);
        AttachButton("ListJumpEndButton", OnListJumpEndClicked);
        AttachButton("ListResetSelectionButton", OnListResetSelectionClicked);
    }

    private void PopulateChoiceControls()
    {
        if (_orientationComboBox is { Items.Count: 0 })
        {
            _orientationComboBox.Items.Add(nameof(Orientation.Vertical));
            _orientationComboBox.Items.Add(nameof(Orientation.Horizontal));
        }

        if (_virtualizationModeComboBox is { Items.Count: 0 })
        {
            _virtualizationModeComboBox.Items.Add(nameof(VirtualizationMode.Standard));
            _virtualizationModeComboBox.Items.Add(nameof(VirtualizationMode.Recycling));
        }

        if (_cacheUnitComboBox is { Items.Count: 0 })
        {
            _cacheUnitComboBox.Items.Add(nameof(VirtualizationCacheLengthUnit.Page));
            _cacheUnitComboBox.Items.Add(nameof(VirtualizationCacheLengthUnit.Item));
            _cacheUnitComboBox.Items.Add(nameof(VirtualizationCacheLengthUnit.Pixel));
        }

        if (_targetIndexSlider != null)
        {
            _targetIndexSlider.Minimum = 0f;
            _targetIndexSlider.Maximum = DirectItemCount - 1;
            _targetIndexSlider.TickFrequency = 8f;
        }
    }

    private void ConfigureTemplateList()
    {
        if (_templateListBox == null)
        {
            return;
        }

        _templateListBox.Background = new Color(12, 18, 22);
        _templateListBox.BorderBrush = new Color(59, 91, 109);
    }

    private void WireEvents()
    {
        if (_orientationComboBox != null)
        {
            _orientationComboBox.SelectionChanged += OnWorkbenchOptionChanged;
        }

        if (_virtualizationModeComboBox != null)
        {
            _virtualizationModeComboBox.SelectionChanged += OnWorkbenchOptionChanged;
        }

        if (_cacheUnitComboBox != null)
        {
            _cacheUnitComboBox.SelectionChanged += OnWorkbenchOptionChanged;
        }

        WireToggle(_isVirtualizingCheckBox);
        WireToggle(_variableHeightsCheckBox);
        WireToggle(_denseCardsCheckBox);

        if (_cacheLengthSlider != null)
        {
            _cacheLengthSlider.ValueChanged += OnCacheLengthChanged;
        }

        if (_targetIndexSlider != null)
        {
            _targetIndexSlider.ValueChanged += OnTargetIndexChanged;
        }

        if (_templateListBox != null)
        {
            _templateListBox.SelectionChanged += OnTemplateSelectionChanged;
        }
    }

    private void ApplyInitialState()
    {
        _suppressEvents = true;

        if (_orientationComboBox != null)
        {
            _orientationComboBox.SelectedItem = nameof(Orientation.Vertical);
        }

        if (_virtualizationModeComboBox != null)
        {
            _virtualizationModeComboBox.SelectedItem = nameof(VirtualizationMode.Standard);
        }

        if (_cacheUnitComboBox != null)
        {
            _cacheUnitComboBox.SelectedItem = nameof(VirtualizationCacheLengthUnit.Page);
        }

        if (_isVirtualizingCheckBox != null)
        {
            _isVirtualizingCheckBox.IsChecked = true;
        }

        if (_variableHeightsCheckBox != null)
        {
            _variableHeightsCheckBox.IsChecked = true;
        }

        if (_denseCardsCheckBox != null)
        {
            _denseCardsCheckBox.IsChecked = false;
        }

        if (_cacheLengthSlider != null)
        {
            _cacheLengthSlider.Value = 1f;
        }

        if (_targetIndexSlider != null)
        {
            _targetIndexSlider.Value = 72f;
        }

        if (_templateListBox != null && _templateItems.Count > 28)
        {
            var item = _templateItems[28];
            _templateListBox.SelectedItem = item;
            _templateListBox.ScrollIntoView(item);
        }

        _suppressEvents = false;
    }

    private void OnWorkbenchOptionChanged(object? sender, SelectionChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        ApplyWorkbenchOptions(rebuildChildren: true, resetOffsets: true);
    }

    private void OnCacheLengthChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        ApplyWorkbenchOptions(rebuildChildren: false, resetOffsets: false);
    }

    private void OnTargetIndexChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        UpdateAllReadouts();
    }

    private void OnTemplateSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        _lastTemplateAction = "Template list selection changed inside the ListBox-hosted virtualization path.";
        UpdateAllReadouts();
    }

    private void OnLineBackClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_workbenchPanel == null)
        {
            return;
        }

        ScrollWorkbenchByLine(forward: false);

        _lastWorkbenchAction = "Applied a single line-back scroll against the workbench viewport owner.";
        UpdateAllReadouts();
    }

    private void OnLineForwardClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_workbenchPanel == null)
        {
            return;
        }

        ScrollWorkbenchByLine(forward: true);

        _lastWorkbenchAction = "Applied a single line-forward scroll against the workbench viewport owner.";
        UpdateAllReadouts();
    }

    private void OnPageBackClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_workbenchPanel == null)
        {
            return;
        }

        ScrollWorkbenchByPage(forward: false);

        _lastWorkbenchAction = "Applied a page-back scroll using the current workbench viewport size.";
        UpdateAllReadouts();
    }

    private void OnPageForwardClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_workbenchPanel == null)
        {
            return;
        }

        ScrollWorkbenchByPage(forward: true);

        _lastWorkbenchAction = "Applied a page-forward scroll using the current workbench viewport size.";
        UpdateAllReadouts();
    }

    private void OnJumpStartClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        JumpWorkbenchTo(0f);
        _lastWorkbenchAction = "Jumped to the leading edge of the workbench viewport.";
        UpdateAllReadouts();
    }

    private void OnJumpMiddleClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_workbenchPanel == null)
        {
            return;
        }

        var max = GetActiveMaxOffset();
        JumpWorkbenchTo(max * 0.5f);
        _lastWorkbenchAction = "Jumped to the midpoint of the virtualized extent using the panel's current viewport metrics.";
        UpdateAllReadouts();
    }

    private void OnJumpEndClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_workbenchPanel == null)
        {
            return;
        }

        JumpWorkbenchTo(GetActiveMaxOffset());
        _lastWorkbenchAction = "Jumped to the trailing edge by setting the panel offset to its maximum scrollable value.";
        UpdateAllReadouts();
    }

    private void OnRevealTargetClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        RevealTargetItem();
    }

    private void OnListJumpStartClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        ScrollTemplateListToIndex(0, "Template list jumped to the first item via ListBox.ScrollIntoView.");
    }

    private void OnListJumpMiddleClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        ScrollTemplateListToIndex(_templateItems.Count / 2, "Template list jumped to the middle item via ListBox.ScrollIntoView.");
    }

    private void OnListJumpEndClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        ScrollTemplateListToIndex(_templateItems.Count - 1, "Template list jumped to the final item via ListBox.ScrollIntoView.");
    }

    private void OnListResetSelectionClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        ScrollTemplateListToIndex(28, "Template list selection reset to a mid-stream item so the items-panel sample opens in a useful state.");
    }

    private void ApplyWorkbenchOptions(bool rebuildChildren, bool resetOffsets)
    {
        if (_suppressEvents || _workbenchPanel == null)
        {
            UpdateAllReadouts();
            return;
        }

        _workbenchPanel.Orientation = GetSelectedOrientation();
        _workbenchPanel.IsVirtualizing = _isVirtualizingCheckBox?.IsChecked == true;
        _workbenchPanel.VirtualizationMode = GetSelectedVirtualizationMode();
        _workbenchPanel.CacheLengthUnit = GetSelectedCacheUnit();
        _workbenchPanel.CacheLength = _cacheLengthSlider?.Value ?? 1f;

        if (rebuildChildren)
        {
            RebuildWorkbenchPanel(resetOffsets);
            _lastWorkbenchAction = "Rebuilt the direct child collection to reflect the latest orientation and item-density choices.";
        }
        else
        {
            if (resetOffsets)
            {
                JumpWorkbenchTo(0f);
            }

            _lastWorkbenchAction = "Updated cache and virtualization options in place on the existing VirtualizingStackPanel instance.";
        }

        UpdateAllReadouts();
    }

    private void RebuildWorkbenchPanel(bool resetOffsets)
    {
        if (_workbenchPanel == null)
        {
            return;
        }

        while (_workbenchPanel.Children.Count > 0)
        {
            _workbenchPanel.RemoveChildAt(_workbenchPanel.Children.Count - 1);
        }

        var isHorizontal = GetSelectedOrientation() == Orientation.Horizontal;
        var useDenseCards = _denseCardsCheckBox?.IsChecked == true;
        var useVariableSizes = _variableHeightsCheckBox?.IsChecked == true;

        for (var i = 0; i < _panelItems.Count; i++)
        {
            _workbenchPanel.AddChild(BuildWorkbenchCard(_panelItems[i], i, isHorizontal, useDenseCards, useVariableSizes));
        }

        if (resetOffsets)
        {
            JumpWorkbenchTo(0f);
        }

        _workbenchPanel.InvalidateMeasure();
        _workbenchPanel.InvalidateArrange();
    }

    private void JumpWorkbenchTo(float offset)
    {
        if (_workbenchPanel == null)
        {
            return;
        }

        if (_workbenchScrollViewer != null)
        {
            if (_workbenchPanel.Orientation == Orientation.Vertical)
            {
                _workbenchScrollViewer.ScrollToVerticalOffset(offset);
            }
            else
            {
                _workbenchScrollViewer.ScrollToHorizontalOffset(offset);
            }
        }
        else
        {
            if (_workbenchPanel.Orientation == Orientation.Vertical)
            {
                _workbenchPanel.SetVerticalOffset(offset);
            }
            else
            {
                _workbenchPanel.SetHorizontalOffset(offset);
            }
        }
    }

    private void RevealTargetItem()
    {
        if (_workbenchPanel == null || _workbenchPanel.Children.Count == 0)
        {
            return;
        }

        var targetIndex = GetTargetIndex();
        if ((uint)targetIndex >= (uint)_workbenchPanel.Children.Count)
        {
            return;
        }

        if (_workbenchPanel.Children[targetIndex] is not FrameworkElement target)
        {
            return;
        }

        var relativeRect = new LayoutRect(
            target.LayoutSlot.X - _workbenchPanel.LayoutSlot.X,
            target.LayoutSlot.Y - _workbenchPanel.LayoutSlot.Y,
            target.LayoutSlot.Width,
            target.LayoutSlot.Height);

        if (_workbenchScrollViewer != null)
        {
            BringRelativeRectIntoView(relativeRect);
            _lastWorkbenchAction = $"Brought item {targetIndex} into view by aligning the inner ScrollViewer offset with the target card bounds.";
        }
        else
        {
            _workbenchPanel.MakeVisible(target, relativeRect);
            _lastWorkbenchAction = $"MakeVisible targeted item {targetIndex} so the panel computed the offset needed to bring that child fully into the viewport.";
        }

        UpdateAllReadouts();
    }

    private void ScrollTemplateListToIndex(int index, string actionText)
    {
        if (_templateListBox == null || _templateItems.Count == 0)
        {
            return;
        }

        var clampedIndex = Math.Clamp(index, 0, _templateItems.Count - 1);
        var item = _templateItems[clampedIndex];
        _templateListBox.SelectedItem = item;
        _templateListBox.ScrollIntoView(item);
        _lastTemplateAction = actionText;
        UpdateAllReadouts();
    }

    private void UpdateAllReadouts()
    {
        UpdateWorkbenchReadouts();
        UpdateTemplateReadouts();
        UpdateObservationText();
    }

    private void UpdateWorkbenchReadouts()
    {
        if (_workbenchPanel == null)
        {
            return;
        }

        var targetIndex = GetTargetIndex();
        var orientation = _workbenchPanel.Orientation == Orientation.Vertical ? "Vertical" : "Horizontal";
        var activeOffset = GetActiveWorkbenchOffset();
        var viewportPrimary = GetActiveWorkbenchViewportPrimary();
        var extentPrimary = GetActiveWorkbenchExtentPrimary();
        var percentRealized = _panelItems.Count == 0
            ? 0f
            : (_workbenchPanel.RealizedChildrenCount * 100f) / _panelItems.Count;
        var visualChildCount = CountVisualChildren(_workbenchPanel);
        var targetItem = _panelItems[Math.Clamp(targetIndex, 0, _panelItems.Count - 1)];

        SetText(
            _cacheLengthValueText,
            $"Cache length: {(_cacheLengthSlider?.Value ?? 0f):0.##} {GetSelectedCacheUnitLabel()}.");
        SetText(
            _targetIndexValueText,
            $"Target item: {targetIndex} ({targetItem.Title}). Use Make target visible to align the active viewport with that card's bounds.");
        SetText(
            _workbenchNarrativeText,
            $"{orientation} orientation with {(GetIsVirtualizingEnabled() ? "virtualization on" : "virtualization off")}, {GetSelectedVirtualizationModeLabel()} mode, and {GetSelectedCacheUnitLabel().ToLowerInvariant()} cache semantics keeps the same panel instance alive while an inner ScrollViewer owns interactive scrolling.");
        SetText(
            _workbenchMetricsInlineText,
            $"Realized range: {_workbenchPanel.FirstRealizedIndex}..{_workbenchPanel.LastRealizedIndex} across {_workbenchPanel.RealizedChildrenCount} children. Active offset: {activeOffset:0.##} of {MathF.Max(0f, extentPrimary - viewportPrimary):0.##}."
        );
        SetText(_workbenchActionText, _lastWorkbenchAction);
        SetText(
            _workbenchTargetSummaryText,
            $"Target item {targetIndex}: {targetItem.Title} | {targetItem.Status} | {targetItem.Owner} | lane {targetItem.Lane}."
        );
        SetText(
            _panelStateText,
            $"Panel instance: {orientation} | IsVirtualizing = {_workbenchPanel.IsVirtualizing} | IsVirtualizationActive = {_workbenchPanel.IsVirtualizationActive} | VirtualizationMode = {_workbenchPanel.VirtualizationMode} | Scroll host = {(_workbenchScrollViewer == null ? "direct panel" : "inner ScrollViewer")}."
        );
        SetText(
            _realizationStateText,
            $"Realization: {_workbenchPanel.FirstRealizedIndex}..{_workbenchPanel.LastRealizedIndex} | count = {_workbenchPanel.RealizedChildrenCount} of {_panelItems.Count} total ({percentRealized:0.0}%)."
        );
        SetText(
            _offsetStateText,
            $"Offsets: viewer horizontal = {_workbenchScrollViewer?.HorizontalOffset ?? 0f:0.##}, viewer vertical = {_workbenchScrollViewer?.VerticalOffset ?? 0f:0.##}, panel horizontal = {_workbenchPanel.HorizontalOffset:0.##}, panel vertical = {_workbenchPanel.VerticalOffset:0.##}, active primary = {activeOffset:0.##}."
        );
        SetText(
            _extentStateText,
            $"Extent = {_workbenchPanel.ExtentWidth:0.##} x {_workbenchPanel.ExtentHeight:0.##} | Viewport = {_workbenchPanel.ViewportWidth:0.##} x {_workbenchPanel.ViewportHeight:0.##}."
        );
        SetText(
            _visualTreeStateText,
            $"Visual children currently exposed for traversal: {visualChildCount}. This should track the realized slice while virtualization is active and revert to the full collection when virtualization is disabled."
        );
        SetText(
            _targetStateText,
            $"Target item {targetIndex} accent: {targetItem.Track}. Variable size toggle is {(GetUseVariableSizes() ? "on" : "off")}; dense cards toggle is {(GetUseDenseCards() ? "on" : "off")}."
        );
    }

    private void UpdateTemplateReadouts()
    {
        var selectedItem = _templateListBox?.SelectedItem as WorkbenchTemplateItem;
        SetText(
            _templateListSummaryText,
            $"Template list: {_templateItems.Count} items projected through ListBox with a VirtualizingStackPanel ItemsPanelTemplate. {_lastTemplateAction}"
        );
        SetText(
            _templateSelectionSummaryText,
            selectedItem == null
                ? "Selected template item: none."
                : $"Selected template item: {selectedItem.Title} | status {selectedItem.Status} | SelectedValue = {_templateListBox?.SelectedValue} | {selectedItem.OwnerLine}."
        );
        SetText(
            _selectedTemplateItemText,
            selectedItem == null
                ? "Current templated selection: none."
                : $"Current templated selection highlights how item templating stays independent from the VirtualizingStackPanel choice: {selectedItem.Title} / {selectedItem.Summary}"
        );
    }

    private void UpdateObservationText()
    {
        if (_workbenchPanel == null)
        {
            return;
        }

        var observation = !_workbenchPanel.IsVirtualizing
            ? "Virtualization is disabled, so the panel should expose the full child collection to the visual tree and realized range diagnostics stop being selective."
            : _workbenchPanel.RealizedChildrenCount <= 0
                ? "The panel has not realized a range yet. Interact with the workbench or resize the viewport to force measurement and arrangement."
                : _workbenchPanel.CacheLengthUnit switch
                {
                    VirtualizationCacheLengthUnit.Page => "Page cache length tends to widen the realized window fastest because cache grows with the viewport size instead of item size.",
                    VirtualizationCacheLengthUnit.Item => "Item cache length keeps realization pressure stable across resizes because cache is expressed in approximate item counts.",
                    _ => "Pixel cache length makes the realized window depend on actual rendered size, which is useful when item dimensions vary substantially."
                };

        SetText(_observationText, observation);
    }

    private static UIElement BuildWorkbenchCard(
        WorkbenchPanelItem item,
        int index,
        bool isHorizontal,
        bool useDenseCards,
        bool useVariableSizes)
    {
        var title = new TextBlock
        {
            Text = item.Title,
            Foreground = new Color(233, 243, 248),
            FontWeight = "SemiBold",
            TextWrapping = TextWrapping.Wrap
        };

        var summary = new TextBlock
        {
            Text = item.Summary,
            Foreground = new Color(183, 203, 217),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0f, 4f, 0f, 0f)
        };

        var owner = new TextBlock
        {
            Text = $"{item.Owner} • {item.Lane} • slot {index:000}",
            Foreground = new Color(138, 173, 194),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0f, 8f, 0f, 0f)
        };

        var badge = new Border
        {
            Background = item.AccentBackground,
            BorderBrush = item.AccentBorder,
            BorderThickness = new Thickness(1f),
            CornerRadius = new CornerRadius(12f),
            Padding = new Thickness(10f, 4f, 10f, 4f),
            Margin = new Thickness(12f, 0f, 0f, 0f),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = item.Status,
                Foreground = item.AccentForeground,
                FontWeight = "SemiBold",
                TextWrapping = TextWrapping.Wrap
            }
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1f, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var textStack = new StackPanel();
        textStack.AddChild(title);
        textStack.AddChild(summary);
        textStack.AddChild(owner);
        Grid.SetColumn(textStack, 0);
        Grid.SetColumn(badge, 1);
        grid.AddChild(textStack);
        grid.AddChild(badge);

        var padding = useDenseCards
            ? new Thickness(10f)
            : new Thickness(14f, 12f, 14f, 12f);
        var margin = isHorizontal
            ? new Thickness(0f, 0f, 12f, 0f)
            : new Thickness(0f, 0f, 0f, 10f);
        var background = item.IsFeatured
            ? new Color(19, 31, 39)
            : new Color(16, 23, 29);
        var borderBrush = item.IsFeatured
            ? new Color(89, 150, 187)
            : new Color(53, 82, 101);

        var card = new Border
        {
            Background = background,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1f),
            CornerRadius = new CornerRadius(8f),
            Padding = padding,
            Margin = margin,
            Child = grid,
            HorizontalAlignment = isHorizontal ? HorizontalAlignment.Left : HorizontalAlignment.Stretch,
            VerticalAlignment = isHorizontal ? VerticalAlignment.Stretch : VerticalAlignment.Top,
            MinWidth = isHorizontal ? (useDenseCards ? 220f : 264f) : 0f,
            Width = isHorizontal ? (useDenseCards ? 228f + (index % 3 * 18f) : 276f + (index % 4 * 22f)) : float.NaN,
            MinHeight = isHorizontal ? 0f : (useDenseCards ? 72f : 90f),
            Height = ResolveCardHeight(index, isHorizontal, useDenseCards, useVariableSizes)
        };

        return card;
    }

    private static float ResolveCardHeight(int index, bool isHorizontal, bool useDenseCards, bool useVariableSizes)
    {
        if (isHorizontal)
        {
            return useDenseCards ? 188f : 216f;
        }

        if (!useVariableSizes)
        {
            return useDenseCards ? 86f : 112f;
        }

        return useDenseCards
            ? 74f + ((index % 4) * 12f)
            : 98f + ((index % 5) * 18f);
    }

    private int GetTargetIndex()
    {
        return _targetIndexSlider == null
            ? 0
            : Math.Clamp((int)MathF.Round(_targetIndexSlider.Value), 0, DirectItemCount - 1);
    }

    private Orientation GetSelectedOrientation()
    {
        return string.Equals(_orientationComboBox?.SelectedItem?.ToString(), nameof(Orientation.Horizontal), StringComparison.Ordinal)
            ? Orientation.Horizontal
            : Orientation.Vertical;
    }

    private VirtualizationMode GetSelectedVirtualizationMode()
    {
        return string.Equals(_virtualizationModeComboBox?.SelectedItem?.ToString(), nameof(VirtualizationMode.Recycling), StringComparison.Ordinal)
            ? VirtualizationMode.Recycling
            : VirtualizationMode.Standard;
    }

    private VirtualizationCacheLengthUnit GetSelectedCacheUnit()
    {
        var selected = _cacheUnitComboBox?.SelectedItem?.ToString();
        if (string.Equals(selected, nameof(VirtualizationCacheLengthUnit.Item), StringComparison.Ordinal))
        {
            return VirtualizationCacheLengthUnit.Item;
        }

        if (string.Equals(selected, nameof(VirtualizationCacheLengthUnit.Pixel), StringComparison.Ordinal))
        {
            return VirtualizationCacheLengthUnit.Pixel;
        }

        return VirtualizationCacheLengthUnit.Page;
    }

    private string GetSelectedCacheUnitLabel()
    {
        return GetSelectedCacheUnit().ToString();
    }

    private string GetSelectedVirtualizationModeLabel()
    {
        return GetSelectedVirtualizationMode().ToString();
    }

    private bool GetIsVirtualizingEnabled()
    {
        return _isVirtualizingCheckBox?.IsChecked == true;
    }

    private bool GetUseVariableSizes()
    {
        return _variableHeightsCheckBox?.IsChecked == true;
    }

    private bool GetUseDenseCards()
    {
        return _denseCardsCheckBox?.IsChecked == true;
    }

    private float GetActiveMaxOffset()
    {
        if (_workbenchPanel == null)
        {
            return 0f;
        }

        if (_workbenchScrollViewer != null)
        {
            return _workbenchPanel.Orientation == Orientation.Vertical
                ? MathF.Max(0f, _workbenchScrollViewer.ExtentHeight - _workbenchScrollViewer.ViewportHeight)
                : MathF.Max(0f, _workbenchScrollViewer.ExtentWidth - _workbenchScrollViewer.ViewportWidth);
        }

        return _workbenchPanel.Orientation == Orientation.Vertical
            ? MathF.Max(0f, _workbenchPanel.ExtentHeight - _workbenchPanel.ViewportHeight)
            : MathF.Max(0f, _workbenchPanel.ExtentWidth - _workbenchPanel.ViewportWidth);
    }

    private float GetActiveWorkbenchOffset()
    {
        if (_workbenchPanel == null)
        {
            return 0f;
        }

        if (_workbenchScrollViewer != null)
        {
            return _workbenchPanel.Orientation == Orientation.Vertical
                ? _workbenchScrollViewer.VerticalOffset
                : _workbenchScrollViewer.HorizontalOffset;
        }

        return _workbenchPanel.Orientation == Orientation.Vertical
            ? _workbenchPanel.VerticalOffset
            : _workbenchPanel.HorizontalOffset;
    }

    private float GetActiveWorkbenchViewportPrimary()
    {
        if (_workbenchPanel == null)
        {
            return 0f;
        }

        if (_workbenchScrollViewer != null)
        {
            return _workbenchPanel.Orientation == Orientation.Vertical
                ? _workbenchScrollViewer.ViewportHeight
                : _workbenchScrollViewer.ViewportWidth;
        }

        return _workbenchPanel.Orientation == Orientation.Vertical
            ? _workbenchPanel.ViewportHeight
            : _workbenchPanel.ViewportWidth;
    }

    private float GetActiveWorkbenchExtentPrimary()
    {
        if (_workbenchPanel == null)
        {
            return 0f;
        }

        if (_workbenchScrollViewer != null)
        {
            return _workbenchPanel.Orientation == Orientation.Vertical
                ? _workbenchScrollViewer.ExtentHeight
                : _workbenchScrollViewer.ExtentWidth;
        }

        return _workbenchPanel.Orientation == Orientation.Vertical
            ? _workbenchPanel.ExtentHeight
            : _workbenchPanel.ExtentWidth;
    }

    private void ScrollWorkbenchByLine(bool forward)
    {
        if (_workbenchPanel == null)
        {
            return;
        }

        if (_workbenchScrollViewer != null)
        {
            var delta = MathF.Max(1f, _workbenchScrollViewer.LineScrollAmount);
            var signedDelta = forward ? delta : -delta;
            if (_workbenchPanel.Orientation == Orientation.Vertical)
            {
                _workbenchScrollViewer.ScrollToVerticalOffset(_workbenchScrollViewer.VerticalOffset + signedDelta);
            }
            else
            {
                _workbenchScrollViewer.ScrollToHorizontalOffset(_workbenchScrollViewer.HorizontalOffset + signedDelta);
            }

            return;
        }

        if (_workbenchPanel.Orientation == Orientation.Vertical)
        {
            if (forward)
            {
                _workbenchPanel.LineDown();
            }
            else
            {
                _workbenchPanel.LineUp();
            }
        }
        else if (forward)
        {
            _workbenchPanel.LineRight();
        }
        else
        {
            _workbenchPanel.LineLeft();
        }
    }

    private void ScrollWorkbenchByPage(bool forward)
    {
        if (_workbenchPanel == null)
        {
            return;
        }

        var delta = MathF.Max(1f, GetActiveWorkbenchViewportPrimary());
        var signedDelta = forward ? delta : -delta;
        JumpWorkbenchTo(GetActiveWorkbenchOffset() + signedDelta);
    }

    private void BringRelativeRectIntoView(LayoutRect relativeRect)
    {
        if (_workbenchPanel == null || _workbenchScrollViewer == null)
        {
            return;
        }

        if (_workbenchPanel.Orientation == Orientation.Vertical)
        {
            var viewportTop = _workbenchScrollViewer.VerticalOffset;
            var viewportBottom = viewportTop + _workbenchScrollViewer.ViewportHeight;
            var itemTop = relativeRect.Y;
            var itemBottom = itemTop + relativeRect.Height;
            var next = viewportTop;

            if (itemTop < viewportTop)
            {
                next = itemTop;
            }
            else if (itemBottom > viewportBottom)
            {
                next += itemBottom - viewportBottom;
            }

            _workbenchScrollViewer.ScrollToVerticalOffset(next);
            return;
        }

        var viewportLeft = _workbenchScrollViewer.HorizontalOffset;
        var viewportRight = viewportLeft + _workbenchScrollViewer.ViewportWidth;
        var itemLeft = relativeRect.X;
        var itemRight = itemLeft + relativeRect.Width;
        var nextHorizontal = viewportLeft;

        if (itemLeft < viewportLeft)
        {
            nextHorizontal = itemLeft;
        }
        else if (itemRight > viewportRight)
        {
            nextHorizontal += itemRight - viewportRight;
        }

        _workbenchScrollViewer.ScrollToHorizontalOffset(nextHorizontal);
    }

    private static int CountVisualChildren(UIElement element)
    {
        var count = 0;
        foreach (var _ in element.GetVisualChildren())
        {
            count++;
        }

        return count;
    }

    private static void SetText(TextBlock? target, string text)
    {
        if (target != null && !string.Equals(target.Text, text, StringComparison.Ordinal))
        {
            target.Text = text;
        }
    }

    private void WireToggle(CheckBox? checkBox)
    {
        if (checkBox == null)
        {
            return;
        }

        checkBox.Checked += OnWorkbenchToggleChanged;
        checkBox.Unchecked += OnWorkbenchToggleChanged;
    }

    private void OnWorkbenchToggleChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        ApplyWorkbenchOptions(rebuildChildren: true, resetOffsets: false);
    }

    private void AttachButton(string name, EventHandler<RoutedSimpleEventArgs> handler)
    {
        if (this.FindName(name) is Button button)
        {
            button.Click += handler;
        }
    }

    private static List<WorkbenchPanelItem> CreatePanelItems()
    {
        var items = new List<WorkbenchPanelItem>(DirectItemCount);
        var lanes = new[] { "Input", "Layout", "Styling", "Automation", "Accessibility" };
        var owners = new[] { "Avery", "Morgan", "Riley", "Jordan", "Casey", "Harper" };
        var statuses = new[] { "Ready", "Active", "Review", "Queued" };
        var tracks = new[] { "Viewport", "Cache", "Measure", "Arrange", "Selection" };

        for (var i = 0; i < DirectItemCount; i++)
        {
            var lane = lanes[i % lanes.Length];
            var owner = owners[i % owners.Length];
            var status = statuses[i % statuses.Length];
            var track = tracks[i % tracks.Length];
            var featured = i % 9 == 0 || i % 11 == 0;

            items.Add(new WorkbenchPanelItem(
                i,
                $"Workbench card {i:000}",
                lane,
                owner,
                status,
                track,
                featured,
                $"Demonstrates how heterogeneous child sizes, richer visuals, and mixed status badges still cooperate with virtualization and scrolling decisions at item {i:000}.",
                ResolveAccentBackground(track),
                ResolveAccentBorder(track),
                ResolveAccentForeground(track)));
        }

        return items;
    }

    private static ObservableCollection<WorkbenchTemplateItem> CreateTemplateItems()
    {
        var items = new ObservableCollection<WorkbenchTemplateItem>();
        var streams = new[] { "Release lane", "Design review", "Perf pass", "Automation backlog" };
        var statuses = new[] { "Ready", "In Progress", "Review", "Blocked" };
        var owners = new[] { "Framework", "Input", "Layout", "QA" };

        for (var i = 0; i < 72; i++)
        {
            items.Add(new WorkbenchTemplateItem(
                500 + i,
                $"Template work item {i:00}",
                $"This templated row keeps the consumer-facing API close to WPF: ListBox for selection and a VirtualizingStackPanel supplied through ItemsPanelTemplate.",
                statuses[i % statuses.Length],
                $"Owner: {owners[i % owners.Length]} • stream: {streams[i % streams.Length]}",
                streams[i % streams.Length]));
        }

        return items;
    }

    private static Color ResolveAccentBackground(string track)
    {
        return track switch
        {
            "Viewport" => new Color(28, 54, 64),
            "Cache" => new Color(46, 57, 32),
            "Measure" => new Color(57, 39, 28),
            "Arrange" => new Color(48, 33, 58),
            _ => new Color(32, 50, 42)
        };
    }

    private static Color ResolveAccentBorder(string track)
    {
        return track switch
        {
            "Viewport" => new Color(105, 177, 201),
            "Cache" => new Color(145, 181, 101),
            "Measure" => new Color(206, 149, 94),
            "Arrange" => new Color(176, 132, 212),
            _ => new Color(102, 192, 151)
        };
    }

    private static Color ResolveAccentForeground(string track)
    {
        return track switch
        {
            "Viewport" => new Color(219, 243, 250),
            "Cache" => new Color(228, 244, 214),
            "Measure" => new Color(251, 233, 213),
            "Arrange" => new Color(239, 227, 251),
            _ => new Color(222, 248, 237)
        };
    }

    private readonly record struct WorkbenchPanelItem(
        int Id,
        string Title,
        string Lane,
        string Owner,
        string Status,
        string Track,
        bool IsFeatured,
        string Summary,
        Color AccentBackground,
        Color AccentBorder,
        Color AccentForeground);

    private sealed record WorkbenchTemplateItem(
        int Id,
        string Title,
        string Summary,
        string Status,
        string OwnerLine,
        string Stream);
}




