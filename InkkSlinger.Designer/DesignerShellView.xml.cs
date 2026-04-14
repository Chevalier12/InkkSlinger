using System;
using System.Globalization;
using InkkSlinger;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger.Designer;

public partial class DesignerShellView : UserControl, IAppExitRequestHandler
{
    private const float SourceLineNumberGutterRightPadding = 6f;
    private readonly DesignerShellViewModel _viewModel;
    private readonly Action _requestAppExit;

    private bool _suppressSourceEditorChanges;
    private int _lastRenderedSourceLineCount = -1;
    private int _lastRenderedSourceFirstVisibleLine = -1;
    private int _lastRenderedSourceVisibleLineCount = -1;
    private float _lastRenderedSourceLineOffset = float.NaN;
    private float _lastRenderedSourceLineHeight = float.NaN;

    public DesignerShellView(
        DesignerDocumentController? documentController = null,
        DesignerDocumentWorkflowController? workflow = null,
        Action? requestAppExit = null)
    {
        InitializeComponent();
        _requestAppExit = requestAppExit ?? DefaultRequestAppExit;
        _viewModel = new DesignerShellViewModel(documentController: documentController, workflow: workflow);
        DataContext = _viewModel;
        _viewModel.RefreshCompleted += OnViewModelRefreshCompleted;
        _viewModel.WorkflowResultProduced += OnViewModelWorkflowResultProduced;
        _viewModel.DiagnosticNavigationRequested += OnViewModelDiagnosticNavigationRequested;
        _viewModel.DeferredAppExitRequested += OnViewModelDeferredAppExitRequested;
        InputBindings.Add(new KeyBinding
        {
            Key = Keys.F5,
            Modifiers = ModifierKeys.None,
            Command = _viewModel.RefreshCommand,
            CommandTarget = this
        });

        LoadDocumentIntoEditor();
        UpdateSourceLineNumberGutter(force: true);
    }

    public DesignerShellViewModel ViewModel => _viewModel;

    public DesignerController Controller => _viewModel.Controller;

    public DesignerDocumentController DocumentController => _viewModel.DocumentController;

    public string SourceText
    {
        get => _viewModel.SourceText;
        set
        {
            _viewModel.SourceText = value;
            LoadDocumentIntoEditor();
        }
    }

    public bool RefreshPreview()
    {
        return _viewModel.RefreshPreview();
    }

    public bool TryRequestAppExit()
    {
        return _viewModel.TryRequestAppExit();
    }

    private void OnSourceEditorTextChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;

        if (_suppressSourceEditorChanges)
        {
            return;
        }

        _viewModel.SourceText = DocumentEditing.GetText(SourceEditor.Document);
        UpdateSourceLineNumberGutter(force: true);
    }

    private void OnSourceEditorLayoutUpdated(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;

        UpdateSourceLineNumberGutter(force: false);
    }

    private void OnDocumentPathTextChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;

        _viewModel.PromptPathText = DocumentPathTextBox.Text;
    }

    private void OnSourceEditorViewportChanged(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;

        UpdateSourceLineNumberGutter(force: false);
    }

    private void LoadDocumentIntoEditor()
    {
        var selectionStart = SourceEditor.SelectionStart;
        var selectionLength = SourceEditor.SelectionLength;
        var horizontalOffset = SourceEditor.HorizontalOffset;
        var verticalOffset = SourceEditor.VerticalOffset;

        _suppressSourceEditorChanges = true;
        try
        {
            DesignerXmlSyntaxHighlighter.PopulateDocument(SourceEditor.Document, _viewModel.DocumentController.CurrentText);

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

    private void UpdateSourceLineNumberGutter(bool force)
    {
        var sourceText = _viewModel.DocumentController.CurrentText;
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

    private void NavigateToDiagnostic(object? parameter)
    {
        if (parameter is not DesignerDiagnosticEntry diagnostic || !diagnostic.Line.HasValue)
        {
            return;
        }

        var sourceText = DocumentEditing.GetText(SourceEditor.Document);
        if (!TryGetLineSelectionRange(sourceText, diagnostic.Line.Value, out var selectionStart, out var selectionLength))
        {
            return;
        }

        _viewModel.SelectedEditorTabIndex = 0;
        FocusManager.SetFocus(SourceEditor);
        SourceEditor.Select(selectionStart, selectionLength);
        SourceEditor.ScrollToHorizontalOffset(0f);

        var lineHeight = EstimateSourceEditorLineHeight(CountSourceLines(sourceText));
        var desiredVerticalOffset = Math.Max(0f, ((diagnostic.Line.Value - 1) * lineHeight) - (SourceEditor.ViewportHeight * 0.35f));
        SourceEditor.ScrollToVerticalOffset(desiredVerticalOffset);
        UpdateSourceLineNumberGutter(force: true);
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

    private void OnViewModelRefreshCompleted(bool succeeded)
    {
        _ = succeeded;
        UpdateSourceLineNumberGutter(force: true);
    }

    private void OnViewModelWorkflowResultProduced(DesignerDocumentWorkflowResult result)
    {
        if (result.ReloadEditor)
        {
            LoadDocumentIntoEditor();
        }

        UpdateSourceLineNumberGutter(force: true);
    }

    private void OnViewModelDiagnosticNavigationRequested(DesignerDiagnosticEntry diagnostic)
    {
        NavigateToDiagnostic(diagnostic);
    }

    private void OnViewModelDeferredAppExitRequested()
    {
        _requestAppExit();
    }

    private static void DefaultRequestAppExit()
    {
        if (UiApplication.Current.HasMainWindow)
        {
            UiApplication.Current.Shutdown();
        }
    }

}