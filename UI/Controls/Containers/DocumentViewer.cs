using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public class DocumentViewer : Control, ITextInputControl, IRenderDirtyBoundsHintProvider, IHyperlinkHoverHost, IUiRootUpdateParticipant
{
    public static readonly RoutedEvent DocumentChangedEvent = new(nameof(DocumentChanged), RoutingStrategy.Bubble);
    public static readonly RoutedEvent HyperlinkNavigateEvent = new(nameof(HyperlinkNavigate), RoutingStrategy.Bubble);
    public static readonly RoutedEvent PageCountChangedEvent = new(nameof(PageCountChanged), RoutingStrategy.Bubble);
    public static readonly RoutedEvent PageViewChangedEvent = new(nameof(PageViewChanged), RoutingStrategy.Bubble);

    public static readonly DependencyProperty DocumentProperty =
        DependencyProperty.Register(
            nameof(Document),
            typeof(FlowDocument),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is DocumentViewer viewer)
                    {
                        viewer.OnDocumentPropertyChanged(args.OldValue as FlowDocument, args.NewValue as FlowDocument);
                    }
                },
                coerceValueCallback: static (_, value) => value as FlowDocument ?? CreateDefaultDocument()));

    public static readonly DependencyProperty ZoomProperty =
        DependencyProperty.Register(
            nameof(Zoom),
            typeof(float),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(
                100f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is DocumentViewer viewer)
                    {
                        viewer.OnZoomChanged();
                    }
                }));

    public static readonly DependencyProperty MinZoomProperty =
        DependencyProperty.Register(
            nameof(MinZoom),
            typeof(float),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(
                20f,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is DocumentViewer viewer)
                    {
                        viewer.OnZoomBoundsChanged();
                    }
                }));

    public static readonly DependencyProperty MaxZoomProperty =
        DependencyProperty.Register(
            nameof(MaxZoom),
            typeof(float),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(
                500f,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is DocumentViewer viewer)
                    {
                        viewer.OnZoomBoundsChanged();
                    }
                }));

    public static readonly DependencyProperty ZoomIncrementProperty =
        DependencyProperty.Register(
            nameof(ZoomIncrement),
            typeof(float),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(10f));

    public static readonly DependencyProperty HorizontalOffsetProperty =
        DependencyProperty.Register(
            nameof(HorizontalOffset),
            typeof(float),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty VerticalOffsetProperty =
        DependencyProperty.Register(
            nameof(VerticalOffset),
            typeof(float),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ExtentWidthProperty =
        DependencyProperty.Register(
            nameof(ExtentWidth),
            typeof(float),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ExtentHeightProperty =
        DependencyProperty.Register(
            nameof(ExtentHeight),
            typeof(float),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ViewportWidthProperty =
        DependencyProperty.Register(
            nameof(ViewportWidth),
            typeof(float),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ViewportHeightProperty =
        DependencyProperty.Register(
            nameof(ViewportHeight),
            typeof(float),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PageCountProperty =
        DependencyProperty.Register(
            nameof(PageCount),
            typeof(int),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MasterPageNumberProperty =
        DependencyProperty.Register(
            nameof(MasterPageNumber),
            typeof(int),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CanIncreaseZoomProperty =
        DependencyProperty.Register(
            nameof(CanIncreaseZoom),
            typeof(bool),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty CanDecreaseZoomProperty =
        DependencyProperty.Register(
            nameof(CanDecreaseZoom),
            typeof(bool),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty CanGoToNextPageProperty =
        DependencyProperty.Register(
            nameof(CanGoToNextPage),
            typeof(bool),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty CanGoToPreviousPageProperty =
        DependencyProperty.Register(
            nameof(CanGoToPreviousPage),
            typeof(bool),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(false));

    public new static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(new Color(28, 28, 28), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(new Color(162, 162, 162), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(1f, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(new Thickness(8f, 5f, 8f, 5f), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TextWrappingProperty =
        DependencyProperty.Register(
            nameof(TextWrapping),
            typeof(TextWrapping),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(TextWrapping.Wrap, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectionBrushProperty =
        DependencyProperty.Register(
            nameof(SelectionBrush),
            typeof(Color),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(new Color(66, 124, 211, 180), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CaretBrushProperty =
        DependencyProperty.Register(
            nameof(CaretBrush),
            typeof(Color),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty IsMouseOverProperty =
        DependencyProperty.Register(
            nameof(IsMouseOver),
            typeof(bool),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty IsFocusedProperty =
        DependencyProperty.Register(
            nameof(IsFocused),
            typeof(bool),
            typeof(DocumentViewer),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly DocumentLayoutEngine _layoutEngine = new();
    private readonly DocumentViewportLayoutCache _layoutCache = new();
    private readonly DocumentViewerInteractionState _interactionState = new();
    private DocumentLayoutResult? _lastMeasuredLayout;
    private DocumentLayoutResult? _lastRenderedLayout;
    private DocumentPageMap _pageMap = DocumentPageMap.Empty;
    private int _caretIndex;
    private int _selectionAnchor;
    private float _horizontalOffset;
    private float _verticalOffset;
    private float _caretBlinkSeconds;
    private bool _isCaretVisible = true;
    private bool _isSelectingWithPointer;
    private bool _pointerSelectionMoved;
    private Hyperlink? _pendingPointerHyperlink;
    private Hyperlink? _hoveredHyperlink;
    private readonly Dictionary<Hyperlink, Style?> _appliedImplicitHyperlinkStyles = new();
    private bool _hasPendingRenderDirtyBoundsHint;
    private LayoutRect _pendingRenderDirtyBoundsHint;

    public DocumentViewer()
    {
        SetValue(DocumentProperty, CreateDefaultDocument());
        RegisterCommandBindings();
        RegisterInputBindings();
        RefreshPageMetrics();
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

    public event EventHandler<RoutedSimpleEventArgs> PageCountChanged
    {
        add => AddHandler(PageCountChangedEvent, value);
        remove => RemoveHandler(PageCountChangedEvent, value);
    }

    public event EventHandler<RoutedSimpleEventArgs> PageViewChanged
    {
        add => AddHandler(PageViewChangedEvent, value);
        remove => RemoveHandler(PageViewChangedEvent, value);
    }

    public FlowDocument Document
    {
        get => GetValue<FlowDocument>(DocumentProperty) ?? CreateDefaultDocument();
        set => SetValue(DocumentProperty, value);
    }

    public float Zoom
    {
        get => GetValue<float>(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public float MinZoom
    {
        get => GetValue<float>(MinZoomProperty);
        set => SetValue(MinZoomProperty, value);
    }

    public float MaxZoom
    {
        get => GetValue<float>(MaxZoomProperty);
        set => SetValue(MaxZoomProperty, value);
    }

    public float ZoomIncrement
    {
        get => GetValue<float>(ZoomIncrementProperty);
        set => SetValue(ZoomIncrementProperty, value);
    }

    public float HorizontalOffset
    {
        get => GetValue<float>(HorizontalOffsetProperty);
        private set => SetValue(HorizontalOffsetProperty, value);
    }

    public float VerticalOffset
    {
        get => GetValue<float>(VerticalOffsetProperty);
        private set => SetValue(VerticalOffsetProperty, value);
    }

    public float ExtentWidth
    {
        get => GetValue<float>(ExtentWidthProperty);
        private set => SetValue(ExtentWidthProperty, value);
    }

    public float ExtentHeight
    {
        get => GetValue<float>(ExtentHeightProperty);
        private set => SetValue(ExtentHeightProperty, value);
    }

    public float ViewportWidth
    {
        get => GetValue<float>(ViewportWidthProperty);
        private set => SetValue(ViewportWidthProperty, value);
    }

    public float ViewportHeight
    {
        get => GetValue<float>(ViewportHeightProperty);
        private set => SetValue(ViewportHeightProperty, value);
    }

    public int PageCount
    {
        get => GetValue<int>(PageCountProperty);
        private set => SetValue(PageCountProperty, value);
    }

    public int MasterPageNumber
    {
        get => GetValue<int>(MasterPageNumberProperty);
        private set => SetValue(MasterPageNumberProperty, value);
    }

    public bool CanIncreaseZoom
    {
        get => GetValue<bool>(CanIncreaseZoomProperty);
        private set => SetValue(CanIncreaseZoomProperty, value);
    }

    public bool CanDecreaseZoom
    {
        get => GetValue<bool>(CanDecreaseZoomProperty);
        private set => SetValue(CanDecreaseZoomProperty, value);
    }

    public bool CanGoToNextPage
    {
        get => GetValue<bool>(CanGoToNextPageProperty);
        private set => SetValue(CanGoToNextPageProperty, value);
    }

    public bool CanGoToPreviousPage
    {
        get => GetValue<bool>(CanGoToPreviousPageProperty);
        private set => SetValue(CanGoToPreviousPageProperty, value);
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
    public int SelectionStart => Math.Min(_selectionAnchor, _caretIndex);
    public int SelectionLength => Math.Abs(_caretIndex - _selectionAnchor);

    private float ZoomScale
    {
        get
        {
            var (effectiveMinZoom, effectiveMaxZoom) = GetEffectiveZoomBounds();
            return Math.Clamp(Zoom, effectiveMinZoom, effectiveMaxZoom) / 100f;
        }
    }

    public void NextPage()
    {
        if (_pageMap.TryGetPage(MasterPageNumber + 1, out var page))
        {
            ScrollToVerticalOffset(page.Top * ZoomScale);
        }
    }

    public void PreviousPage()
    {
        if (_pageMap.TryGetPage(MasterPageNumber - 1, out var page))
        {
            ScrollToVerticalOffset(page.Top * ZoomScale);
        }
    }

    public void FirstPage()
    {
        if (_pageMap.TryGetPage(1, out var page))
        {
            ScrollToVerticalOffset(page.Top * ZoomScale);
        }
    }

    public void LastPage()
    {
        if (_pageMap.TryGetPage(_pageMap.PageCount, out var page))
        {
            ScrollToVerticalOffset(page.Top * ZoomScale);
        }
    }

    public void GoToPage(int pageNumber)
    {
        if (_pageMap.TryGetPage(pageNumber, out var page))
        {
            ScrollToVerticalOffset(page.Top * ZoomScale);
        }
    }

    public void IncreaseZoom()
    {
        Zoom = Math.Min(MaxZoom, Zoom + Math.Max(1f, ZoomIncrement));
    }

    public void DecreaseZoom()
    {
        Zoom = Math.Max(MinZoom, Zoom - Math.Max(1f, ZoomIncrement));
    }

    public void FitToWidth()
    {
        var textRect = GetTextRect();
        if (textRect.Width <= 0f)
        {
            return;
        }

        var layout = BuildOrGetLayout(float.PositiveInfinity);
        if (layout.ContentWidth <= 0f)
        {
            return;
        }

        var fitted = (textRect.Width / layout.ContentWidth) * 100f;
        Zoom = Math.Clamp(fitted, MinZoom, MaxZoom);
    }

    public void SelectAll()
    {
        var length = GetText().Length;
        _selectionAnchor = 0;
        _caretIndex = length;
        EnsureCaretVisible();
        InvalidateVisual();
    }

    public void Copy()
    {
        if (SelectionLength <= 0)
        {
            return;
        }

        var text = GetText();
        var start = SelectionStart;
        var length = SelectionLength;
        TextClipboard.SetText(text.Substring(start, length));

        var fragmentXml = FlowDocumentSerializer.SerializeRange(Document, start, start + length);
        TextClipboard.SetData(FlowDocumentSerializer.ClipboardFormat, fragmentXml);
        TextClipboard.SetData("Xaml", fragmentXml);
    }

    public void ScrollToVerticalOffset(float offset)
    {
        SetOffsets(_horizontalOffset, offset);
    }

    public void ScrollToHorizontalOffset(float offset)
    {
        SetOffsets(offset, _verticalOffset);
    }

    public bool HandleTextInputFromInput(char character)
    {
        _ = character;
        return false;
    }

    public bool HandleKeyDownFromInput(Keys key, ModifierKeys modifiers)
    {
        if (!IsEnabled || !IsFocused)
        {
            return false;
        }

        var shift = (modifiers & ModifierKeys.Shift) != 0;
        var ctrl = (modifiers & ModifierKeys.Control) != 0;

        switch (key)
        {
            case Keys.Left:
                MoveCaretByCharacter(-1, shift, ctrl);
                return true;
            case Keys.Right:
                MoveCaretByCharacter(1, shift, ctrl);
                return true;
            case Keys.Up:
                MoveCaretByLine(moveUp: true, shift);
                return true;
            case Keys.Down:
                MoveCaretByLine(moveUp: false, shift);
                return true;
            case Keys.Home:
                if (ctrl)
                {
                    SetCaret(0, shift);
                }
                else
                {
                    MoveCaretToLineBoundary(moveToLineStart: true, shift);
                }

                return true;
            case Keys.End:
                if (ctrl)
                {
                    SetCaret(GetText().Length, shift);
                }
                else
                {
                    MoveCaretToLineBoundary(moveToLineStart: false, shift);
                }

                return true;
            case Keys.PageUp:
                if (shift)
                {
                    ExpandSelectionByPage(up: true);
                }
                else
                {
                    PreviousPage();
                    CollapseSelectionAtCaret();
                }

                return true;
            case Keys.PageDown:
                if (shift)
                {
                    ExpandSelectionByPage(up: false);
                }
                else
                {
                    NextPage();
                    CollapseSelectionAtCaret();
                }

                return true;
            case Keys.A when ctrl:
                SelectAll();
                return true;
            case Keys.C when ctrl:
                Copy();
                return true;
            case Keys.Enter when ctrl:
                return TryActivateHyperlinkAtSelection();
        }

        return InputGestureService.Execute(key, modifiers, this, this);
    }

    public bool HandlePointerDownFromInput(Vector2 pointerPosition, bool extendSelection)
    {
        if (!IsEnabled)
        {
            return false;
        }

        var textRect = GetTextRect();
        var layout = BuildOrGetLayout(textRect.Width / ZoomScale);
        var offset = DocumentViewportController.HitTestDocumentOffset(layout, pointerPosition, textRect, _horizontalOffset, _verticalOffset, ZoomScale);
        _pendingPointerHyperlink = DocumentViewportController.ResolveHyperlinkAtOffset(Document, offset);

        var clickCount = _interactionState.RegisterPointerDown(offset);
        if (clickCount == 3)
        {
            SelectParagraphAt(offset);
        }
        else if (clickCount == 2)
        {
            SelectWordAt(offset);
        }
        else
        {
            SetCaret(offset, extendSelection);
        }

        _pointerSelectionMoved = false;
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

        var textRect = GetTextRect();
        var layout = BuildOrGetLayout(textRect.Width / ZoomScale);
        var offset = DocumentViewportController.HitTestDocumentOffset(layout, pointerPosition, textRect, _horizontalOffset, _verticalOffset, ZoomScale);
        _caretIndex = offset;
        _pointerSelectionMoved = true;
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
        if (!_pointerSelectionMoved && SelectionLength == 0 && _pendingPointerHyperlink != null)
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

        var before = _verticalOffset;
        _verticalOffset -= MathF.Sign(delta) * DocumentViewerInteractionState.ResolveLineScrollAmount(this);
        ClampOffsetsForCurrentLayout();
        if (MathF.Abs(before - _verticalOffset) <= 0.01f)
        {
            return false;
        }

        SetOffsets(_horizontalOffset, _verticalOffset);
        return true;
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

        var layout = BuildOrGetLayout(textRect.Width / ZoomScale);
        var offset = DocumentViewportController.HitTestDocumentOffset(layout, pointerPosition, textRect, _horizontalOffset, _verticalOffset, ZoomScale);
        SetHoveredHyperlink(DocumentViewportController.ResolveHyperlinkAtOffset(Document, offset));
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

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        UpdateDocumentViewerState(gameTime);
    }

    bool IUiRootUpdateParticipant.IsFrameUpdateActive => IsFocused;

    void IUiRootUpdateParticipant.UpdateFromUiRoot(GameTime gameTime)
    {
        RecordUpdateCallFromUiRoot();
        UpdateDocumentViewerState(gameTime);
    }

    private void UpdateDocumentViewerState(GameTime gameTime)
    {
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

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var textRect = GetTextRectForMeasure(availableSize);
        var availableDocumentWidth = textRect.Width <= 0f ? float.PositiveInfinity : textRect.Width / ZoomScale;
        var layout = BuildOrGetLayout(availableDocumentWidth);
        _lastMeasuredLayout = layout;

        UpdateViewportAndExtent(layout, textRect.Width, textRect.Height);

        return new Vector2(
            (layout.ContentWidth * ZoomScale) + Padding.Horizontal + (BorderThickness * 2f),
            (layout.ContentHeight * ZoomScale) + Padding.Vertical + (BorderThickness * 2f));
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        UiDrawing.DrawFilledRect(spriteBatch, LayoutSlot, Background * Opacity);
        if (BorderThickness > 0f)
        {
            UiDrawing.DrawRectStroke(spriteBatch, LayoutSlot, BorderThickness, BorderBrush * Opacity);
        }

        var textRect = GetTextRect();
        var layout = BuildOrGetLayout(textRect.Width / ZoomScale);
        UpdateViewportAndExtent(layout, textRect.Width, textRect.Height);
        ClampOffsetsForCurrentLayout(layout, textRect);

        DrawSelection(spriteBatch, textRect, layout);

        for (var i = 0; i < layout.Runs.Count; i++)
        {
            var run = layout.Runs[i];
            if (string.IsNullOrEmpty(run.Text))
            {
                continue;
            }

            var localX = (run.Bounds.X * ZoomScale) - _horizontalOffset;
            var localY = (run.Bounds.Y * ZoomScale) - _verticalOffset;
            var position = new Vector2(textRect.X + localX, textRect.Y + localY);
            var runHeight = run.Bounds.Height * ZoomScale;
            if (position.Y + runHeight < textRect.Y || position.Y > textRect.Y + textRect.Height)
            {
                continue;
            }

            var color = ResolveRunColor(run.Style);
            DrawRunString(spriteBatch, run.Text, position, color * Opacity, run.Style);

            if (run.Style.IsUnderline)
            {
                var underlineY = position.Y + runHeight - 1f;
                UiDrawing.DrawFilledRect(
                    spriteBatch,
                    new LayoutRect(position.X, underlineY, Math.Max(1f, run.Bounds.Width * ZoomScale), 1f),
                    color * Opacity);
            }
        }

        DrawTableBorders(spriteBatch, textRect, layout);
        DrawPageSeparators(spriteBatch, textRect);

        if (IsFocused && _isCaretVisible)
        {
            DrawCaret(spriteBatch, textRect, layout);
        }

        CaptureDirtyHint(layout, textRect);
        _lastRenderedLayout = layout;
    }

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        clipRect = LayoutSlot;
        return true;
    }

    protected override void OnResourceScopeChanged(object? sender, ResourceDictionaryChangedEventArgs e)
    {
        base.OnResourceScopeChanged(sender, e);
        ApplyHyperlinkImplicitStyles();
    }

    private static FlowDocument CreateDefaultDocument()
    {
        var document = new FlowDocument();
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run(string.Empty));
        document.Blocks.Add(paragraph);
        return document;
    }

    private void RegisterCommandBindings()
    {
        CommandBindings.Add(new CommandBinding(NavigationCommands.NextPage, (_, _) => NextPage(), (_, args) => args.CanExecute = CanGoToNextPage));
        CommandBindings.Add(new CommandBinding(NavigationCommands.PreviousPage, (_, _) => PreviousPage(), (_, args) => args.CanExecute = CanGoToPreviousPage));
        CommandBindings.Add(new CommandBinding(NavigationCommands.FirstPage, (_, _) => FirstPage(), (_, args) => args.CanExecute = PageCount > 0 && MasterPageNumber > 1));
        CommandBindings.Add(new CommandBinding(NavigationCommands.LastPage, (_, _) => LastPage(), (_, args) => args.CanExecute = PageCount > 0 && MasterPageNumber < PageCount));
        CommandBindings.Add(
            new CommandBinding(
                NavigationCommands.GoToPage,
                (_, args) =>
                {
                    if (TryConvertToInt(args.Parameter, out var pageNumber))
                    {
                        GoToPage(pageNumber);
                    }
                },
                (_, args) => args.CanExecute = TryConvertToInt(args.Parameter, out var pageNumber) && pageNumber >= 1 && pageNumber <= PageCount));

        CommandBindings.Add(new CommandBinding(NavigationCommands.IncreaseZoom, (_, _) => IncreaseZoom(), (_, args) => args.CanExecute = CanIncreaseZoom));
        CommandBindings.Add(new CommandBinding(NavigationCommands.DecreaseZoom, (_, _) => DecreaseZoom(), (_, args) => args.CanExecute = CanDecreaseZoom));
        CommandBindings.Add(new CommandBinding(NavigationCommands.FitToWidth, (_, _) => FitToWidth(), (_, args) => args.CanExecute = true));

        CommandBindings.Add(new CommandBinding(EditingCommands.Copy, (_, _) => Copy(), (_, args) => args.CanExecute = SelectionLength > 0));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectAll, (_, _) => SelectAll(), (_, args) => args.CanExecute = true));

        CommandBindings.Add(new CommandBinding(EditingCommands.Cut, (_, _) => { }, (_, args) => args.CanExecute = false));
        CommandBindings.Add(new CommandBinding(EditingCommands.Paste, (_, _) => { }, (_, args) => args.CanExecute = false));
        CommandBindings.Add(new CommandBinding(EditingCommands.ToggleBold, (_, _) => { }, (_, args) => args.CanExecute = false));
        CommandBindings.Add(new CommandBinding(EditingCommands.ToggleItalic, (_, _) => { }, (_, args) => args.CanExecute = false));
        CommandBindings.Add(new CommandBinding(EditingCommands.ToggleUnderline, (_, _) => { }, (_, args) => args.CanExecute = false));
        CommandBindings.Add(new CommandBinding(EditingCommands.IncreaseListLevel, (_, _) => { }, (_, args) => args.CanExecute = false));
        CommandBindings.Add(new CommandBinding(EditingCommands.DecreaseListLevel, (_, _) => { }, (_, args) => args.CanExecute = false));
        CommandBindings.Add(new CommandBinding(EditingCommands.InsertTable, (_, _) => { }, (_, args) => args.CanExecute = false));
        CommandBindings.Add(new CommandBinding(EditingCommands.SplitCell, (_, _) => { }, (_, args) => args.CanExecute = false));
        CommandBindings.Add(new CommandBinding(EditingCommands.MergeCells, (_, _) => { }, (_, args) => args.CanExecute = false));
    }

    private void RegisterInputBindings()
    {
        AddKeyBinding(Keys.C, ModifierKeys.Control, EditingCommands.Copy);
        AddKeyBinding(Keys.A, ModifierKeys.Control, EditingCommands.SelectAll);
        AddKeyBinding(Keys.PageDown, ModifierKeys.None, NavigationCommands.NextPage);
        AddKeyBinding(Keys.PageUp, ModifierKeys.None, NavigationCommands.PreviousPage);
    }

    private void AddKeyBinding(Keys key, ModifierKeys modifiers, RoutedCommand command)
    {
        InputBindings.Add(new KeyBinding
        {
            Key = key,
            Modifiers = modifiers,
            Command = command
        });
    }

    private void OnDocumentPropertyChanged(FlowDocument? oldDocument, FlowDocument? newDocument)
    {
        SetHoveredHyperlink(null);
        if (oldDocument != null)
        {
            oldDocument.Changed -= OnDocumentChanged;
        }

        if (newDocument != null)
        {
            newDocument.Changed += OnDocumentChanged;
        }

        ClampSelectionToTextLength();
        ApplyHyperlinkImplicitStyles();
        _layoutCache.Invalidate();
        _lastMeasuredLayout = null;
        InvalidateMeasure();
        InvalidateVisual();
        RaiseRoutedEventInternal(DocumentChangedEvent, new RoutedSimpleEventArgs(DocumentChangedEvent));
        RefreshPageMetrics();
    }

    private void OnDocumentChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        ClampSelectionToTextLength();
        ApplyHyperlinkImplicitStyles();
        _layoutCache.Invalidate();
        InvalidateMeasure();
        InvalidateVisual();
        RaiseRoutedEventInternal(DocumentChangedEvent, new RoutedSimpleEventArgs(DocumentChangedEvent));
        RefreshPageMetrics();
    }

    private void OnZoomChanged()
    {
        var (effectiveMinZoom, effectiveMaxZoom) = GetEffectiveZoomBounds();
        var coerced = Math.Clamp(Zoom, effectiveMinZoom, effectiveMaxZoom);
        if (Math.Abs(coerced - Zoom) > 0.001f)
        {
            Zoom = coerced;
            return;
        }

        RefreshZoomCapabilities();
        ClampOffsetsForCurrentLayout();
        RefreshPageMetrics();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnZoomBoundsChanged()
    {
        var (effectiveMinZoom, effectiveMaxZoom) = GetEffectiveZoomBounds();
        var coerced = Math.Clamp(Zoom, effectiveMinZoom, effectiveMaxZoom);
        if (Math.Abs(coerced - Zoom) > 0.001f)
        {
            Zoom = coerced;
            return;
        }

        RefreshZoomCapabilities();
    }

    private DocumentLayoutResult BuildOrGetLayout(float availableWidth)
    {
        var normalizedWidth = TextWrapping == TextWrapping.NoWrap || availableWidth <= 0f
            ? float.PositiveInfinity
            : availableWidth;
        var text = GetText();
        var signature = HashCode.Combine(
            RuntimeHelpers.GetHashCode(Document),
            StringComparer.Ordinal.GetHashCode(text),
            UiTextRenderer.ResolveTypography(this, FontSize),
            (int)TextWrapping,
            (int)MathF.Round(normalizedWidth * 100f));
        var typography = UiTextRenderer.ResolveTypography(this, FontSize);
        var lineHeight = Math.Max(1f, UiTextRenderer.GetLineHeight(typography));
        var key = new DocumentViewportLayoutCache.CacheKey(
            signature,
            normalizedWidth,
            TextWrapping,
            typography.GetHashCode(),
            lineHeight,
            Foreground);

        if (_layoutCache.TryGet(key, out var cached))
        {
            return cached;
        }

        var settings = new DocumentLayoutSettings(
            AvailableWidth: normalizedWidth,
            Typography: typography,
            Wrapping: TextWrapping,
            Foreground: Foreground,
            LineHeight: lineHeight,
            ListIndent: lineHeight * 1.2f,
            ListMarkerGap: 4f,
            TableCellPadding: 4f,
            TableBorderThickness: 1f);
        var built = _layoutEngine.Layout(Document, settings);
        _layoutCache.Store(key, built);
        return built;
    }

    private void UpdateViewportAndExtent(DocumentLayoutResult layout, float viewportWidth, float viewportHeight)
    {
        var scaledExtentWidth = layout.ContentWidth * ZoomScale;
        var scaledExtentHeight = layout.ContentHeight * ZoomScale;
        SetIfChanged(ExtentWidthProperty, ExtentWidth, scaledExtentWidth);
        SetIfChanged(ExtentHeightProperty, ExtentHeight, scaledExtentHeight);
        SetIfChanged(ViewportWidthProperty, ViewportWidth, Math.Max(0f, viewportWidth));
        SetIfChanged(ViewportHeightProperty, ViewportHeight, Math.Max(0f, viewportHeight));

        ClampOffsetsForCurrentLayout(layout, new LayoutRect(0f, 0f, Math.Max(0f, viewportWidth), Math.Max(0f, viewportHeight)));
        RefreshPageMetrics(layout, viewportHeight);
    }

    private void RefreshPageMetrics(DocumentLayoutResult? layoutOverride = null, float viewportHeightOverride = float.NaN)
    {
        var textRect = GetTextRect();
        var layout = layoutOverride ?? BuildOrGetLayout(textRect.Width / ZoomScale);
        var viewportHeight = float.IsNaN(viewportHeightOverride) ? textRect.Height : viewportHeightOverride;
        _pageMap = DocumentViewportController.BuildPageMap(layout, viewportHeight, ZoomScale);

        var oldPageCount = PageCount;
        var oldPage = MasterPageNumber;
        var oldCanNext = CanGoToNextPage;
        var oldCanPrev = CanGoToPreviousPage;

        PageCount = _pageMap.PageCount;
        MasterPageNumber = _pageMap.ResolveCurrentPageNumber(_verticalOffset / ZoomScale);
        CanGoToNextPage = PageCount > 0 && MasterPageNumber < PageCount;
        CanGoToPreviousPage = PageCount > 0 && MasterPageNumber > 1;
        RefreshZoomCapabilities();

        if (oldPageCount != PageCount)
        {
            RaiseRoutedEventInternal(PageCountChangedEvent, new RoutedSimpleEventArgs(PageCountChangedEvent));
        }

        if (oldPage != MasterPageNumber || oldCanNext != CanGoToNextPage || oldCanPrev != CanGoToPreviousPage)
        {
            RaiseRoutedEventInternal(PageViewChangedEvent, new RoutedSimpleEventArgs(PageViewChangedEvent));
        }
    }

    private void ClampOffsetsForCurrentLayout()
    {
        var textRect = GetTextRect();
        var layout = BuildOrGetLayout(textRect.Width / ZoomScale);
        ClampOffsetsForCurrentLayout(layout, textRect);
    }

    private void ClampOffsetsForCurrentLayout(DocumentLayoutResult layout, LayoutRect textRect)
    {
        var h = _horizontalOffset / ZoomScale;
        var v = _verticalOffset / ZoomScale;
        DocumentViewportController.ClampOffsets(ref h, ref v, layout, textRect.Width / ZoomScale, textRect.Height / ZoomScale);
        _horizontalOffset = h * ZoomScale;
        _verticalOffset = v * ZoomScale;

        SetIfChanged(HorizontalOffsetProperty, HorizontalOffset, _horizontalOffset);
        SetIfChanged(VerticalOffsetProperty, VerticalOffset, _verticalOffset);
    }

    private void SetOffsets(float horizontal, float vertical)
    {
        _horizontalOffset = horizontal;
        _verticalOffset = vertical;
        ClampOffsetsForCurrentLayout();
        RefreshPageMetrics();
        InvalidateVisual();
    }

    private void RefreshZoomCapabilities()
    {
        var (effectiveMinZoom, effectiveMaxZoom) = GetEffectiveZoomBounds();
        CanIncreaseZoom = Zoom < effectiveMaxZoom - 0.001f;
        CanDecreaseZoom = Zoom > effectiveMinZoom + 0.001f;
    }

    private (float Min, float Max) GetEffectiveZoomBounds()
    {
        var effectiveMinZoom = Math.Max(1f, MinZoom);
        var effectiveMaxZoom = Math.Max(effectiveMinZoom, MaxZoom);
        return (effectiveMinZoom, effectiveMaxZoom);
    }

    private void MoveCaretByCharacter(int direction, bool extendSelection, bool byWord)
    {
        var text = GetText();
        var target = byWord
            ? direction < 0 ? FindPreviousWordBoundary(text, _caretIndex) : FindNextWordBoundary(text, _caretIndex)
            : Math.Clamp(_caretIndex + direction, 0, text.Length);
        SetCaret(target, extendSelection);
    }

    private static int FindPreviousWordBoundary(string text, int index)
    {
        var i = Math.Clamp(index, 0, text.Length);
        while (i > 0 && char.IsWhiteSpace(text[i - 1]))
        {
            i--;
        }

        while (i > 0 && !char.IsWhiteSpace(text[i - 1]))
        {
            i--;
        }

        return i;
    }

    private static int FindNextWordBoundary(string text, int index)
    {
        var i = Math.Clamp(index, 0, text.Length);
        while (i < text.Length && !char.IsWhiteSpace(text[i]))
        {
            i++;
        }

        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }

        return i;
    }

    private void MoveCaretByLine(bool moveUp, bool extendSelection)
    {
        var textRect = GetTextRect();
        var layout = BuildOrGetLayout(textRect.Width / ZoomScale);
        if (!layout.TryGetCaretPosition(_caretIndex, out var currentPos))
        {
            return;
        }

        var currentLineIndex = FindLineIndexForOffset(layout, _caretIndex);
        if (currentLineIndex < 0)
        {
            return;
        }

        var targetLineIndex = Math.Clamp(currentLineIndex + (moveUp ? -1 : 1), 0, layout.Lines.Count - 1);
        var targetLine = layout.Lines[targetLineIndex];
        var localX = currentPos.X - targetLine.TextStartX;
        var targetColumn = FindColumnFromX(targetLine, localX);
        SetCaret(targetLine.StartOffset + targetColumn, extendSelection);
    }

    private void MoveCaretToLineBoundary(bool moveToLineStart, bool extendSelection)
    {
        var textRect = GetTextRect();
        var layout = BuildOrGetLayout(textRect.Width / ZoomScale);
        var lineIndex = FindLineIndexForOffset(layout, _caretIndex);
        if (lineIndex < 0)
        {
            return;
        }

        var line = layout.Lines[lineIndex];
        SetCaret(moveToLineStart ? line.StartOffset : line.StartOffset + line.Length, extendSelection);
    }

    private void ExpandSelectionByPage(bool up)
    {
        var textRect = GetTextRect();
        var layout = BuildOrGetLayout(textRect.Width / ZoomScale);
        if (!layout.TryGetCaretPosition(_caretIndex, out var currentPos))
        {
            return;
        }

        var pageDelta = textRect.Height / ZoomScale;
        var probe = new Vector2(currentPos.X, Math.Max(0f, currentPos.Y + (up ? -pageDelta : pageDelta)));
        SetCaret(layout.HitTestOffset(probe), extendSelection: true);
    }

    private int FindLineIndexForOffset(DocumentLayoutResult layout, int offset)
    {
        for (var i = 0; i < layout.Lines.Count; i++)
        {
            var line = layout.Lines[i];
            var start = line.StartOffset;
            var end = line.StartOffset + line.Length;
            if (offset >= start && offset <= end)
            {
                return i;
            }
        }

        return layout.Lines.Count - 1;
    }

    private static int FindColumnFromX(DocumentLayoutLine line, float localX)
    {
        for (var i = 0; i < line.PrefixWidths.Length - 1; i++)
        {
            var left = line.PrefixWidths[i];
            var right = line.PrefixWidths[i + 1];
            var mid = left + ((right - left) * 0.5f);
            if (localX <= mid)
            {
                return i;
            }
        }

        return line.Length;
    }

    private void SetCaret(int index, bool extendSelection)
    {
        _caretIndex = Math.Clamp(index, 0, GetText().Length);
        if (!extendSelection)
        {
            _selectionAnchor = _caretIndex;
        }

        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        RefreshPageMetrics();
        InvalidateVisual();
    }

    private void CollapseSelectionAtCaret()
    {
        _selectionAnchor = _caretIndex;
    }

    private void SelectWordAt(int offset)
    {
        var (start, length) = DocumentViewerInteractionState.SelectWord(GetText(), offset);
        _selectionAnchor = start;
        _caretIndex = start + length;
        EnsureCaretVisible();
    }

    private void SelectParagraphAt(int offset)
    {
        var (start, length) = DocumentViewerInteractionState.SelectParagraph(GetText(), offset);
        _selectionAnchor = start;
        _caretIndex = start + length;
        EnsureCaretVisible();
    }

    private void EnsureCaretVisible()
    {
        var textRect = GetTextRect();
        if (textRect.Width <= 0f || textRect.Height <= 0f)
        {
            return;
        }

        var layout = BuildOrGetLayout(textRect.Width / ZoomScale);
        if (!layout.TryGetCaretPosition(_caretIndex, out var caretPos))
        {
            return;
        }

        var caretX = caretPos.X * ZoomScale;
        var caretY = caretPos.Y * ZoomScale;
        var lineHeight = UiTextRenderer.GetLineHeight(this, FontSize) * ZoomScale;

        if (caretX < _horizontalOffset)
        {
            _horizontalOffset = caretX;
        }
        else if (caretX > _horizontalOffset + textRect.Width - 6f)
        {
            _horizontalOffset = caretX - textRect.Width + 6f;
        }

        if (caretY < _verticalOffset)
        {
            _verticalOffset = caretY;
        }
        else if (caretY + lineHeight > _verticalOffset + textRect.Height)
        {
            _verticalOffset = (caretY + lineHeight) - textRect.Height;
        }

        ClampOffsetsForCurrentLayout(layout, textRect);
    }

    private void ClampSelectionToTextLength()
    {
        var length = GetText().Length;
        _caretIndex = Math.Clamp(_caretIndex, 0, length);
        _selectionAnchor = Math.Clamp(_selectionAnchor, 0, length);
    }

    private string GetText()
    {
        return DocumentEditing.GetText(Document);
    }

    private void DrawSelection(SpriteBatch spriteBatch, LayoutRect textRect, DocumentLayoutResult layout)
    {
        if (SelectionLength <= 0)
        {
            return;
        }

        var rects = layout.BuildSelectionRects(SelectionStart, SelectionLength);
        for (var i = 0; i < rects.Count; i++)
        {
            var rect = rects[i];
            var scaled = new LayoutRect(
                textRect.X + (rect.X * ZoomScale) - _horizontalOffset,
                textRect.Y + (rect.Y * ZoomScale) - _verticalOffset,
                Math.Max(1f, rect.Width * ZoomScale),
                Math.Max(1f, rect.Height * ZoomScale));
            UiDrawing.DrawFilledRect(spriteBatch, scaled, SelectionBrush * Opacity);
        }
    }

    private void DrawCaret(SpriteBatch spriteBatch, LayoutRect textRect, DocumentLayoutResult layout)
    {
        if (!layout.TryGetCaretPosition(_caretIndex, out var caretPosition))
        {
            return;
        }

        var x = textRect.X + (caretPosition.X * ZoomScale) - _horizontalOffset;
        var y = textRect.Y + (caretPosition.Y * ZoomScale) - _verticalOffset;
        var height = UiTextRenderer.GetLineHeight(this, FontSize) * ZoomScale;
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x, y, 1f, Math.Max(1f, height)), CaretBrush * Opacity);
    }

    private void DrawTableBorders(SpriteBatch spriteBatch, LayoutRect textRect, DocumentLayoutResult layout)
    {
        for (var i = 0; i < layout.TableCellBounds.Count; i++)
        {
            var cell = layout.TableCellBounds[i];
            var rect = new LayoutRect(
                textRect.X + (cell.X * ZoomScale) - _horizontalOffset,
                textRect.Y + (cell.Y * ZoomScale) - _verticalOffset,
                cell.Width * ZoomScale,
                cell.Height * ZoomScale);

            UiDrawing.DrawRectStroke(spriteBatch, rect, 1f, BorderBrush * Opacity);
        }
    }

    private void DrawPageSeparators(SpriteBatch spriteBatch, LayoutRect textRect)
    {
        if (_pageMap.PageCount <= 1)
        {
            return;
        }

        for (var i = 1; i < _pageMap.Pages.Count; i++)
        {
            var page = _pageMap.Pages[i];
            var y = textRect.Y + (page.Top * ZoomScale) - _verticalOffset;
            if (y < textRect.Y || y > textRect.Y + textRect.Height)
            {
                continue;
            }

            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(textRect.X, y, Math.Max(1f, textRect.Width), 1f),
                new Color(80, 80, 80) * Opacity);
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
            textRect.X + (local.X * ZoomScale) - _horizontalOffset,
            textRect.Y + (local.Y * ZoomScale) - _verticalOffset,
            local.Width * ZoomScale,
            local.Height * ZoomScale);
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

    private void DrawRunString(SpriteBatch spriteBatch, string text, Vector2 position, Color color, DocumentLayoutStyle style)
    {
        UiTextRenderer.DrawString(
            spriteBatch,
            UiTextRenderer.ResolveTypography(this, FontSize * ZoomScale, ToStyleOverride(style)),
            text,
            position,
            color,
            opaqueBackground: false);
    }

    private LayoutRect GetTextRect()
    {
        return new LayoutRect(
            LayoutSlot.X + BorderThickness + Padding.Left,
            LayoutSlot.Y + BorderThickness + Padding.Top,
            Math.Max(0f, LayoutSlot.Width - (BorderThickness * 2f) - Padding.Horizontal),
            Math.Max(0f, LayoutSlot.Height - (BorderThickness * 2f) - Padding.Vertical));
    }

    private LayoutRect GetTextRectForMeasure(Vector2 available)
    {
        return new LayoutRect(
            0f,
            0f,
            Math.Max(0f, available.X - (BorderThickness * 2f) - Padding.Horizontal),
            Math.Max(0f, available.Y - (BorderThickness * 2f) - Padding.Vertical));
    }

    private bool TryActivateHyperlinkAtSelection()
    {
        var offset = SelectionLength > 0 ? SelectionStart : _caretIndex;
        var hyperlink = DocumentViewportController.ResolveHyperlinkAtOffset(Document, offset);
        return hyperlink != null && TryActivateHyperlink(hyperlink);
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

        RaiseRoutedEventInternal(HyperlinkNavigateEvent, new HyperlinkNavigateRoutedEventArgs(HyperlinkNavigateEvent, hyperlink.NavigateUri));
        return true;
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
        InvalidateVisual();
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

    private static bool TryConvertToInt(object? value, out int pageNumber)
    {
        switch (value)
        {
            case int i:
                pageNumber = i;
                return true;
            case string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                pageNumber = parsed;
                return true;
            default:
                pageNumber = 0;
                return false;
        }
    }

    private void SetIfChanged(DependencyProperty property, float current, float value)
    {
        if (Math.Abs(current - value) <= 0.01f)
        {
            return;
        }

        SetValue(property, value);
    }
}

