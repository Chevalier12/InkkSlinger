using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

[TemplatePart("PART_Root", typeof(Grid))]
[TemplatePart("PART_LineNumberBorder", typeof(Border))]
[TemplatePart("PART_LineNumberSeparator", typeof(Border))]
[TemplatePart("PART_LineNumberPresenter", typeof(IDEEditorLineNumberPresenter))]
[TemplatePart("PART_Editor", typeof(RichTextBox))]
[TemplatePart("PART_IndentGuideOverlay", typeof(IDEEditorIndentGuideOverlay))]
public sealed class IDE_Editor : Control, ITextInputControl
{
    private static readonly Lazy<Style> DefaultStyle = new(BuildDefaultStyle);
    private static int _diagUpdateLineNumberGutterCallCount;
    private static int _diagUpdateLineNumberGutterForcedCount;
    private static int _diagUpdateLineNumberGutterAppliedCount;
    private static int _diagUpdateLineNumberGutterNoOpCount;
    private static int _diagUpdateLineNumberGutterVisibleLineTotal;
    private static long _diagUpdateLineNumberGutterElapsedTicks;
    private static int _diagEditorTextChangedCallCount;
    private static long _diagEditorTextChangedElapsedTicks;
    private static long _diagEditorTextChangedUpdateCachedLineCountElapsedTicks;
    private static long _diagEditorTextChangedUpdateLineNumberGutterElapsedTicks;
    private static double _diagEditorTextChangedLineNumberPresenterUpdateMilliseconds;
    private static double _diagEditorTextChangedLineNumberPresenterMeasureMilliseconds;
    private static double _diagEditorTextChangedLineNumberPresenterArrangeMilliseconds;
    private static long _diagEditorTextChangedIndentInvalidateElapsedTicks;
    private static long _diagEditorTextChangedSubscriberElapsedTicks;
    private static int _diagEditorDocumentChangedCallCount;
    private static int _diagEditorViewportChangedCallCount;
    private static int _diagEditorLayoutUpdatedCallCount;
    private static int _diagIndentGuideInvalidateVisualCallCount;
    private static int _diagBuildIndentGuideSnapshotCallCount;
    private static int _diagBuildIndentGuideSnapshotSuccessCount;
    private static int _diagBuildIndentGuideSnapshotSegmentTotal;
    private static long _diagBuildIndentGuideSnapshotElapsedTicks;

    public static readonly DependencyProperty DocumentProperty =
        DependencyProperty.Register(
            nameof(Document),
            typeof(FlowDocument),
            typeof(IDE_Editor),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value as FlowDocument ?? CreateDefaultDocument()));

    public static readonly DependencyProperty TextWrappingProperty =
        DependencyProperty.Register(
            nameof(TextWrapping),
            typeof(TextWrapping),
            typeof(IDE_Editor),
            new FrameworkPropertyMetadata(TextWrapping.Wrap, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HorizontalScrollBarVisibilityProperty =
        DependencyProperty.Register(
            nameof(HorizontalScrollBarVisibility),
            typeof(ScrollBarVisibility),
            typeof(IDE_Editor),
            new FrameworkPropertyMetadata(ScrollBarVisibility.Auto, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty VerticalScrollBarVisibilityProperty =
        DependencyProperty.Register(
            nameof(VerticalScrollBarVisibility),
            typeof(ScrollBarVisibility),
            typeof(IDE_Editor),
            new FrameworkPropertyMetadata(ScrollBarVisibility.Auto, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectionBrushProperty =
        DependencyProperty.Register(
            nameof(SelectionBrush),
            typeof(Color),
            typeof(IDE_Editor),
            new FrameworkPropertyMetadata(new Color(66, 124, 211, 180), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CaretBrushProperty =
        DependencyProperty.Register(
            nameof(CaretBrush),
            typeof(Color),
            typeof(IDE_Editor),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AcceptsReturnProperty =
        DependencyProperty.Register(
            nameof(AcceptsReturn),
            typeof(bool),
            typeof(IDE_Editor),
            new FrameworkPropertyMetadata(true));

    public static readonly DependencyProperty AcceptsTabProperty =
        DependencyProperty.Register(
            nameof(AcceptsTab),
            typeof(bool),
            typeof(IDE_Editor),
            new FrameworkPropertyMetadata(true));

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(
            nameof(IsReadOnly),
            typeof(bool),
            typeof(IDE_Editor),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty IsReadOnlyCaretVisibleProperty =
        DependencyProperty.Register(
            nameof(IsReadOnlyCaretVisible),
            typeof(bool),
            typeof(IDE_Editor),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsInactiveSelectionHighlightEnabledProperty =
        DependencyProperty.Register(
            nameof(IsInactiveSelectionHighlightEnabled),
            typeof(bool),
            typeof(IDE_Editor),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineNumberWidthProperty =
        DependencyProperty.Register(
            nameof(LineNumberWidth),
            typeof(float),
            typeof(IDE_Editor),
            new FrameworkPropertyMetadata(
                56f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                coerceValueCallback: static (_, value) => value is float width && width >= 0f && float.IsFinite(width) ? width : 56f));

    public static readonly DependencyProperty LineNumberBackgroundProperty =
        DependencyProperty.Register(
            nameof(LineNumberBackground),
            typeof(Color),
            typeof(IDE_Editor),
            new FrameworkPropertyMetadata(new Color(10, 16, 23), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineNumberForegroundProperty =
        DependencyProperty.Register(
            nameof(LineNumberForeground),
            typeof(Color),
            typeof(IDE_Editor),
            new FrameworkPropertyMetadata(new Color(74, 104, 128), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineNumberSeparatorBrushProperty =
        DependencyProperty.Register(
            nameof(LineNumberSeparatorBrush),
            typeof(Color),
            typeof(IDE_Editor),
            new FrameworkPropertyMetadata(new Color(26, 38, 55), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IndentGuideBrushProperty =
        DependencyProperty.Register(
            nameof(IndentGuideBrush),
            typeof(Color),
            typeof(IDE_Editor),
            new FrameworkPropertyMetadata(new Color(43, 60, 82, 180), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectionTextBrushProperty =
        DependencyProperty.Register(
            nameof(SelectionTextBrush),
            typeof(Color),
            typeof(IDE_Editor),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectionOpacityProperty =
        DependencyProperty.Register(
            nameof(SelectionOpacity),
            typeof(float),
            typeof(IDE_Editor),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float opacity
                    ? Math.Clamp(opacity, 0f, 1f)
                    : 1f));

    public static readonly DependencyProperty IsUndoEnabledProperty =
        DependencyProperty.Register(
            nameof(IsUndoEnabled),
            typeof(bool),
            typeof(IDE_Editor),
            new FrameworkPropertyMetadata(true));

    public static readonly DependencyProperty UndoLimitProperty =
        DependencyProperty.Register(
            nameof(UndoLimit),
            typeof(int),
            typeof(IDE_Editor),
            new FrameworkPropertyMetadata(
                -1,
                FrameworkPropertyMetadataOptions.None,
                coerceValueCallback: static (_, value) => value is int limit && limit >= -1 ? limit : -1));

    private Grid? _root;
    private Border? _lineNumberBorder;
    private Border? _lineNumberSeparator;
    private IDEEditorLineNumberPresenter? _lineNumberPresenter;
    private IDEEditorIndentGuideOverlay? _indentGuideOverlay;
    private RichTextBox? _editor;
    private string _cachedDocumentText = string.Empty;
    private IDEEditorIndentGuideSnapshot _cachedIndentGuideSnapshot = new(false, default, Array.Empty<IDEEditorIndentGuideSegmentSnapshot>());
    private bool _isIndentGuideSnapshotDirty = true;
    private int _cachedLineCount = 1;
    private int _lastRenderedLineCount = -1;
    private int _lastRenderedFirstVisibleLine = -1;
    private int _lastRenderedVisibleLineCount = -1;
    private float _lastRenderedLineOffset = float.NaN;
    private float _lastRenderedLineHeight = float.NaN;
    private float _lastViewportPresentationHorizontalOffset = float.NaN;
    private float _lastViewportPresentationVerticalOffset = float.NaN;
    private float _lastViewportPresentationViewportWidth = float.NaN;
    private float _lastViewportPresentationViewportHeight = float.NaN;
    private float _lastViewportPresentationExtentWidth = float.NaN;
    private float _lastViewportPresentationExtentHeight = float.NaN;
    private int _runtimeUpdateLineNumberGutterCallCount;
    private int _runtimeUpdateLineNumberGutterForcedCount;
    private int _runtimeUpdateLineNumberGutterAppliedCount;
    private int _runtimeUpdateLineNumberGutterNoOpCount;
    private int _runtimeUpdateLineNumberGutterVisibleLineTotal;
    private long _runtimeUpdateLineNumberGutterElapsedTicks;
    private int _runtimeEditorTextChangedCallCount;
    private long _runtimeEditorTextChangedElapsedTicks;
    private long _runtimeEditorTextChangedUpdateCachedLineCountElapsedTicks;
    private long _runtimeEditorTextChangedUpdateLineNumberGutterElapsedTicks;
    private double _runtimeEditorTextChangedLineNumberPresenterUpdateMilliseconds;
    private double _runtimeEditorTextChangedLineNumberPresenterMeasureMilliseconds;
    private double _runtimeEditorTextChangedLineNumberPresenterArrangeMilliseconds;
    private long _runtimeEditorTextChangedIndentInvalidateElapsedTicks;
    private long _runtimeEditorTextChangedSubscriberElapsedTicks;
    private long _runtimeLastEditorTextChangedElapsedTicks;
    private long _runtimeLastEditorTextChangedUpdateCachedLineCountElapsedTicks;
    private long _runtimeLastEditorTextChangedUpdateLineNumberGutterElapsedTicks;
    private double _runtimeLastEditorTextChangedLineNumberPresenterUpdateMilliseconds;
    private double _runtimeLastEditorTextChangedLineNumberPresenterMeasureMilliseconds;
    private double _runtimeLastEditorTextChangedLineNumberPresenterArrangeMilliseconds;
    private long _runtimeLastEditorTextChangedIndentInvalidateElapsedTicks;
    private long _runtimeLastEditorTextChangedSubscriberElapsedTicks;
    private int _runtimeEditorDocumentChangedCallCount;
    private int _runtimeEditorViewportChangedCallCount;
    private int _runtimeEditorLayoutUpdatedCallCount;
    private int _runtimeIndentGuideInvalidateVisualCallCount;
    private int _runtimeBuildIndentGuideSnapshotCallCount;
    private int _runtimeBuildIndentGuideSnapshotSuccessCount;
    private int _runtimeBuildIndentGuideSnapshotSegmentTotal;
    private long _runtimeBuildIndentGuideSnapshotElapsedTicks;

    public IDE_Editor()
    {
        SetValue(DocumentProperty, CreateDefaultDocument());
        UpdateCachedDocumentSnapshot(Document);
    }

    public event EventHandler? ViewportChanged;

    public event EventHandler<RoutedSimpleEventArgs>? TextChanged;

    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

    public FlowDocument Document
    {
        get => GetValue<FlowDocument>(DocumentProperty)!;
        set => SetValue(DocumentProperty, value);
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

    public float LineNumberWidth
    {
        get => GetValue<float>(LineNumberWidthProperty);
        set => SetValue(LineNumberWidthProperty, value);
    }

    public Color LineNumberBackground
    {
        get => GetValue<Color>(LineNumberBackgroundProperty);
        set => SetValue(LineNumberBackgroundProperty, value);
    }

    public Color LineNumberForeground
    {
        get => GetValue<Color>(LineNumberForegroundProperty);
        set => SetValue(LineNumberForegroundProperty, value);
    }

    public Color LineNumberSeparatorBrush
    {
        get => GetValue<Color>(LineNumberSeparatorBrushProperty);
        set => SetValue(LineNumberSeparatorBrushProperty, value);
    }

    public Color IndentGuideBrush
    {
        get => GetValue<Color>(IndentGuideBrushProperty);
        set => SetValue(IndentGuideBrushProperty, value);
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

    public bool CanUndo => _editor?.CanUndo ?? false;

    public bool CanRedo => _editor?.CanRedo ?? false;

    public RichTextBox Editor => _editor ?? throw new InvalidOperationException("IDE_Editor template has not been applied.");

    public Border LineNumberBorder => _lineNumberBorder ?? throw new InvalidOperationException("IDE_Editor template has not been applied.");

    public IDEEditorLineNumberPresenter LineNumberPresenter => _lineNumberPresenter ?? throw new InvalidOperationException("IDE_Editor template has not been applied.");

    public string DocumentText => DocumentEditing.GetText(Document);

    public int SelectionStart => _editor?.SelectionStart ?? 0;

    public int SelectionLength => _editor?.SelectionLength ?? 0;

    public float HorizontalOffset => _editor?.HorizontalOffset ?? 0f;

    public float VerticalOffset => _editor?.VerticalOffset ?? 0f;

    public float ViewportWidth => _editor?.ViewportWidth ?? 0f;

    public float ViewportHeight => _editor?.ViewportHeight ?? 0f;

    public float ExtentWidth => _editor?.ExtentWidth ?? 0f;

    public float ExtentHeight => _editor?.ExtentHeight ?? 0f;

    public float ScrollableWidth => _editor?.ScrollableWidth ?? 0f;

    public float ScrollableHeight => _editor?.ScrollableHeight ?? 0f;

    public int LineCount => CountLines(Document);

    public float EstimatedLineHeight => EstimateLineHeight(LineCount);

    protected override Style? GetFallbackStyle()
    {
        return DefaultStyle.Value;
    }

    public override void OnApplyTemplate()
    {
        DetachEditorPart();
        base.OnApplyTemplate();

        _root = GetTemplateChild("PART_Root") as Grid;
        _lineNumberBorder = GetTemplateChild("PART_LineNumberBorder") as Border;
        _lineNumberSeparator = GetTemplateChild("PART_LineNumberSeparator") as Border;
        _lineNumberPresenter = GetTemplateChild("PART_LineNumberPresenter") as IDEEditorLineNumberPresenter;
        _indentGuideOverlay = GetTemplateChild("PART_IndentGuideOverlay") as IDEEditorIndentGuideOverlay;
        _editor = GetTemplateChild("PART_Editor") as RichTextBox;

        if (_indentGuideOverlay != null)
        {
            _indentGuideOverlay.Owner = this;
        }

        ApplyTemplateStyling();

        if (_editor == null)
        {
            throw new InvalidOperationException("IDE_Editor template must contain a PART_Editor of type RichTextBox.");
        }

        SyncEditorProperties();
        _editor.DocumentChanged += OnEditorDocumentChanged;
        _editor.TextChanged += OnEditorTextChanged;
        _editor.SelectionChanged += OnEditorSelectionChanged;
        _editor.ViewportChanged += OnEditorViewportChanged;
        _editor.LayoutUpdated += OnEditorLayoutUpdated;

        UpdateCachedDocumentSnapshot(Document);
        UpdateLineNumberGutter(force: true);
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (ReferenceEquals(args.Property, DocumentProperty))
        {
            SyncEditorProperties();
            UpdateCachedDocumentSnapshot(Document);
            UpdateLineNumberGutter(force: true);
            _indentGuideOverlay?.InvalidateVisual();
            return;
        }

        if (ReferenceEquals(args.Property, ForegroundProperty) ||
            ReferenceEquals(args.Property, PaddingProperty) ||
            ReferenceEquals(args.Property, TextWrappingProperty) ||
            ReferenceEquals(args.Property, HorizontalScrollBarVisibilityProperty) ||
            ReferenceEquals(args.Property, VerticalScrollBarVisibilityProperty) ||
            ReferenceEquals(args.Property, SelectionBrushProperty) ||
            ReferenceEquals(args.Property, CaretBrushProperty) ||
            ReferenceEquals(args.Property, AcceptsReturnProperty) ||
            ReferenceEquals(args.Property, AcceptsTabProperty) ||
            ReferenceEquals(args.Property, IsReadOnlyProperty) ||
            ReferenceEquals(args.Property, IsReadOnlyCaretVisibleProperty) ||
            ReferenceEquals(args.Property, IsInactiveSelectionHighlightEnabledProperty) ||
            ReferenceEquals(args.Property, SelectionTextBrushProperty) ||
            ReferenceEquals(args.Property, SelectionOpacityProperty) ||
            ReferenceEquals(args.Property, IsUndoEnabledProperty) ||
            ReferenceEquals(args.Property, UndoLimitProperty) ||
            ReferenceEquals(args.Property, FrameworkElement.FontFamilyProperty) ||
            ReferenceEquals(args.Property, FrameworkElement.FontSizeProperty))
        {
            SyncEditorProperties();
        }

        if (ReferenceEquals(args.Property, LineNumberWidthProperty) ||
            ReferenceEquals(args.Property, LineNumberBackgroundProperty) ||
            ReferenceEquals(args.Property, LineNumberForegroundProperty) ||
            ReferenceEquals(args.Property, LineNumberSeparatorBrushProperty) ||
            ReferenceEquals(args.Property, IndentGuideBrushProperty) ||
            ReferenceEquals(args.Property, FrameworkElement.FontFamilyProperty) ||
            ReferenceEquals(args.Property, FrameworkElement.FontSizeProperty))
        {
            ApplyTemplateStyling();
            UpdateLineNumberGutter(force: true);
            _indentGuideOverlay?.InvalidateVisual();
        }
    }

    public void Select(int start, int length)
    {
        if (_editor == null)
        {
            return;
        }

        var textLength = DocumentText.Length;
        if (start < 0 || start > textLength)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        if (length < 0 || start + length > textLength)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        _editor.Select(start, length);
        UpdateLineNumberGutter(force: false);
        _indentGuideOverlay?.InvalidateVisual();
    }

    public void ScrollToHorizontalOffset(float offset)
    {
        if (!float.IsFinite(offset))
        {
            return;
        }

        _editor?.ScrollToHorizontalOffset(offset);
        UpdateLineNumberGutter(force: false);
        _indentGuideOverlay?.InvalidateVisual();
    }

    public void ScrollToVerticalOffset(float offset)
    {
        if (!float.IsFinite(offset))
        {
            return;
        }

        _editor?.ScrollToVerticalOffset(offset);
        UpdateLineNumberGutter(force: false);
        _indentGuideOverlay?.InvalidateVisual();
    }

    public void PreserveCurrentScrollOffsetsOnNextLayout()
    {
        _editor?.PreserveCurrentScrollOffsetsOnNextLayout();
    }

    public bool HandleTextInputFromInput(char character)
    {
        EnsureEditorFocusState();
        return _editor != null && _editor.HandleTextInputFromInput(character);
    }

    public bool HandleTextCompositionFromInput(string? text)
    {
        EnsureEditorFocusState();
        return _editor != null && _editor.HandleTextCompositionFromInput(text);
    }

    public bool HandleKeyDownFromInput(Keys key, ModifierKeys modifiers)
    {
        EnsureEditorFocusState();
        if (_editor == null)
        {
            return false;
        }

        if (key == Keys.Tab && modifiers == ModifierKeys.None)
        {
            return _editor.HandleTextCompositionFromInput("  ");
        }

        if (key == Keys.Back && modifiers == ModifierKeys.None && TryHandleSpaceAwareBackspace())
        {
            return true;
        }

        return _editor.HandleKeyDownFromInput(key, modifiers);
    }

    private bool TryHandleSpaceAwareBackspace()
    {
        if (_editor == null || SelectionLength != 0)
        {
            return false;
        }

        var documentText = DocumentText;
        var selectionStart = Math.Clamp(SelectionStart, 0, documentText.Length);
        if (selectionStart == 0 || documentText[selectionStart - 1] != ' ')
        {
            return false;
        }

        var deleteStart = selectionStart - 1;
        var deleteLength = 1;
        if (deleteStart > 0 && documentText[deleteStart - 1] == ' ')
        {
            deleteStart--;
            deleteLength++;
        }

        _editor.Select(deleteStart, deleteLength);
        return _editor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None);
    }

    public bool HandlePointerDownFromInput(Vector2 pointerPosition, bool extendSelection)
    {
        EnsureEditorFocusState();
        return _editor != null && _editor.HandlePointerDownFromInput(pointerPosition, extendSelection);
    }

    public bool HandlePointerMoveFromInput(Vector2 pointerPosition)
    {
        EnsureEditorFocusState();
        return _editor != null && _editor.HandlePointerMoveFromInput(pointerPosition);
    }

    public bool HandlePointerUpFromInput()
    {
        EnsureEditorFocusState();
        return _editor != null && _editor.HandlePointerUpFromInput();
    }

    public bool HandleMouseWheelFromInput(int delta)
    {
        var handled = _editor != null && _editor.HandleMouseWheelFromInput(delta);
        if (handled)
        {
            UpdateLineNumberGutter(force: false);
        }

        return handled;
    }

    public void SetMouseOverFromInput(bool isMouseOver)
    {
        IsMouseOver = isMouseOver;
        _editor?.SetMouseOverFromInput(isMouseOver);
    }

    public void SetFocusedFromInput(bool isFocused)
    {
        IsFocused = isFocused;
        _editor?.SetFocusedFromInput(isFocused);
    }

    public void RefreshDocumentMetrics()
    {
        UpdateCachedDocumentSnapshot(Document);
        UpdateLineNumberGutter(force: true);
        _indentGuideOverlay?.InvalidateVisual();
    }

    public bool TryGetCaretBounds(out LayoutRect bounds)
    {
        if (_editor == null)
        {
            bounds = default;
            return false;
        }

        return _editor.TryGetCaretBounds(out bounds);
    }

    public void Undo()
    {
        _editor?.Undo();
    }

    public void Redo()
    {
        _editor?.Redo();
    }

    public void SelectAll()
    {
        _editor?.SelectAll();
    }

    public void Copy()
    {
        _editor?.Copy();
    }

    public void Cut()
    {
        _editor?.Cut();
    }

    public void Paste()
    {
        _editor?.Paste();
    }

    internal IDEEditorIndentGuideSnapshot GetIndentGuideSnapshotForDiagnostics()
    {
        return TryBuildIndentGuideSnapshot(out var snapshot)
            ? snapshot
            : new IDEEditorIndentGuideSnapshot(false, default, Array.Empty<IDEEditorIndentGuideSegmentSnapshot>());
    }

    internal IDEEditorIndentGuideSnapshot GetIndentGuideSnapshotForRender()
    {
        if (!_isIndentGuideSnapshotDirty)
        {
            return _cachedIndentGuideSnapshot;
        }

        _cachedIndentGuideSnapshot = TryBuildIndentGuideSnapshot(out var snapshot)
            ? snapshot
            : new IDEEditorIndentGuideSnapshot(false, default, Array.Empty<IDEEditorIndentGuideSegmentSnapshot>());
        _isIndentGuideSnapshotDirty = false;
        return _cachedIndentGuideSnapshot;
    }

    internal IDEEditorRuntimeDiagnosticsSnapshot GetIDEEditorSnapshotForDiagnostics()
    {
        var metrics = GetScrollMetricsSnapshot();
        return new IDEEditorRuntimeDiagnosticsSnapshot(
            UpdateLineNumberGutterCallCount: _runtimeUpdateLineNumberGutterCallCount,
            UpdateLineNumberGutterForcedCount: _runtimeUpdateLineNumberGutterForcedCount,
            UpdateLineNumberGutterAppliedCount: _runtimeUpdateLineNumberGutterAppliedCount,
            UpdateLineNumberGutterNoOpCount: _runtimeUpdateLineNumberGutterNoOpCount,
            UpdateLineNumberGutterVisibleLineTotal: _runtimeUpdateLineNumberGutterVisibleLineTotal,
            UpdateLineNumberGutterMilliseconds: TicksToMilliseconds(_runtimeUpdateLineNumberGutterElapsedTicks),
            EditorTextChangedCallCount: _runtimeEditorTextChangedCallCount,
            EditorTextChangedMilliseconds: TicksToMilliseconds(_runtimeEditorTextChangedElapsedTicks),
            EditorTextChangedUpdateCachedLineCountMilliseconds: TicksToMilliseconds(_runtimeEditorTextChangedUpdateCachedLineCountElapsedTicks),
            EditorTextChangedUpdateLineNumberGutterMilliseconds: TicksToMilliseconds(_runtimeEditorTextChangedUpdateLineNumberGutterElapsedTicks),
            EditorTextChangedLineNumberPresenterUpdateMilliseconds: _runtimeEditorTextChangedLineNumberPresenterUpdateMilliseconds,
            EditorTextChangedLineNumberPresenterMeasureMilliseconds: _runtimeEditorTextChangedLineNumberPresenterMeasureMilliseconds,
            EditorTextChangedLineNumberPresenterArrangeMilliseconds: _runtimeEditorTextChangedLineNumberPresenterArrangeMilliseconds,
            EditorTextChangedIndentInvalidateMilliseconds: TicksToMilliseconds(_runtimeEditorTextChangedIndentInvalidateElapsedTicks),
            EditorTextChangedSubscriberMilliseconds: TicksToMilliseconds(_runtimeEditorTextChangedSubscriberElapsedTicks),
            LastEditorTextChangedMilliseconds: TicksToMilliseconds(_runtimeLastEditorTextChangedElapsedTicks),
            LastEditorTextChangedUpdateCachedLineCountMilliseconds: TicksToMilliseconds(_runtimeLastEditorTextChangedUpdateCachedLineCountElapsedTicks),
            LastEditorTextChangedUpdateLineNumberGutterMilliseconds: TicksToMilliseconds(_runtimeLastEditorTextChangedUpdateLineNumberGutterElapsedTicks),
            LastEditorTextChangedLineNumberPresenterUpdateMilliseconds: _runtimeLastEditorTextChangedLineNumberPresenterUpdateMilliseconds,
            LastEditorTextChangedLineNumberPresenterMeasureMilliseconds: _runtimeLastEditorTextChangedLineNumberPresenterMeasureMilliseconds,
            LastEditorTextChangedLineNumberPresenterArrangeMilliseconds: _runtimeLastEditorTextChangedLineNumberPresenterArrangeMilliseconds,
            LastEditorTextChangedIndentInvalidateMilliseconds: TicksToMilliseconds(_runtimeLastEditorTextChangedIndentInvalidateElapsedTicks),
            LastEditorTextChangedSubscriberMilliseconds: TicksToMilliseconds(_runtimeLastEditorTextChangedSubscriberElapsedTicks),
            EditorDocumentChangedCallCount: _runtimeEditorDocumentChangedCallCount,
            EditorViewportChangedCallCount: _runtimeEditorViewportChangedCallCount,
            EditorLayoutUpdatedCallCount: _runtimeEditorLayoutUpdatedCallCount,
            IndentGuideInvalidateVisualCallCount: _runtimeIndentGuideInvalidateVisualCallCount,
            BuildIndentGuideSnapshotCallCount: _runtimeBuildIndentGuideSnapshotCallCount,
            BuildIndentGuideSnapshotSuccessCount: _runtimeBuildIndentGuideSnapshotSuccessCount,
            BuildIndentGuideSnapshotSegmentTotal: _runtimeBuildIndentGuideSnapshotSegmentTotal,
            BuildIndentGuideSnapshotMilliseconds: TicksToMilliseconds(_runtimeBuildIndentGuideSnapshotElapsedTicks),
            CachedLineCount: _cachedLineCount,
            LastRenderedLineCount: _lastRenderedLineCount,
            LastRenderedFirstVisibleLine: _lastRenderedFirstVisibleLine,
            LastRenderedVisibleLineCount: _lastRenderedVisibleLineCount,
            LastRenderedLineOffset: _lastRenderedLineOffset,
            LastRenderedLineHeight: _lastRenderedLineHeight,
            HorizontalOffset: metrics.HorizontalOffset,
            VerticalOffset: metrics.VerticalOffset,
            ViewportWidth: metrics.ViewportWidth,
            ViewportHeight: metrics.ViewportHeight,
            ExtentWidth: metrics.ExtentWidth,
            ExtentHeight: metrics.ExtentHeight);
    }

    internal new static IDEEditorTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return CreateTelemetrySnapshot(reset: false);
    }

    internal new static IDEEditorTelemetrySnapshot GetTelemetryAndReset()
    {
        return CreateTelemetrySnapshot(reset: true);
    }

    internal (float HorizontalOffset, float VerticalOffset, float ViewportWidth, float ViewportHeight, float ExtentWidth, float ExtentHeight) GetScrollMetricsSnapshot()
    {
        return _editor?.GetScrollMetricsSnapshot() ?? (0f, 0f, 0f, 0f, 0f, 0f);
    }

    private void OnEditorTextChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        var startTicks = Stopwatch.GetTimestamp();
        _diagEditorTextChangedCallCount++;
        _runtimeEditorTextChangedCallCount++;

        var lineNumberBefore = CaptureLineNumberPresenterRuntimeSnapshot();

        var cachedLineCountStartTicks = Stopwatch.GetTimestamp();
        UpdateCachedDocumentSnapshot(Document);
        var cachedLineCountElapsedTicks = Stopwatch.GetTimestamp() - cachedLineCountStartTicks;

        var updateLineNumberGutterStartTicks = Stopwatch.GetTimestamp();
        UpdateLineNumberGutter(force: true);
        var updateLineNumberGutterElapsedTicks = Stopwatch.GetTimestamp() - updateLineNumberGutterStartTicks;
        var lineNumberAfter = CaptureLineNumberPresenterRuntimeSnapshot();
        var lineNumberUpdateDeltaMs = Math.Max(0d, lineNumberAfter.UpdateVisibleRangeMilliseconds - lineNumberBefore.UpdateVisibleRangeMilliseconds);
        var lineNumberMeasureDeltaMs = Math.Max(0d, lineNumberAfter.MeasureOverrideMilliseconds - lineNumberBefore.MeasureOverrideMilliseconds);
        var lineNumberArrangeDeltaMs = Math.Max(0d, lineNumberAfter.ArrangeOverrideMilliseconds - lineNumberBefore.ArrangeOverrideMilliseconds);

        var indentInvalidateStartTicks = Stopwatch.GetTimestamp();
        InvalidateIndentGuideOverlay();
        var indentInvalidateElapsedTicks = Stopwatch.GetTimestamp() - indentInvalidateStartTicks;

        var subscriberStartTicks = Stopwatch.GetTimestamp();
        TextChanged?.Invoke(this, args);
        var subscriberElapsedTicks = Stopwatch.GetTimestamp() - subscriberStartTicks;
        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;

        _diagEditorTextChangedElapsedTicks += elapsedTicks;
        _diagEditorTextChangedUpdateCachedLineCountElapsedTicks += cachedLineCountElapsedTicks;
        _diagEditorTextChangedUpdateLineNumberGutterElapsedTicks += updateLineNumberGutterElapsedTicks;
        _diagEditorTextChangedLineNumberPresenterUpdateMilliseconds += lineNumberUpdateDeltaMs;
        _diagEditorTextChangedLineNumberPresenterMeasureMilliseconds += lineNumberMeasureDeltaMs;
        _diagEditorTextChangedLineNumberPresenterArrangeMilliseconds += lineNumberArrangeDeltaMs;
        _diagEditorTextChangedIndentInvalidateElapsedTicks += indentInvalidateElapsedTicks;
        _diagEditorTextChangedSubscriberElapsedTicks += subscriberElapsedTicks;

        _runtimeEditorTextChangedElapsedTicks += elapsedTicks;
        _runtimeEditorTextChangedUpdateCachedLineCountElapsedTicks += cachedLineCountElapsedTicks;
        _runtimeEditorTextChangedUpdateLineNumberGutterElapsedTicks += updateLineNumberGutterElapsedTicks;
        _runtimeEditorTextChangedLineNumberPresenterUpdateMilliseconds += lineNumberUpdateDeltaMs;
        _runtimeEditorTextChangedLineNumberPresenterMeasureMilliseconds += lineNumberMeasureDeltaMs;
        _runtimeEditorTextChangedLineNumberPresenterArrangeMilliseconds += lineNumberArrangeDeltaMs;
        _runtimeEditorTextChangedIndentInvalidateElapsedTicks += indentInvalidateElapsedTicks;
        _runtimeEditorTextChangedSubscriberElapsedTicks += subscriberElapsedTicks;

        _runtimeLastEditorTextChangedElapsedTicks = elapsedTicks;
        _runtimeLastEditorTextChangedUpdateCachedLineCountElapsedTicks = cachedLineCountElapsedTicks;
        _runtimeLastEditorTextChangedUpdateLineNumberGutterElapsedTicks = updateLineNumberGutterElapsedTicks;
        _runtimeLastEditorTextChangedLineNumberPresenterUpdateMilliseconds = lineNumberUpdateDeltaMs;
        _runtimeLastEditorTextChangedLineNumberPresenterMeasureMilliseconds = lineNumberMeasureDeltaMs;
        _runtimeLastEditorTextChangedLineNumberPresenterArrangeMilliseconds = lineNumberArrangeDeltaMs;
        _runtimeLastEditorTextChangedIndentInvalidateElapsedTicks = indentInvalidateElapsedTicks;
        _runtimeLastEditorTextChangedSubscriberElapsedTicks = subscriberElapsedTicks;
    }

    private void OnEditorDocumentChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        _diagEditorDocumentChangedCallCount++;
        _runtimeEditorDocumentChangedCallCount++;
        SyncDocumentFromEditor();
        UpdateCachedDocumentSnapshot(Document);
        UpdateLineNumberGutter(force: true);
        InvalidateIndentGuideOverlay();
    }

    private void OnEditorSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        _ = sender;
        InvalidateIndentGuideOverlay();
        SelectionChanged?.Invoke(this, args);
    }

    private void OnEditorViewportChanged(object? sender, EventArgs args)
    {
        _ = sender;
        _diagEditorViewportChangedCallCount++;
        _runtimeEditorViewportChangedCallCount++;
        RefreshViewportDependentPresentationIfNeeded();
        ViewportChanged?.Invoke(this, args);
    }

    private void OnEditorLayoutUpdated(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        _diagEditorLayoutUpdatedCallCount++;
        _runtimeEditorLayoutUpdatedCallCount++;
        RefreshViewportDependentPresentationIfNeeded();
    }

    private void InvalidateIndentGuideOverlay()
    {
        _isIndentGuideSnapshotDirty = true;

        if (_indentGuideOverlay == null)
        {
            return;
        }

        _diagIndentGuideInvalidateVisualCallCount++;
        _runtimeIndentGuideInvalidateVisualCallCount++;
        _indentGuideOverlay.InvalidateVisual();
    }

    private void DetachEditorPart()
    {
        if (_editor != null)
        {
            _editor.DocumentChanged -= OnEditorDocumentChanged;
            _editor.TextChanged -= OnEditorTextChanged;
            _editor.SelectionChanged -= OnEditorSelectionChanged;
            _editor.ViewportChanged -= OnEditorViewportChanged;
            _editor.LayoutUpdated -= OnEditorLayoutUpdated;
        }

        _root = null;
        _lineNumberBorder = null;
        _lineNumberSeparator = null;
        _lineNumberPresenter = null;
        if (_indentGuideOverlay != null)
        {
            _indentGuideOverlay.Owner = null;
        }

        _indentGuideOverlay = null;
        _editor = null;

        _lastRenderedLineCount = -1;
        _lastRenderedFirstVisibleLine = -1;
        _lastRenderedVisibleLineCount = -1;
        _lastRenderedLineOffset = float.NaN;
        _lastRenderedLineHeight = float.NaN;
        _cachedIndentGuideSnapshot = new IDEEditorIndentGuideSnapshot(false, default, Array.Empty<IDEEditorIndentGuideSegmentSnapshot>());
        _isIndentGuideSnapshotDirty = true;
        ResetViewportPresentationCache();
    }

    private void ApplyTemplateStyling()
    {
        if (_root != null && _root.ColumnDefinitions.Count > 0)
        {
            _root.ColumnDefinitions[0].Width = new GridLength(LineNumberWidth, GridUnitType.Pixel);
        }

        if (_lineNumberBorder != null)
        {
            _lineNumberBorder.Background = LineNumberBackground;
        }

        if (_lineNumberSeparator != null)
        {
            _lineNumberSeparator.Background = LineNumberSeparatorBrush;
        }

        if (_lineNumberPresenter != null)
        {
            _lineNumberPresenter.FontFamily = FontFamily;
            _lineNumberPresenter.FontSize = FontSize;
            _lineNumberPresenter.LineForeground = LineNumberForeground;
        }
    }

    private void SyncEditorProperties()
    {
        if (_editor == null)
        {
            return;
        }

        if (!ReferenceEquals(_editor.Document, Document))
        {
            _editor.Document = Document;
        }

        _editor.Foreground = Foreground;
        _editor.Padding = Padding;
        _editor.TextWrapping = TextWrapping;
        _editor.HorizontalScrollBarVisibility = HorizontalScrollBarVisibility;
        _editor.VerticalScrollBarVisibility = VerticalScrollBarVisibility;
        _editor.SelectionBrush = SelectionBrush;
        _editor.CaretBrush = CaretBrush;
        _editor.AcceptsReturn = AcceptsReturn;
        _editor.AcceptsTab = AcceptsTab;
        _editor.IsReadOnly = IsReadOnly;
        _editor.IsReadOnlyCaretVisible = IsReadOnlyCaretVisible;
        _editor.IsInactiveSelectionHighlightEnabled = IsInactiveSelectionHighlightEnabled;
        _editor.SelectionTextBrush = SelectionTextBrush;
        _editor.SelectionOpacity = SelectionOpacity;
        _editor.IsUndoEnabled = IsUndoEnabled;
        _editor.UndoLimit = UndoLimit;
        _editor.FontFamily = FontFamily;
        _editor.FontSize = FontSize;
    }

    private void SyncDocumentFromEditor()
    {
        if (_editor != null && !ReferenceEquals(Document, _editor.Document))
        {
            Document = _editor.Document;
        }
    }

    private void EnsureEditorFocusState()
    {
        if (_editor != null && IsFocused && !_editor.IsFocused)
        {
            _editor.SetFocusedFromInput(true);
        }
    }

    private void UpdateCachedLineCount(FlowDocument? document)
    {
        _cachedLineCount = CountLines(document);
    }

    private void UpdateCachedDocumentSnapshot(FlowDocument? document)
    {
        UpdateCachedLineCount(document);
        _cachedDocumentText = document == null ? string.Empty : DocumentEditing.GetText(document);
    }

    private void RefreshViewportDependentPresentationIfNeeded()
    {
        if (_editor == null)
        {
            return;
        }

        var metrics = GetScrollMetricsSnapshot();
        if (Math.Abs(metrics.HorizontalOffset - _lastViewportPresentationHorizontalOffset) <= 0.01f &&
            Math.Abs(metrics.VerticalOffset - _lastViewportPresentationVerticalOffset) <= 0.01f &&
            Math.Abs(metrics.ViewportWidth - _lastViewportPresentationViewportWidth) <= 0.01f &&
            Math.Abs(metrics.ViewportHeight - _lastViewportPresentationViewportHeight) <= 0.01f &&
            Math.Abs(metrics.ExtentWidth - _lastViewportPresentationExtentWidth) <= 0.01f &&
            Math.Abs(metrics.ExtentHeight - _lastViewportPresentationExtentHeight) <= 0.01f)
        {
            return;
        }

        _lastViewportPresentationHorizontalOffset = metrics.HorizontalOffset;
        _lastViewportPresentationVerticalOffset = metrics.VerticalOffset;
        _lastViewportPresentationViewportWidth = metrics.ViewportWidth;
        _lastViewportPresentationViewportHeight = metrics.ViewportHeight;
        _lastViewportPresentationExtentWidth = metrics.ExtentWidth;
        _lastViewportPresentationExtentHeight = metrics.ExtentHeight;
        UpdateLineNumberGutter(force: false);
        InvalidateIndentGuideOverlay();
    }

    private void ResetViewportPresentationCache()
    {
        _lastViewportPresentationHorizontalOffset = float.NaN;
        _lastViewportPresentationVerticalOffset = float.NaN;
        _lastViewportPresentationViewportWidth = float.NaN;
        _lastViewportPresentationViewportHeight = float.NaN;
        _lastViewportPresentationExtentWidth = float.NaN;
        _lastViewportPresentationExtentHeight = float.NaN;
    }

    private void UpdateLineNumberGutter(bool force)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagUpdateLineNumberGutterCallCount++;
        _runtimeUpdateLineNumberGutterCallCount++;
        if (force)
        {
            _diagUpdateLineNumberGutterForcedCount++;
            _runtimeUpdateLineNumberGutterForcedCount++;
        }

        if (_editor == null || _lineNumberPresenter == null)
        {
            var earlyElapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _diagUpdateLineNumberGutterElapsedTicks += earlyElapsedTicks;
            _runtimeUpdateLineNumberGutterElapsedTicks += earlyElapsedTicks;
            return;
        }

        var lineCount = Math.Max(1, _cachedLineCount);
        var lineHeight = EstimateLineHeight(lineCount);
        var viewportHeight = Math.Max(lineHeight, _editor.ViewportHeight);
        var verticalOffset = Math.Max(0f, _editor.VerticalOffset);
        var approximateVisibleLineCount = Math.Clamp((int)MathF.Ceiling(viewportHeight / lineHeight) + 1, 1, Math.Max(1, lineCount));
        var firstVisibleLine = GetFirstVisibleLine(lineCount, approximateVisibleLineCount, lineHeight, verticalOffset);
        var visibleLineCount = Math.Clamp(approximateVisibleLineCount, 1, Math.Max(1, lineCount - firstVisibleLine));
        var lineOffset = verticalOffset - (firstVisibleLine * lineHeight);

        if (!force &&
            lineCount == _lastRenderedLineCount &&
            firstVisibleLine == _lastRenderedFirstVisibleLine &&
            visibleLineCount == _lastRenderedVisibleLineCount &&
            Math.Abs(lineOffset - _lastRenderedLineOffset) <= 0.01f &&
            Math.Abs(lineHeight - _lastRenderedLineHeight) <= 0.01f)
        {
            _diagUpdateLineNumberGutterNoOpCount++;
            _runtimeUpdateLineNumberGutterNoOpCount++;
            var noOpElapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _diagUpdateLineNumberGutterElapsedTicks += noOpElapsedTicks;
            _runtimeUpdateLineNumberGutterElapsedTicks += noOpElapsedTicks;
            return;
        }

        _lineNumberPresenter.LineHeight = lineHeight;
        _lineNumberPresenter.FontSize = FontSize;
        _lineNumberPresenter.VerticalLineOffset = lineOffset;
        _lineNumberPresenter.UpdateVisibleRange(firstVisibleLine, visibleLineCount);

        _diagUpdateLineNumberGutterAppliedCount++;
        _runtimeUpdateLineNumberGutterAppliedCount++;
        _diagUpdateLineNumberGutterVisibleLineTotal += visibleLineCount;
        _runtimeUpdateLineNumberGutterVisibleLineTotal += visibleLineCount;

        _lastRenderedLineCount = lineCount;
        _lastRenderedFirstVisibleLine = firstVisibleLine;
        _lastRenderedVisibleLineCount = visibleLineCount;
        _lastRenderedLineOffset = lineOffset;
        _lastRenderedLineHeight = lineHeight;

        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _diagUpdateLineNumberGutterElapsedTicks += elapsedTicks;
        _runtimeUpdateLineNumberGutterElapsedTicks += elapsedTicks;
    }

    private bool TryBuildIndentGuideSnapshot(out IDEEditorIndentGuideSnapshot snapshot)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagBuildIndentGuideSnapshotCallCount++;
        _runtimeBuildIndentGuideSnapshotCallCount++;
        snapshot = new IDEEditorIndentGuideSnapshot(false, default, Array.Empty<IDEEditorIndentGuideSegmentSnapshot>());
        if (_editor == null || !_editor.TryGetViewportLayoutSnapshot(out var viewport))
        {
            var failedElapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _diagBuildIndentGuideSnapshotElapsedTicks += failedElapsedTicks;
            _runtimeBuildIndentGuideSnapshotElapsedTicks += failedElapsedTicks;
            return false;
        }

        var segments = BuildIndentGuideSegments(viewport).ToArray();
        snapshot = new IDEEditorIndentGuideSnapshot(true, viewport.TextRect, segments);
        _diagBuildIndentGuideSnapshotSuccessCount++;
        _runtimeBuildIndentGuideSnapshotSuccessCount++;
        _diagBuildIndentGuideSnapshotSegmentTotal += segments.Length;
        _runtimeBuildIndentGuideSnapshotSegmentTotal += segments.Length;
        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _diagBuildIndentGuideSnapshotElapsedTicks += elapsedTicks;
        _runtimeBuildIndentGuideSnapshotElapsedTicks += elapsedTicks;
        return true;
    }

    private static IDEEditorTelemetrySnapshot CreateTelemetrySnapshot(bool reset)
    {
        var snapshot = new IDEEditorTelemetrySnapshot(
            UpdateLineNumberGutterCallCount: _diagUpdateLineNumberGutterCallCount,
            UpdateLineNumberGutterForcedCount: _diagUpdateLineNumberGutterForcedCount,
            UpdateLineNumberGutterAppliedCount: _diagUpdateLineNumberGutterAppliedCount,
            UpdateLineNumberGutterNoOpCount: _diagUpdateLineNumberGutterNoOpCount,
            UpdateLineNumberGutterVisibleLineTotal: _diagUpdateLineNumberGutterVisibleLineTotal,
            UpdateLineNumberGutterMilliseconds: TicksToMilliseconds(_diagUpdateLineNumberGutterElapsedTicks),
            EditorTextChangedCallCount: _diagEditorTextChangedCallCount,
            EditorTextChangedMilliseconds: TicksToMilliseconds(_diagEditorTextChangedElapsedTicks),
            EditorTextChangedUpdateCachedLineCountMilliseconds: TicksToMilliseconds(_diagEditorTextChangedUpdateCachedLineCountElapsedTicks),
            EditorTextChangedUpdateLineNumberGutterMilliseconds: TicksToMilliseconds(_diagEditorTextChangedUpdateLineNumberGutterElapsedTicks),
            EditorTextChangedLineNumberPresenterUpdateMilliseconds: _diagEditorTextChangedLineNumberPresenterUpdateMilliseconds,
            EditorTextChangedLineNumberPresenterMeasureMilliseconds: _diagEditorTextChangedLineNumberPresenterMeasureMilliseconds,
            EditorTextChangedLineNumberPresenterArrangeMilliseconds: _diagEditorTextChangedLineNumberPresenterArrangeMilliseconds,
            EditorTextChangedIndentInvalidateMilliseconds: TicksToMilliseconds(_diagEditorTextChangedIndentInvalidateElapsedTicks),
            EditorTextChangedSubscriberMilliseconds: TicksToMilliseconds(_diagEditorTextChangedSubscriberElapsedTicks),
            EditorDocumentChangedCallCount: _diagEditorDocumentChangedCallCount,
            EditorViewportChangedCallCount: _diagEditorViewportChangedCallCount,
            EditorLayoutUpdatedCallCount: _diagEditorLayoutUpdatedCallCount,
            IndentGuideInvalidateVisualCallCount: _diagIndentGuideInvalidateVisualCallCount,
            BuildIndentGuideSnapshotCallCount: _diagBuildIndentGuideSnapshotCallCount,
            BuildIndentGuideSnapshotSuccessCount: _diagBuildIndentGuideSnapshotSuccessCount,
            BuildIndentGuideSnapshotSegmentTotal: _diagBuildIndentGuideSnapshotSegmentTotal,
            BuildIndentGuideSnapshotMilliseconds: TicksToMilliseconds(_diagBuildIndentGuideSnapshotElapsedTicks));

        if (reset)
        {
            _diagUpdateLineNumberGutterCallCount = 0;
            _diagUpdateLineNumberGutterForcedCount = 0;
            _diagUpdateLineNumberGutterAppliedCount = 0;
            _diagUpdateLineNumberGutterNoOpCount = 0;
            _diagUpdateLineNumberGutterVisibleLineTotal = 0;
            _diagUpdateLineNumberGutterElapsedTicks = 0;
            _diagEditorTextChangedCallCount = 0;
            _diagEditorTextChangedElapsedTicks = 0;
            _diagEditorTextChangedUpdateCachedLineCountElapsedTicks = 0;
            _diagEditorTextChangedUpdateLineNumberGutterElapsedTicks = 0;
            _diagEditorTextChangedLineNumberPresenterUpdateMilliseconds = 0d;
            _diagEditorTextChangedLineNumberPresenterMeasureMilliseconds = 0d;
            _diagEditorTextChangedLineNumberPresenterArrangeMilliseconds = 0d;
            _diagEditorTextChangedIndentInvalidateElapsedTicks = 0;
            _diagEditorTextChangedSubscriberElapsedTicks = 0;
            _diagEditorDocumentChangedCallCount = 0;
            _diagEditorViewportChangedCallCount = 0;
            _diagEditorLayoutUpdatedCallCount = 0;
            _diagIndentGuideInvalidateVisualCallCount = 0;
            _diagBuildIndentGuideSnapshotCallCount = 0;
            _diagBuildIndentGuideSnapshotSuccessCount = 0;
            _diagBuildIndentGuideSnapshotSegmentTotal = 0;
            _diagBuildIndentGuideSnapshotElapsedTicks = 0;
        }

        return snapshot;
    }

    private IDEEditorLineNumberPresenterRuntimeDiagnosticsSnapshot CaptureLineNumberPresenterRuntimeSnapshot()
    {
        return _lineNumberPresenter?.GetIDEEditorLineNumberPresenterSnapshotForDiagnostics() ?? default;
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }

    private IEnumerable<IDEEditorIndentGuideSegmentSnapshot> BuildIndentGuideSegments(RichTextBoxViewportLayoutSnapshot viewport)
    {
        var layout = viewport.Layout;
        if (layout.Lines.Count == 0)
        {
            yield break;
        }

        var documentText = _cachedDocumentText;
        var indentStepWidth = ResolveIndentStepWidth(layout, documentText);
        if (indentStepWidth <= 0.01f)
        {
            yield break;
        }

        var logicalLineInfoCache = BuildLogicalLineInfoCache(layout, documentText, indentStepWidth);

        var activeSegments = new Dictionary<int, IDEEditorIndentGuideSegmentSnapshot>();
        for (var lineIndex = 0; lineIndex < layout.Lines.Count; lineIndex++)
        {
            var line = layout.Lines[lineIndex];
            var rawTop = viewport.TextRect.Y + line.Bounds.Y - viewport.VerticalOffset;
            var rawBottom = rawTop + line.Bounds.Height;
            if (rawBottom <= viewport.TextRect.Y || rawTop >= viewport.TextRect.Y + viewport.TextRect.Height)
            {
                continue;
            }

            var top = Math.Max(viewport.TextRect.Y, rawTop);
            var bottom = Math.Min(viewport.TextRect.Y + viewport.TextRect.Height, rawBottom);
            var logicalLineInfo = ResolveLogicalLineInfo(documentText, line.StartOffset, logicalLineInfoCache);
            var visibleLevels = new HashSet<int>();

            if (logicalLineInfo.HasNonWhitespaceContent && logicalLineInfo.IndentLevelCount > 0)
            {
                var levelCount = logicalLineInfo.IndentLevelCount;
                for (var indentLevel = 1; indentLevel <= levelCount; indentLevel++)
                {
                    visibleLevels.Add(indentLevel);
                    var x = viewport.TextRect.X + line.TextStartX + (indentStepWidth * indentLevel) - viewport.HorizontalOffset;
                    if (x < viewport.TextRect.X || x > viewport.TextRect.X + viewport.TextRect.Width)
                    {
                        continue;
                    }

                    if (activeSegments.TryGetValue(indentLevel, out var activeSegment) && activeSegment.EndVisibleLineIndex == lineIndex - 1)
                    {
                        activeSegments[indentLevel] = activeSegment with
                        {
                            EndVisibleLineIndex = lineIndex,
                            Bottom = bottom
                        };
                    }
                    else
                    {
                        if (activeSegments.TryGetValue(indentLevel, out activeSegment))
                        {
                            yield return activeSegment;
                        }

                        activeSegments[indentLevel] = new IDEEditorIndentGuideSegmentSnapshot(indentLevel, lineIndex, lineIndex, x, top, bottom);
                    }
                }
            }
            else if (logicalLineInfo.IsBlankLine && activeSegments.Count > 0)
            {
                var continuationLevelCount = ResolveContinuationIndentLevelCount(layout, documentText, lineIndex, logicalLineInfoCache);
                for (var indentLevel = 1; indentLevel <= continuationLevelCount; indentLevel++)
                {
                    if (!activeSegments.TryGetValue(indentLevel, out var activeSegment))
                    {
                        continue;
                    }

                    visibleLevels.Add(indentLevel);
                    activeSegments[indentLevel] = activeSegment with
                    {
                        EndVisibleLineIndex = lineIndex,
                        Bottom = bottom
                    };
                }
            }

            foreach (var entry in activeSegments.Where(entry => !visibleLevels.Contains(entry.Key)).ToArray())
            {
                yield return entry.Value;
                activeSegments.Remove(entry.Key);
            }
        }

        foreach (var segment in activeSegments.Values.OrderBy(static segment => segment.IndentLevel).ThenBy(static segment => segment.StartVisibleLineIndex))
        {
            yield return segment;
        }
    }

    private static Dictionary<int, IDEEditorLogicalLineInfo> BuildLogicalLineInfoCache(DocumentLayoutResult layout, string documentText, float indentStepWidth)
    {
        var cache = new Dictionary<int, IDEEditorLogicalLineInfo>();
        var useXmlContinuationHeuristics = LooksLikeXmlDocument(documentText);
        var visibleLogicalLineStarts = ResolveVisibleLogicalLineStarts(layout, documentText);
        var lastStructuralIndentLevelCount = useXmlContinuationHeuristics && visibleLogicalLineStarts.Count > 0
            ? ResolvePreviousStructuralIndentLevelCount(layout, documentText, visibleLogicalLineStarts[0], indentStepWidth)
            : 0;

        for (var index = 0; index < visibleLogicalLineStarts.Count; index++)
        {
            var logicalLineStart = visibleLogicalLineStarts[index];
            var lineText = ResolveLogicalLineText(documentText, logicalLineStart);
            var trimmedLineText = lineText.TrimStart();
            if (trimmedLineText.Length == 0)
            {
                cache[logicalLineStart] = new IDEEditorLogicalLineInfo(false, true, lastStructuralIndentLevelCount);
            }
            else
            {
                var indentLevelCount = ResolveLogicalLineIndentLevelCount(layout, documentText, logicalLineStart, indentStepWidth);
                if (useXmlContinuationHeuristics && !IsXmlStructuralLine(trimmedLineText))
                {
                    indentLevelCount = lastStructuralIndentLevelCount;
                }
                else
                {
                    lastStructuralIndentLevelCount = indentLevelCount;
                }

                cache[logicalLineStart] = new IDEEditorLogicalLineInfo(true, false, indentLevelCount);
            }

        }

        return cache;
    }

    private static List<int> ResolveVisibleLogicalLineStarts(DocumentLayoutResult layout, string documentText)
    {
        var starts = new List<int>();
        var seenStarts = new HashSet<int>();
        for (var lineIndex = 0; lineIndex < layout.Lines.Count; lineIndex++)
        {
            var logicalLineStart = ResolveLogicalLineStart(documentText, layout.Lines[lineIndex].StartOffset);
            if (seenStarts.Add(logicalLineStart))
            {
                starts.Add(logicalLineStart);
            }
        }

        return starts;
    }

    private static int ResolvePreviousStructuralIndentLevelCount(DocumentLayoutResult layout, string documentText, int firstVisibleLogicalLineStart, float indentStepWidth)
    {
        for (var logicalLineStart = ResolvePreviousLogicalLineStart(documentText, firstVisibleLogicalLineStart);
             logicalLineStart >= 0;
             logicalLineStart = ResolvePreviousLogicalLineStart(documentText, logicalLineStart))
        {
            var trimmedLineText = ResolveLogicalLineText(documentText, logicalLineStart).TrimStart();
            if (trimmedLineText.Length == 0 || !IsXmlStructuralLine(trimmedLineText))
            {
                continue;
            }

            return ResolveLogicalLineIndentLevelCount(layout, documentText, logicalLineStart, indentStepWidth);
        }

        return 0;
    }

    private static int ResolveLogicalLineIndentLevelCount(DocumentLayoutResult layout, string documentText, int logicalLineStart, float indentStepWidth)
    {
        var leadingWhitespaceCount = ResolveLogicalLineLeadingWhitespaceCount(documentText, logicalLineStart);
        if (leadingWhitespaceCount <= 0)
        {
            return 0;
        }

        if (!layout.TryGetCaretPosition(logicalLineStart, out var lineStartPosition) ||
            !layout.TryGetCaretPosition(logicalLineStart + leadingWhitespaceCount, out var firstContentPosition))
        {
            return 0;
        }

        var indentWidth = Math.Max(0f, firstContentPosition.X - lineStartPosition.X);
        if (indentWidth <= 0.01f || indentStepWidth <= 0.01f)
        {
            return 0;
        }

        return Math.Max(0, (int)MathF.Floor((indentWidth + 0.01f) / indentStepWidth));
    }

    private static int ResolveContinuationIndentLevelCount(DocumentLayoutResult layout, string documentText, int lineIndex, IReadOnlyDictionary<int, IDEEditorLogicalLineInfo> logicalLineInfoCache)
    {
        for (var nextLineIndex = lineIndex + 1; nextLineIndex < layout.Lines.Count; nextLineIndex++)
        {
            var nextLine = layout.Lines[nextLineIndex];
            var logicalLineInfo = ResolveLogicalLineInfo(documentText, nextLine.StartOffset, logicalLineInfoCache);
            if (logicalLineInfo.IsBlankLine)
            {
                continue;
            }

            return logicalLineInfo.IndentLevelCount;
        }

        for (var prevLineIndex = lineIndex - 1; prevLineIndex >= 0; prevLineIndex--)
        {
            var prevLine = layout.Lines[prevLineIndex];
            var logicalLineInfo = ResolveLogicalLineInfo(documentText, prevLine.StartOffset, logicalLineInfoCache);
            if (logicalLineInfo.IsBlankLine)
            {
                continue;
            }

            return logicalLineInfo.IndentLevelCount;
        }

        return 0;
    }

    private static IDEEditorLogicalLineInfo ResolveLogicalLineInfo(string documentText, int offset, IReadOnlyDictionary<int, IDEEditorLogicalLineInfo> logicalLineInfoCache)
    {
        if (string.IsNullOrEmpty(documentText))
        {
            return new IDEEditorLogicalLineInfo(false, true, 0);
        }

        var clampedOffset = Math.Clamp(offset, 0, documentText.Length);
        var lineStart = ResolveLogicalLineStart(documentText, clampedOffset);
        return logicalLineInfoCache.TryGetValue(lineStart, out var logicalLineInfo)
            ? logicalLineInfo
            : new IDEEditorLogicalLineInfo(false, true, 0);
    }

    private static int ResolveLogicalLineStart(string documentText, int offset)
    {
        var lineStart = Math.Clamp(offset, 0, documentText.Length);
        while (lineStart > 0 && documentText[lineStart - 1] != '\n')
        {
            lineStart--;
        }

        return lineStart;
    }

    private static int ResolvePreviousLogicalLineStart(string documentText, int logicalLineStart)
    {
        if (logicalLineStart <= 0)
        {
            return -1;
        }

        var index = Math.Min(logicalLineStart - 1, documentText.Length - 1);
        if (index >= 0 && documentText[index] == '\n')
        {
            index--;
        }

        while (index >= 0 && documentText[index] != '\n')
        {
            index--;
        }

        return index + 1;
    }

    private static int ResolveLogicalLineLeadingWhitespaceCount(string documentText, int logicalLineStart)
    {
        return CountLeadingWhitespace(ResolveLogicalLineText(documentText, logicalLineStart));
    }

    private static string ResolveLogicalLineText(string documentText, int logicalLineStart)
    {
        var lineEnd = logicalLineStart;
        while (lineEnd < documentText.Length && documentText[lineEnd] != '\n')
        {
            lineEnd++;
        }

        return documentText.Substring(logicalLineStart, lineEnd - logicalLineStart);
    }

    private static bool LooksLikeXmlDocument(string documentText)
    {
        for (var index = 0; index < documentText.Length; index++)
        {
            if (char.IsWhiteSpace(documentText[index]))
            {
                continue;
            }

            return documentText[index] == '<';
        }

        return false;
    }

    private static bool IsXmlStructuralLine(string trimmedLineText)
    {
        return trimmedLineText.StartsWith("<", StringComparison.Ordinal);
    }

    private static int AdvanceToNextLogicalLine(string documentText, int logicalLineStart)
    {
        if (logicalLineStart >= documentText.Length)
        {
            return logicalLineStart + 1;
        }

        var index = logicalLineStart;
        while (index < documentText.Length && documentText[index] != '\n')
        {
            index++;
        }

        return index < documentText.Length ? index + 1 : documentText.Length;
    }

    private static int ResolveIndentLevelCount(DocumentLayoutLine line, float indentStepWidth)
    {
        var leadingWhitespaceCount = CountLeadingWhitespace(line.Text);
        if (leadingWhitespaceCount <= 0 || leadingWhitespaceCount >= line.Text.Length)
        {
            return 0;
        }

        var totalIndentWidth = line.PrefixWidths[leadingWhitespaceCount];
        return Math.Max(0, (int)MathF.Floor((totalIndentWidth + 0.01f) / indentStepWidth));
    }

    private static float ResolveIndentStepWidth(DocumentLayoutResult layout, string documentText)
    {
        var indentStepWidth = float.PositiveInfinity;
        var visibleLogicalLineStarts = ResolveVisibleLogicalLineStarts(layout, documentText);
        for (var index = 0; index < visibleLogicalLineStarts.Count; index++)
        {
            var logicalLineStart = visibleLogicalLineStarts[index];
            var leadingWhitespaceCount = ResolveLogicalLineLeadingWhitespaceCount(documentText, logicalLineStart);
            if (leadingWhitespaceCount <= 0)
            {
                continue;
            }

            if (!layout.TryGetCaretPosition(logicalLineStart, out var lineStartPosition) ||
                !layout.TryGetCaretPosition(logicalLineStart + leadingWhitespaceCount, out var firstContentPosition))
            {
                continue;
            }

            var indentWidth = Math.Max(0f, firstContentPosition.X - lineStartPosition.X);
            if (indentWidth > 0.01f && indentWidth < indentStepWidth)
            {
                indentStepWidth = indentWidth;
            }
        }

        return float.IsFinite(indentStepWidth) ? indentStepWidth : 0f;
    }

    private static int CountLeadingWhitespace(string text)
    {
        var count = 0;
        while (count < text.Length && char.IsWhiteSpace(text[count]))
        {
            count++;
        }

        return count;
    }

    private bool TryGetSelectionLineHeight(out float lineHeight)
    {
        lineHeight = 0f;
        if (_editor == null || !_editor.TryGetViewportLayoutSnapshot(out var viewport))
        {
            return false;
        }

        var selectionStart = SelectionStart;
        var lines = viewport.Layout.Lines;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var endOffset = line.StartOffset + line.Length;
            if (selectionStart > endOffset)
            {
                continue;
            }

            if (line.Bounds.Height <= 0.01f)
            {
                return false;
            }

            lineHeight = line.Bounds.Height;
            return true;
        }

        if (lines.Count == 0)
        {
            return false;
        }

        var lastLineHeight = lines[lines.Count - 1].Bounds.Height;
        if (lastLineHeight <= 0.01f)
        {
            return false;
        }

        lineHeight = lastLineHeight;
        return true;
    }

    private float EstimateLineHeight(int lineCount)
    {
        if (TryGetSelectionLineHeight(out var lineHeight))
        {
            return Math.Max(1f, lineHeight);
        }

        if (_editor != null && lineCount > 0 && _editor.ExtentHeight > 0.01f)
        {
            return Math.Max(1f, _editor.ExtentHeight / lineCount);
        }

        return Math.Max(1f, FontSize * 1.35f);
    }

    private int GetFirstVisibleLine(int lineCount, int approximateVisibleLineCount, float lineHeight, float verticalOffset)
    {
        if (_editor == null)
        {
            return 0;
        }

        return Math.Clamp((int)MathF.Floor(verticalOffset / lineHeight), 0, Math.Max(0, lineCount - 1));
    }

    private static int CountLines(FlowDocument? document)
    {
        if (document == null)
        {
            return 1;
        }

        var lineCount = 0;
        foreach (var paragraph in FlowDocumentPlainText.EnumerateParagraphs(document))
        {
            lineCount++;
            lineCount += CountInlineLineBreaks(paragraph.Inlines);
        }

        return Math.Max(1, lineCount);
    }

    private static int CountInlineLineBreaks(IEnumerable<Inline> inlines)
    {
        var lineBreakCount = 0;
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    lineBreakCount += CountTextLineBreaks(run.Text);
                    break;
                case LineBreak:
                    lineBreakCount++;
                    break;
                case Span span:
                    lineBreakCount += CountInlineLineBreaks(span.Inlines);
                    break;
            }
        }

        return lineBreakCount;
    }

    private static int CountTextLineBreaks(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var lineCount = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\n')
            {
                lineCount++;
            }
            else if (text[index] == '\r')
            {
                lineCount++;
                if (index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }
            }
        }

        return lineCount;
    }

    private static FlowDocument CreateDefaultDocument()
    {
        var document = new FlowDocument();
        document.Blocks.Add(new Paragraph());
        return document;
    }

    private static Style BuildDefaultStyle()
    {
        var style = new Style(typeof(IDE_Editor));
        style.Setters.Add(new Setter(BackgroundProperty, new Color(13, 19, 26)));
        style.Setters.Add(new Setter(BorderBrushProperty, new Color(26, 38, 55)));
        style.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(0f)));
        style.Setters.Add(new Setter(ForegroundProperty, new Color(216, 227, 238)));
        style.Setters.Add(new Setter(PaddingProperty, new Thickness(10f)));
        style.Setters.Add(new Setter(FrameworkElement.FontFamilyProperty, new FontFamily("Consolas")));
        style.Setters.Add(new Setter(FrameworkElement.FontSizeProperty, 12f));
        style.Setters.Add(new Setter(TemplateProperty, BuildDefaultTemplate()));
        return style;
    }

    private static ControlTemplate BuildDefaultTemplate()
    {
        var template = new ControlTemplate(static _ =>
        {
            var border = new Border
            {
                Name = "PART_Border"
            };

            var root = new Grid
            {
                Name = "PART_Root"
            };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56f, GridUnitType.Pixel) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1f, GridUnitType.Pixel) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

            var lineNumberBorder = new Border
            {
                Name = "PART_LineNumberBorder",
                ClipToBounds = true
            };

            var lineNumberPresenter = new IDEEditorLineNumberPresenter
            {
                Name = "PART_LineNumberPresenter",
                Margin = new Thickness(0f, 10f, 6f, 0f)
            };
            lineNumberBorder.Child = lineNumberPresenter;
            Grid.SetColumn(lineNumberBorder, 0);
            root.AddChild(lineNumberBorder);

            var separator = new Border
            {
                Name = "PART_LineNumberSeparator"
            };
            Grid.SetColumn(separator, 1);
            root.AddChild(separator);

            var editor = new RichTextBox
            {
                Name = "PART_Editor",
                Background = Color.Transparent,
                BorderThickness = 0f
            };
            Grid.SetColumn(editor, 2);
            root.AddChild(editor);

            var indentGuideOverlay = new IDEEditorIndentGuideOverlay
            {
                Name = "PART_IndentGuideOverlay"
            };
            Grid.SetColumn(indentGuideOverlay, 2);
            root.AddChild(indentGuideOverlay);

            border.Child = root;
            return border;
        })
        {
            TargetType = typeof(IDE_Editor)
        };

        template.BindTemplate("PART_Border", Border.BackgroundProperty, BackgroundProperty);
        template.BindTemplate("PART_Border", Border.BorderBrushProperty, BorderBrushProperty);
        template.BindTemplate("PART_Border", Border.BorderThicknessProperty, BorderThicknessProperty);
        template.BindTemplate("PART_Editor", RichTextBox.DocumentProperty, DocumentProperty);
        template.BindTemplate("PART_Editor", RichTextBox.ForegroundProperty, ForegroundProperty);
        template.BindTemplate("PART_Editor", RichTextBox.PaddingProperty, PaddingProperty);
        template.BindTemplate("PART_Editor", RichTextBox.TextWrappingProperty, TextWrappingProperty);
        template.BindTemplate("PART_Editor", RichTextBox.HorizontalScrollBarVisibilityProperty, HorizontalScrollBarVisibilityProperty);
        template.BindTemplate("PART_Editor", RichTextBox.VerticalScrollBarVisibilityProperty, VerticalScrollBarVisibilityProperty);
        template.BindTemplate("PART_Editor", RichTextBox.SelectionBrushProperty, SelectionBrushProperty);
        template.BindTemplate("PART_Editor", RichTextBox.CaretBrushProperty, CaretBrushProperty);
        template.BindTemplate("PART_Editor", RichTextBox.AcceptsReturnProperty, AcceptsReturnProperty);
        template.BindTemplate("PART_Editor", RichTextBox.AcceptsTabProperty, AcceptsTabProperty);
        template.BindTemplate("PART_Editor", RichTextBox.IsReadOnlyProperty, IsReadOnlyProperty);
        template.BindTemplate("PART_Editor", RichTextBox.IsReadOnlyCaretVisibleProperty, IsReadOnlyCaretVisibleProperty);
        template.BindTemplate("PART_Editor", RichTextBox.IsInactiveSelectionHighlightEnabledProperty, IsInactiveSelectionHighlightEnabledProperty);
        template.BindTemplate("PART_Editor", RichTextBox.SelectionTextBrushProperty, SelectionTextBrushProperty);
        template.BindTemplate("PART_Editor", RichTextBox.SelectionOpacityProperty, SelectionOpacityProperty);
        template.BindTemplate("PART_Editor", RichTextBox.IsUndoEnabledProperty, IsUndoEnabledProperty);
        template.BindTemplate("PART_Editor", RichTextBox.UndoLimitProperty, UndoLimitProperty);
        template.BindTemplate("PART_Editor", FrameworkElement.FontFamilyProperty, FrameworkElement.FontFamilyProperty);
        template.BindTemplate("PART_Editor", FrameworkElement.FontSizeProperty, FrameworkElement.FontSizeProperty);

        return template;
    }
}

internal readonly record struct IDEEditorIndentGuideSnapshot(
    bool HasTextViewport,
    LayoutRect TextRect,
    IReadOnlyList<IDEEditorIndentGuideSegmentSnapshot> Segments);

internal readonly record struct IDEEditorIndentGuideSegmentSnapshot(
    int IndentLevel,
    int StartVisibleLineIndex,
    int EndVisibleLineIndex,
    float X,
    float Top,
    float Bottom);

internal readonly record struct IDEEditorLogicalLineInfo(
    bool HasNonWhitespaceContent,
    bool IsBlankLine,
    int IndentLevelCount);

internal sealed class IDEEditorIndentGuideOverlay : FrameworkElement
{
    private static int _diagRenderCallCount;
    private static int _diagRenderSkippedNoOwnerCount;
    private static int _diagRenderSkippedEmptySnapshotCount;
    private static int _diagRenderSegmentTotal;
    private static long _diagRenderElapsedTicks;

    public IDE_Editor? Owner { get; set; }
    private int _runtimeRenderCallCount;
    private int _runtimeRenderSkippedNoOwnerCount;
    private int _runtimeRenderSkippedEmptySnapshotCount;
    private int _runtimeRenderSegmentTotal;
    private long _runtimeRenderElapsedTicks;

    public IDEEditorIndentGuideOverlay()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        return new Vector2(
            float.IsFinite(availableSize.X) ? Math.Max(0f, availableSize.X) : 0f,
            float.IsFinite(availableSize.Y) ? Math.Max(0f, availableSize.Y) : 0f);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagRenderCallCount++;
        _runtimeRenderCallCount++;
        if (Owner == null)
        {
            _diagRenderSkippedNoOwnerCount++;
            _runtimeRenderSkippedNoOwnerCount++;
            var noOwnerElapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _diagRenderElapsedTicks += noOwnerElapsedTicks;
            _runtimeRenderElapsedTicks += noOwnerElapsedTicks;
            return;
        }

        var snapshot = Owner.GetIndentGuideSnapshotForRender();
        if (!snapshot.HasTextViewport || snapshot.Segments.Count == 0)
        {
            _diagRenderSkippedEmptySnapshotCount++;
            _runtimeRenderSkippedEmptySnapshotCount++;
            var emptyElapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _diagRenderElapsedTicks += emptyElapsedTicks;
            _runtimeRenderElapsedTicks += emptyElapsedTicks;
            return;
        }

        _diagRenderSegmentTotal += snapshot.Segments.Count;
        _runtimeRenderSegmentTotal += snapshot.Segments.Count;

        UiDrawing.PushClip(spriteBatch, snapshot.TextRect);
        try
        {
            for (var index = 0; index < snapshot.Segments.Count; index++)
            {
                var segment = snapshot.Segments[index];
                var height = Math.Max(1f, segment.Bottom - segment.Top);
                UiDrawing.DrawFilledRect(
                    spriteBatch,
                    new LayoutRect(segment.X, segment.Top, 1f, height),
                    Owner.IndentGuideBrush,
                    Owner.Opacity);
            }
        }
        finally
        {
            UiDrawing.PopClip(spriteBatch);
        }

        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _diagRenderElapsedTicks += elapsedTicks;
        _runtimeRenderElapsedTicks += elapsedTicks;
    }

    internal IDEEditorIndentGuideOverlayRuntimeDiagnosticsSnapshot GetIDEEditorIndentGuideOverlaySnapshotForDiagnostics()
    {
        return new IDEEditorIndentGuideOverlayRuntimeDiagnosticsSnapshot(
            RenderCallCount: _runtimeRenderCallCount,
            RenderSkippedNoOwnerCount: _runtimeRenderSkippedNoOwnerCount,
            RenderSkippedEmptySnapshotCount: _runtimeRenderSkippedEmptySnapshotCount,
            RenderSegmentTotal: _runtimeRenderSegmentTotal,
            RenderMilliseconds: TicksToMilliseconds(_runtimeRenderElapsedTicks),
            HasOwner: Owner != null);
    }

    internal new static IDEEditorIndentGuideOverlayTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return CreateTelemetrySnapshot(reset: false);
    }

    internal new static IDEEditorIndentGuideOverlayTelemetrySnapshot GetTelemetryAndReset()
    {
        return CreateTelemetrySnapshot(reset: true);
    }

    private static IDEEditorIndentGuideOverlayTelemetrySnapshot CreateTelemetrySnapshot(bool reset)
    {
        var snapshot = new IDEEditorIndentGuideOverlayTelemetrySnapshot(
            RenderCallCount: _diagRenderCallCount,
            RenderSkippedNoOwnerCount: _diagRenderSkippedNoOwnerCount,
            RenderSkippedEmptySnapshotCount: _diagRenderSkippedEmptySnapshotCount,
            RenderSegmentTotal: _diagRenderSegmentTotal,
            RenderMilliseconds: TicksToMilliseconds(_diagRenderElapsedTicks));

        if (reset)
        {
            _diagRenderCallCount = 0;
            _diagRenderSkippedNoOwnerCount = 0;
            _diagRenderSkippedEmptySnapshotCount = 0;
            _diagRenderSegmentTotal = 0;
            _diagRenderElapsedTicks = 0;
        }

        return snapshot;
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }
}