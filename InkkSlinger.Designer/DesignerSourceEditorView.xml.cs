using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using InkkSlinger;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger.Designer;

public partial class DesignerSourceEditorView : UserControl
{
    private const float CompletionPopupVerticalOffset = 4f;
    private const float CompletionPopupMaxHeight = 260f;
    private const float CompletionPopupMinWidth = 180f;
    private const float CompletionPopupMaxWidth = 420f;

    public static readonly DependencyProperty SourceTextProperty =
        DependencyProperty.Register(
            nameof(SourceText),
            typeof(string),
            typeof(DesignerSourceEditorView),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is DesignerSourceEditorView view)
                    {
                        view.OnSourceTextChanged(args.NewValue as string ?? string.Empty);
                    }
                }));

    public static readonly DependencyProperty NavigationRequestProperty =
        DependencyProperty.Register(
            nameof(NavigationRequest),
            typeof(DesignerSourceNavigationRequest),
            typeof(DesignerSourceEditorView),
            new FrameworkPropertyMetadata(
                null,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is DesignerSourceEditorView view &&
                        args.NewValue is DesignerSourceNavigationRequest request)
                    {
                        view.NavigateToLine(request.LineNumber);
                    }
                }));

    public static readonly DependencyProperty SourcePropertyInspectorItemsProperty =
        DependencyProperty.Register(
            nameof(SourcePropertyInspectorItems),
            typeof(IEnumerable),
            typeof(DesignerSourceEditorView),
            new FrameworkPropertyMetadata(Array.Empty<object>()));

    private bool _suppressSourceEditorChanges;
    private int _cachedSourceLineCount = 1;
    private int _lastRenderedSourceLineCount = -1;
    private int _lastRenderedSourceFirstVisibleLine = -1;
    private int _lastRenderedSourceVisibleLineCount = -1;
    private float _lastRenderedSourceLineOffset = float.NaN;
    private float _lastRenderedSourceLineHeight = float.NaN;
    private float _lastObservedViewportHorizontalOffset = float.NaN;
    private float _lastObservedViewportVerticalOffset = float.NaN;
    private float _lastObservedViewportWidth = float.NaN;
    private float _lastObservedViewportHeight = float.NaN;
    private IReadOnlyList<DesignerControlCompletionItem> _completionItems = Array.Empty<DesignerControlCompletionItem>();
    private IReadOnlyList<string> _completionItemNames = Array.Empty<string>();
    private IReadOnlyList<DesignerSourceInspectableProperty> _currentSourceInspectorProperties = Array.Empty<DesignerSourceInspectableProperty>();
    private CompletionContext? _completionContext;
    private bool _suppressCompletionListSelectionChanged;
    private LayoutRect _lastCompletionCaretBounds;
    private readonly Dictionary<string, DesignerSourceInspectorPropertyItem> _sourcePropertyItemsByName = new(StringComparer.Ordinal);
    private Type? _currentSourceInspectorControlType;
    private DesignerSourceTagSelection? _currentSourceTagSelection;
    private bool _suppressSourceInspectorApply;
    private bool _suppressSourceInspectorFilterTextChanged;

    public DesignerSourceEditorView()
    {
        InitializeComponent();
        if (Content is Panel rootPanel)
        {
            _ = rootPanel.RemoveChild(CompletionPopup);
        }

        CompletionPopup.Closed += OnCompletionPopupClosed;
        CompletionListBox.SelectionChanged += OnCompletionListSelectionChanged;
        CompletionListBox.AddHandler<MouseRoutedEventArgs>(UIElement.MouseUpEvent, OnCompletionListMouseUp, handledEventsToo: true);
        SourceEditor.AddHandler<KeyRoutedEventArgs>(UIElement.KeyDownEvent, OnSourceEditorKeyDown, handledEventsToo: true);
        SourceEditor.AddHandler<FocusChangedRoutedEventArgs>(UIElement.LostFocusEvent, OnSourceEditorLostFocus, handledEventsToo: true);
        SourceEditor.SelectionChanged += OnSourceEditorSelectionChanged;
        LoadDocumentIntoEditor(SourceText);
        UpdateSourceLineNumberGutter(force: true);
        RefreshSourcePropertyInspector();
    }

    public string SourceText
    {
        get => GetValue<string>(SourceTextProperty) ?? string.Empty;
        set => SetValue(SourceTextProperty, value ?? string.Empty);
    }

    public DesignerSourceNavigationRequest? NavigationRequest
    {
        get => GetValue<DesignerSourceNavigationRequest>(NavigationRequestProperty);
        set => SetValue(NavigationRequestProperty, value);
    }

    public RichTextBox Editor => SourceEditor;

    public Border LineNumberBorder => ((DesignerSourceLineNumberGutterView)SourceLineNumberGutter).BorderHost;

    public DesignerSourceLineNumberPresenter LineNumberPanel => ((DesignerSourceLineNumberGutterView)SourceLineNumberGutter).Presenter;

    public IEnumerable SourcePropertyInspectorItems
    {
        get => GetValue<IEnumerable>(SourcePropertyInspectorItemsProperty) ?? Array.Empty<object>();
        private set => SetValue(SourcePropertyInspectorItemsProperty, value ?? Array.Empty<object>());
    }

    public bool IsControlCompletionOpen => CompletionPopup.IsOpen;

    public IReadOnlyList<string> ControlCompletionItems => _completionItemNames;

    public int ControlCompletionSelectedIndex => CompletionListBox.SelectedIndex;

    public LayoutRect ControlCompletionBounds => CompletionPopup.LayoutSlot;

    public bool TryOpenControlCompletion()
    {
        return RefreshCompletionPopup(openIfPossible: true);
    }

    public bool TryAcceptControlCompletion()
    {
        return TryAcceptSelectedCompletion();
    }

    public void DismissControlCompletion()
    {
        DismissCompletionPopup();
    }

    private void OnSourceEditorTextChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;

        if (_suppressSourceEditorChanges)
        {
            return;
        }

        var currentText = DocumentEditing.GetText(SourceEditor.Document);
        if (!string.Equals(SourceText, currentText, StringComparison.Ordinal))
        {
            SourceText = currentText;
        }

        UpdateCachedSourceLineCount(currentText);
        UpdateSourceLineNumberGutter(force: true);
        RefreshSourcePropertyInspector();
        if (IsControlCompletionOpen)
        {
            _ = RefreshCompletionPopup(openIfPossible: false);
        }
    }

    private void OnSourceEditorLayoutUpdated(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;

        var viewportWidth = SourceEditor.ViewportWidth;
        var viewportHeight = SourceEditor.ViewportHeight;
        var viewportSizeChanged =
            Math.Abs(viewportWidth - _lastObservedViewportWidth) > 0.01f ||
            Math.Abs(viewportHeight - _lastObservedViewportHeight) > 0.01f;
        _lastObservedViewportWidth = viewportWidth;
        _lastObservedViewportHeight = viewportHeight;

        if (!viewportSizeChanged)
        {
            return;
        }

        UpdateSourceLineNumberGutter(force: false);
        if (IsControlCompletionOpen)
        {
            UpdateCompletionPopupPlacement();
        }
    }

    private void OnSourceEditorViewportChanged(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;

        var horizontalOffset = SourceEditor.HorizontalOffset;
        var verticalOffset = SourceEditor.VerticalOffset;
        var viewportWidth = SourceEditor.ViewportWidth;
        var viewportHeight = SourceEditor.ViewportHeight;
        var scrollOffsetChanged =
            Math.Abs(horizontalOffset - _lastObservedViewportHorizontalOffset) > 0.01f ||
            Math.Abs(verticalOffset - _lastObservedViewportVerticalOffset) > 0.01f;
        var viewportSizeChanged =
            Math.Abs(viewportWidth - _lastObservedViewportWidth) > 0.01f ||
            Math.Abs(viewportHeight - _lastObservedViewportHeight) > 0.01f;

        _lastObservedViewportHorizontalOffset = horizontalOffset;
        _lastObservedViewportVerticalOffset = verticalOffset;
        _lastObservedViewportWidth = viewportWidth;
        _lastObservedViewportHeight = viewportHeight;

        if (scrollOffsetChanged || viewportSizeChanged)
        {
            UpdateSourceLineNumberGutter(force: false);
        }

        if (IsControlCompletionOpen)
        {
            UpdateCompletionPopupPlacement();
        }
    }

    private void OnSourceTextChanged(string newText)
    {
        if (_suppressSourceEditorChanges)
        {
            return;
        }

        var documentText = DocumentEditing.GetText(SourceEditor.Document);
        if (string.Equals(documentText, newText, StringComparison.Ordinal))
        {
            return;
        }

        DismissCompletionPopup();
        LoadDocumentIntoEditor(newText);
        RefreshSourcePropertyInspector();
    }

    private void LoadDocumentIntoEditor(string? text)
    {
        DismissCompletionPopup();
        var normalizedText = text ?? string.Empty;
        var selectionStart = SourceEditor.SelectionStart;
        var selectionLength = SourceEditor.SelectionLength;
        var horizontalOffset = SourceEditor.HorizontalOffset;
        var verticalOffset = SourceEditor.VerticalOffset;

        _suppressSourceEditorChanges = true;
        try
        {
            DesignerXmlSyntaxHighlighter.PopulateDocument(SourceEditor.Document, normalizedText);
            UpdateCachedSourceLineCount(normalizedText);

            var updatedTextLength = DocumentEditing.GetText(SourceEditor.Document).Length;
            var clampedSelectionStart = Math.Clamp(selectionStart, 0, updatedTextLength);
            var clampedSelectionLength = Math.Clamp(selectionLength, 0, updatedTextLength - clampedSelectionStart);
            SourceEditor.Select(clampedSelectionStart, clampedSelectionLength);
            SourceEditor.ScrollToHorizontalOffset(horizontalOffset);
            SourceEditor.ScrollToVerticalOffset(verticalOffset);
            UpdateSourceLineNumberGutter(force: true);
        }
        finally
        {
            _suppressSourceEditorChanges = false;
        }
    }

    private void NavigateToLine(int oneBasedLineNumber)
    {
        var sourceText = DocumentEditing.GetText(SourceEditor.Document);
        if (!TryGetLineSelectionRange(sourceText, oneBasedLineNumber, out var selectionStart, out var selectionLength))
        {
            return;
        }

        FocusManager.SetFocus(SourceEditor);
        SourceEditor.Select(selectionStart, selectionLength);
        SourceEditor.ScrollToHorizontalOffset(0f);

        UpdateCachedSourceLineCount(sourceText);
        var lineHeight = EstimateSourceEditorLineHeight(_cachedSourceLineCount);
        var desiredVerticalOffset = Math.Max(0f, ((oneBasedLineNumber - 1) * lineHeight) - (SourceEditor.ViewportHeight * 0.35f));
        SourceEditor.ScrollToVerticalOffset(desiredVerticalOffset);
        UpdateSourceLineNumberGutter(force: true);
        RefreshSourcePropertyInspector();
    }

    private void OnSourceEditorKeyDown(object? sender, KeyRoutedEventArgs args)
    {
        _ = sender;

        if (args.Key == Keys.Space && args.Modifiers == ModifierKeys.Control)
        {
            args.Handled = RefreshCompletionPopup(openIfPossible: true) || IsControlCompletionOpen;
            return;
        }

        if (!IsControlCompletionOpen)
        {
            return;
        }

        switch (args.Key)
        {
            case Keys.Escape:
                DismissCompletionPopup();
                args.Handled = true;
                return;
            case Keys.Up:
                MoveCompletionSelection(-1);
                args.Handled = true;
                return;
            case Keys.Down:
                MoveCompletionSelection(1);
                args.Handled = true;
                return;
            case Keys.Home:
                SetCompletionSelection(0);
                args.Handled = true;
                return;
            case Keys.End:
                SetCompletionSelection(_completionItems.Count - 1);
                args.Handled = true;
                return;
            case Keys.PageUp:
                MoveCompletionSelection(-8);
                args.Handled = true;
                return;
            case Keys.PageDown:
                MoveCompletionSelection(8);
                args.Handled = true;
                return;
            case Keys.Enter:
            case Keys.Tab:
                if (TryAcceptSelectedCompletion())
                {
                    args.Handled = true;
                }

                return;
            default:
                if (ShouldDismissCompletionForKey(args.Key, args.Modifiers))
                {
                    DismissCompletionPopup();
                }

                return;
        }
    }

    private void OnSourceEditorLostFocus(object? sender, FocusChangedRoutedEventArgs args)
    {
        _ = sender;
        if (!IsControlCompletionOpen)
        {
            return;
        }

        if (args.NewFocus != null && IsWithinCompletionPopup(args.NewFocus))
        {
            return;
        }

        DismissCompletionPopup();
    }

    private void RefreshSourcePropertyInspector(string? activePropertyName = null, string? activeEditorText = null)
    {
        var previousVerticalOffset = SourcePropertyInspectorScrollViewer.VerticalOffset;
        var sourceText = DocumentEditing.GetText(SourceEditor.Document);
        if (!DesignerSourcePropertyInspector.TryResolveTagSelection(sourceText, SourceEditor.SelectionStart, out var selection))
        {
            _currentSourceTagSelection = null;
            _currentSourceInspectorControlType = null;
            _currentSourceInspectorProperties = Array.Empty<DesignerSourceInspectableProperty>();
            ClearSourcePropertyInspectorEditorState();
            SourcePropertyInspectorHeaderText.Text = "Select a control tag in the source editor.";
            SourcePropertyInspectorSummaryText.Text = "Changes update the XML source. Press F5 to refresh the preview.";
            SourcePropertyInspectorEmptyState.Text = "Place the caret inside a control start tag such as <Button /> or <Button> to edit its properties.";
            SourcePropertyInspectorEmptyState.Visibility = Visibility.Visible;
            SourcePropertyInspectorFilterBorder.Visibility = Visibility.Collapsed;
            SourcePropertyInspectorScrollViewer.Visibility = Visibility.Collapsed;
            return;
        }

        var controlType = DesignerSourcePropertyInspector.ResolveControlType(selection.ElementName);
        if (controlType == null)
        {
            _currentSourceTagSelection = selection;
            _currentSourceInspectorControlType = null;
            _currentSourceInspectorProperties = Array.Empty<DesignerSourceInspectableProperty>();
            ClearSourcePropertyInspectorEditorState();
            SourcePropertyInspectorHeaderText.Text = selection.ElementName;
            SourcePropertyInspectorSummaryText.Text = "The source tag was found, but no matching control type is registered for editing.";
            SourcePropertyInspectorEmptyState.Text = "This tag cannot be edited through the source property inspector yet.";
            SourcePropertyInspectorEmptyState.Visibility = Visibility.Visible;
            SourcePropertyInspectorFilterBorder.Visibility = Visibility.Collapsed;
            SourcePropertyInspectorScrollViewer.Visibility = Visibility.Collapsed;
            return;
        }

        var properties = DesignerSourcePropertyInspector.GetInspectableProperties(controlType, selection);
        var shouldRebuild =
            _currentSourceInspectorControlType != controlType ||
            _sourcePropertyItemsByName.Count != properties.Count ||
            properties.Any(property => !_sourcePropertyItemsByName.ContainsKey(property.Name));

        _currentSourceTagSelection = selection;
        _currentSourceInspectorControlType = controlType;
        _currentSourceInspectorProperties = properties;
        SourcePropertyInspectorHeaderText.Text = selection.ElementName;
        SourcePropertyInspectorSummaryText.Text = selection.IsSelfClosing
            ? "Editing a self-closing start tag. Source updates immediately. Press F5 to refresh the preview."
            : "Editing an opening start tag. Source updates immediately. Press F5 to refresh the preview.";
        SourcePropertyInspectorFilterBorder.Visibility = Visibility.Visible;

        if (shouldRebuild)
        {
            RebuildSourcePropertyEditorRows(properties, selection);
        }

        UpdateSourcePropertyEditorValues(selection, properties, activePropertyName, activeEditorText);
        ApplySourcePropertyInspectorFilter(selection, properties);
        SourcePropertyInspectorScrollViewer.ScrollToVerticalOffset(previousVerticalOffset);
    }

    private void RebuildSourcePropertyEditorRows(
        IReadOnlyList<DesignerSourceInspectableProperty> properties,
        DesignerSourceTagSelection selection)
    {
        ClearSourcePropertyInspectorEditorState();

        var items = new List<DesignerSourceInspectorPropertyItem>(properties.Count);
        foreach (var property in properties)
        {
            var item = new DesignerSourceInspectorPropertyItem(property);
            item.PropertyChanged += OnSourceInspectorPropertyItemPropertyChanged;
            _sourcePropertyItemsByName[property.Name] = item;
            items.Add(item);
        }

        SourcePropertyInspectorItems = items;

        UpdateSourcePropertyEditorValues(selection, properties, activePropertyName: null, activeEditorText: null);
    }

    private void UpdateSourcePropertyEditorValues(
        DesignerSourceTagSelection selection,
        IReadOnlyList<DesignerSourceInspectableProperty> properties,
        string? activePropertyName,
        string? activeEditorText)
    {
        _suppressSourceInspectorApply = true;
        try
        {
            foreach (var property in properties)
            {
                if (!_sourcePropertyItemsByName.TryGetValue(property.Name, out var item))
                {
                    continue;
                }

                var currentValue = selection.TryGetAttribute(property.Name, out var attribute)
                    ? attribute.Value
                    : string.Empty;
                item.DescriptionText = BuildSourcePropertyDescription(property, currentValue);
                if (property.EditorKind == DesignerSourcePropertyEditorKind.Text &&
                    string.Equals(activePropertyName, property.Name, StringComparison.Ordinal))
                {
                    if (activeEditorText != null && !string.Equals(item.EditorText, activeEditorText, StringComparison.Ordinal))
                    {
                        item.EditorText = activeEditorText;
                    }

                    continue;
                }

                switch (property.EditorKind)
                {
                    case DesignerSourcePropertyEditorKind.Color:
                        UpdateSourcePropertyColorEditorValue(item, property, currentValue);
                        break;
                    case DesignerSourcePropertyEditorKind.Choice:
                        UpdateSourcePropertyChoiceEditorValue(item, property, currentValue);
                        break;
                    default:
                        if (!string.Equals(item.EditorText, currentValue, StringComparison.Ordinal))
                        {
                            item.EditorText = currentValue;
                        }
                        break;
                }
            }
        }
        finally
        {
            _suppressSourceInspectorApply = false;
        }
    }

    private static string BuildSourcePropertyDescription(DesignerSourceInspectableProperty property, string currentValue)
    {
        if (!string.IsNullOrEmpty(currentValue))
        {
            return string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{property.TypeName} • set in source • default {property.DefaultValueDisplay}");
        }

        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{property.TypeName} • not set in source • default {property.DefaultValueDisplay}");
    }

    private void OnSourcePropertyInspectorFilterTextChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_suppressSourceInspectorFilterTextChanged || _currentSourceTagSelection is not DesignerSourceTagSelection selection)
        {
            return;
        }

        ApplySourcePropertyInspectorFilter(selection, _currentSourceInspectorProperties);
        SourcePropertyInspectorScrollViewer.ScrollToVerticalOffset(0f);
    }

    private void ApplySourcePropertyInspectorFilter(
        DesignerSourceTagSelection selection,
        IReadOnlyList<DesignerSourceInspectableProperty> properties)
    {
        var filterText = SourcePropertyInspectorFilterTextBox.Text ?? string.Empty;
        var hasVisibleProperties = false;
        foreach (var property in properties)
        {
            if (!_sourcePropertyItemsByName.TryGetValue(property.Name, out var item))
            {
                continue;
            }

            var currentValue = selection.TryGetAttribute(property.Name, out var attribute)
                ? attribute.Value
                : string.Empty;
            var isVisible = MatchesSourcePropertyInspectorFilter(property, currentValue, filterText);
            item.RowVisibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            hasVisibleProperties |= isVisible;
        }

        if (hasVisibleProperties)
        {
            SourcePropertyInspectorEmptyState.Visibility = Visibility.Collapsed;
            SourcePropertyInspectorScrollViewer.Visibility = Visibility.Visible;
            return;
        }

        SourcePropertyInspectorEmptyState.Text = string.IsNullOrWhiteSpace(filterText)
            ? "No editable properties are available for this control."
            : $"No properties match '{filterText.Trim()}'.";
        SourcePropertyInspectorEmptyState.Visibility = Visibility.Visible;
        SourcePropertyInspectorScrollViewer.Visibility = Visibility.Collapsed;
    }

    private static bool MatchesSourcePropertyInspectorFilter(
        DesignerSourceInspectableProperty property,
        string currentValue,
        string filterText)
    {
        if (string.IsNullOrWhiteSpace(filterText))
        {
            return true;
        }

        var trimmedFilter = filterText.Trim();
        return property.Name.Contains(trimmedFilter, StringComparison.OrdinalIgnoreCase) ||
               property.TypeName.Contains(trimmedFilter, StringComparison.OrdinalIgnoreCase) ||
               property.DefaultValueDisplay.Contains(trimmedFilter, StringComparison.OrdinalIgnoreCase) ||
               (!string.IsNullOrEmpty(currentValue) && currentValue.Contains(trimmedFilter, StringComparison.OrdinalIgnoreCase));
    }

    private void OnSourcePropertyEditorTextChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = args;
        if (_suppressSourceInspectorApply ||
            sender is not TextBox editor ||
            editor.DataContext is not DesignerSourceInspectorPropertyItem item)
        {
            return;
        }

        if (!TryApplySourceInspectorEdit(item.Name, editor.Text))
        {
            return;
        }

        RefreshSourcePropertyInspector(item.Name, editor.Text);
    }

    private void OnSourcePropertyChoiceSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        _ = args;
        if (_suppressSourceInspectorApply ||
            sender is not ComboBox comboBox ||
            comboBox.DataContext is not DesignerSourceInspectorPropertyItem item)
        {
            return;
        }

        var selectedValue = comboBox.SelectedItem as string;
        if (!TryApplySourceInspectorEdit(item.Name, selectedValue))
        {
            return;
        }

        RefreshSourcePropertyInspector(item.Name, selectedValue);
    }

    private void ApplySourceColorPropertyEdit(string propertyName, Color color)
    {
        var propertyValue = DesignerSourcePropertyInspector.FormatColorValue(color);
        if (!TryApplySourceInspectorEdit(propertyName, propertyValue))
        {
            return;
        }

        RefreshSourcePropertyInspector(propertyName, propertyValue);
    }

    private bool TryApplySourceInspectorEdit(string propertyName, string? propertyValue)
    {
        var sourceText = DocumentEditing.GetText(SourceEditor.Document);
        if (!DesignerSourcePropertyInspector.TryResolveTagSelection(sourceText, SourceEditor.SelectionStart, out var selection))
        {
            return false;
        }

        if (!DesignerSourcePropertyInspector.TryApplyPropertyEdit(
                sourceText,
                selection,
                propertyName,
                propertyValue,
                out var updatedText,
                out var updatedAnchorIndex) ||
            string.Equals(updatedText, sourceText, StringComparison.Ordinal))
        {
            return false;
        }

        SourceText = updatedText;
        var clampedAnchorIndex = Math.Clamp(updatedAnchorIndex, 0, DocumentEditing.GetText(SourceEditor.Document).Length);
        SourceEditor.Select(clampedAnchorIndex, 0);
        return true;
    }

    private void ClearSourcePropertyInspectorRows()
    {
        SourcePropertyInspectorItems = Array.Empty<DesignerSourceInspectorPropertyItem>();
    }

    private void ClearSourcePropertyInspectorEditorState()
    {
        foreach (var editor in EnumerateVisualDescendants<DesignerSourceColorPropertyEditor>(SourcePropertyInspectorPropertiesHost))
        {
            editor.ClosePopup();
        }

        foreach (var item in _sourcePropertyItemsByName.Values)
        {
            item.PropertyChanged -= OnSourceInspectorPropertyItemPropertyChanged;
        }

        _sourcePropertyItemsByName.Clear();
        ClearSourcePropertyInspectorRows();
    }

    private void OnSourceInspectorPropertyItemPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_suppressSourceInspectorApply ||
            sender is not DesignerSourceInspectorPropertyItem item ||
            !string.Equals(args.PropertyName, nameof(DesignerSourceInspectorPropertyItem.SelectedColor), StringComparison.Ordinal))
        {
            return;
        }

        ApplySourceColorPropertyEdit(item.Name, item.SelectedColor);
    }

    private static void UpdateSourcePropertyColorEditorValue(
        DesignerSourceInspectorPropertyItem item,
        DesignerSourceInspectableProperty property,
        string currentValue)
    {
        var displayText = string.IsNullOrEmpty(currentValue)
            ? property.DefaultValueDisplay
            : currentValue;

        if (!DesignerSourcePropertyInspector.TryParseColorValue(currentValue, out var color) &&
            !DesignerSourcePropertyInspector.TryParseColorValue(property.DefaultValueDisplay, out color))
        {
            color = Color.Transparent;
        }

        item.SelectedColor = color;
        item.ColorDisplayText = displayText;
    }

    private static void UpdateSourcePropertyChoiceEditorValue(
        DesignerSourceInspectorPropertyItem item,
        DesignerSourceInspectableProperty property,
        string currentValue)
    {
        if (string.IsNullOrEmpty(currentValue))
        {
            if (item.SelectedChoice != null)
            {
                item.SelectedChoice = null;
            }

            return;
        }

        var selectedChoice = property.ChoiceValues.FirstOrDefault(
            choice => string.Equals(choice, currentValue, StringComparison.OrdinalIgnoreCase));
        if (selectedChoice == null)
        {
            if (item.SelectedChoice != null)
            {
                item.SelectedChoice = null;
            }

            return;
        }

        if (!string.Equals(item.SelectedChoice, selectedChoice, StringComparison.Ordinal))
        {
            item.SelectedChoice = selectedChoice;
        }
    }

    private bool RefreshCompletionPopup(bool openIfPossible)
    {
        var sourceText = DocumentEditing.GetText(SourceEditor.Document);
        if (!TryBuildCompletionContext(sourceText, SourceEditor.SelectionStart, SourceEditor.SelectionLength, out var context))
        {
            DismissCompletionPopup();
            return false;
        }

        var items = DesignerControlCompletionCatalog.GetItems(context.Prefix);
        if (items.Count == 0)
        {
            DismissCompletionPopup();
            return false;
        }

        _completionContext = context;
        _completionItems = items;
        var itemNames = items.Select(static item => item.ElementName).ToArray();
        if (!AreCompletionNamesEqual(_completionItemNames, itemNames))
        {
            _completionItemNames = itemNames;
            RebuildCompletionList();
        }
        else if (CompletionListBox.SelectedIndex < 0 && _completionItems.Count > 0)
        {
            SetCompletionSelection(0);
        }

        if (!CompletionPopup.IsOpen)
        {
            if (!openIfPossible)
            {
                return false;
            }

            var host = ResolveOverlayHost(this);
            if (host == null)
            {
                DismissCompletionPopup();
                return false;
            }

            CompletionPopup.Width = float.NaN;
            CompletionPopup.Height = float.NaN;
            CompletionPopup.MaxHeight = CompletionPopupMaxHeight;
            CompletionPopup.Open(host);
        }

        UpdateCompletionPopupPlacement();
        FocusManager.SetFocus(SourceEditor);
        return true;
    }

    private void RebuildCompletionList()
    {
        _suppressCompletionListSelectionChanged = true;
        try
        {
            CompletionListBox.ItemsSource = _completionItems;
            CompletionListBox.SelectedIndex = _completionItems.Count > 0 ? 0 : -1;
        }
        finally
        {
            _suppressCompletionListSelectionChanged = false;
        }
    }

    private void UpdateCompletionPopupPlacement()
    {
        if (!CompletionPopup.IsOpen)
        {
            return;
        }

        if (!SourceEditor.TryGetCaretBounds(out var caretBounds))
        {
            DismissCompletionPopup();
            return;
        }

        if (AreRectsEffectivelyEqual(_lastCompletionCaretBounds, caretBounds))
        {
            return;
        }

        if (!CompletionPopup.TrySetRootSpacePosition(caretBounds.X, caretBounds.Y + caretBounds.Height + CompletionPopupVerticalOffset))
        {
            DismissCompletionPopup();
            return;
        }

        _lastCompletionCaretBounds = caretBounds;
    }

    private void DismissCompletionPopup()
    {
        if (CompletionPopup.IsOpen)
        {
            CompletionPopup.Close();
        }

        ResetCompletionState();
    }

    private void OnCompletionPopupClosed(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        ResetCompletionState();
    }

    private void ResetCompletionState()
    {
        _completionContext = null;
        _completionItems = Array.Empty<DesignerControlCompletionItem>();
        _completionItemNames = Array.Empty<string>();
        _lastCompletionCaretBounds = default;

        _suppressCompletionListSelectionChanged = true;
        try
        {
            CompletionListBox.ItemsSource = null;
            CompletionListBox.SelectedIndex = -1;
        }
        finally
        {
            _suppressCompletionListSelectionChanged = false;
        }
    }

    private void MoveCompletionSelection(int delta)
    {
        if (_completionItems.Count == 0)
        {
            return;
        }

        var selectedIndex = CompletionListBox.SelectedIndex;
        if (selectedIndex < 0)
        {
            selectedIndex = 0;
        }

        SetCompletionSelection(Math.Clamp(selectedIndex + delta, 0, _completionItems.Count - 1));
    }

    private void SetCompletionSelection(int index)
    {
        if (_completionItems.Count == 0)
        {
            return;
        }

        var clamped = Math.Clamp(index, 0, _completionItems.Count - 1);
        _suppressCompletionListSelectionChanged = true;
        try
        {
            CompletionListBox.SelectedIndex = clamped;
            CompletionListBox.ScrollIntoView(CompletionListBox.SelectedItem);
        }
        finally
        {
            _suppressCompletionListSelectionChanged = false;
        }
    }

    private bool TryAcceptSelectedCompletion()
    {
        if (!IsControlCompletionOpen)
        {
            return false;
        }

        var selectedIndex = CompletionListBox.SelectedIndex;
        if (selectedIndex < 0 || selectedIndex >= _completionItems.Count)
        {
            return false;
        }

        var sourceText = DocumentEditing.GetText(SourceEditor.Document);
        if (!TryBuildCompletionContext(sourceText, SourceEditor.SelectionStart, SourceEditor.SelectionLength, out var context))
        {
            DismissCompletionPopup();
            return false;
        }

        var selectedItem = _completionItems[selectedIndex];
        var replacement = BuildCompletionReplacement(selectedItem.ElementName);
        SourceEditor.SetFocusedFromInput(true);
        SourceEditor.Select(context.ReplaceStart, context.ReplaceLength);
        if (!SourceEditor.HandleTextCompositionFromInput(replacement))
        {
            return false;
        }

        var caretIndex = context.ReplaceStart + selectedItem.ElementName.Length + 1;
        SourceEditor.Select(caretIndex, 0);
        FocusManager.SetFocus(SourceEditor);
        DismissCompletionPopup();
        return true;
    }

    private static string BuildCompletionReplacement(string elementName)
    {
        return elementName + "></" + elementName + ">";
    }

    private static bool ShouldDismissCompletionForKey(Keys key, ModifierKeys modifiers)
    {
        if (modifiers != ModifierKeys.None)
        {
            return key != Keys.LeftControl && key != Keys.RightControl;
        }

        return key != Keys.LeftShift && key != Keys.RightShift;
    }

    private static bool TryBuildCompletionContext(string sourceText, int selectionStart, int selectionLength, out CompletionContext context)
    {
        context = default;
        var text = sourceText ?? string.Empty;
        var selectionEnd = Math.Clamp(selectionStart + selectionLength, 0, text.Length);
        selectionStart = Math.Clamp(selectionStart, 0, text.Length);

        var bracketIndex = -1;
        for (var index = selectionStart - 1; index >= 0; index--)
        {
            var current = text[index];
            if (current == '<')
            {
                bracketIndex = index;
                break;
            }

            if (current == '>' || current == '\r' || current == '\n' || current == '\'' || current == '"')
            {
                return false;
            }
        }

        if (bracketIndex < 0 || bracketIndex + 1 > text.Length)
        {
            return false;
        }

        if (bracketIndex + 1 < text.Length)
        {
            var next = text[bracketIndex + 1];
            if (next == '/' || next == '!' || next == '?')
            {
                return false;
            }
        }

        var replaceStart = bracketIndex + 1;
        for (var index = replaceStart; index < selectionStart; index++)
        {
            if (!IsCompletionIdentifierChar(text[index]))
            {
                return false;
            }
        }

        var replaceEnd = selectionEnd;
        while (replaceEnd < text.Length && IsCompletionIdentifierChar(text[replaceEnd]))
        {
            replaceEnd++;
        }

        if (replaceEnd < text.Length && text[replaceEnd] == '/' && replaceEnd + 1 < text.Length && text[replaceEnd + 1] == '>')
        {
            replaceEnd += 2;
        }
        else if (replaceEnd + 3 < text.Length && text[replaceEnd] == '>' && text[replaceEnd + 1] == '<' && text[replaceEnd + 2] == '/')
        {
            var closingEnd = replaceEnd + 3;
            while (closingEnd < text.Length && IsCompletionIdentifierChar(text[closingEnd]))
            {
                closingEnd++;
            }

            if (closingEnd < text.Length && text[closingEnd] == '>')
            {
                replaceEnd = closingEnd + 1;
            }
        }

        var prefixLength = selectionStart - replaceStart;
        if (prefixLength < 0)
        {
            return false;
        }

        var prefix = text.Substring(replaceStart, prefixLength);
        context = new CompletionContext(bracketIndex, replaceStart, replaceEnd - replaceStart, prefix);
        return true;
    }

    private static bool IsCompletionIdentifierChar(char value)
    {
        return char.IsLetterOrDigit(value) || value == '_' || value == '.';
    }

    private void OnCompletionListSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_suppressCompletionListSelectionChanged)
        {
            return;
        }

        UpdateCompletionPopupPlacement();
    }

    private void OnSourceEditorSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_suppressSourceEditorChanges)
        {
            return;
        }

        RefreshSourcePropertyInspector();
        if (!IsControlCompletionOpen)
        {
            return;
        }

        UpdateCompletionPopupPlacement();
    }

    private void OnCompletionListMouseUp(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        if (args.Button != MouseButton.Left || !IsControlCompletionOpen)
        {
            return;
        }

        if (args.OriginalSource is UIElement source && IsWithinCompletionListItem(source))
        {
            _ = TryAcceptSelectedCompletion();
            args.Handled = true;
        }
    }

    private bool IsWithinCompletionPopup(UIElement element)
    {
        for (var current = element; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (ReferenceEquals(current, CompletionPopup))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsWithinCompletionListItem(UIElement element)
    {
        for (var current = element; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is ListBoxItem)
            {
                return true;
            }
        }

        return false;
    }

    private static Panel? ResolveOverlayHost(UIElement owner)
    {
        Panel? fallbackHost = null;
        for (var current = owner; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is Panel panel)
            {
                fallbackHost = panel;
            }
        }

        return fallbackHost;
    }

    private static bool AreCompletionNamesEqual(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreRectsEffectivelyEqual(LayoutRect left, LayoutRect right)
    {
        return MathF.Abs(left.X - right.X) < 0.001f &&
               MathF.Abs(left.Y - right.Y) < 0.001f &&
               MathF.Abs(left.Width - right.Width) < 0.001f &&
               MathF.Abs(left.Height - right.Height) < 0.001f;
    }

    private void UpdateCachedSourceLineCount(string? text)
    {
        _cachedSourceLineCount = CountSourceLines(text);
    }

    private void UpdateSourceLineNumberGutter(bool force)
    {
        var lineCount = Math.Max(1, _cachedSourceLineCount);
        var lineHeight = EstimateSourceEditorLineHeight(lineCount);
        var fontSize = SourceEditor.FontSize;
        var viewportHeight = Math.Max(lineHeight, SourceEditor.ViewportHeight);
        var verticalOffset = Math.Max(0f, SourceEditor.VerticalOffset);
        var approximateVisibleLineCount = Math.Clamp((int)MathF.Ceiling(viewportHeight / lineHeight) + 1, 1, Math.Max(1, lineCount));
        var firstVisibleLine = GetFirstVisibleSourceLine(lineCount, approximateVisibleLineCount, lineHeight, verticalOffset);
        var visibleLineCount = Math.Clamp(approximateVisibleLineCount, 1, Math.Max(1, lineCount - firstVisibleLine));
        var lineOffset = verticalOffset - (firstVisibleLine * lineHeight);

        if (!force &&
            lineCount == _lastRenderedSourceLineCount &&
            firstVisibleLine == _lastRenderedSourceFirstVisibleLine &&
            visibleLineCount == _lastRenderedSourceVisibleLineCount &&
            Math.Abs(lineOffset - _lastRenderedSourceLineOffset) <= 0.01f &&
            Math.Abs(lineHeight - _lastRenderedSourceLineHeight) <= 0.01f)
        {
            return;
        }

        LineNumberPanel.LineHeight = lineHeight;
        LineNumberPanel.FontSize = fontSize;
        LineNumberPanel.VerticalLineOffset = lineOffset;
        LineNumberPanel.UpdateVisibleRange(firstVisibleLine, visibleLineCount);

        _lastRenderedSourceLineCount = lineCount;
        _lastRenderedSourceFirstVisibleLine = firstVisibleLine;
        _lastRenderedSourceVisibleLineCount = visibleLineCount;
        _lastRenderedSourceLineOffset = lineOffset;
        _lastRenderedSourceLineHeight = lineHeight;
    }

    private static int CountSourceLines(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 1;
        }

        var lineCount = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lineCount++;
            }
        }

        return lineCount;
    }

    private float EstimateSourceEditorLineHeight(int lineCount)
    {
        if (SourceEditor.TryGetCaretBounds(out var caretBounds) && caretBounds.Height > 0.01f)
        {
            return Math.Max(1f, caretBounds.Height);
        }

        if (lineCount > 0 && SourceEditor.ExtentHeight > 0.01f)
        {
            return Math.Max(1f, SourceEditor.ExtentHeight / lineCount);
        }

        return Math.Max(1f, SourceEditor.FontSize * 1.35f);
    }

    private int GetFirstVisibleSourceLine(int lineCount, int approximateVisibleLineCount, float lineHeight, float verticalOffset)
    {
        var firstVisibleLineFromScroll = Math.Clamp((int)MathF.Floor(verticalOffset / lineHeight), 0, Math.Max(0, lineCount - 1));
        var scrollableLineCount = Math.Max(0, lineCount - approximateVisibleLineCount);
        if (scrollableLineCount > 0 && SourceEditor.ScrollableHeight > 0.01f)
        {
            firstVisibleLineFromScroll = Math.Clamp(
                (int)MathF.Round((verticalOffset / SourceEditor.ScrollableHeight) * scrollableLineCount),
                0,
                Math.Max(0, lineCount - 1));
        }

        return firstVisibleLineFromScroll;
    }

    private static bool TryGetLineSelectionRange(string text, int oneBasedLineNumber, out int selectionStart, out int selectionLength)
    {
        selectionStart = 0;
        selectionLength = 0;
        if (oneBasedLineNumber < 1)
        {
            return false;
        }

        var currentLine = 1;
        var index = 0;
        while (currentLine < oneBasedLineNumber && index < text.Length)
        {
            if (text[index] == '\r')
            {
                if (index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }

                currentLine++;
                selectionStart = index + 1;
            }
            else if (text[index] == '\n')
            {
                currentLine++;
                selectionStart = index + 1;
            }

            index++;
        }

        if (currentLine != oneBasedLineNumber)
        {
            return false;
        }

        var lineEnd = selectionStart;
        while (lineEnd < text.Length && text[lineEnd] != '\r' && text[lineEnd] != '\n')
        {
            lineEnd++;
        }

        selectionLength = lineEnd - selectionStart;
        if (selectionLength > 0)
        {
            return true;
        }

        if (lineEnd < text.Length)
        {
            selectionLength = lineEnd + 1 < text.Length && text[lineEnd] == '\r' && text[lineEnd + 1] == '\n'
                ? 2
                : 1;
        }

        return true;
    }

    private static IEnumerable<T> EnumerateVisualDescendants<T>(UIElement root)
        where T : UIElement
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in EnumerateVisualDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private readonly record struct CompletionContext(int OpenBracketIndex, int ReplaceStart, int ReplaceLength, string Prefix);
}