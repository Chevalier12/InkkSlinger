using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Runtime.CompilerServices;
using System.Text;
using System.Collections.Immutable;

namespace InkkSlinger;

public class RichTextBox : Control, ITextInputControl, IRenderDirtyBoundsHintProvider
{
    public static readonly RoutedEvent DocumentChangedEvent =
        new(nameof(DocumentChanged), RoutingStrategy.Bubble);
    public static readonly RoutedEvent HyperlinkNavigateEvent =
        new(nameof(HyperlinkNavigate), RoutingStrategy.Bubble);

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
    private readonly DocumentLayoutEngine _layoutEngine = new();
    private readonly DocumentViewportLayoutCache _layoutCache = new();
    private const int PerfSampleCap = 256;
    private readonly List<double> _perfLayoutBuildSamplesMs = [];
    private DocumentLayoutResult? _lastMeasuredLayout;
    private DocumentLayoutResult? _lastRenderedLayout;
    private int _caretIndex;
    private int _selectionAnchor;
    private float _caretBlinkSeconds;
    private bool _isCaretVisible = true;
    private bool _isSelectingWithPointer;
    private bool _pointerSelectionMoved;
    private string? _pendingPointerHyperlinkUri;
    private bool _hasPendingRenderDirtyBoundsHint;
    private LayoutRect _pendingRenderDirtyBoundsHint;
    private float _horizontalOffset;
    private float _verticalOffset;
    private DateTime _lastPointerDownUtc;
    private int _lastPointerDownIndex = -1;
    private int _pointerClickCount;
    private int _lastSelectionHitTestOffset = -1;
    private string _pendingInvalidationReason = "Unspecified";
    private bool _typingBoldActive;
    private bool _typingItalicActive;
    private bool _typingUnderlineActive;
    private ModifierKeys _activeKeyModifiers;
    private int _perfLayoutCacheHitCount;
    private int _perfLayoutCacheMissCount;
    private int _perfLayoutBuildSampleCount;
    private double _perfLayoutBuildTotalMs;
    private double _perfLayoutBuildMaxMs;
    private int _perfRenderSampleCount;
    private double _perfRenderTotalMs;
    private double _perfRenderLastMs;
    private double _perfRenderMaxMs;
    private int _perfSelectionGeometrySampleCount;
    private double _perfSelectionGeometryTotalMs;
    private double _perfSelectionGeometryLastMs;
    private double _perfSelectionGeometryMaxMs;
    private int _perfClipboardSerializeSampleCount;
    private double _perfClipboardSerializeTotalMs;
    private double _perfClipboardSerializeLastMs;
    private double _perfClipboardSerializeMaxMs;
    private int _perfClipboardDeserializeSampleCount;
    private double _perfClipboardDeserializeTotalMs;
    private double _perfClipboardDeserializeLastMs;
    private double _perfClipboardDeserializeMaxMs;
    private int _perfEditSampleCount;
    private double _perfEditTotalMs;
    private double _perfEditLastMs;
    private double _perfEditMaxMs;
    private readonly Queue<RecentOperationEntry> _recentOperations = new();
    private int _recentOperationSequence;
    private DocumentRichnessSnapshot _lastDocumentRichness = DocumentRichnessSnapshot.Empty;
    private const float PointerAutoScrollStep = 16f;
    private const double MultiClickWindowMs = 450d;
    private const int RecentOperationCapacity = 10;
    private const string ClipboardRtfFormat = "Rich Text Format";
    private const string ClipboardXamlFormat = "Xaml";
    private const string ClipboardXamlPackageFormat = "XamlPackage";
    private static readonly ImmutableHashSet<string> RichGuardedReplaceSelectionCommands =
        ImmutableHashSet.Create(StringComparer.Ordinal, "InsertText", "InsertTextStyled", "Backspace", "DeleteForward", "DeleteSelection");
    private static readonly ImmutableHashSet<string> RichGuardedFragmentCommands =
        ImmutableHashSet.Create(StringComparer.Ordinal, "EnterParagraphBreak", "InsertTextStyled", "InsertText");

    public RichTextBox()
    {
        SetValue(DocumentProperty, CreateDefaultDocument());
        _lastDocumentRichness = CaptureDocumentRichness(Document);
        RegisterEditingCommandBindings();
        RegisterEditingInputBindings();
    }

    public event EventHandler<RoutedSimpleEventArgs> DocumentChanged
    {
        add => AddHandler(DocumentChangedEvent, value);
        remove => RemoveHandler(DocumentChangedEvent, value);
    }

    public event EventHandler<HyperlinkNavigateRoutedEventArgs> HyperlinkNavigate
    {
        add => AddHandler(HyperlinkNavigateEvent, value);
        remove => RemoveHandler(HyperlinkNavigateEvent, value);
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

    public RichTextBoxPerformanceSnapshot GetPerformanceSnapshot()
    {
        return new RichTextBoxPerformanceSnapshot(
            _perfLayoutCacheHitCount,
            _perfLayoutCacheMissCount,
            _perfLayoutBuildSampleCount,
            Average(_perfLayoutBuildTotalMs, _perfLayoutBuildSampleCount),
            Percentile(_perfLayoutBuildSamplesMs, 0.95),
            Percentile(_perfLayoutBuildSamplesMs, 0.99),
            _perfLayoutBuildMaxMs,
            _perfRenderSampleCount,
            _perfRenderLastMs,
            Average(_perfRenderTotalMs, _perfRenderSampleCount),
            _perfRenderMaxMs,
            _perfSelectionGeometrySampleCount,
            _perfSelectionGeometryLastMs,
            Average(_perfSelectionGeometryTotalMs, _perfSelectionGeometrySampleCount),
            _perfSelectionGeometryMaxMs,
            _perfClipboardSerializeSampleCount,
            _perfClipboardSerializeLastMs,
            Average(_perfClipboardSerializeTotalMs, _perfClipboardSerializeSampleCount),
            _perfClipboardSerializeMaxMs,
            _perfClipboardDeserializeSampleCount,
            _perfClipboardDeserializeLastMs,
            Average(_perfClipboardDeserializeTotalMs, _perfClipboardDeserializeSampleCount),
            _perfClipboardDeserializeMaxMs,
            _perfEditSampleCount,
            _perfEditLastMs,
            Average(_perfEditTotalMs, _perfEditSampleCount),
            _perfEditMaxMs,
            _undoManager.UndoDepth,
            _undoManager.RedoDepth,
            _undoManager.UndoOperationCount,
            _undoManager.RedoOperationCount);
    }

    public void ResetPerformanceSnapshot()
    {
        _perfLayoutCacheHitCount = 0;
        _perfLayoutCacheMissCount = 0;
        _perfLayoutBuildSampleCount = 0;
        _perfLayoutBuildTotalMs = 0d;
        _perfLayoutBuildMaxMs = 0d;
        _perfLayoutBuildSamplesMs.Clear();
        _perfRenderSampleCount = 0;
        _perfRenderTotalMs = 0d;
        _perfRenderLastMs = 0d;
        _perfRenderMaxMs = 0d;
        _perfSelectionGeometrySampleCount = 0;
        _perfSelectionGeometryTotalMs = 0d;
        _perfSelectionGeometryLastMs = 0d;
        _perfSelectionGeometryMaxMs = 0d;
        _perfClipboardSerializeSampleCount = 0;
        _perfClipboardSerializeTotalMs = 0d;
        _perfClipboardSerializeLastMs = 0d;
        _perfClipboardSerializeMaxMs = 0d;
        _perfClipboardDeserializeSampleCount = 0;
        _perfClipboardDeserializeTotalMs = 0d;
        _perfClipboardDeserializeLastMs = 0d;
        _perfClipboardDeserializeMaxMs = 0d;
        _perfEditSampleCount = 0;
        _perfEditTotalMs = 0d;
        _perfEditLastMs = 0d;
        _perfEditMaxMs = 0d;
    }

    public bool HandleTextInputFromInput(char character)
    {
        return HandleTextCompositionFromInput(character.ToString());
    }

    public bool HandleTextCompositionFromInput(string? text)
    {
        if (!IsEnabled || !IsFocused || IsReadOnly || string.IsNullOrEmpty(text))
        {
            return false;
        }

        var normalized = FilterCompositionText(text);
        if (normalized.Length == 0)
        {
            return false;
        }
        RecordOperation("TextComposition", $"text=\"{SanitizeForLog(normalized)}\"");

        var undoDepthBefore = _undoManager.UndoDepth;
        var redoDepthBefore = _undoManager.RedoDepth;
        var textLengthBefore = GetText().Length;
        if (_typingBoldActive || _typingItalicActive || _typingUnderlineActive)
        {
            if (!TryInsertTypingFormattedTextWithinParagraph(normalized, "InsertTextStyled", GroupingPolicy.TypingBurst))
            {
                InsertTypingFormattedText(normalized, "InsertTextStyled");
            }
        }
        else if (TryReplaceSelectionWithinParagraphPreservingInlineStyles(normalized, "InsertText", GroupingPolicy.TypingBurst))
        {
            // Handled by style-preserving paragraph edit path for rich documents.
        }
        else if (TryReplaceSelectionWithinPlainParagraphPreservingStructure(normalized, "InsertText", GroupingPolicy.TypingBurst))
        {
            // Handled by structure-preserving plain paragraph edit path.
        }
        else if (DocumentContainsRichInlineFormatting(Document))
        {
            InsertTypingFormattedText(normalized, "InsertText");
        }
        else
        {
            ReplaceSelection(normalized, "InsertText", GroupingPolicy.TypingBurst);
        }

        RichTextBoxDiagnostics.ObserveCommandTrace(
            $"TextComposition:{normalized.Length}",
            "InsertText",
            canExecute: true,
            handled: true,
            GetText().Length - textLengthBefore,
            undoDepthBefore,
            _undoManager.UndoDepth,
            redoDepthBefore,
            _undoManager.RedoDepth);
        return true;
    }

    private bool TryReplaceSelectionWithinPlainParagraphPreservingStructure(string replacement, string commandType, GroupingPolicy policy)
    {
        if (replacement.IndexOf('\n') >= 0)
        {
            return false;
        }

        var start = SelectionStart;
        var length = SelectionLength;
        var end = start + length;
        var entries = CollectParagraphEntries(Document);
        if (entries.Count == 0)
        {
            return false;
        }

        var paragraphIndex = -1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (start >= entries[i].StartOffset && end <= entries[i].EndOffset)
            {
                paragraphIndex = i;
                break;
            }
        }

        if (paragraphIndex < 0)
        {
            return false;
        }

        if (entries[paragraphIndex].Paragraph.Parent is FlowDocument &&
            !DocumentContainsRichInlineFormatting(Document))
        {
            return false;
        }

        var localStart = start - entries[paragraphIndex].StartOffset;
        var paragraphText = FlowDocumentPlainText.GetInlineText(entries[paragraphIndex].Paragraph.Inlines);
        if (localStart < 0 || localStart > paragraphText.Length)
        {
            return false;
        }

        var localLength = Math.Clamp(length, 0, paragraphText.Length - localStart);
        var nextParagraphText = paragraphText.Remove(localStart, localLength).Insert(localStart, replacement);

        var editStart = Stopwatch.GetTimestamp();
        var undoDepthBefore = _undoManager.UndoDepth;
        var redoDepthBefore = _undoManager.RedoDepth;
        var textLengthBefore = GetText().Length;
        var beforeDocument = DocumentEditing.CloneDocument(Document);
        var beforeText = GetText();
        var afterDocument = DocumentEditing.CloneDocument(Document);
        var paragraphs = new List<Paragraph>(FlowDocumentPlainText.EnumerateParagraphs(afterDocument));
        if (paragraphIndex >= paragraphs.Count)
        {
            return false;
        }

        var paragraph = paragraphs[paragraphIndex];
        ReplaceParagraphTextPreservingSimpleWrappers(paragraph, nextParagraphText);
        var afterText = DocumentEditing.GetText(afterDocument);
        var caretAfter = start + replacement.Length;
        var session = new DocumentEditSession(Document, _undoManager);
        session.BeginTransaction(
            commandType,
            policy,
            new DocumentEditContext(
                _caretIndex,
                caretAfter,
                start,
                length,
                caretAfter,
                0,
                commandType));
        session.ApplyOperation(new ReplaceDocumentOperation("Document", beforeText, afterText, beforeDocument, afterDocument));
        session.CommitTransaction();
        _caretIndex = caretAfter;
        _selectionAnchor = _caretIndex;
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        RecordEditSample(elapsedMs);
        RichTextBoxDiagnostics.ObserveEdit(commandType, elapsedMs, start, length, _caretIndex);
        RichTextBoxDiagnostics.ObserveCommandTrace(
            "EditMethod",
            commandType,
            canExecute: true,
            handled: true,
            GetText().Length - textLengthBefore,
            undoDepthBefore,
            _undoManager.UndoDepth,
            redoDepthBefore,
            _undoManager.RedoDepth);
        TraceInvariants(commandType);
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateMeasure();
        InvalidateVisualWithReason($"Edit:{commandType}");
        return true;
    }

    private bool TryReplaceSelectionWithinParagraphPreservingInlineStyles(string replacement, string commandType, GroupingPolicy policy)
    {
        if (replacement.IndexOf('\n') >= 0 || !DocumentContainsRichInlineFormatting(Document))
        {
            return false;
        }

        var start = SelectionStart;
        var length = SelectionLength;
        var end = start + length;
        var entries = CollectParagraphEntries(Document);
        if (entries.Count == 0)
        {
            return false;
        }

        var paragraphIndex = -1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (start >= entries[i].StartOffset && end <= entries[i].EndOffset)
            {
                paragraphIndex = i;
                break;
            }
        }

        if (paragraphIndex < 0)
        {
            return false;
        }

        var paragraphLength = Math.Max(0, entries[paragraphIndex].EndOffset - entries[paragraphIndex].StartOffset);
        var localStart = Math.Clamp(start - entries[paragraphIndex].StartOffset, 0, paragraphLength);
        var localLength = Math.Clamp(length, 0, paragraphLength - localStart);
        var localEnd = localStart + localLength;

        var editStart = Stopwatch.GetTimestamp();
        var undoDepthBefore = _undoManager.UndoDepth;
        var redoDepthBefore = _undoManager.RedoDepth;
        var textLengthBefore = GetText().Length;
        var beforeDocument = DocumentEditing.CloneDocument(Document);
        var beforeText = GetText();
        var afterDocument = DocumentEditing.CloneDocument(Document);
        var paragraphs = new List<Paragraph>(FlowDocumentPlainText.EnumerateParagraphs(afterDocument));
        if (paragraphIndex >= paragraphs.Count)
        {
            return false;
        }

        var destination = paragraphs[paragraphIndex];
        destination.Inlines.Clear();
        AppendStyledInlineRangeFromParagraph(entries[paragraphIndex].Paragraph, 0, localStart, destination);
        if (!string.IsNullOrEmpty(replacement))
        {
            destination.Inlines.Add(new Run(replacement));
        }

        AppendStyledInlineRangeFromParagraph(entries[paragraphIndex].Paragraph, localEnd, paragraphLength, destination);
        if (destination.Inlines.Count == 0)
        {
            destination.Inlines.Add(new Run(string.Empty));
        }

        var afterText = DocumentEditing.GetText(afterDocument);
        var caretAfter = start + replacement.Length;
        var session = new DocumentEditSession(Document, _undoManager);
        session.BeginTransaction(
            commandType,
            policy,
            new DocumentEditContext(
                _caretIndex,
                caretAfter,
                start,
                length,
                caretAfter,
                0,
                commandType));
        session.ApplyOperation(new ReplaceDocumentOperation("Document", beforeText, afterText, beforeDocument, afterDocument));
        session.CommitTransaction();
        _caretIndex = caretAfter;
        _selectionAnchor = _caretIndex;
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        RecordEditSample(elapsedMs);
        RichTextBoxDiagnostics.ObserveEdit(commandType, elapsedMs, start, length, _caretIndex);
        RichTextBoxDiagnostics.ObserveCommandTrace(
            "EditMethod",
            commandType,
            canExecute: true,
            handled: true,
            GetText().Length - textLengthBefore,
            undoDepthBefore,
            _undoManager.UndoDepth,
            redoDepthBefore,
            _undoManager.RedoDepth);
        TraceInvariants(commandType);
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateMeasure();
        InvalidateVisualWithReason($"Edit:{commandType}");
        return true;
    }

    private static void ReplaceParagraphTextPreservingSimpleWrappers(Paragraph paragraph, string text)
    {
        if (TryCloneSimpleSpanWrapperChain(paragraph, out var wrapper))
        {
            SetInnermostSpanText(wrapper!, text);
            paragraph.Inlines.Clear();
            paragraph.Inlines.Add(wrapper!);
            return;
        }

        paragraph.Inlines.Clear();
        paragraph.Inlines.Add(new Run(text));
    }

    private static bool TryCloneSimpleSpanWrapperChain(Paragraph paragraph, out Span? wrapper)
    {
        wrapper = null;
        if (paragraph.Inlines.Count != 1 || paragraph.Inlines[0] is not Span root)
        {
            return false;
        }

        if (!TryCloneSpanChain(root, out var cloned))
        {
            return false;
        }

        wrapper = cloned;
        return true;
    }

    private static bool TryCloneSpanChain(Span source, out Span? cloned)
    {
        cloned = source switch
        {
            Bold => new Bold(),
            Italic => new Italic(),
            Underline => new Underline(),
            Hyperlink hyperlink => new Hyperlink { NavigateUri = hyperlink.NavigateUri },
            Span => new Span(),
            _ => null
        };

        if (cloned is null)
        {
            return false;
        }

        if (source.Inlines.Count == 0)
        {
            return true;
        }

        if (source.Inlines.Count == 1 && source.Inlines[0] is Span nested)
        {
            if (!TryCloneSpanChain(nested, out var nestedClone))
            {
                return false;
            }

            cloned.Inlines.Add(nestedClone!);
            return true;
        }

        if (source.Inlines.Count == 1 && source.Inlines[0] is Run)
        {
            return true;
        }

        return false;
    }

    private static void SetInnermostSpanText(Span root, string text)
    {
        var current = root;
        while (current.Inlines.Count == 1 && current.Inlines[0] is Span nested)
        {
            current = nested;
        }

        current.Inlines.Clear();
        current.Inlines.Add(new Run(text));
    }

    private bool TryInsertParagraphBreakWithinStructuredParagraph(string commandType, GroupingPolicy policy)
    {
        RecordOperation("StructuredEnter", "Begin");
        var start = SelectionStart;
        var length = SelectionLength;
        var end = start + length;
        var entries = CollectParagraphEntries(Document);
        if (entries.Count == 0)
        {
            RecordOperation("StructuredEnter", "NoParagraphEntries");
            return false;
        }

        var paragraphIndex = -1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (start >= entries[i].StartOffset && end <= entries[i].EndOffset)
            {
                paragraphIndex = i;
                break;
            }
        }

        if (paragraphIndex < 0)
        {
            RecordOperation("StructuredEnter", "SelectionNotInSingleParagraph");
            return false;
        }

        if (entries[paragraphIndex].Paragraph.Parent is FlowDocument &&
            !DocumentContainsRichInlineFormatting(Document))
        {
            return false;
        }

        var paragraphText = FlowDocumentPlainText.GetInlineText(entries[paragraphIndex].Paragraph.Inlines);
        var localStart = start - entries[paragraphIndex].StartOffset;
        if (localStart < 0 || localStart > paragraphText.Length)
        {
            RecordOperation("StructuredEnter", "LocalStartOutOfRange");
            return false;
        }

        var localLength = Math.Clamp(length, 0, paragraphText.Length - localStart);
        var nextParagraphText = paragraphText.Remove(localStart, localLength).Insert(localStart, "\n");
        var split = nextParagraphText.Split('\n');
        if (split.Length != 2)
        {
            RecordOperation("StructuredEnter", "SplitFailed");
            return false;
        }

        var editStart = Stopwatch.GetTimestamp();
        var undoDepthBefore = _undoManager.UndoDepth;
        var redoDepthBefore = _undoManager.RedoDepth;
        var textLengthBefore = GetText().Length;
        var beforeDocument = DocumentEditing.CloneDocument(Document);
        var beforeText = GetText();
        var afterDocument = DocumentEditing.CloneDocument(Document);
        var paragraphs = new List<Paragraph>(FlowDocumentPlainText.EnumerateParagraphs(afterDocument));
        if (paragraphIndex >= paragraphs.Count)
        {
            RecordOperation("StructuredEnter", "ParagraphIndexOutOfRangeAfterClone");
            return false;
        }

        var paragraph = paragraphs[paragraphIndex];
        if (paragraph.Parent is ListItem listItem && listItem.Parent is InkkSlinger.List list)
        {
            if (TryApplyListEnterBehavior(paragraph, listItem, list, split, out afterDocument, out var caretAfter))
            {
                RecordOperation("StructuredEnter", "ListBehaviorApplied");
                return CommitStructuredDocumentReplacement(
                    commandType,
                    policy,
                    start,
                    length,
                    caretAfter,
                    beforeDocument,
                    beforeText,
                    afterDocument,
                    textLengthBefore,
                    undoDepthBefore,
                    redoDepthBefore,
                    editStart);
            }

            RecordOperation("StructuredEnter", "ListBehaviorRejected");
            return false;
        }

        var leftParagraph = CreateParagraphFromStyledRange(paragraph, 0, localStart);
        var rightParagraph = CreateParagraphFromStyledRange(paragraph, localStart + localLength, paragraphText.Length);
        ReplaceParagraphInlines(paragraph, leftParagraph);
        if (!InsertParagraphAfter(paragraph, rightParagraph))
        {
            RecordOperation("StructuredEnter", "InsertParagraphAfterFailed");
            return false;
        }
        RecordOperation("StructuredEnter", "ParagraphSplitApplied");
        return CommitStructuredDocumentReplacement(
            commandType,
            policy,
            start,
            length,
            start + 1,
            beforeDocument,
            beforeText,
            afterDocument,
            textLengthBefore,
            undoDepthBefore,
            redoDepthBefore,
            editStart);
    }

    private bool CommitStructuredDocumentReplacement(
        string commandType,
        GroupingPolicy policy,
        int start,
        int length,
        int caretAfter,
        FlowDocument beforeDocument,
        string beforeText,
        FlowDocument afterDocument,
        int textLengthBefore,
        int undoDepthBefore,
        int redoDepthBefore,
        long editStart)
    {
        var afterText = DocumentEditing.GetText(afterDocument);
        var session = new DocumentEditSession(Document, _undoManager);
        session.BeginTransaction(
            commandType,
            policy,
            new DocumentEditContext(
                _caretIndex,
                caretAfter,
                start,
                length,
                caretAfter,
                0,
                commandType));
        session.ApplyOperation(new ReplaceDocumentOperation("Document", beforeText, afterText, beforeDocument, afterDocument));
        session.CommitTransaction();
        _caretIndex = caretAfter;
        _selectionAnchor = _caretIndex;
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        RecordEditSample(elapsedMs);
        RichTextBoxDiagnostics.ObserveEdit(commandType, elapsedMs, start, length, _caretIndex);
        RichTextBoxDiagnostics.ObserveCommandTrace(
            "EditMethod",
            commandType,
            canExecute: true,
            handled: true,
            GetText().Length - textLengthBefore,
            undoDepthBefore,
            _undoManager.UndoDepth,
            redoDepthBefore,
            _undoManager.RedoDepth);
        TraceInvariants(commandType);
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateMeasure();
        InvalidateVisualWithReason($"Edit:{commandType}");
        return true;
    }

    private static Paragraph CreateParagraphFromStyledRange(Paragraph source, int start, int end)
    {
        var paragraph = new Paragraph();
        var clampedStart = Math.Max(0, start);
        var clampedEnd = Math.Max(clampedStart, end);
        AppendStyledInlineRangeFromParagraph(source, clampedStart, clampedEnd, paragraph);
        if (paragraph.Inlines.Count == 0)
        {
            paragraph.Inlines.Add(new Run(string.Empty));
        }

        return paragraph;
    }

    private static void ReplaceParagraphInlines(Paragraph target, Paragraph source)
    {
        target.Inlines.Clear();
        while (source.Inlines.Count > 0)
        {
            var inline = source.Inlines[0];
            source.Inlines.RemoveAt(0);
            target.Inlines.Add(inline);
        }

        if (target.Inlines.Count == 0)
        {
            target.Inlines.Add(new Run(string.Empty));
        }
    }

    private static bool TryApplyListEnterBehavior(
        Paragraph paragraph,
        ListItem listItem,
        InkkSlinger.List list,
        string[] split,
        out FlowDocument afterDocument,
        out int caretAfter)
    {
        afterDocument = null!;
        caretAfter = 0;
        if (split.Length != 2)
        {
            return false;
        }

        var isCurrentEmpty = string.IsNullOrEmpty(split[0]) && string.IsNullOrEmpty(split[1]);
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

        ReplaceParagraphTextPreservingSimpleWrappers(paragraph, split[0]);
        var newItem = new ListItem();
        var insertedParagraph = CreateParagraph(split[1]);
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

    private static FlowDocument? GetDocumentFromElement(TextElement element)
    {
        TextElement? current = element;
        while (current is not null)
        {
            if (current is FlowDocument document)
            {
                return document;
            }

            current = current.Parent;
        }

        return null;
    }

    private static bool RemoveListItem(ListItem item)
    {
        return item.Parent is InkkSlinger.List list && list.Items.Remove(item);
    }

    private static bool InsertParagraphAfterBlock(Block block, Paragraph paragraph)
    {
        if (block.Parent is FlowDocument doc)
        {
            var index = doc.Blocks.IndexOf(block);
            if (index < 0)
            {
                return false;
            }

            doc.Blocks.Insert(index + 1, paragraph);
            return true;
        }

        if (block.Parent is Section section)
        {
            var index = section.Blocks.IndexOf(block);
            if (index < 0)
            {
                return false;
            }

            section.Blocks.Insert(index + 1, paragraph);
            return true;
        }

        if (block.Parent is ListItem listItem)
        {
            var index = listItem.Blocks.IndexOf(block);
            if (index < 0)
            {
                return false;
            }

            listItem.Blocks.Insert(index + 1, paragraph);
            return true;
        }

        if (block.Parent is TableCell cell)
        {
            var index = cell.Blocks.IndexOf(block);
            if (index < 0)
            {
                return false;
            }

            cell.Blocks.Insert(index + 1, paragraph);
            return true;
        }

        return false;
    }

    private static bool RemoveBlockFromParent(Block block)
    {
        if (block.Parent is FlowDocument doc)
        {
            return doc.Blocks.Remove(block);
        }

        if (block.Parent is Section section)
        {
            return section.Blocks.Remove(block);
        }

        if (block.Parent is ListItem listItem)
        {
            return listItem.Blocks.Remove(block);
        }

        if (block.Parent is TableCell cell)
        {
            return cell.Blocks.Remove(block);
        }

        return false;
    }

    private static bool HasFollowingSiblingBlockOfType<TBlock>(Block block)
        where TBlock : Block
    {
        if (block.Parent is FlowDocument doc)
        {
            var index = doc.Blocks.IndexOf(block);
            return index >= 0 && index + 1 < doc.Blocks.Count && doc.Blocks[index + 1] is TBlock;
        }

        if (block.Parent is Section section)
        {
            var index = section.Blocks.IndexOf(block);
            return index >= 0 && index + 1 < section.Blocks.Count && section.Blocks[index + 1] is TBlock;
        }

        return false;
    }

    private static Paragraph? TryFindParagraphAfterBlock(Block block)
    {
        if (block.Parent is FlowDocument doc)
        {
            var index = doc.Blocks.IndexOf(block);
            if (index >= 0 && index + 1 < doc.Blocks.Count && doc.Blocks[index + 1] is Paragraph p)
            {
                return p;
            }
        }
        else if (block.Parent is Section section)
        {
            var index = section.Blocks.IndexOf(block);
            if (index >= 0 && index + 1 < section.Blocks.Count && section.Blocks[index + 1] is Paragraph p)
            {
                return p;
            }
        }
        else if (block.Parent is ListItem listItem)
        {
            var index = listItem.Blocks.IndexOf(block);
            if (index >= 0 && index + 1 < listItem.Blocks.Count && listItem.Blocks[index + 1] is Paragraph p)
            {
                return p;
            }
        }
        else if (block.Parent is TableCell cell)
        {
            var index = cell.Blocks.IndexOf(block);
            if (index >= 0 && index + 1 < cell.Blocks.Count && cell.Blocks[index + 1] is Paragraph p)
            {
                return p;
            }
        }

        return null;
    }

    private static int FindParagraphStartOffset(FlowDocument document, Paragraph paragraph)
    {
        var entries = CollectParagraphEntries(document);
        for (var i = 0; i < entries.Count; i++)
        {
            if (ReferenceEquals(entries[i].Paragraph, paragraph))
            {
                return entries[i].StartOffset;
            }
        }

        return -1;
    }

    private static bool InsertParagraphAfter(Paragraph paragraph, Paragraph newParagraph)
    {
        if (paragraph.Parent is FlowDocument flowDocument)
        {
            var index = flowDocument.Blocks.IndexOf(paragraph);
            if (index < 0)
            {
                return false;
            }

            flowDocument.Blocks.Insert(index + 1, newParagraph);
            return true;
        }

        if (paragraph.Parent is ListItem listItem)
        {
            var index = listItem.Blocks.IndexOf(paragraph);
            if (index < 0)
            {
                return false;
            }

            listItem.Blocks.Insert(index + 1, newParagraph);
            return true;
        }

        if (paragraph.Parent is TableCell tableCell)
        {
            var index = tableCell.Blocks.IndexOf(paragraph);
            if (index < 0)
            {
                return false;
            }

            tableCell.Blocks.Insert(index + 1, newParagraph);
            return true;
        }

        if (paragraph.Parent is Section section)
        {
            var index = section.Blocks.IndexOf(paragraph);
            if (index < 0)
            {
                return false;
            }

            section.Blocks.Insert(index + 1, newParagraph);
            return true;
        }

        return false;
    }

    private bool TryBackspaceAtStructuredParagraphStart(string commandType, GroupingPolicy policy)
    {
        RecordOperation("StructuredBackspace", "Begin");
        if (SelectionLength > 0)
        {
            RecordOperation("StructuredBackspace", "SelectionNotCollapsed");
            return false;
        }

        var caret = _caretIndex;
        var entries = CollectParagraphEntries(Document);
        if (entries.Count == 0)
        {
            RecordOperation("StructuredBackspace", "NoParagraphEntries");
            return false;
        }

        var paragraphIndex = -1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].StartOffset == caret)
            {
                paragraphIndex = i;
                break;
            }
        }

        if (paragraphIndex < 0)
        {
            RecordOperation("StructuredBackspace", "CaretNotAtParagraphStart");
            return false;
        }

        var editStart = Stopwatch.GetTimestamp();
        var undoDepthBefore = _undoManager.UndoDepth;
        var redoDepthBefore = _undoManager.RedoDepth;
        var textLengthBefore = GetText().Length;
        var beforeDocument = DocumentEditing.CloneDocument(Document);
        var beforeText = GetText();
        var afterDocument = DocumentEditing.CloneDocument(Document);
        var paragraphs = new List<Paragraph>(FlowDocumentPlainText.EnumerateParagraphs(afterDocument));
        if (paragraphIndex >= paragraphs.Count)
        {
            RecordOperation("StructuredBackspace", "ParagraphIndexOutOfRangeAfterClone");
            return false;
        }

        var current = paragraphs[paragraphIndex];
        if (current.Parent is FlowDocument)
        {
            if (!TryBackspaceAtTopLevelParagraphStart(current, out var caretAfterTopLevel))
            {
                RecordOperation("StructuredBackspace", "TopLevelMergeRejected");
                return false;
            }
            RecordOperation("StructuredBackspace", "TopLevelMergeApplied");

            var afterTextTopLevel = DocumentEditing.GetText(afterDocument);
            var topLevelSession = new DocumentEditSession(Document, _undoManager);
            topLevelSession.BeginTransaction(
                commandType,
                policy,
                new DocumentEditContext(
                    _caretIndex,
                    caretAfterTopLevel,
                    Math.Max(0, caret - 1),
                    1,
                    caretAfterTopLevel,
                    0,
                    commandType));
            topLevelSession.ApplyOperation(new ReplaceDocumentOperation("Document", beforeText, afterTextTopLevel, beforeDocument, afterDocument));
            topLevelSession.CommitTransaction();
            _caretIndex = caretAfterTopLevel;
            _selectionAnchor = _caretIndex;
            var elapsedTopLevelMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
            RecordEditSample(elapsedTopLevelMs);
            RichTextBoxDiagnostics.ObserveEdit(commandType, elapsedTopLevelMs, Math.Max(0, caret - 1), 1, _caretIndex);
            RichTextBoxDiagnostics.ObserveCommandTrace(
                "EditMethod",
                commandType,
                canExecute: true,
                handled: true,
                GetText().Length - textLengthBefore,
                undoDepthBefore,
                _undoManager.UndoDepth,
                redoDepthBefore,
                _undoManager.RedoDepth);
            TraceInvariants(commandType);
            EnsureCaretVisible();
            _caretBlinkSeconds = 0f;
            _isCaretVisible = true;
            InvalidateMeasure();
            InvalidateVisualWithReason($"Edit:{commandType}");
            return true;
        }

        if (!TryGetPreviousSiblingParagraph(current, out var previous))
        {
            if (!TryMergeListItemBoundaryAtParagraphStart(current, out previous))
            {
                RecordOperation("StructuredBackspace", "NoPreviousSiblingOrListBoundaryMerge");
                return false;
            }
            RecordOperation("StructuredBackspace", "ListBoundaryMergeApplied");

            var afterTextBoundary = DocumentEditing.GetText(afterDocument);
            var caretAfterBoundary = Math.Max(0, caret - 1);
            var boundarySession = new DocumentEditSession(Document, _undoManager);
            boundarySession.BeginTransaction(
                commandType,
                policy,
                new DocumentEditContext(
                    _caretIndex,
                    caretAfterBoundary,
                    caret - 1,
                    1,
                    caretAfterBoundary,
                    0,
                    commandType));
            boundarySession.ApplyOperation(new ReplaceDocumentOperation("Document", beforeText, afterTextBoundary, beforeDocument, afterDocument));
            boundarySession.CommitTransaction();
            _caretIndex = caretAfterBoundary;
            _selectionAnchor = _caretIndex;
            var elapsedBoundaryMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
            RecordEditSample(elapsedBoundaryMs);
            RichTextBoxDiagnostics.ObserveEdit(commandType, elapsedBoundaryMs, caret - 1, 1, _caretIndex);
            RichTextBoxDiagnostics.ObserveCommandTrace(
                "EditMethod",
                commandType,
                canExecute: true,
                handled: true,
                GetText().Length - textLengthBefore,
                undoDepthBefore,
                _undoManager.UndoDepth,
                redoDepthBefore,
                _undoManager.RedoDepth);
            TraceInvariants(commandType);
            EnsureCaretVisible();
            _caretBlinkSeconds = 0f;
            _isCaretVisible = true;
            InvalidateMeasure();
            InvalidateVisualWithReason($"Edit:{commandType}");
            return true;
        }

        AppendParagraphContentPreservingStyles(previous, current);
        if (!RemoveParagraphFromParent(current))
        {
            RecordOperation("StructuredBackspace", "RemoveParagraphFromParentFailed");
            return false;
        }
        RecordOperation("StructuredBackspace", "SiblingParagraphMergeApplied");

        var afterText = DocumentEditing.GetText(afterDocument);
        var caretAfter = Math.Max(0, caret - 1);
        var session = new DocumentEditSession(Document, _undoManager);
        session.BeginTransaction(
            commandType,
            policy,
            new DocumentEditContext(
                _caretIndex,
                caretAfter,
                caret - 1,
                1,
                caretAfter,
                0,
                commandType));
        session.ApplyOperation(new ReplaceDocumentOperation("Document", beforeText, afterText, beforeDocument, afterDocument));
        session.CommitTransaction();
        _caretIndex = caretAfter;
        _selectionAnchor = _caretIndex;
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        RecordEditSample(elapsedMs);
        RichTextBoxDiagnostics.ObserveEdit(commandType, elapsedMs, caret - 1, 1, _caretIndex);
        RichTextBoxDiagnostics.ObserveCommandTrace(
            "EditMethod",
            commandType,
            canExecute: true,
            handled: true,
            GetText().Length - textLengthBefore,
            undoDepthBefore,
            _undoManager.UndoDepth,
            redoDepthBefore,
            _undoManager.RedoDepth);
        TraceInvariants(commandType);
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateMeasure();
        InvalidateVisualWithReason($"Edit:{commandType}");
        return true;
    }

    private static bool TryBackspaceAtTopLevelParagraphStart(Paragraph current, out int caretAfter)
    {
        caretAfter = 0;
        if (current.Parent is not FlowDocument document)
        {
            return false;
        }

        var currentIndex = document.Blocks.IndexOf(current);
        if (currentIndex <= 0)
        {
            return false;
        }

        var previousBlock = document.Blocks[currentIndex - 1];
        if (previousBlock is Paragraph previousTopLevelParagraph)
        {
            var previousTextLength = GetParagraphLogicalLength(previousTopLevelParagraph);
            AppendParagraphContentPreservingStyles(previousTopLevelParagraph, current);
            document.Blocks.RemoveAt(currentIndex);
            caretAfter = FindParagraphStartOffset(document, previousTopLevelParagraph) + previousTextLength;
            return caretAfter >= 0;
        }

        if (previousBlock is not InkkSlinger.List list || list.Items.Count == 0)
        {
            return false;
        }

        if (!TryGetLastParagraph(list.Items[list.Items.Count - 1], out var previousParagraph))
        {
            return false;
        }

        var previousListTextLength = GetParagraphLogicalLength(previousParagraph);
        AppendParagraphContentPreservingStyles(previousParagraph, current);
        document.Blocks.RemoveAt(currentIndex);

        var owner = GetDocumentFromElement(list);
        if (owner is null)
        {
            return false;
        }

        caretAfter = FindParagraphStartOffset(owner, previousParagraph) + previousListTextLength;
        return caretAfter >= 0;
    }

    private static bool TryMergeListItemBoundaryAtParagraphStart(Paragraph current, out Paragraph previous)
    {
        previous = null!;
        if (current.Parent is not ListItem currentItem || currentItem.Parent is not InkkSlinger.List list)
        {
            return false;
        }

        if (currentItem.Blocks.Count == 0 || !ReferenceEquals(currentItem.Blocks[0], current))
        {
            return false;
        }

        var itemIndex = list.Items.IndexOf(currentItem);
        if (itemIndex <= 0)
        {
            return false;
        }

        var previousItem = list.Items[itemIndex - 1];
        if (!TryGetLastParagraph(previousItem, out previous))
        {
            return false;
        }

        AppendParagraphContentPreservingStyles(previous, current);
        list.Items.Remove(currentItem);
        return true;
    }

    private static void AppendParagraphContentPreservingStyles(Paragraph destination, Paragraph source)
    {
        if (destination.Inlines.Count == 1 &&
            destination.Inlines[0] is Run existingRun &&
            string.IsNullOrEmpty(existingRun.Text))
        {
            destination.Inlines.Clear();
        }

        AppendStyledInlineRangeFromParagraph(source, 0, GetParagraphLogicalLength(source), destination);
        if (destination.Inlines.Count == 0)
        {
            destination.Inlines.Add(new Run(string.Empty));
        }
    }

    private static bool TryGetLastParagraph(ListItem item, out Paragraph paragraph)
    {
        for (var i = item.Blocks.Count - 1; i >= 0; i--)
        {
            if (item.Blocks[i] is Paragraph p)
            {
                paragraph = p;
                return true;
            }
        }

        paragraph = null!;
        return false;
    }

    private static bool TryGetPreviousSiblingParagraph(Paragraph paragraph, out Paragraph previous)
    {
        if (paragraph.Parent is ListItem listItem)
        {
            var index = listItem.Blocks.IndexOf(paragraph);
            if (index > 0 && listItem.Blocks[index - 1] is Paragraph p)
            {
                previous = p;
                return true;
            }
        }
        else if (paragraph.Parent is TableCell tableCell)
        {
            var index = tableCell.Blocks.IndexOf(paragraph);
            if (index > 0 && tableCell.Blocks[index - 1] is Paragraph p)
            {
                previous = p;
                return true;
            }
        }
        else if (paragraph.Parent is Section section)
        {
            var index = section.Blocks.IndexOf(paragraph);
            if (index > 0 && section.Blocks[index - 1] is Paragraph p)
            {
                previous = p;
                return true;
            }
        }

        previous = null!;
        return false;
    }

    private static bool RemoveParagraphFromParent(Paragraph paragraph)
    {
        if (paragraph.Parent is ListItem listItem)
        {
            return listItem.Blocks.Remove(paragraph);
        }

        if (paragraph.Parent is TableCell tableCell)
        {
            return tableCell.Blocks.Remove(paragraph);
        }

        if (paragraph.Parent is Section section)
        {
            return section.Blocks.Remove(paragraph);
        }

        return false;
    }

    public bool HandleKeyDownFromInput(Keys key, ModifierKeys modifiers)
    {
        if (!IsEnabled || !IsFocused)
        {
            return false;
        }

        RecordOperation("KeyDown", $"key={key} mods={modifiers}");
        var inputDescriptor = $"Key:{key}+{modifiers}";
        var undoDepthBefore = _undoManager.UndoDepth;
        var redoDepthBefore = _undoManager.RedoDepth;
        var textLengthBefore = GetText().Length;
        _activeKeyModifiers = modifiers;
        try
        {
            var ctrl = (modifiers & ModifierKeys.Control) != 0;
            if (key == Keys.Space && modifiers == ModifierKeys.None)
            {
                var handled = TryInsertSpaceAtListTableBoundary() || HandleTextCompositionFromInput(" ");
                RichTextBoxDiagnostics.ObserveCommandTrace(
                    inputDescriptor,
                    "SpaceTextInput",
                    canExecute: true,
                    handled,
                    GetText().Length - textLengthBefore,
                    undoDepthBefore,
                    _undoManager.UndoDepth,
                    redoDepthBefore,
                    _undoManager.RedoDepth);
                return handled;
            }

            if (key == Keys.Enter && (ctrl || IsReadOnly) && TryActivateHyperlinkAtSelection())
            {
                RichTextBoxDiagnostics.ObserveCommandTrace(
                    inputDescriptor,
                    "ActivateHyperlink",
                    canExecute: true,
                    handled: true,
                    mutationDelta: 0,
                    undoDepthBefore,
                    _undoManager.UndoDepth,
                    redoDepthBefore,
                    _undoManager.RedoDepth);
                return true;
            }

            if (ctrl && key == Keys.Z)
            {
                var handled = _undoManager.Undo();
                if (handled)
                {
                    ClampSelectionToTextLength();
                    TraceInvariants("Undo");
                    InvalidateVisualWithReason("Undo");
                }

                RichTextBoxDiagnostics.ObserveUndo(
                    "Undo",
                    handled,
                    undoDepthBefore,
                    _undoManager.UndoDepth,
                    redoDepthBefore,
                    _undoManager.RedoDepth,
                    _undoManager.UndoOperationCount,
                    _undoManager.RedoOperationCount);
                RichTextBoxDiagnostics.ObserveCommandTrace(
                    inputDescriptor,
                    "Undo",
                    canExecute: undoDepthBefore > 0,
                    handled,
                    GetText().Length - textLengthBefore,
                    undoDepthBefore,
                    _undoManager.UndoDepth,
                    redoDepthBefore,
                    _undoManager.RedoDepth);
                return true;
            }

            if (ctrl && key == Keys.Y)
            {
                var handled = _undoManager.Redo();
                if (handled)
                {
                    ClampSelectionToTextLength();
                    TraceInvariants("Redo");
                    InvalidateVisualWithReason("Redo");
                }

                RichTextBoxDiagnostics.ObserveUndo(
                    "Redo",
                    handled,
                    undoDepthBefore,
                    _undoManager.UndoDepth,
                    redoDepthBefore,
                    _undoManager.RedoDepth,
                    _undoManager.UndoOperationCount,
                    _undoManager.RedoOperationCount);
                RichTextBoxDiagnostics.ObserveCommandTrace(
                    inputDescriptor,
                    "Redo",
                    canExecute: redoDepthBefore > 0,
                    handled,
                    GetText().Length - textLengthBefore,
                    undoDepthBefore,
                    _undoManager.UndoDepth,
                    redoDepthBefore,
                    _undoManager.RedoDepth);
                return true;
            }

            if (TryExecuteEditingCommandFromKey(key, modifiers))
            {
                RichTextBoxDiagnostics.ObserveCommandTrace(
                    inputDescriptor,
                    "EditingKeyBinding",
                    canExecute: true,
                    handled: true,
                    GetText().Length - textLengthBefore,
                    undoDepthBefore,
                    _undoManager.UndoDepth,
                    redoDepthBefore,
                    _undoManager.RedoDepth);
                return true;
            }

            RichTextBoxDiagnostics.ObserveCommandTrace(
                inputDescriptor,
                "NoMatch",
                canExecute: false,
                handled: false,
                0,
                undoDepthBefore,
                _undoManager.UndoDepth,
                redoDepthBefore,
                _undoManager.RedoDepth);
            return false;
        }
        finally
        {
            _activeKeyModifiers = ModifierKeys.None;
        }
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

    public bool HandlePointerDownFromInput(Vector2 pointerPosition, bool extendSelection)
    {
        if (!IsEnabled)
        {
            return false;
        }

        UpdatePointerClickCount(pointerPosition);
        var index = GetTextIndexFromPoint(pointerPosition);
        _lastSelectionHitTestOffset = index;
        _pendingPointerHyperlinkUri = ResolveHyperlinkUriAtOffset(index);
        _pointerSelectionMoved = false;
        if (_pointerClickCount >= 3)
        {
            SelectParagraphAt(index);
        }
        else if (_pointerClickCount == 2)
        {
            SelectWordAt(index);
        }
        else
        {
            SetCaret(index, extendSelection);
        }

        _isSelectingWithPointer = true;
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateVisualWithReason("PointerDownSelection");
        return true;
    }

    public bool HandlePointerMoveFromInput(Vector2 pointerPosition)
    {
        if (!IsEnabled || !IsFocused || !_isSelectingWithPointer)
        {
            return false;
        }

        var adjustedPoint = pointerPosition;
        AutoScrollForPointer(ref adjustedPoint);
        var index = GetTextIndexFromPoint(adjustedPoint);
        _lastSelectionHitTestOffset = index;
        _pointerSelectionMoved = true;
        _caretIndex = index;
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateVisualWithReason("PointerDragSelection");
        return true;
    }

    public bool HandlePointerUpFromInput()
    {
        if (!_isSelectingWithPointer)
        {
            return false;
        }

        _isSelectingWithPointer = false;
        if (IsReadOnly &&
            !_pointerSelectionMoved &&
            SelectionLength == 0 &&
            !string.IsNullOrWhiteSpace(_pendingPointerHyperlinkUri))
        {
            RaiseHyperlinkNavigate(_pendingPointerHyperlinkUri!);
        }

        _pendingPointerHyperlinkUri = null;
        return true;
    }

    public bool HandleMouseWheelFromInput(int delta)
    {
        if (!IsEnabled || !IsFocused || delta == 0)
        {
            return false;
        }

        var textRect = GetTextRect();
        var layout = BuildOrGetLayout(textRect.Width);
        var previous = _verticalOffset;
        _verticalOffset -= MathF.Sign(delta) * (FontStashTextRenderer.GetLineHeight(Font) * 3f);
        ClampScrollOffsets(layout, textRect);
        if (Math.Abs(previous - _verticalOffset) > 0.01f)
        {
            InvalidateVisualWithReason("MouseWheelScroll");
            return true;
        }

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
        if (isFocused)
        {
            EnsureCaretVisible();
        }

        InvalidateVisual();
    }

    public bool TryConsumeRenderDirtyBoundsHint(out LayoutRect bounds)
    {
        if (!_hasPendingRenderDirtyBoundsHint)
        {
            bounds = default;
            return false;
        }

        bounds = _pendingRenderDirtyBoundsHint;
        _hasPendingRenderDirtyBoundsHint = false;
        return true;
    }

    public override void InvalidateVisual()
    {
        RichTextBoxDiagnostics.ObserveLayoutInvalidation(
            _pendingInvalidationReason,
            GetText().Length,
            _caretIndex,
            SelectionStart,
            SelectionLength);
        _pendingInvalidationReason = "Unspecified";
        _hasPendingRenderDirtyBoundsHint = false;
        base.InvalidateVisual();
    }

    private void InvalidateVisualWithReason(string reason)
    {
        _pendingInvalidationReason = reason;
        InvalidateVisual();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var textWidth = TextWrapping == TextWrapping.NoWrap
            ? float.PositiveInfinity
            : Math.Max(0f, availableSize.X - Padding.Horizontal - (BorderThickness * 2f));
        var layout = BuildOrGetLayout(textWidth);
        _lastMeasuredLayout = layout;
        return new Vector2(
            layout.ContentWidth + Padding.Horizontal + (BorderThickness * 2f),
            layout.ContentHeight + Padding.Vertical + (BorderThickness * 2f));
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
        var renderStartTicks = Stopwatch.GetTimestamp();
        base.OnRender(spriteBatch);
        UiDrawing.DrawFilledRect(spriteBatch, LayoutSlot, Background * Opacity);
        if (BorderThickness > 0f)
        {
            UiDrawing.DrawRectStroke(spriteBatch, LayoutSlot, BorderThickness, BorderBrush * Opacity);
        }

        var textRect = GetTextRect();

        var layout = BuildOrGetLayout(textRect.Width);
        ClampScrollOffsets(layout, textRect);
        DrawSelection(spriteBatch, textRect, layout);
        for (var i = 0; i < layout.Runs.Count; i++)
        {
            var run = layout.Runs[i];
            if (string.IsNullOrEmpty(run.Text))
            {
                continue;
            }

            var color = ResolveRunColor(run.Style);
            var position = new Vector2(textRect.X + run.Bounds.X - _horizontalOffset, textRect.Y + run.Bounds.Y - _verticalOffset);
            if (position.Y + run.Bounds.Height < textRect.Y || position.Y > textRect.Y + textRect.Height)
            {
                continue;
            }

            FontStashTextRenderer.DrawString(spriteBatch, Font, run.Text, position, color * Opacity);
            if (run.Style.IsBold)
            {
                FontStashTextRenderer.DrawString(spriteBatch, Font, run.Text, new Vector2(position.X + 1f, position.Y), color * Opacity * 0.8f);
            }

            if (run.Style.IsUnderline)
            {
                var underlineY = position.Y + run.Bounds.Height - 1f;
                UiDrawing.DrawFilledRect(
                    spriteBatch,
                    new LayoutRect(position.X, underlineY, Math.Max(1f, run.Bounds.Width), 1f),
                    color * Opacity);
            }
        }

        DrawTableBorders(spriteBatch, textRect, layout);
        if (IsFocused && _isCaretVisible)
        {
            DrawCaret(spriteBatch, textRect, layout);
        }

        CaptureDirtyHint(layout, textRect);
        _lastRenderedLayout = layout;
        RecordRenderSample(Stopwatch.GetElapsedTime(renderStartTicks).TotalMilliseconds);
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
        _layoutCache.Invalidate();
        _lastMeasuredLayout = null;
        RaiseRoutedEventInternal(DocumentChangedEvent, new RoutedSimpleEventArgs(DocumentChangedEvent));
        InvalidateMeasure();
        InvalidateVisual();
        _lastDocumentRichness = CaptureDocumentRichness(Document);
        RecordOperation("DocumentPropertyChanged", $"blocks={Document.Blocks.Count}");
    }

    private void OnDocumentChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        ClampSelectionToTextLength();
        _layoutCache.Invalidate();
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
        return DocumentEditing.GetText(Document);
    }

    private void RegisterEditingCommandBindings()
    {
        CommandBindings.Add(new CommandBinding(EditingCommands.Backspace, (_, _) => ExecuteBackspace(), (_, args) => args.CanExecute = CanBackspace()));
        CommandBindings.Add(new CommandBinding(EditingCommands.Delete, (_, _) => ExecuteDelete(), (_, args) => args.CanExecute = CanDelete()));
        CommandBindings.Add(new CommandBinding(EditingCommands.EnterParagraphBreak, (_, _) => ExecuteEnterParagraphBreak(), (_, args) => args.CanExecute = CanMutateText()));
        CommandBindings.Add(new CommandBinding(EditingCommands.EnterLineBreak, (_, _) => ExecuteEnterLineBreak(), (_, args) => args.CanExecute = CanMutateText()));
        CommandBindings.Add(new CommandBinding(EditingCommands.TabForward, (_, _) => ExecuteTabForward(), (_, args) => args.CanExecute = CanMutateText()));
        CommandBindings.Add(new CommandBinding(EditingCommands.TabBackward, (_, _) => ExecuteTabBackward(), (_, args) => args.CanExecute = CanTabBackward()));

        CommandBindings.Add(new CommandBinding(EditingCommands.Copy, (_, _) => ExecuteCopy(), (_, args) => args.CanExecute = SelectionLength > 0));
        CommandBindings.Add(new CommandBinding(EditingCommands.Cut, (_, _) => ExecuteCut(), (_, args) => args.CanExecute = !IsReadOnly && SelectionLength > 0));
        CommandBindings.Add(new CommandBinding(EditingCommands.Paste, (_, _) => ExecutePaste(), (_, args) => args.CanExecute = !IsReadOnly && CanPasteFromClipboard()));

        CommandBindings.Add(new CommandBinding(EditingCommands.SelectAll, (_, _) => ExecuteSelectAll(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveLeftByCharacter, (_, _) => ExecuteMoveLeftByCharacter(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveRightByCharacter, (_, _) => ExecuteMoveRightByCharacter(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveLeftByWord, (_, _) => ExecuteMoveLeftByWord(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveRightByWord, (_, _) => ExecuteMoveRightByWord(), (_, args) => args.CanExecute = true));

        CommandBindings.Add(new CommandBinding(EditingCommands.ToggleBold, (_, _) => ExecuteToggleBold(), (_, args) => args.CanExecute = !IsReadOnly));
        CommandBindings.Add(new CommandBinding(EditingCommands.ToggleItalic, (_, _) => ExecuteToggleItalic(), (_, args) => args.CanExecute = !IsReadOnly));
        CommandBindings.Add(new CommandBinding(EditingCommands.ToggleUnderline, (_, _) => ExecuteToggleUnderline(), (_, args) => args.CanExecute = !IsReadOnly));

        CommandBindings.Add(new CommandBinding(EditingCommands.IncreaseListLevel, (_, _) => ExecuteIncreaseListLevel(), (_, args) => args.CanExecute = CanExecuteListLevelChange(increase: true)));
        CommandBindings.Add(new CommandBinding(EditingCommands.DecreaseListLevel, (_, _) => ExecuteDecreaseListLevel(), (_, args) => args.CanExecute = CanExecuteListLevelChange(increase: false)));
        CommandBindings.Add(new CommandBinding(EditingCommands.InsertTable, (_, _) => ExecuteInsertTable(), (_, args) => args.CanExecute = !IsReadOnly));
        CommandBindings.Add(
            new CommandBinding(
                EditingCommands.SplitCell,
                (_, _) => ExecuteSplitCell(),
                (_, args) =>
                {
                    args.CanExecute = !IsReadOnly && TryGetActiveTableCell(Document, _caretIndex, out TableCellSelectionInfo _);
                }));
        CommandBindings.Add(new CommandBinding(EditingCommands.MergeCells, (_, _) => ExecuteMergeCells(), (_, args) => args.CanExecute = !IsReadOnly && CanMergeActiveCell()));
    }

    private void RegisterEditingInputBindings()
    {
        AddEditingKeyBinding(Keys.Back, ModifierKeys.None, EditingCommands.Backspace);
        AddEditingKeyBinding(Keys.Delete, ModifierKeys.None, EditingCommands.Delete);
        AddEditingKeyBinding(Keys.Enter, ModifierKeys.None, EditingCommands.EnterParagraphBreak);
        AddEditingKeyBinding(Keys.Enter, ModifierKeys.Shift, EditingCommands.EnterLineBreak);
        AddEditingKeyBinding(Keys.Tab, ModifierKeys.None, EditingCommands.TabForward);
        AddEditingKeyBinding(Keys.Tab, ModifierKeys.Shift, EditingCommands.TabBackward);

        AddEditingKeyBinding(Keys.C, ModifierKeys.Control, EditingCommands.Copy);
        AddEditingKeyBinding(Keys.X, ModifierKeys.Control, EditingCommands.Cut);
        AddEditingKeyBinding(Keys.V, ModifierKeys.Control, EditingCommands.Paste);
        AddEditingKeyBinding(Keys.A, ModifierKeys.Control, EditingCommands.SelectAll);

        AddEditingKeyBinding(Keys.Left, ModifierKeys.None, EditingCommands.MoveLeftByCharacter);
        AddEditingKeyBinding(Keys.Right, ModifierKeys.None, EditingCommands.MoveRightByCharacter);
        AddEditingKeyBinding(Keys.Left, ModifierKeys.Control, EditingCommands.MoveLeftByWord);
        AddEditingKeyBinding(Keys.Right, ModifierKeys.Control, EditingCommands.MoveRightByWord);
        AddEditingKeyBinding(Keys.Left, ModifierKeys.Shift, EditingCommands.MoveLeftByCharacter);
        AddEditingKeyBinding(Keys.Right, ModifierKeys.Shift, EditingCommands.MoveRightByCharacter);
        AddEditingKeyBinding(Keys.Left, ModifierKeys.Control | ModifierKeys.Shift, EditingCommands.MoveLeftByWord);
        AddEditingKeyBinding(Keys.Right, ModifierKeys.Control | ModifierKeys.Shift, EditingCommands.MoveRightByWord);

        AddEditingKeyBinding(Keys.B, ModifierKeys.Control, EditingCommands.ToggleBold);
        AddEditingKeyBinding(Keys.I, ModifierKeys.Control, EditingCommands.ToggleItalic);
        AddEditingKeyBinding(Keys.U, ModifierKeys.Control, EditingCommands.ToggleUnderline);
    }

    private void AddEditingKeyBinding(Keys key, ModifierKeys modifiers, RoutedCommand command)
    {
        InputBindings.Add(
            new KeyBinding
            {
                Key = key,
                Modifiers = modifiers,
                Command = command
            });
    }

    private bool TryExecuteEditingCommandFromKey(Keys key, ModifierKeys modifiers)
    {
        if (key == Keys.Up || key == Keys.Down)
        {
            if ((modifiers & ModifierKeys.Control) != 0)
            {
                return false;
            }

            var extend = (modifiers & ModifierKeys.Shift) != 0;
            MoveCaretByLine(moveUp: key == Keys.Up, extendSelection: extend);
            return true;
        }

        if (key == Keys.Home)
        {
            var shift = (modifiers & ModifierKeys.Shift) != 0;
            if ((modifiers & ModifierKeys.Control) != 0)
            {
                SetCaret(0, shift);
            }
            else
            {
                MoveCaretToLineBoundary(moveToLineStart: true, extendSelection: shift);
            }

            return true;
        }

        if (key == Keys.End)
        {
            var shift = (modifiers & ModifierKeys.Shift) != 0;
            if ((modifiers & ModifierKeys.Control) != 0)
            {
                SetCaret(GetText().Length, shift);
            }
            else
            {
                MoveCaretToLineBoundary(moveToLineStart: false, extendSelection: shift);
            }

            return true;
        }

        if (!HasEditingKeyBinding(key, modifiers))
        {
            return false;
        }

        return InputGestureService.Execute(key, modifiers, this, this);
    }

    private bool HasEditingKeyBinding(Keys key, ModifierKeys modifiers)
    {
        for (var i = 0; i < InputBindings.Count; i++)
        {
            if (InputBindings[i] is KeyBinding binding && binding.Matches(key, modifiers))
            {
                return true;
            }
        }

        return false;
    }

    private bool CanMutateText()
    {
        return !IsReadOnly;
    }

    private bool CanBackspace()
    {
        return !IsReadOnly && (SelectionLength > 0 || _caretIndex > 0);
    }

    private bool CanDelete()
    {
        return !IsReadOnly && (SelectionLength > 0 || _caretIndex < GetText().Length);
    }

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

    private bool CanMergeActiveCell()
    {
        if (!TryGetActiveTableCell(Document, _caretIndex, out var active))
        {
            return false;
        }

        return active.CellIndex + 1 < active.Row.Cells.Count;
    }

    private bool CanApplyInlineFormat()
    {
        return !IsReadOnly && SelectionLength > 0;
    }

    private bool ExtendSelectionModifierActive()
    {
        return (_activeKeyModifiers & ModifierKeys.Shift) != 0;
    }

    private void ExecuteBackspace()
    {
        RecordOperation("Command", "Backspace");
        if (TryHandleTableBoundaryDeletion(backspace: true))
        {
            RecordOperation("Branch", "Backspace->TableBoundaryDeletion");
            return;
        }

        if (SelectionLength > 0)
        {
            if (TryDeleteSelectionPreservingStructure("DeleteSelection", GroupingPolicy.StructuralAtomic))
            {
                RecordOperation("Branch", "Backspace->DeleteSelectionStructured");
                return;
            }

            RecordOperation("Branch", "Backspace->DeleteSelection");
            ReplaceSelection(string.Empty, "DeleteSelection", GroupingPolicy.StructuralAtomic);
            return;
        }

        if (_caretIndex <= 0)
        {
            RecordOperation("Branch", "Backspace->NoopAtStart");
            return;
        }

        if (TryBackspaceAtStructuredParagraphStart("Backspace", GroupingPolicy.DeletionBurst))
        {
            RecordOperation("Branch", "Backspace->StructuredParagraphStart");
            return;
        }

        _selectionAnchor = _caretIndex - 1;
        if (TryReplaceSelectionWithinParagraphPreservingInlineStyles(string.Empty, "Backspace", GroupingPolicy.DeletionBurst))
        {
            RecordOperation("Branch", "Backspace->StructuredCharacterDeleteStyled");
            return;
        }

        if (TryReplaceSelectionWithinPlainParagraphPreservingStructure(string.Empty, "Backspace", GroupingPolicy.DeletionBurst))
        {
            RecordOperation("Branch", "Backspace->StructuredCharacterDelete");
            return;
        }

        RecordOperation("Branch", "Backspace->FallbackReplaceSelection");
        ReplaceSelection(string.Empty, "Backspace", GroupingPolicy.DeletionBurst);
    }

    private void ExecuteDelete()
    {
        RecordOperation("Command", "Delete");
        if (TryHandleTableBoundaryDeletion(backspace: false))
        {
            RecordOperation("Branch", "Delete->TableBoundaryDeletion");
            return;
        }

        if (SelectionLength > 0)
        {
            if (TryDeleteSelectionPreservingStructure("DeleteSelection", GroupingPolicy.StructuralAtomic))
            {
                RecordOperation("Branch", "Delete->DeleteSelectionStructured");
                return;
            }

            RecordOperation("Branch", "Delete->DeleteSelection");
            ReplaceSelection(string.Empty, "DeleteSelection", GroupingPolicy.StructuralAtomic);
            return;
        }

        var text = GetText();
        if (_caretIndex >= text.Length)
        {
            RecordOperation("Branch", "Delete->NoopAtEnd");
            return;
        }

        _selectionAnchor = _caretIndex + 1;
        if (TryReplaceSelectionWithinParagraphPreservingInlineStyles(string.Empty, "DeleteForward", GroupingPolicy.DeletionBurst))
        {
            RecordOperation("Branch", "Delete->StructuredCharacterDeleteStyled");
            return;
        }

        if (TryReplaceSelectionWithinPlainParagraphPreservingStructure(string.Empty, "DeleteForward", GroupingPolicy.DeletionBurst))
        {
            RecordOperation("Branch", "Delete->StructuredCharacterDelete");
            return;
        }

        if (DocumentContainsRichInlineFormatting(Document) &&
            IsAtParagraphSeparator(Document, _caretIndex))
        {
            RecordOperation("Branch", "Delete->RichParagraphBoundaryNoop");
            return;
        }

        RecordOperation("Branch", "Delete->FallbackReplaceSelection");
        ReplaceSelection(string.Empty, "DeleteForward", GroupingPolicy.DeletionBurst);
    }

    private bool TryDeleteSelectionPreservingStructure(string commandType, GroupingPolicy policy)
    {
        var start = SelectionStart;
        var length = SelectionLength;
        if (length <= 0)
        {
            return false;
        }

        if (!DocumentContainsRichInlineFormatting(Document))
        {
            return false;
        }

        var textLength = GetText().Length;
        if (start == 0 && length >= textLength)
        {
            RecordOperation("DeleteSelectionStructured", "SkipForFullDocumentSelection");
            return false;
        }

        var end = start + length;
        var editStart = Stopwatch.GetTimestamp();
        var undoDepthBefore = _undoManager.UndoDepth;
        var redoDepthBefore = _undoManager.RedoDepth;
        var textLengthBefore = GetText().Length;
        var beforeDocument = DocumentEditing.CloneDocument(Document);
        var beforeText = GetText();
        var entries = CollectParagraphEntries(Document);
        if (entries.Count == 0)
        {
            return false;
        }
        var firstTableParagraphStart = int.MaxValue;
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].Paragraph.Parent is TableCell)
            {
                firstTableParagraphStart = Math.Min(firstTableParagraphStart, entries[i].StartOffset);
            }
        }

        var protectTableParagraphs = false;
        if (firstTableParagraphStart != int.MaxValue)
        {
            if (start < firstTableParagraphStart && end > firstTableParagraphStart)
            {
                end = firstTableParagraphStart;
                length = Math.Max(0, end - start);
                if (length <= 0)
                {
                    return false;
                }

                protectTableParagraphs = true;
                RecordOperation("DeleteSelectionStructured", $"ClampToTableBoundary start={start} end={end}");
            }
            else if (end <= firstTableParagraphStart)
            {
                protectTableParagraphs = true;
            }
        }

        var afterDocument = DocumentEditing.CloneDocument(Document);
        var paragraphs = new List<Paragraph>(FlowDocumentPlainText.EnumerateParagraphs(afterDocument));
        if (paragraphs.Count != entries.Count)
        {
            return false;
        }

        var changed = false;
        for (var i = 0; i < entries.Count; i++)
        {
            if (protectTableParagraphs && paragraphs[i].Parent is TableCell)
            {
                continue;
            }

            var overlapStart = Math.Max(start, entries[i].StartOffset);
            var overlapEnd = Math.Min(end, entries[i].EndOffset);
            if (overlapEnd <= overlapStart)
            {
                continue;
            }

            var paragraph = paragraphs[i];
            paragraph.Inlines.Clear();
            var paragraphLength = Math.Max(0, entries[i].EndOffset - entries[i].StartOffset);
            var localStart = Math.Clamp(overlapStart - entries[i].StartOffset, 0, paragraphLength);
            var localEnd = Math.Clamp(overlapEnd - entries[i].StartOffset, localStart, paragraphLength);
            AppendStyledInlineRangeFromParagraph(entries[i].Paragraph, 0, localStart, paragraph);
            AppendStyledInlineRangeFromParagraph(entries[i].Paragraph, localEnd, paragraphLength, paragraph);
            if (paragraph.Inlines.Count == 0)
            {
                paragraph.Inlines.Add(new Run(string.Empty));
            }

            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        PruneEmptyStructuralScaffolding(afterDocument);

        RecordOperation("MutationRoute", $"DeleteSelectionStructured cmd={commandType} policy={policy} start={start} len={length}");
        var afterText = DocumentEditing.GetText(afterDocument);
        var caretAfter = start;
        var session = new DocumentEditSession(Document, _undoManager);
        session.BeginTransaction(
            commandType,
            policy,
            new DocumentEditContext(
                _caretIndex,
                caretAfter,
                start,
                length,
                caretAfter,
                0,
                commandType));
        session.ApplyOperation(new ReplaceDocumentOperation("Document", beforeText, afterText, beforeDocument, afterDocument));
        session.CommitTransaction();
        _caretIndex = caretAfter;
        _selectionAnchor = _caretIndex;
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        RecordEditSample(elapsedMs);
        RichTextBoxDiagnostics.ObserveEdit(commandType, elapsedMs, start, length, _caretIndex);
        RichTextBoxDiagnostics.ObserveCommandTrace(
            "EditMethod",
            commandType,
            canExecute: true,
            handled: true,
            GetText().Length - textLengthBefore,
            undoDepthBefore,
            _undoManager.UndoDepth,
            redoDepthBefore,
            _undoManager.RedoDepth);
        TraceInvariants(commandType);
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateMeasure();
        InvalidateVisualWithReason($"Edit:{commandType}");
        return true;
    }

    private static void AppendStyledInlineRangeFromParagraph(Paragraph source, int start, int end, Paragraph destination)
    {
        if (end <= start)
        {
            return;
        }

        var cursor = 0;
        for (var i = 0; i < source.Inlines.Count; i++)
        {
            AppendInlineRange(source.Inlines[i], start, end, ref cursor, destination.Inlines.Add);
        }
    }

    private static void AppendInlineRange(Inline inline, int start, int end, ref int cursor, Action<Inline> append)
    {
        var inlineStart = cursor;
        var inlineLength = GetInlineLogicalLength(inline);
        var inlineEnd = inlineStart + inlineLength;
        cursor = inlineEnd;

        var overlapStart = Math.Max(start, inlineStart);
        var overlapEnd = Math.Min(end, inlineEnd);
        if (overlapEnd <= overlapStart)
        {
            return;
        }

        switch (inline)
        {
            case Run run:
            {
                var localStart = overlapStart - inlineStart;
                var localLength = overlapEnd - overlapStart;
                if (localLength <= 0)
                {
                    return;
                }

                var text = run.Text.Substring(localStart, localLength);
                var clone = new Run(text)
                {
                    Foreground = run.Foreground
                };
                append(clone);
                return;
            }
            case LineBreak:
                append(new LineBreak());
                return;
            case Bold or Italic or Underline or Hyperlink or Span:
            {
                if (inline is not Span span)
                {
                    return;
                }

                var clonedSpan = CloneSpanShell(span);
                var nestedCursor = inlineStart;
                for (var i = 0; i < span.Inlines.Count; i++)
                {
                    AppendInlineRange(span.Inlines[i], start, end, ref nestedCursor, clonedSpan.Inlines.Add);
                }

                if (clonedSpan.Inlines.Count > 0)
                {
                    append(clonedSpan);
                }

                return;
            }
            case InlineUIContainer:
                append(new InlineUIContainer());
                return;
            default:
                return;
        }
    }

    private static Span CloneSpanShell(Span span)
    {
        return span switch
        {
            Bold => new Bold(),
            Italic => new Italic(),
            Underline => new Underline(),
            Hyperlink hyperlink => new Hyperlink { NavigateUri = hyperlink.NavigateUri },
            _ => new Span()
        };
    }

    private static void PruneEmptyStructuralScaffolding(FlowDocument document)
    {
        for (var blockIndex = document.Blocks.Count - 1; blockIndex >= 0; blockIndex--)
        {
            if (document.Blocks[blockIndex] is not InkkSlinger.List list)
            {
                continue;
            }

            for (var itemIndex = list.Items.Count - 1; itemIndex >= 0; itemIndex--)
            {
                if (ListItemHasVisibleContent(list.Items[itemIndex]))
                {
                    continue;
                }

                list.Items.RemoveAt(itemIndex);
            }

            if (list.Items.Count == 0)
            {
                document.Blocks.RemoveAt(blockIndex);
            }
        }

        PruneRedundantEmptyTopLevelParagraphs(document);
    }

    private static void PruneRedundantEmptyTopLevelParagraphs(FlowDocument document)
    {
        var seenEmptyParagraph = false;
        for (var i = document.Blocks.Count - 1; i >= 0; i--)
        {
            if (document.Blocks[i] is not Paragraph paragraph)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(FlowDocumentPlainText.GetInlineText(paragraph.Inlines)))
            {
                continue;
            }

            if (!seenEmptyParagraph)
            {
                seenEmptyParagraph = true;
                continue;
            }

            if (document.Blocks.Count > 1)
            {
                document.Blocks.RemoveAt(i);
            }
        }
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

    private void ExecuteEnterParagraphBreak()
    {
        RecordOperation("Command", "EnterParagraphBreak");
        if (IsReadOnly)
        {
            RecordOperation("Branch", "EnterParagraphBreak->ReadOnlyNoop");
            return;
        }

        if (TryInsertParagraphBreakWithinStructuredParagraph("EnterParagraphBreak", GroupingPolicy.StructuralAtomic))
        {
            RecordOperation("Branch", "EnterParagraphBreak->Structured");
            return;
        }

        RecordOperation("Branch", "EnterParagraphBreak->FallbackFragmentInsert");
        var fragment = new FlowDocument();
        fragment.Blocks.Add(CreateParagraph(string.Empty));
        fragment.Blocks.Add(CreateParagraph(string.Empty));
        ReplaceSelectionWithFragment(fragment, "EnterParagraphBreak", GroupingPolicy.StructuralAtomic);
    }

    private void ExecuteEnterLineBreak()
    {
        if (IsReadOnly)
        {
            return;
        }

        ApplyStructuralEdit(
            "EnterLineBreak",
            GroupingPolicy.StructuralAtomic,
            static (doc, start, length, _) =>
            {
                var fragment = new FlowDocument();
                var paragraph = new Paragraph();
                paragraph.Inlines.Add(new LineBreak());
                fragment.Blocks.Add(paragraph);
                var merged = BuildDocumentWithFragment(doc, fragment, start, length);
                DocumentEditing.ReplaceDocumentContent(doc, merged);
                return true;
            },
            postApply: (_, start, _, _) =>
            {
                _caretIndex = start + 1;
                _selectionAnchor = _caretIndex;
            });
    }

    private void ExecuteTabForward()
    {
        if (TryMoveCaretToAdjacentTableCell(forward: true))
        {
            return;
        }

        ReplaceSelection("\t", "TabForward", GroupingPolicy.StructuralAtomic);
    }

    private void ExecuteTabBackward()
    {
        if (TryMoveCaretToAdjacentTableCell(forward: false))
        {
            return;
        }

        if (CanExecuteListLevelChange(increase: false))
        {
            ExecuteDecreaseListLevel();
            return;
        }
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
        PublishRichClipboardPayloads(richSlice, selected);
        RichTextBoxDiagnostics.ObserveClipboardPayload(
            "Copy",
            "Rich+Text",
            Encoding.UTF8.GetByteCount(richSlice) + Encoding.UTF8.GetByteCount(selected),
            "WriteClipboard");
        var elapsedMs = Stopwatch.GetElapsedTime(serializeStart).TotalMilliseconds;
        RecordClipboardSerializeSample(elapsedMs);
        RichTextBoxDiagnostics.ObserveClipboard("Copy", usedRichPayload: true, fallbackToText: false, elapsedMs);
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
        PublishRichClipboardPayloads(richSlice, selected);
        RichTextBoxDiagnostics.ObserveClipboardPayload(
            "Cut",
            "Rich+Text",
            Encoding.UTF8.GetByteCount(richSlice) + Encoding.UTF8.GetByteCount(selected),
            "WriteClipboard");
        var elapsedMs = Stopwatch.GetElapsedTime(serializeStart).TotalMilliseconds;
        RecordClipboardSerializeSample(elapsedMs);
        RichTextBoxDiagnostics.ObserveClipboard("Cut", usedRichPayload: true, fallbackToText: false, elapsedMs);
        ReplaceSelection(string.Empty, "CutSelection", GroupingPolicy.StructuralAtomic);
    }

    private void ExecutePaste()
    {
        if (IsReadOnly)
        {
            return;
        }

        var pasteStart = Stopwatch.GetTimestamp();
        var usedRichPayload = false;
        var fallbackToText = false;
        if (TryGetRichClipboardPayload(out var richPayload, out var richFormat))
        {
            usedRichPayload = true;
            RichTextBoxDiagnostics.ObserveClipboardPayload(
                "Paste",
                $"Rich:{richFormat}",
                Encoding.UTF8.GetByteCount(richPayload),
                "ReadRichPayload");
            var deserializeStart = Stopwatch.GetTimestamp();
            try
            {
                var fragment = FlowDocumentSerializer.DeserializeFragment(richPayload);
                RecordClipboardDeserializeSample(Stopwatch.GetElapsedTime(deserializeStart).TotalMilliseconds);
                if (TryPasteRichFragment(fragment))
                {
                    RichTextBoxDiagnostics.ObserveClipboard(
                        "Paste",
                        usedRichPayload: true,
                        fallbackToText: false,
                        Stopwatch.GetElapsedTime(pasteStart).TotalMilliseconds);
                    RichTextBoxDiagnostics.ObserveClipboardPayload(
                        "Paste",
                        $"Rich:{richFormat}",
                        Encoding.UTF8.GetByteCount(richPayload),
                        "AppliedRichFragment");
                    return;
                }

                fallbackToText = true;
                RichTextBoxDiagnostics.ObserveClipboardPayload(
                    "Paste",
                    $"Rich:{richFormat}",
                    Encoding.UTF8.GetByteCount(richPayload),
                    "RichFragmentRejectedFallbackToText");
            }
            catch (Exception)
            {
                RecordClipboardDeserializeSample(Stopwatch.GetElapsedTime(deserializeStart).TotalMilliseconds);
                fallbackToText = true;
                RichTextBoxDiagnostics.ObserveClipboardPayload(
                    "Paste",
                    $"Rich:{richFormat}",
                    Encoding.UTF8.GetByteCount(richPayload),
                    "RichDeserializeExceptionFallbackToText");
                // Fall through to text fallback when rich payload is invalid.
            }
        }

        if (!TextClipboard.TryGetText(out var pasted))
        {
            RichTextBoxDiagnostics.ObserveClipboard(
                "Paste",
                usedRichPayload,
                fallbackToText,
                Stopwatch.GetElapsedTime(pasteStart).TotalMilliseconds);
            return;
        }

        RichTextBoxDiagnostics.ObserveClipboardPayload(
            "Paste",
            "Text",
            Encoding.UTF8.GetByteCount(pasted),
            fallbackToText ? "FallbackTextApplied" : "TextApplied");
        ReplaceSelection(NormalizeNewlines(pasted), "Paste", GroupingPolicy.StructuralAtomic);
        RichTextBoxDiagnostics.ObserveClipboard(
            "Paste",
            usedRichPayload,
            fallbackToText,
            Stopwatch.GetElapsedTime(pasteStart).TotalMilliseconds);
    }

    private void ExecuteSelectAll()
    {
        _selectionAnchor = 0;
        _caretIndex = GetText().Length;
        EnsureCaretVisible();
        InvalidateVisual();
    }

    private void ExecuteMoveLeftByCharacter()
    {
        MoveCaret(-1, ExtendSelectionModifierActive());
    }

    private void ExecuteMoveRightByCharacter()
    {
        MoveCaret(1, ExtendSelectionModifierActive());
    }

    private void ExecuteMoveLeftByWord()
    {
        MoveCaretByWord(moveLeft: true, extendSelection: ExtendSelectionModifierActive());
    }

    private void ExecuteMoveRightByWord()
    {
        MoveCaretByWord(moveLeft: false, extendSelection: ExtendSelectionModifierActive());
    }

    private void ExecuteToggleBold()
    {
        RecordOperation("Command", "ToggleBold");
        if (SelectionLength <= 0)
        {
            _typingBoldActive = !_typingBoldActive;
            RecordOperation("Branch", $"ToggleBold->TypingMode:{_typingBoldActive}");
            return;
        }

        RecordOperation("Branch", "ToggleBold->ApplyInlineFormatToSelection");
        ApplyInlineFormatToSelection<Bold>(static () => new Bold(), "ToggleBold");
    }

    private void ExecuteToggleItalic()
    {
        RecordOperation("Command", "ToggleItalic");
        if (SelectionLength <= 0)
        {
            _typingItalicActive = !_typingItalicActive;
            RecordOperation("Branch", $"ToggleItalic->TypingMode:{_typingItalicActive}");
            return;
        }

        RecordOperation("Branch", "ToggleItalic->ApplyInlineFormatToSelection");
        ApplyInlineFormatToSelection<Italic>(static () => new Italic(), "ToggleItalic");
    }

    private void ExecuteToggleUnderline()
    {
        RecordOperation("Command", "ToggleUnderline");
        if (SelectionLength <= 0)
        {
            _typingUnderlineActive = !_typingUnderlineActive;
            RecordOperation("Branch", $"ToggleUnderline->TypingMode:{_typingUnderlineActive}");
            return;
        }

        RecordOperation("Branch", "ToggleUnderline->ApplyInlineFormatToSelection");
        ApplyInlineFormatToSelection<Underline>(static () => new Underline(), "ToggleUnderline");
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

    private void ApplyInlineFormatToSelection<TSpan>(Func<Span> spanFactory, string commandType)
        where TSpan : Span
    {
        if (!CanApplyInlineFormat())
        {
            RecordOperation("FormatSelection", $"{commandType}->CannotApply");
            return;
        }

        var editStart = Stopwatch.GetTimestamp();
        var undoDepthBefore = _undoManager.UndoDepth;
        var redoDepthBefore = _undoManager.RedoDepth;
        var textLengthBefore = GetText().Length;
        var start = SelectionStart;
        var length = SelectionLength;
        var originalAnchor = _selectionAnchor;
        var originalCaret = _caretIndex;
        var beforeDoc = DocumentEditing.CloneDocument(Document);
        var beforeText = GetText();
        var fragmentXml = FlowDocumentSerializer.SerializeRange(Document, start, start + length);
        var formattedFragment = FlowDocumentSerializer.DeserializeFragment(fragmentXml);
        var removeExistingFormat = SelectionIsFullyStyledBy<TSpan>(formattedFragment);
        RecordOperation("FormatSelection", $"{commandType}->Begin removeExisting={removeExistingFormat} sel=({start},{length})");
        FlowDocument afterDocument;
        if (start == 0 &&
            length == GetText().Length &&
            TryBuildDocumentWithWholeDocumentInlineFormat<TSpan>(
                Document,
                removeExistingFormat,
                spanFactory,
                out afterDocument))
        {
            RecordOperation("FormatSelection", $"{commandType}->WholeDocumentStructuredPath");
        }
        else if (TryBuildDocumentWithWholeParagraphInlineFormat<TSpan>(
                Document,
                start,
                length,
                removeExistingFormat,
                spanFactory,
                out afterDocument))
        {
            RecordOperation("FormatSelection", $"{commandType}->WholeParagraphPath");
            // afterDocument produced by whole-paragraph in-place transform on clone.
        }
        else if (TryBuildDocumentWithInlineFormatWithinParagraph<TSpan>(
                     Document,
                     start,
                     length,
                     removeExistingFormat,
                     spanFactory,
                     out afterDocument))
        {
            RecordOperation("FormatSelection", $"{commandType}->SingleParagraphStructuredPath");
        }
        else if (TryBuildDocumentWithInlineFormatAcrossParagraphs<TSpan>(
                     Document,
                     start,
                     length,
                     removeExistingFormat,
                     spanFactory,
                     out afterDocument))
        {
            RecordOperation("FormatSelection", $"{commandType}->MultiParagraphStructuredPath");
        }
        else if (DocumentContainsRichInlineFormatting(Document) &&
                 TryBuildDocumentWithSelectedParagraphStructuredFallback<TSpan>(
                     Document,
                     start,
                     length,
                     removeExistingFormat,
                     spanFactory,
                     out afterDocument))
        {
            RecordOperation("FormatSelection", $"{commandType}->RichDocumentParagraphFallbackPath");
        }
        else if (removeExistingFormat)
        {
            RecordOperation("FormatSelection", $"{commandType}->FragmentRemovePath");
            RemoveInlineSpanFromParagraphs<TSpan>(formattedFragment);
            afterDocument = BuildDocumentWithFragment(Document, formattedFragment, start, length);
        }
        else
        {
            RecordOperation("FormatSelection", $"{commandType}->FragmentApplyPath");
            ApplyInlineSpanToParagraphs(formattedFragment, spanFactory);
            afterDocument = BuildDocumentWithFragment(Document, formattedFragment, start, length);
        }
        var afterText = DocumentEditing.GetText(afterDocument);
        var session = new DocumentEditSession(Document, _undoManager);
        session.BeginTransaction(
            commandType,
            GroupingPolicy.FormatBurst,
            new DocumentEditContext(
                _caretIndex,
                _caretIndex,
                start,
                length,
                start,
                length,
                commandType));
        session.ApplyOperation(new ReplaceDocumentOperation("Document", beforeText, afterText, beforeDoc, afterDocument));
        session.CommitTransaction();
        var maxCaret = GetText().Length;
        _selectionAnchor = Math.Clamp(originalAnchor, 0, maxCaret);
        _caretIndex = Math.Clamp(originalCaret, 0, maxCaret);
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        RecordEditSample(elapsedMs);
        RichTextBoxDiagnostics.ObserveEdit(commandType, elapsedMs, start, length, _caretIndex);
        RichTextBoxDiagnostics.ObserveCommandTrace(
            "EditMethod",
            commandType,
            canExecute: true,
            handled: true,
            GetText().Length - textLengthBefore,
            undoDepthBefore,
            _undoManager.UndoDepth,
            redoDepthBefore,
            _undoManager.RedoDepth);
        TraceInvariants(commandType);
        EnsureCaretVisible();
        InvalidateMeasure();
        InvalidateVisualWithReason($"Edit:{commandType}");
    }

    private static bool TryBuildDocumentWithWholeDocumentInlineFormat<TSpan>(
        FlowDocument source,
        bool removeExistingFormat,
        Func<Span> spanFactory,
        out FlowDocument afterDocument)
        where TSpan : Span
    {
        afterDocument = DocumentEditing.CloneDocument(source);
        if (afterDocument.Blocks.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < afterDocument.Blocks.Count; i++)
        {
            if (removeExistingFormat)
            {
                RemoveInlineSpanFromBlock<TSpan>(afterDocument.Blocks[i]);
            }
            else
            {
                ApplyInlineSpanToBlock(afterDocument.Blocks[i], spanFactory);
            }
        }

        return true;
    }

    private static bool TryBuildDocumentWithSelectedParagraphStructuredFallback<TSpan>(
        FlowDocument source,
        int selectionStart,
        int selectionLength,
        bool removeExistingFormat,
        Func<Span> spanFactory,
        out FlowDocument afterDocument)
        where TSpan : Span
    {
        afterDocument = null!;
        if (selectionLength <= 0)
        {
            return false;
        }

        var selected = ResolveSelectedParagraphs(source, selectionStart, selectionLength, selectionStart + selectionLength);
        if (selected.Count == 0)
        {
            return false;
        }

        var entries = CollectParagraphEntries(source);
        if (entries.Count == 0)
        {
            return false;
        }

        var selectedStarts = new HashSet<int>();
        for (var i = 0; i < selected.Count; i++)
        {
            selectedStarts.Add(selected[i].StartOffset);
        }

        afterDocument = DocumentEditing.CloneDocument(source);
        var cloneEntries = CollectParagraphEntries(afterDocument);
        if (cloneEntries.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < cloneEntries.Count; i++)
        {
            if (!selectedStarts.Contains(cloneEntries[i].StartOffset))
            {
                continue;
            }

            var paragraph = cloneEntries[i].Paragraph;
            if (removeExistingFormat)
            {
                RemoveInlineSpanFromCollection<TSpan>(paragraph.Inlines);
            }
            else
            {
                WrapParagraphInlines(paragraph, spanFactory);
            }

            if (paragraph.Inlines.Count == 0)
            {
                paragraph.Inlines.Add(new Run(string.Empty));
            }
        }

        return true;
    }

    private static bool TryBuildDocumentWithInlineFormatAcrossParagraphs<TSpan>(
        FlowDocument source,
        int selectionStart,
        int selectionLength,
        bool removeExistingFormat,
        Func<Span> spanFactory,
        out FlowDocument afterDocument)
        where TSpan : Span
    {
        afterDocument = null!;
        if (selectionLength <= 0)
        {
            return false;
        }

        var entries = CollectParagraphEntries(source);
        if (entries.Count == 0)
        {
            return false;
        }

        var selectionEnd = selectionStart + selectionLength;
        var first = -1;
        var last = -1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].EndOffset <= selectionStart || entries[i].StartOffset >= selectionEnd)
            {
                continue;
            }

            first = first < 0 ? i : first;
            last = i;
        }

        if (first < 0 || last < 0 || first == last)
        {
            return false;
        }

        afterDocument = DocumentEditing.CloneDocument(source);
        var paragraphs = new List<Paragraph>(FlowDocumentPlainText.EnumerateParagraphs(afterDocument));
        if (last >= paragraphs.Count)
        {
            return false;
        }

        for (var index = first; index <= last; index++)
        {
            var paragraph = paragraphs[index];
            var paragraphText = FlowDocumentPlainText.GetInlineText(paragraph.Inlines);
            var localStart = Math.Clamp(selectionStart - entries[index].StartOffset, 0, paragraphText.Length);
            var localEnd = Math.Clamp(selectionEnd - entries[index].StartOffset, 0, paragraphText.Length);
            if (localEnd < localStart)
            {
                continue;
            }

            var formatLength = localEnd - localStart;
            if (formatLength <= 0)
            {
                continue;
            }

            var before = paragraphText[..localStart];
            var middle = paragraphText.Substring(localStart, formatLength);
            var after = paragraphText[localEnd..];
            paragraph.Inlines.Clear();

            if (before.Length > 0)
            {
                paragraph.Inlines.Add(new Run(before));
            }

            if (middle.Length > 0)
            {
                if (removeExistingFormat)
                {
                    paragraph.Inlines.Add(new Run(middle));
                }
                else
                {
                    var span = spanFactory();
                    span.Inlines.Add(new Run(middle));
                    paragraph.Inlines.Add(span);
                }
            }

            if (after.Length > 0)
            {
                paragraph.Inlines.Add(new Run(after));
            }

            if (paragraph.Inlines.Count == 0)
            {
                paragraph.Inlines.Add(new Run(string.Empty));
            }
        }

        return true;
    }

    private static bool TryBuildDocumentWithInlineFormatWithinParagraph<TSpan>(
        FlowDocument source,
        int selectionStart,
        int selectionLength,
        bool removeExistingFormat,
        Func<Span> spanFactory,
        out FlowDocument afterDocument)
        where TSpan : Span
    {
        afterDocument = null!;
        if (selectionLength <= 0)
        {
            return false;
        }

        var entries = CollectParagraphEntries(source);
        if (entries.Count == 0)
        {
            return false;
        }

        var selectionEnd = selectionStart + selectionLength;
        var paragraphIndex = -1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (selectionStart >= entries[i].StartOffset &&
                selectionEnd <= entries[i].EndOffset)
            {
                paragraphIndex = i;
                break;
            }
        }

        if (paragraphIndex < 0)
        {
            return false;
        }

        var localStart = selectionStart - entries[paragraphIndex].StartOffset;
        var paragraphText = FlowDocumentPlainText.GetInlineText(entries[paragraphIndex].Paragraph.Inlines);
        if (localStart < 0 || localStart > paragraphText.Length)
        {
            return false;
        }

        var localLength = Math.Clamp(selectionLength, 0, paragraphText.Length - localStart);
        if (localLength <= 0)
        {
            return false;
        }

        afterDocument = DocumentEditing.CloneDocument(source);
        var paragraphs = new List<Paragraph>(FlowDocumentPlainText.EnumerateParagraphs(afterDocument));
        if (paragraphIndex >= paragraphs.Count)
        {
            return false;
        }

        var paragraph = paragraphs[paragraphIndex];
        var sourceParagraph = entries[paragraphIndex].Paragraph;
        var localEnd = localStart + localLength;
        var selectedText = paragraphText.Substring(localStart, localLength);

        paragraph.Inlines.Clear();
        AppendStyledInlineRangeFromParagraph(sourceParagraph, 0, localStart, paragraph);

        if (selectedText.Length > 0)
        {
            if (removeExistingFormat)
            {
                paragraph.Inlines.Add(new Run(selectedText));
            }
            else
            {
                var span = spanFactory();
                span.Inlines.Add(new Run(selectedText));
                paragraph.Inlines.Add(span);
            }
        }

        AppendStyledInlineRangeFromParagraph(sourceParagraph, localEnd, paragraphText.Length, paragraph);

        if (paragraph.Inlines.Count == 0)
        {
            paragraph.Inlines.Add(new Run(string.Empty));
        }

        return true;
    }

    private static bool TryBuildDocumentWithWholeParagraphInlineFormat<TSpan>(
        FlowDocument source,
        int selectionStart,
        int selectionLength,
        bool removeExistingFormat,
        Func<Span> spanFactory,
        out FlowDocument afterDocument)
        where TSpan : Span
    {
        afterDocument = null!;
        if (selectionLength <= 0)
        {
            return false;
        }

        var entries = CollectParagraphEntries(source);
        if (entries.Count == 0)
        {
            return false;
        }

        var selectionEnd = selectionStart + selectionLength;
        var targetParagraphIndex = -1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (selectionStart == entries[i].StartOffset &&
                selectionEnd == entries[i].EndOffset)
            {
                targetParagraphIndex = i;
                break;
            }
        }

        if (targetParagraphIndex < 0)
        {
            return false;
        }

        afterDocument = DocumentEditing.CloneDocument(source);
        var paragraphs = new List<Paragraph>(FlowDocumentPlainText.EnumerateParagraphs(afterDocument));
        if (targetParagraphIndex >= paragraphs.Count)
        {
            return false;
        }

        var paragraph = paragraphs[targetParagraphIndex];
        if (removeExistingFormat)
        {
            RemoveInlineSpanFromCollection<TSpan>(paragraph.Inlines);
        }
        else
        {
            WrapParagraphInlines(paragraph, spanFactory);
        }

        if (paragraph.Inlines.Count == 0)
        {
            paragraph.Inlines.Add(new Run(string.Empty));
        }

        return true;
    }

    private static bool SelectionIsFullyStyledBy<TSpan>(FlowDocument document)
        where TSpan : Span
    {
        var hasContent = false;
        var allStyled = true;
        for (var i = 0; i < document.Blocks.Count; i++)
        {
            EvaluateBlockStyleState<TSpan>(document.Blocks[i], inheritedStyled: false, ref hasContent, ref allStyled);
            if (!allStyled)
            {
                return false;
            }
        }

        return hasContent && allStyled;
    }

    private static void EvaluateBlockStyleState<TSpan>(Block block, bool inheritedStyled, ref bool hasContent, ref bool allStyled)
        where TSpan : Span
    {
        switch (block)
        {
            case Paragraph paragraph:
                for (var i = 0; i < paragraph.Inlines.Count; i++)
                {
                    EvaluateInlineStyleState<TSpan>(paragraph.Inlines[i], inheritedStyled, ref hasContent, ref allStyled);
                    if (!allStyled)
                    {
                        return;
                    }
                }

                break;
            case Section section:
                for (var i = 0; i < section.Blocks.Count; i++)
                {
                    EvaluateBlockStyleState<TSpan>(section.Blocks[i], inheritedStyled, ref hasContent, ref allStyled);
                    if (!allStyled)
                    {
                        return;
                    }
                }

                break;
            case InkkSlinger.List list:
                for (var i = 0; i < list.Items.Count; i++)
                {
                    var item = list.Items[i];
                    for (var j = 0; j < item.Blocks.Count; j++)
                    {
                        EvaluateBlockStyleState<TSpan>(item.Blocks[j], inheritedStyled, ref hasContent, ref allStyled);
                        if (!allStyled)
                        {
                            return;
                        }
                    }
                }

                break;
            case Table table:
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
                                EvaluateBlockStyleState<TSpan>(cell.Blocks[m], inheritedStyled, ref hasContent, ref allStyled);
                                if (!allStyled)
                                {
                                    return;
                                }
                            }
                        }
                    }
                }

                break;
        }
    }

    private static void EvaluateInlineStyleState<TSpan>(Inline inline, bool inheritedStyled, ref bool hasContent, ref bool allStyled)
        where TSpan : Span
    {
        var styled = inheritedStyled || inline is TSpan;
        switch (inline)
        {
            case Run run when run.Text.Length > 0:
                hasContent = true;
                if (!styled)
                {
                    allStyled = false;
                }

                break;
            case LineBreak:
            case InlineUIContainer:
                hasContent = true;
                if (!styled)
                {
                    allStyled = false;
                }

                break;
            case Span span:
                for (var i = 0; i < span.Inlines.Count; i++)
                {
                    EvaluateInlineStyleState<TSpan>(span.Inlines[i], styled, ref hasContent, ref allStyled);
                    if (!allStyled)
                    {
                        return;
                    }
                }

                break;
        }
    }

    private static void RemoveInlineSpanFromParagraphs<TSpan>(FlowDocument document)
        where TSpan : Span
    {
        for (var i = 0; i < document.Blocks.Count; i++)
        {
            RemoveInlineSpanFromBlock<TSpan>(document.Blocks[i]);
        }
    }

    private static void RemoveInlineSpanFromBlock<TSpan>(Block block)
        where TSpan : Span
    {
        switch (block)
        {
            case Paragraph paragraph:
                RemoveInlineSpanFromCollection<TSpan>(paragraph.Inlines);
                break;
            case Section section:
                for (var i = 0; i < section.Blocks.Count; i++)
                {
                    RemoveInlineSpanFromBlock<TSpan>(section.Blocks[i]);
                }

                break;
            case InkkSlinger.List list:
                for (var i = 0; i < list.Items.Count; i++)
                {
                    var item = list.Items[i];
                    for (var j = 0; j < item.Blocks.Count; j++)
                    {
                        RemoveInlineSpanFromBlock<TSpan>(item.Blocks[j]);
                    }
                }

                break;
            case Table table:
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
                                RemoveInlineSpanFromBlock<TSpan>(cell.Blocks[m]);
                            }
                        }
                    }
                }

                break;
        }
    }

    private static void RemoveInlineSpanFromCollection<TSpan>(IList<Inline> inlines)
        where TSpan : Span
    {
        for (var i = 0; i < inlines.Count;)
        {
            var inline = inlines[i];
            if (inline is TSpan target)
            {
                inlines.RemoveAt(i);
                var movedChildren = new List<Inline>();
                while (target.Inlines.Count > 0)
                {
                    var child = target.Inlines[0];
                    target.Inlines.RemoveAt(0);
                    movedChildren.Add(child);
                }

                RemoveInlineSpanFromCollection<TSpan>(movedChildren);
                for (var j = 0; j < movedChildren.Count; j++)
                {
                    inlines.Insert(i + j, movedChildren[j]);
                }

                i += movedChildren.Count;
                continue;
            }

            if (inline is Span span)
            {
                RemoveInlineSpanFromCollection<TSpan>(span.Inlines);
            }

            i++;
        }
    }

    private static void ApplyInlineSpanToParagraphs(FlowDocument document, Func<Span> spanFactory)
    {
        for (var i = 0; i < document.Blocks.Count; i++)
        {
            ApplyInlineSpanToBlock(document.Blocks[i], spanFactory);
        }
    }

    private static void ApplyInlineSpanToBlock(Block block, Func<Span> spanFactory)
    {
        switch (block)
        {
            case Paragraph paragraph:
                WrapParagraphInlines(paragraph, spanFactory);
                break;
            case Section section:
                for (var i = 0; i < section.Blocks.Count; i++)
                {
                    ApplyInlineSpanToBlock(section.Blocks[i], spanFactory);
                }

                break;
            case InkkSlinger.List list:
                for (var i = 0; i < list.Items.Count; i++)
                {
                    var item = list.Items[i];
                    for (var j = 0; j < item.Blocks.Count; j++)
                    {
                        ApplyInlineSpanToBlock(item.Blocks[j], spanFactory);
                    }
                }

                break;
            case Table table:
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
                                ApplyInlineSpanToBlock(cell.Blocks[m], spanFactory);
                            }
                        }
                    }
                }

                break;
        }
    }

    private static void WrapParagraphInlines(Paragraph paragraph, Func<Span> spanFactory)
    {
        if (paragraph.Inlines.Count == 0)
        {
            return;
        }

        var span = spanFactory();
        while (paragraph.Inlines.Count > 0)
        {
            var inline = paragraph.Inlines[0];
            paragraph.Inlines.RemoveAt(0);
            span.Inlines.Add(inline);
        }

        paragraph.Inlines.Add(span);
    }

    private void ReplaceSelection(string replacement, string commandType, GroupingPolicy policy)
    {
        if (ShouldGuardRichFallback(commandType))
        {
            if (TryReplaceSelectionWithinParagraphPreservingInlineStyles(replacement ?? string.Empty, commandType, policy))
            {
                RecordOperation("Guard", $"ReplaceSelection->{commandType}:ReroutedStylePreserving");
                return;
            }

            if (string.IsNullOrEmpty(replacement) &&
                SelectionLength > 0 &&
                TryDeleteSelectionPreservingStructure(commandType, policy))
            {
                RecordOperation("Guard", $"ReplaceSelection->{commandType}:ReroutedStructuredDelete");
                return;
            }

            if (!IsFullDocumentSelection())
            {
                RecordOperation("Guard", $"ReplaceSelection->{commandType}:BlockedInRichMode");
                RichTextBoxDiagnostics.ObserveRichFallbackBlocked(
                    "ReplaceSelection",
                    commandType,
                    SelectionStart,
                    SelectionLength,
                    (replacement ?? string.Empty).Length,
                    BuildRecentOperationLines());
                return;
            }
        }

        RecordOperation(
            "MutationRoute",
            $"ReplaceSelection cmd={commandType} policy={policy} start={SelectionStart} len={SelectionLength} insLen={(replacement ?? string.Empty).Length}");
        var editStart = Stopwatch.GetTimestamp();
        var undoDepthBefore = _undoManager.UndoDepth;
        var redoDepthBefore = _undoManager.RedoDepth;
        var textLengthBefore = GetText().Length;
        var start = SelectionStart;
        var length = SelectionLength;
        var caretBefore = _caretIndex;
        var selectionStartBefore = start;
        var selectionLengthBefore = length;
        var normalizedReplacement = replacement ?? string.Empty;
        var caretAfter = start + normalizedReplacement.Length;
        var session = new DocumentEditSession(Document, _undoManager);
        session.BeginTransaction(
            commandType,
            policy,
            new DocumentEditContext(
                caretBefore,
                caretAfter,
                selectionStartBefore,
                selectionLengthBefore,
                caretAfter,
                0,
                commandType));
        DocumentEditing.ReplaceTextRange(Document, start, length, normalizedReplacement, session);
        session.CommitTransaction();
        _caretIndex = start + normalizedReplacement.Length;
        _selectionAnchor = _caretIndex;
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        RecordEditSample(elapsedMs);
        RichTextBoxDiagnostics.ObserveEdit(commandType, elapsedMs, start, length, _caretIndex);
        RichTextBoxDiagnostics.ObserveCommandTrace(
            "EditMethod",
            commandType,
            canExecute: true,
            handled: true,
            GetText().Length - textLengthBefore,
            undoDepthBefore,
            _undoManager.UndoDepth,
            redoDepthBefore,
            _undoManager.RedoDepth);
        TraceInvariants(commandType);
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateMeasure();
        InvalidateVisualWithReason($"Edit:{commandType}");
    }

    private void InsertTypingFormattedText(string text, string commandType)
    {
        var fragment = CreateTypingFragment(text);
        ReplaceSelectionWithFragment(fragment, commandType, GroupingPolicy.TypingBurst);
    }

    private bool TryInsertTypingFormattedTextWithinParagraph(string text, string commandType, GroupingPolicy policy)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('\n') >= 0)
        {
            return false;
        }

        var start = SelectionStart;
        var length = SelectionLength;
        var end = start + length;
        var entries = CollectParagraphEntries(Document);
        if (entries.Count == 0)
        {
            return false;
        }

        var paragraphIndex = -1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (start >= entries[i].StartOffset && end <= entries[i].EndOffset)
            {
                paragraphIndex = i;
                break;
            }
        }

        if (paragraphIndex < 0)
        {
            return false;
        }

        if (entries[paragraphIndex].Paragraph.Parent is FlowDocument &&
            !DocumentContainsRichInlineFormatting(Document))
        {
            return false;
        }

        var paragraphText = FlowDocumentPlainText.GetInlineText(entries[paragraphIndex].Paragraph.Inlines);
        var localStart = start - entries[paragraphIndex].StartOffset;
        if (localStart < 0 || localStart > paragraphText.Length)
        {
            return false;
        }

        var localLength = Math.Clamp(length, 0, paragraphText.Length - localStart);
        var beforeText = paragraphText[..localStart];
        var afterText = paragraphText[(localStart + localLength)..];

        var editStart = Stopwatch.GetTimestamp();
        var undoDepthBefore = _undoManager.UndoDepth;
        var redoDepthBefore = _undoManager.RedoDepth;
        var textLengthBefore = GetText().Length;
        var beforeDocument = DocumentEditing.CloneDocument(Document);
        var beforeRawText = GetText();
        var afterDocument = DocumentEditing.CloneDocument(Document);
        var paragraphs = new List<Paragraph>(FlowDocumentPlainText.EnumerateParagraphs(afterDocument));
        if (paragraphIndex >= paragraphs.Count)
        {
            return false;
        }

        var paragraph = paragraphs[paragraphIndex];
        paragraph.Inlines.Clear();
        if (beforeText.Length > 0)
        {
            paragraph.Inlines.Add(new Run(beforeText));
        }

        Inline typed = new Run(text);
        if (_typingUnderlineActive)
        {
            var underline = new Underline();
            underline.Inlines.Add(typed);
            typed = underline;
        }

        if (_typingItalicActive)
        {
            var italic = new Italic();
            italic.Inlines.Add(typed);
            typed = italic;
        }

        if (_typingBoldActive)
        {
            var bold = new Bold();
            bold.Inlines.Add(typed);
            typed = bold;
        }

        paragraph.Inlines.Add(typed);
        if (afterText.Length > 0)
        {
            paragraph.Inlines.Add(new Run(afterText));
        }

        if (paragraph.Inlines.Count == 0)
        {
            paragraph.Inlines.Add(new Run(string.Empty));
        }

        var afterRawText = DocumentEditing.GetText(afterDocument);
        var caretAfter = start + text.Length;
        RecordOperation(
            "MutationRoute",
            $"InsertTextStyledStructured cmd={commandType} policy={policy} start={start} len={length} insLen={text.Length}");
        var session = new DocumentEditSession(Document, _undoManager);
        session.BeginTransaction(
            commandType,
            policy,
            new DocumentEditContext(
                _caretIndex,
                caretAfter,
                start,
                length,
                caretAfter,
                0,
                commandType));
        session.ApplyOperation(new ReplaceDocumentOperation("Document", beforeRawText, afterRawText, beforeDocument, afterDocument));
        session.CommitTransaction();
        _caretIndex = caretAfter;
        _selectionAnchor = _caretIndex;
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        RecordEditSample(elapsedMs);
        RichTextBoxDiagnostics.ObserveEdit(commandType, elapsedMs, start, length, _caretIndex);
        RichTextBoxDiagnostics.ObserveCommandTrace(
            "EditMethod",
            commandType,
            canExecute: true,
            handled: true,
            GetText().Length - textLengthBefore,
            undoDepthBefore,
            _undoManager.UndoDepth,
            redoDepthBefore,
            _undoManager.RedoDepth);
        TraceInvariants(commandType);
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateMeasure();
        InvalidateVisualWithReason($"Edit:{commandType}");
        return true;
    }

    private FlowDocument CreateTypingFragment(string text)
    {
        var fragment = new FlowDocument();
        var paragraph = new Paragraph();
        Inline inline = new Run(text ?? string.Empty);
        if (_typingUnderlineActive)
        {
            var underline = new Underline();
            underline.Inlines.Add(inline);
            inline = underline;
        }

        if (_typingItalicActive)
        {
            var italic = new Italic();
            italic.Inlines.Add(inline);
            inline = italic;
        }

        if (_typingBoldActive)
        {
            var bold = new Bold();
            bold.Inlines.Add(inline);
            inline = bold;
        }

        paragraph.Inlines.Add(inline);
        fragment.Blocks.Add(paragraph);
        return fragment;
    }

    private static bool DocumentContainsRichInlineFormatting(FlowDocument document)
    {
        for (var i = 0; i < document.Blocks.Count; i++)
        {
            if (!BlockIsPlainTextCompatible(document.Blocks[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool BlockIsPlainTextCompatible(Block block)
    {
        switch (block)
        {
            case Paragraph paragraph:
                return IsParagraphPlainTextCompatible(paragraph);
            default:
                return false;
        }
    }

    private static bool IsParagraphPlainTextCompatible(Paragraph paragraph)
    {
        for (var i = 0; i < paragraph.Inlines.Count; i++)
        {
            if (!InlineIsPlainTextCompatible(paragraph.Inlines[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool InlineIsPlainTextCompatible(Inline inline)
    {
        return inline switch
        {
            Run run => !run.Foreground.HasValue,
            LineBreak => true,
            InlineUIContainer => true,
            _ => false
        };
    }

    private void SetCaret(int index, bool extendSelection)
    {
        _caretIndex = Math.Clamp(index, 0, GetText().Length);
        if (!extendSelection)
        {
            _selectionAnchor = _caretIndex;
        }

        EnsureCaretVisible();
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
        EnsureCaretVisible();
    }

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

        RichTextBoxDiagnostics.ObserveInvariant(
            stage,
            isValid: issues.Count == 0,
            issues.Count == 0
                ? $"ok blocks={Document.Blocks.Count} textLen={textLength}"
                : string.Join(" | ", issues));
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
            RichTextBoxDiagnostics.ObserveStructureTransition(
                stage,
                _lastDocumentRichness.ToSummary(),
                current.ToSummary(),
                BuildRecentOperationLines());
        }

        if (_lastDocumentRichness.HasRichStructure &&
            !current.HasRichStructure &&
            current.IsPlainTextCompatible)
        {
            RichTextBoxDiagnostics.ObserveFlattening(
                stage,
                _lastDocumentRichness.ToSummary(),
                current.ToSummary(),
                BuildRecentOperationLines());
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
        foreach (var block in document.Blocks)
        {
            AccumulateBlockRichness(block, ref blocks, ref lists, ref tables, ref sections, ref richInlines);
        }

        return new DocumentRichnessSnapshot(
            blocks,
            lists,
            tables,
            sections,
            richInlines,
            HasRichStructure: lists > 0 || tables > 0 || sections > 0 || richInlines > 0,
            IsPlainTextCompatible: !DocumentContainsRichInlineFormatting(document));
    }

    private static void AccumulateBlockRichness(
        Block block,
        ref int blocks,
        ref int lists,
        ref int tables,
        ref int sections,
        ref int richInlines)
    {
        blocks++;
        switch (block)
        {
            case Paragraph paragraph:
                for (var i = 0; i < paragraph.Inlines.Count; i++)
                {
                    AccumulateInlineRichness(paragraph.Inlines[i], ref richInlines);
                }

                break;
            case InkkSlinger.List list:
                lists++;
                for (var i = 0; i < list.Items.Count; i++)
                {
                    var item = list.Items[i];
                    for (var j = 0; j < item.Blocks.Count; j++)
                    {
                        AccumulateBlockRichness(item.Blocks[j], ref blocks, ref lists, ref tables, ref sections, ref richInlines);
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
                                AccumulateBlockRichness(cell.Blocks[m], ref blocks, ref lists, ref tables, ref sections, ref richInlines);
                            }
                        }
                    }
                }

                break;
            case Section section:
                sections++;
                for (var i = 0; i < section.Blocks.Count; i++)
                {
                    AccumulateBlockRichness(section.Blocks[i], ref blocks, ref lists, ref tables, ref sections, ref richInlines);
                }

                break;
        }
    }

    private static void AccumulateInlineRichness(Inline inline, ref int richInlines)
    {
        switch (inline)
        {
            case Bold bold:
                richInlines++;
                for (var i = 0; i < bold.Inlines.Count; i++)
                {
                    AccumulateInlineRichness(bold.Inlines[i], ref richInlines);
                }

                break;
            case Italic italic:
                richInlines++;
                for (var i = 0; i < italic.Inlines.Count; i++)
                {
                    AccumulateInlineRichness(italic.Inlines[i], ref richInlines);
                }

                break;
            case Underline underline:
                richInlines++;
                for (var i = 0; i < underline.Inlines.Count; i++)
                {
                    AccumulateInlineRichness(underline.Inlines[i], ref richInlines);
                }

                break;
            case Hyperlink hyperlink:
                richInlines++;
                for (var i = 0; i < hyperlink.Inlines.Count; i++)
                {
                    AccumulateInlineRichness(hyperlink.Inlines[i], ref richInlines);
                }

                break;
            case Span span:
                richInlines++;
                for (var i = 0; i < span.Inlines.Count; i++)
                {
                    AccumulateInlineRichness(span.Inlines[i], ref richInlines);
                }

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

    private int GetTextIndexFromPoint(Vector2 point)
    {
        var textRect = GetTextRect();
        var layout = BuildOrGetLayout(textRect.Width);
        ClampScrollOffsets(layout, textRect);
        var hit = layout.HitTestOffset(new Vector2(
            (point.X - textRect.X) + _horizontalOffset,
            (point.Y - textRect.Y) + _verticalOffset));
        _lastSelectionHitTestOffset = hit;
        return hit;
    }

    private void DrawCaret(SpriteBatch spriteBatch, LayoutRect textRect, DocumentLayoutResult layout)
    {
        if (!layout.TryGetCaretPosition(_caretIndex, out var caret))
        {
            return;
        }

        var lineHeight = Math.Max(1f, FontStashTextRenderer.GetLineHeight(Font));
        var caretRect = new LayoutRect(textRect.X + caret.X - _horizontalOffset, textRect.Y + caret.Y - _verticalOffset, 1f, lineHeight);
        UiDrawing.DrawFilledRect(
            spriteBatch,
            caretRect,
            CaretBrush * Opacity);
        var line = ResolveLineForOffset(layout, _caretIndex);
        RichTextBoxDiagnostics.ObserveSelection(
            _caretIndex,
            _selectionAnchor,
            SelectionStart,
            SelectionLength,
            line.Index,
            _lastSelectionHitTestOffset,
            caretRect,
            selectionRectCount: 0);
    }

    private void DrawSelection(SpriteBatch spriteBatch, LayoutRect textRect, DocumentLayoutResult layout)
    {
        if (SelectionLength <= 0)
        {
            return;
        }

        var selectionStartTicks = Stopwatch.GetTimestamp();
        var rects = layout.BuildSelectionRects(SelectionStart, SelectionLength);
        for (var i = 0; i < rects.Count; i++)
        {
            var rect = rects[i];
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(textRect.X + rect.X - _horizontalOffset, textRect.Y + rect.Y - _verticalOffset, rect.Width, rect.Height),
                SelectionBrush * Opacity);
        }

        var line = ResolveLineForOffset(layout, _caretIndex);
        var caretRect = layout.TryGetCaretPosition(_caretIndex, out var caret)
            ? new LayoutRect(textRect.X + caret.X - _horizontalOffset, textRect.Y + caret.Y - _verticalOffset, 1f, Math.Max(1f, FontStashTextRenderer.GetLineHeight(Font)))
            : default;
        RichTextBoxDiagnostics.ObserveSelection(
            _caretIndex,
            _selectionAnchor,
            SelectionStart,
            SelectionLength,
            line.Index,
            _lastSelectionHitTestOffset,
            caretRect,
            rects.Count);
        RecordSelectionGeometrySample(Stopwatch.GetElapsedTime(selectionStartTicks).TotalMilliseconds);
    }

    private static string NormalizeNewlines(string? text)
    {
        return (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private static string FilterCompositionText(string text)
    {
        var filtered = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (!char.IsControl(text[i]))
            {
                filtered.Append(text[i]);
            }
        }

        return filtered.ToString();
    }

    private static void PublishRichClipboardPayloads(string richSlice, string selectedText)
    {
        TextClipboard.SetData(FlowDocumentSerializer.ClipboardFormat, richSlice);
        TextClipboard.SetData(ClipboardXamlFormat, richSlice);
        TextClipboard.SetData(ClipboardXamlPackageFormat, richSlice);
        TextClipboard.SetData(ClipboardRtfFormat, BuildRtfFromPlainText(selectedText));
    }

    private static bool TryGetRichClipboardPayload(out string payload, out string format)
    {
        if (TryGetNonEmptyClipboardData(ClipboardXamlPackageFormat, out payload))
        {
            format = ClipboardXamlPackageFormat;
            return true;
        }

        if (TryGetNonEmptyClipboardData(ClipboardXamlFormat, out payload))
        {
            format = ClipboardXamlFormat;
            return true;
        }

        if (TryGetNonEmptyClipboardData(FlowDocumentSerializer.ClipboardFormat, out payload))
        {
            format = FlowDocumentSerializer.ClipboardFormat;
            return true;
        }

        payload = string.Empty;
        format = string.Empty;
        return false;
    }

    private static bool TryGetNonEmptyClipboardData(string format, out string value)
    {
        if (TextClipboard.TryGetData<string>(format, out var payload) &&
            !string.IsNullOrWhiteSpace(payload))
        {
            value = payload;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static string BuildRtfFromPlainText(string text)
    {
        var normalized = NormalizeNewlines(text);
        var sb = new StringBuilder();
        sb.Append(@"{\rtf1\ansi ");
        for (var i = 0; i < normalized.Length; i++)
        {
            var ch = normalized[i];
            switch (ch)
            {
                case '\\':
                    sb.Append(@"\\");
                    break;
                case '{':
                    sb.Append(@"\{");
                    break;
                case '}':
                    sb.Append(@"\}");
                    break;
                case '\n':
                    sb.Append(@"\par ");
                    break;
                default:
                    if (ch <= 0x7f)
                    {
                        sb.Append(ch);
                    }
                    else
                    {
                        sb.Append(@"\u");
                        sb.Append((short)ch);
                        sb.Append('?');
                    }

                    break;
            }
        }

        sb.Append('}');
        return sb.ToString();
    }

    private bool CanPasteFromClipboard()
    {
        if (TryGetRichClipboardPayload(out _, out _))
        {
            return true;
        }

        return TextClipboard.TryGetText(out _);
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

    private void ReplaceSelectionWithFragment(FlowDocument fragment, string commandType, GroupingPolicy policy)
    {
        if (IsRichStructuredDocument() &&
            RichGuardedFragmentCommands.Contains(commandType) &&
            !IsFullDocumentSelection())
        {
            RecordOperation("Guard", $"ReplaceSelectionWithFragment->{commandType}:BlockedInRichMode");
            RichTextBoxDiagnostics.ObserveRichFallbackBlocked(
                "ReplaceSelectionWithFragment",
                commandType,
                SelectionStart,
                SelectionLength,
                DocumentEditing.GetText(fragment).Length,
                BuildRecentOperationLines());
            return;
        }

        RecordOperation(
            "MutationRoute",
            $"ReplaceSelectionWithFragment cmd={commandType} policy={policy} start={SelectionStart} len={SelectionLength} fragBlocks={fragment.Blocks.Count}");
        var editStart = Stopwatch.GetTimestamp();
        var undoDepthBefore = _undoManager.UndoDepth;
        var redoDepthBefore = _undoManager.RedoDepth;
        var textLengthBefore = GetText().Length;
        var selectionStart = SelectionStart;
        var selectionLength = SelectionLength;
        var beforeDocument = DocumentEditing.CloneDocument(Document);
        var beforeText = GetText();
        var insertedText = DocumentEditing.GetText(fragment);
        var afterDocument = BuildDocumentWithFragment(Document, fragment, selectionStart, selectionLength);
        var afterText = DocumentEditing.GetText(afterDocument);
        var caretAfter = selectionStart + insertedText.Length;

        var session = new DocumentEditSession(Document, _undoManager);
        session.BeginTransaction(
            commandType,
            policy,
            new DocumentEditContext(
                _caretIndex,
                caretAfter,
                selectionStart,
                selectionLength,
                caretAfter,
                0,
                commandType));
        session.ApplyOperation(new ReplaceDocumentOperation("Document", beforeText, afterText, beforeDocument, afterDocument));
        session.CommitTransaction();

        _caretIndex = caretAfter;
        _selectionAnchor = _caretIndex;
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        RecordEditSample(elapsedMs);
        RichTextBoxDiagnostics.ObserveEdit(commandType, elapsedMs, selectionStart, selectionLength, _caretIndex);
        RichTextBoxDiagnostics.ObserveCommandTrace(
            "EditMethod",
            commandType,
            canExecute: true,
            handled: true,
            GetText().Length - textLengthBefore,
            undoDepthBefore,
            _undoManager.UndoDepth,
            redoDepthBefore,
            _undoManager.RedoDepth);
        TraceInvariants(commandType);
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateMeasure();
        InvalidateVisualWithReason($"Edit:{commandType}");
    }

    private static FlowDocument BuildDocumentWithFragment(FlowDocument current, FlowDocument fragment, int selectionStart, int selectionLength)
    {
        var text = DocumentEditing.GetText(current);
        var clampedStart = Math.Clamp(selectionStart, 0, text.Length);
        var clampedLength = Math.Clamp(selectionLength, 0, text.Length - clampedStart);
        var clampedEnd = clampedStart + clampedLength;
        var prefix = SliceDocumentRange(current, 0, clampedStart);
        var suffix = SliceDocumentRange(current, clampedEnd, text.Length);
        var fragmentClone = DocumentEditing.CloneDocument(fragment);
        var isSingleParagraphInsert = clampedLength == 0 &&
                                      fragmentClone.Blocks.Count == 1 &&
                                      fragmentClone.Blocks[0] is Paragraph;

        var result = new FlowDocument();
        AppendDocumentBlocks(result, prefix, mergeParagraphBoundary: false);
        AppendDocumentBlocks(
            result,
            fragmentClone,
            mergeParagraphBoundary: !IsAtParagraphStart(current, clampedStart));
        AppendDocumentBlocks(
            result,
            suffix,
            mergeParagraphBoundary: (!IsAtParagraphStart(current, clampedEnd) &&
                                     !IsAtParagraphSeparator(current, clampedEnd)) ||
                                    isSingleParagraphInsert);

        if (result.Blocks.Count == 0)
        {
            result.Blocks.Add(CreateParagraph(string.Empty));
        }

        return result;
    }

    private static FlowDocument SliceDocumentRange(FlowDocument source, int startOffset, int endOffset)
    {
        if (endOffset <= startOffset)
        {
            return new FlowDocument();
        }

        var xml = FlowDocumentSerializer.SerializeRange(source, startOffset, endOffset);
        return FlowDocumentSerializer.DeserializeFragment(xml);
    }

    private static void AppendDocumentBlocks(FlowDocument target, FlowDocument source, bool mergeParagraphBoundary)
    {
        if (mergeParagraphBoundary &&
            target.Blocks.Count > 0 &&
            source.Blocks.Count > 0 &&
            target.Blocks[target.Blocks.Count - 1] is Paragraph destinationParagraph &&
            source.Blocks[0] is Paragraph sourceParagraph)
        {
            while (sourceParagraph.Inlines.Count > 0)
            {
                var inline = sourceParagraph.Inlines[0];
                sourceParagraph.Inlines.RemoveAt(0);
                destinationParagraph.Inlines.Add(inline);
            }

            if (destinationParagraph.Inlines.Count == 0)
            {
                destinationParagraph.Inlines.Add(new Run(string.Empty));
            }

            source.Blocks.RemoveAt(0);
        }

        while (source.Blocks.Count > 0)
        {
            var block = source.Blocks[0];
            source.Blocks.RemoveAt(0);
            target.Blocks.Add(block);
        }
    }

    private static bool IsAtParagraphStart(FlowDocument document, int offset)
    {
        if (offset <= 0)
        {
            return true;
        }

        var entries = CollectParagraphEntries(document);
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].StartOffset == offset)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAtParagraphSeparator(FlowDocument document, int offset)
    {
        if (offset < 0)
        {
            return false;
        }

        var entries = CollectParagraphEntries(document);
        for (var i = 0; i < entries.Count - 1; i++)
        {
            if (entries[i].EndOffset == offset)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsRichStructuredDocument()
    {
        return DocumentContainsRichInlineFormatting(Document);
    }

    private bool ShouldGuardRichFallback(string commandType)
    {
        return IsRichStructuredDocument() && RichGuardedReplaceSelectionCommands.Contains(commandType);
    }

    private bool IsFullDocumentSelection()
    {
        var textLength = GetText().Length;
        return SelectionStart == 0 && SelectionLength >= textLength;
    }

    private static Paragraph CreateParagraph(string text)
    {
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run(text ?? string.Empty));
        return paragraph;
    }

    private void RecordLayoutBuildSample(double elapsedMs)
    {
        var bounded = Math.Max(0d, elapsedMs);
        _perfLayoutBuildSampleCount++;
        _perfLayoutBuildTotalMs += bounded;
        _perfLayoutBuildMaxMs = Math.Max(_perfLayoutBuildMaxMs, bounded);
        AppendSample(_perfLayoutBuildSamplesMs, bounded);
    }

    private void RecordRenderSample(double elapsedMs)
    {
        var bounded = Math.Max(0d, elapsedMs);
        _perfRenderSampleCount++;
        _perfRenderTotalMs += bounded;
        _perfRenderLastMs = bounded;
        _perfRenderMaxMs = Math.Max(_perfRenderMaxMs, bounded);
    }

    private void RecordSelectionGeometrySample(double elapsedMs)
    {
        var bounded = Math.Max(0d, elapsedMs);
        _perfSelectionGeometrySampleCount++;
        _perfSelectionGeometryTotalMs += bounded;
        _perfSelectionGeometryLastMs = bounded;
        _perfSelectionGeometryMaxMs = Math.Max(_perfSelectionGeometryMaxMs, bounded);
    }

    private void RecordClipboardSerializeSample(double elapsedMs)
    {
        var bounded = Math.Max(0d, elapsedMs);
        _perfClipboardSerializeSampleCount++;
        _perfClipboardSerializeTotalMs += bounded;
        _perfClipboardSerializeLastMs = bounded;
        _perfClipboardSerializeMaxMs = Math.Max(_perfClipboardSerializeMaxMs, bounded);
    }

    private void RecordClipboardDeserializeSample(double elapsedMs)
    {
        var bounded = Math.Max(0d, elapsedMs);
        _perfClipboardDeserializeSampleCount++;
        _perfClipboardDeserializeTotalMs += bounded;
        _perfClipboardDeserializeLastMs = bounded;
        _perfClipboardDeserializeMaxMs = Math.Max(_perfClipboardDeserializeMaxMs, bounded);
    }

    private void RecordEditSample(double elapsedMs)
    {
        var bounded = Math.Max(0d, elapsedMs);
        _perfEditSampleCount++;
        _perfEditTotalMs += bounded;
        _perfEditLastMs = bounded;
        _perfEditMaxMs = Math.Max(_perfEditMaxMs, bounded);
    }

    private static void AppendSample(List<double> samples, double value)
    {
        if (samples.Count >= PerfSampleCap)
        {
            samples.RemoveAt(0);
        }

        samples.Add(value);
    }

    private static double Average(double total, int count)
    {
        if (count <= 0)
        {
            return 0d;
        }

        return total / count;
    }

    private static double Percentile(List<double> samples, double percentile)
    {
        if (samples.Count == 0)
        {
            return 0d;
        }

        var ordered = samples.ToArray();
        Array.Sort(ordered);
        var rawIndex = (ordered.Length - 1) * percentile;
        var lower = (int)Math.Floor(rawIndex);
        var upper = (int)Math.Ceiling(rawIndex);
        if (lower == upper)
        {
            return ordered[lower];
        }

        var fraction = rawIndex - lower;
        return ordered[lower] + ((ordered[upper] - ordered[lower]) * fraction);
    }

    private void ApplyStructuralEdit(
        string reason,
        GroupingPolicy policy,
        Func<FlowDocument, int, int, int, bool> applyMutation,
        Action<FlowDocument, int, int, int>? postApply = null)
    {
        var editStart = Stopwatch.GetTimestamp();
        var undoDepthBefore = _undoManager.UndoDepth;
        var redoDepthBefore = _undoManager.RedoDepth;
        var textLengthBefore = GetText().Length;
        var selectionStart = SelectionStart;
        var selectionLength = SelectionLength;
        var beforeDocument = DocumentEditing.CloneDocument(Document);
        var beforeText = GetText();
        var afterDocument = DocumentEditing.CloneDocument(Document);
        if (!applyMutation(afterDocument, selectionStart, selectionLength, _caretIndex))
        {
            return;
        }

        var afterText = DocumentEditing.GetText(afterDocument);
        var session = new DocumentEditSession(Document, _undoManager);
        session.BeginTransaction(
            reason,
            policy,
            new DocumentEditContext(
                _caretIndex,
                _caretIndex,
                selectionStart,
                selectionLength,
                selectionStart,
                0,
                reason));
        session.ApplyOperation(new ReplaceDocumentOperation("Document", beforeText, afterText, beforeDocument, afterDocument));
        session.CommitTransaction();

        postApply?.Invoke(Document, selectionStart, selectionLength, _caretIndex);
        _selectionAnchor = _caretIndex;
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        RecordEditSample(elapsedMs);
        RichTextBoxDiagnostics.ObserveEdit(reason, elapsedMs, selectionStart, selectionLength, _caretIndex);
        RichTextBoxDiagnostics.ObserveCommandTrace(
            "EditMethod",
            reason,
            canExecute: true,
            handled: true,
            GetText().Length - textLengthBefore,
            undoDepthBefore,
            _undoManager.UndoDepth,
            redoDepthBefore,
            _undoManager.RedoDepth);
        TraceInvariants(reason);
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateMeasure();
        InvalidateVisualWithReason($"Edit:{reason}");
    }

    private static List<ParagraphSelectionEntry> ResolveSelectedParagraphs(FlowDocument document, int selectionStart, int selectionLength, int caretOffset)
    {
        var entries = CollectParagraphEntries(document);
        if (entries.Count == 0)
        {
            return entries;
        }

        if (selectionLength <= 0)
        {
            var point = Math.Clamp(caretOffset, 0, Math.Max(0, entries[entries.Count - 1].EndOffset));
            for (var i = 0; i < entries.Count; i++)
            {
                if (point >= entries[i].StartOffset && point <= entries[i].EndOffset)
                {
                    return [entries[i]];
                }
            }

            return [entries[entries.Count - 1]];
        }

        var start = Math.Clamp(selectionStart, 0, int.MaxValue);
        var end = start + Math.Max(0, selectionLength);
        var selected = new List<ParagraphSelectionEntry>();
        for (var i = 0; i < entries.Count; i++)
        {
            var overlaps = entries[i].EndOffset > start && entries[i].StartOffset < end;
            if (overlaps)
            {
                selected.Add(entries[i]);
            }
        }

        if (selected.Count == 0)
        {
            selected.Add(entries[0]);
        }

        return selected;
    }

    private static List<ParagraphSelectionEntry> CollectParagraphEntries(FlowDocument document)
    {
        var entries = new List<ParagraphSelectionEntry>();
        var runningOffset = 0;
        var paragraphs = new List<Paragraph>(FlowDocumentPlainText.EnumerateParagraphs(document));
        for (var i = 0; i < paragraphs.Count; i++)
        {
            var paragraph = paragraphs[i];
            var length = GetParagraphLogicalLength(paragraph);
            entries.Add(new ParagraphSelectionEntry(paragraph, runningOffset, runningOffset + length));
            runningOffset += length;
            if (i < paragraphs.Count - 1)
            {
                runningOffset += 1;
            }
        }

        return entries;
    }

    private static int GetParagraphLogicalLength(Paragraph paragraph)
    {
        var length = 0;
        foreach (var inline in paragraph.Inlines)
        {
            length += GetInlineLogicalLength(inline);
        }

        return length;
    }

    private static int GetInlineLogicalLength(Inline inline)
    {
        switch (inline)
        {
            case Run run:
                return run.Text.Length;
            case LineBreak:
                return 1;
            case Span span:
            {
                var total = 0;
                foreach (var nested in span.Inlines)
                {
                    total += GetInlineLogicalLength(nested);
                }

                return total;
            }
            case InlineUIContainer:
                return 1;
            default:
                return 0;
        }
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

    private static FlowDocument BuildDocumentWithFragment(FlowDocument current, Table table, int selectionStart, int selectionLength)
    {
        var fragment = new FlowDocument();
        fragment.Blocks.Add(table);
        return BuildDocumentWithFragment(current, fragment, selectionStart, selectionLength);
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

    private static bool TryGetAncestor<T>(TextElement element, out T ancestor)
        where T : TextElement
    {
        for (var current = element.Parent; current != null; current = current.Parent)
        {
            if (current is T typed)
            {
                ancestor = typed;
                return true;
            }
        }

        ancestor = null!;
        return false;
    }

    private bool TryActivateHyperlinkAtSelection()
    {
        var offset = SelectionLength > 0 ? SelectionStart : _caretIndex;
        var uri = ResolveHyperlinkUriAtOffset(offset);
        if (string.IsNullOrWhiteSpace(uri))
        {
            return false;
        }

        RaiseHyperlinkNavigate(uri);
        return true;
    }

    private void RaiseHyperlinkNavigate(string uri)
    {
        var args = new HyperlinkNavigateRoutedEventArgs(HyperlinkNavigateEvent, uri);
        RaiseRoutedEventInternal(HyperlinkNavigateEvent, args);
    }

    private string? ResolveHyperlinkUriAtOffset(int offset)
    {
        var paragraphs = CollectParagraphEntries(Document);
        for (var i = 0; i < paragraphs.Count; i++)
        {
            if (offset < paragraphs[i].StartOffset || offset > paragraphs[i].EndOffset)
            {
                continue;
            }

            var localOffset = Math.Clamp(offset - paragraphs[i].StartOffset, 0, Math.Max(0, paragraphs[i].EndOffset - paragraphs[i].StartOffset));
            return ResolveHyperlinkUriWithinInlines(paragraphs[i].Paragraph.Inlines, localOffset);
        }

        return null;
    }

    private static string? ResolveHyperlinkUriWithinInlines(IEnumerable<Inline> inlines, int localOffset)
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
                !string.IsNullOrWhiteSpace(hyperlink.NavigateUri))
            {
                return hyperlink.NavigateUri;
            }

            if (inline is Span span)
            {
                var nested = ResolveHyperlinkUriWithinInlines(span.Inlines, Math.Max(0, localOffset - cursor));
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }

            cursor = end;
        }

        return null;
    }

    private readonly record struct ParagraphSelectionEntry(Paragraph Paragraph, int StartOffset, int EndOffset);

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
        bool HasRichStructure,
        bool IsPlainTextCompatible)
    {
        public static readonly DocumentRichnessSnapshot Empty = new(0, 0, 0, 0, 0, false, true);

        public string ToSummary()
        {
            return $"blocks={BlockCount},lists={ListCount},tables={TableCount},sections={SectionCount},richInlines={RichInlineCount},plain={IsPlainTextCompatible}";
        }
    }

    private readonly record struct TableCellSelectionInfo(
        TableRow Row,
        TableCell Cell,
        int CellIndex,
        int StartOffset,
        int EndOffset);

    private DocumentLayoutResult BuildOrGetLayout(float availableWidth)
    {
        var layoutLookupStart = Stopwatch.GetTimestamp();
        var normalizedWidth = TextWrapping == TextWrapping.NoWrap || availableWidth <= 0f
            ? float.PositiveInfinity
            : availableWidth;
        var text = GetText();
        var signature = HashCode.Combine(
            RuntimeHelpers.GetHashCode(Document),
            StringComparer.Ordinal.GetHashCode(text),
            Font is null ? 0 : RuntimeHelpers.GetHashCode(Font),
            (int)TextWrapping,
            (int)MathF.Round(normalizedWidth * 100f));
        var lineHeight = Math.Max(1f, FontStashTextRenderer.GetLineHeight(Font));
        var key = new DocumentViewportLayoutCache.CacheKey(
            signature,
            normalizedWidth,
            TextWrapping,
            Font is null ? 0 : RuntimeHelpers.GetHashCode(Font),
            lineHeight,
            Foreground);
        if (_layoutCache.TryGet(key, out var cached))
        {
            _perfLayoutCacheHitCount++;
            RichTextBoxDiagnostics.ObserveLayout(
                cacheHit: true,
                elapsedMs: Stopwatch.GetElapsedTime(layoutLookupStart).TotalMilliseconds,
                textLength: text.Length);
            return cached;
        }

        _perfLayoutCacheMissCount++;
        RichTextBoxDiagnostics.ObserveLayoutInvalidation(
            "LayoutCacheMiss",
            text.Length,
            _caretIndex,
            SelectionStart,
            SelectionLength);
        var buildStart = Stopwatch.GetTimestamp();
        var settings = new DocumentLayoutSettings(
            AvailableWidth: normalizedWidth,
            Font: Font,
            Wrapping: TextWrapping,
            Foreground: Foreground,
            LineHeight: lineHeight,
            ListIndent: lineHeight * 1.2f,
            ListMarkerGap: 4f,
            TableCellPadding: 4f,
            TableBorderThickness: 1f);
        var built = _layoutEngine.Layout(Document, settings);
        _layoutCache.Store(key, built);
        var buildMs = Stopwatch.GetElapsedTime(buildStart).TotalMilliseconds;
        RecordLayoutBuildSample(buildMs);
        RichTextBoxDiagnostics.ObserveLayout(
            cacheHit: false,
            elapsedMs: Stopwatch.GetElapsedTime(layoutLookupStart).TotalMilliseconds,
            textLength: text.Length);
        return built;
    }

    private Color ResolveRunColor(DocumentLayoutStyle style)
    {
        if (style.ForegroundOverride.HasValue)
        {
            return style.ForegroundOverride.Value;
        }

        if (style.IsHyperlink)
        {
            return new Color(117, 181, 255);
        }

        return Foreground;
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

    private void CaptureDirtyHint(DocumentLayoutResult current, LayoutRect textRect)
    {
        if (_lastRenderedLayout == null || _lastRenderedLayout.Lines.Count == 0 || current.Lines.Count == 0)
        {
            return;
        }

        var dirty = false;
        var minX = float.MaxValue;
        var minY = float.MaxValue;
        var maxX = float.MinValue;
        var maxY = float.MinValue;
        var lineCount = Math.Max(_lastRenderedLayout.Lines.Count, current.Lines.Count);
        for (var i = 0; i < lineCount; i++)
        {
            var hasOld = i < _lastRenderedLayout.Lines.Count;
            var hasNew = i < current.Lines.Count;
            if (hasOld && hasNew)
            {
                var oldLine = _lastRenderedLayout.Lines[i];
                var newLine = current.Lines[i];
                if (string.Equals(oldLine.Text, newLine.Text, StringComparison.Ordinal) &&
                    oldLine.StartOffset == newLine.StartOffset &&
                    Math.Abs(oldLine.Bounds.Y - newLine.Bounds.Y) < 0.01f)
                {
                    continue;
                }

                Include(oldLine.Bounds);
                Include(newLine.Bounds);
                continue;
            }

            if (hasOld)
            {
                Include(_lastRenderedLayout.Lines[i].Bounds);
            }

            if (hasNew)
            {
                Include(current.Lines[i].Bounds);
            }
        }

        if (!dirty)
        {
            return;
        }

        var local = new LayoutRect(minX, minY, Math.Max(1f, maxX - minX), Math.Max(1f, maxY - minY));
        _pendingRenderDirtyBoundsHint = new LayoutRect(
            textRect.X + local.X - _horizontalOffset,
            textRect.Y + local.Y - _verticalOffset,
            local.Width,
            local.Height);
        _hasPendingRenderDirtyBoundsHint = true;

        void Include(LayoutRect rect)
        {
            dirty = true;
            minX = Math.Min(minX, rect.X);
            minY = Math.Min(minY, rect.Y);
            maxX = Math.Max(maxX, rect.X + rect.Width);
            maxY = Math.Max(maxY, rect.Y + rect.Height);
        }
    }

    private LayoutRect GetTextRect()
    {
        return new LayoutRect(
            LayoutSlot.X + BorderThickness + Padding.Left,
            LayoutSlot.Y + BorderThickness + Padding.Top,
            Math.Max(0f, LayoutSlot.Width - (BorderThickness * 2f) - Padding.Horizontal),
            Math.Max(0f, LayoutSlot.Height - (BorderThickness * 2f) - Padding.Vertical));
    }

    private void MoveCaretByWord(bool moveLeft, bool extendSelection)
    {
        var text = GetText();
        var target = GetWordBoundary(text, _caretIndex, moveLeft);
        SetCaret(target, extendSelection);
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateVisual();
    }

    private void MoveCaretToLineBoundary(bool moveToLineStart, bool extendSelection)
    {
        var textRect = GetTextRect();
        var layout = BuildOrGetLayout(textRect.Width);
        var line = ResolveLineForOffset(layout, _caretIndex);
        var target = moveToLineStart ? line.StartOffset : line.StartOffset + line.Length;
        SetCaret(target, extendSelection);
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateVisual();
    }

    private void MoveCaretByLine(bool moveUp, bool extendSelection)
    {
        var textRect = GetTextRect();
        var layout = BuildOrGetLayout(textRect.Width);
        if (layout.Lines.Count == 0)
        {
            return;
        }

        var currentLine = ResolveLineForOffset(layout, _caretIndex);
        var targetLineIndex = moveUp
            ? Math.Max(0, currentLine.Index - 1)
            : Math.Min(layout.Lines.Count - 1, currentLine.Index + 1);
        if (targetLineIndex == currentLine.Index)
        {
            return;
        }

        var currentColumn = Math.Clamp(_caretIndex - currentLine.StartOffset, 0, currentLine.PrefixWidths.Length - 1);
        var desiredX = currentLine.TextStartX + currentLine.PrefixWidths[currentColumn];
        var targetLine = layout.Lines[targetLineIndex];
        var targetColumn = ResolveClosestColumnForX(targetLine, desiredX);
        var targetOffset = Math.Clamp(targetLine.StartOffset + targetColumn, 0, GetText().Length);
        SetCaret(targetOffset, extendSelection);
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        EnsureCaretVisible();
        InvalidateVisual();
    }

    private static int ResolveClosestColumnForX(DocumentLayoutLine line, float desiredX)
    {
        if (line.PrefixWidths.Length == 0)
        {
            return 0;
        }

        var bestColumn = 0;
        var bestDistance = float.MaxValue;
        for (var i = 0; i < line.PrefixWidths.Length; i++)
        {
            var x = line.TextStartX + line.PrefixWidths[i];
            var distance = MathF.Abs(desiredX - x);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestColumn = i;
            }
        }

        return bestColumn;
    }

    private static int GetWordBoundary(string text, int index, bool moveLeft)
    {
        var length = text.Length;
        var clamped = Math.Clamp(index, 0, length);
        if (moveLeft)
        {
            if (clamped <= 0)
            {
                return 0;
            }

            var i = clamped;
            if (char.IsWhiteSpace(text[i - 1]))
            {
                while (i > 0 && char.IsWhiteSpace(text[i - 1]))
                {
                    i--;
                }
            }
            else if (IsWordChar(text[i - 1]))
            {
                while (i > 0 && IsWordChar(text[i - 1]))
                {
                    i--;
                }
            }
            else
            {
                while (i > 0 && IsPunctuationChar(text[i - 1]))
                {
                    i--;
                }

                while (i > 0 && IsWordChar(text[i - 1]))
                {
                    i--;
                }
            }

            return i;
        }

        if (clamped >= length)
        {
            return length;
        }

        var j = clamped;
        if (char.IsWhiteSpace(text[j]))
        {
            while (j < length && char.IsWhiteSpace(text[j]))
            {
                j++;
            }
        }
        else if (IsWordChar(text[j]))
        {
            while (j < length && IsWordChar(text[j]))
            {
                j++;
            }

            while (j < length && IsPunctuationChar(text[j]))
            {
                j++;
            }
        }
        else
        {
            while (j < length && IsPunctuationChar(text[j]))
            {
                j++;
            }
        }

        return j;
    }

    private static bool IsWordChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_';
    }

    private static bool IsPunctuationChar(char c)
    {
        return !char.IsWhiteSpace(c) && !IsWordChar(c);
    }

    private DocumentLayoutLine ResolveLineForOffset(DocumentLayoutResult layout, int offset)
    {
        if (layout.Lines.Count == 0)
        {
            return new DocumentLayoutLine
            {
                Index = 0,
                StartOffset = 0,
                Length = 0,
                Text = string.Empty,
                TextStartX = 0f,
                Bounds = new LayoutRect(0f, 0f, 0f, FontStashTextRenderer.GetLineHeight(Font)),
                Runs = Array.Empty<DocumentLayoutRun>(),
                PrefixWidths = [0f]
            };
        }

        var clamped = Math.Clamp(offset, 0, layout.TextLength);
        for (var i = 0; i < layout.Lines.Count; i++)
        {
            var line = layout.Lines[i];
            var end = line.StartOffset + line.Length;
            if (clamped <= end)
            {
                return line;
            }
        }

        return layout.Lines[layout.Lines.Count - 1];
    }

    private void EnsureCaretVisible()
    {
        var textRect = GetTextRect();
        if (textRect.Width <= 0f || textRect.Height <= 0f)
        {
            return;
        }

        var layout = BuildOrGetLayout(textRect.Width);
        if (!layout.TryGetCaretPosition(_caretIndex, out var caret))
        {
            return;
        }

        var lineHeight = Math.Max(1f, FontStashTextRenderer.GetLineHeight(Font));
        var changed = false;
        var visibleX = caret.X - _horizontalOffset;
        if (visibleX < 0f)
        {
            _horizontalOffset = caret.X;
            changed = true;
        }
        else if (visibleX > Math.Max(0f, textRect.Width - 2f))
        {
            _horizontalOffset = Math.Max(0f, caret.X - textRect.Width + 2f);
            changed = true;
        }

        var visibleY = caret.Y - _verticalOffset;
        if (visibleY < 0f)
        {
            _verticalOffset = caret.Y;
            changed = true;
        }
        else if (visibleY + lineHeight > textRect.Height)
        {
            _verticalOffset = Math.Max(0f, caret.Y + lineHeight - textRect.Height);
            changed = true;
        }

        ClampScrollOffsets(layout, textRect);
        if (changed)
        {
            InvalidateVisualWithReason("CaretBlink");
        }
    }

    private void ClampScrollOffsets(DocumentLayoutResult layout, LayoutRect textRect)
    {
        var maxX = Math.Max(0f, layout.ContentWidth - textRect.Width);
        var maxY = Math.Max(0f, layout.ContentHeight - textRect.Height);
        _horizontalOffset = Math.Clamp(_horizontalOffset, 0f, maxX);
        _verticalOffset = Math.Clamp(_verticalOffset, 0f, maxY);
    }

    private void SelectWordAt(int index)
    {
        var text = GetText();
        if (text.Length == 0)
        {
            _selectionAnchor = 0;
            _caretIndex = 0;
            return;
        }

        var clamped = Math.Clamp(index, 0, Math.Max(0, text.Length - 1));
        if (char.IsWhiteSpace(text[clamped]))
        {
            if (text[clamped] == '\n')
            {
                _selectionAnchor = clamped;
                _caretIndex = clamped;
                return;
            }
            
            _selectionAnchor = clamped;
            _caretIndex = Math.Min(text.Length, clamped + 1);
            return;
        }

        var start = clamped;
        while (start > 0 && !char.IsWhiteSpace(text[start - 1]))
        {
            start--;
        }

        var end = clamped;
        while (end < text.Length && !char.IsWhiteSpace(text[end]))
        {
            end++;
        }

        _selectionAnchor = start;
        _caretIndex = end;
        EnsureCaretVisible();
    }

    private void SelectParagraphAt(int index)
    {
        var entries = CollectParagraphEntries(Document);
        if (entries.Count == 0)
        {
            _selectionAnchor = 0;
            _caretIndex = 0;
            return;
        }

        var maxOffset = Math.Max(0, entries[entries.Count - 1].EndOffset);
        var clamped = Math.Clamp(index, 0, maxOffset);
        for (var i = 0; i < entries.Count; i++)
        {
            if (clamped < entries[i].StartOffset || clamped > entries[i].EndOffset)
            {
                continue;
            }

            _selectionAnchor = entries[i].StartOffset;
            _caretIndex = entries[i].EndOffset;
            EnsureCaretVisible();
            return;
        }

        _selectionAnchor = entries[entries.Count - 1].StartOffset;
        _caretIndex = entries[entries.Count - 1].EndOffset;
        EnsureCaretVisible();
    }

    private void UpdatePointerClickCount(Vector2 pointerPosition)
    {
        var now = DateTime.UtcNow;
        var index = GetTextIndexFromPoint(pointerPosition);
        var withinWindow = (now - _lastPointerDownUtc).TotalMilliseconds <= MultiClickWindowMs;
        if (withinWindow && Math.Abs(index - _lastPointerDownIndex) <= 1)
        {
            _pointerClickCount = Math.Min(3, _pointerClickCount + 1);
        }
        else
        {
            _pointerClickCount = 1;
        }

        _lastPointerDownUtc = now;
        _lastPointerDownIndex = index;
    }

    private void AutoScrollForPointer(ref Vector2 pointer)
    {
        var textRect = GetTextRect();
        var layout = BuildOrGetLayout(textRect.Width);
        var changed = false;
        if (pointer.Y < textRect.Y)
        {
            _verticalOffset = Math.Max(0f, _verticalOffset - PointerAutoScrollStep);
            changed = true;
        }
        else if (pointer.Y > textRect.Y + textRect.Height)
        {
            _verticalOffset += PointerAutoScrollStep;
            changed = true;
        }

        if (pointer.X < textRect.X)
        {
            _horizontalOffset = Math.Max(0f, _horizontalOffset - PointerAutoScrollStep);
            changed = true;
        }
        else if (pointer.X > textRect.X + textRect.Width)
        {
            _horizontalOffset += PointerAutoScrollStep;
            changed = true;
        }

        ClampScrollOffsets(layout, textRect);
        pointer = new Vector2(
            Math.Clamp(pointer.X, textRect.X, textRect.X + textRect.Width),
            Math.Clamp(pointer.Y, textRect.Y, textRect.Y + textRect.Height));
        if (changed)
        {
            InvalidateVisual();
        }
    }

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        clipRect = LayoutSlot;
        return true;
    }
}

public readonly record struct RichTextBoxPerformanceSnapshot(
    int LayoutCacheHitCount,
    int LayoutCacheMissCount,
    int LayoutBuildSampleCount,
    double AverageLayoutBuildMilliseconds,
    double P95LayoutBuildMilliseconds,
    double P99LayoutBuildMilliseconds,
    double MaxLayoutBuildMilliseconds,
    int RenderSampleCount,
    double LastRenderMilliseconds,
    double AverageRenderMilliseconds,
    double MaxRenderMilliseconds,
    int SelectionGeometrySampleCount,
    double LastSelectionGeometryMilliseconds,
    double AverageSelectionGeometryMilliseconds,
    double MaxSelectionGeometryMilliseconds,
    int ClipboardSerializeSampleCount,
    double LastClipboardSerializeMilliseconds,
    double AverageClipboardSerializeMilliseconds,
    double MaxClipboardSerializeMilliseconds,
    int ClipboardDeserializeSampleCount,
    double LastClipboardDeserializeMilliseconds,
    double AverageClipboardDeserializeMilliseconds,
    double MaxClipboardDeserializeMilliseconds,
    int EditSampleCount,
    double LastEditMilliseconds,
    double AverageEditMilliseconds,
    double MaxEditMilliseconds,
    int UndoDepth,
    int RedoDepth,
    int UndoOperationCount,
    int RedoOperationCount);
