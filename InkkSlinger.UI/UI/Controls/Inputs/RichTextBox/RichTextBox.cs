using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Runtime.CompilerServices;
using System.Text;
using System.Collections.Immutable;
using InkkSlinger.UI.Telemetry;

namespace InkkSlinger;

[TemplatePart("PART_ContentHost", typeof(ScrollViewer))]
public partial class RichTextBox : Control, ITextInputControl, IRenderDirtyBoundsHintProvider, IHyperlinkHoverHost, IUiRootUpdateParticipant
{
    private static readonly IReadOnlyList<object> EmptySelectionItems = Array.Empty<object>();

    public static readonly RoutedEvent DocumentChangedEvent =
        new(nameof(DocumentChanged), RoutingStrategy.Bubble);
    public static readonly RoutedEvent TextChangedEvent =
        new(nameof(TextChanged), RoutingStrategy.Bubble);
    public static readonly RoutedEvent SelectionChangedEvent =
        new(nameof(SelectionChanged), RoutingStrategy.Bubble);
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

    public new static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(new Color(28, 28, 28), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(new Color(162, 162, 162), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(1f, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty PaddingProperty =
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

    public static readonly DependencyProperty HorizontalScrollBarVisibilityProperty =
        DependencyProperty.Register(
            nameof(HorizontalScrollBarVisibility),
            typeof(ScrollBarVisibility),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(
                ScrollBarVisibility.Auto,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty VerticalScrollBarVisibilityProperty =
        DependencyProperty.Register(
            nameof(VerticalScrollBarVisibility),
            typeof(ScrollBarVisibility),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(
                ScrollBarVisibility.Auto,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

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

    public static readonly DependencyProperty SelectionTextBrushProperty =
        DependencyProperty.Register(
            nameof(SelectionTextBrush),
            typeof(Color),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectionOpacityProperty =
        DependencyProperty.Register(
            nameof(SelectionOpacity),
            typeof(float),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float opacity
                    ? Math.Clamp(opacity, 0f, 1f)
                    : 1f));

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(
            nameof(IsReadOnly),
            typeof(bool),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.None));

    public static readonly DependencyProperty IsReadOnlyCaretVisibleProperty =
        DependencyProperty.Register(
            nameof(IsReadOnlyCaretVisible),
            typeof(bool),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsInactiveSelectionHighlightEnabledProperty =
        DependencyProperty.Register(
            nameof(IsInactiveSelectionHighlightEnabled),
            typeof(bool),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AcceptsReturnProperty =
        DependencyProperty.Register(
            nameof(AcceptsReturn),
            typeof(bool),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.None));

    public static readonly DependencyProperty AcceptsTabProperty =
        DependencyProperty.Register(
            nameof(AcceptsTab),
            typeof(bool),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.None));

    public static readonly DependencyProperty IsUndoEnabledProperty =
        DependencyProperty.Register(
            nameof(IsUndoEnabled),
            typeof(bool),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.None,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is RichTextBox richTextBox && args.NewValue is bool enabled)
                    {
                        richTextBox._undoManager.IsUndoEnabled = enabled;
                    }
                }));

    public static readonly DependencyProperty UndoLimitProperty =
        DependencyProperty.Register(
            nameof(UndoLimit),
            typeof(int),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(
                -1,
                FrameworkPropertyMetadataOptions.None,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is RichTextBox richTextBox && args.NewValue is int limit)
                    {
                        richTextBox._undoManager.UndoLimit = limit;
                    }
                },
                coerceValueCallback: static (_, value) => value is int limit && limit >= -1 ? limit : -1));

    public new static readonly DependencyProperty IsMouseOverProperty =
        DependencyProperty.Register(
            nameof(IsMouseOver),
            typeof(bool),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty IsFocusedProperty =
        DependencyProperty.Register(
            nameof(IsFocused),
            typeof(bool),
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly DocumentUndoManager _undoManager = new();
    private readonly DocumentLayoutEngine _layoutEngine = new();
    private readonly DocumentViewportLayoutCache _layoutCache = new();
    private readonly RichTextBoxPerformanceTracker _perfTracker = new();
    private DocumentLayoutResult? _lastMeasuredLayout;
    private DocumentLayoutResult? _lastRenderedLayout;
    private int _caretIndex;
    private int _selectionAnchor;
    private float _caretBlinkSeconds;
    private bool _isCaretVisible = true;
    private bool _isSelectingWithPointer;
    private bool _pointerSelectionMoved;
    private Hyperlink? _pendingPointerHyperlink;
    private Hyperlink? _hoveredHyperlink;
    private readonly Dictionary<Hyperlink, Style?> _appliedImplicitHyperlinkStyles = new();
    private bool _hasPendingRenderDirtyBoundsHint;
    private LayoutRect _pendingRenderDirtyBoundsHint;
    private bool _preserveRenderDirtyBoundsHint;
    private float _horizontalOffset;
    private float _verticalOffset;
    private float _lastViewportChangedHorizontalOffset = float.NaN;
    private float _lastViewportChangedVerticalOffset = float.NaN;
    private float _lastViewportChangedViewportWidth = float.NaN;
    private float _lastViewportChangedViewportHeight = float.NaN;
    private float _lastViewportChangedExtentWidth = float.NaN;
    private float _lastViewportChangedExtentHeight = float.NaN;
    private bool _hasPendingViewportChangedNotification;
    private int _documentChangeBatchDepth;
    private bool _hasPendingDocumentChangedEvent;
    private bool _hasPendingTextChangedEvent;
    private bool _hasPendingDocumentMaintenanceWork;
    private bool _suppressMeasureInvalidationForDocumentBatch;
    private bool _forceFullRenderInvalidationForDocumentChange;
    private bool _deferDocumentChangeBatchFlush;
    private int _deferredDocumentChangeFlushVersion;
    private DateTime _lastPointerDownUtc;
    private int _lastPointerDownIndex = -1;
    private int _pointerClickCount;
    private int _lastSelectionHitTestOffset = -1;
    private string _pendingInvalidationReason = "Unspecified";
    private bool _typingBoldActive;
    private bool _typingItalicActive;
    private bool _typingUnderlineActive;
    private ModifierKeys _activeKeyModifiers;
    private long _suppressSpaceTextInputUntilTicks;
    private readonly List<UIElement> _documentHostedVisualChildren = new();
    private readonly HostedDocumentVisualHost _hostedDocumentVisualHost = new();

    private readonly Queue<RecentOperationEntry> _recentOperations = new();
    private int _recentOperationSequence;
    private DocumentRichnessSnapshot _lastDocumentRichness = DocumentRichnessSnapshot.Empty;
    private const float PointerAutoScrollStep = 16f;
    private const double MultiClickWindowMs = 450d;
    private const int RecentOperationCapacity = 10;
    internal const string ClipboardRtfFormat = "Rich Text Format";
    internal const string ClipboardXamlFormat = "Xaml";
    internal const string ClipboardXamlPackageFormat = "XamlPackage";
    internal const string ClipboardTextFormat = "Text";
    internal const string ClipboardUnicodeTextFormat = "UnicodeText";
    private static readonly ImmutableHashSet<string> RichGuardedReplaceSelectionCommands =
        ImmutableHashSet.Create(StringComparer.Ordinal, "InsertText", "InsertTextStyled", "TabForward", "Backspace", "DeleteForward", "DeleteSelection", "DeletePreviousWord", "DeleteNextWord");
    private static readonly ImmutableHashSet<string> RichGuardedFragmentCommands =
        ImmutableHashSet.Create(StringComparer.Ordinal, "EnterParagraphBreak", "InsertTextStyled", "InsertText");

    public RichTextBox()
    {
        _scrollContentPresenter = new RichTextBoxScrollContentPresenter(this);
        SetValue(DocumentProperty, CreateDefaultDocument());
        _lastDocumentRichness = CaptureDocumentRichness(Document);
        _undoManager.IsUndoEnabled = IsUndoEnabled;
        _undoManager.UndoLimit = UndoLimit;
        RegisterEditingCommandBindings();
        RegisterEditingInputBindings();
    }

    public event EventHandler? ViewportChanged;

    public event EventHandler<RoutedSimpleEventArgs> DocumentChanged
    {
        add => AddHandler(DocumentChangedEvent, value);
        remove => RemoveHandler(DocumentChangedEvent, value);
    }

    public event EventHandler<RoutedSimpleEventArgs> TextChanged
    {
        add => AddHandler(TextChangedEvent, value);
        remove => RemoveHandler(TextChangedEvent, value);
    }

    public event EventHandler<SelectionChangedEventArgs> SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
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

    public new Color Foreground
    {
        get => GetValue<Color>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public new Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public new Color BorderBrush
    {
        get => GetValue<Color>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public new float BorderThickness
    {
        get => GetValue<float>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public new Thickness Padding
    {
        get => GetValue<Thickness>(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    public TextWrapping TextWrapping
    {
        get => GetValue<TextWrapping>(TextWrappingProperty);
        set => SetValue(TextWrappingProperty, value);
    }

    public ScrollBarVisibility HorizontalScrollBarVisibility
    {
        get => GetValue<ScrollBarVisibility>(HorizontalScrollBarVisibilityProperty);
        set => SetValue(HorizontalScrollBarVisibilityProperty, value);
    }

    public ScrollBarVisibility VerticalScrollBarVisibility
    {
        get => GetValue<ScrollBarVisibility>(VerticalScrollBarVisibilityProperty);
        set => SetValue(VerticalScrollBarVisibilityProperty, value);
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

    public Color SelectionTextBrush
    {
        get => GetValue<Color>(SelectionTextBrushProperty);
        set => SetValue(SelectionTextBrushProperty, value);
    }

    public float SelectionOpacity
    {
        get => GetValue<float>(SelectionOpacityProperty);
        set => SetValue(SelectionOpacityProperty, value);
    }

    public bool IsReadOnly
    {
        get => GetValue<bool>(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public bool IsReadOnlyCaretVisible
    {
        get => GetValue<bool>(IsReadOnlyCaretVisibleProperty);
        set => SetValue(IsReadOnlyCaretVisibleProperty, value);
    }

    public bool IsInactiveSelectionHighlightEnabled
    {
        get => GetValue<bool>(IsInactiveSelectionHighlightEnabledProperty);
        set => SetValue(IsInactiveSelectionHighlightEnabledProperty, value);
    }

    public bool AcceptsReturn
    {
        get => GetValue<bool>(AcceptsReturnProperty);
        set => SetValue(AcceptsReturnProperty, value);
    }

    public bool AcceptsTab
    {
        get => GetValue<bool>(AcceptsTabProperty);
        set => SetValue(AcceptsTabProperty, value);
    }

    public bool IsSpellCheckEnabled
    {
        get => SpellCheck.GetIsEnabled(this);
        set => SpellCheck.SetIsEnabled(this, value);
    }

    public bool IsUndoEnabled
    {
        get => GetValue<bool>(IsUndoEnabledProperty);
        set => SetValue(IsUndoEnabledProperty, value);
    }

    public int UndoLimit
    {
        get => GetValue<int>(UndoLimitProperty);
        set => SetValue(UndoLimitProperty, value);
    }

    public new bool IsMouseOver
    {
        get => GetValue<bool>(IsMouseOverProperty);
        private set => SetValue(IsMouseOverProperty, value);
    }

    public new bool IsFocused
    {
        get => GetValue<bool>(IsFocusedProperty);
        private set => SetValue(IsFocusedProperty, value);
    }

    public int CaretIndex => _caretIndex;

    public TextSelection Selection => new(_selectionAnchor, _caretIndex);

    public TextPointer CaretPosition => CreateTextPointer(_caretIndex);

    public DocumentTextSelection DocumentSelection => new(CreateTextPointer(_selectionAnchor), CreateTextPointer(_caretIndex));

    public TextRange SelectionRange => DocumentSelection.ToRange();

    public int SelectionStart => Math.Min(_selectionAnchor, _caretIndex);

    public int SelectionLength => Math.Abs(_caretIndex - _selectionAnchor);

    public bool CanUndo => _undoManager.CanUndo;

    public bool CanRedo => _undoManager.CanRedo;

    public float HorizontalOffset => GetScrollMetrics().HorizontalOffset;

    public float VerticalOffset => GetScrollMetrics().VerticalOffset;

    public float ViewportWidth => GetScrollMetrics().ViewportWidth;

    public float ViewportHeight => GetScrollMetrics().ViewportHeight;

    public float ExtentWidth => GetScrollMetrics().ExtentWidth;

    public float ExtentHeight => GetScrollMetrics().ExtentHeight;

    public float ScrollableWidth => GetScrollMetrics().ScrollableWidth;

    public float ScrollableHeight => GetScrollMetrics().ScrollableHeight;

    internal (float HorizontalOffset, float VerticalOffset, float ViewportWidth, float ViewportHeight, float ExtentWidth, float ExtentHeight) GetScrollMetricsSnapshot()
    {
        var metrics = GetScrollMetrics();
        return (
            metrics.HorizontalOffset,
            metrics.VerticalOffset,
            metrics.ViewportWidth,
            metrics.ViewportHeight,
            metrics.ExtentWidth,
            metrics.ExtentHeight);
    }

    public RichTextBoxPerformanceSnapshot GetPerformanceSnapshot()
    {
        return _perfTracker.GetSnapshot(_undoManager);
    }

    public void ResetPerformanceSnapshot()
    {
        _perfTracker.Reset();
    }

    public void Undo()
    {
        ExecuteUndo();
    }

    public void Redo()
    {
        ExecuteRedo();
    }

    private bool ExecuteUndo()
    {
        var handled = ExecuteTextMutationBatch(() => _undoManager.Undo());
        if (!handled)
        {
            return false;
        }

        ClampSelectionToTextLength();
        TraceInvariants("Undo");
        InvalidateVisualWithReason("Undo");
        return true;
    }

    private bool ExecuteRedo()
    {
        var handled = ExecuteTextMutationBatch(() => _undoManager.Redo());
        if (!handled)
        {
            return false;
        }

        ClampSelectionToTextLength();
        TraceInvariants("Redo");
        InvalidateVisualWithReason("Redo");
        return true;
    }

    private void RaiseSelectionChangedEventIfNeeded(int previousStart, int previousLength)
    {
        if (previousStart == SelectionStart && previousLength == SelectionLength)
        {
            return;
        }

        RaiseRoutedEventInternal(
            SelectionChangedEvent,
            new SelectionChangedEventArgs(SelectionChangedEvent, EmptySelectionItems, EmptySelectionItems));
    }

    private TextPointer CreateTextPointer(int offset)
    {
        return DocumentPointers.CreateAtDocumentOffset(Document, offset);
    }

    private int ResolveDocumentOffset(TextPointer position, string parameterName)
    {
        if (!DocumentPointers.TryGetDocumentOffset(Document, position, out var offset))
        {
            throw new ArgumentException("TextPointer must belong to the current RichTextBox document.", parameterName);
        }

        return offset;
    }

    private void ValidateSpellCheckCharacterIndex(int charIndex)
    {
        var textLength = GetText().Length;
        if (charIndex < 0 || charIndex > textLength)
        {
            throw new ArgumentOutOfRangeException(nameof(charIndex));
        }
    }

    private static void ValidateLogicalDirection(LogicalDirection direction, string parameterName)
    {
        if (direction is LogicalDirection.Backward or LogicalDirection.Forward)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(parameterName);
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


    private bool ExtendSelectionModifierActive()
    {
        return (_activeKeyModifiers & ModifierKeys.Shift) != 0;
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

    private static string FormatRect(LayoutRect rect)
    {
        return $"({rect.X:0.##},{rect.Y:0.##},{rect.Width:0.##},{rect.Height:0.##})";
    }

    private DocumentLayoutResult BuildOrGetLayout(float availableWidth)
    {
        var normalizedWidth = TextWrapping == TextWrapping.NoWrap || availableWidth <= 0f
            ? float.PositiveInfinity
            : availableWidth;
        var containsHostedVisualChildren = ContainsHostedDocumentChildren(Document);
        var canReuseHostedLayoutCache = containsHostedVisualChildren && AreHostedDocumentChildrenLayoutStable();
        var text = GetText();
        var typography = UiTextRenderer.ResolveTypography(this, FontSize);
        var signature = HashCode.Combine(
            RuntimeHelpers.GetHashCode(Document),
            StringComparer.Ordinal.GetHashCode(text),
            typography,
            (int)TextWrapping);
        var lineHeight = Math.Max(1f, UiTextRenderer.GetLineHeight(typography));
        var key = new DocumentViewportLayoutCache.CacheKey(
            signature,
            normalizedWidth,
            TextWrapping,
            typography.GetHashCode(),
            lineHeight,
            Foreground);
        if ((!containsHostedVisualChildren || canReuseHostedLayoutCache) && _layoutCache.TryGet(key, out var cached))
        {
            _perfTracker.RecordLayoutCacheHit();
            return cached;
        }

        _perfTracker.RecordLayoutCacheMiss();
        var buildStart = Stopwatch.GetTimestamp();
        var settings = new DocumentLayoutSettings(
            AvailableWidth: normalizedWidth,
            Typography: typography,
            Wrapping: TextWrapping,
            Foreground: Foreground,
            LineHeight: lineHeight,
            ListIndent: lineHeight * 2.2f,
            ListMarkerGap: 4f,
            TableCellPadding: 4f,
            TableBorderThickness: 1f,
            ConstrainTablesToAvailableWidth: false);
        var built = _layoutEngine.Layout(Document, settings);
        _layoutCache.Store(key, built);
        var buildMs = Stopwatch.GetElapsedTime(buildStart).TotalMilliseconds;
        _perfTracker.RecordLayoutBuild(buildMs);
        return built;
    }

    private bool AreHostedDocumentChildrenLayoutStable()
    {
        for (var index = 0; index < _documentHostedVisualChildren.Count; index++)
        {
            if (_documentHostedVisualChildren[index] is not FrameworkElement frameworkElement)
            {
                return false;
            }

            if (frameworkElement.NeedsMeasure || frameworkElement.NeedsArrange)
            {
                return false;
            }
        }

        return _documentHostedVisualChildren.Count > 0;
    }

    private static bool ContainsHostedDocumentChildren(FlowDocument document)
    {
        return ContainsHostedDocumentChildren(document.Blocks);
    }

    private static bool ContainsHostedDocumentChildren(IEnumerable<Block> blocks)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Paragraph paragraph when ContainsHostedDocumentChildren(paragraph.Inlines):
                    return true;
                case BlockUIContainer blockUiContainer when blockUiContainer.Child != null:
                    return true;
                case Section section when ContainsHostedDocumentChildren(section.Blocks):
                    return true;
                case InkkSlinger.List list:
                    for (var index = 0; index < list.Items.Count; index++)
                    {
                        if (ContainsHostedDocumentChildren(list.Items[index].Blocks))
                        {
                            return true;
                        }
                    }

                    break;
                case Table table:
                    for (var rowGroupIndex = 0; rowGroupIndex < table.RowGroups.Count; rowGroupIndex++)
                    {
                        var rowGroup = table.RowGroups[rowGroupIndex];
                        for (var rowIndex = 0; rowIndex < rowGroup.Rows.Count; rowIndex++)
                        {
                            var row = rowGroup.Rows[rowIndex];
                            for (var cellIndex = 0; cellIndex < row.Cells.Count; cellIndex++)
                            {
                                if (ContainsHostedDocumentChildren(row.Cells[cellIndex].Blocks))
                                {
                                    return true;
                                }
                            }
                        }
                    }

                    break;
            }
        }

        return false;
    }

    private static bool ContainsHostedDocumentChildren(IEnumerable<Inline> inlines)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case InlineUIContainer inlineUiContainer when inlineUiContainer.Child != null:
                    return true;
                case Span span when ContainsHostedDocumentChildren(span.Inlines):
                    return true;
            }
        }

        return false;
    }

}



