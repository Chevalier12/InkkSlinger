using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

internal static class RichTextBoxDiagnostics
{
    private static readonly bool IsLayoutEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_RICHTEXT_LAYOUT_LOGS"), "1", StringComparison.Ordinal);

    private static readonly bool IsEditEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_RICHTEXT_EDIT_LOGS"), "1", StringComparison.Ordinal);

    private static readonly bool IsClipboardEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_RICHTEXT_CLIPBOARD_LOGS"), "1", StringComparison.Ordinal);

    private static readonly bool IsInvariantEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_RICHTEXT_INVARIANT_LOGS"), "1", StringComparison.Ordinal);

    private static readonly bool IsLayoutInvalidationEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_RICHTEXT_INVALIDATION_LOGS"), "1", StringComparison.Ordinal);

    private static readonly bool IsCommandTraceEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_RICHTEXT_COMMAND_TRACE_LOGS"), "1", StringComparison.Ordinal);

    private static readonly bool IsSelectionEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_RICHTEXT_SELECTION_LOGS"), "1", StringComparison.Ordinal);

    private static readonly bool IsUndoEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_RICHTEXT_UNDO_LOGS"), "1", StringComparison.Ordinal);

    private static readonly bool IsClipboardPayloadEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_RICHTEXT_CLIPBOARD_PAYLOAD_LOGS"), "1", StringComparison.Ordinal);

    private static readonly bool IsFlatteningEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_RICHTEXT_FLATTEN_LOGS"), "1", StringComparison.Ordinal);

    private static readonly bool IsPasteCpuEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_RICHTEXT_PASTE_CPU_LOGS"), "1", StringComparison.Ordinal);

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

    public static void ObserveInvariant(string stage, bool isValid, string details)
    {
        if (!IsInvariantEnabled)
        {
            return;
        }

        var line = $"[RichTextInvariant] stage={stage} valid={isValid} details={details}";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }

    public static void ObserveLayoutInvalidation(string reason, int textLength, int caret, int selectionStart, int selectionLength)
    {
        if (!IsLayoutInvalidationEnabled)
        {
            return;
        }

        var line = $"[RichTextInvalidation] reason={reason} textLen={textLength} caret={caret} sel=({selectionStart},{selectionLength})";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }

    public static void ObserveCommandTrace(
        string input,
        string command,
        bool canExecute,
        bool handled,
        int mutationDelta,
        int undoDepthBefore,
        int undoDepthAfter,
        int redoDepthBefore,
        int redoDepthAfter)
    {
        if (!IsCommandTraceEnabled)
        {
            return;
        }

        var line = $"[RichTextCommandTrace] input={input} cmd={command} can={canExecute} handled={handled} mutDelta={mutationDelta} undo={undoDepthBefore}->{undoDepthAfter} redo={redoDepthBefore}->{redoDepthAfter}";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }

    public static void ObserveSelection(
        int caret,
        int anchor,
        int selectionStart,
        int selectionLength,
        int lineIndex,
        int hitOffset,
        LayoutRect caretRect,
        int selectionRectCount)
    {
        if (!IsSelectionEnabled)
        {
            return;
        }

        var line = $"[RichTextSelection] caret={caret} anchor={anchor} sel=({selectionStart},{selectionLength}) line={lineIndex} hit={hitOffset} caretRect=({caretRect.X:0.##},{caretRect.Y:0.##},{caretRect.Width:0.##},{caretRect.Height:0.##}) rects={selectionRectCount}";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }

    public static void ObserveUndo(
        string operation,
        bool handled,
        int undoDepthBefore,
        int undoDepthAfter,
        int redoDepthBefore,
        int redoDepthAfter,
        int undoOpsAfter,
        int redoOpsAfter)
    {
        if (!IsUndoEnabled)
        {
            return;
        }

        var line = $"[RichTextUndo] op={operation} handled={handled} undoDepth={undoDepthBefore}->{undoDepthAfter} redoDepth={redoDepthBefore}->{redoDepthAfter} undoOps={undoOpsAfter} redoOps={redoOpsAfter}";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }

    public static void ObserveClipboardPayload(string operation, string payloadKind, int payloadBytes, string branch)
    {
        if (!IsClipboardPayloadEnabled)
        {
            return;
        }

        var line = $"[RichTextClipboardPayload] op={operation} kind={payloadKind} bytes={payloadBytes} branch={branch}";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }

    public static void ObserveFlattening(
        string stage,
        string beforeSummary,
        string afterSummary,
        IReadOnlyList<string> recentOperations)
    {
        if (!IsFlatteningEnabled)
        {
            return;
        }

        var header = $"[RichTextFlatten] stage={stage} before={beforeSummary} after={afterSummary}";
        Debug.WriteLine(header);
        Console.WriteLine(header);
        for (var i = 0; i < recentOperations.Count; i++)
        {
            var line = $"[RichTextFlattenOp] {recentOperations[i]}";
            Debug.WriteLine(line);
            Console.WriteLine(line);
        }
    }

    public static void ObserveStructureTransition(
        string stage,
        string beforeSummary,
        string afterSummary,
        IReadOnlyList<string> recentOperations)
    {
        if (!IsFlatteningEnabled)
        {
            return;
        }

        var header = $"[RichTextStructureTransition] stage={stage} before={beforeSummary} after={afterSummary}";
        Debug.WriteLine(header);
        Console.WriteLine(header);
        for (var i = 0; i < recentOperations.Count; i++)
        {
            var line = $"[RichTextStructureOp] {recentOperations[i]}";
            Debug.WriteLine(line);
            Console.WriteLine(line);
        }
    }

    public static void ObserveRichFallbackBlocked(
        string route,
        string commandType,
        int selectionStart,
        int selectionLength,
        int replacementLength,
        IReadOnlyList<string> recentOperations)
    {
        if (!IsFlatteningEnabled)
        {
            return;
        }

        var header = $"[RichTextFallbackBlocked] route={route} cmd={commandType} sel=({selectionStart},{selectionLength}) replLen={replacementLength}";
        Debug.WriteLine(header);
        Console.WriteLine(header);
        for (var i = 0; i < recentOperations.Count; i++)
        {
            var line = $"[RichTextFallbackBlockedOp] {recentOperations[i]}";
            Debug.WriteLine(line);
            Console.WriteLine(line);
        }
    }

    public static void ObservePasteCpu(
        string route,
        string richFormat,
        bool usedRichPayload,
        bool fallbackToText,
        int payloadBytes,
        int pastedChars,
        bool richStructuredTarget,
        int selectionStartBefore,
        int selectionLengthBefore,
        int selectionStartAfter,
        int selectionLengthAfter,
        int caretBefore,
        int caretAfter,
        int textLengthBefore,
        int textLengthAfter,
        string structureBefore,
        string structureAfter,
        int structuredTextCompositionCount,
        int structuredEnterCount,
        bool structuredDeleteSelectionApplied,
        double lookupRichMs,
        double deserializeMs,
        double readTextMs,
        double normalizeMs,
        double structuredInsertMs,
        double replaceSelectionMs,
        long clipboardSyncCalls,
        long clipboardSyncThrottleSkips,
        long clipboardExternalReads,
        double clipboardExternalReadMs,
        string clipboardLastSyncSource,
        double clipboardLastSyncMs,
        bool clipboardLastSyncChanged,
        bool clipboardLastSyncThrottled,
        double totalMs)
    {
        if (!IsPasteCpuEnabled)
        {
            return;
        }

        var line =
            $"[RichTextPasteCpu] route={route} richFmt={richFormat} rich={usedRichPayload} fallback={fallbackToText} payloadBytes={payloadBytes} chars={pastedChars} richTarget={richStructuredTarget} selBefore=({selectionStartBefore},{selectionLengthBefore}) selAfter=({selectionStartAfter},{selectionLengthAfter}) caret={caretBefore}->{caretAfter} textLen={textLengthBefore}->{textLengthAfter} structBefore={structureBefore} structAfter={structureAfter} structuredOps=(text:{structuredTextCompositionCount},enter:{structuredEnterCount},delSel:{structuredDeleteSelectionApplied}) lookupRichMs={lookupRichMs:0.000} deserializeMs={deserializeMs:0.000} readTextMs={readTextMs:0.000} normalizeMs={normalizeMs:0.000} structuredMs={structuredInsertMs:0.000} replaceMs={replaceSelectionMs:0.000} clipSync=(calls:{clipboardSyncCalls},throttle:{clipboardSyncThrottleSkips},reads:{clipboardExternalReads},readMs:{clipboardExternalReadMs:0.000},last:{clipboardLastSyncSource},lastMs:{clipboardLastSyncMs:0.000},changed:{clipboardLastSyncChanged},throttled:{clipboardLastSyncThrottled}) totalMs={totalMs:0.000}";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }
}
