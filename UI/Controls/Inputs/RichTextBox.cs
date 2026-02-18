using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public class RichTextBox : Control, ITextInputControl, IRenderDirtyBoundsHintProvider
{
    public static readonly RoutedEvent DocumentChangedEvent =
        new(nameof(DocumentChanged), RoutingStrategy.Bubble);

    public static readonly DependencyProperty DocumentProperty =
        DependencyProperty.Register(
            nameof(Document),
            typeof(FlowDocument),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is RichTextBox richTextBox)
                    {
                        richTextBox.OnDocumentPropertyChanged(args.OldValue as FlowDocument, args.NewValue as FlowDocument);
                    }
                },
                coerceValueCallback: static (_, value) => value as FlowDocument ?? CreateDefaultDocument()));

    public static readonly DependencyProperty FontProperty =
        DependencyProperty.Register(
            nameof(Font),
            typeof(SpriteFont),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(new Color(28, 28, 28), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(new Color(162, 162, 162), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(1f, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(new Thickness(8f, 5f, 8f, 5f), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TextWrappingProperty =
        DependencyProperty.Register(
            nameof(TextWrapping),
            typeof(TextWrapping),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(TextWrapping.Wrap, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectionBrushProperty =
        DependencyProperty.Register(
            nameof(SelectionBrush),
            typeof(Color),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(new Color(66, 124, 211, 180), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CaretBrushProperty =
        DependencyProperty.Register(
            nameof(CaretBrush),
            typeof(Color),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(
            nameof(IsReadOnly),
            typeof(bool),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.None));

    public static readonly DependencyProperty IsMouseOverProperty =
        DependencyProperty.Register(
            nameof(IsMouseOver),
            typeof(bool),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsFocusedProperty =
        DependencyProperty.Register(
            nameof(IsFocused),
            typeof(bool),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly DocumentUndoManager _undoManager = new();
    private readonly Dictionary<char, float> _glyphWidthCache = new();
    private int _caretIndex;
    private int _selectionAnchor;
    private float _caretBlinkSeconds;
    private bool _isCaretVisible = true;
    private bool _isSelectingWithPointer;

    public RichTextBox()
    {
        SetValue(DocumentProperty, CreateDefaultDocument());
    }

    public event EventHandler<RoutedSimpleEventArgs> DocumentChanged
    {
        add => AddHandler(DocumentChangedEvent, value);
        remove => RemoveHandler(DocumentChangedEvent, value);
    }

    public FlowDocument Document
    {
        get => GetValue<FlowDocument>(DocumentProperty) ?? CreateDefaultDocument();
        set => SetValue(DocumentProperty, value);
    }

    public SpriteFont? Font
    {
        get => GetValue<SpriteFont>(FontProperty);
        set => SetValue(FontProperty, value);
    }

    public Color Foreground
    {
        get => GetValue<Color>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Color BorderBrush
    {
        get => GetValue<Color>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public float BorderThickness
    {
        get => GetValue<float>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public Thickness Padding
    {
        get => GetValue<Thickness>(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    public TextWrapping TextWrapping
    {
        get => GetValue<TextWrapping>(TextWrappingProperty);
        set => SetValue(TextWrappingProperty, value);
    }

    public Color SelectionBrush
    {
        get => GetValue<Color>(SelectionBrushProperty);
        set => SetValue(SelectionBrushProperty, value);
    }

    public Color CaretBrush
    {
        get => GetValue<Color>(CaretBrushProperty);
        set => SetValue(CaretBrushProperty, value);
    }

    public bool IsReadOnly
    {
        get => GetValue<bool>(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public bool IsMouseOver
    {
        get => GetValue<bool>(IsMouseOverProperty);
        private set => SetValue(IsMouseOverProperty, value);
    }

    public bool IsFocused
    {
        get => GetValue<bool>(IsFocusedProperty);
        private set => SetValue(IsFocusedProperty, value);
    }

    public int CaretIndex => _caretIndex;

    public int SelectionStart => Math.Min(_selectionAnchor, _caretIndex);

    public int SelectionLength => Math.Abs(_caretIndex - _selectionAnchor);

    internal bool IsRenderCacheStable => !IsFocused && SelectionLength == 0;

    public bool HandleTextInputFromInput(char character)
    {
        if (!IsEnabled || !IsFocused || IsReadOnly)
        {
            return false;
        }

        if (char.IsControl(character))
        {
            return false;
        }

        ReplaceSelection(character.ToString());
        return true;
    }

    public bool HandleKeyDownFromInput(Keys key, ModifierKeys modifiers)
    {
        if (!IsEnabled || !IsFocused)
        {
            return false;
        }

        var ctrl = (modifiers & ModifierKeys.Control) != 0;
        var shift = (modifiers & ModifierKeys.Shift) != 0;
        switch (key)
        {
            case Keys.Left:
                MoveCaret(-1, shift);
                return true;
            case Keys.Right:
                MoveCaret(1, shift);
                return true;
            case Keys.Home:
                SetCaret(0, shift);
                return true;
            case Keys.End:
                SetCaret(GetText().Length, shift);
                return true;
            case Keys.A when ctrl:
                _selectionAnchor = 0;
                _caretIndex = GetText().Length;
                InvalidateVisual();
                return true;
            case Keys.C when ctrl:
                if (SelectionLength > 0)
                {
                    var selected = GetText().Substring(SelectionStart, SelectionLength);
                    TextClipboard.SetText(selected);
                    var richSlice = FlowDocumentSerializer.Serialize(Document);
                    TextClipboard.SetData(FlowDocumentSerializer.ClipboardFormat, richSlice);
                }

                return true;
            case Keys.X when ctrl:
                if (!IsReadOnly && SelectionLength > 0)
                {
                    var selected = GetText().Substring(SelectionStart, SelectionLength);
                    TextClipboard.SetText(selected);
                    TextClipboard.SetData(FlowDocumentSerializer.ClipboardFormat, FlowDocumentSerializer.Serialize(Document));
                    ReplaceSelection(string.Empty);
                }

                return true;
            case Keys.V when ctrl:
                if (!IsReadOnly && TextClipboard.TryGetText(out var pasted))
                {
                    ReplaceSelection(NormalizeNewlines(pasted));
                }

                return true;
            case Keys.Z when ctrl:
                if (_undoManager.Undo())
                {
                    ClampSelectionToTextLength();
                    InvalidateVisual();
                }

                return true;
            case Keys.Y when ctrl:
                if (_undoManager.Redo())
                {
                    ClampSelectionToTextLength();
                    InvalidateVisual();
                }

                return true;
            case Keys.Back:
                if (IsReadOnly)
                {
                    return true;
                }

                if (SelectionLength > 0)
                {
                    ReplaceSelection(string.Empty);
                }
                else if (_caretIndex > 0)
                {
                    _selectionAnchor = _caretIndex - 1;
                    ReplaceSelection(string.Empty);
                }

                return true;
            case Keys.Delete:
                if (IsReadOnly)
                {
                    return true;
                }

                if (SelectionLength > 0)
                {
                    ReplaceSelection(string.Empty);
                }
                else if (_caretIndex < GetText().Length)
                {
                    _selectionAnchor = _caretIndex + 1;
                    ReplaceSelection(string.Empty);
                }

                return true;
            case Keys.Enter:
                if (!IsReadOnly)
                {
                    ReplaceSelection("\n");
                }

                return true;
            default:
                return false;
        }
    }

    public bool HandlePointerDownFromInput(Vector2 pointerPosition, bool extendSelection)
    {
        if (!IsEnabled)
        {
            return false;
        }

        var index = GetTextIndexFromPoint(pointerPosition);
        SetCaret(index, extendSelection);
        _isSelectingWithPointer = true;
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateVisual();
        return true;
    }

    public bool HandlePointerMoveFromInput(Vector2 pointerPosition)
    {
        if (!IsEnabled || !IsFocused || !_isSelectingWithPointer)
        {
            return false;
        }

        var index = GetTextIndexFromPoint(pointerPosition);
        _caretIndex = index;
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateVisual();
        return true;
    }

    public bool HandlePointerUpFromInput()
    {
        if (!_isSelectingWithPointer)
        {
            return false;
        }

        _isSelectingWithPointer = false;
        return true;
    }

    public bool HandleMouseWheelFromInput(int delta)
    {
        _ = delta;
        return false;
    }

    public void SetMouseOverFromInput(bool isMouseOver)
    {
        if (IsMouseOver == isMouseOver)
        {
            return;
        }

        IsMouseOver = isMouseOver;
    }

    public void SetFocusedFromInput(bool isFocused)
    {
        if (IsFocused == isFocused)
        {
            return;
        }

        IsFocused = isFocused;
        _caretBlinkSeconds = 0f;
        _isCaretVisible = isFocused;
        _isSelectingWithPointer = false;
        InvalidateVisual();
    }

    public bool TryConsumeRenderDirtyBoundsHint(out LayoutRect bounds)
    {
        bounds = default;
        return false;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var text = GetText();
        var width = TextWrapping == TextWrapping.NoWrap ? float.PositiveInfinity : Math.Max(0f, availableSize.X - Padding.Horizontal);
        var layout = TextLayout.Layout(text, Font, width, TextWrapping);
        return new Vector2(
            layout.Size.X + Padding.Horizontal + (BorderThickness * 2f),
            layout.Size.Y + Padding.Vertical + (BorderThickness * 2f));
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (!IsFocused)
        {
            return;
        }

        _caretBlinkSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_caretBlinkSeconds >= 0.5f)
        {
            _caretBlinkSeconds = 0f;
            _isCaretVisible = !_isCaretVisible;
            InvalidateVisual();
        }
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        base.OnRender(spriteBatch);
        UiDrawing.DrawFilledRect(spriteBatch, LayoutSlot, Background * Opacity);
        if (BorderThickness > 0f)
        {
            UiDrawing.DrawRectStroke(spriteBatch, LayoutSlot, BorderThickness, BorderBrush * Opacity);
        }

        var textRect = new LayoutRect(
            LayoutSlot.X + BorderThickness + Padding.Left,
            LayoutSlot.Y + BorderThickness + Padding.Top,
            Math.Max(0f, LayoutSlot.Width - (BorderThickness * 2f) - Padding.Horizontal),
            Math.Max(0f, LayoutSlot.Height - (BorderThickness * 2f) - Padding.Vertical));

        var text = GetText();
        var width = TextWrapping == TextWrapping.NoWrap ? float.PositiveInfinity : textRect.Width;
        var layout = TextLayout.Layout(text, Font, width, TextWrapping);
        var lineHeight = FontStashTextRenderer.GetLineHeight(Font);
        DrawSelection(spriteBatch, textRect, layout, lineHeight);

        for (var i = 0; i < layout.Lines.Count; i++)
        {
            var line = layout.Lines[i];
            if (line.Length == 0)
            {
                continue;
            }

            var position = new Vector2(textRect.X, textRect.Y + (i * lineHeight));
            FontStashTextRenderer.DrawString(spriteBatch, Font, line, position, Foreground * Opacity);
        }

        if (IsFocused && _isCaretVisible)
        {
            DrawCaret(spriteBatch, textRect, layout, lineHeight);
        }
    }

    private void OnDocumentPropertyChanged(FlowDocument? oldDocument, FlowDocument? newDocument)
    {
        if (oldDocument != null)
        {
            oldDocument.Changed -= OnDocumentChanged;
        }

        var active = newDocument ?? CreateDefaultDocument();
        active.Changed += OnDocumentChanged;
        ClampSelectionToTextLength();
        RaiseRoutedEventInternal(DocumentChangedEvent, new RoutedSimpleEventArgs(DocumentChangedEvent));
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnDocumentChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        ClampSelectionToTextLength();
        RaiseRoutedEventInternal(DocumentChangedEvent, new RoutedSimpleEventArgs(DocumentChangedEvent));
        InvalidateMeasure();
        InvalidateVisual();
    }

    private static FlowDocument CreateDefaultDocument()
    {
        var document = new FlowDocument();
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run(string.Empty));
        document.Blocks.Add(paragraph);
        return document;
    }

    private string GetText()
    {
        return NormalizeNewlines(FlowDocumentPlainText.GetText(Document));
    }

    private void ReplaceSelection(string replacement)
    {
        var start = SelectionStart;
        var length = SelectionLength;
        var content = GetText();
        var updated = content.Remove(start, length).Insert(start, replacement);
        DocumentEditing.ReplaceAllText(Document, updated, _undoManager);
        _caretIndex = start + replacement.Length;
        _selectionAnchor = _caretIndex;
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void SetCaret(int index, bool extendSelection)
    {
        _caretIndex = Math.Clamp(index, 0, GetText().Length);
        if (!extendSelection)
        {
            _selectionAnchor = _caretIndex;
        }
    }

    private void MoveCaret(int delta, bool extendSelection)
    {
        SetCaret(Math.Clamp(_caretIndex + delta, 0, GetText().Length), extendSelection);
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateVisual();
    }

    private void ClampSelectionToTextLength()
    {
        var length = GetText().Length;
        _caretIndex = Math.Clamp(_caretIndex, 0, length);
        _selectionAnchor = Math.Clamp(_selectionAnchor, 0, length);
    }

    private int GetTextIndexFromPoint(Vector2 point)
    {
        var textRect = new LayoutRect(
            LayoutSlot.X + BorderThickness + Padding.Left,
            LayoutSlot.Y + BorderThickness + Padding.Top,
            Math.Max(0f, LayoutSlot.Width - (BorderThickness * 2f) - Padding.Horizontal),
            Math.Max(0f, LayoutSlot.Height - (BorderThickness * 2f) - Padding.Vertical));
        var text = GetText();
        var width = TextWrapping == TextWrapping.NoWrap ? float.PositiveInfinity : textRect.Width;
        var layout = TextLayout.Layout(text, Font, width, TextWrapping);
        var lineStarts = BuildLineStartOffsets(text, layout.Lines, width);
        var lineHeight = Math.Max(1f, FontStashTextRenderer.GetLineHeight(Font));
        var lineIndex = (int)MathF.Floor((point.Y - textRect.Y) / lineHeight);
        lineIndex = Math.Clamp(lineIndex, 0, Math.Max(0, layout.Lines.Count - 1));
        var lineStart = lineIndex >= 0 && lineIndex < lineStarts.Count ? lineStarts[lineIndex] : text.Length;

        if (lineIndex >= layout.Lines.Count)
        {
            return text.Length;
        }

        var line = layout.Lines[lineIndex];
        var x = point.X - textRect.X;
        if (x <= 0f)
        {
            return lineStart;
        }

        var current = 0f;
        for (var i = 0; i < line.Length; i++)
        {
            var glyph = GetGlyphWidth(line[i]);
            var mid = current + (glyph * 0.5f);
            if (x <= mid)
            {
                return lineStart + i;
            }

            current += glyph;
        }

        return Math.Min(text.Length, lineStart + line.Length);
    }

    private float GetGlyphWidth(char glyph)
    {
        if (_glyphWidthCache.TryGetValue(glyph, out var cached))
        {
            return cached;
        }

        var measured = FontStashTextRenderer.MeasureWidth(Font, glyph.ToString()) ;
        _glyphWidthCache[glyph] = measured;
        return measured;
    }

    private void DrawCaret(SpriteBatch spriteBatch, LayoutRect textRect, TextLayout.TextLayoutResult layout, float lineHeight)
    {
        var text = GetText();
        var width = TextWrapping == TextWrapping.NoWrap ? float.PositiveInfinity : textRect.Width;
        var lineStarts = BuildLineStartOffsets(text, layout.Lines, width);
        var (lineIndex, column) = ResolveCaretLineAndColumn(layout, lineStarts, _caretIndex);
        var lineText = lineIndex >= 0 && lineIndex < layout.Lines.Count ? layout.Lines[lineIndex] : string.Empty;
        var prefix = column <= 0 ? string.Empty : lineText[..Math.Min(column, lineText.Length)];
        var x = textRect.X + FontStashTextRenderer.MeasureWidth(Font, prefix);
        var y = textRect.Y + (lineIndex * lineHeight);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x, y, 1f, lineHeight), CaretBrush * Opacity);
    }

    private void DrawSelection(SpriteBatch spriteBatch, LayoutRect textRect, TextLayout.TextLayoutResult layout, float lineHeight)
    {
        if (SelectionLength <= 0)
        {
            return;
        }

        var selectionStart = SelectionStart;
        var selectionEnd = selectionStart + SelectionLength;
        var text = GetText();
        var width = TextWrapping == TextWrapping.NoWrap ? float.PositiveInfinity : textRect.Width;
        var lineStarts = BuildLineStartOffsets(text, layout.Lines, width);
        for (var lineIndex = 0; lineIndex < layout.Lines.Count; lineIndex++)
        {
            var line = layout.Lines[lineIndex];
            var lineStart = lineIndex >= 0 && lineIndex < lineStarts.Count ? lineStarts[lineIndex] : text.Length;
            var lineEnd = lineStart + line.Length;

            if (selectionEnd <= lineStart || selectionStart >= lineEnd)
            {
                continue;
            }

            var localStart = Math.Max(0, selectionStart - lineStart);
            var localEnd = Math.Min(line.Length, selectionEnd - lineStart);
            var leftText = localStart <= 0 ? string.Empty : line[..localStart];
            var selectedText = localEnd <= localStart ? string.Empty : line[localStart..localEnd];
            var x = textRect.X + FontStashTextRenderer.MeasureWidth(Font, leftText);
            var selectionWidth = Math.Max(1f, FontStashTextRenderer.MeasureWidth(Font, selectedText));
            var y = textRect.Y + (lineIndex * lineHeight);
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x, y, selectionWidth, lineHeight), SelectionBrush * Opacity);
        }
    }

    private static (int LineIndex, int Column) ResolveCaretLineAndColumn(
        TextLayout.TextLayoutResult layout,
        IReadOnlyList<int> lineStarts,
        int caretIndex)
    {
        for (var i = 0; i < layout.Lines.Count; i++)
        {
            var lineStart = i >= 0 && i < lineStarts.Count ? lineStarts[i] : 0;
            var lineLength = layout.Lines[i].Length;
            var lineEnd = lineStart + lineLength;
            if (caretIndex <= lineEnd)
            {
                return (i, Math.Clamp(caretIndex - lineStart, 0, lineLength));
            }
        }

        var lastIndex = Math.Max(0, layout.Lines.Count - 1);
        var lastLength = layout.Lines.Count == 0 ? 0 : layout.Lines[lastIndex].Length;
        return (lastIndex, lastLength);
    }

    private static string NormalizeNewlines(string? text)
    {
        return (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private IReadOnlyList<int> BuildLineStartOffsets(string text, IReadOnlyList<string> lines, float width)
    {
        if (lines.Count == 0)
        {
            return Array.Empty<int>();
        }

        var starts = new List<int>(lines.Count);
        var normalizedParagraphs = text.Split('\n');
        var globalOffset = 0;
        foreach (var paragraph in normalizedParagraphs)
        {
            var paragraphLines = LayoutParagraphLines(paragraph, width);
            var localOffset = 0;
            foreach (var line in paragraphLines)
            {
                if (starts.Count >= lines.Count)
                {
                    break;
                }

                starts.Add(globalOffset + localOffset);
                localOffset += line.Length;
            }

            globalOffset += paragraph.Length;
            if (globalOffset < text.Length)
            {
                globalOffset += 1;
            }
        }

        while (starts.Count < lines.Count)
        {
            starts.Add(text.Length);
        }

        return starts;
    }

    private IReadOnlyList<string> LayoutParagraphLines(string paragraph, float width)
    {
        if (paragraph.Length == 0)
        {
            return [string.Empty];
        }

        var paragraphLayout = TextLayout.Layout(paragraph, Font, width, TextWrapping);
        if (paragraphLayout.Lines.Count > 0)
        {
            return paragraphLayout.Lines;
        }

        return [string.Empty];
    }
}
