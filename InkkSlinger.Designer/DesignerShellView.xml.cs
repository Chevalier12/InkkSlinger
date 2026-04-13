using System;
using System.Globalization;
using InkkSlinger;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger.Designer;

public partial class DesignerShellView : UserControl, IAppExitRequestHandler
{
    private const string DefaultSourceText = """
        <UserControl xmlns="urn:inkkslinger-ui"
                     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                     Background="#111827">
          <Grid Margin="24">
            <Grid.RowDefinitions>
              <RowDefinition Height="Auto" />
              <RowDefinition Height="18" />
              <RowDefinition Height="*" />
            </Grid.RowDefinitions>

            <TextBlock Text="Designer Preview"
                       Foreground="#E7EDF5"
                       FontSize="22"
                       FontWeight="SemiBold" />

            <Border Grid.Row="2"
                    Background="#182230"
                    BorderBrush="#35506B"
                    BorderThickness="1"
                    CornerRadius="12"
                    Padding="18">
              <StackPanel>
                <TextBlock Text="Manual refresh is enabled."
                           Foreground="#E7EDF5"
                           FontSize="18" />
                <TextBlock Text="Edit the XML below, then press F5 or the toolbar button."
                           Foreground="#8AA3B8"
                           Margin="0,6,0,12" />
                <Button x:Name="PreviewButton"
                        Content="Preview Action"
                        Width="180"
                        Height="40"
                        Background="#1F8EFA"
                        BorderBrush="#56A7F7"
                        BorderThickness="1" />
              </StackPanel>
            </Border>
          </Grid>
        </UserControl>
        """;

    private readonly DesignerController _controller;
    private readonly DesignerDocumentController _documentController;
    private readonly DesignerDocumentWorkflowController _workflow;
    private readonly Action _requestAppExit;
    private readonly RelayCommand _newCommand;
    private readonly RelayCommand _openCommand;
    private readonly RelayCommand _saveCommand;
    private readonly RelayCommand _saveAsCommand;
    private readonly RelayCommand _refreshCommand;
    private readonly RelayCommand _promptPrimaryCommand;
    private readonly RelayCommand _promptSecondaryCommand;
    private readonly RelayCommand _promptCancelCommand;
    private readonly Button _newButton;
    private readonly Button _openButton;
    private readonly Button _saveButton;
    private readonly Button _saveAsButton;
    private readonly Button _refreshButton;
    private readonly Border _workflowPromptBorder;
    private readonly TextBlock _workflowPromptTitleText;
    private readonly TextBlock _workflowPromptMessageText;
    private readonly Button _workflowPromptPrimaryButton;
    private readonly Button _workflowPromptSecondaryButton;
    private readonly Button _workflowPromptCancelButton;
    private readonly TextBox _documentPathTextBox;
    private readonly ContentControl _previewHost;
    private readonly RichTextBox _sourceEditor;
    private readonly TreeView _visualTreeView;
    private readonly StackPanel _inspectorPanel;
    private readonly StackPanel _diagnosticsPanel;
    private readonly TextBlock _toolbarStatusText;
    private readonly TextBlock _documentStatusText;
    private readonly TextBlock _previewStatusText;
    private readonly TextBlock _visualTreeSummaryText;
    private readonly TextBlock _inspectorSummaryText;
    private readonly TextBlock _diagnosticsSummaryText;

    private string? _documentStatusOverrideText;
    private Color? _documentStatusOverrideColor;
    private bool _suppressSourceEditorChanges;
    private bool _suppressTreeSelection;

    public DesignerShellView(
        DesignerDocumentController? documentController = null,
        DesignerDocumentWorkflowController? workflow = null,
        Action? requestAppExit = null)
    {
        InitializeComponent();

        _controller = new DesignerController();
        _documentController = documentController ?? new DesignerDocumentController(DefaultSourceText);
        _workflow = workflow ?? new DesignerDocumentWorkflowController(_documentController);
        _requestAppExit = requestAppExit ?? DefaultRequestAppExit;
        _refreshCommand = new RelayCommand(_ => RefreshPreview());
        _newCommand = new RelayCommand(_ => ExecuteNewDocument());
        _openCommand = new RelayCommand(_ => ExecuteOpenDocument());
        _saveCommand = new RelayCommand(_ => ExecuteSaveDocument(), _ => CanSaveDocument());
        _saveAsCommand = new RelayCommand(_ => ExecuteSaveDocumentAs());
        _promptPrimaryCommand = new RelayCommand(_ => ExecutePromptPrimary(), _ => _workflow.Prompt.IsVisible);
        _promptSecondaryCommand = new RelayCommand(_ => ExecutePromptSecondary(), _ => _workflow.Prompt.ShowsDiscardAction);
        _promptCancelCommand = new RelayCommand(_ => ExecutePromptCancel(), _ => _workflow.Prompt.IsVisible);
        _newButton = RequireElement<Button>("NewButton");
        _openButton = RequireElement<Button>("OpenButton");
        _saveButton = RequireElement<Button>("SaveButton");
        _saveAsButton = RequireElement<Button>("SaveAsButton");
        _refreshButton = RequireElement<Button>("RefreshButton");
        _workflowPromptBorder = RequireElement<Border>("WorkflowPromptBorder");
        _workflowPromptTitleText = RequireElement<TextBlock>("WorkflowPromptTitleText");
        _workflowPromptMessageText = RequireElement<TextBlock>("WorkflowPromptMessageText");
        _workflowPromptPrimaryButton = RequireElement<Button>("WorkflowPromptPrimaryButton");
        _workflowPromptSecondaryButton = RequireElement<Button>("WorkflowPromptSecondaryButton");
        _workflowPromptCancelButton = RequireElement<Button>("WorkflowPromptCancelButton");
        _documentPathTextBox = RequireElement<TextBox>("DocumentPathTextBox");
        _previewHost = RequireElement<ContentControl>("PreviewHost");
        _sourceEditor = RequireElement<RichTextBox>("SourceEditor");
        _visualTreeView = RequireElement<TreeView>("VisualTreeView");
        _inspectorPanel = RequireElement<StackPanel>("InspectorPanel");
        _diagnosticsPanel = RequireElement<StackPanel>("DiagnosticsPanel");
        _toolbarStatusText = RequireElement<TextBlock>("ToolbarStatusText");
        _documentStatusText = RequireElement<TextBlock>("DocumentStatusText");
        _previewStatusText = RequireElement<TextBlock>("PreviewStatusText");
        _visualTreeSummaryText = RequireElement<TextBlock>("VisualTreeSummaryText");
        _inspectorSummaryText = RequireElement<TextBlock>("InspectorSummaryText");
        _diagnosticsSummaryText = RequireElement<TextBlock>("DiagnosticsSummaryText");

        _newButton.Command = _newCommand;
        _newButton.CommandTarget = this;
        _openButton.Command = _openCommand;
        _openButton.CommandTarget = this;
        _saveButton.Command = _saveCommand;
        _saveButton.CommandTarget = this;
        _saveAsButton.Command = _saveAsCommand;
        _saveAsButton.CommandTarget = this;
        _refreshButton.Command = _refreshCommand;
        _refreshButton.CommandTarget = this;
        _workflowPromptPrimaryButton.Command = _promptPrimaryCommand;
        _workflowPromptPrimaryButton.CommandTarget = this;
        _workflowPromptSecondaryButton.Command = _promptSecondaryCommand;
        _workflowPromptSecondaryButton.CommandTarget = this;
        _workflowPromptCancelButton.Command = _promptCancelCommand;
        _workflowPromptCancelButton.CommandTarget = this;
        _sourceEditor.TextChanged += OnSourceEditorTextChanged;
        _visualTreeView.SelectedItemChanged += OnVisualTreeSelectionChanged;
        InputBindings.Add(new KeyBinding
        {
            Key = Keys.F5,
            Modifiers = ModifierKeys.None,
            Command = _refreshCommand,
            CommandTarget = this
        });

        LoadDocumentIntoEditor();
        ApplyControllerState();
        UpdateWorkflowPromptChrome(syncTextFromWorkflow: true);
        UpdateDocumentChrome();
    }

    public DesignerController Controller => _controller;

    public DesignerDocumentController DocumentController => _documentController;

    public string SourceText
    {
        get => _documentController.CurrentText;
        set
        {
            ClearDocumentStatusOverride();
            _documentController.UpdateText(value);
            LoadDocumentIntoEditor();
            UpdateDocumentChrome();
        }
    }

    public bool RefreshPreview()
    {
        SyncEditorTextIntoDocumentController();
        var succeeded = _controller.Refresh(_documentController.CurrentText);
        ApplyControllerState();
        UpdateDocumentChrome();
        return succeeded;
    }

    public bool TryRequestAppExit()
    {
        SyncEditorTextIntoDocumentController();
        var result = _workflow.BeginClose();
        HandleDocumentWorkflowResult(result);
        return result.CloseAction == DesignerWorkflowCloseAction.AllowCurrentRequest;
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
        SyncEditorTextIntoDocumentController();
        LoadDocumentIntoEditor();
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

        var selectedItem = _visualTreeView.SelectedItem;
        _controller.SelectVisualNode(selectedItem?.Tag as string);
        UpdateInspectorPanel();
    }

    private void ApplyControllerState()
    {
        UpdateToolbarAndPreviewText();
        UpdatePreviewHost();
        RebuildVisualTree();
        UpdateInspectorPanel();
        UpdateDiagnosticsPanel();
    }

    private void ExecuteNewDocument()
    {
        SyncEditorTextIntoDocumentController();
        HandleDocumentWorkflowResult(_workflow.BeginNew());
    }

    private void ExecuteOpenDocument()
    {
        SyncEditorTextIntoDocumentController();
        HandleDocumentWorkflowResult(_workflow.BeginOpen());
    }

    private void ExecuteSaveDocument()
    {
        SyncEditorTextIntoDocumentController();
        HandleDocumentWorkflowResult(_workflow.Save());
    }

    private void ExecuteSaveDocumentAs()
    {
        SyncEditorTextIntoDocumentController();
        HandleDocumentWorkflowResult(_workflow.BeginSaveAs());
    }

    private void ExecutePromptPrimary()
    {
        SyncEditorTextIntoDocumentController();
        var result = _workflow.Prompt.Kind switch
        {
            DesignerDocumentPromptKind.UnsavedChanges => _workflow.ResolveUnsavedChanges(DesignerUnsavedChangesChoice.Save),
            DesignerDocumentPromptKind.OverwriteConfirmation => _workflow.ConfirmOverwriteSavePath(),
            _ => _workflow.SubmitPromptPath(_documentPathTextBox.Text)
        };
        HandleDocumentWorkflowResult(result);
    }

    private void ExecutePromptSecondary()
    {
        SyncEditorTextIntoDocumentController();
        HandleDocumentWorkflowResult(_workflow.ResolveUnsavedChanges(DesignerUnsavedChangesChoice.Discard));
    }

    private void ExecutePromptCancel()
    {
        HandleDocumentWorkflowResult(_workflow.CancelPrompt());
    }

    private bool CanSaveDocument()
    {
        return _documentController.IsDirty || !string.IsNullOrWhiteSpace(_documentController.CurrentPath);
    }

    private void UpdateToolbarAndPreviewText()
    {
        switch (_controller.PreviewState)
        {
            case DesignerPreviewState.Success:
                _toolbarStatusText.Text = string.Create(
                    CultureInfo.InvariantCulture,
                    $"Refresh succeeded. Diagnostics: {_controller.Diagnostics.Count}.");
                _toolbarStatusText.Foreground = new Color(111, 183, 255);
                _previewStatusText.Text = "Preview loaded from the latest manual refresh.";
                _previewStatusText.Foreground = new Color(141, 161, 181);
                break;

            case DesignerPreviewState.Error:
                _toolbarStatusText.Text = "Refresh failed. Preview was cleared to an error state.";
                _toolbarStatusText.Foreground = new Color(255, 164, 128);
                _previewStatusText.Text = _controller.PreviewFailureMessage ?? "Preview failed to load.";
                _previewStatusText.Foreground = new Color(255, 164, 128);
                break;

            default:
                _toolbarStatusText.Text = "Refresh idle. Edit the source and press F5.";
                _toolbarStatusText.Foreground = new Color(111, 183, 255);
                _previewStatusText.Text = "Preview is idle until you refresh.";
                _previewStatusText.Foreground = new Color(141, 161, 181);
                break;
        }
    }

    private void UpdatePreviewHost()
    {
        _previewHost.Content = _controller.PreviewState switch
        {
            DesignerPreviewState.Success => _controller.PreviewRoot,
            DesignerPreviewState.Error => BuildPreviewMessage(
                "Preview unavailable",
                _controller.PreviewFailureMessage ?? "The current XML did not load successfully.",
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
        _visualTreeView.Items.Clear();
        if (_controller.VisualTreeRoot == null)
        {
            _visualTreeSummaryText.Text = _controller.PreviewState == DesignerPreviewState.Error
                ? "No visual tree is available because the last refresh failed."
                : "Refresh to inspect the last successful preview.";
            return;
        }

        _visualTreeSummaryText.Text = "Selecting a node updates the inspector below.";
        var rootItem = CreateTreeItem(_controller.VisualTreeRoot, 0);
        _visualTreeView.Items.Add(rootItem);

        _suppressTreeSelection = true;
        _visualTreeView.SelectItem(rootItem);
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

    private void UpdateInspectorPanel()
    {
        ClearPanel(_inspectorPanel);
        if (_controller.Inspector == DesignerInspectorModel.Empty)
        {
            _inspectorSummaryText.Text = "Select a visual tree node to inspect it.";
            _inspectorPanel.AddChild(CreateBodyText("No inspector data is available yet.", new Color(141, 161, 181), 0f));
            return;
        }

        _inspectorSummaryText.Text = _controller.Inspector.Header;
        for (var i = 0; i < _controller.Inspector.Properties.Count; i++)
        {
            var property = _controller.Inspector.Properties[i];
            _inspectorPanel.AddChild(CreateInspectorPropertyRow(property, i == 0 ? 0f : 8f));
        }
    }

    private void UpdateDiagnosticsPanel()
    {
        ClearPanel(_diagnosticsPanel);
        if (_controller.Diagnostics.Count == 0)
        {
            _diagnosticsSummaryText.Text = _controller.PreviewState == DesignerPreviewState.Success
                ? "No parser diagnostics were reported during the last refresh."
                : "Parser warnings and errors appear after refresh.";
            _diagnosticsPanel.AddChild(CreateBodyText("Diagnostics will populate here after a manual refresh.", new Color(141, 161, 181), 0f));
            return;
        }

        var warningCount = 0;
        var errorCount = 0;
        for (var i = 0; i < _controller.Diagnostics.Count; i++)
        {
            var diagnostic = _controller.Diagnostics[i];
            if (diagnostic.Level == DesignerDiagnosticLevel.Warning)
            {
                warningCount++;
            }
            else
            {
                errorCount++;
            }

            _diagnosticsPanel.AddChild(CreateDiagnosticCard(diagnostic, i == 0 ? 0f : 10f));
        }

        _diagnosticsSummaryText.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"{errorCount} error(s), {warningCount} warning(s) from the last refresh.");
    }

    private void LoadDocumentIntoEditor()
    {
        _suppressSourceEditorChanges = true;
        try
        {
            DesignerXmlSyntaxHighlighter.PopulateDocument(_sourceEditor.Document, _documentController.CurrentText);
        }
        finally
        {
            _suppressSourceEditorChanges = false;
        }
    }

    private void SyncEditorTextIntoDocumentController()
    {
        _documentController.UpdateText(DocumentEditing.GetText(_sourceEditor.Document));
    }

    private void UpdateDocumentChrome()
    {
        _saveCommand.RaiseCanExecuteChanged();

        if (!string.IsNullOrWhiteSpace(_documentStatusOverrideText))
        {
            _documentStatusText.Text = _documentStatusOverrideText;
            _documentStatusText.Foreground = _documentStatusOverrideColor ?? new Color(143, 210, 179);
            return;
        }

        var dirtySuffix = _documentController.IsDirty ? "dirty" : "saved";
        var pathSuffix = string.IsNullOrWhiteSpace(_documentController.CurrentPath)
            ? "memory only"
            : _documentController.CurrentPath;
        _documentStatusText.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"{_documentController.DisplayName} • {dirtySuffix} • {pathSuffix}");
        _documentStatusText.Foreground = _documentController.IsDirty
            ? new Color(255, 205, 96)
            : new Color(143, 210, 179);
    }

    private void UpdateWorkflowPromptChrome(bool syncTextFromWorkflow)
    {
        var prompt = _workflow.Prompt;
        _workflowPromptBorder.Visibility = prompt.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        _workflowPromptTitleText.Text = prompt.Title;
        _workflowPromptMessageText.Text = prompt.Message;
        _documentPathTextBox.Visibility = prompt.ShowsPathEditor ? Visibility.Visible : Visibility.Collapsed;
        _workflowPromptPrimaryButton.Content = prompt.ConfirmText;
        _workflowPromptSecondaryButton.Visibility = prompt.ShowsDiscardAction ? Visibility.Visible : Visibility.Collapsed;
        _workflowPromptCancelButton.Visibility = prompt.IsVisible ? Visibility.Visible : Visibility.Collapsed;

        if (syncTextFromWorkflow)
        {
            _documentPathTextBox.Text = prompt.PathText ?? string.Empty;
        }

        _promptPrimaryCommand.RaiseCanExecuteChanged();
        _promptSecondaryCommand.RaiseCanExecuteChanged();
        _promptCancelCommand.RaiseCanExecuteChanged();
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
            _requestAppExit();
        }
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

    private static Border CreateInspectorPropertyRow(DesignerInspectorProperty property, float topMargin)
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(0f, topMargin, 0f, 0f)
        };
        panel.AddChild(new TextBlock
        {
            Text = property.Name,
            Foreground = new Color(141, 161, 181),
            FontSize = 13f
        });
        panel.AddChild(new TextBlock
        {
            Text = property.Value,
            Foreground = new Color(216, 227, 238),
            Margin = new Thickness(0f, 2f, 0f, 0f),
            TextWrapping = TextWrapping.Wrap
        });

        return new Border
        {
            Background = new Color(16, 24, 33, 220),
            BorderBrush = new Color(43, 57, 73),
            BorderThickness = new Thickness(1f),
            CornerRadius = new CornerRadius(8f),
            Padding = new Thickness(10f),
            Child = panel
        };
    }

    private static Border CreateDiagnosticCard(DesignerDiagnosticEntry diagnostic, float topMargin)
    {
        var severityColor = diagnostic.Level == DesignerDiagnosticLevel.Warning
            ? new Color(255, 205, 96)
            : new Color(255, 164, 128);

        var panel = new StackPanel();
        panel.AddChild(new TextBlock
        {
            Foreground = severityColor,
            Text = string.Create(CultureInfo.InvariantCulture, $"[{diagnostic.Level}] {diagnostic.Code}"),
            FontSize = 13f
        });
        panel.AddChild(new TextBlock
        {
            Text = diagnostic.Message,
            Foreground = new Color(231, 237, 245),
            Margin = new Thickness(0f, 4f, 0f, 0f),
            TextWrapping = TextWrapping.Wrap
        });
        panel.AddChild(new TextBlock
        {
            Text = string.Create(CultureInfo.InvariantCulture, $"{diagnostic.TargetDescription} • {diagnostic.LocationText}"),
            Foreground = new Color(141, 161, 181),
            Margin = new Thickness(0f, 4f, 0f, 0f),
            TextWrapping = TextWrapping.Wrap
        });

        if (!string.IsNullOrWhiteSpace(diagnostic.Hint))
        {
            panel.AddChild(new TextBlock
            {
                Text = string.Create(CultureInfo.InvariantCulture, $"Hint: {diagnostic.Hint}"),
                Foreground = new Color(184, 200, 214),
                Margin = new Thickness(0f, 4f, 0f, 0f),
                TextWrapping = TextWrapping.Wrap
            });
        }

        return new Border
        {
            Background = new Color(16, 24, 33, 220),
            BorderBrush = severityColor,
            BorderThickness = new Thickness(1f),
            CornerRadius = new CornerRadius(8f),
            Padding = new Thickness(10f),
            Margin = new Thickness(0f, topMargin, 0f, 0f),
            Child = panel
        };
    }

    private T RequireElement<T>(string name)
        where T : class
    {
        return this.FindName(name) as T
            ?? throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"Missing required element '{name}'."));
    }
}