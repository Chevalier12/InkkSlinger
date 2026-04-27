using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using InkkSlinger;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger.Designer;

public partial class DesignerSourceEditorView : UserControl
{
    private const float CompletionPopupVerticalOffset = 4f;
    private const float CompletionPopupMaxHeight = 260f;
    private const float CompletionPopupMinWidth = 180f;
    private const float CompletionPopupMaxWidth = 420f;
    private const int DeferredBulkEditRefreshThreshold = 256;

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

    public static readonly DependencyProperty EditorIndentTextProperty =
        DependencyProperty.Register(
            nameof(EditorIndentText),
            typeof(string),
            typeof(DesignerSourceEditorView),
            new FrameworkPropertyMetadata(
                IDEEditorTextCommandService.DefaultIndent,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is DesignerSourceEditorView view)
                    {
                        view.RefreshHighlightedSourceDocument(previousText: null, currentText: view.SourceText, dismissCompletionPopup: false);
                    }
                },
                coerceValueCallback: static (_, value) => value is string text && text.Length > 0 ? text : IDEEditorTextCommandService.DefaultIndent));

    private bool _suppressSourceEditorChanges;
    private float _lastObservedViewportHorizontalOffset = float.NaN;
    private float _lastObservedViewportVerticalOffset = float.NaN;
    private float _lastObservedViewportWidth = float.NaN;
    private float _lastObservedViewportHeight = float.NaN;
    private IReadOnlyList<DesignerControlCompletionItem> _completionItems = Array.Empty<DesignerControlCompletionItem>();
    private IReadOnlyList<string> _completionItemNames = Array.Empty<string>();
    private IReadOnlyList<DesignerXmlDocumentOverviewItem> _sourceOverviewItems = Array.Empty<DesignerXmlDocumentOverviewItem>();
    private IReadOnlyList<DesignerSourceInspectableProperty> _currentSourceInspectorProperties = Array.Empty<DesignerSourceInspectableProperty>();
    private CompletionContext? _completionContext;
    private bool _suppressCompletionListSelectionChanged;
    private LayoutRect _lastCompletionCaretBounds;
    private readonly Dictionary<string, DesignerSourceInspectorPropertyItem> _sourcePropertyItemsByName = new(StringComparer.Ordinal);
    private Type? _currentSourceInspectorControlType;
    private DesignerSourceTagSelection? _currentSourceTagSelection;
    private bool _suppressSourceInspectorApply;
    private bool _suppressSourceInspectorFilterTextChanged;
    private bool _forceFullSourceHighlightRefresh;
    private int _deferredSourceRefreshVersion;
    private int? _pendingSourceEditorSelectionStart;
    private int _pendingSourceEditorSelectionLength;
    private IReadOnlyList<DesignerXmlFoldRange> _currentXmlFoldRanges = Array.Empty<DesignerXmlFoldRange>();
    private readonly HashSet<string> _collapsedXmlFoldRangeKeys = new(StringComparer.Ordinal);
    private bool _suppressPairedTagRenameSync;
    private bool _isSourceMinimapDragging;
    private int? _lastSourceMinimapNavigatedLine;
    private float? _lastSourceMinimapNavigatedVerticalOffset;
    private static long _diagSourceEditorTextChangedCallCount;
    private static long _diagSourceEditorTextChangedElapsedTicks;
    private static long _diagSourceEditorTextChangedRefreshHighlightedCallCount;
    private static long _diagSourceEditorTextChangedRefreshHighlightedElapsedTicks;
    private static long _diagSourceEditorTextChangedRefreshPropertyInspectorCallCount;
    private static long _diagSourceEditorTextChangedRefreshPropertyInspectorElapsedTicks;
    private static long _diagSourceEditorTextChangedRefreshCompletionCallCount;
    private static long _diagSourceEditorTextChangedRefreshCompletionElapsedTicks;
    private long _runtimeSourceEditorTextChangedCallCount;
    private long _runtimeSourceEditorTextChangedElapsedTicks;
    private long _runtimeSourceEditorTextChangedRefreshHighlightedCallCount;
    private long _runtimeSourceEditorTextChangedRefreshHighlightedElapsedTicks;
    private long _runtimeSourceEditorTextChangedRefreshPropertyInspectorCallCount;
    private long _runtimeSourceEditorTextChangedRefreshPropertyInspectorElapsedTicks;
    private long _runtimeSourceEditorTextChangedRefreshCompletionCallCount;
    private long _runtimeSourceEditorTextChangedRefreshCompletionElapsedTicks;
    private long _runtimeLastSourceEditorTextChangedElapsedTicks;
    private bool _runtimeLastSourceEditorTextChangedRefreshedHighlighted;
    private long _runtimeLastSourceEditorTextChangedRefreshHighlightedElapsedTicks;
    private long _runtimeLastSourceEditorTextChangedRefreshPropertyInspectorElapsedTicks;
    private bool _runtimeLastSourceEditorTextChangedRefreshedCompletion;
    private long _runtimeLastSourceEditorTextChangedRefreshCompletionElapsedTicks;

    public DesignerSourceEditorView()
    {
        InitializeComponent();
        if (Content is Panel rootPanel)
        {
            _ = rootPanel.RemoveChild(CompletionPopup);
        }

        CompletionPopup.Closed += OnCompletionPopupClosed;
        CompletionListBox.SelectionChanged += OnCompletionListSelectionChanged;
        SourceMinimap.NavigateRequested += OnSourceMinimapNavigateRequested;
        CompletionListBox.AddHandler<MouseRoutedEventArgs>(UIElement.MouseUpEvent, OnCompletionListMouseUp, handledEventsToo: true);
        AddHandler<MouseRoutedEventArgs>(UIElement.PreviewMouseLeftButtonDownEvent, OnSourceMinimapPreviewMouseLeftButtonDown, handledEventsToo: true);
        AddHandler<MouseRoutedEventArgs>(UIElement.PreviewMouseMoveEvent, OnSourceMinimapPreviewMouseMove, handledEventsToo: true);
        AddHandler<MouseRoutedEventArgs>(UIElement.PreviewMouseLeftButtonUpEvent, OnSourceMinimapPreviewMouseLeftButtonUp, handledEventsToo: true);
        SourceEditor.AddHandler<KeyRoutedEventArgs>(UIElement.KeyDownEvent, OnSourceEditorKeyDown, handledEventsToo: true);
        SourceEditor.AddHandler<FocusChangedRoutedEventArgs>(UIElement.LostFocusEvent, OnSourceEditorLostFocus, handledEventsToo: true);
        SourceEditor.SelectionChanged += OnSourceEditorSelectionChanged;
        LoadDocumentIntoEditor(SourceText);
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

    public IDE_Editor Editor => SourceEditor;

    public IDEEditorMinimap Minimap => SourceMinimap;

    public Border LineNumberBorder => SourceEditor.LineNumberBorder;

    public IDEEditorLineNumberPresenter LineNumberPanel => SourceEditor.LineNumberPresenter;

    public IEnumerable SourcePropertyInspectorItems
    {
        get => GetValue<IEnumerable>(SourcePropertyInspectorItemsProperty) ?? Array.Empty<object>();
        private set => SetValue(SourcePropertyInspectorItemsProperty, value ?? Array.Empty<object>());
    }

    public bool IsControlCompletionOpen => CompletionPopup.IsOpen;

    public string EditorIndentText
    {
        get => GetValue<string>(EditorIndentTextProperty) ?? IDEEditorTextCommandService.DefaultIndent;
        set => SetValue(EditorIndentTextProperty, value);
    }

    public int CollapsedXmlFoldCount => _collapsedXmlFoldRangeKeys.Count;

    public IReadOnlyList<DesignerXmlDocumentOverviewItem> SourceOverviewItems => _sourceOverviewItems;

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

    public DesignerSourceEditorViewRuntimeDiagnosticsSnapshot GetDesignerSourceEditorViewSnapshotForDiagnostics()
    {
        return new DesignerSourceEditorViewRuntimeDiagnosticsSnapshot(
            SourceEditorTextChangedCallCount: _runtimeSourceEditorTextChangedCallCount,
            SourceEditorTextChangedMilliseconds: TicksToMilliseconds(_runtimeSourceEditorTextChangedElapsedTicks),
            SourceEditorTextChangedRefreshHighlightedCallCount: _runtimeSourceEditorTextChangedRefreshHighlightedCallCount,
            SourceEditorTextChangedRefreshHighlightedMilliseconds: TicksToMilliseconds(_runtimeSourceEditorTextChangedRefreshHighlightedElapsedTicks),
            SourceEditorTextChangedRefreshPropertyInspectorCallCount: _runtimeSourceEditorTextChangedRefreshPropertyInspectorCallCount,
            SourceEditorTextChangedRefreshPropertyInspectorMilliseconds: TicksToMilliseconds(_runtimeSourceEditorTextChangedRefreshPropertyInspectorElapsedTicks),
            SourceEditorTextChangedRefreshCompletionCallCount: _runtimeSourceEditorTextChangedRefreshCompletionCallCount,
            SourceEditorTextChangedRefreshCompletionMilliseconds: TicksToMilliseconds(_runtimeSourceEditorTextChangedRefreshCompletionElapsedTicks),
            LastSourceEditorTextChangedMilliseconds: TicksToMilliseconds(_runtimeLastSourceEditorTextChangedElapsedTicks),
            LastSourceEditorTextChangedRefreshedHighlighted: _runtimeLastSourceEditorTextChangedRefreshedHighlighted,
            LastSourceEditorTextChangedRefreshHighlightedMilliseconds: TicksToMilliseconds(_runtimeLastSourceEditorTextChangedRefreshHighlightedElapsedTicks),
            LastSourceEditorTextChangedRefreshPropertyInspectorMilliseconds: TicksToMilliseconds(_runtimeLastSourceEditorTextChangedRefreshPropertyInspectorElapsedTicks),
            LastSourceEditorTextChangedRefreshedCompletion: _runtimeLastSourceEditorTextChangedRefreshedCompletion,
            LastSourceEditorTextChangedRefreshCompletionMilliseconds: TicksToMilliseconds(_runtimeLastSourceEditorTextChangedRefreshCompletionElapsedTicks));
    }

    public new static DesignerSourceEditorViewTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return CreateTelemetrySnapshot(reset: false);
    }

    public new static DesignerSourceEditorViewTelemetrySnapshot GetTelemetryAndReset()
    {
        return CreateTelemetrySnapshot(reset: true);
    }

    private void OnSourceEditorTextChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;

        if (_suppressSourceEditorChanges)
        {
            return;
        }

        var startTicks = Stopwatch.GetTimestamp();
        _runtimeSourceEditorTextChangedCallCount++;
        _diagSourceEditorTextChangedCallCount++;

        var previousText = SourceText;
        var currentText = DocumentEditing.GetText(SourceEditor.Document);
        var rawInputText = currentText;
        if (_collapsedXmlFoldRangeKeys.Count > 0)
        {
            return;
        }

        if (ShouldDeferSourceEditorRefresh(previousText, currentText))
        {
            if (!string.Equals(previousText, currentText, StringComparison.Ordinal))
            {
                SourceText = currentText;
            }

            DismissCompletionPopup();
            ScheduleDeferredSourceEditorRefresh(currentText);

            var bulkElapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _runtimeSourceEditorTextChangedElapsedTicks += bulkElapsedTicks;
            _diagSourceEditorTextChangedElapsedTicks += bulkElapsedTicks;
            _runtimeLastSourceEditorTextChangedElapsedTicks = bulkElapsedTicks;
            _runtimeLastSourceEditorTextChangedRefreshedHighlighted = false;
            _runtimeLastSourceEditorTextChangedRefreshHighlightedElapsedTicks = 0;
            _runtimeLastSourceEditorTextChangedRefreshPropertyInspectorElapsedTicks = 0;
            _runtimeLastSourceEditorTextChangedRefreshedCompletion = false;
            _runtimeLastSourceEditorTextChangedRefreshCompletionElapsedTicks = 0;

            ApplyPendingSourceEditorSelection();
            return;
        }

        TryAutoInsertTagSuffix(ref currentText);
        if (!_suppressPairedTagRenameSync)
        {
            TrySynchronizePairedTagRename(previousText, ref currentText);
        }

        if (string.Equals(rawInputText, currentText, StringComparison.Ordinal))
        {
            PreserveCaretAfterSourceEditorInsertion(previousText, currentText);
        }
        if (!string.Equals(previousText, currentText, StringComparison.Ordinal))
        {
            SourceText = currentText;
        }

        var forceFullHighlightRefresh = _forceFullSourceHighlightRefresh;
        _forceFullSourceHighlightRefresh = false;

        var refreshedHighlighted = forceFullHighlightRefresh || ShouldRefreshHighlightedSourceDocument(previousText, currentText);
        long refreshHighlightedElapsedTicks = 0;
        if (refreshedHighlighted)
        {
            var refreshHighlightedStartTicks = Stopwatch.GetTimestamp();
            RefreshHighlightedSourceDocument(
                forceFullHighlightRefresh ? null : previousText,
                currentText,
                dismissCompletionPopup: false);
            refreshHighlightedElapsedTicks = Stopwatch.GetTimestamp() - refreshHighlightedStartTicks;
            _runtimeSourceEditorTextChangedRefreshHighlightedCallCount++;
            _runtimeSourceEditorTextChangedRefreshHighlightedElapsedTicks += refreshHighlightedElapsedTicks;
            _diagSourceEditorTextChangedRefreshHighlightedCallCount++;
            _diagSourceEditorTextChangedRefreshHighlightedElapsedTicks += refreshHighlightedElapsedTicks;
        }

        var refreshPropertyInspectorStartTicks = Stopwatch.GetTimestamp();
        RefreshSourcePropertyInspector();
        var refreshPropertyInspectorElapsedTicks = Stopwatch.GetTimestamp() - refreshPropertyInspectorStartTicks;
        _runtimeSourceEditorTextChangedRefreshPropertyInspectorCallCount++;
        _runtimeSourceEditorTextChangedRefreshPropertyInspectorElapsedTicks += refreshPropertyInspectorElapsedTicks;
        _diagSourceEditorTextChangedRefreshPropertyInspectorCallCount++;
        _diagSourceEditorTextChangedRefreshPropertyInspectorElapsedTicks += refreshPropertyInspectorElapsedTicks;

        var refreshedCompletion = false;
        long refreshCompletionElapsedTicks = 0;
        if (IsControlCompletionOpen)
        {
            refreshedCompletion = true;
            var refreshCompletionStartTicks = Stopwatch.GetTimestamp();
            _ = RefreshCompletionPopup(openIfPossible: false);
            refreshCompletionElapsedTicks = Stopwatch.GetTimestamp() - refreshCompletionStartTicks;
            _runtimeSourceEditorTextChangedRefreshCompletionCallCount++;
            _runtimeSourceEditorTextChangedRefreshCompletionElapsedTicks += refreshCompletionElapsedTicks;
            _diagSourceEditorTextChangedRefreshCompletionCallCount++;
            _diagSourceEditorTextChangedRefreshCompletionElapsedTicks += refreshCompletionElapsedTicks;
        }

        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _runtimeSourceEditorTextChangedElapsedTicks += elapsedTicks;
        _diagSourceEditorTextChangedElapsedTicks += elapsedTicks;
        _runtimeLastSourceEditorTextChangedElapsedTicks = elapsedTicks;
        _runtimeLastSourceEditorTextChangedRefreshedHighlighted = refreshedHighlighted;
        _runtimeLastSourceEditorTextChangedRefreshHighlightedElapsedTicks = refreshHighlightedElapsedTicks;
        _runtimeLastSourceEditorTextChangedRefreshPropertyInspectorElapsedTicks = refreshPropertyInspectorElapsedTicks;
        _runtimeLastSourceEditorTextChangedRefreshedCompletion = refreshedCompletion;
        _runtimeLastSourceEditorTextChangedRefreshCompletionElapsedTicks = refreshCompletionElapsedTicks;

        ApplyPendingSourceEditorSelection();
    }

    private void TryAutoInsertTagSuffix(ref string currentText)
    {
        var previousText = SourceText;
        if (!TryGetSingleCharacterInsertion(previousText, currentText, out var insertedIndex, out var insertedCharacter))
        {
            return;
        }

        if (insertedCharacter == '>')
        {
            TryAutoInsertClosingTag(currentText, insertedIndex, out currentText);
            return;
        }

        if (insertedCharacter == '\n')
        {
            ApplySourceEditorTextEdit(
                DesignerXmlEditorLanguageService.ApplySmartEnter(previousText, currentText, insertedIndex, EditorIndentText),
                keepPendingSelection: true,
                previousText: previousText);
            currentText = DocumentEditing.GetText(SourceEditor.Document);
            return;
        }

        if (DesignerXmlEditorLanguageService.TryHandlePairedCharacter(currentText, insertedIndex, insertedCharacter, out var pairedEdit))
        {
            ApplySourceEditorTextEdit(pairedEdit, keepPendingSelection: true);
            currentText = DocumentEditing.GetText(SourceEditor.Document);
            return;
        }

        if (insertedCharacter == '/')
        {
            if (insertedIndex > 0 && currentText[insertedIndex - 1] == '<')
            {
                TryAutoInsertInferredClosingTag(currentText, insertedIndex, out currentText);
                return;
            }

            TryAutoInsertSelfClosingBracket(currentText, insertedIndex, out currentText);
        }

        if (!_pendingSourceEditorSelectionStart.HasValue)
        {
            _pendingSourceEditorSelectionStart = Math.Clamp(insertedIndex + 1, 0, currentText.Length);
            _pendingSourceEditorSelectionLength = 0;
        }
    }

    private void PreserveCaretAfterSourceEditorInsertion(string previousText, string currentText)
    {
        if (_pendingSourceEditorSelectionStart.HasValue ||
            !TryGetTextInsertion(previousText, currentText, out var insertedIndex, out var insertedLength))
        {
            return;
        }

        _pendingSourceEditorSelectionStart = Math.Clamp(insertedIndex + insertedLength, 0, currentText.Length);
        _pendingSourceEditorSelectionLength = 0;
    }

    private void ApplySourceEditorTextEdit(
        IDEEditorTextEditResult edit,
        bool dismissCompletionPopup = false,
        string? previousText = null,
        bool keepPendingSelection = false)
    {
        var normalizedText = IDEEditorTextCommandService.Normalize(edit.Text);
        _forceFullSourceHighlightRefresh = false;
        RefreshHighlightedSourceDocument(previousText: previousText, currentText: normalizedText, dismissCompletionPopup: dismissCompletionPopup);
        var selectionStart = Math.Clamp(edit.SelectionStart, 0, normalizedText.Length);
        var selectionLength = Math.Clamp(edit.SelectionLength, 0, normalizedText.Length - selectionStart);
        SourceEditor.Select(selectionStart, selectionLength);
        SourceText = normalizedText;
        if (keepPendingSelection ||
            SourceEditor.SelectionStart != selectionStart ||
            SourceEditor.SelectionLength != selectionLength)
        {
            _pendingSourceEditorSelectionStart = selectionStart;
            _pendingSourceEditorSelectionLength = selectionLength;
        }
        else
        {
            _pendingSourceEditorSelectionStart = null;
            _pendingSourceEditorSelectionLength = 0;
        }
    }

    private void TrySynchronizePairedTagRename(string previousText, ref string currentText)
    {
        if (DesignerXmlEditorLanguageService.TrySynchronizePairedTagRename(
                previousText,
                currentText,
                SourceEditor.SelectionStart,
                out var edit))
        {
            ApplySourceEditorTextEdit(edit, keepPendingSelection: true);
            currentText = DocumentEditing.GetText(SourceEditor.Document);
        }
    }

    private void ApplyPendingSourceEditorSelection()
    {
        if (!_pendingSourceEditorSelectionStart.HasValue)
        {
            return;
        }

        var textLength = DocumentEditing.GetText(SourceEditor.Document).Length;
        var selectionStart = Math.Clamp(_pendingSourceEditorSelectionStart.Value, 0, textLength);
        var selectionLength = Math.Clamp(_pendingSourceEditorSelectionLength, 0, textLength - selectionStart);
        _pendingSourceEditorSelectionStart = null;
        _pendingSourceEditorSelectionLength = 0;
        SourceEditor.Select(selectionStart, selectionLength);
    }

    private void TryAutoInsertClosingTag(string currentText, int insertedIndex, out string updatedText)
    {
        updatedText = currentText;
        if (!TryGetCompletedStartTagName(currentText, insertedIndex, out var tagName) ||
            !DesignerXmlSyntaxHighlighter.TryClassifyTagName(tagName, out _) ||
            HasImmediateClosingTag(currentText, insertedIndex + 1, tagName))
        {
            return;
        }

        var closingTag = "</" + tagName + ">";
        _ = TryCreateMultilinePropertyElementSuffix(
            currentText,
            insertedIndex,
            tagName,
            closingTag,
            out var tagSuffix,
            out var caretOffset);
        _suppressSourceEditorChanges = true;
        try
        {
            SourceEditor.Select(insertedIndex + 1, 0);
            DocumentEditing.InsertTextAt(SourceEditor.Document, insertedIndex + 1, tagSuffix);
            SourceEditor.Select(insertedIndex + 1 + caretOffset, 0);
            updatedText = DocumentEditing.GetText(SourceEditor.Document);
        }
        finally
        {
            _suppressSourceEditorChanges = false;
        }
    }

    private void TryAutoInsertSelfClosingBracket(string currentText, int insertedIndex, out string updatedText)
    {
        updatedText = currentText;
        if (!DesignerXmlEditorLanguageService.TryHandleSelfClosingTagSlash(currentText, insertedIndex, out var edit))
        {
            return;
        }

        ApplySourceEditorTextEdit(edit, keepPendingSelection: true);
        updatedText = DocumentEditing.GetText(SourceEditor.Document);
    }

    private void TryAutoInsertInferredClosingTag(string currentText, int slashIndex, out string updatedText)
    {
        updatedText = currentText;
        if (!TryFindInferredClosingTagTarget(currentText, slashIndex - 1, out var target))
        {
            return;
        }

        var closingTagOpenIndex = slashIndex - 1;
        var closingTagIndentation = GetInferredClosingTagIndentation(currentText, target.TagStartIndex, closingTagOpenIndex);
        _suppressSourceEditorChanges = true;
        try
        {
            if (target.IsSelfClosing)
            {
                var removalStart = target.SelfClosingSlashIndex;
                while (removalStart > target.TagStartIndex && char.IsWhiteSpace(currentText[removalStart - 1]))
                {
                    removalStart--;
                }

                DocumentEditing.DeleteRange(SourceEditor.Document, removalStart, target.SelfClosingSlashIndex - removalStart + 1);
                _forceFullSourceHighlightRefresh = true;
                closingTagOpenIndex -= target.SelfClosingSlashIndex - removalStart + 1;
            }

            if (closingTagIndentation.Length > 0)
            {
                DocumentEditing.InsertTextAt(SourceEditor.Document, closingTagOpenIndex, closingTagIndentation);
                _forceFullSourceHighlightRefresh = true;
                closingTagOpenIndex += closingTagIndentation.Length;
            }

            SourceEditor.Select(closingTagOpenIndex + 2, 0);
            if (!SourceEditor.HandleTextCompositionFromInput(target.Name + ">"))
            {
                return;
            }

            SourceEditor.Select(closingTagOpenIndex + target.Name.Length + 3, 0);
            updatedText = DocumentEditing.GetText(SourceEditor.Document);
        }
        finally
        {
            _suppressSourceEditorChanges = false;
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

        if (IsControlCompletionOpen)
        {
            UpdateCompletionPopupPlacement();
        }

        UpdateSourceMinimapMetrics();
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

        if (IsControlCompletionOpen)
        {
            UpdateCompletionPopupPlacement();
        }

        UpdateSourceMinimapMetrics();
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
        ClearCollapsedXmlFolds();
        RefreshHighlightedSourceDocument(previousText: null, currentText: newText, dismissCompletionPopup: false);
        RefreshSourcePropertyInspector();
    }

    private void LoadDocumentIntoEditor(string? text)
    {
        RefreshHighlightedSourceDocument(previousText: null, currentText: text, dismissCompletionPopup: true);
    }

    private void RefreshHighlightedSourceDocument(string? previousText, string? currentText, bool dismissCompletionPopup)
    {
        if (dismissCompletionPopup)
        {
            DismissCompletionPopup();
        }

        var normalizedText = currentText ?? string.Empty;
        RefreshSourceOverview(normalizedText);
        var displayText = CreateSourceEditorDisplayText(normalizedText);
        var selectionStart = SourceEditor.SelectionStart;
        var selectionLength = SourceEditor.SelectionLength;
        var horizontalOffset = SourceEditor.HorizontalOffset;
        var verticalOffset = SourceEditor.VerticalOffset;

        _suppressSourceEditorChanges = true;
        try
        {
            var refreshedIncrementally = _collapsedXmlFoldRangeKeys.Count == 0 &&
                !string.IsNullOrEmpty(previousText) &&
                DesignerXmlSyntaxHighlighter.TryPopulateDocumentIncrementally(SourceEditor.Document, previousText, displayText);
            if (!refreshedIncrementally)
            {
                DesignerXmlSyntaxHighlighter.PopulateDocument(SourceEditor.Document, displayText);
            }

            SourceEditor.RefreshDocumentMetrics();

            var updatedTextLength = DocumentEditing.GetText(SourceEditor.Document).Length;
            var clampedSelectionStart = Math.Clamp(selectionStart, 0, updatedTextLength);
            var clampedSelectionLength = Math.Clamp(selectionLength, 0, updatedTextLength - clampedSelectionStart);
            SourceEditor.Select(clampedSelectionStart, clampedSelectionLength);
            SourceEditor.ScrollToHorizontalOffset(horizontalOffset);
            SourceEditor.ScrollToVerticalOffset(verticalOffset);
            SourceEditor.IsReadOnly = _collapsedXmlFoldRangeKeys.Count > 0;
            SourceEditor.PreserveCurrentScrollOffsetsOnNextLayout();
        }
        finally
        {
            _suppressSourceEditorChanges = false;
        }
    }

    private void NavigateToLine(int oneBasedLineNumber)
    {
        var sourceText = SourceText;
        if (!TryGetLineSelectionRange(sourceText, oneBasedLineNumber, out var selectionStart, out var selectionLength))
        {
            return;
        }

        FocusManager.SetFocus(SourceEditor);
        SourceEditor.Select(selectionStart, selectionLength);
        SourceEditor.ScrollToHorizontalOffset(0f);

        var lineHeight = Math.Max(1f, SourceEditor.EstimatedLineHeight);
        var desiredVerticalOffset = Math.Max(0f, ((oneBasedLineNumber - 1) * lineHeight) - (SourceEditor.ViewportHeight * 0.35f));
        SourceEditor.ScrollToVerticalOffset(desiredVerticalOffset);
        RefreshSourcePropertyInspector();
    }

    private void ScrollSourceEditorToOffsetFromMinimap(float verticalOffset)
    {
        SourceEditor.ScrollToVerticalOffset(Math.Max(0f, verticalOffset));
    }

    private void OnSourceEditorKeyDown(object? sender, KeyRoutedEventArgs args)
    {
        _ = sender;

        if (args.Key == Keys.Space && args.Modifiers == ModifierKeys.Control)
        {
            args.Handled = RefreshCompletionPopup(openIfPossible: true) || IsControlCompletionOpen;
            return;
        }

        if (IsControlCompletionOpen)
        {
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

        if (TryHandleSourceEditorCommandKey(args.Key, args.Modifiers))
        {
            args.Handled = true;
            return;
        }

        if (args.Key == Keys.Back && args.Modifiers == ModifierKeys.None)
        {
            args.Handled = SourceEditor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None);
        }
    }

    private bool TryHandleSourceEditorCommandKey(Keys key, ModifierKeys modifiers)
    {
        var text = DocumentEditing.GetText(SourceEditor.Document);
        var selectionStart = SourceEditor.SelectionStart;
        var selectionLength = SourceEditor.SelectionLength;

        if (key == Keys.Enter && modifiers == ModifierKeys.None)
        {
            var inserted = IDEEditorTextCommandService.ReplaceSelection(text, selectionStart, selectionLength, "\n");
            ApplySourceEditorTextEdit(
                DesignerXmlEditorLanguageService.ApplySmartEnter(text, inserted.Text, selectionStart, EditorIndentText),
                previousText: text);
            return true;
        }

        if (key == Keys.Tab && modifiers == ModifierKeys.None)
        {
            ApplySourceEditorTextEdit(
                selectionLength == 0
                    ? IDEEditorTextCommandService.ReplaceSelection(text, selectionStart, selectionLength, EditorIndentText)
                    : IDEEditorTextCommandService.IndentSelectedLines(text, selectionStart, selectionLength, EditorIndentText));
            return true;
        }

        if (key == Keys.Tab && modifiers == ModifierKeys.Shift)
        {
            ApplySourceEditorTextEdit(IDEEditorTextCommandService.OutdentSelectedLines(text, selectionStart, selectionLength, EditorIndentText));
            return true;
        }

        if (IsSlashKey(key) && modifiers == ModifierKeys.Control)
        {
            ApplySourceEditorTextEdit(DesignerXmlEditorLanguageService.ToggleXmlComment(text, selectionStart, selectionLength));
            return true;
        }

        if (key == Keys.K && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            ApplySourceEditorTextEdit(IDEEditorTextCommandService.DeleteSelectedLines(text, selectionStart, selectionLength));
            return true;
        }

        if ((key == Keys.Down || key == Keys.Up) && modifiers == (ModifierKeys.Alt | ModifierKeys.Shift))
        {
            ApplySourceEditorTextEdit(IDEEditorTextCommandService.DuplicateSelectedLines(text, selectionStart, selectionLength));
            return true;
        }

        if ((key == Keys.Down || key == Keys.Up) && modifiers == ModifierKeys.Alt)
        {
            ApplySourceEditorTextEdit(IDEEditorTextCommandService.MoveSelectedLines(text, selectionStart, selectionLength, key == Keys.Up ? -1 : 1));
            return true;
        }

        if (key == Keys.F && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            ApplySourceEditorTextEdit(IDEEditorTextCommandService.FormatAll(text, value => DesignerXmlEditorLanguageService.FormatDocument(value, EditorIndentText), selectionStart, selectionLength));
            return true;
        }

        if (key == Keys.OemOpenBrackets && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            ToggleXmlFoldAtCaret();
            return true;
        }

        if (key == Keys.OemCloseBrackets && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            ClearCollapsedXmlFolds();
            RefreshHighlightedSourceDocument(previousText: null, currentText: SourceText, dismissCompletionPopup: false);
            return true;
        }

        if (key == Keys.M && modifiers == ModifierKeys.Control &&
            DesignerXmlEditorLanguageService.TryFindMatchingTag(text, selectionStart, out var match))
        {
            SourceEditor.Select(match.Start, match.Length);
            return true;
        }

        return false;
    }

    private static bool IsSlashKey(Keys key)
    {
        return key is Keys.Divide or Keys.OemQuestion;
    }

    private string CreateSourceEditorDisplayText(string sourceText)
    {
        _currentXmlFoldRanges = DesignerXmlEditorLanguageService.GetFoldRanges(sourceText);
        if (_collapsedXmlFoldRangeKeys.Count == 0)
        {
            return sourceText;
        }

        var collapsedRanges = _currentXmlFoldRanges
            .Where(range => _collapsedXmlFoldRangeKeys.Contains(GetXmlFoldRangeKey(range)))
            .ToArray();
        _collapsedXmlFoldRangeKeys.Clear();
        foreach (var range in collapsedRanges)
        {
            _collapsedXmlFoldRangeKeys.Add(GetXmlFoldRangeKey(range));
        }

        return DesignerXmlEditorLanguageService.TryCreateFoldedProjection(sourceText, collapsedRanges, out var projection)
            ? projection
            : sourceText;
    }

    private void ToggleXmlFoldAtCaret()
    {
        if (_collapsedXmlFoldRangeKeys.Count > 0)
        {
            ClearCollapsedXmlFolds();
            RefreshHighlightedSourceDocument(previousText: null, currentText: SourceText, dismissCompletionPopup: false);
            return;
        }

        if (!DesignerXmlEditorLanguageService.TryFindFoldRangeAtOrNearCaret(SourceText, SourceEditor.SelectionStart, out var range))
        {
            return;
        }

        _collapsedXmlFoldRangeKeys.Add(GetXmlFoldRangeKey(range));
        RefreshHighlightedSourceDocument(previousText: null, currentText: SourceText, dismissCompletionPopup: false);
    }

    private void ClearCollapsedXmlFolds()
    {
        if (_collapsedXmlFoldRangeKeys.Count == 0)
        {
            SourceEditor.IsReadOnly = false;
            return;
        }

        _collapsedXmlFoldRangeKeys.Clear();
        SourceEditor.IsReadOnly = false;
    }

    private static string GetXmlFoldRangeKey(DesignerXmlFoldRange range)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{range.StartOffset}:{range.EndOffset}:{range.Name}");
    }

    private void RefreshSourceOverview(string sourceText)
    {
        _sourceOverviewItems = DesignerXmlEditorLanguageService.GetDocumentOverview(sourceText);
        SourceMinimap.SourceText = sourceText;
        UpdateSourceMinimapMetrics();
    }

    private void UpdateSourceMinimapMetrics()
    {
        SourceMinimap.EditorVerticalOffset = SourceEditor.VerticalOffset;
        SourceMinimap.EditorViewportHeight = SourceEditor.ViewportHeight;
        SourceMinimap.EditorEstimatedLineHeight = Math.Max(1f, SourceEditor.EstimatedLineHeight);
    }

    private void OnSourceMinimapNavigateRequested(object? sender, IDEEditorMinimapNavigateEventArgs args)
    {
        _ = sender;
        if (_lastSourceMinimapNavigatedVerticalOffset.HasValue &&
            Math.Abs(_lastSourceMinimapNavigatedVerticalOffset.Value - args.VerticalOffset) < 0.01f)
        {
            return;
        }

        _lastSourceMinimapNavigatedLine = args.LineNumber;
        _lastSourceMinimapNavigatedVerticalOffset = args.VerticalOffset;
        if (_collapsedXmlFoldRangeKeys.Count > 0)
        {
            ClearCollapsedXmlFolds();
            RefreshHighlightedSourceDocument(previousText: null, currentText: SourceText, dismissCompletionPopup: false);
        }

        ScrollSourceEditorToOffsetFromMinimap(args.VerticalOffset);
    }

    private void OnSourceMinimapPreviewMouseLeftButtonDown(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        if (args.Button != MouseButton.Left)
        {
            return;
        }

        _lastSourceMinimapNavigatedLine = null;
        _lastSourceMinimapNavigatedVerticalOffset = null;
        if (SourceMinimap.BeginPointerNavigation(args.Position))
        {
            _isSourceMinimapDragging = true;
            args.Handled = true;
            return;
        }
    }

    private void OnSourceMinimapPreviewMouseMove(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        if (!_isSourceMinimapDragging)
        {
            return;
        }

        if (SourceMinimap.ContinuePointerNavigation(args.Position))
        {
            args.Handled = true;
        }
    }

    private void OnSourceMinimapPreviewMouseLeftButtonUp(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        if (!_isSourceMinimapDragging)
        {
            return;
        }

        _isSourceMinimapDragging = false;
        SourceMinimap.EndPointerNavigation();
        _lastSourceMinimapNavigatedLine = null;
        _lastSourceMinimapNavigatedVerticalOffset = null;
        args.Handled = true;
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
            SourcePropertyInspectorHeaderText.Text = "Select a tag in the source editor.";
            SourcePropertyInspectorSummaryText.Text = "Changes update the XML source. Press F5 to refresh the preview.";
            SourcePropertyInspectorEmptyState.Text = "Place the caret inside a start tag such as <Button /> or <RowDefinition /> to edit its properties.";
            SourcePropertyInspectorEmptyState.Visibility = Visibility.Visible;
            SourcePropertyInspectorFilterBorder.Visibility = Visibility.Collapsed;
            SourcePropertyInspectorScrollViewer.Visibility = Visibility.Collapsed;
            return;
        }

        var controlType = DesignerSourcePropertyInspector.ResolveInspectableType(selection);
        if (controlType == null)
        {
            _currentSourceTagSelection = selection;
            _currentSourceInspectorControlType = null;
            _currentSourceInspectorProperties = Array.Empty<DesignerSourceInspectableProperty>();
            ClearSourcePropertyInspectorEditorState();
            SourcePropertyInspectorHeaderText.Text = selection.ElementName;
            SourcePropertyInspectorSummaryText.Text = "The source tag was found, but no matching tag type is registered for editing.";
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

                var currentValue = selection.TryGetEditablePropertyValue(property.Name, out var resolvedValue)
                    ? resolvedValue
                    : string.Empty;
                item.DescriptionText = BuildSourcePropertyDescription(property, currentValue);
                if ((property.EditorKind == DesignerSourcePropertyEditorKind.Text ||
                     property.EditorKind == DesignerSourcePropertyEditorKind.TextChoice) &&
                    string.Equals(activePropertyName, property.Name, StringComparison.Ordinal))
                {
                    if (activeEditorText != null && !string.Equals(item.EditorText, activeEditorText, StringComparison.Ordinal))
                    {
                        item.EditorText = activeEditorText;
                    }

                    if (property.EditorKind == DesignerSourcePropertyEditorKind.TextChoice)
                    {
                        UpdateSourcePropertyChoiceEditorValue(item, property, activeEditorText ?? currentValue);
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
                    case DesignerSourcePropertyEditorKind.TextChoice:
                        UpdateSourcePropertyTextChoiceEditorValue(item, property, currentValue);
                        break;
                    case DesignerSourcePropertyEditorKind.Composite:
                        UpdateSourcePropertyCompositeEditorValue(
                            item,
                            property,
                            currentValue,
                            preserveVisibleValues: string.Equals(activePropertyName, property.Name, StringComparison.Ordinal));
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

            var currentValue = selection.TryGetEditablePropertyValue(property.Name, out var resolvedValue)
                ? resolvedValue
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

    private void OnSourcePropertyCompositeTextChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = args;
        if (_suppressSourceInspectorApply ||
            sender is not TextBox editor ||
            editor.Tag is not DesignerSourceInspectorCompositeComponentItem componentItem)
        {
            return;
        }

        if (!string.Equals(componentItem.Text, editor.Text, StringComparison.Ordinal))
        {
            componentItem.Text = editor.Text;
        }

        var propertyValue = componentItem.Owner.BuildCompositeEditorText();
        if (!TryApplySourceInspectorEdit(componentItem.Owner.Name, propertyValue))
        {
            return;
        }

        RefreshSourcePropertyInspector(componentItem.Owner.Name);
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
        if (item.EditorKind == DesignerSourcePropertyEditorKind.TextChoice &&
            !string.Equals(item.EditorText, selectedValue ?? string.Empty, StringComparison.Ordinal))
        {
            item.EditorText = selectedValue ?? string.Empty;
        }

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

    private static void UpdateSourcePropertyTextChoiceEditorValue(
        DesignerSourceInspectorPropertyItem item,
        DesignerSourceInspectableProperty property,
        string currentValue)
    {
        if (!string.Equals(item.EditorText, currentValue, StringComparison.Ordinal))
        {
            item.EditorText = currentValue;
        }

        UpdateSourcePropertyChoiceEditorValue(item, property, currentValue);
    }

    private static void UpdateSourcePropertyCompositeEditorValue(
        DesignerSourceInspectorPropertyItem item,
        DesignerSourceInspectableProperty property,
        string currentValue,
        bool preserveVisibleValues)
    {
        var componentValues = DesignerSourcePropertyInspector.ExpandCompositeEditorValues(
            property.CompositeValueKind,
            currentValue,
            property.DefaultValueDisplay);
        item.SetCompositeEditorValues(componentValues, preserveVisibleValues);
    }

    private bool RefreshCompletionPopup(bool openIfPossible)
    {
        var sourceText = DocumentEditing.GetText(SourceEditor.Document);
        if (!TryBuildCompletionContext(sourceText, SourceEditor.SelectionStart, SourceEditor.SelectionLength, out var context))
        {
            DismissCompletionPopup();
            return false;
        }

        var items = context.Prefix.Contains('.', StringComparison.Ordinal)
            ? DesignerControlCompletionCatalog.GetPropertyElementItems(context.Prefix)
            : DesignerControlCompletionCatalog.GetItems(context.Prefix);
        if (items.Count == 0)
        {
            DismissCompletionPopup();
            return false;
        }

        _completionContext = context;
        _completionItems = items;
        var itemNames = items.Select(static item => item.DisplayName).ToArray();
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
        _suppressPairedTagRenameSync = true;
        try
        {
            if (!SourceEditor.HandleTextCompositionFromInput(replacement))
            {
                return false;
            }
        }
        finally
        {
            _suppressPairedTagRenameSync = false;
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

    private static bool TryGetSingleCharacterInsertion(
        string previousText,
        string currentText,
        out int insertedIndex,
        out char insertedCharacter)
    {
        insertedIndex = -1;
        insertedCharacter = default;

        if (currentText.Length != previousText.Length + 1)
        {
            return false;
        }

        var prefixLength = 0;
        while (prefixLength < previousText.Length &&
               previousText[prefixLength] == currentText[prefixLength])
        {
            prefixLength++;
        }

        var previousSuffixIndex = previousText.Length - 1;
        var currentSuffixIndex = currentText.Length - 1;
        while (previousSuffixIndex >= prefixLength &&
               currentSuffixIndex > prefixLength &&
               previousText[previousSuffixIndex] == currentText[currentSuffixIndex])
        {
            previousSuffixIndex--;
            currentSuffixIndex--;
        }

        if (previousSuffixIndex >= prefixLength)
        {
            return false;
        }

        insertedIndex = prefixLength;
        if (insertedIndex < 0 || insertedIndex >= currentText.Length)
        {
            return false;
        }

        insertedCharacter = currentText[insertedIndex];
        return true;
    }

    private static bool TryGetTextInsertion(
        string previousText,
        string currentText,
        out int insertedIndex,
        out int insertedLength)
    {
        insertedIndex = -1;
        insertedLength = 0;

        if (currentText.Length <= previousText.Length)
        {
            return false;
        }

        var prefixLength = 0;
        while (prefixLength < previousText.Length &&
               previousText[prefixLength] == currentText[prefixLength])
        {
            prefixLength++;
        }

        var previousSuffixIndex = previousText.Length - 1;
        var currentSuffixIndex = currentText.Length - 1;
        while (previousSuffixIndex >= prefixLength &&
               currentSuffixIndex >= prefixLength &&
               previousText[previousSuffixIndex] == currentText[currentSuffixIndex])
        {
            previousSuffixIndex--;
            currentSuffixIndex--;
        }

        if (previousSuffixIndex >= prefixLength)
        {
            return false;
        }

        insertedIndex = prefixLength;
        insertedLength = currentSuffixIndex - prefixLength + 1;
        return insertedLength > 0;
    }

    private static bool ShouldRefreshHighlightedSourceDocument(string previousText, string currentText)
    {
        if (TryGetSingleCharacterInsertion(previousText, currentText, out var insertedIndex, out var insertedCharacter) &&
            IsAttributeValueTextCharacter(insertedCharacter) &&
            IsInsideQuotedAttributeValue(currentText, insertedIndex))
        {
            return false;
        }

        return !IsWhitespaceOnlyEdit(previousText, currentText);
    }

    private static bool ShouldDeferSourceEditorRefresh(string previousText, string currentText)
    {
        return TryGetTextInsertion(previousText, currentText, out _, out var insertedLength) &&
            insertedLength >= DeferredBulkEditRefreshThreshold;
    }

    private void ScheduleDeferredSourceEditorRefresh(string currentText)
    {
        var refreshVersion = ++_deferredSourceRefreshVersion;
        Dispatcher.EnqueueDeferred(
            () =>
            {
                if (refreshVersion != _deferredSourceRefreshVersion ||
                    !string.Equals(SourceText, currentText, StringComparison.Ordinal))
                {
                    return;
                }

                RefreshHighlightedSourceDocument(previousText: null, currentText, dismissCompletionPopup: false);
                RefreshSourcePropertyInspector();
            });
    }

    private static bool IsAttributeValueTextCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value is '_' or '-' or '.' or ':';
    }

    private static bool IsInsideQuotedAttributeValue(string text, int index)
    {
        var tagStart = text.LastIndexOf('<', Math.Clamp(index, 0, Math.Max(0, text.Length - 1)));
        if (tagStart < 0)
        {
            return false;
        }

        var previousTagEnd = text.LastIndexOf('>', Math.Clamp(index, 0, Math.Max(0, text.Length - 1)));
        if (previousTagEnd > tagStart)
        {
            return false;
        }

        var quote = '\0';
        for (var i = tagStart + 1; i < index; i++)
        {
            if (quote == '\0')
            {
                if (text[i] is '"' or '\'')
                {
                    quote = text[i];
                }

                continue;
            }

            if (text[i] == quote)
            {
                quote = '\0';
            }
        }

        return quote != '\0';
    }

    private static DesignerSourceEditorViewTelemetrySnapshot CreateTelemetrySnapshot(bool reset)
    {
        var snapshot = new DesignerSourceEditorViewTelemetrySnapshot(
            SourceEditorTextChangedCallCount: _diagSourceEditorTextChangedCallCount,
            SourceEditorTextChangedMilliseconds: TicksToMilliseconds(_diagSourceEditorTextChangedElapsedTicks),
            SourceEditorTextChangedRefreshHighlightedCallCount: _diagSourceEditorTextChangedRefreshHighlightedCallCount,
            SourceEditorTextChangedRefreshHighlightedMilliseconds: TicksToMilliseconds(_diagSourceEditorTextChangedRefreshHighlightedElapsedTicks),
            SourceEditorTextChangedRefreshPropertyInspectorCallCount: _diagSourceEditorTextChangedRefreshPropertyInspectorCallCount,
            SourceEditorTextChangedRefreshPropertyInspectorMilliseconds: TicksToMilliseconds(_diagSourceEditorTextChangedRefreshPropertyInspectorElapsedTicks),
            SourceEditorTextChangedRefreshCompletionCallCount: _diagSourceEditorTextChangedRefreshCompletionCallCount,
            SourceEditorTextChangedRefreshCompletionMilliseconds: TicksToMilliseconds(_diagSourceEditorTextChangedRefreshCompletionElapsedTicks));

        if (reset)
        {
            _diagSourceEditorTextChangedCallCount = 0;
            _diagSourceEditorTextChangedElapsedTicks = 0;
            _diagSourceEditorTextChangedRefreshHighlightedCallCount = 0;
            _diagSourceEditorTextChangedRefreshHighlightedElapsedTicks = 0;
            _diagSourceEditorTextChangedRefreshPropertyInspectorCallCount = 0;
            _diagSourceEditorTextChangedRefreshPropertyInspectorElapsedTicks = 0;
            _diagSourceEditorTextChangedRefreshCompletionCallCount = 0;
            _diagSourceEditorTextChangedRefreshCompletionElapsedTicks = 0;
        }

        return snapshot;
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        return milliseconds.ToString("0.###");
    }

    private static bool IsWhitespaceOnlyEdit(string previousText, string currentText)
    {
        var prefixLength = 0;
        var maxPrefixLength = Math.Min(previousText.Length, currentText.Length);
        while (prefixLength < maxPrefixLength &&
               previousText[prefixLength] == currentText[prefixLength])
        {
            prefixLength++;
        }

        var previousSuffixIndex = previousText.Length - 1;
        var currentSuffixIndex = currentText.Length - 1;
        while (previousSuffixIndex >= prefixLength &&
               currentSuffixIndex >= prefixLength &&
               previousText[previousSuffixIndex] == currentText[currentSuffixIndex])
        {
            previousSuffixIndex--;
            currentSuffixIndex--;
        }

        return IsWhitespaceOnlyRange(previousText, prefixLength, previousSuffixIndex) &&
               IsWhitespaceOnlyRange(currentText, prefixLength, currentSuffixIndex);
    }

    private static bool IsWhitespaceOnlyRange(string text, int start, int end)
    {
        for (var index = start; index <= end; index++)
        {
            if (!char.IsWhiteSpace(text[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static void TryAutoIndentImmediateClosingTag(
        string previousText,
        string currentText,
        int insertedIndex,
        out string updatedText)
    {
        updatedText = currentText;

        if (insertedIndex <= 0 || insertedIndex >= currentText.Length)
        {
            return;
        }

        if (!TryGetCompletedStartTagName(previousText, insertedIndex - 1, out var tagName) ||
            !HasImmediateClosingTag(currentText, insertedIndex + 1, tagName))
        {
            return;
        }

        var lineStartIndex = GetLineStartIndex(currentText, insertedIndex);
        var indentationLength = 0;
        while (lineStartIndex + indentationLength < currentText.Length &&
               IsIndentationCharacter(currentText[lineStartIndex + indentationLength]))
        {
            indentationLength++;
        }

        if (indentationLength == 0)
        {
            return;
        }

        var indentation = currentText.Substring(lineStartIndex, indentationLength);
        updatedText = currentText.Insert(insertedIndex + 1, indentation);
    }

    private static int GetLineStartIndex(string text, int index)
    {
        var clampedIndex = Math.Clamp(index, 0, text.Length);
        while (clampedIndex > 0)
        {
            var previous = text[clampedIndex - 1];
            if (previous is '\r' or '\n')
            {
                break;
            }

            clampedIndex--;
        }

        return clampedIndex;
    }

    private static bool IsIndentationCharacter(char value)
    {
        return value is ' ' or '\t';
    }

    private static bool TryGetCompletedStartTagName(string sourceText, int closingBracketIndex, out string tagName)
    {
        tagName = string.Empty;
        if (closingBracketIndex <= 0 || closingBracketIndex >= sourceText.Length || sourceText[closingBracketIndex] != '>')
        {
            return false;
        }

        var openBracketIndex = closingBracketIndex - 1;
        while (openBracketIndex >= 0 && sourceText[openBracketIndex] != '<')
        {
            if (!IsAutoCloseTagNameCharacter(sourceText[openBracketIndex]))
            {
                return false;
            }

            openBracketIndex--;
        }

        if (openBracketIndex < 0 || openBracketIndex + 1 >= closingBracketIndex)
        {
            return false;
        }

        var firstTagCharacter = sourceText[openBracketIndex + 1];
        if (firstTagCharacter == '/' || firstTagCharacter == '!' || firstTagCharacter == '?')
        {
            return false;
        }

        tagName = sourceText.Substring(openBracketIndex + 1, closingBracketIndex - openBracketIndex - 1);
        return tagName.Length > 0;
    }

    private static bool HasImmediateClosingTag(string sourceText, int closingTagStartIndex, string tagName)
    {
        if (closingTagStartIndex < 0 || closingTagStartIndex >= sourceText.Length)
        {
            return false;
        }

        return sourceText.AsSpan(closingTagStartIndex).StartsWith(("</" + tagName + ">") .AsSpan(), StringComparison.Ordinal);
    }

    private bool TryCreateMultilinePropertyElementSuffix(
        string sourceText,
        int closingBracketIndex,
        string tagName,
        string closingTag,
        out string suffix,
        out int caretOffset)
    {
        suffix = closingTag;
        caretOffset = 0;
        if (!tagName.Contains('.', StringComparison.Ordinal))
        {
            return false;
        }

        var lineStartIndex = GetLineStartIndex(sourceText, closingBracketIndex);
        var indentationLength = 0;
        while (lineStartIndex + indentationLength < closingBracketIndex &&
               IsIndentationCharacter(sourceText[lineStartIndex + indentationLength]))
        {
            indentationLength++;
        }

        if (indentationLength == 0)
        {
            return false;
        }

        for (var index = lineStartIndex + indentationLength; index < closingBracketIndex; index++)
        {
            if (sourceText[index] == '<')
            {
                break;
            }

            if (!char.IsWhiteSpace(sourceText[index]))
            {
                return false;
            }
        }

        var indentation = sourceText.Substring(lineStartIndex, indentationLength);
        var innerIndentation = indentation + EditorIndentText;
        suffix = "\n" + innerIndentation + "\n" + indentation + closingTag;
        caretOffset = 1 + innerIndentation.Length;
        return true;
    }

    private static string GetInferredClosingTagIndentation(string sourceText, int targetTagStartIndex, int closingTagOpenIndex)
    {
        if (targetTagStartIndex < 0 ||
            targetTagStartIndex >= sourceText.Length ||
            closingTagOpenIndex < 0 ||
            closingTagOpenIndex >= sourceText.Length)
        {
            return string.Empty;
        }

        var closingLineStartIndex = GetLineStartIndex(sourceText, closingTagOpenIndex);
        for (var index = closingLineStartIndex; index < closingTagOpenIndex; index++)
        {
            if (!IsIndentationCharacter(sourceText[index]))
            {
                return string.Empty;
            }
        }

        var targetLineStartIndex = GetLineStartIndex(sourceText, targetTagStartIndex);
        var targetIndentationLength = 0;
        while (targetLineStartIndex + targetIndentationLength < sourceText.Length &&
               IsIndentationCharacter(sourceText[targetLineStartIndex + targetIndentationLength]))
        {
            targetIndentationLength++;
        }

        var closingIndentationLength = closingTagOpenIndex - closingLineStartIndex;
        if (targetIndentationLength <= closingIndentationLength)
        {
            return string.Empty;
        }

        return sourceText.Substring(
            targetLineStartIndex + closingIndentationLength,
            targetIndentationLength - closingIndentationLength);
    }

    private static bool TryFindInferredClosingTagTarget(string sourceText, int closingTagOpenIndex, out AutoCloseTagTarget target)
    {
        target = default;
        var closedAncestorDepth = 0;
        var searchIndex = closingTagOpenIndex - 1;
        while (TryFindPreviousTagForInference(sourceText, searchIndex, out var tagStartIndex, out var tagCloseIndex))
        {
            if (tagStartIndex + 1 >= sourceText.Length)
            {
                searchIndex = tagStartIndex - 1;
                continue;
            }

            var next = sourceText[tagStartIndex + 1];
            if (next is '!' or '?')
            {
                searchIndex = tagStartIndex - 1;
                continue;
            }

            if (next == '/')
            {
                var closingNameStart = tagStartIndex + 2;
                if (TryReadInferenceTagName(sourceText, closingNameStart, tagCloseIndex, out var closingName) &&
                    DesignerXmlSyntaxHighlighter.TryClassifyTagName(closingName, out _))
                {
                    closedAncestorDepth++;
                }

                searchIndex = tagStartIndex - 1;
                continue;
            }

            var nameStart = tagStartIndex + 1;
            if (!TryReadInferenceTagName(sourceText, nameStart, tagCloseIndex, out var tagName) ||
                !DesignerXmlSyntaxHighlighter.TryClassifyTagName(tagName, out _))
            {
                searchIndex = tagStartIndex - 1;
                continue;
            }

            var selfClosingSlashIndex = FindSelfClosingSlashIndex(sourceText, tagStartIndex, tagCloseIndex);
            var parsedTag = new AutoCloseTagTarget(tagName, tagStartIndex, tagCloseIndex, selfClosingSlashIndex, selfClosingSlashIndex >= 0);
            if (parsedTag.IsSelfClosing)
            {
                if (closedAncestorDepth == 0)
                {
                    target = parsedTag;
                    return true;
                }
            }
            else
            {
                if (closedAncestorDepth == 0)
                {
                    target = parsedTag;
                    return true;
                }

                closedAncestorDepth--;
            }

            searchIndex = tagStartIndex - 1;
        }

        return false;
    }

    private static bool TryFindPreviousTagForInference(string sourceText, int searchIndex, out int tagStartIndex, out int tagCloseIndex)
    {
        tagStartIndex = -1;
        tagCloseIndex = -1;

        for (var closeIndex = Math.Min(searchIndex, sourceText.Length - 1); closeIndex >= 0; closeIndex--)
        {
            if (sourceText[closeIndex] != '>')
            {
                continue;
            }

            for (var startIndex = closeIndex - 1; startIndex >= 0; startIndex--)
            {
                if (sourceText[startIndex] != '<')
                {
                    continue;
                }

                tagStartIndex = startIndex;
                tagCloseIndex = closeIndex;
                return true;
            }

            break;
        }

        return false;
    }

    private static bool TryFindTagCloseForInference(string sourceText, int tagStartIndex, out int closeIndex)
    {
        closeIndex = -1;
        char quoteCharacter = '\0';
        for (var index = tagStartIndex + 1; index < sourceText.Length; index++)
        {
            var current = sourceText[index];
            if (quoteCharacter != '\0')
            {
                if (current == quoteCharacter)
                {
                    quoteCharacter = '\0';
                }

                continue;
            }

            if (current is '\'' or '"')
            {
                quoteCharacter = current;
                continue;
            }

            if (current == '>')
            {
                closeIndex = index;
                return true;
            }
        }

        return false;
    }

    private static bool TryReadInferenceTagName(string sourceText, int startIndex, int closeIndex, out string tagName)
    {
        tagName = string.Empty;
        var index = startIndex;
        while (index < closeIndex && char.IsWhiteSpace(sourceText[index]))
        {
            index++;
        }

        var nameStart = index;
        while (index < closeIndex && IsAutoCloseTagNameCharacter(sourceText[index]))
        {
            index++;
        }

        if (index <= nameStart)
        {
            return false;
        }

        tagName = sourceText.Substring(nameStart, index - nameStart);
        return true;
    }

    private static int FindSelfClosingSlashIndex(string sourceText, int tagStartIndex, int closeIndex)
    {
        var index = closeIndex - 1;
        while (index > tagStartIndex && char.IsWhiteSpace(sourceText[index]))
        {
            index--;
        }

        return index > tagStartIndex && sourceText[index] == '/' ? index : -1;
    }

    private static bool IsAutoCloseTagNameCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value is '_' or '.' or ':';
    }

    private readonly record struct AutoCloseTagTarget(string Name, int TagStartIndex, int TagCloseIndex, int SelfClosingSlashIndex, bool IsSelfClosing);

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
