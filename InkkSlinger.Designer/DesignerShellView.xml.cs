using System;
using System.ComponentModel;
using InkkSlinger;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger.Designer;

public partial class DesignerShellView : UserControl, IAppExitRequestHandler
{
    private readonly DesignerShellViewModel _viewModel;
    private readonly Action _requestAppExit;

    public DesignerShellView(
        DesignerDocumentController? documentController = null,
        DesignerDocumentWorkflowController? workflow = null,
        Action? requestAppExit = null,
        DesignerProjectSession? projectSession = null,
        Action? requestStartPage = null)
    {
        InitializeComponent();
        _requestAppExit = requestAppExit ?? DefaultRequestAppExit;
        _viewModel = new DesignerShellViewModel(documentController: documentController, workflow: workflow, projectSession: projectSession);
        DataContext = _viewModel;
        _viewModel.DeferredAppExitRequested += OnViewModelDeferredAppExitRequested;
        if (requestStartPage != null)
        {
            _viewModel.BackToStartRequested += requestStartPage;
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ProjectExplorerTree.SelectedItemChanged += OnProjectExplorerSelectedItemChanged;
        RebuildProjectExplorerTree();
        InputBindings.Add(new KeyBinding
        {
            Key = Keys.F5,
            Modifiers = ModifierKeys.None,
            Command = _viewModel.RefreshCommand,
            CommandTarget = this
        });
    }

    public DesignerShellViewModel ViewModel => _viewModel;

    public DesignerController Controller => _viewModel.Controller;

    public DesignerDocumentController DocumentController => _viewModel.DocumentController;

    public DesignerSourceEditorView SourceEditorView => (DesignerSourceEditorView)SourceEditorPane;

    public DesignerSourceEditorView AppResourcesEditorView => (DesignerSourceEditorView)AppResourcesEditorPane;

    public IDE_Editor SourceEditorControl => SourceEditorView.Editor;

    public IDE_Editor AppResourcesEditorControl => AppResourcesEditorView.Editor;

    public Border SourceLineNumberBorderControl => SourceEditorView.LineNumberBorder;

    public IDEEditorLineNumberPresenter SourceLineNumberPanelControl => SourceEditorView.LineNumberPanel;

    public string SourceText
    {
        get => _viewModel.SourceText;
        set => _viewModel.SourceText = value;
    }

    public string AppResourcesText
    {
        get => _viewModel.AppResourcesText;
        set => _viewModel.AppResourcesText = value;
    }

    public bool RefreshPreview()
    {
        return _viewModel.RefreshPreview();
    }

    public bool TryRequestAppExit()
    {
        return _viewModel.TryRequestAppExit();
    }

    private void OnViewModelDeferredAppExitRequested()
    {
        _requestAppExit();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(DesignerShellViewModel.ProjectRootNode))
        {
            RebuildProjectExplorerTree();
        }
    }

    private void OnProjectExplorerSelectedItemChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (ProjectExplorerTree.SelectedItem is TreeViewItem { Tag: DesignerProjectNode node })
        {
            _viewModel.SelectProjectNode(node);
        }
    }

    private void RebuildProjectExplorerTree()
    {
        var selectedPath = _viewModel.SelectedProjectNode?.FullPath;
        ProjectExplorerTree.Items.Clear();
        ProjectExplorerTree.HierarchicalItemsSource = null;
        if (_viewModel.ProjectRootNode == null)
        {
            return;
        }

        ProjectExplorerTree.HierarchicalChildrenSelector = static item => item is DesignerProjectNode node ? node.Children : Array.Empty<DesignerProjectNode>();
        ProjectExplorerTree.HierarchicalHasChildrenSelector = static item => item is DesignerProjectNode { IsFolder: true };
        ProjectExplorerTree.HierarchicalHeaderSelector = static item => item is DesignerProjectNode node ? (node.IsFolder ? "[+] " : "    ") + node.Name : string.Empty;
        ProjectExplorerTree.HierarchicalExpandedSelector = static item => item is DesignerProjectNode { IsFolder: true };
        ProjectExplorerTree.HierarchicalItemsSource = new[] { _viewModel.ProjectRootNode };

        var selectedNode = FindProjectExplorerNode(_viewModel.ProjectRootNode, selectedPath) ?? _viewModel.ProjectRootNode;
        ProjectExplorerTree.SelectHierarchicalItem(selectedNode);
    }

    private static DesignerProjectNode? FindProjectExplorerNode(DesignerProjectNode node, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (string.Equals(node.FullPath, path, StringComparison.OrdinalIgnoreCase))
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            var match = FindProjectExplorerNode(child, path);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static void DefaultRequestAppExit()
    {
        if (UiApplication.Current.HasMainWindow)
        {
            UiApplication.Current.Shutdown();
        }
    }

}