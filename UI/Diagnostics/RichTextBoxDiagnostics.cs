using System;
using System.Diagnostics;

namespace InkkSlinger;

internal static class RichTextBoxDiagnostics
{
    private static readonly bool IsLayoutEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_RICHTEXT_LAYOUT_LOGS"), "1", StringComparison.Ordinal);

    private static readonly bool IsEditEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_RICHTEXT_EDIT_LOGS"), "1", StringComparison.Ordinal);

    private static readonly bool IsClipboardEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_RICHTEXT_CLIPBOARD_LOGS"), "1", StringComparison.Ordinal);

    public static void ObserveLayout(bool cacheHit, double elapsedMs, int textLength)
    {
        if (!IsLayoutEnabled)
        {
            return;
        }

        var line = $"[RichTextLayout] hit={cacheHit} ms={elapsedMs:0.000} textLen={textLength}";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }

    public static void ObserveEdit(string command, double elapsedMs, int selectionStart, int selectionLength, int caretAfter)
    {
        if (!IsEditEnabled)
        {
            return;
        }

        var line = $"[RichTextEdit] cmd={command} ms={elapsedMs:0.000} sel=({selectionStart},{selectionLength}) caret={caretAfter}";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }

    public static void ObserveClipboard(string operation, bool usedRichPayload, bool fallbackToText, double elapsedMs)
    {
        if (!IsClipboardEnabled)
        {
            return;
        }

        var line = $"[RichTextClipboard] op={operation} rich={usedRichPayload} fallback={fallbackToText} ms={elapsedMs:0.000}";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }
}
