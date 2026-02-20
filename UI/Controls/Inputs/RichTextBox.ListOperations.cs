using System;
using System.Collections.Generic;

namespace InkkSlinger;

public partial class RichTextBox
{
    private bool CanTabBackward()
    {
        if (IsReadOnly)
        {
            return false;
        }

        if (TryGetActiveTableCell(Document, _caretIndex, out TableCellSelectionInfo _))
        {
            return true;
        }

        if (CanExecuteListLevelChange(increase: false))
        {
            return true;
        }

        // Keep Shift+Tab handled as a no-op outside list/table contexts to avoid focus traversal.
        return true;
    }

    private bool CanExecuteListLevelChange(bool increase)
    {
        if (IsReadOnly)
        {
            return false;
        }

        var selection = ResolveSelectedParagraphs(Document, SelectionStart, SelectionLength, _caretIndex);
        if (selection.Count == 0)
        {
            return false;
        }

        if (increase)
        {
            return true;
        }

        for (var i = 0; i < selection.Count; i++)
        {
            if (selection[i].Paragraph.Parent is ListItem)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ListItemHasVisibleContent(ListItem item)
    {
        for (var i = 0; i < item.Blocks.Count; i++)
        {
            if (item.Blocks[i] is not Paragraph paragraph)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(FlowDocumentPlainText.GetInlineText(paragraph.Inlines)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryApplyListEnterBehavior(
        Paragraph paragraph,
        ListItem listItem,
        InkkSlinger.List list,
        string leftText,
        string rightText,
        out FlowDocument afterDocument,
        out int caretAfter)
    {
        afterDocument = null!;
        caretAfter = 0;
        var isCurrentEmpty = string.IsNullOrEmpty(leftText) && string.IsNullOrEmpty(rightText);
        if (isCurrentEmpty)
        {
            var listBlock = (Block)list;
            if (!RemoveListItem(listItem))
            {
                return false;
            }

            if (!InsertParagraphAfterBlock(listBlock, CreateParagraph(string.Empty)))
            {
                return false;
            }

            if (list.Items.Count == 0)
            {
                RemoveBlockFromParent(listBlock);
            }

            afterDocument = GetDocumentFromElement(listBlock);
            if (afterDocument is null)
            {
                return false;
            }

            var inserted = TryFindParagraphAfterBlock(listBlock);
            if (inserted is null)
            {
                return false;
            }

            caretAfter = FindParagraphStartOffset(afterDocument, inserted);
            return caretAfter >= 0;
        }

        ReplaceParagraphTextPreservingSimpleWrappers(paragraph, leftText);
        var newItem = new ListItem();
        var insertedParagraph = CreateParagraph(rightText);
        newItem.Blocks.Add(insertedParagraph);
        var itemIndex = list.Items.IndexOf(listItem);
        if (itemIndex < 0)
        {
            return false;
        }

        list.Items.Insert(itemIndex + 1, newItem);
        afterDocument = GetDocumentFromElement(list);
        if (afterDocument is null)
        {
            return false;
        }

        caretAfter = FindParagraphStartOffset(afterDocument, insertedParagraph);
        return caretAfter >= 0;
    }

    private static bool RemoveListItem(ListItem item)
    {
        return item.Parent is InkkSlinger.List list && list.Items.Remove(item);
    }

    private void ExecuteIncreaseListLevel()
    {
        if (IsReadOnly)
        {
            return;
        }

        ApplyStructuralEdit(
            "IncreaseListLevel",
            GroupingPolicy.StructuralAtomic,
            static (doc, start, length, caret) =>
            {
                var selected = ResolveSelectedParagraphs(doc, start, length, caret);
                if (selected.Count == 0)
                {
                    return false;
                }

                var changed = false;
                var paragraphsToListify = new List<Paragraph>();
                for (var i = 0; i < selected.Count; i++)
                {
                    var paragraph = selected[i].Paragraph;
                    if (paragraph.Parent is ListItem item &&
                        item.Parent is InkkSlinger.List parentList)
                    {
                        var index = parentList.Items.IndexOf(item);
                        if (index <= 0)
                        {
                            continue;
                        }

                        var previous = parentList.Items[index - 1];
                        var nested = GetOrCreateNestedList(previous, parentList.IsOrdered);
                        parentList.Items.Remove(item);
                        nested.Items.Add(item);
                        changed = true;
                        continue;
                    }

                    paragraphsToListify.Add(paragraph);
                }

                if (paragraphsToListify.Count > 0 &&
                    ConvertParagraphsToLists(paragraphsToListify))
                {
                    changed = true;
                }

                return changed;
            });
    }

    private void ExecuteDecreaseListLevel()
    {
        if (IsReadOnly)
        {
            return;
        }

        ApplyStructuralEdit(
            "DecreaseListLevel",
            GroupingPolicy.StructuralAtomic,
            static (doc, start, length, caret) =>
            {
                var selected = ResolveSelectedParagraphs(doc, start, length, caret);
                if (selected.Count == 0)
                {
                    return false;
                }

                var changed = false;
                for (var i = 0; i < selected.Count; i++)
                {
                    if (TryOutdentParagraph(selected[i].Paragraph))
                    {
                        changed = true;
                    }
                }

                return changed;
            });
    }

    private bool TryInsertSpaceAtListTableBoundary()
    {
        if (SelectionLength != 0 || _lastSelectionHitTestOffset >= 0)
        {
            return false;
        }

        var handled = false;
        var caretAfter = -1;
        ApplyStructuralEdit(
            "InsertSpaceListBoundary",
            GroupingPolicy.StructuralAtomic,
            (doc, start, length, _) =>
            {
                if (length != 0)
                {
                    return false;
                }

                var entries = CollectParagraphEntries(doc);
                if (entries.Count == 0)
                {
                    return false;
                }

                var paragraphIndex = -1;
                for (var i = 0; i < entries.Count; i++)
                {
                    if (start >= entries[i].StartOffset && start <= entries[i].EndOffset)
                    {
                        paragraphIndex = i;
                        break;
                    }
                }

                if (paragraphIndex < 0)
                {
                    return false;
                }

                var paragraph = entries[paragraphIndex].Paragraph;
                if (!string.IsNullOrEmpty(FlowDocumentPlainText.GetInlineText(paragraph.Inlines)))
                {
                    return false;
                }

                if (paragraph.Parent is not ListItem currentItem || currentItem.Parent is not InkkSlinger.List list)
                {
                    return false;
                }

                var itemIndex = list.Items.IndexOf(currentItem);
                if (itemIndex <= 0 || itemIndex != list.Items.Count - 1)
                {
                    return false;
                }

                if (!HasFollowingSiblingBlockOfType<Table>(list))
                {
                    return false;
                }

                if (currentItem.Blocks.Count != 1 || !ReferenceEquals(currentItem.Blocks[0], paragraph))
                {
                    return false;
                }

                var previousItem = list.Items[itemIndex - 1];
                currentItem.Blocks.Remove(paragraph);
                previousItem.Blocks.Add(paragraph);
                if (currentItem.Blocks.Count == 0)
                {
                    list.Items.RemoveAt(itemIndex);
                }

                ReplaceParagraphTextPreservingSimpleWrappers(paragraph, " ");
                var startOffset = FindParagraphStartOffset(doc, paragraph);
                if (startOffset < 0)
                {
                    return false;
                }

                caretAfter = startOffset + 1;
                handled = true;
                return true;
            },
            postApply: (_, _, _, _) =>
            {
                _caretIndex = Math.Max(0, caretAfter);
            });
        return handled;
    }

    private static bool ConvertParagraphsToLists(IReadOnlyList<Paragraph> paragraphs)
    {
        if (paragraphs.Count == 0)
        {
            return false;
        }

        var changed = false;
        var groups = new Dictionary<TextElement, List<Paragraph>>();
        for (var i = 0; i < paragraphs.Count; i++)
        {
            if (paragraphs[i].Parent is not TextElement owner)
            {
                continue;
            }

            if (!groups.TryGetValue(owner, out var group))
            {
                group = [];
                groups[owner] = group;
            }

            group.Add(paragraphs[i]);
        }

        foreach (var pair in groups)
        {
            if (!TryGetParagraphBlockCollection(pair.Key, out var blocks))
            {
                continue;
            }

            var indexed = new List<(int Index, Paragraph Paragraph)>();
            for (var i = 0; i < pair.Value.Count; i++)
            {
                var index = blocks.IndexOf(pair.Value[i]);
                if (index >= 0)
                {
                    indexed.Add((index, pair.Value[i]));
                }
            }

            if (indexed.Count == 0)
            {
                continue;
            }

            indexed.Sort(static (left, right) => left.Index.CompareTo(right.Index));
            var cursor = 0;
            while (cursor < indexed.Count)
            {
                var startIndex = indexed[cursor].Index;
                var endCursor = cursor + 1;
                while (endCursor < indexed.Count && indexed[endCursor].Index == indexed[endCursor - 1].Index + 1)
                {
                    endCursor++;
                }

                var list = new InkkSlinger.List();
                for (var i = endCursor - 1; i >= cursor; i--)
                {
                    blocks.RemoveAt(indexed[i].Index);
                }

                for (var i = cursor; i < endCursor; i++)
                {
                    var item = new ListItem();
                    item.Blocks.Add(indexed[i].Paragraph);
                    list.Items.Add(item);
                }

                blocks.Insert(startIndex, list);
                changed = true;
                cursor = endCursor;
            }
        }

        return changed;
    }

    private static bool TryGetParagraphBlockCollection(TextElement owner, out IList<Block> blocks)
    {
        switch (owner)
        {
            case FlowDocument document:
                blocks = document.Blocks;
                return true;
            case Section section:
                blocks = section.Blocks;
                return true;
            case ListItem item:
                blocks = item.Blocks;
                return true;
            case TableCell cell:
                blocks = cell.Blocks;
                return true;
            default:
                blocks = Array.Empty<Block>();
                return false;
        }
    }

    private static bool TryOutdentParagraph(Paragraph paragraph)
    {
        if (paragraph.Parent is not ListItem item || item.Parent is not InkkSlinger.List list)
        {
            return false;
        }

        if (list.Parent is ListItem parentItem && parentItem.Parent is InkkSlinger.List parentList)
        {
            var itemIndex = list.Items.IndexOf(item);
            if (itemIndex < 0)
            {
                return false;
            }

            list.Items.RemoveAt(itemIndex);
            var parentIndex = parentList.Items.IndexOf(parentItem);
            parentList.Items.Insert(parentIndex + 1, item);
            if (list.Items.Count == 0)
            {
                parentItem.Blocks.Remove(list);
            }

            return true;
        }

        if (list.Parent is FlowDocument document)
        {
            var listIndex = document.Blocks.IndexOf(list);
            var itemIndex = list.Items.IndexOf(item);
            if (listIndex < 0 || itemIndex < 0)
            {
                return false;
            }

            list.Items.RemoveAt(itemIndex);
            item.Blocks.Remove(paragraph);
            document.Blocks.Insert(listIndex + 1 + itemIndex, paragraph);
            if (item.Blocks.Count > 0)
            {
                var extra = new ListItem();
                while (item.Blocks.Count > 0)
                {
                    var block = item.Blocks[0];
                    item.Blocks.RemoveAt(0);
                    extra.Blocks.Add(block);
                }

                list.Items.Insert(itemIndex, extra);
            }

            if (list.Items.Count == 0)
            {
                document.Blocks.Remove(list);
            }

            return true;
        }

        if (list.Parent is Section section)
        {
            var listIndex = section.Blocks.IndexOf(list);
            var itemIndex = list.Items.IndexOf(item);
            if (listIndex < 0 || itemIndex < 0)
            {
                return false;
            }

            list.Items.RemoveAt(itemIndex);
            item.Blocks.Remove(paragraph);
            section.Blocks.Insert(listIndex + 1 + itemIndex, paragraph);
            if (list.Items.Count == 0)
            {
                section.Blocks.Remove(list);
            }

            return true;
        }

        return false;
    }

    private static InkkSlinger.List GetOrCreateNestedList(ListItem item, bool ordered)
    {
        for (var i = 0; i < item.Blocks.Count; i++)
        {
            if (item.Blocks[i] is InkkSlinger.List existing)
            {
                return existing;
            }
        }

        var created = new InkkSlinger.List
        {
            IsOrdered = ordered
        };
        item.Blocks.Add(created);
        return created;
    }
}
