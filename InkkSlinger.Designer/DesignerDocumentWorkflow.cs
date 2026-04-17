using System;
using System.Globalization;
using System.IO;

namespace InkkSlinger.Designer;

public enum DesignerDocumentPromptKind
{
    None,
    OpenPath,
    SavePath,
    UnsavedChanges,
    OverwriteConfirmation
}

public enum DesignerPendingWorkflowContinuation
{
    None,
    New,
    Open
}

public enum DesignerUnsavedChangesChoice
{
    Save,
    Discard,
    Cancel
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

    public bool ShowsPathEditor => Kind is DesignerDocumentPromptKind.OpenPath or DesignerDocumentPromptKind.SavePath;

    public bool ShowsDiscardAction => Kind == DesignerDocumentPromptKind.UnsavedChanges;
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
    private DesignerPendingWorkflowContinuation _pendingContinuation;
    private string? _pendingSavePath;

    public DesignerDocumentWorkflowController(DesignerDocumentController documentController)
    {
        _documentController = documentController ?? throw new ArgumentNullException(nameof(documentController));
    }

    public DesignerDocumentPromptState Prompt { get; private set; } = DesignerDocumentPromptState.None;

    public DesignerDocumentWorkflowResult BeginNew()
    {
        if (!TryPrepareForContinuation(DesignerPendingWorkflowContinuation.New, out var promptResult))
        {
            return promptResult;
        }

        var promptWasVisible = Prompt.IsVisible;
        _documentController.New();
        ClearPrompt();
        return Success(reloadEditor: true, promptChanged: promptWasVisible, "Created a new document.");
    }

    public DesignerDocumentWorkflowResult BeginOpen()
    {
        if (!TryPrepareForContinuation(DesignerPendingWorkflowContinuation.Open, out var promptResult))
        {
            return promptResult;
        }

        return ShowOpenPathPrompt("Enter a path to open another XML document.");
    }

    public DesignerDocumentWorkflowResult BeginClose()
    {
        var promptWasVisible = Prompt.IsVisible;
        ClearPendingState();
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
                ClearPrompt();
                return Success(
                    reloadEditor: false,
                    promptChanged: promptWasVisible,
                    string.Create(CultureInfo.InvariantCulture, $"Saved {_documentController.DisplayName}."));
            }

            return ShowSavePathPrompt(
                DesignerPendingWorkflowContinuation.None,
                "Choose where to save the current XML document.");
        }
        catch (Exception ex)
        {
            return Error(ex.Message);
        }
    }

    public DesignerDocumentWorkflowResult BeginSaveAs()
    {
        return ShowSavePathPrompt(
            DesignerPendingWorkflowContinuation.None,
            "Enter the destination path for the current XML document.");
    }

    public DesignerDocumentWorkflowResult ResolveUnsavedChanges(DesignerUnsavedChangesChoice choice)
    {
        if (Prompt.Kind != DesignerDocumentPromptKind.UnsavedChanges)
        {
            return Info(promptChanged: false, "No unsaved-changes confirmation is active.");
        }

        switch (choice)
        {
            case DesignerUnsavedChangesChoice.Cancel:
            {
                var promptWasVisible = Prompt.IsVisible;
                ClearPendingState();
                ClearPrompt();
                return Warning(promptChanged: promptWasVisible, "Action canceled.");
            }

            case DesignerUnsavedChangesChoice.Discard:
                return ContinuePendingContinuation(savedBeforeContinue: false);

            case DesignerUnsavedChangesChoice.Save:
                if (!string.IsNullOrWhiteSpace(_documentController.CurrentPath))
                {
                    try
                    {
                        _documentController.Save();
                        return ContinuePendingContinuation(savedBeforeContinue: true);
                    }
                    catch (Exception ex)
                    {
                        return Error(ex.Message);
                    }
                }

                return ShowSavePathPrompt(
                    _pendingContinuation,
                    "Save the current document before continuing.");

            default:
                return Warning(promptChanged: false, "Unsupported confirmation choice.");
        }
    }

    public DesignerDocumentWorkflowResult SubmitPromptPath(string? pathText)
    {
        var path = pathText?.Trim();
        return Prompt.Kind switch
        {
            DesignerDocumentPromptKind.OpenPath => SubmitOpenPath(path),
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
                _pendingContinuation,
                "Overwrite canceled. Choose a different path or confirm the current one.",
                _pendingSavePath);
        }

        var promptWasVisible = Prompt.IsVisible;
        ClearPendingState();
        ClearPrompt();
        return Warning(promptChanged: promptWasVisible, "Action canceled.");
    }

    private DesignerDocumentWorkflowResult SubmitOpenPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Warning(promptChanged: false, "Open needs a file path.");
        }

        try
        {
            _documentController.Open(path);
            var promptWasVisible = Prompt.IsVisible;
            ClearPendingState();
            ClearPrompt();
            return Success(
                reloadEditor: true,
                promptChanged: promptWasVisible,
                string.Create(CultureInfo.InvariantCulture, $"Opened {_documentController.DisplayName}."));
        }
        catch (Exception ex)
        {
            return Error(ex.Message);
        }
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
            _documentController.SaveAs(path);
        }
        catch (Exception ex)
        {
            return Error(ex.Message);
        }

        _pendingSavePath = null;

        if (_pendingContinuation != DesignerPendingWorkflowContinuation.None)
        {
            return ContinuePendingContinuation(savedBeforeContinue: true);
        }

        var promptWasVisible = Prompt.IsVisible;
        ClearPendingState();
        ClearPrompt();
        return Success(
            reloadEditor: false,
            promptChanged: promptWasVisible,
            string.Create(CultureInfo.InvariantCulture, $"Saved {_documentController.DisplayName}."));
    }

    private bool TryPrepareForContinuation(
        DesignerPendingWorkflowContinuation pendingContinuation,
        out DesignerDocumentWorkflowResult result)
    {
        if (Prompt.IsVisible)
        {
            result = Info(promptChanged: false, "Resolve the current document prompt before continuing.");
            return false;
        }

        if (!_documentController.IsDirty)
        {
            result = Info(promptChanged: false, string.Empty);
            return true;
        }

        _pendingContinuation = pendingContinuation;
        _pendingSavePath = null;
        Prompt = new DesignerDocumentPromptState(
            DesignerDocumentPromptKind.UnsavedChanges,
            "Unsaved Changes",
            string.Create(
                CultureInfo.InvariantCulture,
                $"{_documentController.DisplayName} has unsaved changes. Save before continuing?"),
            "Save",
            null);
        result = Info(promptChanged: true, "Choose Save, Discard, or Cancel before continuing.");
        return false;
    }

    private DesignerDocumentWorkflowResult ContinuePendingContinuation(bool savedBeforeContinue)
    {
        var pendingContinuation = _pendingContinuation;
        ClearPendingState();

        switch (pendingContinuation)
        {
            case DesignerPendingWorkflowContinuation.New:
            {
                var promptWasVisible = Prompt.IsVisible;
                _documentController.New();
                ClearPrompt();
                return Success(
                    reloadEditor: true,
                    promptChanged: promptWasVisible,
                    savedBeforeContinue
                        ? "Saved the current document and created a new one."
                        : "Discarded unsaved changes and created a new document.");
            }

            case DesignerPendingWorkflowContinuation.Open:
                return ShowOpenPathPrompt(
                    savedBeforeContinue
                        ? "Saved the current document. Enter a path to open another XML document."
                        : "Discarded unsaved changes. Enter a path to open another XML document.");

            default:
            {
                var promptWasVisible = Prompt.IsVisible;
                ClearPrompt();
                return Info(promptChanged: promptWasVisible, string.Empty);
            }
        }
    }

    private DesignerDocumentWorkflowResult ShowOpenPathPrompt(string message)
    {
        ClearPendingState();
        Prompt = new DesignerDocumentPromptState(
            DesignerDocumentPromptKind.OpenPath,
            "Open XML Document",
            "Load an XML view from disk into the designer.",
            "Open",
            _documentController.CurrentPath ?? string.Empty);
        return Info(promptChanged: true, message);
    }

    private DesignerDocumentWorkflowResult ShowSavePathPrompt(
        DesignerPendingWorkflowContinuation pendingContinuationAfterSave,
        string message,
        string? suggestedPath = null)
    {
        _pendingContinuation = pendingContinuationAfterSave;
        _pendingSavePath = string.IsNullOrWhiteSpace(suggestedPath)
            ? BuildSuggestedPath()
            : suggestedPath.Trim();
        Prompt = new DesignerDocumentPromptState(
            DesignerDocumentPromptKind.SavePath,
            pendingContinuationAfterSave == DesignerPendingWorkflowContinuation.None ? "Save Document As" : "Save Current Document",
            pendingContinuationAfterSave == DesignerPendingWorkflowContinuation.None
                ? "Choose where the current XML document should be saved."
                : "Choose where to save the current XML document before continuing.",
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

    private void ClearPendingState()
    {
        _pendingContinuation = DesignerPendingWorkflowContinuation.None;
        _pendingSavePath = null;
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