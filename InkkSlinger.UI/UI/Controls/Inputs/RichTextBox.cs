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
    private const string ClipboardRtfFormat = "Rich Text Format";
    private const string ClipboardXamlFormat = "Xaml";
    private const string ClipboardXamlPackageFormat = "XamlPackage";
    private const string ClipboardTextFormat = "Text";
    private const string ClipboardUnicodeTextFormat = "UnicodeText";
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
        return IsSupportedDataFormat(dataFormat);
    }

    public bool CanSave(string dataFormat)
    {
        return IsSupportedDataFormat(dataFormat);
    }

    public void Load(Stream stream, string dataFormat)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!CanLoad(dataFormat))
        {
            throw new NotSupportedException($"RichTextBox does not support loading format '{dataFormat}'.");
        }

        var payload = ReadStreamPayload(stream);
        var document = DeserializeDocumentPayload(payload, dataFormat);
        ApplyLoadedDocument(document, "LoadDocument");
    }

    public void Save(Stream stream, string dataFormat)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!CanSave(dataFormat))
        {
            throw new NotSupportedException($"RichTextBox does not support saving format '{dataFormat}'.");
        }

        var payload = SerializeDocumentPayload(Document, dataFormat);
        WriteStreamPayload(stream, payload);
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

        var payload = ReadStreamPayload(stream);
        if (IsRichFragmentFormat(dataFormat))
        {
            var fragment = DeserializeFragmentPayload(payload, dataFormat);
            ReplaceSelectionWithFragment(fragment, "LoadSelection", GroupingPolicy.StructuralAtomic);
            return;
        }

        var text = DeserializeTextPayload(payload, dataFormat);
        ReplaceSelection(NormalizeNewlines(text), "LoadSelection", GroupingPolicy.StructuralAtomic);
    }

    public void SaveSelection(Stream stream, string dataFormat)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!CanSave(dataFormat))
        {
            throw new NotSupportedException($"RichTextBox does not support saving format '{dataFormat}'.");
        }

        var payload = SerializeSelectionPayload(dataFormat);
        WriteStreamPayload(stream, payload);
    }

    public int GetNextSpellingErrorCharacterIndex(int charIndex, LogicalDirection direction)
    {
        ValidateSpellCheckCharacterIndex(charIndex);
        ValidateLogicalDirection(direction, nameof(direction));

        return IsSpellCheckEnabled ? -1 : -1;
    }

    public TextPointer? GetNextSpellingErrorPosition(TextPointer position, LogicalDirection direction)
    {
        _ = ResolveDocumentOffset(position, nameof(position));
        ValidateLogicalDirection(direction, nameof(direction));

        return null;
    }

    public SpellingError? GetSpellingError(TextPointer position)
    {
        _ = ResolveDocumentOffset(position, nameof(position));
        return null;
    }

    public TextRange? GetSpellingErrorRange(TextPointer position)
    {
        _ = ResolveDocumentOffset(position, nameof(position));
        return null;
    }

    public void SelectAll()
    {
        ExecuteSelectAllCore();
    }

    public void Select(int start, int length)
    {
        var textLength = GetText().Length;
        if (start < 0 || start > textLength)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        if (length < 0 || start + length > textLength)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        UpdateSelectionState(start, start + length, ensureCaretVisible: true);
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateVisualWithReason("Select");
    }

    public void Select(TextPointer anchorPosition, TextPointer movingPosition)
    {
        var anchorOffset = ResolveDocumentOffset(anchorPosition, nameof(anchorPosition));
        var movingOffset = ResolveDocumentOffset(movingPosition, nameof(movingPosition));
        UpdateSelectionState(anchorOffset, movingOffset, ensureCaretVisible: true);
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateVisualWithReason("Select");
    }

    public TextPointer? GetPositionFromPoint(Vector2 point, bool snapToText)
    {
        var textRect = GetTextRect();
        if (textRect.Width <= 0f || textRect.Height <= 0f)
        {
            return null;
        }

        if (!snapToText &&
            (point.X < textRect.X ||
             point.Y < textRect.Y ||
             point.X > textRect.X + textRect.Width ||
             point.Y > textRect.Y + textRect.Height))
        {
            return null;
        }

        var offset = GetTextIndexFromPoint(point);
        return CreateTextPointer(offset);
    }

    /// <summary>
    /// Gets the caret's rendered bounds in root-space coordinates, clipped to the visible text viewport.
    /// </summary>
    public bool TryGetCaretBounds(out LayoutRect bounds)
    {
        bounds = default;

        var textRect = GetTextRect();
        if (textRect.Width <= 0f || textRect.Height <= 0f)
        {
            return false;
        }

        var layout = BuildOrGetLayout(textRect.Width);
        ClampScrollOffsets(layout, textRect);
        if (!TryGetCaretRenderRect(textRect, layout, GetEffectiveHorizontalOffset(), GetEffectiveVerticalOffset(), out var caretRect))
        {
            return false;
        }

        if (TryProjectRectToRootSpace(caretRect, out var rootSpaceBounds))
        {
            caretRect = rootSpaceBounds;
        }

        bounds = NormalizeRect(caretRect);
        return bounds.Width > 0f && bounds.Height > 0f;
    }

    public void LineUp()
    {
        ScrollBy(0f, -UiTextRenderer.GetLineHeight(this, FontSize), "LineUp");
    }

    public void LineDown()
    {
        ScrollBy(0f, UiTextRenderer.GetLineHeight(this, FontSize), "LineDown");
    }

    public void LineLeft()
    {
        ScrollBy(-16f, 0f, "LineLeft");
    }

    public void LineRight()
    {
        ScrollBy(16f, 0f, "LineRight");
    }

    public void PageUp()
    {
        ScrollBy(0f, -Math.Max(0f, ViewportHeight), "PageUp");
    }

    public void PageDown()
    {
        ScrollBy(0f, Math.Max(0f, ViewportHeight), "PageDown");
    }

    public void PageLeft()
    {
        ScrollBy(-Math.Max(0f, ViewportWidth), 0f, "PageLeft");
    }

    public void PageRight()
    {
        ScrollBy(Math.Max(0f, ViewportWidth), 0f, "PageRight");
    }

    public void ScrollToHome()
    {
        SetScrollOffsets(0f, 0f, "ScrollToHome");
    }

    public void ScrollToEnd()
    {
        var metrics = GetScrollMetrics();
        SetScrollOffsets(metrics.ScrollableWidth, metrics.ScrollableHeight, "ScrollToEnd");
    }

    public void ScrollToHorizontalOffset(float offset)
    {
        SetScrollOffsets(offset, GetEffectiveVerticalOffset(), "ScrollToHorizontalOffset");
    }

    public void ScrollToVerticalOffset(float offset)
    {
        SetScrollOffsets(GetEffectiveHorizontalOffset(), offset, "ScrollToVerticalOffset");
    }

    public void PreserveCurrentScrollOffsetsOnNextLayout()
    {
        _horizontalOffset = GetEffectiveHorizontalOffset();
        _verticalOffset = GetEffectiveVerticalOffset();
        _hasPendingContentHostScrollOffsets = _contentHost != null;
    }

    public bool HandleTextInputFromInput(char character)
    {
        if (character == ' ' && ShouldSuppressDuplicateSpaceTextInput())
        {
            RecordOperation("TextInputSuppressed", "space-duplicate");
            return true;
        }

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
        var beforeParagraph = DocumentEditing.CloneParagraph(entries[paragraphIndex].Paragraph);
        var afterParagraph = DocumentEditing.CloneParagraph(entries[paragraphIndex].Paragraph);
        ReplaceParagraphTextPreservingSimpleWrappers(afterParagraph, nextParagraphText);
        var caretAfter = start + replacement.Length;
        return CommitParagraphReplacement(
            commandType,
            policy,
            start,
            length,
            caretAfter,
            paragraphIndex,
            beforeParagraph,
            afterParagraph,
            editStart);
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
        var sourceParagraph = entries[paragraphIndex].Paragraph;
        var beforeParagraph = DocumentEditing.CloneParagraph(sourceParagraph);
        var destination = new Paragraph();
        CopyParagraphSettings(sourceParagraph, destination);
        destination.Inlines.Clear();
        AppendStyledInlineRangeFromParagraph(sourceParagraph, 0, localStart, destination);
        if (!string.IsNullOrEmpty(replacement))
        {
            destination.Inlines.Add(CreateReplacementInlinePreservingContext(sourceParagraph, localStart, replacement));
        }

        AppendStyledInlineRangeFromParagraph(sourceParagraph, localEnd, paragraphLength, destination);
        NormalizeAdjacentInlines(destination);
        if (destination.Inlines.Count == 0)
        {
            destination.Inlines.Add(new Run(string.Empty));
        }

        var caretAfter = start + replacement.Length;
        return CommitParagraphReplacement(
            commandType,
            policy,
            start,
            length,
            caretAfter,
            paragraphIndex,
            beforeParagraph,
            destination,
            editStart);
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
        var structuredEnterStart = Stopwatch.GetTimestamp();
        var start = SelectionStart;
        var length = SelectionLength;
        var end = start + length;
        var collectEntriesStart = Stopwatch.GetTimestamp();
        var entries = CollectParagraphEntries(Document);
        var collectEntriesMs = Stopwatch.GetElapsedTime(collectEntriesStart).TotalMilliseconds;
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
        var postSelectionText = paragraphText.Remove(localStart, localLength);
        var leftText = postSelectionText[..localStart];
        var rightText = postSelectionText[localStart..];

        var editStart = Stopwatch.GetTimestamp();
        var sourceParagraph = entries[paragraphIndex].Paragraph;
        var beforeParagraph = DocumentEditing.CloneParagraph(sourceParagraph);
        var paragraph = sourceParagraph;
        if (paragraph.Parent is ListItem && paragraph.Parent.Parent is InkkSlinger.List)
        {
            var undoDepthBefore = _undoManager.UndoDepth;
            var redoDepthBefore = _undoManager.RedoDepth;
            var textLengthBefore = GetText().Length;
            var beforeText = GetText();
            var cloneDocumentStart = Stopwatch.GetTimestamp();
            var afterDocument = DocumentEditing.CloneDocument(Document);
            var cloneDocumentMs = Stopwatch.GetElapsedTime(cloneDocumentStart).TotalMilliseconds;
            var enumerateParagraphsStart = Stopwatch.GetTimestamp();
            var paragraphs = new List<Paragraph>(FlowDocumentPlainText.EnumerateParagraphs(afterDocument));
            var enumerateParagraphsMs = Stopwatch.GetElapsedTime(enumerateParagraphsStart).TotalMilliseconds;
            if (paragraphIndex >= paragraphs.Count)
            {
                RecordOperation("StructuredEnter", "ParagraphIndexOutOfRangeAfterClone");
                return false;
            }

            paragraph = paragraphs[paragraphIndex];
            if (paragraph.Parent is not ListItem clonedListItem || clonedListItem.Parent is not InkkSlinger.List clonedList)
            {
                RecordOperation("StructuredEnter", "ListCloneParentResolutionFailed");
                return false;
            }

            var listBehaviorStart = Stopwatch.GetTimestamp();
            if (TryApplyListEnterBehavior(paragraph, clonedListItem, clonedList, leftText, rightText, out afterDocument, out var caretAfter))
            {
                RecordOperation("StructuredEnter", "ListBehaviorApplied");
                var prepareParagraphsMs = Stopwatch.GetElapsedTime(listBehaviorStart).TotalMilliseconds;
                var commitStart = Stopwatch.GetTimestamp();
                var committed = CommitStructuredDocumentReplacement(
                    commandType,
                    policy,
                    start,
                    length,
                    caretAfter,
                    Document,
                    beforeText,
                    afterDocument,
                    textLengthBefore,
                    undoDepthBefore,
                    redoDepthBefore,
                    editStart);
                _perfTracker.ClearStructuredEnterCommitBreakdown();
                _perfTracker.RecordStructuredEnter(
                    collectEntriesMs,
                    cloneDocumentMs,
                    enumerateParagraphsMs,
                    prepareParagraphsMs,
                    Stopwatch.GetElapsedTime(commitStart).TotalMilliseconds,
                    Stopwatch.GetElapsedTime(structuredEnterStart).TotalMilliseconds,
                    usedDocumentReplacement: true);
                return committed;
            }

            RecordOperation("StructuredEnter", "ListBehaviorRejected");
            return false;
        }

        var prepareParagraphsStart = Stopwatch.GetTimestamp();
        var leftParagraph = CreateParagraphFromStyledRange(paragraph, 0, localStart);
        var rightParagraph = CreateParagraphFromStyledRange(paragraph, localStart + localLength, paragraphText.Length);
        RecordOperation("StructuredEnter", "ParagraphSplitApplied");
        var prepareParagraphsMsFinal = Stopwatch.GetElapsedTime(prepareParagraphsStart).TotalMilliseconds;
        var splitCommitStart = Stopwatch.GetTimestamp();
        var splitCommitted = CommitParagraphSplit(
            commandType,
            policy,
            start,
            length,
            start + 1,
            paragraphIndex,
            beforeParagraph,
            leftParagraph,
            rightParagraph,
            editStart);
        _perfTracker.RecordStructuredEnter(
            collectEntriesMs,
            0d,
            0d,
            prepareParagraphsMsFinal,
            Stopwatch.GetElapsedTime(splitCommitStart).TotalMilliseconds,
            Stopwatch.GetElapsedTime(structuredEnterStart).TotalMilliseconds,
            usedDocumentReplacement: false);
        return splitCommitted;
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
        ExecuteTextMutationBatch(() =>
        {
            session.ApplyOperation(new ReplaceDocumentOperation("Document", beforeText, afterText, Document, afterDocument));
            session.CommitTransaction();
        });
        UpdateSelectionState(caretAfter, caretAfter, ensureCaretVisible: false);
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        _perfTracker.RecordEdit(elapsedMs);
        TraceInvariants(commandType);
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateAfterTextMutation(commandType);
        return true;
    }

    private bool CommitParagraphReplacement(
        string commandType,
        GroupingPolicy policy,
        int start,
        int length,
        int caretAfter,
        int paragraphIndex,
        Paragraph beforeParagraph,
        Paragraph afterParagraph,
        long editStart)
    {
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
        ExecuteTextMutationBatch(() =>
        {
            session.ApplyOperation(
                new ReplaceParagraphOperation(
                    paragraphIndex,
                    DocumentEditing.GetParagraphText(beforeParagraph),
                    DocumentEditing.GetParagraphText(afterParagraph),
                    beforeParagraph,
                    afterParagraph));
            session.CommitTransaction();
        });
        UpdateSelectionState(caretAfter, caretAfter, ensureCaretVisible: false);
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        _perfTracker.RecordEdit(elapsedMs);
        TraceInvariants(commandType);
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateAfterTextMutation(commandType);
        return true;
    }

    private bool CommitParagraphSplit(
        string commandType,
        GroupingPolicy policy,
        int start,
        int length,
        int caretAfter,
        int paragraphIndex,
        Paragraph beforeParagraph,
        Paragraph currentParagraphAfterSplit,
        Paragraph insertedParagraph,
        long editStart)
    {
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
        var applyOperationMs = 0d;
        var commitTransactionMs = 0d;
        var mutationBatchStart = Stopwatch.GetTimestamp();
        ExecuteTextMutationBatch(() =>
        {
            var applyOperationStart = Stopwatch.GetTimestamp();
            session.ApplyOperation(
                new SplitStructuredParagraphOperation(
                    paragraphIndex,
                    DocumentEditing.GetParagraphText(beforeParagraph),
                    $"{DocumentEditing.GetParagraphText(currentParagraphAfterSplit)}\n{DocumentEditing.GetParagraphText(insertedParagraph)}",
                    beforeParagraph,
                    currentParagraphAfterSplit,
                    insertedParagraph));
            applyOperationMs = Stopwatch.GetElapsedTime(applyOperationStart).TotalMilliseconds;
            var commitTransactionStart = Stopwatch.GetTimestamp();
            session.CommitTransaction();
            commitTransactionMs = Stopwatch.GetElapsedTime(commitTransactionStart).TotalMilliseconds;
        });
        var mutationBatchMs = Stopwatch.GetElapsedTime(mutationBatchStart).TotalMilliseconds;
        var selectionStart = Stopwatch.GetTimestamp();
        UpdateSelectionState(caretAfter, caretAfter, ensureCaretVisible: false);
        var selectionMs = Stopwatch.GetElapsedTime(selectionStart).TotalMilliseconds;
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        _perfTracker.RecordEdit(elapsedMs);
        var traceInvariantsStart = Stopwatch.GetTimestamp();
        TraceInvariants(commandType);
        var traceInvariantsMs = Stopwatch.GetElapsedTime(traceInvariantsStart).TotalMilliseconds;
        var ensureCaretVisibleStart = Stopwatch.GetTimestamp();
        EnsureCaretVisible();
        var ensureCaretVisibleMs = Stopwatch.GetElapsedTime(ensureCaretVisibleStart).TotalMilliseconds;
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        var invalidateAfterMutationStart = Stopwatch.GetTimestamp();
        InvalidateAfterTextMutation(commandType);
        var invalidateAfterMutationMs = Stopwatch.GetElapsedTime(invalidateAfterMutationStart).TotalMilliseconds;
        _perfTracker.RecordStructuredEnterCommitBreakdown(
            mutationBatchMs,
            applyOperationMs,
            commitTransactionMs,
            selectionMs,
            traceInvariantsMs,
            ensureCaretVisibleMs,
            invalidateAfterMutationMs);
        return true;
    }

    private bool CommitParagraphMerge(
        string commandType,
        GroupingPolicy policy,
        int start,
        int length,
        int caretAfter,
        int previousParagraphIndex,
        Paragraph beforePreviousParagraph,
        Paragraph beforeCurrentParagraph,
        Paragraph mergedParagraph,
        long editStart)
    {
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
        ExecuteTextMutationBatch(() =>
        {
            session.ApplyOperation(
                new MergeStructuredParagraphOperation(
                    previousParagraphIndex,
                    DocumentEditing.GetParagraphText(beforePreviousParagraph),
                    DocumentEditing.GetParagraphText(beforeCurrentParagraph),
                    DocumentEditing.GetParagraphText(mergedParagraph),
                    beforePreviousParagraph,
                    beforeCurrentParagraph,
                    mergedParagraph));
            session.CommitTransaction();
        });
        UpdateSelectionState(caretAfter, caretAfter, ensureCaretVisible: false);
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        _perfTracker.RecordEdit(elapsedMs);
        TraceInvariants(commandType);
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateAfterTextMutation(commandType);
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

        var sourceCurrent = entries[paragraphIndex].Paragraph;
        if (TryGetImmediatePreviousParagraph(sourceCurrent, out var sourcePrevious))
        {
            var editStartLocal = Stopwatch.GetTimestamp();
            var beforePreviousParagraph = DocumentEditing.CloneParagraph(sourcePrevious);
            var beforeCurrentParagraph = DocumentEditing.CloneParagraph(sourceCurrent);
            var mergedParagraph = DocumentEditing.CloneParagraph(sourcePrevious);
            var caretAfterLocal = FindParagraphStartOffset(Document, sourcePrevious) + GetParagraphLogicalLength(sourcePrevious);
            AppendParagraphContentPreservingStyles(mergedParagraph, sourceCurrent);
            RecordOperation("StructuredBackspace", "SiblingParagraphMergeApplied");
            return CommitParagraphMerge(
                commandType,
                policy,
                Math.Max(0, caret - 1),
                1,
                caretAfterLocal,
                paragraphIndex - 1,
                beforePreviousParagraph,
                beforeCurrentParagraph,
                mergedParagraph,
                editStartLocal);
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
            ExecuteTextMutationBatch(() =>
            {
                topLevelSession.ApplyOperation(new ReplaceDocumentOperation("Document", beforeText, afterTextTopLevel, beforeDocument, afterDocument));
                topLevelSession.CommitTransaction();
            });
            UpdateSelectionState(caretAfterTopLevel, caretAfterTopLevel, ensureCaretVisible: false);
            var elapsedTopLevelMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
            _perfTracker.RecordEdit(elapsedTopLevelMs);
            TraceInvariants(commandType);
            EnsureCaretVisible();
            _caretBlinkSeconds = 0f;
            _isCaretVisible = true;
            InvalidateAfterTextMutation(commandType);
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
            ExecuteTextMutationBatch(() =>
            {
                boundarySession.ApplyOperation(new ReplaceDocumentOperation("Document", beforeText, afterTextBoundary, beforeDocument, afterDocument));
                boundarySession.CommitTransaction();
            });
            UpdateSelectionState(caretAfterBoundary, caretAfterBoundary, ensureCaretVisible: false);
            var elapsedBoundaryMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
            _perfTracker.RecordEdit(elapsedBoundaryMs);
            TraceInvariants(commandType);
            EnsureCaretVisible();
            _caretBlinkSeconds = 0f;
            _isCaretVisible = true;
            InvalidateAfterTextMutation(commandType);
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
        ExecuteTextMutationBatch(() =>
        {
            session.ApplyOperation(new ReplaceDocumentOperation("Document", beforeText, afterText, beforeDocument, afterDocument));
            session.CommitTransaction();
        });
        UpdateSelectionState(caretAfter, caretAfter, ensureCaretVisible: false);
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        _perfTracker.RecordEdit(elapsedMs);
        TraceInvariants(commandType);
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateAfterTextMutation(commandType);
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

    private static bool TryGetImmediatePreviousParagraph(Paragraph paragraph, out Paragraph previous)
    {
        if (paragraph.Parent is FlowDocument document)
        {
            var index = document.Blocks.IndexOf(paragraph);
            if (index > 0 && document.Blocks[index - 1] is Paragraph previousParagraph)
            {
                previous = previousParagraph;
                return true;
            }
        }

        if (TryGetPreviousSiblingParagraph(paragraph, out previous))
        {
            return true;
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
                if (handled)
                {
                    ArmDuplicateSpaceTextInputSuppression();
                }
                return handled;
            }

            if (key == Keys.Enter && (ctrl || IsReadOnly) && TryActivateHyperlinkAtSelection())
            {
                return true;
            }

            if (ctrl && key == Keys.Z)
            {
                var handled = ExecuteUndo();
                return true;
            }

            if (ctrl && key == Keys.Y)
            {
                var handled = ExecuteRedo();
                return true;
            }

            if (TryExecuteEditingCommandFromKey(key, modifiers))
            {
                return true;
            }

            return false;
        }
        finally
        {
            _activeKeyModifiers = ModifierKeys.None;
        }
    }

    private void ArmDuplicateSpaceTextInputSuppression()
    {
        _suppressSpaceTextInputUntilTicks = Stopwatch.GetTimestamp() + (Stopwatch.Frequency / 4);
    }

    private bool ShouldSuppressDuplicateSpaceTextInput()
    {
        var until = _suppressSpaceTextInputUntilTicks;
        if (until <= 0)
        {
            return false;
        }

        var now = Stopwatch.GetTimestamp();
        if (now <= until)
        {
            _suppressSpaceTextInputUntilTicks = 0;
            return true;
        }

        _suppressSpaceTextInputUntilTicks = 0;
        return false;
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
        _pendingPointerHyperlink = ResolveHyperlinkAtOffset(index);
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
        UpdateSelectionState(_selectionAnchor, index, ensureCaretVisible: true);
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
            _pendingPointerHyperlink != null)
        {
            TryActivateHyperlink(_pendingPointerHyperlink);
        }

        _pendingPointerHyperlink = null;
        return true;
    }

    public bool HandleMouseWheelFromInput(int delta)
    {
        if (!IsEnabled || delta == 0)
        {
            return false;
        }

        if (_contentHost != null)
        {
                return _contentHost.HandleMouseWheelFromInput(delta);
        }

        return ScrollBy(0f, -MathF.Sign(delta) * (UiTextRenderer.GetLineHeight(this, FontSize) * 3f), "MouseWheelScroll");
    }

    public void SetMouseOverFromInput(bool isMouseOver)
    {
        if (IsMouseOver == isMouseOver)
        {
            return;
        }

        IsMouseOver = isMouseOver;
        if (!isMouseOver)
        {
            SetHoveredHyperlink(null);
        }
    }

    public void UpdateHoveredHyperlinkFromPointer(Vector2 pointerPosition)
    {
        if (!IsEnabled)
        {
            SetHoveredHyperlink(null);
            return;
        }

        var textRect = GetTextRect();
        if (pointerPosition.X < textRect.X ||
            pointerPosition.Y < textRect.Y ||
            pointerPosition.X > textRect.X + textRect.Width ||
            pointerPosition.Y > textRect.Y + textRect.Height)
        {
            SetHoveredHyperlink(null);
            return;
        }

        var index = GetTextIndexFromPoint(pointerPosition);
        SetHoveredHyperlink(ResolveHyperlinkAtOffset(index));
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
        _pendingInvalidationReason = "Unspecified";
        if (!_preserveRenderDirtyBoundsHint)
        {
            _hasPendingRenderDirtyBoundsHint = false;
        }

        base.InvalidateVisual();
    }

    private void InvalidateVisualWithReason(string reason)
    {
        _pendingInvalidationReason = reason;
        InvalidateVisual();
    }

    private void InvalidateAfterTextMutation(string reason)
    {
        InvalidateMeasure();
        _scrollContentPresenter.InvalidateMeasure();
        _contentHost?.InvalidateScrollInfo();

        // Hosted document children only need a local placement refresh when the editor slot is stable.
        if (ContainsHostedDocumentChildren(Document) &&
            !TryRefreshHostedDocumentChildLayoutAfterTextMutation())
        {
            InvalidateArrange();
        }

        if (TryGetLocalizedTextDirtyBoundsHint(out var dirtyBounds))
        {
            InvalidateVisualWithDirtyBoundsHint(dirtyBounds, $"Edit:{reason}");
            return;
        }

        InvalidateVisualWithDirtyBoundsHint(LayoutSlot, $"Edit:{reason}");
    }

    private void InvalidateAfterDocumentChange()
    {
        if (_suppressMeasureInvalidationForDocumentBatch)
        {
            if (ContainsHostedDocumentChildren(Document) &&
                !TryRefreshHostedDocumentChildLayoutAfterTextMutation())
            {
                InvalidateArrange();
            }
        }
        else
        {
            InvalidateMeasure();
        }

        if (TryGetLocalizedTextDirtyBoundsHint(out var dirtyBounds))
        {
            InvalidateVisualWithDirtyBoundsHint(dirtyBounds, "DocumentChange");
            return;
        }

        InvalidateVisualWithDirtyBoundsHint(LayoutSlot, "DocumentChange");
    }

    private void InvalidateVisualWithDirtyBoundsHint(LayoutRect bounds, string reason)
    {
        _pendingInvalidationReason = reason;
        _pendingRenderDirtyBoundsHint = NormalizeRect(bounds);
        _hasPendingRenderDirtyBoundsHint = true;
        _preserveRenderDirtyBoundsHint = true;
        try
        {
            base.InvalidateVisual();
        }
        finally
        {
            _preserveRenderDirtyBoundsHint = false;
        }
    }

    private bool TryRefreshHostedDocumentChildLayoutAfterTextMutation()
    {
        if (_documentHostedVisualChildren.Count == 0 || NeedsMeasure || NeedsArrange)
        {
            return false;
        }

        var textRect = GetTextRect();
        if (textRect.Width <= 0f || textRect.Height <= 0f)
        {
            return false;
        }

        var layout = BuildOrGetLayout(textRect.Width);
        ClampScrollOffsets(layout, textRect);
        EnsureHostedDocumentChildLayout(textRect, layout);
        return true;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        var textWidth = TextWrapping == TextWrapping.NoWrap
            ? float.PositiveInfinity
            : Math.Max(0f, availableSize.X - Padding.Horizontal - (BorderThickness * 2f));
        var layout = BuildOrGetLayout(textWidth);
        _lastMeasuredLayout = layout;
        desired.X = Math.Max(desired.X, layout.ContentWidth + Padding.Horizontal + (BorderThickness * 2f));
        desired.Y = Math.Max(desired.Y, layout.ContentHeight + Padding.Vertical + (BorderThickness * 2f));
        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        EnsureHostedDocumentChildLayout();
        QueueViewportChangedNotification();
        return arranged;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        UpdateRichTextState(gameTime);
    }

    bool IUiRootUpdateParticipant.IsFrameUpdateActive => _hasPendingViewportChangedNotification || IsFocused;

    void IUiRootUpdateParticipant.UpdateFromUiRoot(GameTime gameTime)
    {
        RecordUpdateCallFromUiRoot();
        UpdateRichTextState(gameTime);
    }

    private void UpdateRichTextState(GameTime gameTime)
    {
        FlushPendingViewportChangedNotification();

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
        var hasTemplateRoot = HasTemplateRoot;
        if (hasTemplateRoot)
        {
            DrawTemplateVisualTree(spriteBatch);
        }
        else
        {
            UiDrawing.DrawFilledRect(spriteBatch, LayoutSlot, Background * Opacity);
            if (BorderThickness > 0f)
            {
                UiDrawing.DrawRectStroke(spriteBatch, LayoutSlot, BorderThickness, BorderBrush * Opacity);
            }
        }

        if (_contentHost != null)
        {
            var hostedRootRenderStartTicks = Stopwatch.GetTimestamp();
            _diagHostedRootRenderCallCount++;
            _runtimeHostedRootRenderCallCount++;
            var hostedTextRect = GetTextRect();
            if (hostedTextRect.Width > 0f && hostedTextRect.Height > 0f)
            {
                var hostedRootLayoutResolveStartTicks = Stopwatch.GetTimestamp();
                var hostedLayout = BuildOrGetLayout(hostedTextRect.Width);
                ClampScrollOffsets(hostedLayout, hostedTextRect);
                var hostedRootLayoutResolveElapsedTicks = Stopwatch.GetTimestamp() - hostedRootLayoutResolveStartTicks;
                _diagHostedRootRenderLayoutResolveElapsedTicks += hostedRootLayoutResolveElapsedTicks;
                _runtimeHostedRootRenderLayoutResolveElapsedTicks += hostedRootLayoutResolveElapsedTicks;
                EnsureHostedDocumentChildLayout(hostedTextRect, hostedLayout);
                CaptureDirtyHint(hostedLayout, hostedTextRect);
                _lastRenderedLayout = hostedLayout;
            }

            var hostedRootRenderElapsedTicks = Stopwatch.GetTimestamp() - hostedRootRenderStartTicks;
            _diagHostedRootRenderElapsedTicks += hostedRootRenderElapsedTicks;
            _runtimeHostedRootRenderElapsedTicks += hostedRootRenderElapsedTicks;

            return;
        }

        var renderStartTicks = Stopwatch.GetTimestamp();
        var layoutResolveMs = 0d;
        var selectionMs = 0d;
        var runsMs = 0d;
        var runCount = 0;
        var runCharacterCount = 0;
        var tableBordersMs = 0d;
        var caretMs = 0d;
        var hostedLayoutMs = 0d;
        var hostedChildrenDrawMs = 0d;
        var hostedChildrenDrawCount = 0;

        var textRect = GetTextRect();

        var layoutResolveStart = Stopwatch.GetTimestamp();
        var layout = BuildOrGetLayout(textRect.Width);
        ClampScrollOffsets(layout, textRect);
        layoutResolveMs = Stopwatch.GetElapsedTime(layoutResolveStart).TotalMilliseconds;

        RenderDocumentSurface(
            spriteBatch,
            textRect,
            layout,
            GetEffectiveHorizontalOffset(),
            GetEffectiveVerticalOffset(),
            includeHostedChildren: hasTemplateRoot,
            out selectionMs,
            out runsMs,
            out runCount,
            out runCharacterCount,
            out tableBordersMs,
            out caretMs,
            out hostedLayoutMs,
            out hostedChildrenDrawMs,
            out hostedChildrenDrawCount);

        CaptureDirtyHint(layout, textRect);
        _lastRenderedLayout = layout;
        _perfTracker.RecordRenderBreakdown(
            layoutResolveMs,
            selectionMs,
            runsMs,
            runCount,
            runCharacterCount,
            tableBordersMs,
            caretMs,
            hostedLayoutMs,
            hostedChildrenDrawMs,
            hostedChildrenDrawCount);
        _perfTracker.RecordRender(Stopwatch.GetElapsedTime(renderStartTicks).TotalMilliseconds);
    }

    protected override bool ShouldAutoDrawVisualChildren => !HasTemplateRoot;

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        foreach (var child in base.GetVisualChildren())
        {
            yield return child;
        }

        if (_documentHostedVisualChildren.Count > 0)
        {
            EnsureHostedDocumentChildLayout();
            yield return _hostedDocumentVisualHost;
        }
    }

    internal override int GetVisualChildCountForTraversal()
    {
        EnsureHostedDocumentChildLayout();
        return base.GetVisualChildCountForTraversal() + _documentHostedVisualChildren.Count;
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        EnsureHostedDocumentChildLayout();

        var baseCount = base.GetVisualChildCountForTraversal();
        if (index < baseCount)
        {
            return base.GetVisualChildAtForTraversal(index);
        }

        var hostedIndex = index - baseCount;
        if ((uint)hostedIndex < (uint)_documentHostedVisualChildren.Count)
        {
            return _documentHostedVisualChildren[hostedIndex];
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    internal override IEnumerable<UIElement> GetRetainedRenderChildren()
    {
        if (!HasTemplateRoot)
        {
            foreach (var child in base.GetRetainedRenderChildren())
            {
                yield return child;
            }
        }

        if (_documentHostedVisualChildren.Count > 0)
        {
            EnsureHostedDocumentChildLayout();
            yield return _hostedDocumentVisualHost;
        }
    }

    private void DrawTemplateVisualTree(SpriteBatch spriteBatch)
    {
        foreach (var child in base.GetVisualChildren())
        {
            child.Draw(spriteBatch);
        }
    }

    private void RenderDocumentSurface(
        SpriteBatch spriteBatch,
        LayoutRect textRect,
        DocumentLayoutResult layout,
        float horizontalOffset,
        float verticalOffset,
        bool includeHostedChildren)
    {
        RenderDocumentSurface(
            spriteBatch,
            textRect,
            layout,
            horizontalOffset,
            verticalOffset,
            includeHostedChildren,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _);
    }

    private void RenderDocumentSurface(
        SpriteBatch spriteBatch,
        LayoutRect textRect,
        DocumentLayoutResult layout,
        float horizontalOffset,
        float verticalOffset,
        bool includeHostedChildren,
        out double selectionMs,
        out double runsMs,
        out int runCount,
        out int runCharacterCount,
        out double tableBordersMs,
        out double caretMs,
        out double hostedLayoutMs,
        out double hostedChildrenDrawMs,
        out int hostedChildrenDrawCount)
    {
        selectionMs = 0d;
        runsMs = 0d;
        runCount = 0;
        runCharacterCount = 0;
        tableBordersMs = 0d;
        caretMs = 0d;
        hostedLayoutMs = 0d;
        hostedChildrenDrawMs = 0d;
        hostedChildrenDrawCount = 0;

        UiDrawing.PushClip(spriteBatch, textRect);
        try
        {
            var selectionStart = Stopwatch.GetTimestamp();
            DrawSelection(spriteBatch, textRect, layout, horizontalOffset, verticalOffset);
            selectionMs = Stopwatch.GetElapsedTime(selectionStart).TotalMilliseconds;

            var runsStart = Stopwatch.GetTimestamp();
            if (TryGetVisibleLineRange(layout, textRect, verticalOffset, out var firstVisibleLineIndex, out var lastVisibleLineIndex))
            {
                for (var lineIndex = firstVisibleLineIndex; lineIndex <= lastVisibleLineIndex; lineIndex++)
                {
                    var line = layout.Lines[lineIndex];
                    var lineBottom = textRect.Y + line.Bounds.Y + line.Bounds.Height - verticalOffset;
                    var lineTop = textRect.Y + line.Bounds.Y - verticalOffset;
                    if (lineBottom < textRect.Y || lineTop > textRect.Y + textRect.Height)
                    {
                        continue;
                    }

                    for (var runIndex = 0; runIndex < line.Runs.Count; runIndex++)
                    {
                        var run = line.Runs[runIndex];
                        if (string.IsNullOrEmpty(run.Text))
                        {
                            continue;
                        }

                        var color = ResolveRunColor(run.Style);
                        runCount++;
                        runCharacterCount += run.Text.Length;

                        var position = new Vector2(textRect.X + run.Bounds.X - horizontalOffset, textRect.Y + run.Bounds.Y - verticalOffset);
                        DrawRunText(spriteBatch, run, position, color);
                        DrawRunUnderline(spriteBatch, run, position, color);
                    }
                }
            }

            runsMs = Stopwatch.GetElapsedTime(runsStart).TotalMilliseconds;

            var tableBordersStart = Stopwatch.GetTimestamp();
            DrawTableBorders(spriteBatch, textRect, layout, horizontalOffset, verticalOffset);
            tableBordersMs = Stopwatch.GetElapsedTime(tableBordersStart).TotalMilliseconds;
            if (IsFocused && _isCaretVisible && (!IsReadOnly || IsReadOnlyCaretVisible))
            {
                var caretStart = Stopwatch.GetTimestamp();
                DrawCaret(spriteBatch, textRect, layout, horizontalOffset, verticalOffset);
                caretMs = Stopwatch.GetElapsedTime(caretStart).TotalMilliseconds;
            }

            if (includeHostedChildren)
            {
                var hostedLayoutStart = Stopwatch.GetTimestamp();
                EnsureHostedDocumentChildLayout(textRect, layout);
                hostedLayoutMs = Stopwatch.GetElapsedTime(hostedLayoutStart).TotalMilliseconds;
                var hostedChildrenDrawStart = Stopwatch.GetTimestamp();
                if (_documentHostedVisualChildren.Count > 0)
                {
                    hostedChildrenDrawCount = _documentHostedVisualChildren.Count;
                    _hostedDocumentVisualHost.Draw(spriteBatch);
                }

                hostedChildrenDrawMs = Stopwatch.GetElapsedTime(hostedChildrenDrawStart).TotalMilliseconds;
            }
        }
        finally
        {
            UiDrawing.PopClip(spriteBatch);
        }
    }

    private void OnDocumentPropertyChanged(FlowDocument? oldDocument, FlowDocument? newDocument)
    {
        SetHoveredHyperlink(null);
        if (oldDocument != null)
        {
            oldDocument.Changed -= OnDocumentChanged;
        }

        var active = newDocument ?? CreateDefaultDocument();
        active.Changed += OnDocumentChanged;
        var currentDocumentRichness = CaptureDocumentRichness(Document);
        PerformDocumentMaintenance(currentDocumentRichness);
        _layoutCache.Invalidate();
        _lastMeasuredLayout = null;
        RaiseRoutedEventInternal(DocumentChangedEvent, new RoutedSimpleEventArgs(DocumentChangedEvent));
        RaiseTextChangedEvent();
        InvalidateAfterDocumentChange();
        _lastDocumentRichness = currentDocumentRichness;
        RecordOperation("DocumentPropertyChanged", $"blocks={Document.Blocks.Count}");
    }

    private void OnDocumentChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        if (_documentChangeBatchDepth > 0)
        {
            _hasPendingDocumentChangedEvent = true;
            _hasPendingTextChangedEvent = true;
            _hasPendingDocumentMaintenanceWork = true;
            return;
        }

        var currentDocumentRichness = CaptureDocumentRichness(Document);
        PerformDocumentMaintenance(currentDocumentRichness);
        _layoutCache.Invalidate();
        _lastMeasuredLayout = null;
        RaiseRoutedEventInternal(DocumentChangedEvent, new RoutedSimpleEventArgs(DocumentChangedEvent));
        RaiseTextChangedEvent();
        InvalidateAfterDocumentChange();
        _lastDocumentRichness = currentDocumentRichness;
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

    private void UpdateSelectionState(int selectionAnchor, int caretIndex, bool ensureCaretVisible)
    {
        var previousStart = SelectionStart;
        var previousLength = SelectionLength;
        _selectionAnchor = selectionAnchor;
        _caretIndex = caretIndex;
        if (ensureCaretVisible)
        {
            EnsureCaretVisible();
        }

        RaiseSelectionChangedEventIfNeeded(previousStart, previousLength);
    }

    private void RaiseTextChangedEvent()
    {
        RaiseRoutedEventInternal(TextChangedEvent, new RoutedSimpleEventArgs(TextChangedEvent));
    }

    private void ExecuteDocumentChangeBatch(Action action)
    {
        BeginDocumentChangeBatch();
        try
        {
            action();
        }
        finally
        {
            EndDocumentChangeBatch();
        }
    }

    private T ExecuteDocumentChangeBatch<T>(Func<T> action)
    {
        BeginDocumentChangeBatch();
        try
        {
            return action();
        }
        finally
        {
            EndDocumentChangeBatch();
        }
    }

    private void ExecuteTextMutationBatch(Action action)
    {
        var previous = _suppressMeasureInvalidationForDocumentBatch;
        _suppressMeasureInvalidationForDocumentBatch = true;
        try
        {
            ExecuteDocumentChangeBatch(action);
        }
        finally
        {
            _suppressMeasureInvalidationForDocumentBatch = previous;
        }
    }

    private T ExecuteTextMutationBatch<T>(Func<T> action)
    {
        var previous = _suppressMeasureInvalidationForDocumentBatch;
        _suppressMeasureInvalidationForDocumentBatch = true;
        try
        {
            return ExecuteDocumentChangeBatch(action);
        }
        finally
        {
            _suppressMeasureInvalidationForDocumentBatch = previous;
        }
    }

    private void BeginDocumentChangeBatch()
    {
        _documentChangeBatchDepth++;
    }

    private void EndDocumentChangeBatch()
    {
        if (_documentChangeBatchDepth <= 0)
        {
            return;
        }

        _documentChangeBatchDepth--;
        if (_documentChangeBatchDepth == 0)
        {
            FlushPendingDocumentChangeEvents();
        }
    }

    private void FlushPendingDocumentChangeEvents()
    {
        if (!_hasPendingDocumentChangedEvent && !_hasPendingTextChangedEvent)
        {
            return;
        }

        var flushStart = Stopwatch.GetTimestamp();
        var raiseTextChanged = _hasPendingTextChangedEvent;
        var currentDocumentRichness = _lastDocumentRichness;
        var maintenanceMs = 0d;
        var documentChangedEventMs = 0d;
        var textChangedEventMs = 0d;
        var invalidateAfterDocumentChangeMs = 0d;
        _hasPendingDocumentChangedEvent = false;
        _hasPendingTextChangedEvent = false;
        if (_hasPendingDocumentMaintenanceWork)
        {
            _hasPendingDocumentMaintenanceWork = false;
            var maintenanceStart = Stopwatch.GetTimestamp();
            currentDocumentRichness = CaptureDocumentRichness(Document);
            PerformDocumentMaintenance(currentDocumentRichness);
            maintenanceMs = Stopwatch.GetElapsedTime(maintenanceStart).TotalMilliseconds;
        }

        _layoutCache.Invalidate();
        _lastMeasuredLayout = null;
        var documentChangedEventStart = Stopwatch.GetTimestamp();
        RaiseRoutedEventInternal(DocumentChangedEvent, new RoutedSimpleEventArgs(DocumentChangedEvent));
        documentChangedEventMs = Stopwatch.GetElapsedTime(documentChangedEventStart).TotalMilliseconds;
        if (raiseTextChanged)
        {
            var textChangedEventStart = Stopwatch.GetTimestamp();
            RaiseTextChangedEvent();
            textChangedEventMs = Stopwatch.GetElapsedTime(textChangedEventStart).TotalMilliseconds;
        }

        var invalidateAfterDocumentChangeStart = Stopwatch.GetTimestamp();
        InvalidateAfterDocumentChange();
        invalidateAfterDocumentChangeMs = Stopwatch.GetElapsedTime(invalidateAfterDocumentChangeStart).TotalMilliseconds;
        _lastDocumentRichness = currentDocumentRichness;
        _perfTracker.RecordStructuredEnterFlushBreakdown(
            Stopwatch.GetElapsedTime(flushStart).TotalMilliseconds,
            maintenanceMs,
            documentChangedEventMs,
            textChangedEventMs,
            invalidateAfterDocumentChangeMs);
    }

    private void PerformDocumentMaintenance(DocumentRichnessSnapshot currentDocumentRichness)
    {
        if (_lastDocumentRichness.HostedChildCount > 0 ||
            currentDocumentRichness.HostedChildCount > 0 ||
            _documentHostedVisualChildren.Count > 0)
        {
            SyncHostedDocumentChildren();
        }

        if (_lastDocumentRichness.HyperlinkCount > 0 ||
            currentDocumentRichness.HyperlinkCount > 0 ||
            _appliedImplicitHyperlinkStyles.Count > 0 ||
            _hoveredHyperlink != null)
        {
            ApplyHyperlinkImplicitStyles();
        }

        ClampSelectionToTextLength();
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

    private bool ScrollBy(float horizontalDelta, float verticalDelta, string reason)
    {
        return SetScrollOffsets(GetEffectiveHorizontalOffset() + horizontalDelta, GetEffectiveVerticalOffset() + verticalDelta, reason);
    }

    private bool SetScrollOffsets(float horizontalOffset, float verticalOffset, string reason)
    {
        var metrics = GetScrollMetrics();
        var clampedHorizontal = Math.Clamp(horizontalOffset, 0f, metrics.ScrollableWidth);
        var clampedVertical = Math.Clamp(verticalOffset, 0f, metrics.ScrollableHeight);
        var currentHorizontal = GetEffectiveHorizontalOffset();
        var currentVertical = GetEffectiveVerticalOffset();
        if (Math.Abs(clampedHorizontal - currentHorizontal) <= 0.01f &&
            Math.Abs(clampedVertical - currentVertical) <= 0.01f)
        {
            return false;
        }

        if (_contentHost != null && HasUsableContentHostMetrics())
        {
            _hasPendingContentHostScrollOffsets = false;
            _contentHost.ScrollToHorizontalOffset(clampedHorizontal);
            _contentHost.ScrollToVerticalOffset(clampedVertical);
            NotifyViewportChangedIfNeeded(ViewportNotificationSource.SetScrollOffsets);
            EnsureHostedDocumentChildLayout();
            return true;
        }

        _horizontalOffset = clampedHorizontal;
        _verticalOffset = clampedVertical;
        _hasPendingContentHostScrollOffsets = _contentHost != null;
        NotifyViewportChangedIfNeeded(ViewportNotificationSource.SetScrollOffsets);
        InvalidateVisualWithReason(reason);
        return true;
    }

    private void QueueViewportChangedNotification()
    {
        _diagQueueViewportChangedNotificationCallCount++;
        _runtimeQueueViewportChangedNotificationCallCount++;
        if (_hasPendingViewportChangedNotification)
        {
            _diagQueueViewportChangedNotificationAlreadyPendingCount++;
            _runtimeQueueViewportChangedNotificationAlreadyPendingCount++;
        }

        _hasPendingViewportChangedNotification = true;
    }

    private void FlushPendingViewportChangedNotification()
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagFlushPendingViewportChangedNotificationCallCount++;
        _runtimeFlushPendingViewportChangedNotificationCallCount++;
        if (!_hasPendingViewportChangedNotification)
        {
            _diagFlushPendingViewportChangedNotificationSkippedNoPendingCount++;
            _runtimeFlushPendingViewportChangedNotificationSkippedNoPendingCount++;
            var elapsedNoPendingTicks = Stopwatch.GetTimestamp() - startTicks;
            _diagFlushPendingViewportChangedNotificationElapsedTicks += elapsedNoPendingTicks;
            _runtimeFlushPendingViewportChangedNotificationElapsedTicks += elapsedNoPendingTicks;
            return;
        }

        _hasPendingViewportChangedNotification = false;
        var notifyStartTicks = Stopwatch.GetTimestamp();
        NotifyViewportChangedIfNeeded(ViewportNotificationSource.PendingFlush);
        var notifyElapsedTicks = Stopwatch.GetTimestamp() - notifyStartTicks;
        _diagFlushPendingViewportChangedNotificationNotifyElapsedTicks += notifyElapsedTicks;
        _runtimeFlushPendingViewportChangedNotificationNotifyElapsedTicks += notifyElapsedTicks;
        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _diagFlushPendingViewportChangedNotificationElapsedTicks += elapsedTicks;
        _runtimeFlushPendingViewportChangedNotificationElapsedTicks += elapsedTicks;
    }

    private void NotifyViewportChangedIfNeeded(ViewportNotificationSource source = ViewportNotificationSource.Unknown)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagNotifyViewportChangedCallCount++;
        _runtimeNotifyViewportChangedCallCount++;
        TrackNotifyViewportChangedCallSource(source);
        var metrics = GetScrollMetrics();
        var changeMask = GetViewportMetricChangeMask(
            metrics,
            _lastViewportChangedHorizontalOffset,
            _lastViewportChangedVerticalOffset,
            _lastViewportChangedViewportWidth,
            _lastViewportChangedViewportHeight,
            _lastViewportChangedExtentWidth,
            _lastViewportChangedExtentHeight);
        if (changeMask == ViewportMetricChangeMask.None)
        {
            _diagNotifyViewportChangedSkippedNoChangeCount++;
            _runtimeNotifyViewportChangedSkippedNoChangeCount++;
            var skippedElapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _diagNotifyViewportChangedElapsedTicks += skippedElapsedTicks;
            _runtimeNotifyViewportChangedElapsedTicks += skippedElapsedTicks;
            return;
        }

        _lastViewportChangedHorizontalOffset = metrics.HorizontalOffset;
        _lastViewportChangedVerticalOffset = metrics.VerticalOffset;
        _lastViewportChangedViewportWidth = metrics.ViewportWidth;
        _lastViewportChangedViewportHeight = metrics.ViewportHeight;
        _lastViewportChangedExtentWidth = metrics.ExtentWidth;
        _lastViewportChangedExtentHeight = metrics.ExtentHeight;
        _diagNotifyViewportChangedRaisedCount++;
        _runtimeNotifyViewportChangedRaisedCount++;
        TrackNotifyViewportChangedRaise(source, changeMask, metrics);
        var subscriberStartTicks = Stopwatch.GetTimestamp();
        ViewportChanged?.Invoke(this, EventArgs.Empty);
        var subscriberElapsedTicks = Stopwatch.GetTimestamp() - subscriberStartTicks;
        _diagNotifyViewportChangedSubscriberElapsedTicks += subscriberElapsedTicks;
        _runtimeNotifyViewportChangedSubscriberElapsedTicks += subscriberElapsedTicks;
        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _diagNotifyViewportChangedElapsedTicks += elapsedTicks;
        _runtimeNotifyViewportChangedElapsedTicks += elapsedTicks;
    }

    private RichTextBoxScrollMetrics GetScrollMetrics()
    {
        if (_contentHost != null && HasUsableContentHostMetrics())
        {
            return new RichTextBoxScrollMetrics(
                _contentHost.HorizontalOffset,
                _contentHost.VerticalOffset,
                _contentHost.ViewportWidth,
                _contentHost.ViewportHeight,
                _contentHost.ExtentWidth,
                _contentHost.ExtentHeight);
        }

        var textRect = GetTextRect();
        if (textRect.Width <= 0f || textRect.Height <= 0f)
        {
            return new RichTextBoxScrollMetrics(_horizontalOffset, _verticalOffset, 0f, 0f, 0f, 0f);
        }

        var layout = BuildOrGetLayout(textRect.Width);
        ClampScrollOffsets(layout, textRect);
        return new RichTextBoxScrollMetrics(
            _horizontalOffset,
            _verticalOffset,
            textRect.Width,
            textRect.Height,
            layout.ContentWidth,
            layout.ContentHeight);
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
        CommandBindings.Add(new CommandBinding(EditingCommands.DeletePreviousWord, (_, _) => ExecuteDeletePreviousWord(), (_, args) => args.CanExecute = CanBackspace()));
        CommandBindings.Add(new CommandBinding(EditingCommands.DeleteNextWord, (_, _) => ExecuteDeleteNextWord(), (_, args) => args.CanExecute = CanDelete()));
        CommandBindings.Add(new CommandBinding(EditingCommands.EnterParagraphBreak, (_, _) => ExecuteEnterParagraphBreak(), (_, args) => args.CanExecute = CanMutateText()));
        CommandBindings.Add(new CommandBinding(EditingCommands.EnterLineBreak, (_, _) => ExecuteEnterLineBreak(), (_, args) => args.CanExecute = CanMutateText()));
        CommandBindings.Add(new CommandBinding(EditingCommands.TabForward, (_, _) => ExecuteTabForward(), (_, args) => args.CanExecute = CanMutateText()));
        CommandBindings.Add(new CommandBinding(EditingCommands.TabBackward, (_, _) => ExecuteTabBackward(), (_, args) => args.CanExecute = CanTabBackward()));

        CommandBindings.Add(new CommandBinding(EditingCommands.Copy, (_, _) => ExecuteCopy(), (_, args) => args.CanExecute = SelectionLength > 0));
        CommandBindings.Add(new CommandBinding(EditingCommands.Cut, (_, _) => ExecuteCut(), (_, args) => args.CanExecute = !IsReadOnly && SelectionLength > 0));
        CommandBindings.Add(new CommandBinding(EditingCommands.Paste, (_, _) => ExecutePaste(), (_, args) => args.CanExecute = !IsReadOnly && CanPasteFromClipboard()));

        CommandBindings.Add(new CommandBinding(EditingCommands.SelectAll, (_, _) => ExecuteSelectAllCore(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveLeftByCharacter, (_, _) => ExecuteMoveLeftByCharacter(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveRightByCharacter, (_, _) => ExecuteMoveRightByCharacter(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveLeftByWord, (_, _) => ExecuteMoveLeftByWord(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveRightByWord, (_, _) => ExecuteMoveRightByWord(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectLeftByCharacter, (_, _) => ExecuteSelectLeftByCharacter(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectRightByCharacter, (_, _) => ExecuteSelectRightByCharacter(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectLeftByWord, (_, _) => ExecuteSelectLeftByWord(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectRightByWord, (_, _) => ExecuteSelectRightByWord(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveUpByLine, (_, _) => ExecuteMoveUpByLine(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveDownByLine, (_, _) => ExecuteMoveDownByLine(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveUpByPage, (_, _) => ExecuteMoveUpByPage(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveDownByPage, (_, _) => ExecuteMoveDownByPage(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveUpByParagraph, (_, _) => ExecuteMoveUpByParagraph(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveDownByParagraph, (_, _) => ExecuteMoveDownByParagraph(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveToLineStart, (_, _) => ExecuteMoveToLineStart(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveToLineEnd, (_, _) => ExecuteMoveToLineEnd(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveToParagraphStart, (_, _) => ExecuteMoveToParagraphStart(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveToParagraphEnd, (_, _) => ExecuteMoveToParagraphEnd(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveToDocumentStart, (_, _) => ExecuteMoveToDocumentStart(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveToDocumentEnd, (_, _) => ExecuteMoveToDocumentEnd(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectUpByLine, (_, _) => ExecuteSelectUpByLine(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectDownByLine, (_, _) => ExecuteSelectDownByLine(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectUpByPage, (_, _) => ExecuteSelectUpByPage(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectDownByPage, (_, _) => ExecuteSelectDownByPage(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectUpByParagraph, (_, _) => ExecuteSelectUpByParagraph(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectDownByParagraph, (_, _) => ExecuteSelectDownByParagraph(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectToLineStart, (_, _) => ExecuteSelectToLineStart(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectToLineEnd, (_, _) => ExecuteSelectToLineEnd(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectToParagraphStart, (_, _) => ExecuteSelectToParagraphStart(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectToParagraphEnd, (_, _) => ExecuteSelectToParagraphEnd(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectToDocumentStart, (_, _) => ExecuteSelectToDocumentStart(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectToDocumentEnd, (_, _) => ExecuteSelectToDocumentEnd(), (_, args) => args.CanExecute = true));

        CommandBindings.Add(new CommandBinding(EditingCommands.ToggleBold, (_, _) => ExecuteToggleBold(), (_, args) => args.CanExecute = !IsReadOnly));
        CommandBindings.Add(new CommandBinding(EditingCommands.ToggleItalic, (_, _) => ExecuteToggleItalic(), (_, args) => args.CanExecute = !IsReadOnly));
        CommandBindings.Add(new CommandBinding(EditingCommands.ToggleUnderline, (_, _) => ExecuteToggleUnderline(), (_, args) => args.CanExecute = !IsReadOnly));
        CommandBindings.Add(new CommandBinding(EditingCommands.ToggleBullets, (_, _) => ExecuteToggleBullets(), (_, args) => args.CanExecute = CanExecuteListStyleToggle()));
        CommandBindings.Add(new CommandBinding(EditingCommands.ToggleNumbering, (_, _) => ExecuteToggleNumbering(), (_, args) => args.CanExecute = CanExecuteListStyleToggle()));

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
        AddEditingKeyBinding(Keys.Back, ModifierKeys.Control, EditingCommands.DeletePreviousWord);
        AddEditingKeyBinding(Keys.Delete, ModifierKeys.Control, EditingCommands.DeleteNextWord);
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
        AddEditingKeyBinding(Keys.Up, ModifierKeys.None, EditingCommands.MoveUpByLine);
        AddEditingKeyBinding(Keys.Down, ModifierKeys.None, EditingCommands.MoveDownByLine);
        AddEditingKeyBinding(Keys.PageUp, ModifierKeys.None, EditingCommands.MoveUpByPage);
        AddEditingKeyBinding(Keys.PageDown, ModifierKeys.None, EditingCommands.MoveDownByPage);
        AddEditingKeyBinding(Keys.Up, ModifierKeys.Control, EditingCommands.MoveUpByParagraph);
        AddEditingKeyBinding(Keys.Down, ModifierKeys.Control, EditingCommands.MoveDownByParagraph);
        AddEditingKeyBinding(Keys.Home, ModifierKeys.None, EditingCommands.MoveToLineStart);
        AddEditingKeyBinding(Keys.End, ModifierKeys.None, EditingCommands.MoveToLineEnd);
        AddEditingKeyBinding(Keys.Home, ModifierKeys.Control, EditingCommands.MoveToDocumentStart);
        AddEditingKeyBinding(Keys.End, ModifierKeys.Control, EditingCommands.MoveToDocumentEnd);
        AddEditingKeyBinding(Keys.Left, ModifierKeys.Shift, EditingCommands.SelectLeftByCharacter);
        AddEditingKeyBinding(Keys.Right, ModifierKeys.Shift, EditingCommands.SelectRightByCharacter);
        AddEditingKeyBinding(Keys.Left, ModifierKeys.Control | ModifierKeys.Shift, EditingCommands.SelectLeftByWord);
        AddEditingKeyBinding(Keys.Right, ModifierKeys.Control | ModifierKeys.Shift, EditingCommands.SelectRightByWord);
        AddEditingKeyBinding(Keys.Up, ModifierKeys.Shift, EditingCommands.SelectUpByLine);
        AddEditingKeyBinding(Keys.Down, ModifierKeys.Shift, EditingCommands.SelectDownByLine);
        AddEditingKeyBinding(Keys.PageUp, ModifierKeys.Shift, EditingCommands.SelectUpByPage);
        AddEditingKeyBinding(Keys.PageDown, ModifierKeys.Shift, EditingCommands.SelectDownByPage);
        AddEditingKeyBinding(Keys.Up, ModifierKeys.Control | ModifierKeys.Shift, EditingCommands.SelectUpByParagraph);
        AddEditingKeyBinding(Keys.Down, ModifierKeys.Control | ModifierKeys.Shift, EditingCommands.SelectDownByParagraph);
        AddEditingKeyBinding(Keys.Home, ModifierKeys.Shift, EditingCommands.SelectToLineStart);
        AddEditingKeyBinding(Keys.End, ModifierKeys.Shift, EditingCommands.SelectToLineEnd);
        AddEditingKeyBinding(Keys.Home, ModifierKeys.Control | ModifierKeys.Shift, EditingCommands.SelectToDocumentStart);
        AddEditingKeyBinding(Keys.End, ModifierKeys.Control | ModifierKeys.Shift, EditingCommands.SelectToDocumentEnd);

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
        if ((key == Keys.Enter && (modifiers & ModifierKeys.Control) == 0 && !AcceptsReturn) ||
            (key == Keys.Tab && !AcceptsTab))
        {
            return false;
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

        RemoveFullyDeletedTopLevelParagraphs(afterDocument, entries, paragraphs, start, end);
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
        ExecuteTextMutationBatch(() =>
        {
            session.ApplyOperation(new ReplaceDocumentOperation("Document", beforeText, afterText, beforeDocument, afterDocument));
            session.CommitTransaction();
        });
        _caretIndex = caretAfter;
        _selectionAnchor = _caretIndex;
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        _perfTracker.RecordEdit(elapsedMs);
        TraceInvariants(commandType);
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateAfterTextMutation(commandType);
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
                append(new InlineUIContainer
                {
                    Child = ((InlineUIContainer)inline).Child
                });
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

    private static void CopyParagraphSettings(Paragraph source, Paragraph destination)
    {
        destination.DefaultIncrementalTab = source.DefaultIncrementalTab;
        for (var i = 0; i < source.Tabs.Count; i++)
        {
            var tab = source.Tabs[i];
            destination.Tabs.Add(new TextTabProperties(tab.Alignment, tab.Location, tab.TabLeader, tab.AligningCharacter));
        }
    }

    private static Inline CreateReplacementInlinePreservingContext(Paragraph source, int offset, string text)
    {
        var clampedOffset = Math.Clamp(offset, 0, GetParagraphLogicalLength(source));
        var cursor = 0;
        for (var i = 0; i < source.Inlines.Count; i++)
        {
            if (TryCreateInlineWithInheritedFormatting(source.Inlines[i], clampedOffset, ref cursor, text, out var replacement))
            {
                return replacement;
            }
        }

        return new Run(text);
    }

    private static bool TryCreateInlineWithInheritedFormatting(Inline inline, int offset, ref int cursor, string text, out Inline replacement)
    {
        var inlineStart = cursor;
        var inlineLength = GetInlineLogicalLength(inline);
        var inlineEnd = inlineStart + inlineLength;
        cursor = inlineEnd;
        if (offset < inlineStart || offset > inlineEnd)
        {
            replacement = null!;
            return false;
        }

        switch (inline)
        {
            case Run run:
                replacement = new Run(text)
                {
                    Foreground = run.Foreground
                };
                return true;
            case Span span:
            {
                var nestedCursor = inlineStart;
                for (var i = 0; i < span.Inlines.Count; i++)
                {
                    if (!TryCreateInlineWithInheritedFormatting(span.Inlines[i], offset, ref nestedCursor, text, out var nestedReplacement))
                    {
                        continue;
                    }

                    var clonedSpan = CloneSpanShell(span);
                    clonedSpan.Inlines.Add(nestedReplacement);
                    replacement = clonedSpan;
                    return true;
                }

                var shell = CloneSpanShell(span);
                shell.Inlines.Add(new Run(text));
                replacement = shell;
                return true;
            }
            default:
                replacement = new Run(text);
                return true;
        }
    }

    private static void NormalizeAdjacentInlines(Paragraph paragraph)
    {
        var normalized = NormalizeInlineSequence(paragraph.Inlines);
        paragraph.Inlines.Clear();
        for (var i = 0; i < normalized.Count; i++)
        {
            paragraph.Inlines.Add(normalized[i]);
        }
    }

    private static List<Inline> NormalizeInlineSequence(IEnumerable<Inline> source)
    {
        var normalized = new List<Inline>();
        foreach (var inline in source)
        {
            var current = NormalizeInline(inline);
            if (normalized.Count > 0 && TryMergeAdjacentInlines(normalized[^1], current))
            {
                continue;
            }

            normalized.Add(current);
        }

        return normalized;
    }

    private static Inline NormalizeInline(Inline inline)
    {
        if (inline is not Span span)
        {
            return inline;
        }

        var normalizedChildren = NormalizeInlineSequence(span.Inlines);
        span.Inlines.Clear();
        for (var i = 0; i < normalizedChildren.Count; i++)
        {
            span.Inlines.Add(normalizedChildren[i]);
        }

        return span;
    }

    private static bool TryMergeAdjacentInlines(Inline previous, Inline current)
    {
        if (previous is Run previousRun && current is Run currentRun && previousRun.Foreground == currentRun.Foreground)
        {
            previousRun.Text += currentRun.Text;
            return true;
        }

        if (previous is not Span previousSpan || current is not Span currentSpan || !CanMergeSpanShells(previousSpan, currentSpan))
        {
            return false;
        }

        while (currentSpan.Inlines.Count > 0)
        {
            var child = currentSpan.Inlines[0];
            currentSpan.Inlines.RemoveAt(0);
            previousSpan.Inlines.Add(child);
        }

        return true;
    }

    private static bool CanMergeSpanShells(Span left, Span right)
    {
        if (left.GetType() != right.GetType())
        {
            return false;
        }

        if (left is Hyperlink leftHyperlink && right is Hyperlink rightHyperlink)
        {
            return string.Equals(leftHyperlink.NavigateUri, rightHyperlink.NavigateUri, StringComparison.Ordinal);
        }

        return true;
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
        var keepTrailingEmptyParagraph = true;
        for (var i = document.Blocks.Count - 1; i >= 0; i--)
        {
            if (document.Blocks[i] is not Paragraph paragraph)
            {
                break;
            }

            if (!string.IsNullOrWhiteSpace(FlowDocumentPlainText.GetInlineText(paragraph.Inlines)))
            {
                break;
            }

            if (keepTrailingEmptyParagraph)
            {
                keepTrailingEmptyParagraph = false;
                continue;
            }

            if (document.Blocks.Count > 1)
            {
                document.Blocks.RemoveAt(i);
            }
        }
    }

    private static void RemoveFullyDeletedTopLevelParagraphs(
        FlowDocument document,
        IReadOnlyList<ParagraphSelectionEntry> entries,
        IReadOnlyList<Paragraph> paragraphs,
        int selectionStart,
        int selectionEnd)
    {
        for (var i = paragraphs.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(paragraphs[i].Parent, document))
            {
                continue;
            }

            if (!ShouldRemoveFullyDeletedTopLevelParagraph(entries, i, selectionStart, selectionEnd))
            {
                continue;
            }

            document.Blocks.Remove(paragraphs[i]);
        }

        if (document.Blocks.Count == 0)
        {
            document.Blocks.Add(CreateParagraph(string.Empty));
        }
    }

    private static bool ShouldRemoveFullyDeletedTopLevelParagraph(
        IReadOnlyList<ParagraphSelectionEntry> entries,
        int paragraphIndex,
        int selectionStart,
        int selectionEnd)
    {
        if (paragraphIndex < 0 || paragraphIndex >= entries.Count - 1)
        {
            return false;
        }

        var entry = entries[paragraphIndex];
        var paragraphFullySelected = selectionStart <= entry.StartOffset && selectionEnd >= entry.EndOffset;
        if (!paragraphFullySelected)
        {
            return false;
        }

        return selectionEnd > entry.EndOffset;
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
                UpdateSelectionState(start + 1, start + 1, ensureCaretVisible: false);
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
        PublishRichClipboardPayloads(richSlice, selected);
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

        var pasteStart = Stopwatch.GetTimestamp();
        var selectionStartBefore = SelectionStart;
        var selectionLengthBefore = SelectionLength;
        var caretBefore = _caretIndex;
        var textLengthBefore = GetText().Length;
        var structureBefore = CaptureDocumentRichness(Document).ToSummary();
        var clipboardBefore = TextClipboard.GetSnapshot();
        var clipboardSnapshot = TextClipboard.CaptureSnapshot();
        var fallbackToText = false;
        var route = "Noop";
        var richFormatName = "None";
        var payloadBytes = 0;
        var pastedChars = 0;
        var normalizeMs = 0d;
        var structuredTextCompositionCount = 0;
        var structuredEnterCount = 0;
        var structuredDeleteSelectionApplied = false;
        var lookupRichMs = 0d;
        var deserializeMs = 0d;
        var readTextMs = 0d;
        var structuredInsertMs = 0d;
        var replaceSelectionMs = 0d;
        var richStructuredTarget = IsRichStructuredDocument() && !IsFullDocumentSelection();
        void EmitPasteCpu()
        {
            var clipboardAfter = TextClipboard.GetSnapshot();
            var selectionStartAfter = SelectionStart;
            var selectionLengthAfter = SelectionLength;
            var caretAfter = _caretIndex;
            var textLengthAfter = GetText().Length;
            var structureAfter = CaptureDocumentRichness(Document).ToSummary();
        }

        var lookupRichStart = Stopwatch.GetTimestamp();
        if (TryGetRichClipboardPayload(clipboardSnapshot, out var richPayload, out var richFormat))
        {
            lookupRichMs = Stopwatch.GetElapsedTime(lookupRichStart).TotalMilliseconds;
            richFormatName = richFormat;
            payloadBytes = Encoding.UTF8.GetByteCount(richPayload);
            var deserializeStart = Stopwatch.GetTimestamp();
            try
            {
                var fragment = DeserializeFragmentPayload(richPayload, richFormat);
                deserializeMs = Stopwatch.GetElapsedTime(deserializeStart).TotalMilliseconds;
                _perfTracker.RecordClipboardDeserialize(deserializeMs);
                if (!richStructuredTarget && TryPasteRichFragment(fragment))
                {
                    route = "RichFragment";
                    pastedChars = DocumentEditing.GetText(fragment).Length;
                    EmitPasteCpu();
                    return;
                }

                fallbackToText = true;
                var normalizeStart = Stopwatch.GetTimestamp();
                var fragmentText = NormalizeNewlines(DocumentEditing.GetText(fragment));
                normalizeMs += Stopwatch.GetElapsedTime(normalizeStart).TotalMilliseconds;
                pastedChars = fragmentText.Length;
                var structuredStart = Stopwatch.GetTimestamp();
                if (richStructuredTarget && TryPastePlainTextPreservingStructure(fragmentText, out var structuredStats))
                {
                    route = "RichFallbackTextStructured";
                    structuredInsertMs += Stopwatch.GetElapsedTime(structuredStart).TotalMilliseconds;
                    structuredTextCompositionCount += structuredStats.TextCompositionCount;
                    structuredEnterCount += structuredStats.EnterCount;
                    structuredDeleteSelectionApplied |= structuredStats.DeleteSelectionApplied;
                    EmitPasteCpu();
                    return;
                }

            }
            catch (Exception)
            {
                deserializeMs = Stopwatch.GetElapsedTime(deserializeStart).TotalMilliseconds;
                _perfTracker.RecordClipboardDeserialize(deserializeMs);
                fallbackToText = true;
                // Fall through to text fallback when rich payload is invalid.
            }
        }
        else
        {
            lookupRichMs = Stopwatch.GetElapsedTime(lookupRichStart).TotalMilliseconds;
        }

        var readTextStart = Stopwatch.GetTimestamp();
        if (!clipboardSnapshot.TryGetText(out var pasted))
        {
            readTextMs = Stopwatch.GetElapsedTime(readTextStart).TotalMilliseconds;
            route = "ClipboardEmpty";
            EmitPasteCpu();
            return;
        }
        readTextMs = Stopwatch.GetElapsedTime(readTextStart).TotalMilliseconds;
        pastedChars = pasted.Length;
        payloadBytes = Math.Max(payloadBytes, Encoding.UTF8.GetByteCount(pasted));

        var normalizeTextStart = Stopwatch.GetTimestamp();
        var normalizedPasted = NormalizeNewlines(pasted);
        normalizeMs += Stopwatch.GetElapsedTime(normalizeTextStart).TotalMilliseconds;
        pastedChars = normalizedPasted.Length;
        var structuredTextStart = Stopwatch.GetTimestamp();
        if (richStructuredTarget && TryPastePlainTextPreservingStructure(normalizedPasted, out var textStructuredStats))
        {
            route = fallbackToText ? "FallbackTextStructured" : "TextStructured";
            structuredInsertMs += Stopwatch.GetElapsedTime(structuredTextStart).TotalMilliseconds;
            structuredTextCompositionCount += textStructuredStats.TextCompositionCount;
            structuredEnterCount += textStructuredStats.EnterCount;
            structuredDeleteSelectionApplied |= textStructuredStats.DeleteSelectionApplied;
            EmitPasteCpu();
            return;
        }

        var replaceStart = Stopwatch.GetTimestamp();
        ReplaceSelection(normalizedPasted, "Paste", GroupingPolicy.StructuralAtomic);
        replaceSelectionMs = Stopwatch.GetElapsedTime(replaceStart).TotalMilliseconds;
        route = fallbackToText ? "FallbackTextReplaceSelection" : "TextReplaceSelection";
        EmitPasteCpu();
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

        var i = 0;
        while (i < text.Length)
        {
            var ch = text[i];
            if (ch == '\r')
            {
                i++;
                continue;
            }

            if (ch == '\n')
            {
                ExecuteEnterParagraphBreak();
                stats = stats with { EnterCount = stats.EnterCount + 1 };
                i++;
                continue;
            }

            var segmentStart = i;
            while (i < text.Length && text[i] != '\n' && text[i] != '\r')
            {
                i++;
            }

            var segmentLength = i - segmentStart;
            if (segmentLength <= 0)
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

    private readonly record struct PasteStructuredStats(int TextCompositionCount, int EnterCount, bool DeleteSelectionApplied);


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
        ExecuteTextMutationBatch(() =>
        {
            DocumentEditing.ReplaceTextRange(Document, start, length, normalizedReplacement, session);
            session.CommitTransaction();
        });
        UpdateSelectionState(start + normalizedReplacement.Length, start + normalizedReplacement.Length, ensureCaretVisible: false);
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        _perfTracker.RecordEdit(elapsedMs);
        TraceInvariants(commandType);
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateAfterTextMutation(commandType);
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
        var beforeParagraph = DocumentEditing.CloneParagraph(entries[paragraphIndex].Paragraph);
        var paragraph = new Paragraph();
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

        var caretAfter = start + text.Length;
        RecordOperation(
            "MutationRoute",
            $"InsertTextStyledStructured cmd={commandType} policy={policy} start={start} len={length} insLen={text.Length}");
        return CommitParagraphReplacement(
            commandType,
            policy,
            start,
            length,
            caretAfter,
            paragraphIndex,
            beforeParagraph,
            paragraph,
            editStart);
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

    private void SyncHostedDocumentChildren()
    {
        var nextChildren = new List<UIElement>();
        CollectHostedInlineChildren(Document, nextChildren);

        for (var i = 0; i < _documentHostedVisualChildren.Count; i++)
        {
            var child = _documentHostedVisualChildren[i];
            if (nextChildren.Contains(child))
            {
                continue;
            }

            if (ReferenceEquals(child.VisualParent, _hostedDocumentVisualHost))
            {
                child.SetVisualParent(null);
            }

            if (ReferenceEquals(child.LogicalParent, this))
            {
                child.SetLogicalParent(null);
            }
        }

        if (nextChildren.Count > 0 && !ReferenceEquals(_hostedDocumentVisualHost.VisualParent, this))
        {
            _hostedDocumentVisualHost.SetVisualParent(this);
        }
        else if (nextChildren.Count == 0 && ReferenceEquals(_hostedDocumentVisualHost.VisualParent, this))
        {
            _hostedDocumentVisualHost.SetVisualParent(null);
        }

        for (var i = 0; i < nextChildren.Count; i++)
        {
            var child = nextChildren[i];
            if (!ReferenceEquals(child.VisualParent, _hostedDocumentVisualHost))
            {
                child.SetVisualParent(_hostedDocumentVisualHost);
            }

            if (!ReferenceEquals(child.LogicalParent, this))
            {
                child.SetLogicalParent(this);
            }
        }

        _documentHostedVisualChildren.Clear();
        _documentHostedVisualChildren.AddRange(nextChildren);
        _hostedDocumentVisualHost.SetChildren(_documentHostedVisualChildren);
    }

    private void EnsureHostedDocumentChildLayout()
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagEnsureHostedDocumentChildLayoutCallCount++;
        _runtimeEnsureHostedDocumentChildLayoutCallCount++;
        if (_documentHostedVisualChildren.Count == 0)
        {
            _diagEnsureHostedDocumentChildLayoutSkippedNoHostedChildrenCount++;
            _runtimeEnsureHostedDocumentChildLayoutSkippedNoHostedChildrenCount++;
            var skippedElapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _diagEnsureHostedDocumentChildLayoutElapsedTicks += skippedElapsedTicks;
            _runtimeEnsureHostedDocumentChildLayoutElapsedTicks += skippedElapsedTicks;
            return;
        }

        var textRect = GetTextRect();
        if (textRect.Width <= 0f || textRect.Height <= 0f)
        {
            _diagEnsureHostedDocumentChildLayoutSkippedZeroTextRectCount++;
            _runtimeEnsureHostedDocumentChildLayoutSkippedZeroTextRectCount++;
            var skippedElapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _diagEnsureHostedDocumentChildLayoutElapsedTicks += skippedElapsedTicks;
            _runtimeEnsureHostedDocumentChildLayoutElapsedTicks += skippedElapsedTicks;
            return;
        }

        var layout = BuildOrGetLayout(textRect.Width);
        ClampScrollOffsets(layout, textRect);
        EnsureHostedDocumentChildLayout(textRect, layout);
        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _diagEnsureHostedDocumentChildLayoutElapsedTicks += elapsedTicks;
        _runtimeEnsureHostedDocumentChildLayoutElapsedTicks += elapsedTicks;
    }

    private void EnsureHostedDocumentChildLayout(LayoutRect textRect, DocumentLayoutResult layout)
    {
        if (_documentHostedVisualChildren.Count == 0)
        {
            _diagEnsureHostedDocumentChildLayoutSkippedNoHostedChildrenCount++;
            _runtimeEnsureHostedDocumentChildLayoutSkippedNoHostedChildrenCount++;
            return;
        }

        var horizontalOffset = GetEffectiveHorizontalOffset();
        var verticalOffset = GetEffectiveVerticalOffset();

        var hostedLayoutChanged = !AreLayoutRectsEquivalent(_hostedDocumentVisualHost.LayoutSlot, textRect);
        _hostedDocumentVisualHost.SetLayoutSlot(textRect);

        for (var i = 0; i < layout.HostedElements.Count; i++)
        {
            var placement = layout.HostedElements[i];
            var rect = new LayoutRect(
                textRect.X + placement.Bounds.X - horizontalOffset,
                textRect.Y + placement.Bounds.Y - verticalOffset,
                placement.Bounds.Width,
                placement.Bounds.Height);

            if (placement.Child is FrameworkElement arrangedFrameworkChild)
            {
                var previousSlot = arrangedFrameworkChild.LayoutSlot;
                arrangedFrameworkChild.Arrange(rect);
                hostedLayoutChanged |= !AreLayoutRectsEquivalent(previousSlot, arrangedFrameworkChild.LayoutSlot);
            }
            else
            {
                var previousSlot = placement.Child.LayoutSlot;
                placement.Child.SetLayoutSlot(rect);
                hostedLayoutChanged |= !AreLayoutRectsEquivalent(previousSlot, placement.Child.LayoutSlot);
            }
        }

        if (hostedLayoutChanged)
        {
            _hostedDocumentVisualHost.InvalidateVisual();
            for (var i = 0; i < _documentHostedVisualChildren.Count; i++)
            {
                _documentHostedVisualChildren[i].InvalidateVisual();
            }
        }
    }

    private static bool AreLayoutRectsEquivalent(LayoutRect left, LayoutRect right)
    {
        return Math.Abs(left.X - right.X) < 0.01f &&
               Math.Abs(left.Y - right.Y) < 0.01f &&
               Math.Abs(left.Width - right.Width) < 0.01f &&
               Math.Abs(left.Height - right.Height) < 0.01f;
    }

    private static void CollectHostedInlineChildren(FlowDocument document, List<UIElement> children)
    {
        CollectHostedBlockChildren(document.Blocks, children);
    }

    private static void CollectHostedBlockChildren(IEnumerable<Block> blocks, List<UIElement> children)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Paragraph paragraph:
                    CollectHostedInlineChildren(paragraph.Inlines, children);
                    break;
                case BlockUIContainer blockUiContainer when blockUiContainer.Child != null:
                    children.Add(blockUiContainer.Child);
                    break;
                case Section section:
                    CollectHostedBlockChildren(section.Blocks, children);
                    break;
                case InkkSlinger.List list:
                    for (var itemIndex = 0; itemIndex < list.Items.Count; itemIndex++)
                    {
                        CollectHostedBlockChildren(list.Items[itemIndex].Blocks, children);
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
                                CollectHostedBlockChildren(row.Cells[cellIndex].Blocks, children);
                            }
                        }
                    }

                    break;
            }
        }
    }

    private static void CollectHostedInlineChildren(IEnumerable<Inline> inlines, List<UIElement> children)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case InlineUIContainer inlineUiContainer when inlineUiContainer.Child != null:
                    children.Add(inlineUiContainer.Child);
                    break;
                case Span span:
                    CollectHostedInlineChildren(span.Inlines, children);
                    break;
            }
        }
    }

    private static List<HostedInlinePlacement> CollectHostedInlinePlacements(FlowDocument document)
    {
        var placements = new List<HostedInlinePlacement>();
        var logicalBlocks = new List<TextElement>();
        CollectHostedLogicalBlocks(document.Blocks, logicalBlocks);
        var offset = 0;
        for (var blockIndex = 0; blockIndex < logicalBlocks.Count; blockIndex++)
        {
            switch (logicalBlocks[blockIndex])
            {
                case Paragraph paragraph:
                {
                    var localOffset = 0;
                    CollectHostedInlinePlacements(paragraph.Inlines, offset, ref localOffset, placements);
                    offset += GetParagraphLogicalLength(paragraph);
                    break;
                }
                case BlockUIContainer blockUiContainer:
                    if (blockUiContainer.Child != null)
                    {
                        placements.Add(new HostedInlinePlacement(blockUiContainer.Child, offset));
                    }

                    offset += 1;
                    break;
            }

            if (blockIndex < logicalBlocks.Count - 1)
            {
                offset++;
            }
        }

        return placements;
    }

    private static void CollectHostedLogicalBlocks(IEnumerable<Block> blocks, List<TextElement> logicalBlocks)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Paragraph paragraph:
                    logicalBlocks.Add(paragraph);
                    break;
                case BlockUIContainer blockUiContainer:
                    logicalBlocks.Add(blockUiContainer);
                    break;
                case Section section:
                    CollectHostedLogicalBlocks(section.Blocks, logicalBlocks);
                    break;
                case InkkSlinger.List list:
                    for (var itemIndex = 0; itemIndex < list.Items.Count; itemIndex++)
                    {
                        CollectHostedLogicalBlocks(list.Items[itemIndex].Blocks, logicalBlocks);
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
                                CollectHostedLogicalBlocks(row.Cells[cellIndex].Blocks, logicalBlocks);
                            }
                        }
                    }

                    break;
            }
        }
    }

    private static void CollectHostedInlinePlacements(
        IEnumerable<Inline> inlines,
        int paragraphOffset,
        ref int localOffset,
        List<HostedInlinePlacement> placements)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    localOffset += run.Text.Length;
                    break;
                case LineBreak:
                    localOffset++;
                    break;
                case InlineUIContainer inlineUiContainer:
                    if (inlineUiContainer.Child != null)
                    {
                        placements.Add(new HostedInlinePlacement(inlineUiContainer.Child, paragraphOffset + localOffset));
                    }

                    localOffset++;
                    break;
                case Span span:
                    CollectHostedInlinePlacements(span.Inlines, paragraphOffset, ref localOffset, placements);
                    break;
            }
        }
    }

    private readonly record struct HostedInlinePlacement(UIElement Child, int Offset);

    private sealed class HostedDocumentVisualHost : UIElement
    {
        private IReadOnlyList<UIElement> _children = Array.Empty<UIElement>();

        public HostedDocumentVisualHost()
        {
            ClipToBounds = true;
        }

        public void SetChildren(IReadOnlyList<UIElement> children)
        {
            _children = children;
        }

        public override IEnumerable<UIElement> GetVisualChildren()
        {
            for (var i = 0; i < _children.Count; i++)
            {
                yield return _children[i];
            }
        }

        internal override int GetVisualChildCountForTraversal()
        {
            return _children.Count;
        }

        internal override UIElement GetVisualChildAtForTraversal(int index)
        {
            if ((uint)index < (uint)_children.Count)
            {
                return _children[index];
            }

            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    private int GetTextIndexFromPoint(Vector2 point)
    {
        var textRect = GetTextRect();
        var layout = BuildOrGetLayout(textRect.Width);
        ClampScrollOffsets(layout, textRect);
        var hit = layout.HitTestOffset(new Vector2(
            (point.X - textRect.X) + GetEffectiveHorizontalOffset(),
            (point.Y - textRect.Y) + GetEffectiveVerticalOffset()));
        _lastSelectionHitTestOffset = hit;
        return hit;
    }

    private void DrawCaret(SpriteBatch spriteBatch, LayoutRect textRect, DocumentLayoutResult layout)
    {
        DrawCaret(spriteBatch, textRect, layout, GetEffectiveHorizontalOffset(), GetEffectiveVerticalOffset());
    }

    private void DrawCaret(SpriteBatch spriteBatch, LayoutRect textRect, DocumentLayoutResult layout, float horizontalOffset, float verticalOffset)
    {
        if (!TryGetCaretRenderRect(textRect, layout, horizontalOffset, verticalOffset, out var caretRect))
        {
            return;
        }

        UiDrawing.DrawFilledRect(
            spriteBatch,
            caretRect,
            CaretBrush * Opacity);
        var line = ResolveLineForOffset(layout, _caretIndex);
    }

    private void DrawSelection(SpriteBatch spriteBatch, LayoutRect textRect, DocumentLayoutResult layout)
    {
        DrawSelection(spriteBatch, textRect, layout, GetEffectiveHorizontalOffset(), GetEffectiveVerticalOffset());
    }

    private void DrawSelection(SpriteBatch spriteBatch, LayoutRect textRect, DocumentLayoutResult layout, float horizontalOffset, float verticalOffset)
    {
        if (SelectionLength <= 0 || (!IsFocused && !IsInactiveSelectionHighlightEnabled))
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
                new LayoutRect(textRect.X + rect.X - horizontalOffset, textRect.Y + rect.Y - verticalOffset, rect.Width, rect.Height),
                SelectionBrush * (Opacity * SelectionOpacity));
        }

        var line = ResolveLineForOffset(layout, _caretIndex);
        var caretRect = layout.TryGetCaretPosition(_caretIndex, out var caret)
            ? new LayoutRect(textRect.X + caret.X - horizontalOffset, textRect.Y + caret.Y - verticalOffset, 1f, Math.Max(1f, UiTextRenderer.GetLineHeight(this, FontSize)))
            : default;
        _perfTracker.RecordSelectionGeometry(Stopwatch.GetElapsedTime(selectionStartTicks).TotalMilliseconds);
    }

    private bool TryGetCaretRenderRect(
        LayoutRect textRect,
        DocumentLayoutResult layout,
        float horizontalOffset,
        float verticalOffset,
        out LayoutRect caretRect)
    {
        caretRect = default;
        if (textRect.Width <= 0f || textRect.Height <= 0f)
        {
            return false;
        }

        if (!layout.TryGetCaretPosition(_caretIndex, out var caret))
        {
            return false;
        }

        var lineHeight = Math.Max(1f, UiTextRenderer.GetLineHeight(this, FontSize));
        var rawRect = new LayoutRect(
            textRect.X + caret.X - horizontalOffset,
            textRect.Y + caret.Y - verticalOffset,
            1f,
            lineHeight);
        caretRect = IntersectRect(rawRect, textRect);
        return caretRect.Width > 0f && caretRect.Height > 0f;
    }

    private void DrawRunText(SpriteBatch spriteBatch, DocumentLayoutRun run, Vector2 position, Color baseColor)
    {
        if (run.Text.Length == 0)
        {
            return;
        }

        var overlapStart = Math.Max(SelectionStart, run.StartOffset);
        var overlapEnd = Math.Min(SelectionStart + SelectionLength, run.StartOffset + run.Length);
        var styleOverride = ToStyleOverride(run.Style);
        if (SelectionLength <= 0 || overlapEnd <= overlapStart)
        {
            UiTextRenderer.DrawString(
                spriteBatch,
                this,
                run.Text,
                position,
                baseColor * Opacity,
                FontSize,
                opaqueBackground: false,
                styleOverride: styleOverride);
            return;
        }

        var prefixLength = overlapStart - run.StartOffset;
        var selectedLength = overlapEnd - overlapStart;
        var prefixText = prefixLength > 0 ? run.Text[..prefixLength] : string.Empty;
        var selectedText = selectedLength > 0 ? run.Text.Substring(prefixLength, selectedLength) : string.Empty;
        var suffixText = (prefixLength + selectedLength) < run.Text.Length
            ? run.Text[(prefixLength + selectedLength)..]
            : string.Empty;
        var typography = UiTextRenderer.ResolveTypography(this, FontSize, styleOverride);
        var currentX = position.X;

        if (prefixText.Length > 0)
        {
            UiTextRenderer.DrawString(spriteBatch, typography, prefixText, new Vector2(currentX, position.Y), baseColor * Opacity);
            currentX += UiTextRenderer.MeasureWidth(typography, prefixText);
        }

        if (selectedText.Length > 0)
        {
            UiTextRenderer.DrawString(spriteBatch, typography, selectedText, new Vector2(currentX, position.Y), SelectionTextBrush * Opacity);
            currentX += UiTextRenderer.MeasureWidth(typography, selectedText);
        }

        if (suffixText.Length > 0)
        {
            UiTextRenderer.DrawString(spriteBatch, typography, suffixText, new Vector2(currentX, position.Y), baseColor * Opacity);
        }
    }

    private void DrawRunUnderline(SpriteBatch spriteBatch, DocumentLayoutRun run, Vector2 position, Color color)
    {
        if (!run.Style.IsUnderline)
        {
            return;
        }

        var underlineY = position.Y + run.Bounds.Height - 1f;
        UiDrawing.DrawFilledRect(
            spriteBatch,
            new LayoutRect(position.X, underlineY, Math.Max(1f, run.Bounds.Width), 1f),
            color * Opacity);
    }

    private static bool TryGetVisibleLineRange(
        DocumentLayoutResult layout,
        LayoutRect textRect,
        float verticalOffset,
        out int firstVisibleLineIndex,
        out int lastVisibleLineIndex)
    {
        firstVisibleLineIndex = -1;
        lastVisibleLineIndex = -1;
        if (layout.Lines.Count == 0 || textRect.Height <= 0f)
        {
            return false;
        }

        var visibleTop = verticalOffset;
        var visibleBottom = verticalOffset + textRect.Height;
        firstVisibleLineIndex = FindFirstVisibleLineIndex(layout.Lines, visibleTop);
        if (firstVisibleLineIndex < 0)
        {
            return false;
        }

        lastVisibleLineIndex = FindLastVisibleLineIndex(layout.Lines, visibleBottom, firstVisibleLineIndex);
        return lastVisibleLineIndex >= firstVisibleLineIndex;
    }

    private static int FindFirstVisibleLineIndex(IReadOnlyList<DocumentLayoutLine> lines, float visibleTop)
    {
        var low = 0;
        var high = lines.Count - 1;
        var result = -1;
        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var line = lines[mid];
            var lineBottom = line.Bounds.Y + line.Bounds.Height;
            if (lineBottom >= visibleTop)
            {
                result = mid;
                high = mid - 1;
            }
            else
            {
                low = mid + 1;
            }
        }

        return result;
    }

    private static int FindLastVisibleLineIndex(IReadOnlyList<DocumentLayoutLine> lines, float visibleBottom, int firstVisibleLineIndex)
    {
        var low = firstVisibleLineIndex;
        var high = lines.Count - 1;
        var result = firstVisibleLineIndex;
        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var line = lines[mid];
            if (line.Bounds.Y <= visibleBottom)
            {
                result = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return result;
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

    private static bool TryGetRichClipboardPayload(TextClipboardReadSnapshot snapshot, out string payload, out string format)
    {
        if (TryGetNonEmptyClipboardData(snapshot, ClipboardXamlPackageFormat, out payload))
        {
            format = ClipboardXamlPackageFormat;
            return true;
        }

        if (TryGetNonEmptyClipboardData(snapshot, ClipboardXamlFormat, out payload))
        {
            format = ClipboardXamlFormat;
            return true;
        }

        if (TryGetNonEmptyClipboardData(snapshot, FlowDocumentSerializer.ClipboardFormat, out payload))
        {
            format = FlowDocumentSerializer.ClipboardFormat;
            return true;
        }

        if (TryGetNonEmptyClipboardData(snapshot, ClipboardRtfFormat, out payload))
        {
            format = ClipboardRtfFormat;
            return true;
        }

        payload = string.Empty;
        format = string.Empty;
        return false;
    }

    private static bool TryGetNonEmptyClipboardData(TextClipboardReadSnapshot snapshot, string format, out string value)
    {
        if (snapshot.TryGetData<string>(format, out var payload) &&
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

    private string SerializeSelectionPayload(string dataFormat)
    {
        var selectionEnd = SelectionStart + SelectionLength;
        return NormalizeDataFormat(dataFormat) switch
        {
            ClipboardXamlFormat or ClipboardXamlPackageFormat or FlowDocumentSerializer.ClipboardFormat =>
                FlowDocumentSerializer.SerializeRange(Document, SelectionStart, selectionEnd),
            ClipboardRtfFormat => BuildRtfFromPlainText(GetText().Substring(SelectionStart, SelectionLength)),
            ClipboardTextFormat or ClipboardUnicodeTextFormat => NormalizeNewlines(GetText().Substring(SelectionStart, SelectionLength)),
            _ => throw new NotSupportedException($"RichTextBox does not support saving format '{dataFormat}'.")
        };
    }

    private static string SerializeDocumentPayload(FlowDocument document, string dataFormat)
    {
        return NormalizeDataFormat(dataFormat) switch
        {
            ClipboardXamlFormat or ClipboardXamlPackageFormat or FlowDocumentSerializer.ClipboardFormat => FlowDocumentSerializer.Serialize(document),
            ClipboardRtfFormat => BuildRtfFromPlainText(DocumentEditing.GetText(document)),
            ClipboardTextFormat or ClipboardUnicodeTextFormat => NormalizeNewlines(DocumentEditing.GetText(document)),
            _ => throw new NotSupportedException($"RichTextBox does not support saving format '{dataFormat}'.")
        };
    }

    private static FlowDocument DeserializeDocumentPayload(string payload, string dataFormat)
    {
        if (IsRichFragmentFormat(dataFormat))
        {
            return DeserializeFragmentPayload(payload, dataFormat);
        }

        var text = DeserializeTextPayload(payload, dataFormat);
        return CreateDocumentFromPlainText(text);
    }

    private static FlowDocument DeserializeFragmentPayload(string payload, string dataFormat)
    {
        return NormalizeDataFormat(dataFormat) switch
        {
            ClipboardXamlFormat or ClipboardXamlPackageFormat or FlowDocumentSerializer.ClipboardFormat => FlowDocumentSerializer.DeserializeFragment(payload),
            ClipboardRtfFormat => CreateDocumentFromPlainText(ParseRtfToPlainText(payload)),
            ClipboardTextFormat or ClipboardUnicodeTextFormat => CreateDocumentFromPlainText(payload),
            _ => throw new NotSupportedException($"RichTextBox does not support loading format '{dataFormat}'.")
        };
    }

    private static string DeserializeTextPayload(string payload, string dataFormat)
    {
        return NormalizeDataFormat(dataFormat) switch
        {
            ClipboardRtfFormat => ParseRtfToPlainText(payload),
            ClipboardTextFormat or ClipboardUnicodeTextFormat => payload,
            ClipboardXamlFormat or ClipboardXamlPackageFormat or FlowDocumentSerializer.ClipboardFormat => DocumentEditing.GetText(FlowDocumentSerializer.DeserializeFragment(payload)),
            _ => throw new NotSupportedException($"RichTextBox does not support loading format '{dataFormat}'.")
        };
    }

    private static string ReadStreamPayload(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return reader.ReadToEnd();
    }

    private static void WriteStreamPayload(Stream stream, string payload)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
            stream.SetLength(0);
        }

        using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(payload);
        writer.Flush();
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }
    }

    private static bool IsSupportedDataFormat(string? dataFormat)
    {
        var normalized = NormalizeDataFormat(dataFormat);
        return normalized == FlowDocumentSerializer.ClipboardFormat ||
               normalized == ClipboardXamlFormat ||
               normalized == ClipboardXamlPackageFormat ||
               normalized == ClipboardRtfFormat ||
               normalized == ClipboardTextFormat ||
               normalized == ClipboardUnicodeTextFormat;
    }

    private static bool IsRichFragmentFormat(string? dataFormat)
    {
        var normalized = NormalizeDataFormat(dataFormat);
        return normalized == FlowDocumentSerializer.ClipboardFormat ||
               normalized == ClipboardXamlFormat ||
               normalized == ClipboardXamlPackageFormat ||
               normalized == ClipboardRtfFormat;
    }

    private static string NormalizeDataFormat(string? dataFormat)
    {
        return string.IsNullOrWhiteSpace(dataFormat) ? string.Empty : dataFormat.Trim();
    }

    private static FlowDocument CreateDocumentFromPlainText(string text)
    {
        var document = CreateDefaultDocument();
        DocumentEditing.ReplaceAllText(document, NormalizeNewlines(text));
        return document;
    }

    private static string ParseRtfToPlainText(string rtf)
    {
        if (string.IsNullOrWhiteSpace(rtf))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(rtf.Length);
        for (var i = 0; i < rtf.Length; i++)
        {
            var ch = rtf[i];
            if (ch == '{' || ch == '}')
            {
                continue;
            }

            if (ch != '\\')
            {
                builder.Append(ch);
                continue;
            }

            if (i + 1 >= rtf.Length)
            {
                break;
            }

            var next = rtf[++i];
            if (next is '\\' or '{' or '}')
            {
                builder.Append(next);
                continue;
            }

            if (next == '\'')
            {
                if (i + 2 < rtf.Length)
                {
                    var hex = rtf.Substring(i + 1, 2);
                    if (byte.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var value))
                    {
                        builder.Append((char)value);
                    }

                    i += 2;
                }

                continue;
            }

            if (!char.IsLetter(next))
            {
                if (next == '~')
                {
                    builder.Append(' ');
                }

                continue;
            }

            var keywordStart = i;
            while (i < rtf.Length && char.IsLetter(rtf[i]))
            {
                i++;
            }

            var keyword = rtf.Substring(keywordStart, i - keywordStart);
            var negative = false;
            if (i < rtf.Length && rtf[i] == '-')
            {
                negative = true;
                i++;
            }

            var numberStart = i;
            while (i < rtf.Length && char.IsDigit(rtf[i]))
            {
                i++;
            }

            int? parameter = null;
            if (i > numberStart && int.TryParse(rtf.Substring(numberStart, i - numberStart), out var parsed))
            {
                parameter = negative ? -parsed : parsed;
            }

            if (i < rtf.Length && rtf[i] == ' ')
            {
                // Consume the control-word delimiter.
            }
            else
            {
                i--;
            }

            switch (keyword)
            {
                case "par":
                case "line":
                    builder.Append('\n');
                    break;
                case "tab":
                    builder.Append('\t');
                    break;
                case "u" when parameter.HasValue:
                    builder.Append((char)(short)parameter.Value);
                    if (i + 1 < rtf.Length)
                    {
                        i++;
                    }

                    break;
            }
        }

        return NormalizeNewlines(builder.ToString());
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

    private void ReplaceSelectionWithFragment(FlowDocument fragment, string commandType, GroupingPolicy policy)
    {
        if (IsRichStructuredDocument() &&
            RichGuardedFragmentCommands.Contains(commandType) &&
            !IsFullDocumentSelection())
        {
            RecordOperation("Guard", $"ReplaceSelectionWithFragment->{commandType}:BlockedInRichMode");
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
        ExecuteTextMutationBatch(() =>
        {
            session.ApplyOperation(new ReplaceDocumentOperation("Document", beforeText, afterText, beforeDocument, afterDocument));
            session.CommitTransaction();
        });

        UpdateSelectionState(caretAfter, caretAfter, ensureCaretVisible: false);
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        _perfTracker.RecordEdit(elapsedMs);
        TraceInvariants(commandType);
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateAfterTextMutation(commandType);
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
        ExecuteTextMutationBatch(() =>
        {
            session.ApplyOperation(new ReplaceDocumentOperation("Document", beforeText, afterText, beforeDocument, afterDocument));
            session.CommitTransaction();
        });

        postApply?.Invoke(Document, selectionStart, selectionLength, _caretIndex);
        UpdateSelectionState(_caretIndex, _caretIndex, ensureCaretVisible: false);
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        _perfTracker.RecordEdit(elapsedMs);
        TraceInvariants(reason);
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateAfterTextMutation(reason);
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


    private static FlowDocument BuildDocumentWithFragment(FlowDocument current, Table table, int selectionStart, int selectionLength)
    {
        var fragment = new FlowDocument();
        fragment.Blocks.Add(table);
        return BuildDocumentWithFragment(current, fragment, selectionStart, selectionLength);
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
            ListIndent: lineHeight * 1.2f,
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

    private static UiTextStyleOverride ToStyleOverride(DocumentLayoutStyle style)
    {
        var value = UiTextStyleOverride.None;
        if (style.IsBold)
        {
            value |= UiTextStyleOverride.Bold;
        }

        if (style.IsItalic)
        {
            value |= UiTextStyleOverride.Italic;
        }

        return value;
    }


    private void CaptureDirtyHint(DocumentLayoutResult current, LayoutRect textRect)
    {
        if (!TryBuildLocalizedTextDirtyBoundsHint(current, textRect, out var dirtyBounds))
        {
            return;
        }

        _pendingRenderDirtyBoundsHint = dirtyBounds;
        _hasPendingRenderDirtyBoundsHint = true;
    }

    private bool TryGetLocalizedTextDirtyBoundsHint(out LayoutRect bounds)
    {
        bounds = default;
        var textRect = GetTextRect();
        if (textRect.Width <= 0f || textRect.Height <= 0f)
        {
            return false;
        }

        var layout = BuildOrGetLayout(textRect.Width);
        ClampScrollOffsets(layout, textRect);
        return TryBuildLocalizedTextDirtyBoundsHint(layout, textRect, out bounds);
    }

    private bool TryBuildLocalizedTextDirtyBoundsHint(DocumentLayoutResult current, LayoutRect textRect, out LayoutRect bounds)
    {
        bounds = default;
        if (_lastRenderedLayout == null || _lastRenderedLayout.Lines.Count == 0 || current.Lines.Count == 0)
        {
            return false;
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
            return false;
        }

        var local = new LayoutRect(minX, minY, Math.Max(1f, maxX - minX), Math.Max(1f, maxY - minY));
        var absolute = new LayoutRect(
            textRect.X + local.X - GetEffectiveHorizontalOffset(),
            textRect.Y + local.Y - GetEffectiveVerticalOffset(),
            local.Width,
            local.Height);
        absolute = IntersectRect(ExpandRect(absolute, 2f), textRect);
        if (absolute.Width <= 0f || absolute.Height <= 0f)
        {
            return false;
        }

        if (TryProjectRectToRootSpace(absolute, out var rootSpaceBounds))
        {
            absolute = rootSpaceBounds;
        }

        bounds = NormalizeRect(absolute);
        return bounds.Width > 0f && bounds.Height > 0f;

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
        if (_contentHost != null && _contentHost.TryGetContentViewportClipRect(out var clipRect))
        {
            return clipRect;
        }

        return new LayoutRect(
            LayoutSlot.X + BorderThickness + Padding.Left,
            LayoutSlot.Y + BorderThickness + Padding.Top,
            Math.Max(0f, LayoutSlot.Width - (BorderThickness * 2f) - Padding.Horizontal),
            Math.Max(0f, LayoutSlot.Height - (BorderThickness * 2f) - Padding.Vertical));
    }

    private bool TryProjectRectToRootSpace(LayoutRect rect, out LayoutRect projectedRect)
    {
        projectedRect = rect;
        var transform = Matrix.Identity;
        var hasTransform = false;
        for (var current = this as UIElement; current != null; current = current.VisualParent)
        {
            if (!current.TryGetLocalRenderTransformSnapshot(out var localTransform))
            {
                continue;
            }

            transform *= localTransform;
            hasTransform = true;
        }

        if (!hasTransform)
        {
            return true;
        }

        var topLeft = Vector2.Transform(new Vector2(rect.X, rect.Y), transform);
        var topRight = Vector2.Transform(new Vector2(rect.X + rect.Width, rect.Y), transform);
        var bottomLeft = Vector2.Transform(new Vector2(rect.X, rect.Y + rect.Height), transform);
        var bottomRight = Vector2.Transform(new Vector2(rect.X + rect.Width, rect.Y + rect.Height), transform);

        var minX = MathF.Min(MathF.Min(topLeft.X, topRight.X), MathF.Min(bottomLeft.X, bottomRight.X));
        var minY = MathF.Min(MathF.Min(topLeft.Y, topRight.Y), MathF.Min(bottomLeft.Y, bottomRight.Y));
        var maxX = MathF.Max(MathF.Max(topLeft.X, topRight.X), MathF.Max(bottomLeft.X, bottomRight.X));
        var maxY = MathF.Max(MathF.Max(topLeft.Y, topRight.Y), MathF.Max(bottomLeft.Y, bottomRight.Y));

        projectedRect = new LayoutRect(minX, minY, MathF.Max(0f, maxX - minX), MathF.Max(0f, maxY - minY));
        return projectedRect.Width > 0f && projectedRect.Height > 0f;
    }

    private static LayoutRect IntersectRect(LayoutRect left, LayoutRect right)
    {
        var x = MathF.Max(left.X, right.X);
        var y = MathF.Max(left.Y, right.Y);
        var rightEdge = MathF.Min(left.X + left.Width, right.X + right.Width);
        var bottomEdge = MathF.Min(left.Y + left.Height, right.Y + right.Height);
        return new LayoutRect(x, y, MathF.Max(0f, rightEdge - x), MathF.Max(0f, bottomEdge - y));
    }

    private static LayoutRect ExpandRect(LayoutRect rect, float padding)
    {
        var safePadding = MathF.Max(0f, padding);
        return new LayoutRect(
            rect.X - safePadding,
            rect.Y - safePadding,
            MathF.Max(0f, rect.Width + (safePadding * 2f)),
            MathF.Max(0f, rect.Height + (safePadding * 2f)));
    }

    private static LayoutRect NormalizeRect(LayoutRect rect)
    {
        var x = rect.X;
        var y = rect.Y;
        var width = rect.Width;
        var height = rect.Height;
        if (width < 0f)
        {
            x += width;
            width = -width;
        }

        if (height < 0f)
        {
            y += height;
            height = -height;
        }

        return new LayoutRect(x, y, width, height);
    }

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        clipRect = LayoutSlot;
        return true;
    }

    private readonly record struct RichTextBoxScrollMetrics(
        float HorizontalOffset,
        float VerticalOffset,
        float ViewportWidth,
        float ViewportHeight,
        float ExtentWidth,
        float ExtentHeight)
    {
        public float ScrollableWidth => Math.Max(0f, ExtentWidth - ViewportWidth);

        public float ScrollableHeight => Math.Max(0f, ExtentHeight - ViewportHeight);
    }
}



