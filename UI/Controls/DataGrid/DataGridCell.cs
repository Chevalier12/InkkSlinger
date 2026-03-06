using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class DataGridCell : Control
{
    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(
            nameof(Content),
            typeof(object),
            typeof(DataGridCell),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is not DataGridCell cell || cell._isSynchronizingContentValue)
                    {
                        return;
                    }

                    cell.SyncAliasValue(ContentProperty, ValueProperty, args.NewValue);
                }));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(object),
            typeof(DataGridCell),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is not DataGridCell cell || cell._isSynchronizingContentValue)
                    {
                        return;
                    }

                    cell.SyncAliasValue(ValueProperty, ContentProperty, args.NewValue);
                }));

    public new static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(
            nameof(IsSelected),
            typeof(bool),
            typeof(DataGridCell),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(DataGridCell),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(DataGridCell),
            new FrameworkPropertyMetadata(new Color(24, 34, 49), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedBackgroundProperty =
        DependencyProperty.Register(
            nameof(SelectedBackground),
            typeof(Color),
            typeof(DataGridCell),
            new FrameworkPropertyMetadata(new Color(61, 99, 145), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(DataGridCell),
            new FrameworkPropertyMetadata(new Color(57, 80, 111), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty FontProperty =
        DependencyProperty.Register(
            nameof(Font),
            typeof(SpriteFont),
            typeof(DataGridCell),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    private bool _isSynchronizingContentValue;
    private DataGrid? _owner;
    private DataGridRowState? _rowState;
    private DataGridColumnState? _columnState;
    private UIElement? _editorElement;

    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public object? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public new bool IsSelected
    {
        get => GetValue<bool>(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
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

    public Color SelectedBackground
    {
        get => GetValue<Color>(SelectedBackgroundProperty);
        set => SetValue(SelectedBackgroundProperty, value);
    }

    public new Color BorderBrush
    {
        get => GetValue<Color>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public new SpriteFont? Font
    {
        get => GetValue<SpriteFont>(FontProperty);
        set => SetValue(FontProperty, value);
    }

    internal int ColumnIndex { get; set; }

    internal DataGridRowState? RowState => _rowState;

    internal DataGridColumnState? ColumnState => _columnState;

    internal UIElement? EditingElement => _editorElement;

    internal bool ShowHorizontalGridLine { get; set; } = true;

    internal bool ShowVerticalGridLine { get; set; } = true;

    internal Color HorizontalGridLineBrush { get; set; } = new Color(57, 80, 111);

    internal Color VerticalGridLineBrush { get; set; } = new Color(57, 80, 111);

    internal bool ShowHorizontalGridLineForTesting => ShowHorizontalGridLine;

    internal bool ShowVerticalGridLineForTesting => ShowVerticalGridLine;

    internal Color HorizontalGridLineBrushForTesting => HorizontalGridLineBrush;

    internal Color VerticalGridLineBrushForTesting => VerticalGridLineBrush;

    internal void BindState(
        DataGrid owner,
        DataGridRowState rowState,
        DataGridColumnState columnState,
        bool showHorizontalGridLine,
        bool showVerticalGridLine,
        Color horizontalGridLineBrush,
        Color verticalGridLineBrush)
    {
        _owner = owner;
        _rowState = rowState;
        _columnState = columnState;
        ColumnIndex = columnState.DisplayIndex;
        Font = owner.Font;
        if (GetValueSource(ForegroundProperty) == DependencyPropertyValueSource.Default)
        {
            Foreground = owner.Foreground;
        }

        ShowHorizontalGridLine = showHorizontalGridLine;
        ShowVerticalGridLine = showVerticalGridLine;
        HorizontalGridLineBrush = horizontalGridLineBrush;
        VerticalGridLineBrush = verticalGridLineBrush;
        RefreshContentFromState();
    }

    internal void RefreshContentFromState()
    {
        if (_owner == null || _rowState == null || _columnState == null)
        {
            return;
        }

        Content = _owner.ResolveCellContent(_rowState.Item, _columnState.Column);
        SyncTemplateContentTypography();
    }

    internal void ApplySelectionState(bool isSelected)
    {
        IsSelected = isSelected;
    }

    internal void BeginEdit(UIElement editorElement)
    {
        EndEdit();
        _editorElement = editorElement;
        _editorElement.SetVisualParent(this);
        _editorElement.SetLogicalParent(this);
        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
    }

    internal void EndEdit()
    {
        if (_editorElement == null)
        {
            return;
        }

        _editorElement.SetVisualParent(null);
        _editorElement.SetLogicalParent(null);
        _editorElement = null;
        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
    }

    public override System.Collections.Generic.IEnumerable<UIElement> GetVisualChildren()
    {
        foreach (var child in base.GetVisualChildren())
        {
            yield return child;
        }

        if (_editorElement != null)
        {
            yield return _editorElement;
        }
    }

    public override System.Collections.Generic.IEnumerable<UIElement> GetLogicalChildren()
    {
        foreach (var child in base.GetLogicalChildren())
        {
            yield return child;
        }

        if (_editorElement != null)
        {
            yield return _editorElement;
        }
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        SyncTemplateContentTypography();
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);
        if (args.Property == ContentProperty ||
            args.Property == FontProperty ||
            args.Property == ForegroundProperty ||
            args.Property == FrameworkElement.FontFamilyProperty ||
            args.Property == FrameworkElement.FontSizeProperty ||
            args.Property == FrameworkElement.FontWeightProperty)
        {
            SyncTemplateContentTypography();
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        if (_editorElement is FrameworkElement editor)
        {
            editor.Measure(availableSize);
        }

        var desired = base.MeasureOverride(availableSize);
        if (HasTemplateRoot)
        {
            SyncTemplateContentTypography();
            if (_editorElement is FrameworkElement editorElement)
            {
                desired.X = System.MathF.Max(desired.X, editorElement.DesiredSize.X);
                desired.Y = System.MathF.Max(desired.Y, editorElement.DesiredSize.Y);
            }

            return desired;
        }

        var text = Content?.ToString() ?? string.Empty;
        var padding = Padding;
        var border = BorderThickness;
        var chromeWidth = padding.Left + padding.Right + border.Left + border.Right;
        var chromeHeight = padding.Top + padding.Bottom + border.Top + border.Bottom;
        var width = FontStashTextRenderer.MeasureWidth(Font, text, FontSize) + chromeWidth;
        var height = FontStashTextRenderer.GetLineHeight(Font, FontSize) + chromeHeight;
        desired.X = System.MathF.Max(desired.X, width);
        desired.Y = System.MathF.Max(desired.Y, height);
        if (_editorElement is FrameworkElement inlineEditor)
        {
            desired.X = System.MathF.Max(desired.X, inlineEditor.DesiredSize.X);
            desired.Y = System.MathF.Max(desired.Y, inlineEditor.DesiredSize.Y);
        }

        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        if (HasTemplateRoot)
        {
            SyncTemplateContentTypography();
        }

        if (_editorElement is FrameworkElement editor)
        {
            editor.Arrange(new LayoutRect(LayoutSlot.X, LayoutSlot.Y, finalSize.X, finalSize.Y));
        }

        return arranged;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        base.OnRender(spriteBatch);

        if (!HasTemplateRoot)
        {
            var hasStyleDrivenBackground = GetValueSource(BackgroundProperty) != DependencyPropertyValueSource.Default;
            var fill = hasStyleDrivenBackground
                ? Background
                : (IsSelected ? SelectedBackground : Background);
            UiDrawing.DrawFilledRect(spriteBatch, LayoutSlot, fill, Opacity);

            var border = BorderThickness;
            if (border.Left > 0f)
            {
                UiDrawing.DrawFilledRect(
                    spriteBatch,
                    new LayoutRect(LayoutSlot.X, LayoutSlot.Y, border.Left, LayoutSlot.Height),
                    BorderBrush,
                    Opacity);
            }

            if (border.Right > 0f)
            {
                UiDrawing.DrawFilledRect(
                    spriteBatch,
                    new LayoutRect(LayoutSlot.X + LayoutSlot.Width - border.Right, LayoutSlot.Y, border.Right, LayoutSlot.Height),
                    BorderBrush,
                    Opacity);
            }

            if (border.Top > 0f)
            {
                UiDrawing.DrawFilledRect(
                    spriteBatch,
                    new LayoutRect(LayoutSlot.X, LayoutSlot.Y, LayoutSlot.Width, border.Top),
                    BorderBrush,
                    Opacity);
            }

            if (border.Bottom > 0f)
            {
                UiDrawing.DrawFilledRect(
                    spriteBatch,
                    new LayoutRect(LayoutSlot.X, LayoutSlot.Y + LayoutSlot.Height - border.Bottom, LayoutSlot.Width, border.Bottom),
                    BorderBrush,
                    Opacity);
            }
        }

        if (ShowHorizontalGridLine && LayoutSlot.Width > 0f && LayoutSlot.Height > 0f)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(LayoutSlot.X, LayoutSlot.Y + LayoutSlot.Height - 1f, LayoutSlot.Width, 1f),
                HorizontalGridLineBrush,
                Opacity);
        }

        if (ShowVerticalGridLine && LayoutSlot.Width > 0f && LayoutSlot.Height > 0f)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(LayoutSlot.X + LayoutSlot.Width - 1f, LayoutSlot.Y, 1f, LayoutSlot.Height),
                VerticalGridLineBrush,
                Opacity);
        }

        if (HasTemplateRoot)
        {
            return;
        }

        if (_editorElement != null)
        {
            return;
        }

        var text = Content?.ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var padding = Padding;
        var borderThickness = BorderThickness;
        var left = LayoutSlot.X + borderThickness.Left + padding.Left;
        var top = LayoutSlot.Y + borderThickness.Top + padding.Top;
        var bottom = LayoutSlot.Y + LayoutSlot.Height - borderThickness.Bottom - padding.Bottom;
        var contentHeight = System.MathF.Max(0f, bottom - top);
        var lineHeight = FontStashTextRenderer.GetLineHeight(Font, FontSize);
        var x = left;
        var y = top + ((contentHeight - lineHeight) / 2f);
        FontStashTextRenderer.DrawString(spriteBatch, Font, text, new Vector2(x, y), Foreground * Opacity, FontSize);
    }

    private void SyncAliasValue(DependencyProperty? sourceProperty, DependencyProperty? targetProperty, object? value)
    {
        if (sourceProperty == null || targetProperty == null)
        {
            return;
        }

        _isSynchronizingContentValue = true;
        try
        {
            if (!Equals(GetValue(targetProperty), value))
            {
                SetValue(targetProperty, value);
            }
        }
        finally
        {
            _isSynchronizingContentValue = false;
        }
    }

    private void SyncTemplateContentTypography()
    {
        if (!TryGetFallbackContentLabel(out var label))
        {
            return;
        }

        label.Font = Font;
        label.Foreground = Foreground;
        label.FontFamily = FontFamily;
        label.FontSize = FontSize;
        label.FontWeight = FontWeight;
    }

    internal (SpriteFont? Font, Color Foreground, string FontFamily, float FontSize, string FontWeight) GetDisplayedTypography()
    {
        if (TryGetFallbackContentLabel(out var label))
        {
            return (label.Font, label.Foreground, label.FontFamily, label.FontSize, label.FontWeight);
        }

        return (Font, Foreground, FontFamily, FontSize, FontWeight);
    }

    private bool TryGetFallbackContentLabel(out Label label)
    {
        label = null!;
        if (!HasTemplateRoot)
        {
            return false;
        }

        foreach (var child in GetVisualChildren())
        {
            if (child is not Border border)
            {
                continue;
            }

            foreach (var borderChild in border.GetVisualChildren())
            {
                if (borderChild is not ContentPresenter presenter)
                {
                    continue;
                }

                foreach (var presented in presenter.GetVisualChildren())
                {
                    if (presented is Label contentLabel)
                    {
                        label = contentLabel;
                        return true;
                    }
                }
            }
        }

        return false;
    }
}

