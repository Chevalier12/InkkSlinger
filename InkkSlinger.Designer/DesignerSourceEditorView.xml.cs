using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InkkSlinger;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger.Designer;

public partial class DesignerSourceEditorView : UserControl
{
    private const float SourceLineNumberGutterRightPadding = 6f;
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

    private bool _suppressSourceEditorChanges;
    private int _lastRenderedSourceLineCount = -1;
    private int _lastRenderedSourceFirstVisibleLine = -1;
    private int _lastRenderedSourceVisibleLineCount = -1;
    private float _lastRenderedSourceLineOffset = float.NaN;
    private float _lastRenderedSourceLineHeight = float.NaN;
    private readonly Popup _completionPopup;
    private readonly ListBox _completionListBox;
    private IReadOnlyList<DesignerControlCompletionItem> _completionItems = Array.Empty<DesignerControlCompletionItem>();
    private IReadOnlyList<string> _completionItemNames = Array.Empty<string>();
    private CompletionContext? _completionContext;
    private bool _suppressCompletionListSelectionChanged;
    private LayoutRect _lastCompletionCaretBounds;

    public DesignerSourceEditorView()
    {
        InitializeComponent();
        (_completionPopup, _completionListBox) = CreateCompletionPopup();
        _completionPopup.Closed += OnCompletionPopupClosed;
        _completionListBox.SelectionChanged += OnCompletionListSelectionChanged;
        _completionListBox.AddHandler<MouseRoutedEventArgs>(UIElement.MouseUpEvent, OnCompletionListMouseUp, handledEventsToo: true);
        SourceEditor.AddHandler<KeyRoutedEventArgs>(UIElement.KeyDownEvent, OnSourceEditorKeyDown, handledEventsToo: true);
        SourceEditor.AddHandler<FocusChangedRoutedEventArgs>(UIElement.LostFocusEvent, OnSourceEditorLostFocus, handledEventsToo: true);
        SourceEditor.SelectionChanged += OnSourceEditorSelectionChanged;
        LoadDocumentIntoEditor(SourceText);
        UpdateSourceLineNumberGutter(force: true);
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

    public Border LineNumberBorder => SourceLineNumberBorder;

    public ItemsControl LineNumberPanel => SourceLineNumberPanel;

    public bool IsControlCompletionOpen => _completionPopup.IsOpen;

    public IReadOnlyList<string> ControlCompletionItems => _completionItemNames;

    public int ControlCompletionSelectedIndex => _completionListBox.SelectedIndex;

    public LayoutRect ControlCompletionBounds => _completionPopup.LayoutSlot;

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

        UpdateSourceLineNumberGutter(force: true);
        if (IsControlCompletionOpen)
        {
            _ = RefreshCompletionPopup(openIfPossible: false);
        }
    }

    private void OnSourceEditorLayoutUpdated(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        UpdateSourceLineNumberGutter(force: false);
    }

    private void OnSourceEditorViewportChanged(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        UpdateSourceLineNumberGutter(force: false);
        UpdateCompletionPopupPlacement();
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
    }

    private void LoadDocumentIntoEditor(string? text)
    {
        DismissCompletionPopup();
        var selectionStart = SourceEditor.SelectionStart;
        var selectionLength = SourceEditor.SelectionLength;
        var horizontalOffset = SourceEditor.HorizontalOffset;
        var verticalOffset = SourceEditor.VerticalOffset;

        _suppressSourceEditorChanges = true;
        try
        {
            DesignerXmlSyntaxHighlighter.PopulateDocument(SourceEditor.Document, text ?? string.Empty);

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

        var lineHeight = EstimateSourceEditorLineHeight(CountSourceLines(sourceText));
        var desiredVerticalOffset = Math.Max(0f, ((oneBasedLineNumber - 1) * lineHeight) - (SourceEditor.ViewportHeight * 0.35f));
        SourceEditor.ScrollToVerticalOffset(desiredVerticalOffset);
        UpdateSourceLineNumberGutter(force: true);
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

    private (Popup Popup, ListBox ListBox) CreateCompletionPopup()
    {
        var listBox = new ListBox
        {
            Style = (Style)FindResource("CompletionListBoxStyle")
        };

        var popup = new Popup
        {
            Style = (Style)FindResource("CompletionPopupStyle"),
            Content = listBox,
            MaxHeight = CompletionPopupMaxHeight
        };

        return (popup, listBox);
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
        else if (_completionListBox.SelectedIndex < 0 && _completionItems.Count > 0)
        {
            SetCompletionSelection(0);
        }

        if (!_completionPopup.IsOpen)
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

            _completionPopup.Width = float.NaN;
            _completionPopup.Height = float.NaN;
            _completionPopup.MaxHeight = CompletionPopupMaxHeight;
            _completionPopup.Open(host);
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
            _completionListBox.ItemsSource = _completionItems;
            _completionListBox.SelectedIndex = _completionItems.Count > 0 ? 0 : -1;
        }
        finally
        {
            _suppressCompletionListSelectionChanged = false;
        }
    }

    private void UpdateCompletionPopupPlacement()
    {
        if (!_completionPopup.IsOpen)
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

        if (!_completionPopup.TrySetRootSpacePosition(caretBounds.X, caretBounds.Y + caretBounds.Height + CompletionPopupVerticalOffset))
        {
            DismissCompletionPopup();
            return;
        }

        _lastCompletionCaretBounds = caretBounds;
    }

    private void DismissCompletionPopup()
    {
        if (_completionPopup.IsOpen)
        {
            _completionPopup.Close();
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
            _completionListBox.ItemsSource = null;
            _completionListBox.SelectedIndex = -1;
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

        var selectedIndex = _completionListBox.SelectedIndex;
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
            _completionListBox.SelectedIndex = clamped;
            _completionListBox.ScrollIntoView(_completionListBox.SelectedItem);
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

        var selectedIndex = _completionListBox.SelectedIndex;
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
            if (ReferenceEquals(current, _completionPopup))
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

    private void UpdateSourceLineNumberGutter(bool force)
    {
        var sourceText = DocumentEditing.GetText(SourceEditor.Document);
        var lineCount = CountSourceLines(sourceText);
        var lineHeight = EstimateSourceEditorLineHeight(lineCount);
        var viewportHeight = Math.Max(lineHeight, SourceEditor.ViewportHeight);
        var verticalOffset = Math.Max(0f, SourceEditor.VerticalOffset);
        var approximateVisibleLineCount = Math.Clamp((int)MathF.Ceiling(viewportHeight / lineHeight) + 1, 1, Math.Max(1, lineCount));
        var firstVisibleLine = GetFirstVisibleSourceLine(sourceText, lineCount, approximateVisibleLineCount, lineHeight, verticalOffset);
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

        SourceLineNumberPanel.Margin = new Thickness(0f, 10f - lineOffset, SourceLineNumberGutterRightPadding, 0f);
        SourceLineNumberPanel.ItemsSource = BuildSourceLineNumberEntries(firstVisibleLine, visibleLineCount, lineHeight, SourceEditor.FontSize);

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
        if (lineCount > 0 && SourceEditor.ExtentHeight > 0.01f)
        {
            return Math.Max(1f, SourceEditor.ExtentHeight / lineCount);
        }

        return Math.Max(1f, SourceEditor.FontSize * 1.35f);
    }

    private int GetFirstVisibleSourceLine(string sourceText, int lineCount, int approximateVisibleLineCount, float lineHeight, float verticalOffset)
    {
        var topVisibleTextPoint = new Vector2(
            SourceEditor.LayoutSlot.X + SourceEditor.BorderThickness + SourceEditor.Padding.Left + 1f,
            SourceEditor.LayoutSlot.Y + SourceEditor.BorderThickness + SourceEditor.Padding.Top + 1f);
        var firstVisiblePosition = SourceEditor.GetPositionFromPoint(topVisibleTextPoint, snapToText: true);
        var firstVisibleLineFromHitTest = 0;
        if (firstVisiblePosition.HasValue)
        {
            var documentOffset = Math.Clamp(DocumentPointers.GetDocumentOffset(firstVisiblePosition.Value), 0, sourceText.Length);
            var lineIndex = 0;
            for (var i = 0; i < documentOffset; i++)
            {
                if (sourceText[i] == '\n')
                {
                    lineIndex++;
                }
            }

            firstVisibleLineFromHitTest = Math.Clamp(lineIndex, 0, Math.Max(0, lineCount - 1));
        }

        var firstVisibleLineFromScroll = Math.Clamp((int)MathF.Floor(verticalOffset / lineHeight), 0, Math.Max(0, lineCount - 1));
        var scrollableLineCount = Math.Max(0, lineCount - approximateVisibleLineCount);
        if (scrollableLineCount > 0 && SourceEditor.ScrollableHeight > 0.01f)
        {
            firstVisibleLineFromScroll = Math.Clamp(
                (int)MathF.Round((verticalOffset / SourceEditor.ScrollableHeight) * scrollableLineCount),
                0,
                Math.Max(0, lineCount - 1));
        }

        return Math.Max(firstVisibleLineFromHitTest, firstVisibleLineFromScroll);
    }

    private static DesignerSourceLineNumberViewModel[] BuildSourceLineNumberEntries(int firstVisibleLine, int visibleLineCount, float lineHeight, float fontSize)
    {
        var entries = new DesignerSourceLineNumberViewModel[visibleLineCount];
        for (var lineIndex = 0; lineIndex < visibleLineCount; lineIndex++)
        {
            entries[lineIndex] = new DesignerSourceLineNumberViewModel(
                (firstVisibleLine + lineIndex + 1).ToString(CultureInfo.InvariantCulture),
                lineHeight,
                fontSize);
        }

        return entries;
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

    private readonly record struct CompletionContext(int OpenBracketIndex, int ReplaceStart, int ReplaceLength, string Prefix);
}