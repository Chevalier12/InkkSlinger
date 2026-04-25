using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace InkkSlinger;

public partial class RichTextBox
{
    public void Copy()
    {
        ExecuteCopy();
    }

    public void Cut()
    {
        ExecuteCut();
    }

    public void Paste()
    {
        ExecutePaste();
    }

    public bool CanLoad(string dataFormat)
    {
        return RichTextBoxClipboardCodec.IsSupportedDataFormat(dataFormat);
    }

    public bool CanSave(string dataFormat)
    {
        return RichTextBoxClipboardCodec.IsSupportedDataFormat(dataFormat);
    }

    public void Load(Stream stream, string dataFormat)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!CanLoad(dataFormat))
        {
            throw new NotSupportedException($"RichTextBox does not support loading format '{dataFormat}'.");
        }

        var payload = RichTextBoxClipboardCodec.ReadStreamPayload(stream);
        var document = RichTextBoxClipboardCodec.DeserializeDocumentPayload(payload, dataFormat);
        ApplyLoadedDocument(document, "LoadDocument");
    }

    public void Save(Stream stream, string dataFormat)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!CanSave(dataFormat))
        {
            throw new NotSupportedException($"RichTextBox does not support saving format '{dataFormat}'.");
        }

        var payload = RichTextBoxClipboardCodec.SerializeDocumentPayload(Document, dataFormat);
        RichTextBoxClipboardCodec.WriteStreamPayload(stream, payload);
    }

    public void LoadSelection(Stream stream, string dataFormat)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (IsReadOnly)
        {
            throw new InvalidOperationException("Cannot load selection when RichTextBox is read-only.");
        }

        if (!CanLoad(dataFormat))
        {
            throw new NotSupportedException($"RichTextBox does not support loading format '{dataFormat}'.");
        }

        var payload = RichTextBoxClipboardCodec.ReadStreamPayload(stream);
        if (RichTextBoxClipboardCodec.IsRichFragmentFormat(dataFormat))
        {
            var fragment = RichTextBoxClipboardCodec.DeserializeFragmentPayload(payload, dataFormat);
            ReplaceSelectionWithFragment(fragment, "LoadSelection", GroupingPolicy.StructuralAtomic);
            return;
        }

        var text = RichTextBoxClipboardCodec.DeserializeTextPayload(payload, dataFormat);
        ReplaceSelection(NormalizeNewlines(text), "LoadSelection", GroupingPolicy.StructuralAtomic);
    }

    public void SaveSelection(Stream stream, string dataFormat)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!CanSave(dataFormat))
        {
            throw new NotSupportedException($"RichTextBox does not support saving format '{dataFormat}'.");
        }

        var payload = RichTextBoxClipboardCodec.SerializeSelectionPayload(Document, SelectionStart, SelectionLength, dataFormat);
        RichTextBoxClipboardCodec.WriteStreamPayload(stream, payload);
    }

    private void ExecuteCopy()
    {
        if (SelectionLength <= 0)
        {
            return;
        }

        var serializeStart = Stopwatch.GetTimestamp();
        var selected = GetText().Substring(SelectionStart, SelectionLength);
        TextClipboard.SetText(selected);
        var richSlice = FlowDocumentSerializer.SerializeRange(Document, SelectionStart, SelectionStart + SelectionLength);
        RichTextBoxClipboardCodec.PublishRichClipboardPayloads(richSlice, selected);
        var elapsedMs = Stopwatch.GetElapsedTime(serializeStart).TotalMilliseconds;
        _perfTracker.RecordClipboardSerialize(elapsedMs);
    }

    private void ExecuteCut()
    {
        if (SelectionLength <= 0 || IsReadOnly)
        {
            return;
        }

        var serializeStart = Stopwatch.GetTimestamp();
        var selected = GetText().Substring(SelectionStart, SelectionLength);
        TextClipboard.SetText(selected);
        var richSlice = FlowDocumentSerializer.SerializeRange(Document, SelectionStart, SelectionStart + SelectionLength);
        RichTextBoxClipboardCodec.PublishRichClipboardPayloads(richSlice, selected);
        var elapsedMs = Stopwatch.GetElapsedTime(serializeStart).TotalMilliseconds;
        _perfTracker.RecordClipboardSerialize(elapsedMs);
        ReplaceSelection(string.Empty, "CutSelection", GroupingPolicy.StructuralAtomic);
    }

    private void ExecutePaste()
    {
        if (IsReadOnly)
        {
            return;
        }

        var clipboardSnapshot = TextClipboard.CaptureSnapshot();
        var fallbackToText = false;
        var richStructuredTarget = IsRichStructuredDocument() && !IsFullDocumentSelection();

        if (RichTextBoxClipboardCodec.TryGetRichClipboardPayload(clipboardSnapshot, out var richPayload, out var richFormat))
        {
            var deserializeStart = Stopwatch.GetTimestamp();
            try
            {
                var fragment = RichTextBoxClipboardCodec.DeserializeFragmentPayload(richPayload, richFormat);
                _perfTracker.RecordClipboardDeserialize(Stopwatch.GetElapsedTime(deserializeStart).TotalMilliseconds);
                if (!richStructuredTarget && TryPasteRichFragment(fragment))
                {
                    return;
                }

                fallbackToText = true;
                var fragmentText = NormalizeNewlines(DocumentEditing.GetText(fragment));
                if (richStructuredTarget && TryPastePlainTextPreservingStructure(fragmentText, out _))
                {
                    return;
                }
            }
            catch (Exception)
            {
                _perfTracker.RecordClipboardDeserialize(Stopwatch.GetElapsedTime(deserializeStart).TotalMilliseconds);
                fallbackToText = true;
            }
        }

        if (!clipboardSnapshot.TryGetText(out var pasted))
        {
            return;
        }

        var normalizedPasted = NormalizeNewlines(pasted);
        if (richStructuredTarget && TryPastePlainTextPreservingStructure(normalizedPasted, out _))
        {
            return;
        }

        ReplaceSelection(normalizedPasted, fallbackToText ? "PasteFallbackText" : "Paste", GroupingPolicy.StructuralAtomic);
    }

    private bool TryPastePlainTextPreservingStructure(string text, out PasteStructuredStats stats)
    {
        stats = default;
        var hadSelection = SelectionLength > 0;
        if (SelectionLength > 0 &&
            !TryDeleteSelectionPreservingStructure("PasteDeleteSelection", GroupingPolicy.StructuralAtomic))
        {
            return false;
        }

        stats = stats with { DeleteSelectionApplied = hadSelection };

        var index = 0;
        while (index < text.Length)
        {
            var ch = text[index];
            if (ch == '\r')
            {
                index++;
                continue;
            }

            if (ch == '\n')
            {
                if (!TryInsertParagraphBreakWithinStructuredParagraph("PasteStructuredEnter", GroupingPolicy.StructuralAtomic))
                {
                    return false;
                }

                stats = stats with { EnterCount = stats.EnterCount + 1 };
                index++;
                continue;
            }

            var segmentStart = index;
            while (index < text.Length && text[index] is not '\r' and not '\n')
            {
                index++;
            }

            var segmentLength = index - segmentStart;
            if (segmentLength == 0)
            {
                continue;
            }

            var segment = text.Substring(segmentStart, segmentLength);
            if (!HandleTextCompositionFromInput(segment))
            {
                return false;
            }

            stats = stats with { TextCompositionCount = stats.TextCompositionCount + 1 };
        }

        return true;
    }

    private void ApplyLoadedDocument(FlowDocument document, string reason)
    {
        ExecuteDocumentChangeBatch(() => Document = document);
        _typingBoldActive = false;
        _typingItalicActive = false;
        _typingUnderlineActive = false;
        UpdateSelectionState(0, 0, ensureCaretVisible: false);
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        EnsureCaretVisible();
        InvalidateMeasure();
        InvalidateVisualWithReason(reason);
    }

    private bool CanPasteFromClipboard()
    {
        return TextClipboard.HasPastePayloadFast();
    }

    private bool TryPasteRichFragment(FlowDocument fragment)
    {
        if (fragment.Blocks.Count == 0)
        {
            return false;
        }

        ReplaceSelectionWithFragment(fragment, "PasteRichFragment", GroupingPolicy.StructuralAtomic);
        return true;
    }

    private readonly record struct PasteStructuredStats(int TextCompositionCount, int EnterCount, bool DeleteSelectionApplied);
}