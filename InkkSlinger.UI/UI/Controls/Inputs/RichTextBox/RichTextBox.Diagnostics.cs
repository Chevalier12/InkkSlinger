using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace InkkSlinger;

public partial class RichTextBox
{
    private void TraceInvariants(string stage)
    {
        DetectUnexpectedFlattening(stage);
        var issues = new List<string>();
        ValidateElement(Document, expectedParent: null, issues);
        if (Document.Blocks.Count == 0)
        {
            issues.Add("FlowDocument has zero blocks.");
        }

        var textLength = GetText().Length;
        if (_caretIndex < 0 || _caretIndex > textLength)
        {
            issues.Add($"CaretIndex out-of-range ({_caretIndex}/{textLength}).");
        }

        var selectionStart = SelectionStart;
        var selectionLength = SelectionLength;
        if (selectionStart < 0 || selectionStart > textLength)
        {
            issues.Add($"SelectionStart out-of-range ({selectionStart}/{textLength}).");
        }

        if (selectionLength < 0 || selectionStart + selectionLength > textLength)
        {
            issues.Add($"SelectionLength invalid ({selectionStart}+{selectionLength}>{textLength}).");
        }
    }

    private void DetectUnexpectedFlattening(string stage)
    {
        var current = CaptureDocumentRichness(Document);
        var structureDropped =
            current.ListCount < _lastDocumentRichness.ListCount ||
            current.TableCount < _lastDocumentRichness.TableCount ||
            current.SectionCount < _lastDocumentRichness.SectionCount ||
            current.RichInlineCount < _lastDocumentRichness.RichInlineCount;
        if (structureDropped)
        {
        }

        if (_lastDocumentRichness.HasRichStructure &&
            !current.HasRichStructure &&
            current.IsPlainTextCompatible)
        {
        }

        _lastDocumentRichness = current;
    }

    private void RecordOperation(string operation, string details, [CallerMemberName] string caller = "")
    {
        var entry = new RecentOperationEntry(
            ++_recentOperationSequence,
            DateTime.UtcNow,
            caller,
            operation,
            details,
            _caretIndex,
            _selectionAnchor,
            SelectionStart,
            SelectionLength);
        _recentOperations.Enqueue(entry);
        while (_recentOperations.Count > RecentOperationCapacity)
        {
            _recentOperations.Dequeue();
        }
    }

    private IReadOnlyList<string> BuildRecentOperationLines()
    {
        var lines = new List<string>(_recentOperations.Count);
        foreach (var entry in _recentOperations)
        {
            lines.Add(
                $"#{entry.Sequence} utc={entry.Utc:O} caller={entry.Caller} op={entry.Operation} details={entry.Details} caret={entry.Caret} anchor={entry.Anchor} sel=({entry.SelectionStart},{entry.SelectionLength})");
        }

        return lines;
    }

    private static string SanitizeForLog(string value)
    {
        return value
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static DocumentRichnessSnapshot CaptureDocumentRichness(FlowDocument document)
    {
        var blocks = 0;
        var lists = 0;
        var tables = 0;
        var sections = 0;
        var richInlines = 0;
        var hyperlinks = 0;
        var hostedChildren = 0;
        foreach (var block in document.Blocks)
        {
            AccumulateBlockRichness(block, ref blocks, ref lists, ref tables, ref sections, ref richInlines, ref hyperlinks, ref hostedChildren);
        }

        return new DocumentRichnessSnapshot(
            blocks,
            lists,
            tables,
            sections,
            richInlines,
            hyperlinks,
            hostedChildren,
            HasRichStructure: lists > 0 || tables > 0 || sections > 0 || richInlines > 0,
            IsPlainTextCompatible: !DocumentContainsRichInlineFormatting(document));
    }

    private static void AccumulateBlockRichness(
        Block block,
        ref int blocks,
        ref int lists,
        ref int tables,
        ref int sections,
        ref int richInlines,
        ref int hyperlinks,
        ref int hostedChildren)
    {
        blocks++;
        switch (block)
        {
            case Paragraph paragraph:
                for (var i = 0; i < paragraph.Inlines.Count; i++)
                {
                    AccumulateInlineRichness(paragraph.Inlines[i], ref richInlines, ref hyperlinks, ref hostedChildren);
                }

                break;
            case InkkSlinger.List list:
                lists++;
                for (var i = 0; i < list.Items.Count; i++)
                {
                    var item = list.Items[i];
                    for (var j = 0; j < item.Blocks.Count; j++)
                    {
                        AccumulateBlockRichness(item.Blocks[j], ref blocks, ref lists, ref tables, ref sections, ref richInlines, ref hyperlinks, ref hostedChildren);
                    }
                }

                break;
            case Table table:
                tables++;
                for (var i = 0; i < table.RowGroups.Count; i++)
                {
                    var group = table.RowGroups[i];
                    for (var j = 0; j < group.Rows.Count; j++)
                    {
                        var row = group.Rows[j];
                        for (var k = 0; k < row.Cells.Count; k++)
                        {
                            var cell = row.Cells[k];
                            for (var m = 0; m < cell.Blocks.Count; m++)
                            {
                                AccumulateBlockRichness(cell.Blocks[m], ref blocks, ref lists, ref tables, ref sections, ref richInlines, ref hyperlinks, ref hostedChildren);
                            }
                        }
                    }
                }

                break;
            case Section section:
                sections++;
                for (var i = 0; i < section.Blocks.Count; i++)
                {
                    AccumulateBlockRichness(section.Blocks[i], ref blocks, ref lists, ref tables, ref sections, ref richInlines, ref hyperlinks, ref hostedChildren);
                }

                break;
            case BlockUIContainer blockUiContainer when blockUiContainer.Child != null:
                hostedChildren++;
                break;
        }
    }

    private static void AccumulateInlineRichness(Inline inline, ref int richInlines, ref int hyperlinks, ref int hostedChildren)
    {
        switch (inline)
        {
            case Bold bold:
                richInlines++;
                for (var i = 0; i < bold.Inlines.Count; i++)
                {
                    AccumulateInlineRichness(bold.Inlines[i], ref richInlines, ref hyperlinks, ref hostedChildren);
                }

                break;
            case Italic italic:
                richInlines++;
                for (var i = 0; i < italic.Inlines.Count; i++)
                {
                    AccumulateInlineRichness(italic.Inlines[i], ref richInlines, ref hyperlinks, ref hostedChildren);
                }

                break;
            case Underline underline:
                richInlines++;
                for (var i = 0; i < underline.Inlines.Count; i++)
                {
                    AccumulateInlineRichness(underline.Inlines[i], ref richInlines, ref hyperlinks, ref hostedChildren);
                }

                break;
            case Hyperlink hyperlink:
                richInlines++;
                hyperlinks++;
                for (var i = 0; i < hyperlink.Inlines.Count; i++)
                {
                    AccumulateInlineRichness(hyperlink.Inlines[i], ref richInlines, ref hyperlinks, ref hostedChildren);
                }

                break;
            case Span span:
                richInlines++;
                for (var i = 0; i < span.Inlines.Count; i++)
                {
                    AccumulateInlineRichness(span.Inlines[i], ref richInlines, ref hyperlinks, ref hostedChildren);
                }

                break;
            case InlineUIContainer inlineUiContainer when inlineUiContainer.Child != null:
                hostedChildren++;
                break;
        }
    }

    private static void ValidateElement(TextElement element, TextElement? expectedParent, List<string> issues)
    {
        if (!ReferenceEquals(element.Parent, expectedParent))
        {
            issues.Add($"{element.GetType().Name} parent mismatch.");
        }

        switch (element)
        {
            case FlowDocument document:
                for (var i = 0; i < document.Blocks.Count; i++)
                {
                    ValidateElement(document.Blocks[i], document, issues);
                }

                break;
            case Section section:
                for (var i = 0; i < section.Blocks.Count; i++)
                {
                    ValidateElement(section.Blocks[i], section, issues);
                }

                break;
            case Paragraph paragraph:
                for (var i = 0; i < paragraph.Inlines.Count; i++)
                {
                    ValidateElement(paragraph.Inlines[i], paragraph, issues);
                }

                break;
            case Span span:
                for (var i = 0; i < span.Inlines.Count; i++)
                {
                    ValidateElement(span.Inlines[i], span, issues);
                }

                break;
            case List list:
                for (var i = 0; i < list.Items.Count; i++)
                {
                    ValidateElement(list.Items[i], list, issues);
                }

                break;
            case ListItem listItem:
                for (var i = 0; i < listItem.Blocks.Count; i++)
                {
                    ValidateElement(listItem.Blocks[i], listItem, issues);
                }

                break;
            case Table table:
                for (var i = 0; i < table.RowGroups.Count; i++)
                {
                    ValidateElement(table.RowGroups[i], table, issues);
                }

                break;
            case TableRowGroup rowGroup:
                for (var i = 0; i < rowGroup.Rows.Count; i++)
                {
                    ValidateElement(rowGroup.Rows[i], rowGroup, issues);
                }

                break;
            case TableRow row:
                for (var i = 0; i < row.Cells.Count; i++)
                {
                    ValidateElement(row.Cells[i], row, issues);
                }

                break;
            case TableCell cell:
                if (cell.RowSpan <= 0 || cell.ColumnSpan <= 0)
                {
                    issues.Add("TableCell span values must be > 0.");
                }

                for (var i = 0; i < cell.Blocks.Count; i++)
                {
                    ValidateElement(cell.Blocks[i], cell, issues);
                }

                break;
        }
    }

    private readonly record struct RecentOperationEntry(
        int Sequence,
        DateTime Utc,
        string Caller,
        string Operation,
        string Details,
        int Caret,
        int Anchor,
        int SelectionStart,
        int SelectionLength);

    private readonly record struct DocumentRichnessSnapshot(
        int BlockCount,
        int ListCount,
        int TableCount,
        int SectionCount,
        int RichInlineCount,
        int HyperlinkCount,
        int HostedChildCount,
        bool HasRichStructure,
        bool IsPlainTextCompatible)
    {
        public static readonly DocumentRichnessSnapshot Empty = new(0, 0, 0, 0, 0, 0, 0, false, true);

        public string ToSummary()
        {
            return $"blocks={BlockCount},lists={ListCount},tables={TableCount},sections={SectionCount},richInlines={RichInlineCount},hyperlinks={HyperlinkCount},hostedChildren={HostedChildCount},plain={IsPlainTextCompatible}";
        }
    }
}