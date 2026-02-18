using System;
using System.Collections.Generic;

namespace InkkSlinger;

public static class TextClipboard
{
    private static string _text = string.Empty;
    private static readonly Dictionary<string, object?> DataByFormat = new(StringComparer.Ordinal);

    public static System.Func<string?>? GetTextOverride { get; set; }

    public static System.Action<string>? SetTextOverride { get; set; }

    public static bool TryGetText(out string text)
    {
        if (GetTextOverride != null)
        {
            text = GetTextOverride() ?? string.Empty;
            return text.Length > 0;
        }

        text = _text;
        return text.Length > 0;
    }

    public static void SetText(string text)
    {
        text ??= string.Empty;

        if (SetTextOverride != null)
        {
            SetTextOverride(text);
            return;
        }

        _text = text;
    }

    public static void SetData(string format, object? value)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            throw new ArgumentException("Clipboard format is required.", nameof(format));
        }

        DataByFormat[format] = value;
    }

    public static bool TryGetData<T>(string format, out T? value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(format) || !DataByFormat.TryGetValue(format, out var boxed))
        {
            return false;
        }

        if (boxed is T typed)
        {
            value = typed;
            return true;
        }

        return false;
    }

    public static void ResetForTests()
    {
        _text = string.Empty;
        DataByFormat.Clear();
        GetTextOverride = null;
        SetTextOverride = null;
    }
}
