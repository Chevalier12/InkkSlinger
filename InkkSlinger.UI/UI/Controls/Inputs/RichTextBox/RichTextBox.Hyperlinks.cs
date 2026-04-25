using System;
using System.Collections.Generic;

namespace InkkSlinger;

public partial class RichTextBox
{
    private bool TryActivateHyperlinkAtSelection()
    {
        var offset = SelectionLength > 0 ? SelectionStart : _caretIndex;
        var hyperlink = ResolveHyperlinkAtOffset(offset);
        if (hyperlink == null)
        {
            return false;
        }

        return TryActivateHyperlink(hyperlink);
    }

    private void RaiseHyperlinkNavigate(string uri)
    {
        var args = new HyperlinkNavigateRoutedEventArgs(HyperlinkNavigateEvent, uri);
        RaiseRoutedEventInternal(HyperlinkNavigateEvent, args);
    }

    private bool TryActivateHyperlink(Hyperlink hyperlink)
    {
        if (CommandSourceExecution.TryExecute(hyperlink, this))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(hyperlink.NavigateUri))
        {
            return false;
        }

        RaiseHyperlinkNavigate(hyperlink.NavigateUri!);
        return true;
    }

    private Hyperlink? ResolveHyperlinkAtOffset(int offset)
    {
        return DocumentViewportController.ResolveHyperlinkAtOffset(Document, offset);
    }

    private static Hyperlink? ResolveHyperlinkWithinInlines(IEnumerable<Inline> inlines, int localOffset)
    {
        var cursor = 0;
        foreach (var inline in inlines)
        {
            var length = GetInlineLogicalLength(inline);
            var end = cursor + length;
            if (localOffset < cursor || localOffset > end)
            {
                cursor = end;
                continue;
            }

            if (inline is Hyperlink hyperlink &&
                (!string.IsNullOrWhiteSpace(hyperlink.NavigateUri) || hyperlink.Command != null))
            {
                return hyperlink;
            }

            if (inline is Span span)
            {
                var nested = ResolveHyperlinkWithinInlines(span.Inlines, Math.Max(0, localOffset - cursor));
                if (nested != null)
                {
                    return nested;
                }
            }

            cursor = end;
        }

        return null;
    }

    private void SetHoveredHyperlink(Hyperlink? hyperlink)
    {
        if (ReferenceEquals(_hoveredHyperlink, hyperlink))
        {
            return;
        }

        if (_hoveredHyperlink != null)
        {
            _hoveredHyperlink.IsMouseOver = false;
        }

        _hoveredHyperlink = hyperlink;
        if (_hoveredHyperlink != null)
        {
            _hoveredHyperlink.IsMouseOver = true;
        }

        _layoutCache.Invalidate();
        InvalidateVisualWithReason("HyperlinkHoverStateChanged");
    }

    protected override void OnResourceScopeChanged(object? sender, ResourceDictionaryChangedEventArgs e)
    {
        base.OnResourceScopeChanged(sender, e);
        ApplyHyperlinkImplicitStyles();
    }

    private void ApplyHyperlinkImplicitStyles()
    {
        var currentHyperlinks = new HashSet<Hyperlink>();
        Style? implicitStyle = null;
        if (TryFindResource(typeof(Hyperlink), out var resource) && resource is Style hyperlinkStyle)
        {
            implicitStyle = hyperlinkStyle;
        }

        foreach (var hyperlink in DocumentViewportController.EnumerateHyperlinks(Document))
        {
            currentHyperlinks.Add(hyperlink);
            ApplyHyperlinkImplicitStyle(hyperlink, implicitStyle);
        }

        var staleHyperlinks = new List<Hyperlink>();
        foreach (var pair in _appliedImplicitHyperlinkStyles)
        {
            if (!currentHyperlinks.Contains(pair.Key))
            {
                staleHyperlinks.Add(pair.Key);
            }
        }

        for (var i = 0; i < staleHyperlinks.Count; i++)
        {
            RemoveTrackedHyperlinkImplicitStyle(staleHyperlinks[i]);
        }

        if (_hoveredHyperlink != null && !currentHyperlinks.Contains(_hoveredHyperlink))
        {
            _hoveredHyperlink = null;
        }

        _layoutCache.Invalidate();
        InvalidateVisualWithReason("HyperlinkImplicitStyleChanged");
    }

    private void ApplyHyperlinkImplicitStyle(Hyperlink hyperlink, Style? implicitStyle)
    {
        if (implicitStyle == null)
        {
            RemoveTrackedHyperlinkImplicitStyle(hyperlink);
            return;
        }

        if (_appliedImplicitHyperlinkStyles.TryGetValue(hyperlink, out var trackedStyle))
        {
            if (hyperlink.GetValueSource(TextElement.StyleProperty) == DependencyPropertyValueSource.Local &&
                !ReferenceEquals(hyperlink.Style, trackedStyle))
            {
                _appliedImplicitHyperlinkStyles.Remove(hyperlink);
                return;
            }
        }
        else if (hyperlink.GetValueSource(TextElement.StyleProperty) == DependencyPropertyValueSource.Local)
        {
            return;
        }

        if (!ReferenceEquals(hyperlink.Style, implicitStyle))
        {
            hyperlink.Style = implicitStyle;
        }

        _appliedImplicitHyperlinkStyles[hyperlink] = implicitStyle;
    }

    private void RemoveTrackedHyperlinkImplicitStyle(Hyperlink hyperlink)
    {
        if (_appliedImplicitHyperlinkStyles.TryGetValue(hyperlink, out var trackedStyle))
        {
            if (hyperlink.GetValueSource(TextElement.StyleProperty) == DependencyPropertyValueSource.Local &&
                ReferenceEquals(hyperlink.Style, trackedStyle))
            {
                hyperlink.ClearValue(TextElement.StyleProperty);
            }

            _appliedImplicitHyperlinkStyles.Remove(hyperlink);
        }
    }

    private static IEnumerable<Hyperlink> EnumerateHyperlinks(FlowDocument document)
    {
        for (var i = 0; i < document.Blocks.Count; i++)
        {
            foreach (var hyperlink in EnumerateHyperlinks(document.Blocks[i]))
            {
                yield return hyperlink;
            }
        }
    }

    private static IEnumerable<Hyperlink> EnumerateHyperlinks(Block block)
    {
        switch (block)
        {
            case Paragraph paragraph:
                for (var i = 0; i < paragraph.Inlines.Count; i++)
                {
                    foreach (var hyperlink in EnumerateHyperlinks(paragraph.Inlines[i]))
                    {
                        yield return hyperlink;
                    }
                }

                yield break;
            case Section section:
                for (var i = 0; i < section.Blocks.Count; i++)
                {
                    foreach (var hyperlink in EnumerateHyperlinks(section.Blocks[i]))
                    {
                        yield return hyperlink;
                    }
                }

                yield break;
            case InkkSlinger.List list:
                for (var i = 0; i < list.Items.Count; i++)
                {
                    for (var j = 0; j < list.Items[i].Blocks.Count; j++)
                    {
                        foreach (var hyperlink in EnumerateHyperlinks(list.Items[i].Blocks[j]))
                        {
                            yield return hyperlink;
                        }
                    }
                }

                yield break;
            case Table table:
                for (var i = 0; i < table.RowGroups.Count; i++)
                {
                    var rowGroup = table.RowGroups[i];
                    for (var j = 0; j < rowGroup.Rows.Count; j++)
                    {
                        var row = rowGroup.Rows[j];
                        for (var k = 0; k < row.Cells.Count; k++)
                        {
                            var cell = row.Cells[k];
                            for (var m = 0; m < cell.Blocks.Count; m++)
                            {
                                foreach (var hyperlink in EnumerateHyperlinks(cell.Blocks[m]))
                                {
                                    yield return hyperlink;
                                }
                            }
                        }
                    }
                }

                yield break;
        }
    }

    private static IEnumerable<Hyperlink> EnumerateHyperlinks(Inline inline)
    {
        if (inline is Hyperlink hyperlink)
        {
            yield return hyperlink;
        }

        if (inline is not Span span)
        {
            yield break;
        }

        for (var i = 0; i < span.Inlines.Count; i++)
        {
            foreach (var nested in EnumerateHyperlinks(span.Inlines[i]))
            {
                yield return nested;
            }
        }
    }
}