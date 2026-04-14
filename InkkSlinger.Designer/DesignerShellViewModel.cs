using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace InkkSlinger.Designer;

public sealed class DesignerShellViewModel : INotifyPropertyChanged
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

    private string _promptPathText = string.Empty;

    public DesignerShellViewModel(
        DesignerController? controller = null,
        DesignerDocumentController? documentController = null,
        DesignerDocumentWorkflowController? workflow = null)
    {
        Controller = controller ?? new DesignerController();
        DocumentController = documentController ?? new DesignerDocumentController(DefaultSourceText);
        Workflow = workflow ?? new DesignerDocumentWorkflowController(DocumentController);
        _promptPathText = Workflow.Prompt.PathText ?? string.Empty;

        RefreshCommand = new RelayCommand(_ => RefreshPreview());
        NavigateToDiagnosticCommand = new RelayCommand(ExecuteNavigateToDiagnostic);
        NewCommand = new RelayCommand(_ => ExecuteWorkflowAction(Workflow.BeginNew));
        OpenCommand = new RelayCommand(_ => ExecuteWorkflowAction(Workflow.BeginOpen));
        SaveCommand = new RelayCommand(_ => ExecuteWorkflowAction(Workflow.Save), _ => CanSaveDocument());
        SaveAsCommand = new RelayCommand(_ => ExecuteWorkflowAction(Workflow.BeginSaveAs));
        PromptPrimaryCommand = new RelayCommand(_ => ExecutePromptPrimary(), _ => Workflow.Prompt.IsVisible);
        PromptSecondaryCommand = new RelayCommand(
            _ => ExecuteWorkflowAction(() => Workflow.ResolveUnsavedChanges(DesignerUnsavedChangesChoice.Discard)),
            _ => Workflow.Prompt.ShowsDiscardAction);
        PromptCancelCommand = new RelayCommand(_ => ExecuteWorkflowAction(Workflow.CancelPrompt, syncEditorFirst: false), _ => Workflow.Prompt.IsVisible);
    }

    public DesignerController Controller { get; }

    public DesignerDocumentController DocumentController { get; }

    public DesignerDocumentWorkflowController Workflow { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action<bool>? RefreshCompleted;

    public event Action<DesignerDocumentWorkflowResult>? WorkflowResultProduced;

    public event Action<DesignerDiagnosticEntry>? DiagnosticNavigationRequested;

    public event Action? DeferredAppExitRequested;

    public RelayCommand NewCommand { get; }

    public RelayCommand OpenCommand { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand SaveAsCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand NavigateToDiagnosticCommand { get; }

    public RelayCommand PromptPrimaryCommand { get; }

    public RelayCommand PromptSecondaryCommand { get; }

    public RelayCommand PromptCancelCommand { get; }

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
        RefreshCommandStates();
        RefreshCompleted?.Invoke(succeeded);
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
        PromptSecondaryCommand.RaiseCanExecuteChanged();
        PromptCancelCommand.RaiseCanExecuteChanged();
    }

    private void ExecutePromptPrimary()
    {
        var result = Workflow.Prompt.Kind switch
        {
            DesignerDocumentPromptKind.UnsavedChanges => Workflow.ResolveUnsavedChanges(DesignerUnsavedChangesChoice.Save),
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

    private void ExecuteNavigateToDiagnostic(object? parameter)
    {
        if (parameter is DesignerDiagnosticEntry diagnostic)
        {
            DiagnosticNavigationRequested?.Invoke(diagnostic);
        }
    }

    private void HandleWorkflowResult(DesignerDocumentWorkflowResult result)
    {
        PromptPathText = Workflow.Prompt.PathText ?? string.Empty;
        RefreshCommandStates();
        WorkflowResultProduced?.Invoke(result);

        if (result.CloseAction == DesignerWorkflowCloseAction.RequestDeferredClose)
        {
            DeferredAppExitRequested?.Invoke();
        }
    }

    private bool CanSaveDocument()
    {
        return DocumentController.IsDirty || !string.IsNullOrWhiteSpace(DocumentController.CurrentPath);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}