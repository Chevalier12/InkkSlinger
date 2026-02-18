using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Runtime.CompilerServices;

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
    private const float PointerAutoScrollStep = 16f;
    private const double MultiClickWindowMs = 450d;

    public RichTextBox()
    {
        SetValue(DocumentProperty, CreateDefaultDocument());
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
        if (!IsEnabled || !IsFocused || IsReadOnly)
        {
            return false;
        }

        if (char.IsControl(character))
        {
            return false;
        }

        ReplaceSelection(character.ToString(), "InsertText", GroupingPolicy.TypingBurst);
        return true;
    }

    public bool HandleKeyDownFromInput(Keys key, ModifierKeys modifiers)
    {
        if (!IsEnabled || !IsFocused)
        {
            return false;
        }

        _activeKeyModifiers = modifiers;
        try
        {
            var ctrl = (modifiers & ModifierKeys.Control) != 0;
            if (key == Keys.Enter && (ctrl || IsReadOnly) && TryActivateHyperlinkAtSelection())
            {
                return true;
            }

            if (ctrl && key == Keys.Z)
            {
                if (_undoManager.Undo())
                {
                    ClampSelectionToTextLength();
                    InvalidateVisual();
                }

                return true;
            }

            if (ctrl && key == Keys.Y)
            {
                if (_undoManager.Redo())
                {
                    ClampSelectionToTextLength();
                    InvalidateVisual();
                }

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

    public bool HandlePointerDownFromInput(Vector2 pointerPosition, bool extendSelection)
    {
        if (!IsEnabled)
        {
            return false;
        }

        UpdatePointerClickCount(pointerPosition);
        var index = GetTextIndexFromPoint(pointerPosition);
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
        InvalidateVisual();
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
        _pointerSelectionMoved = true;
        _caretIndex = index;
        EnsureCaretVisible();
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
        if (!_pointerSelectionMoved &&
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
            InvalidateVisual();
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
        _hasPendingRenderDirtyBoundsHint = false;
        base.InvalidateVisual();
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

        CommandBindings.Add(new CommandBinding(EditingCommands.ToggleBold, (_, _) => ExecuteToggleBold(), (_, args) => args.CanExecute = CanApplyInlineFormat()));
        CommandBindings.Add(new CommandBinding(EditingCommands.ToggleItalic, (_, _) => ExecuteToggleItalic(), (_, args) => args.CanExecute = CanApplyInlineFormat()));
        CommandBindings.Add(new CommandBinding(EditingCommands.ToggleUnderline, (_, _) => ExecuteToggleUnderline(), (_, args) => args.CanExecute = CanApplyInlineFormat()));

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

        _ = InputGestureService.Execute(key, modifiers, this, this);
        return true;
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

        if (SelectionLength > 0)
        {
            return true;
        }

        var text = GetText();
        return _caretIndex > 0 && _caretIndex <= text.Length;
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
        if (TryHandleTableBoundaryDeletion(backspace: true))
        {
            return;
        }

        if (SelectionLength > 0)
        {
            ReplaceSelection(string.Empty, "DeleteSelection", GroupingPolicy.StructuralAtomic);
            return;
        }

        if (_caretIndex <= 0)
        {
            return;
        }

        _selectionAnchor = _caretIndex - 1;
        ReplaceSelection(string.Empty, "Backspace", GroupingPolicy.DeletionBurst);
    }

    private void ExecuteDelete()
    {
        if (TryHandleTableBoundaryDeletion(backspace: false))
        {
            return;
        }

        if (SelectionLength > 0)
        {
            ReplaceSelection(string.Empty, "DeleteSelection", GroupingPolicy.StructuralAtomic);
            return;
        }

        var text = GetText();
        if (_caretIndex >= text.Length)
        {
            return;
        }

        _selectionAnchor = _caretIndex + 1;
        ReplaceSelection(string.Empty, "DeleteForward", GroupingPolicy.DeletionBurst);
    }

    private void ExecuteEnterParagraphBreak()
    {
        ReplaceSelection("\n", "EnterParagraphBreak", GroupingPolicy.StructuralAtomic);
    }

    private void ExecuteEnterLineBreak()
    {
        ReplaceSelection("\n", "EnterLineBreak", GroupingPolicy.StructuralAtomic);
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

        if (SelectionLength > 0)
        {
            ReplaceSelection(string.Empty, "TabBackward", GroupingPolicy.StructuralAtomic);
            return;
        }

        var text = GetText();
        if (_caretIndex <= 0 || _caretIndex > text.Length)
        {
            return;
        }

        _selectionAnchor = _caretIndex - 1;
        ReplaceSelection(string.Empty, "TabBackward", GroupingPolicy.StructuralAtomic);
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
        TextClipboard.SetData(FlowDocumentSerializer.ClipboardFormat, richSlice);
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
        TextClipboard.SetData(
            FlowDocumentSerializer.ClipboardFormat,
            FlowDocumentSerializer.SerializeRange(Document, SelectionStart, SelectionStart + SelectionLength));
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
        if (TextClipboard.TryGetData<string>(FlowDocumentSerializer.ClipboardFormat, out var richPayload) &&
            !string.IsNullOrWhiteSpace(richPayload))
        {
            usedRichPayload = true;
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
                    return;
                }

                fallbackToText = true;
            }
            catch (Exception)
            {
                RecordClipboardDeserializeSample(Stopwatch.GetElapsedTime(deserializeStart).TotalMilliseconds);
                fallbackToText = true;
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
        ApplyInlineFormatToSelection(static () => new Bold(), "ToggleBold");
    }

    private void ExecuteToggleItalic()
    {
        ApplyInlineFormatToSelection(static () => new Italic(), "ToggleItalic");
    }

    private void ExecuteToggleUnderline()
    {
        ApplyInlineFormatToSelection(static () => new Underline(), "ToggleUnderline");
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

    private void ApplyInlineFormatToSelection(Func<Span> spanFactory, string commandType)
    {
        if (!CanApplyInlineFormat())
        {
            return;
        }

        var editStart = Stopwatch.GetTimestamp();
        var start = SelectionStart;
        var length = SelectionLength;
        var text = GetText();
        var before = text[..start];
        var selected = text.Substring(start, length);
        var after = text[(start + length)..];

        var beforeDoc = DocumentEditing.CloneDocument(Document);
        var formatted = new FlowDocument();
        var paragraph = new Paragraph();
        if (before.Length > 0)
        {
            paragraph.Inlines.Add(new Run(before));
        }

        var span = spanFactory();
        span.Inlines.Add(new Run(selected));
        paragraph.Inlines.Add(span);
        if (after.Length > 0)
        {
            paragraph.Inlines.Add(new Run(after));
        }

        formatted.Blocks.Add(paragraph);
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
        session.ApplyOperation(new ApplyInlineFormatOperation("Document", selected, selected, beforeDoc, formatted));
        session.CommitTransaction();
        _selectionAnchor = start;
        _caretIndex = start + length;
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        RecordEditSample(elapsedMs);
        RichTextBoxDiagnostics.ObserveEdit(commandType, elapsedMs, start, length, _caretIndex);
        EnsureCaretVisible();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void ReplaceSelection(string replacement, string commandType, GroupingPolicy policy)
    {
        var editStart = Stopwatch.GetTimestamp();
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
        EnsureCaretVisible();
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

    private int GetTextIndexFromPoint(Vector2 point)
    {
        var textRect = GetTextRect();
        var layout = BuildOrGetLayout(textRect.Width);
        ClampScrollOffsets(layout, textRect);
        return layout.HitTestOffset(new Vector2(
            (point.X - textRect.X) + _horizontalOffset,
            (point.Y - textRect.Y) + _verticalOffset));
    }

    private void DrawCaret(SpriteBatch spriteBatch, LayoutRect textRect, DocumentLayoutResult layout)
    {
        if (!layout.TryGetCaretPosition(_caretIndex, out var caret))
        {
            return;
        }

        var lineHeight = Math.Max(1f, FontStashTextRenderer.GetLineHeight(Font));
        UiDrawing.DrawFilledRect(
            spriteBatch,
            new LayoutRect(textRect.X + caret.X - _horizontalOffset, textRect.Y + caret.Y - _verticalOffset, 1f, lineHeight),
            CaretBrush * Opacity);
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

        RecordSelectionGeometrySample(Stopwatch.GetElapsedTime(selectionStartTicks).TotalMilliseconds);
    }

    private static string NormalizeNewlines(string? text)
    {
        return (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private bool CanPasteFromClipboard()
    {
        if (TextClipboard.TryGetData<string>(FlowDocumentSerializer.ClipboardFormat, out var richPayload) &&
            !string.IsNullOrWhiteSpace(richPayload))
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

        var editStart = Stopwatch.GetTimestamp();
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
            "PasteRichFragment",
            GroupingPolicy.StructuralAtomic,
            new DocumentEditContext(
                _caretIndex,
                caretAfter,
                selectionStart,
                selectionLength,
                caretAfter,
                0,
                "PasteRichFragment"));
        session.ApplyOperation(new ReplaceDocumentOperation("Document", beforeText, afterText, beforeDocument, afterDocument));
        session.CommitTransaction();

        _caretIndex = caretAfter;
        _selectionAnchor = _caretIndex;
        var elapsedMs = Stopwatch.GetElapsedTime(editStart).TotalMilliseconds;
        RecordEditSample(elapsedMs);
        RichTextBoxDiagnostics.ObserveEdit("PasteRichFragment", elapsedMs, selectionStart, selectionLength, _caretIndex);
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateMeasure();
        InvalidateVisual();
        return true;
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

        var result = new FlowDocument();
        AppendDocumentBlocks(result, prefix, mergeParagraphBoundary: false);
        AppendDocumentBlocks(
            result,
            fragmentClone,
            mergeParagraphBoundary: !IsAtParagraphStart(current, clampedStart));
        AppendDocumentBlocks(
            result,
            suffix,
            mergeParagraphBoundary: !IsAtParagraphStart(current, clampedEnd));

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
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateMeasure();
        InvalidateVisual();
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
        if (style.IsHyperlink)
        {
            return new Color(117, 181, 255);
        }

        if (style.IsItalic)
        {
            return new Color(
                (byte)Math.Clamp(Foreground.R + 12, 0, 255),
                (byte)Math.Clamp(Foreground.G + 12, 0, 255),
                (byte)Math.Clamp(Foreground.B + 12, 0, 255),
                Foreground.A);
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
            else
            {
                while (i > 0 && !char.IsWhiteSpace(text[i - 1]))
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
        else
        {
            while (j < length && !char.IsWhiteSpace(text[j]))
            {
                j++;
            }
        }

        return j;
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
            InvalidateVisual();
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
        var text = GetText();
        if (text.Length == 0)
        {
            _selectionAnchor = 0;
            _caretIndex = 0;
            return;
        }

        var clamped = Math.Clamp(index, 0, text.Length);
        var start = clamped;
        while (start > 0 && text[start - 1] != '\n')
        {
            start--;
        }

        var end = clamped;
        while (end < text.Length && text[end] != '\n')
        {
            end++;
        }

        _selectionAnchor = start;
        _caretIndex = end;
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
