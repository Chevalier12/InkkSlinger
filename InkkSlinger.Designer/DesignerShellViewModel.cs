using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using InkkSlinger;
using Microsoft.Xna.Framework;

namespace InkkSlinger.Designer;

public sealed class DesignerShellViewModel : INotifyPropertyChanged
{
    private static readonly HashSet<string> InspectorIdentityPropertyNames =
        new(StringComparer.Ordinal)
        {
            "Node", "Type", "Name", "Visual Children", "Is Enabled",
            "Actual Size", "Desired Size"
        };

    private const string DefaultSourceText =
        "<UserControl xmlns=\"urn:inkkslinger-ui\"\n" +
        "             xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"\n" +
        "             Background=\"#111827\">\n" +
        "    <Grid Margin=\"24\">\n" +
        "        <Grid.RowDefinitions>\n" +
        "            <RowDefinition Height=\"Auto\" />\n" +
        "            <RowDefinition Height=\"18\" />\n" +
        "            <RowDefinition Height=\"*\" />\n" +
        "        </Grid.RowDefinitions>\n" +
        "\n" +
        "        <TextBlock Text=\"Designer Preview\"\n" +
        "                   Foreground=\"#E7EDF5\"\n" +
        "                   FontSize=\"22\"\n" +
        "                   FontWeight=\"SemiBold\" />\n" +
        "\n" +
        "        <Border Grid.Row=\"2\"\n" +
        "                Background=\"#182230\"\n" +
        "                BorderBrush=\"#35506B\"\n" +
        "                BorderThickness=\"1\"\n" +
        "                CornerRadius=\"12\"\n" +
        "                Padding=\"18\">\n" +
        "            <StackPanel>\n" +
        "                <TextBlock Text=\"Manual refresh is enabled.\"\n" +
        "                           Foreground=\"#E7EDF5\"\n" +
        "                           FontSize=\"18\" />\n" +
        "                <TextBlock Text=\"Edit the XML below, then press F5 or the toolbar button.\"\n" +
        "                           Foreground=\"#8AA3B8\"\n" +
        "                           Margin=\"0,6,0,12\" />\n" +
        "                <Button x:Name=\"PreviewButton\"\n" +
        "                        Content=\"Preview Action\"\n" +
        "                        Width=\"180\"\n" +
        "                        Height=\"40\"\n" +
        "                        Background=\"#1F8EFA\"\n" +
        "                        BorderBrush=\"#56A7F7\"\n" +
        "                        BorderThickness=\"1\" />\n" +
        "            </StackPanel>\n" +
        "        </Border>\n" +
        "    </Grid>\n" +
        "</UserControl>\n";

    private string _promptPathText = string.Empty;
    private object? _previewContent;
    private DesignerPreviewPlaceholderModel? _previewPlaceholder;
    private Visibility _previewContentVisibility = Visibility.Collapsed;
    private Visibility _previewPlaceholderVisibility = Visibility.Visible;
    private string _toolbarStatusText = "Refresh idle. Edit the source and press F5.";
    private Color _toolbarStatusForeground = new(111, 183, 255);
    private string _visualTreeSummaryText = "Refresh to inspect the last successful preview.";
    private string _inspectorSummaryText = "Select a visual tree node to inspect it.";
    private string _diagnosticsSummaryText = "Errors and warnings appear after refresh.";
    private string _diagnosticsTabHeader = "Diagnostics";
    private string _documentStatusText = "Untitled.xml • memory only";
    private Color _documentStatusForeground = new(143, 210, 179);
    private string? _documentStatusOverrideText;
    private Color? _documentStatusOverrideColor;
    private int _selectedEditorTabIndex;
    private DesignerSourceNavigationRequest? _sourceNavigationRequest;
    private DesignerDocumentPromptState _workflowPrompt = DesignerDocumentPromptState.None;
    private IReadOnlyList<DesignerVisualTreeNodeViewModel> _visualTreeNodes = Array.Empty<DesignerVisualTreeNodeViewModel>();
    private IReadOnlyList<DesignerVisualTreeNodeViewModel> _visualTreeRoots = Array.Empty<DesignerVisualTreeNodeViewModel>();
    private IReadOnlyList<DesignerInspectorSectionViewModel> _inspectorSections = Array.Empty<DesignerInspectorSectionViewModel>();
    private IReadOnlyList<DesignerDiagnosticEntry> _diagnostics = Array.Empty<DesignerDiagnosticEntry>();
    private IReadOnlyList<DesignerRootTemplateViewModel> _rootTemplates = Array.Empty<DesignerRootTemplateViewModel>();
    private DesignerRootTemplateViewModel? _selectedRootTemplate;
    private readonly Dictionary<string, DesignerVisualTreeNodeViewModel> _visualTreeNodesById = new(StringComparer.Ordinal);
    private DesignerVisualTreeNodeViewModel? _selectedVisualTreeNode;

    public DesignerShellViewModel(
        DesignerController? controller = null,
        DesignerDocumentController? documentController = null,
        DesignerDocumentWorkflowController? workflow = null)
    {
        Controller = controller ?? new DesignerController();
        DocumentController = documentController ?? new DesignerDocumentController(DefaultSourceText);
        Workflow = workflow ?? new DesignerDocumentWorkflowController(DocumentController);
        _promptPathText = Workflow.Prompt.PathText ?? string.Empty;
        _rootTemplates = CreateRootTemplates();
        _selectedRootTemplate = _rootTemplates.FirstOrDefault();

        RefreshCommand = new RelayCommand(_ => RefreshPreview());
        NavigateToDiagnosticCommand = new RelayCommand(ExecuteNavigateToDiagnostic);
        SaveCommand = new RelayCommand(_ => ExecuteWorkflowAction(Workflow.Save), _ => CanSaveDocument());
        PromptPrimaryCommand = new RelayCommand(_ => ExecutePromptPrimary(), _ => Workflow.Prompt.IsVisible);
        PromptCancelCommand = new RelayCommand(_ => ExecuteWorkflowAction(Workflow.CancelPrompt, syncEditorFirst: false), _ => Workflow.Prompt.IsVisible);
        SelectVisualTreeNodeCommand = new RelayCommand(ExecuteSelectVisualTreeNode);
        ToggleVisualTreeNodeExpansionCommand = new RelayCommand(ExecuteToggleVisualTreeNodeExpansion);

        RefreshPresentationState(selectDiagnosticsOnError: false);
        RefreshWorkflowPromptState();
        RefreshDocumentStatus();
    }

    public DesignerController Controller { get; }

    public DesignerDocumentController DocumentController { get; }

    public DesignerDocumentWorkflowController Workflow { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action? DeferredAppExitRequested;

    public RelayCommand SaveCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand NavigateToDiagnosticCommand { get; }

    public RelayCommand PromptPrimaryCommand { get; }


    public RelayCommand PromptCancelCommand { get; }

    public RelayCommand SelectVisualTreeNodeCommand { get; }

    public RelayCommand ToggleVisualTreeNodeExpansionCommand { get; }

    public IReadOnlyList<DesignerRootTemplateViewModel> RootTemplates => _rootTemplates;

    public DesignerRootTemplateViewModel? SelectedRootTemplate
    {
        get => _selectedRootTemplate;
        set
        {
            if (ReferenceEquals(_selectedRootTemplate, value) || value == null)
            {
                return;
            }

            _selectedRootTemplate = value;
            OnPropertyChanged();
            if (string.IsNullOrWhiteSpace(value.ElementName))
            {
                return;
            }

            ApplyRootTemplate(value);
        }
    }

    public object? PreviewContent
    {
        get => _previewContent;
        private set => SetField(ref _previewContent, value);
    }

    public DesignerPreviewPlaceholderModel? PreviewPlaceholder
    {
        get => _previewPlaceholder;
        private set => SetField(ref _previewPlaceholder, value);
    }

    public Visibility PreviewContentVisibility
    {
        get => _previewContentVisibility;
        private set => SetField(ref _previewContentVisibility, value);
    }

    public Visibility PreviewPlaceholderVisibility
    {
        get => _previewPlaceholderVisibility;
        private set => SetField(ref _previewPlaceholderVisibility, value);
    }

    public string ToolbarStatusText
    {
        get => _toolbarStatusText;
        private set => SetField(ref _toolbarStatusText, value);
    }

    public Color ToolbarStatusForeground
    {
        get => _toolbarStatusForeground;
        private set => SetField(ref _toolbarStatusForeground, value);
    }

    public string VisualTreeSummaryText
    {
        get => _visualTreeSummaryText;
        private set => SetField(ref _visualTreeSummaryText, value);
    }

    public IReadOnlyList<DesignerVisualTreeNodeViewModel> VisualTreeRoots => _visualTreeRoots;

    public IReadOnlyList<DesignerVisualTreeNodeViewModel> VisualTreeNodes
    {
        get => _visualTreeNodes;
        private set => SetField(ref _visualTreeNodes, value);
    }

    public string InspectorSummaryText
    {
        get => _inspectorSummaryText;
        private set => SetField(ref _inspectorSummaryText, value);
    }

    public IReadOnlyList<DesignerInspectorSectionViewModel> InspectorSections
    {
        get => _inspectorSections;
        private set => SetField(ref _inspectorSections, value);
    }

    public string DiagnosticsSummaryText
    {
        get => _diagnosticsSummaryText;
        private set => SetField(ref _diagnosticsSummaryText, value);
    }

    public string DiagnosticsTabHeader
    {
        get => _diagnosticsTabHeader;
        private set => SetField(ref _diagnosticsTabHeader, value);
    }

    public IReadOnlyList<DesignerDiagnosticEntry> Diagnostics
    {
        get => _diagnostics;
        private set => SetField(ref _diagnostics, value);
    }

    public string DocumentStatusText
    {
        get => _documentStatusText;
        private set => SetField(ref _documentStatusText, value);
    }

    public Color DocumentStatusForeground
    {
        get => _documentStatusForeground;
        private set => SetField(ref _documentStatusForeground, value);
    }

    public DesignerDocumentPromptState WorkflowPrompt
    {
        get => _workflowPrompt;
        private set => SetField(ref _workflowPrompt, value);
    }

    public Visibility WorkflowPromptVisibility => WorkflowPrompt.IsVisible ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WorkflowPromptPathEditorVisibility => WorkflowPrompt.ShowsPathEditor ? Visibility.Visible : Visibility.Collapsed;


    public int SelectedEditorTabIndex
    {
        get => _selectedEditorTabIndex;
        set => SetField(ref _selectedEditorTabIndex, value);
    }

    public DesignerSourceNavigationRequest? SourceNavigationRequest
    {
        get => _sourceNavigationRequest;
        private set => SetField(ref _sourceNavigationRequest, value);
    }

    public string SourceText
    {
        get => DocumentController.CurrentText;
        set
        {
            if (string.Equals(DocumentController.CurrentText, value, StringComparison.Ordinal))
            {
                return;
            }

            DocumentController.UpdateText(value);
            OnPropertyChanged();
            RefreshCommandStates();
        }
    }

    public string PromptPathText
    {
        get => _promptPathText;
        set
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(_promptPathText, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _promptPathText = normalized;
            OnPropertyChanged();
        }
    }

    public bool RefreshPreview()
    {
        var succeeded = Controller.Refresh(DocumentController.CurrentText);
        RefreshPresentationState(selectDiagnosticsOnError: true);
        RefreshCommandStates();
        return succeeded;
    }

    public bool TryRequestAppExit()
    {
        var result = Workflow.BeginClose();
        HandleWorkflowResult(result);
        return result.CloseAction == DesignerWorkflowCloseAction.AllowCurrentRequest;
    }

    public void RefreshCommandStates()
    {
        SaveCommand.RaiseCanExecuteChanged();
        PromptPrimaryCommand.RaiseCanExecuteChanged();
        PromptCancelCommand.RaiseCanExecuteChanged();
    }

    public void SetDocumentStatusOverride(string? message, Color? color)
    {
        _documentStatusOverrideText = string.IsNullOrWhiteSpace(message) ? null : message;
        _documentStatusOverrideColor = color;
        RefreshDocumentStatus();
    }

    public void ClearDocumentStatusOverride()
    {
        _documentStatusOverrideText = null;
        _documentStatusOverrideColor = null;
        RefreshDocumentStatus();
    }

    private void ExecutePromptPrimary()
    {
        var result = Workflow.Prompt.Kind switch
        {
            DesignerDocumentPromptKind.OverwriteConfirmation => Workflow.ConfirmOverwriteSavePath(),
            _ => Workflow.SubmitPromptPath(PromptPathText)
        };

        HandleWorkflowResult(result);
    }

    private void ExecuteWorkflowAction(Func<DesignerDocumentWorkflowResult> action, bool syncEditorFirst = true)
    {
        _ = syncEditorFirst;
        HandleWorkflowResult(action());
    }

    private void ExecuteSelectVisualTreeNode(object? parameter)
    {
        if (parameter is not DesignerVisualTreeNodeViewModel node)
        {
            return;
        }

        ApplySelectedVisualTreeNode(node.Id);
        Controller.SelectVisualNode(node.Id);
        RefreshInspectorPresentation();
    }

    private void ExecuteToggleVisualTreeNodeExpansion(object? parameter)
    {
        if (parameter is DesignerVisualTreeNodeViewModel node && node.HasChildren)
        {
            node.IsExpanded = !node.IsExpanded;
            RefreshVisibleVisualTreeNodes();
        }
    }

    private void ExecuteNavigateToDiagnostic(object? parameter)
    {
        if (parameter is DesignerDiagnosticEntry diagnostic && diagnostic.Line.HasValue)
        {
            SelectedEditorTabIndex = 0;
            SourceNavigationRequest = new DesignerSourceNavigationRequest(diagnostic.Line.Value);
        }
    }

    private void ApplyRootTemplate(DesignerRootTemplateViewModel template)
    {
        SourceText = CreateBarebonesRootSource(template.ElementName);
        SelectedEditorTabIndex = 0;
        SourceNavigationRequest = null;
        SetDocumentStatusOverride(
            string.Create(CultureInfo.InvariantCulture, $"Created {template.ElementName} root scaffold."),
            new Color(143, 210, 179));
        _selectedRootTemplate = _rootTemplates.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedRootTemplate));
    }

    private static IReadOnlyList<DesignerRootTemplateViewModel> CreateRootTemplates()
    {
        var completionItems = DesignerControlCompletionCatalog.GetAllItems();
        var templates = new List<DesignerRootTemplateViewModel>
        {
            new("New root...", string.Empty, typeof(UIElement))
        };
        var includedNames = new HashSet<string>(StringComparer.Ordinal);

        string[] commonRootNames =
        [
            "UserControl",
            "Grid",
            "Canvas",
            "StackPanel",
            "DockPanel",
            "WrapPanel",
            "Border",
            "ContentControl"
        ];

        foreach (var rootName in commonRootNames)
        {
            var item = completionItems.FirstOrDefault(
                candidate => string.Equals(candidate.ElementName, rootName, StringComparison.Ordinal));
            if (item.ElementName == null || !includedNames.Add(item.ElementName))
            {
                continue;
            }

            templates.Add(new DesignerRootTemplateViewModel(
                item.ElementName,
                item.ElementName,
                item.ElementType));
        }

        foreach (var item in completionItems)
        {
            if (!includedNames.Add(item.ElementName))
            {
                continue;
            }

            templates.Add(new DesignerRootTemplateViewModel(
                item.ElementName,
                item.ElementName,
                item.ElementType));
        }

        return templates;
    }

    private static string CreateBarebonesRootSource(string elementName)
    {
        return $"<{elementName} xmlns=\"urn:inkkslinger-ui\"\n" +
            "             xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            $"</{elementName}>\n";
    }

    private void HandleWorkflowResult(DesignerDocumentWorkflowResult result)
    {
        PromptPathText = Workflow.Prompt.PathText ?? string.Empty;
        if (result.ReloadEditor)
        {
            OnPropertyChanged(nameof(SourceText));
            SourceNavigationRequest = null;
        }

        RefreshWorkflowPromptState();
        RefreshCommandStates();
        ApplyDocumentWorkflowStatus(result);

        if (result.CloseAction == DesignerWorkflowCloseAction.RequestDeferredClose)
        {
            DeferredAppExitRequested?.Invoke();
        }
    }

    private bool CanSaveDocument()
    {
        return DocumentController.IsDirty || !string.IsNullOrWhiteSpace(DocumentController.CurrentPath);
    }

    private void RefreshPresentationState(bool selectDiagnosticsOnError)
    {
        RefreshToolbarAndPreviewStatus();
        RefreshPreviewPresentation();
        RefreshVisualTreePresentation();
        RefreshInspectorPresentation();
        RefreshDiagnosticsPresentation(selectDiagnosticsOnError);
        RefreshDocumentStatus();
    }

    private void RefreshToolbarAndPreviewStatus()
    {
        switch (Controller.PreviewState)
        {
            case DesignerPreviewState.Success:
                ToolbarStatusText = string.Create(
                    CultureInfo.InvariantCulture,
                    $"Refresh succeeded. Diagnostics: {Controller.Diagnostics.Count}.");
                ToolbarStatusForeground = new Color(111, 183, 255);
                break;

            case DesignerPreviewState.Error:
                ToolbarStatusText = "Refresh failed. Preview was cleared to an error state.";
                ToolbarStatusForeground = new Color(255, 164, 128);
                break;

            default:
                ToolbarStatusText = "Refresh idle. Edit the source and press F5.";
                ToolbarStatusForeground = new Color(111, 183, 255);
                break;
        }
    }

    private void RefreshPreviewPresentation()
    {
        switch (Controller.PreviewState)
        {
            case DesignerPreviewState.Success:
                PreviewContent = Controller.PreviewRoot;
                PreviewPlaceholder = null;
                PreviewContentVisibility = Visibility.Visible;
                PreviewPlaceholderVisibility = Visibility.Collapsed;
                break;

            case DesignerPreviewState.Error:
                PreviewContent = null;
                PreviewPlaceholder = new DesignerPreviewPlaceholderModel(
                    "Preview unavailable",
                    Controller.PreviewFailureMessage ?? "The current XML did not load successfully.",
                    new Color(255, 164, 128),
                    new Color(212, 226, 238));
                PreviewContentVisibility = Visibility.Collapsed;
                PreviewPlaceholderVisibility = Visibility.Visible;
                break;

            default:
                PreviewContent = null;
                PreviewPlaceholder = new DesignerPreviewPlaceholderModel(
                    "Preview waiting",
                    "The editor is decoupled from rendering in this slice. Refresh when you want to rebuild the preview.",
                    new Color(111, 183, 255),
                    new Color(184, 200, 214));
                PreviewContentVisibility = Visibility.Collapsed;
                PreviewPlaceholderVisibility = Visibility.Visible;
                break;
        }
    }

    private void RefreshVisualTreePresentation()
    {
        _visualTreeNodesById.Clear();
        if (Controller.VisualTreeRoot == null)
        {
            _visualTreeRoots = Array.Empty<DesignerVisualTreeNodeViewModel>();
            VisualTreeNodes = Array.Empty<DesignerVisualTreeNodeViewModel>();
            OnPropertyChanged(nameof(VisualTreeRoots));
            VisualTreeSummaryText = Controller.PreviewState == DesignerPreviewState.Error
                ? "No visual tree is available because the last refresh failed."
                : "Refresh to inspect the last successful preview.";
            _selectedVisualTreeNode = null;
            return;
        }

        var rootNode = BuildVisualTreeNode(Controller.VisualTreeRoot, depth: 0);
        _visualTreeRoots = new[] { rootNode };
        RefreshVisibleVisualTreeNodes();
        OnPropertyChanged(nameof(VisualTreeRoots));
        VisualTreeSummaryText = "Selecting a node updates the inspector below.";
        ApplySelectedVisualTreeNode(Controller.SelectedNodeId ?? rootNode.Id);
    }

    private DesignerVisualTreeNodeViewModel BuildVisualTreeNode(DesignerVisualNode node, int depth)
    {
        var children = node.Children.Select(child => BuildVisualTreeNode(child, depth + 1)).ToArray();
        var viewModel = new DesignerVisualTreeNodeViewModel(
            node.Id,
            node.Label,
            children,
            depth,
            isExpanded: depth < 1);
        _visualTreeNodesById[node.Id] = viewModel;
        return viewModel;
    }

    private void RefreshVisibleVisualTreeNodes()
    {
        if (_visualTreeRoots.Count == 0)
        {
            VisualTreeNodes = Array.Empty<DesignerVisualTreeNodeViewModel>();
            return;
        }

        var visibleNodes = new List<DesignerVisualTreeNodeViewModel>();
        for (var index = 0; index < _visualTreeRoots.Count; index++)
        {
            AppendVisibleVisualTreeNodes(_visualTreeRoots[index], visibleNodes);
        }

        VisualTreeNodes = visibleNodes;
    }

    private static void AppendVisibleVisualTreeNodes(
        DesignerVisualTreeNodeViewModel node,
        List<DesignerVisualTreeNodeViewModel> destination)
    {
        destination.Add(node);
        if (!node.IsExpanded)
        {
            return;
        }

        for (var index = 0; index < node.Children.Count; index++)
        {
            AppendVisibleVisualTreeNodes(node.Children[index], destination);
        }
    }

    private void ApplySelectedVisualTreeNode(string? nodeId)
    {
        if (_selectedVisualTreeNode != null)
        {
            _selectedVisualTreeNode.IsSelected = false;
        }

        if (nodeId != null && _visualTreeNodesById.TryGetValue(nodeId, out var nextNode))
        {
            nextNode.IsSelected = true;
            _selectedVisualTreeNode = nextNode;
        }
        else
        {
            _selectedVisualTreeNode = null;
        }
    }

    private void RefreshInspectorPresentation()
    {
        if (Controller.Inspector == DesignerInspectorModel.Empty)
        {
            InspectorSummaryText = "Select a visual tree node to inspect it.";
            InspectorSections = Array.Empty<DesignerInspectorSectionViewModel>();
            return;
        }

        InspectorSummaryText = Controller.Inspector.Header;
        var identity = Controller.Inspector.Properties
            .Where(static p => InspectorIdentityPropertyNames.Contains(p.Name))
            .ToArray();
        var propertyRows = Controller.Inspector.Properties
            .Where(static p => !InspectorIdentityPropertyNames.Contains(p.Name))
            .ToArray();

        var sections = new List<DesignerInspectorSectionViewModel>(2);
        if (identity.Length > 0)
        {
            sections.Add(new DesignerInspectorSectionViewModel("Identity & Layout", identity));
        }

        if (propertyRows.Length > 0)
        {
            sections.Add(new DesignerInspectorSectionViewModel("Properties", propertyRows));
        }

        InspectorSections = sections;
    }

    private void RefreshDiagnosticsPresentation(bool selectDiagnosticsOnError)
    {
        Diagnostics = Controller.Diagnostics;
        if (Diagnostics.Count == 0)
        {
            DiagnosticsTabHeader = "Diagnostics";
            DiagnosticsSummaryText = Controller.PreviewState == DesignerPreviewState.Success
                ? "No parser diagnostics were reported during the last refresh."
                : "Parser warnings and errors appear after refresh.";
            return;
        }

        var warningCount = Diagnostics.Count(static diagnostic => diagnostic.Level == DesignerDiagnosticLevel.Warning);
        var errorCount = Diagnostics.Count - warningCount;
        DiagnosticsTabHeader = errorCount > 0
            ? string.Create(CultureInfo.InvariantCulture, $"Diagnostics (!{errorCount})")
            : warningCount > 0
                ? string.Create(CultureInfo.InvariantCulture, $"Diagnostics ({warningCount})")
                : "Diagnostics";
        DiagnosticsSummaryText = string.Create(
            CultureInfo.InvariantCulture,
            $"{errorCount} error(s), {warningCount} warning(s) from the last refresh.");

        if (selectDiagnosticsOnError && Controller.PreviewState == DesignerPreviewState.Error && errorCount > 0)
        {
            SelectedEditorTabIndex = 1;
        }
    }

    private void RefreshWorkflowPromptState()
    {
        WorkflowPrompt = Workflow.Prompt;
        OnPropertyChanged(nameof(WorkflowPromptVisibility));
        OnPropertyChanged(nameof(WorkflowPromptPathEditorVisibility));
    }

    private void RefreshDocumentStatus()
    {
        if (!string.IsNullOrWhiteSpace(_documentStatusOverrideText))
        {
            DocumentStatusText = _documentStatusOverrideText;
            DocumentStatusForeground = _documentStatusOverrideColor ?? new Color(143, 210, 179);
            return;
        }

        var dirtySuffix = DocumentController.IsDirty ? "dirty" : "saved";
        var pathSuffix = string.IsNullOrWhiteSpace(DocumentController.CurrentPath)
            ? "memory only"
            : DocumentController.CurrentPath;
        DocumentStatusText = string.Create(
            CultureInfo.InvariantCulture,
            $"{DocumentController.DisplayName} • {dirtySuffix} • {pathSuffix}");
        DocumentStatusForeground = DocumentController.IsDirty
            ? new Color(255, 205, 96)
            : new Color(143, 210, 179);
    }

    private void ApplyDocumentWorkflowStatus(DesignerDocumentWorkflowResult result)
    {
        if (string.IsNullOrWhiteSpace(result.Message))
        {
            ClearDocumentStatusOverride();
            return;
        }

        SetDocumentStatusOverride(
            result.Message,
            result.StatusKind switch
            {
                DesignerWorkflowStatusKind.Success => new Color(143, 210, 179),
                DesignerWorkflowStatusKind.Warning => new Color(255, 205, 96),
                DesignerWorkflowStatusKind.Error => new Color(255, 164, 128),
                _ => new Color(111, 183, 255)
            });
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
