using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace InkkSlinger;

public abstract class TextElement
{
    private TextElement? _parent;

    public TextElement? Parent => _parent;

    public event EventHandler? Changed;

    internal void SetParent(TextElement? parent)
    {
        _parent = parent;
    }

    protected void RaiseChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
        if (_parent != null)
        {
            _parent.RaiseChanged();
        }
    }
}

public abstract class Inline : TextElement;

public abstract class Block : TextElement;

public sealed class FlowDocument : TextElement
{
    public FlowDocument()
    {
        Blocks = new TextElementCollection<FlowDocument, Block>(this, RaiseChanged);
    }

    public TextElementCollection<FlowDocument, Block> Blocks { get; }
}

public sealed class Section : Block
{
    public Section()
    {
        Blocks = new TextElementCollection<Section, Block>(this, RaiseChanged);
    }

    public TextElementCollection<Section, Block> Blocks { get; }
}

public sealed class Paragraph : Block
{
    public Paragraph()
    {
        Inlines = new TextElementCollection<Paragraph, Inline>(this, RaiseChanged);
    }

    public TextElementCollection<Paragraph, Inline> Inlines { get; }
}

public sealed class Run : Inline
{
    private string _text = string.Empty;

    public Run()
    {
    }

    public Run(string? text)
    {
        _text = text ?? string.Empty;
    }

    public string Text
    {
        get => _text;
        set
        {
            var next = value ?? string.Empty;
            if (string.Equals(_text, next, StringComparison.Ordinal))
            {
                return;
            }

            _text = next;
            RaiseChanged();
        }
    }
}

public class Span : Inline
{
    public Span()
    {
        Inlines = new TextElementCollection<Span, Inline>(this, RaiseChanged);
    }

    public TextElementCollection<Span, Inline> Inlines { get; }
}

public sealed class Bold : Span;

public sealed class Italic : Span;

public sealed class Underline : Span;

public sealed class Hyperlink : Span
{
    public string? NavigateUri { get; set; }
}

public sealed class LineBreak : Inline;

public sealed class InlineUIContainer : Inline
{
    public UIElement? Child { get; set; }
}

public sealed class BlockUIContainer : Block
{
    public UIElement? Child { get; set; }
}

public sealed class List : Block
{
    public List()
    {
        Items = new TextElementCollection<List, ListItem>(this, RaiseChanged);
    }

    public TextElementCollection<List, ListItem> Items { get; }
}

public sealed class ListItem : TextElement
{
    public ListItem()
    {
        Blocks = new TextElementCollection<ListItem, Block>(this, RaiseChanged);
    }

    public TextElementCollection<ListItem, Block> Blocks { get; }
}

public sealed class Table : Block
{
    public Table()
    {
        RowGroups = new TextElementCollection<Table, TableRowGroup>(this, RaiseChanged);
    }

    public TextElementCollection<Table, TableRowGroup> RowGroups { get; }
}

public sealed class TableRowGroup : TextElement
{
    public TableRowGroup()
    {
        Rows = new TextElementCollection<TableRowGroup, TableRow>(this, RaiseChanged);
    }

    public TextElementCollection<TableRowGroup, TableRow> Rows { get; }
}

public sealed class TableRow : TextElement
{
    public TableRow()
    {
        Cells = new TextElementCollection<TableRow, TableCell>(this, RaiseChanged);
    }

    public TextElementCollection<TableRow, TableCell> Cells { get; }
}

public sealed class TableCell : TextElement
{
    private int _rowSpan = 1;
    private int _columnSpan = 1;

    public TableCell()
    {
        Blocks = new TextElementCollection<TableCell, Block>(this, RaiseChanged);
    }

    public TextElementCollection<TableCell, Block> Blocks { get; }

    public int RowSpan
    {
        get => _rowSpan;
        set => _rowSpan = Math.Max(1, value);
    }

    public int ColumnSpan
    {
        get => _columnSpan;
        set => _columnSpan = Math.Max(1, value);
    }
}

public sealed class TextElementCollection<TParent, TChild> : IList<TChild>, IList
    where TParent : TextElement
    where TChild : TextElement
{
    private readonly TParent _parent;
    private readonly Action _onChanged;
    private readonly System.Collections.Generic.List<TChild> _items = [];

    public TextElementCollection(TParent parent, Action onChanged)
    {
        _parent = parent;
        _onChanged = onChanged;
    }

    public int Count => _items.Count;

    public bool IsReadOnly => false;

    bool IList.IsReadOnly => false;

    bool IList.IsFixedSize => false;

    object ICollection.SyncRoot => this;

    bool ICollection.IsSynchronized => false;

    public TChild this[int index]
    {
        get => _items[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            EnsureCanAttach(value);
            Detach(_items[index]);
            _items[index] = value;
            value.SetParent(_parent);
            _onChanged();
        }
    }

    object? IList.this[int index]
    {
        get => this[index];
        set
        {
            if (value is not TChild typed)
            {
                throw new ArgumentException($"Value must be of type '{typeof(TChild).Name}'.", nameof(value));
            }

            this[index] = typed;
        }
    }

    public void Add(TChild item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureCanAttach(item);
        _items.Add(item);
        item.SetParent(_parent);
        _onChanged();
    }

    public void Clear()
    {
        for (var i = 0; i < _items.Count; i++)
        {
            Detach(_items[i]);
        }

        _items.Clear();
        _onChanged();
    }

    public bool Contains(TChild item)
    {
        return _items.Contains(item);
    }

    public void CopyTo(TChild[] array, int arrayIndex)
    {
        _items.CopyTo(array, arrayIndex);
    }

    void ICollection.CopyTo(Array array, int index)
    {
        if (array is TChild[] typed)
        {
            CopyTo(typed, index);
            return;
        }

        for (var i = 0; i < _items.Count; i++)
        {
            array.SetValue(_items[i], index + i);
        }
    }

    public IEnumerator<TChild> GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    public int IndexOf(TChild item)
    {
        return _items.IndexOf(item);
    }

    int IList.Add(object? value)
    {
        if (value is not TChild typed)
        {
            throw new ArgumentException($"Value must be of type '{typeof(TChild).Name}'.", nameof(value));
        }

        Add(typed);
        return _items.Count - 1;
    }

    bool IList.Contains(object? value)
    {
        return value is TChild typed && Contains(typed);
    }

    int IList.IndexOf(object? value)
    {
        return value is TChild typed ? IndexOf(typed) : -1;
    }

    public void Insert(int index, TChild item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureCanAttach(item);
        _items.Insert(index, item);
        item.SetParent(_parent);
        _onChanged();
    }

    void IList.Insert(int index, object? value)
    {
        if (value is not TChild typed)
        {
            throw new ArgumentException($"Value must be of type '{typeof(TChild).Name}'.", nameof(value));
        }

        Insert(index, typed);
    }

    public bool Remove(TChild item)
    {
        if (!_items.Remove(item))
        {
            return false;
        }

        Detach(item);
        _onChanged();
        return true;
    }

    void IList.Remove(object? value)
    {
        if (value is TChild typed)
        {
            Remove(typed);
        }
    }

    public void RemoveAt(int index)
    {
        var item = _items[index];
        _items.RemoveAt(index);
        Detach(item);
        _onChanged();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private static void Detach(TChild item)
    {
        item.SetParent(null);
    }

    private static void EnsureCanAttach(TChild item)
    {
        if (item.Parent != null)
        {
            throw new InvalidOperationException("TextElement already has a parent.");
        }
    }
}

public static class FlowDocumentPlainText
{
    public static string GetText(FlowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var lines = new System.Collections.Generic.List<string>();
        foreach (var paragraph in EnumerateParagraphs(document))
        {
            lines.Add(GetInlineText(paragraph.Inlines));
        }

        if (lines.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static void SetText(FlowDocument document, string? text)
    {
        ArgumentNullException.ThrowIfNull(document);
        var normalized = (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal);
        document.Blocks.Clear();
        var paragraphs = normalized.Split('\n');
        if (paragraphs.Length == 0)
        {
            return;
        }

        foreach (var paragraphText in paragraphs)
        {
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run(paragraphText));
            document.Blocks.Add(paragraph);
        }
    }

    internal static IEnumerable<Paragraph> EnumerateParagraphs(FlowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        foreach (var paragraph in EnumerateParagraphs(document.Blocks))
        {
            yield return paragraph;
        }
    }

    internal static string GetInlineText(IEnumerable<Inline> inlines)
    {
        var buffer = new System.Text.StringBuilder();
        AppendInlineText(inlines, buffer);
        return buffer.ToString();
    }

    private static void AppendInlineText(IEnumerable<Inline> inlines, System.Text.StringBuilder buffer)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    buffer.Append(run.Text);
                    break;
                case LineBreak:
                    buffer.Append(Environment.NewLine);
                    break;
                case Span span:
                    AppendInlineText(span.Inlines, buffer);
                    break;
            }
        }
    }

    private static IEnumerable<Paragraph> EnumerateParagraphs(IEnumerable<Block> blocks)
    {
        foreach (var block in blocks)
        {
            foreach (var paragraph in EnumerateParagraphs(block))
            {
                yield return paragraph;
            }
        }
    }

    private static IEnumerable<Paragraph> EnumerateParagraphs(Block block)
    {
        switch (block)
        {
            case Paragraph paragraph:
                yield return paragraph;
                yield break;
            case Section section:
                foreach (var nested in EnumerateParagraphs(section.Blocks))
                {
                    yield return nested;
                }

                yield break;
            case List list:
                foreach (var item in list.Items)
                {
                    foreach (var nested in EnumerateParagraphs(item.Blocks))
                    {
                        yield return nested;
                    }
                }

                yield break;
            case Table table:
                foreach (var rowGroup in table.RowGroups)
                {
                    foreach (var row in rowGroup.Rows)
                    {
                        foreach (var cell in row.Cells)
                        {
                            foreach (var nested in EnumerateParagraphs(cell.Blocks))
                            {
                                yield return nested;
                            }
                        }
                    }
                }

                yield break;
            default:
                yield break;
        }
    }
}
