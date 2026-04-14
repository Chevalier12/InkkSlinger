using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InkkSlinger;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger.Designer;

public partial class DesignerShellView : UserControl, IAppExitRequestHandler
{
    private const string DiagnosticsTabBaseHeader = "Diagnostics";
    private const float SourceLineNumberGutterRightPadding = 6f;
    private static readonly Color DiagnosticCardHoverBackground = new(19, 33, 49);
    private static readonly Color DiagnosticCardHoverBorderBrush = new(41, 72, 102);
    private static readonly Color DiagnosticCardDefaultChrome = Color.Transparent;
    private readonly DesignerShellViewModel _viewModel;
    private readonly Action _requestAppExit;

    private string? _documentStatusOverrideText;
    private Color? _documentStatusOverrideColor;
    private bool _suppressSourceEditorChanges;
    private bool _suppressTreeSelection;
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
        ApplyControllerState();
        UpdateWorkflowPromptChrome(syncTextFromWorkflow: true);
        UpdateDocumentChrome();
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
            ClearDocumentStatusOverride();
            _viewModel.SourceText = value;
            LoadDocumentIntoEditor();
            UpdateDocumentChrome();
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

        ClearDocumentStatusOverride();
        _viewModel.SourceText = DocumentEditing.GetText(SourceEditor.Document);
        UpdateSourceLineNumberGutter(force: true);
        UpdateDocumentChrome();
    }

    private void OnVisualTreeSelectionChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;

        if (_suppressTreeSelection)
        {
            return;
        }

        var selectedItem = VisualTreeView.SelectedItem;
        _viewModel.Controller.SelectVisualNode(selectedItem?.Tag as string);
        UpdateInspectorPanel();
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

    private void ApplyControllerState()
    {
        UpdateToolbarAndPreviewText();
        UpdatePreviewHost();
        RebuildVisualTree();
        UpdateInspectorPanel();
        UpdateDiagnosticsPanel();
    }

    private void UpdateToolbarAndPreviewText()
    {
        switch (_viewModel.Controller.PreviewState)
        {
            case DesignerPreviewState.Success:
                ToolbarStatusText.Text = string.Create(
                    CultureInfo.InvariantCulture,
                    $"Refresh succeeded. Diagnostics: {_viewModel.Controller.Diagnostics.Count}.");
                ToolbarStatusText.Foreground = new Color(111, 183, 255);
                PreviewStatusText.Text = "Preview loaded from the latest manual refresh.";
                PreviewStatusText.Foreground = new Color(141, 161, 181);
                break;

            case DesignerPreviewState.Error:
                ToolbarStatusText.Text = "Refresh failed. Preview was cleared to an error state.";
                ToolbarStatusText.Foreground = new Color(255, 164, 128);
                PreviewStatusText.Text = _viewModel.Controller.PreviewFailureMessage ?? "Preview failed to load.";
                PreviewStatusText.Foreground = new Color(255, 164, 128);
                break;

            default:
                ToolbarStatusText.Text = "Refresh idle. Edit the source and press F5.";
                ToolbarStatusText.Foreground = new Color(111, 183, 255);
                PreviewStatusText.Text = "Preview is idle until you refresh.";
                PreviewStatusText.Foreground = new Color(141, 161, 181);
                break;
        }
    }

    private void UpdatePreviewHost()
    {
        PreviewHost.Content = _viewModel.Controller.PreviewState switch
        {
            DesignerPreviewState.Success => _viewModel.Controller.PreviewRoot,
            DesignerPreviewState.Error => BuildPreviewMessage(
                "Preview unavailable",
                _viewModel.Controller.PreviewFailureMessage ?? "The current XML did not load successfully.",
                new Color(255, 164, 128),
                new Color(212, 226, 238)),
            _ => BuildPreviewMessage(
                "Preview waiting",
                "The editor is decoupled from rendering in this slice. Refresh when you want to rebuild the preview.",
                new Color(111, 183, 255),
                new Color(184, 200, 214))
        };
    }

    private void RebuildVisualTree()
    {
        VisualTreeView.Items.Clear();
        if (_viewModel.Controller.VisualTreeRoot == null)
        {
            VisualTreeSummaryText.Text = _viewModel.Controller.PreviewState == DesignerPreviewState.Error
                ? "No visual tree is available because the last refresh failed."
                : "Refresh to inspect the last successful preview.";
            return;
        }

        VisualTreeSummaryText.Text = "Selecting a node updates the inspector below.";
        var rootItem = CreateTreeItem(_viewModel.Controller.VisualTreeRoot, 0);
        VisualTreeView.Items.Add(rootItem);

        _suppressTreeSelection = true;
        VisualTreeView.SelectItem(rootItem);
        _suppressTreeSelection = false;
    }

    private TreeViewItem CreateTreeItem(DesignerVisualNode node, int depth)
    {
        var item = new TreeViewItem
        {
            Header = node.Label,
            Tag = node.Id,
            Padding = new Thickness(4f, 2f, 4f, 2f),
            IsExpanded = depth < 1
        };

        for (var i = 0; i < node.Children.Count; i++)
        {
            item.Items.Add(CreateTreeItem(node.Children[i], depth + 1));
        }

        return item;
    }

    private static readonly HashSet<string> _inspectorIdentityPropertyNames =
        new(StringComparer.Ordinal)
        {
            "Node", "Type", "Name", "Visual Children", "Is Enabled",
            "Actual Size", "Desired Size"
        };

    private void UpdateInspectorPanel()
    {
        ClearPanel(InspectorPanel);
        if (_viewModel.Controller.Inspector == DesignerInspectorModel.Empty)
        {
            InspectorSummaryText.Text = "Select a visual tree node to inspect it.";
            InspectorPanel.AddChild(CreateBodyText("No inspector data is available yet.", new Color(141, 161, 181), 0f));
            return;
        }

        InspectorSummaryText.Text = _viewModel.Controller.Inspector.Header;

        var identity = _viewModel.Controller.Inspector.Properties
            .Where(static p => _inspectorIdentityPropertyNames.Contains(p.Name))
            .ToList();
        var dpProps = _viewModel.Controller.Inspector.Properties
            .Where(static p => !_inspectorIdentityPropertyNames.Contains(p.Name))
            .ToList();

        if (identity.Count > 0)
        {
            InspectorPanel.AddChild(CreateInspectorSection("Identity & Layout", identity, 0f));
        }

        if (dpProps.Count > 0)
        {
            InspectorPanel.AddChild(CreateInspectorSection("Properties", dpProps, identity.Count > 0 ? 8f : 0f));
        }
    }

    private void UpdateDiagnosticsPanel()
    {
        if (_viewModel.Controller.Diagnostics.Count == 0)
        {
            UpdateDiagnosticsTabHeader(errorCount: 0, warningCount: 0);
            DiagnosticsSummaryText.Text = _viewModel.Controller.PreviewState == DesignerPreviewState.Success
                ? "No parser diagnostics were reported during the last refresh."
                : "Parser warnings and errors appear after refresh.";
            DiagnosticsItemsControl.ItemsSource = Array.Empty<DesignerDiagnosticEntry>();
            return;
        }

        var warningCount = 0;
        var errorCount = 0;
        for (var i = 0; i < _viewModel.Controller.Diagnostics.Count; i++)
        {
            var diagnostic = _viewModel.Controller.Diagnostics[i];
            if (diagnostic.Level == DesignerDiagnosticLevel.Warning)
            {
                warningCount++;
            }
            else
            {
                errorCount++;
            }
        }

        DiagnosticsItemsControl.ItemsSource = _viewModel.Controller.Diagnostics;
        ApplyDiagnosticCardHoverChrome();

        UpdateDiagnosticsTabHeader(errorCount, warningCount);
        DiagnosticsSummaryText.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"{errorCount} error(s), {warningCount} warning(s) from the last refresh.");

        if (_viewModel.Controller.PreviewState == DesignerPreviewState.Error && errorCount > 0)
        {
            EditorTabControl.SelectedItem = DiagnosticsTab;
        }
    }

    private void ApplyDiagnosticCardHoverChrome()
    {
        var buttons = new List<Button>();
        CollectDescendants(DiagnosticsItemsControl, buttons, button => button.DataContext is DesignerDiagnosticEntry);
        foreach (var button in buttons)
        {
            button.Background = DiagnosticCardDefaultChrome;
            button.BorderBrush = DiagnosticCardDefaultChrome;
            button.AddHandler<MouseRoutedEventArgs>(UIElement.MouseEnterEvent, OnDiagnosticCardMouseEnter);
            button.AddHandler<MouseRoutedEventArgs>(UIElement.MouseLeaveEvent, OnDiagnosticCardMouseLeave);
        }
    }

    private void OnDiagnosticCardMouseEnter(object? sender, MouseRoutedEventArgs args)
    {
        _ = args;

        if (sender is not Button button)
        {
            return;
        }

        button.Background = DiagnosticCardHoverBackground;
        button.BorderBrush = DiagnosticCardHoverBorderBrush;
    }

    private void OnDiagnosticCardMouseLeave(object? sender, MouseRoutedEventArgs args)
    {
        _ = args;

        if (sender is not Button button)
        {
            return;
        }

        button.Background = DiagnosticCardDefaultChrome;
        button.BorderBrush = DiagnosticCardDefaultChrome;
    }

    private static void CollectDescendants<TElement>(UIElement root, List<TElement> matches, Func<TElement, bool>? predicate = null)
        where TElement : UIElement
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (child is TElement match && (predicate == null || predicate(match)))
            {
                matches.Add(match);
            }

            CollectDescendants(child, matches, predicate);
        }
    }

    private void UpdateDiagnosticsTabHeader(int errorCount, int warningCount)
    {
        DiagnosticsTab.Header = errorCount > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{DiagnosticsTabBaseHeader} (!{errorCount})")
            : warningCount > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{DiagnosticsTabBaseHeader} ({warningCount})")
                : DiagnosticsTabBaseHeader;
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

        ClearPanel(SourceLineNumberPanel);
        SourceLineNumberPanel.Margin = new Thickness(0f, 10f - lineOffset, SourceLineNumberGutterRightPadding, 0f);

        for (var lineIndex = 0; lineIndex < visibleLineCount; lineIndex++)
        {
            SourceLineNumberPanel.AddChild(CreateLineNumberEntry(firstVisibleLine + lineIndex + 1, lineHeight, SourceEditor.FontSize));
        }

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

    private static Border CreateLineNumberEntry(int lineNumber, float lineHeight, float fontSize)
    {
        return new Border
        {
            Height = lineHeight,
            Child = new TextBlock
            {
                Text = lineNumber.ToString(CultureInfo.InvariantCulture),
                Foreground = new Color(78, 102, 124),
                FontFamily = "Consolas",
                FontSize = fontSize,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
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

        EditorTabControl.SelectedItem = SourceTab;
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

    private void UpdateDocumentChrome()
    {
        _viewModel.RefreshCommandStates();

        if (!string.IsNullOrWhiteSpace(_documentStatusOverrideText))
        {
            DocumentStatusText.Text = _documentStatusOverrideText;
            DocumentStatusText.Foreground = _documentStatusOverrideColor ?? new Color(143, 210, 179);
            return;
        }

        var dirtySuffix = _viewModel.DocumentController.IsDirty ? "dirty" : "saved";
        var pathSuffix = string.IsNullOrWhiteSpace(_viewModel.DocumentController.CurrentPath)
            ? "memory only"
            : _viewModel.DocumentController.CurrentPath;
        DocumentStatusText.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"{_viewModel.DocumentController.DisplayName} • {dirtySuffix} • {pathSuffix}");
        DocumentStatusText.Foreground = _viewModel.DocumentController.IsDirty
            ? new Color(255, 205, 96)
            : new Color(143, 210, 179);
    }

    private void UpdateWorkflowPromptChrome(bool syncTextFromWorkflow)
    {
        var prompt = _viewModel.Workflow.Prompt;
        WorkflowPromptBorder.Visibility = prompt.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        WorkflowPromptTitleText.Text = prompt.Title;
        WorkflowPromptMessageText.Text = prompt.Message;
        DocumentPathTextBox.Visibility = prompt.ShowsPathEditor ? Visibility.Visible : Visibility.Collapsed;
        WorkflowPromptPrimaryButton.Content = prompt.ConfirmText;
        WorkflowPromptSecondaryButton.Visibility = prompt.ShowsDiscardAction ? Visibility.Visible : Visibility.Collapsed;
        WorkflowPromptCancelButton.Visibility = prompt.IsVisible ? Visibility.Visible : Visibility.Collapsed;

        if (syncTextFromWorkflow && !string.Equals(DocumentPathTextBox.Text, _viewModel.PromptPathText, StringComparison.Ordinal))
        {
            DocumentPathTextBox.Text = _viewModel.PromptPathText;
        }

        _viewModel.RefreshCommandStates();
    }

    private void HandleDocumentWorkflowResult(DesignerDocumentWorkflowResult result)
    {
        if (result.ReloadEditor)
        {
            LoadDocumentIntoEditor();
        }

        ApplyDocumentWorkflowStatus(result);
        UpdateWorkflowPromptChrome(result.PromptChanged);
        UpdateDocumentChrome();

        if (result.CloseAction == DesignerWorkflowCloseAction.RequestDeferredClose)
        {
            return;
        }
    }

    private void OnViewModelRefreshCompleted(bool succeeded)
    {
        _ = succeeded;
        ApplyControllerState();
        UpdateDocumentChrome();
    }

    private void OnViewModelWorkflowResultProduced(DesignerDocumentWorkflowResult result)
    {
        HandleDocumentWorkflowResult(result);
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

    private void ApplyDocumentWorkflowStatus(DesignerDocumentWorkflowResult result)
    {
        if (string.IsNullOrWhiteSpace(result.Message))
        {
            ClearDocumentStatusOverride();
            return;
        }

        SetDocumentStatus(
            result.Message,
            result.StatusKind switch
            {
                DesignerWorkflowStatusKind.Success => new Color(143, 210, 179),
                DesignerWorkflowStatusKind.Warning => new Color(255, 205, 96),
                DesignerWorkflowStatusKind.Error => new Color(255, 164, 128),
                _ => new Color(111, 183, 255)
            });
    }

    private void SetDocumentStatus(string message, Color color)
    {
        _documentStatusOverrideText = message;
        _documentStatusOverrideColor = color;
    }

    private void ClearDocumentStatusOverride()
    {
        _documentStatusOverrideText = null;
        _documentStatusOverrideColor = null;
    }

    private static StackPanel BuildPreviewMessage(string title, string body, Color accent, Color bodyColor)
    {
        var panel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Width = 420f
        };
        panel.AddChild(new Border
        {
            Width = 56f,
            Height = 4f,
            Background = accent,
            Margin = new Thickness(0f, 0f, 0f, 16f)
        });
        panel.AddChild(new TextBlock
        {
            Text = title,
            Foreground = new Color(231, 237, 245),
            FontSize = 24f,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        panel.AddChild(new TextBlock
        {
            Text = body,
            Foreground = bodyColor,
            Margin = new Thickness(0f, 10f, 0f, 0f),
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        return panel;
    }

    private static void ClearPanel(Panel panel)
    {
        while (panel.Children.Count > 0)
        {
            _ = panel.RemoveChildAt(panel.Children.Count - 1);
        }
    }

    private static TextBlock CreateBodyText(string text, Color color, float topMargin)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = color,
            Margin = new Thickness(0f, topMargin, 0f, 0f),
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static UIElement CreateInspectorSection(
        string title,
        IReadOnlyList<DesignerInspectorProperty> properties,
        float topMargin)
    {
        var rows = new StackPanel();

        // Section header
        rows.AddChild(new Border
        {
            Background = new Color(9, 13, 19),
            BorderBrush = new Color(20, 34, 50),
            BorderThickness = new Thickness(0f, 0f, 0f, 1f),
            Padding = new Thickness(10f, 5f, 10f, 5f),
            Child = new TextBlock
            {
                Text = title.ToUpperInvariant(),
                Foreground = new Color(74, 104, 128),
                FontSize = 11f
            }
        });

        // One compact row per property
        for (var i = 0; i < properties.Count; i++)
        {
            var prop = properties[i];
            var isLast = i == properties.Count - 1;
            var rowBg = (i % 2 == 0)
                ? new Color(11, 17, 24)
                : new Color(9, 13, 19);

            var nameBlock = new TextBlock
            {
                Text = prop.Name,
                Foreground = new Color(74, 104, 128),
                FontSize = 12f,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0f, 0f, 12f, 0f)
            };
            var valueBlock = new TextBlock
            {
                Text = prop.Value,
                Foreground = new Color(184, 216, 240),
                FontSize = 12f,
                VerticalAlignment = VerticalAlignment.Top,
                TextWrapping = TextWrapping.Wrap
            };

            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1f, GridUnitType.Star) });
            Grid.SetColumn(nameBlock, 0);
            Grid.SetColumn(valueBlock, 1);
            rowGrid.AddChild(nameBlock);
            rowGrid.AddChild(valueBlock);

            rows.AddChild(new Border
            {
                Background = rowBg,
                BorderBrush = new Color(15, 26, 38),
                BorderThickness = isLast ? Thickness.Empty : new Thickness(0f, 0f, 0f, 1f),
                Padding = new Thickness(10f, 5f, 10f, 5f),
                Child = rowGrid
            });
        }

        // Accent-left section card
        return new Border
        {
            Margin = new Thickness(0f, topMargin, 0f, 0f),
            Background = new Color(11, 17, 24),
            BorderBrush = new Color(30, 74, 120),
            BorderThickness = new Thickness(3f, 1f, 1f, 1f),
            CornerRadius = new CornerRadius(6f),
            ClipToBounds = true,
            Child = rows
        };
    }

}