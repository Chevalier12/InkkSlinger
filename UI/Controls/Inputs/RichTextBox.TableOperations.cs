using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class RichTextBox
{
    private void ExecuteInsertTable()
    {
        if (IsReadOnly)
        {
            return;
        }

        ApplyStructuralEdit(
            "InsertTable",
            GroupingPolicy.StructuralAtomic,
            static (doc, start, length, _) =>
            {
                var table = CreateDefaultTable();
                var merged = BuildDocumentWithFragment(doc, table, start, length);
                DocumentEditing.ReplaceDocumentContent(doc, merged);
                return true;
            },
            postApply: (_, start, _, _) =>
            {
                if (TryGetTableCellStartOffsetAtOrAfter(Document, start, out var offset))
                {
                    SetCaret(offset, extendSelection: false);
                }
            });
    }

    private void ExecuteSplitCell()
    {
        if (IsReadOnly)
        {
            return;
        }

        ApplyStructuralEdit(
            "SplitCell",
            GroupingPolicy.StructuralAtomic,
            static (doc, _, _, caret) =>
            {
                if (!TryGetActiveTableCell(doc, caret, out var active))
                {
                    return false;
                }

                var next = new TableCell();
                next.Blocks.Add(CreateParagraph(string.Empty));
                if (active.Cell.ColumnSpan > 1)
                {
                    active.Cell.ColumnSpan -= 1;
                }

                active.Row.Cells.Insert(active.CellIndex + 1, next);
                return true;
            });
    }

    private void ExecuteMergeCells()
    {
        if (IsReadOnly)
        {
            return;
        }

        ApplyStructuralEdit(
            "MergeCells",
            GroupingPolicy.StructuralAtomic,
            static (doc, _, _, caret) =>
            {
                if (!TryGetActiveTableCell(doc, caret, out var active))
                {
                    return false;
                }

                var nextIndex = active.CellIndex + 1;
                if (nextIndex >= active.Row.Cells.Count)
                {
                    return false;
                }

                var next = active.Row.Cells[nextIndex];
                active.Cell.ColumnSpan += Math.Max(1, next.ColumnSpan);
                while (next.Blocks.Count > 0)
                {
                    var block = next.Blocks[0];
                    next.Blocks.RemoveAt(0);
                    active.Cell.Blocks.Add(block);
                }

                active.Row.Cells.RemoveAt(nextIndex);
                return true;
            });
    }

    private bool CanMergeActiveCell()
    {
        if (!TryGetActiveTableCell(Document, _caretIndex, out var active))
        {
            return false;
        }

        return active.CellIndex + 1 < active.Row.Cells.Count;
    }

    private static Table CreateDefaultTable()
    {
        var table = new Table();
        var group = new TableRowGroup();
        for (var rowIndex = 0; rowIndex < 2; rowIndex++)
        {
            var row = new TableRow();
            for (var cellIndex = 0; cellIndex < 2; cellIndex++)
            {
                var cell = new TableCell();
                cell.Blocks.Add(CreateParagraph(string.Empty));
                row.Cells.Add(cell);
            }

            group.Rows.Add(row);
        }

        table.RowGroups.Add(group);
        return table;
    }

    private bool TryMoveCaretToAdjacentTableCell(bool forward)
    {
        if (SelectionLength > 0)
        {
            return false;
        }

        if (!TryGetActiveTableCell(Document, _caretIndex, out var active))
        {
            return false;
        }

        var cells = CollectTableCells(Document);
        var currentIndex = -1;
        for (var i = 0; i < cells.Count; i++)
        {
            if (ReferenceEquals(cells[i].Cell, active.Cell) && ReferenceEquals(cells[i].Row, active.Row))
            {
                currentIndex = i;
                break;
            }
        }

        if (currentIndex < 0)
        {
            return false;
        }

        var targetIndex = forward ? currentIndex + 1 : currentIndex - 1;
        if (targetIndex < 0 || targetIndex >= cells.Count)
        {
            return false;
        }

        SetCaret(cells[targetIndex].StartOffset, extendSelection: false);
        InvalidateVisual();
        return true;
    }

    private bool TryHandleTableBoundaryDeletion(bool backspace)
    {
        if (IsReadOnly || SelectionLength > 0)
        {
            return false;
        }

        var cells = CollectTableCells(Document);
        var currentIndex = -1;
        for (var i = 0; i < cells.Count; i++)
        {
            if (_caretIndex >= cells[i].StartOffset && _caretIndex <= cells[i].EndOffset)
            {
                currentIndex = i;
                break;
            }
        }

        if (currentIndex < 0)
        {
            return false;
        }

        if (backspace && _caretIndex <= cells[currentIndex].StartOffset)
        {
            if (currentIndex == 0)
            {
                return true;
            }

            SetCaret(cells[currentIndex - 1].EndOffset, extendSelection: false);
            InvalidateVisual();
            return true;
        }

        if (!backspace && _caretIndex >= cells[currentIndex].EndOffset)
        {
            if (currentIndex >= cells.Count - 1)
            {
                return true;
            }

            SetCaret(cells[currentIndex + 1].StartOffset, extendSelection: false);
            InvalidateVisual();
            return true;
        }

        return false;
    }

    private static bool TryGetTableCellStartOffsetAtOrAfter(FlowDocument document, int minOffset, out int offset)
    {
        var cells = CollectTableCells(document);
        if (cells.Count == 0)
        {
            offset = 0;
            return false;
        }

        var best = cells[0].StartOffset;
        for (var i = 0; i < cells.Count; i++)
        {
            if (cells[i].StartOffset >= minOffset)
            {
                best = cells[i].StartOffset;
                break;
            }
        }

        offset = best;
        return true;
    }

    private static bool TryGetActiveTableCell(FlowDocument document, int caretOffset, out TableCellSelectionInfo info)
    {
        var cells = CollectTableCells(document);
        for (var i = 0; i < cells.Count; i++)
        {
            if (caretOffset >= cells[i].StartOffset && caretOffset <= cells[i].EndOffset)
            {
                info = cells[i];
                return true;
            }
        }

        info = default;
        return false;
    }

    private static List<TableCellSelectionInfo> CollectTableCells(FlowDocument document)
    {
        var paragraphs = CollectParagraphEntries(document);
        var result = new List<TableCellSelectionInfo>();
        for (var i = 0; i < paragraphs.Count; i++)
        {
            if (TryGetAncestor<TableCell>(paragraphs[i].Paragraph, out var cell) &&
                TryGetAncestor<TableRow>(paragraphs[i].Paragraph, out var row))
            {
                var found = false;
                for (var j = 0; j < result.Count; j++)
                {
                    if (ReferenceEquals(result[j].Cell, cell))
                    {
                        var current = result[j];
                        var updated = current with
                        {
                            StartOffset = Math.Min(current.StartOffset, paragraphs[i].StartOffset),
                            EndOffset = Math.Max(current.EndOffset, paragraphs[i].EndOffset)
                        };
                        result[j] = updated;
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    continue;
                }

                var cellIndex = row.Cells.IndexOf(cell);
                result.Add(
                    new TableCellSelectionInfo(
                        row,
                        cell,
                        cellIndex,
                        paragraphs[i].StartOffset,
                        paragraphs[i].EndOffset));
            }
        }

        result.Sort(static (left, right) => left.StartOffset.CompareTo(right.StartOffset));
        return result;
    }

    private void DrawTableBorders(SpriteBatch spriteBatch, LayoutRect textRect, DocumentLayoutResult layout)
    {
        if (layout.TableCellBounds.Count == 0)
        {
            return;
        }

        var stroke = 1f;
        var color = new Color(95, 95, 95) * Opacity;
        for (var i = 0; i < layout.TableCellBounds.Count; i++)
        {
            var cell = layout.TableCellBounds[i];
            UiDrawing.DrawRectStroke(
                spriteBatch,
                new LayoutRect(textRect.X + cell.X - _horizontalOffset, textRect.Y + cell.Y - _verticalOffset, cell.Width, cell.Height),
                stroke,
                color);
        }
    }

    private readonly record struct TableCellSelectionInfo(
        TableRow Row,
        TableCell Cell,
        int CellIndex,
        int StartOffset,
        int EndOffset);
}
