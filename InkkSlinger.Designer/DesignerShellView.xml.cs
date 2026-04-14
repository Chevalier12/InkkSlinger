using System;
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
        Action? requestAppExit = null)
    {
        InitializeComponent();
        _requestAppExit = requestAppExit ?? DefaultRequestAppExit;
        _viewModel = new DesignerShellViewModel(documentController: documentController, workflow: workflow);
        DataContext = _viewModel;
        _viewModel.DeferredAppExitRequested += OnViewModelDeferredAppExitRequested;
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

    public RichTextBox SourceEditorControl => SourceEditorView.Editor;

    public Border SourceLineNumberBorderControl => SourceEditorView.LineNumberBorder;

    public ItemsControl SourceLineNumberPanelControl => SourceEditorView.LineNumberPanel;

    public string SourceText
    {
        get => _viewModel.SourceText;
        set => _viewModel.SourceText = value;
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

    private static void DefaultRequestAppExit()
    {
        if (UiApplication.Current.HasMainWindow)
        {
            UiApplication.Current.Shutdown();
        }
    }

}