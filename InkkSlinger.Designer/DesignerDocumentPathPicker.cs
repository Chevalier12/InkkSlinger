using System;

namespace InkkSlinger.Designer;

public interface IDesignerDocumentPathPicker
{
    string? PickOpenPath(string? currentPath);

    string? PickSavePath(string? currentPath);
}

internal sealed class TextBoxBackedDesignerDocumentPathPicker : IDesignerDocumentPathPicker
{
    private readonly TextBox _pathEditor;

    public TextBoxBackedDesignerDocumentPathPicker(TextBox pathEditor)
    {
        _pathEditor = pathEditor ?? throw new ArgumentNullException(nameof(pathEditor));
    }

    public string? PickOpenPath(string? currentPath)
    {
        _ = currentPath;
        return GetNormalizedPath();
    }

    public string? PickSavePath(string? currentPath)
    {
        _ = currentPath;
        return GetNormalizedPath();
    }

    private string? GetNormalizedPath()
    {
        var text = _pathEditor.Text?.Trim();
        return string.IsNullOrWhiteSpace(text)
            ? null
            : text;
    }
}