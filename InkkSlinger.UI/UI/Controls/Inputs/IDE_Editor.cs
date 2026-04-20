using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

[TemplatePart("PART_Root", typeof(Grid))]
[TemplatePart("PART_LineNumberBorder", typeof(Border))]
[TemplatePart("PART_LineNumberSeparator", typeof(Border))]
[TemplatePart("PART_LineNumberPresenter", typeof(IDEEditorLineNumberPresenter))]
[TemplatePart("PART_Editor", typeof(RichTextBox))]
public sealed class IDE_Editor : Control, ITextInputControl
{
    private static readonly Lazy<Style> DefaultStyle = new(BuildDefaultStyle);

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
                coerceValueCallback: static (_, value) => value is float width && width >= 0f ? width : 56f));

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

    private Grid? _root;
    private Border? _lineNumberBorder;
    private Border? _lineNumberSeparator;
    private IDEEditorLineNumberPresenter? _lineNumberPresenter;
    private RichTextBox? _editor;
    private int _cachedLineCount = 1;
    private int _lastRenderedLineCount = -1;
    private int _lastRenderedFirstVisibleLine = -1;
    private int _lastRenderedVisibleLineCount = -1;
    private float _lastRenderedLineOffset = float.NaN;
    private float _lastRenderedLineHeight = float.NaN;

    public IDE_Editor()
    {
        SetValue(DocumentProperty, CreateDefaultDocument());
        UpdateCachedLineCount(DocumentText);
    }

    public event EventHandler? ViewportChanged;

    public event EventHandler<RoutedSimpleEventArgs>? TextChanged;

    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

    public FlowDocument Document
    {
        get => GetValue<FlowDocument>(DocumentProperty) ?? CreateDefaultDocument();
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

    public int LineCount => CountLines(DocumentText);

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
        _editor = GetTemplateChild("PART_Editor") as RichTextBox;

        ApplyTemplateStyling();

        if (_editor == null)
        {
            return;
        }

        SyncEditorProperties();
        _editor.DocumentChanged += OnEditorDocumentChanged;
        _editor.TextChanged += OnEditorTextChanged;
        _editor.SelectionChanged += OnEditorSelectionChanged;
        _editor.ViewportChanged += OnEditorViewportChanged;
        _editor.LayoutUpdated += OnEditorLayoutUpdated;

        UpdateCachedLineCount(DocumentText);
        UpdateLineNumberGutter(force: true);
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (ReferenceEquals(args.Property, DocumentProperty))
        {
            SyncEditorProperties();
            UpdateCachedLineCount(DocumentText);
            UpdateLineNumberGutter(force: true);
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
            ReferenceEquals(args.Property, FrameworkElement.FontFamilyProperty) ||
            ReferenceEquals(args.Property, FrameworkElement.FontSizeProperty))
        {
            SyncEditorProperties();
        }

        if (ReferenceEquals(args.Property, LineNumberWidthProperty) ||
            ReferenceEquals(args.Property, LineNumberBackgroundProperty) ||
            ReferenceEquals(args.Property, LineNumberForegroundProperty) ||
            ReferenceEquals(args.Property, LineNumberSeparatorBrushProperty) ||
            ReferenceEquals(args.Property, FrameworkElement.FontFamilyProperty) ||
            ReferenceEquals(args.Property, FrameworkElement.FontSizeProperty))
        {
            ApplyTemplateStyling();
            UpdateLineNumberGutter(force: true);
        }
    }

    public void Select(int start, int length)
    {
        _editor?.Select(start, length);
        UpdateLineNumberGutter(force: false);
    }

    public void ScrollToHorizontalOffset(float offset)
    {
        _editor?.ScrollToHorizontalOffset(offset);
        UpdateLineNumberGutter(force: false);
    }

    public void ScrollToVerticalOffset(float offset)
    {
        _editor?.ScrollToVerticalOffset(offset);
        UpdateLineNumberGutter(force: false);
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
        return _editor != null && _editor.HandleKeyDownFromInput(key, modifiers);
    }

    public bool HandlePointerDownFromInput(Vector2 pointerPosition, bool extendSelection)
    {
        return _editor != null && _editor.HandlePointerDownFromInput(pointerPosition, extendSelection);
    }

    public bool HandlePointerMoveFromInput(Vector2 pointerPosition)
    {
        return _editor != null && _editor.HandlePointerMoveFromInput(pointerPosition);
    }

    public bool HandlePointerUpFromInput()
    {
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
        UpdateCachedLineCount(DocumentText);
        UpdateLineNumberGutter(force: true);
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

    internal (float HorizontalOffset, float VerticalOffset, float ViewportWidth, float ViewportHeight, float ExtentWidth, float ExtentHeight) GetScrollMetricsSnapshot()
    {
        return _editor?.GetScrollMetricsSnapshot() ?? (0f, 0f, 0f, 0f, 0f, 0f);
    }

    private void OnEditorTextChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        UpdateCachedLineCount(DocumentText);
        UpdateLineNumberGutter(force: true);
        TextChanged?.Invoke(this, args);
    }

    private void OnEditorDocumentChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        SyncDocumentFromEditor();
        UpdateCachedLineCount(DocumentText);
        UpdateLineNumberGutter(force: true);
    }

    private void OnEditorSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        _ = sender;
        SelectionChanged?.Invoke(this, args);
    }

    private void OnEditorViewportChanged(object? sender, EventArgs args)
    {
        _ = sender;
        UpdateLineNumberGutter(force: false);
        ViewportChanged?.Invoke(this, args);
    }

    private void OnEditorLayoutUpdated(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        UpdateLineNumberGutter(force: false);
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
        _editor = null;
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

    private void UpdateCachedLineCount(string? text)
    {
        _cachedLineCount = CountLines(text);
    }

    private void UpdateLineNumberGutter(bool force)
    {
        if (_editor == null || _lineNumberPresenter == null)
        {
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
            return;
        }

        _lineNumberPresenter.LineHeight = lineHeight;
        _lineNumberPresenter.FontSize = FontSize;
        _lineNumberPresenter.VerticalLineOffset = lineOffset;
        _lineNumberPresenter.UpdateVisibleRange(firstVisibleLine, visibleLineCount);

        _lastRenderedLineCount = lineCount;
        _lastRenderedFirstVisibleLine = firstVisibleLine;
        _lastRenderedVisibleLineCount = visibleLineCount;
        _lastRenderedLineOffset = lineOffset;
        _lastRenderedLineHeight = lineHeight;
    }

    private float EstimateLineHeight(int lineCount)
    {
        if (_editor != null && _editor.TryGetCaretBounds(out var caretBounds) && caretBounds.Height > 0.01f)
        {
            return Math.Max(1f, caretBounds.Height);
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

        var firstVisibleLineFromScroll = Math.Clamp((int)MathF.Floor(verticalOffset / lineHeight), 0, Math.Max(0, lineCount - 1));
        var scrollableLineCount = Math.Max(0, lineCount - approximateVisibleLineCount);
        if (scrollableLineCount > 0 && _editor.ScrollableHeight > 0.01f)
        {
            firstVisibleLineFromScroll = Math.Clamp(
                (int)MathF.Round((verticalOffset / _editor.ScrollableHeight) * scrollableLineCount),
                0,
                Math.Max(0, lineCount - 1));
        }

        return firstVisibleLineFromScroll;
    }

    private static int CountLines(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 1;
        }

        var lineCount = 1;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\n')
            {
                lineCount++;
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
        template.BindTemplate("PART_Editor", FrameworkElement.FontFamilyProperty, FrameworkElement.FontFamilyProperty);
        template.BindTemplate("PART_Editor", FrameworkElement.FontSizeProperty, FrameworkElement.FontSizeProperty);

        return template;
    }
}