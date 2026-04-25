using System;
using System.Globalization;
using System.IO;

namespace InkkSlinger.Designer;

public enum DesignerDocumentPromptKind
{
    None,
    SavePath,
    OverwriteConfirmation
}

public enum DesignerWorkflowStatusKind
{
    Info,
    Success,
    Warning,
    Error
}

public enum DesignerWorkflowCloseAction
{
    None,
    AllowCurrentRequest,
    RequestDeferredClose
}

public sealed record DesignerDocumentPromptState(
    DesignerDocumentPromptKind Kind,
    string Title,
    string Message,
    string ConfirmText,
    string? PathText)
{
    public static readonly DesignerDocumentPromptState None = new(
        DesignerDocumentPromptKind.None,
        string.Empty,
        string.Empty,
        string.Empty,
        null);

    public bool IsVisible => Kind != DesignerDocumentPromptKind.None;

    public bool ShowsPathEditor => Kind == DesignerDocumentPromptKind.SavePath;
}

public sealed record DesignerDocumentWorkflowResult(
    bool ReloadEditor,
    bool PromptChanged,
    string Message,
    DesignerWorkflowStatusKind StatusKind,
    DesignerWorkflowCloseAction CloseAction = DesignerWorkflowCloseAction.None);

public sealed class DesignerDocumentWorkflowController
{
    private readonly DesignerDocumentController _documentController;
    private string? _pendingSavePath;

    public DesignerDocumentWorkflowController(DesignerDocumentController documentController)
    {
        _documentController = documentController ?? throw new ArgumentNullException(nameof(documentController));
    }

    public DesignerDocumentPromptState Prompt { get; private set; } = DesignerDocumentPromptState.None;

    public DesignerDocumentWorkflowResult BeginClose()
    {
        var promptWasVisible = Prompt.IsVisible;
        _pendingSavePath = null;
        ClearPrompt();
        return AllowCurrentClose(promptChanged: promptWasVisible, string.Empty);
    }

    public DesignerDocumentWorkflowResult Save()
    {
        try
        {
            if (_documentController.Save())
            {
                var promptWasVisible = Prompt.IsVisible;
                _pendingSavePath = null;
                ClearPrompt();
                return Success(
                    reloadEditor: false,
                    promptChanged: promptWasVisible,
                    string.Create(CultureInfo.InvariantCulture, $"Saved {_documentController.DisplayName}."));
            }

            return ShowSavePathPrompt("Choose where to save the current XML document.");
        }
        catch (Exception ex)
        {
            return Error(ex.Message);
        }
    }

    public DesignerDocumentWorkflowResult SubmitPromptPath(string? pathText)
    {
        var path = pathText?.Trim();
        return Prompt.Kind switch
        {
            DesignerDocumentPromptKind.SavePath => SubmitSavePath(path),
            DesignerDocumentPromptKind.OverwriteConfirmation => ConfirmOverwriteSavePath(),
            _ => Info(promptChanged: false, "No path prompt is active.")
        };
    }

    public DesignerDocumentWorkflowResult ConfirmOverwriteSavePath()
    {
        if (Prompt.Kind != DesignerDocumentPromptKind.OverwriteConfirmation || string.IsNullOrWhiteSpace(_pendingSavePath))
        {
            return Info(promptChanged: false, "No overwrite confirmation is active.");
        }

        return CompleteSavePath(_pendingSavePath);
    }

    public DesignerDocumentWorkflowResult CancelPrompt()
    {
        if (!Prompt.IsVisible)
        {
            return Info(promptChanged: false, "No document prompt is active.");
        }

        if (Prompt.Kind == DesignerDocumentPromptKind.OverwriteConfirmation)
        {
            return ShowSavePathPrompt(
                "Overwrite canceled. Choose a different path or confirm the current one.",
                _pendingSavePath);
        }

        var promptWasVisible = Prompt.IsVisible;
        _pendingSavePath = null;
        ClearPrompt();
        return Warning(promptChanged: promptWasVisible, "Action canceled.");
    }

    private DesignerDocumentWorkflowResult SubmitSavePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Warning(promptChanged: false, "Save needs a file path.");
        }

        _pendingSavePath = path;
        if (_documentController.PathExists(path))
        {
            Prompt = new DesignerDocumentPromptState(
                DesignerDocumentPromptKind.OverwriteConfirmation,
                "Overwrite Existing File",
                string.Create(CultureInfo.InvariantCulture, $"{Path.GetFileName(path)} already exists. Overwrite it?"),
                "Overwrite",
                path);
            return Warning(promptChanged: true, "Confirm overwrite before saving.");
        }

        return CompleteSavePath(path);
    }

    private DesignerDocumentWorkflowResult CompleteSavePath(string path)
    {
        try
        {
            _documentController.SaveToPath(path);
        }
        catch (Exception ex)
        {
            return Error(ex.Message);
        }

        _pendingSavePath = null;

        var promptWasVisible = Prompt.IsVisible;
        ClearPrompt();
        return Success(
            reloadEditor: false,
            promptChanged: promptWasVisible,
            string.Create(CultureInfo.InvariantCulture, $"Saved {_documentController.DisplayName}."));
    }

    private DesignerDocumentWorkflowResult ShowSavePathPrompt(
        string message,
        string? suggestedPath = null)
    {
        _pendingSavePath = string.IsNullOrWhiteSpace(suggestedPath)
            ? BuildSuggestedPath()
            : suggestedPath.Trim();
        Prompt = new DesignerDocumentPromptState(
            DesignerDocumentPromptKind.SavePath,
            "Save Document",
            "Choose where the current XML document should be saved.",
            "Save",
            _pendingSavePath);
        return Info(promptChanged: true, message);
    }

    private string BuildSuggestedPath()
    {
        return string.IsNullOrWhiteSpace(_documentController.CurrentPath)
            ? _documentController.DisplayName
            : _documentController.CurrentPath!;
    }

    private void ClearPrompt()
    {
        Prompt = DesignerDocumentPromptState.None;
    }

    private static DesignerDocumentWorkflowResult Info(bool promptChanged, string message)
    {
        return new DesignerDocumentWorkflowResult(false, promptChanged, message, DesignerWorkflowStatusKind.Info);
    }

    private static DesignerDocumentWorkflowResult Success(bool reloadEditor, bool promptChanged, string message)
    {
        return new DesignerDocumentWorkflowResult(reloadEditor, promptChanged, message, DesignerWorkflowStatusKind.Success);
    }

    private static DesignerDocumentWorkflowResult Warning(bool promptChanged, string message)
    {
        return new DesignerDocumentWorkflowResult(false, promptChanged, message, DesignerWorkflowStatusKind.Warning);
    }

    private static DesignerDocumentWorkflowResult AllowCurrentClose(bool promptChanged, string message)
    {
        return new DesignerDocumentWorkflowResult(
            false,
            promptChanged,
            message,
            DesignerWorkflowStatusKind.Info,
            DesignerWorkflowCloseAction.AllowCurrentRequest);
    }

    private static DesignerDocumentWorkflowResult Error(string message)
    {
        return new DesignerDocumentWorkflowResult(false, false, message, DesignerWorkflowStatusKind.Error);
    }
}