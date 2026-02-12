using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public class Expander : ContentControl
{
    public static readonly RoutedEvent ExpandedEvent =
        new(nameof(Expanded), RoutingStrategy.Bubble);

    public static readonly RoutedEvent CollapsedEvent =
        new(nameof(Collapsed), RoutingStrategy.Bubble);

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(
            nameof(Header),
            typeof(object),
            typeof(Expander),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (d, args) =>
                {
                    if (d is Expander expander)
                    {
                        expander.UpdateHeaderElement(args.OldValue, args.NewValue);
                    }
                }));

    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(
            nameof(IsExpanded),
            typeof(bool),
            typeof(Expander),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (d, args) =>
                {
                    if (d is Expander expander && args.NewValue is bool expanded)
                    {
                        expander.RaiseRoutedEvent(
                            expanded ? ExpandedEvent : CollapsedEvent,
                            new RoutedSimpleEventArgs(expanded ? ExpandedEvent : CollapsedEvent));
                    }
                }));

    public static readonly DependencyProperty ExpandDirectionProperty =
        DependencyProperty.Register(
            nameof(ExpandDirection),
            typeof(ExpandDirection),
            typeof(Expander),
            new FrameworkPropertyMetadata(
                ExpandDirection.Down,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontProperty =
        DependencyProperty.Register(
            nameof(Font),
            typeof(SpriteFont),
            typeof(Expander),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(Expander),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(Expander),
            new FrameworkPropertyMetadata(new Color(20, 20, 20), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HeaderBackgroundProperty =
        DependencyProperty.Register(
            nameof(HeaderBackground),
            typeof(Color),
            typeof(Expander),
            new FrameworkPropertyMetadata(new Color(38, 38, 38), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(Expander),
            new FrameworkPropertyMetadata(new Color(94, 94, 94), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(Expander),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float thickness && thickness >= 0f ? thickness : 0f));

    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(Expander),
            new FrameworkPropertyMetadata(
                new Thickness(8f),
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HeaderPaddingProperty =
        DependencyProperty.Register(
            nameof(HeaderPadding),
            typeof(Thickness),
            typeof(Expander),
            new FrameworkPropertyMetadata(
                new Thickness(10f, 6f, 10f, 6f),
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    private UIElement? _headerElement;
    private LayoutRect _headerRect;
    private LayoutRect _contentRect;
    private bool _isHeaderPressed;

    public Expander()
    {
        Focusable = true;
        Cursor = UiCursor.Hand;
    }

    public event EventHandler<RoutedSimpleEventArgs> Expanded
    {
        add => AddHandler(ExpandedEvent, value);
        remove => RemoveHandler(ExpandedEvent, value);
    }

    public event EventHandler<RoutedSimpleEventArgs> Collapsed
    {
        add => AddHandler(CollapsedEvent, value);
        remove => RemoveHandler(CollapsedEvent, value);
    }

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue<bool>(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public ExpandDirection ExpandDirection
    {
        get => GetValue<ExpandDirection>(ExpandDirectionProperty);
        set => SetValue(ExpandDirectionProperty, value);
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

    public Color HeaderBackground
    {
        get => GetValue<Color>(HeaderBackgroundProperty);
        set => SetValue(HeaderBackgroundProperty, value);
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

    public Thickness HeaderPadding
    {
        get => GetValue<Thickness>(HeaderPaddingProperty);
        set => SetValue(HeaderPaddingProperty, value);
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        foreach (var element in base.GetVisualChildren())
        {
            yield return element;
        }

        if (_headerElement != null && !ReferenceEquals(_headerElement, ContentElement))
        {
            yield return _headerElement;
        }
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        foreach (var element in base.GetLogicalChildren())
        {
            yield return element;
        }

        if (_headerElement != null && !ReferenceEquals(_headerElement, ContentElement))
        {
            yield return _headerElement;
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var headerSize = MeasureHeader(availableSize);
        var contentSize = Vector2.Zero;

        if (IsExpanded && ContentElement is FrameworkElement content)
        {
            var contentAvailable = GetContentAvailableSize(availableSize, headerSize);
            content.Measure(contentAvailable);
            contentSize = content.DesiredSize;
        }

        return ComposeDesiredSize(headerSize, contentSize);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var headerSize = MeasureHeader(finalSize);
        var slots = ComputeSlots(finalSize, headerSize);
        _headerRect = slots.HeaderRect;
        _contentRect = slots.ContentRect;

        if (_headerElement is FrameworkElement header)
        {
            header.Arrange(_headerRect);
        }

        if (ContentElement is FrameworkElement content)
        {
            if (IsExpanded)
            {
                content.Arrange(_contentRect);
            }
            else
            {
                content.Arrange(new LayoutRect(_contentRect.X, _contentRect.Y, 0f, 0f));
            }
        }

        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var slot = LayoutSlot;
        UiDrawing.DrawFilledRect(spriteBatch, slot, Background, Opacity);
        if (BorderThickness > 0f)
        {
            UiDrawing.DrawRectStroke(spriteBatch, slot, BorderThickness, BorderBrush, Opacity);
        }

        UiDrawing.DrawFilledRect(spriteBatch, _headerRect, HeaderBackground, Opacity);

        DrawChevron(spriteBatch, _headerRect);
        if (_headerElement == null)
        {
            DrawHeaderText(spriteBatch, _headerRect);
        }
    }

    protected override void OnMouseLeftButtonDown(RoutedMouseButtonEventArgs args)
    {
        base.OnMouseLeftButtonDown(args);
        if (!IsEnabled)
        {
            return;
        }

        if (!Contains(_headerRect, args.Position))
        {
            return;
        }

        _isHeaderPressed = true;
        Focus();
        CaptureMouse();
        args.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(RoutedMouseButtonEventArgs args)
    {
        base.OnMouseLeftButtonUp(args);
        var shouldToggle = _isHeaderPressed && Contains(_headerRect, args.Position);
        _isHeaderPressed = false;
        if (ReferenceEquals(InputManager.MouseCapturedElement, this))
        {
            ReleaseMouseCapture();
        }

        if (!shouldToggle || !IsEnabled)
        {
            return;
        }

        IsExpanded = !IsExpanded;
        args.Handled = true;
    }

    protected override void OnLostMouseCapture(RoutedMouseCaptureEventArgs args)
    {
        base.OnLostMouseCapture(args);
        _isHeaderPressed = false;
    }

    protected override void OnKeyDown(RoutedKeyEventArgs args)
    {
        base.OnKeyDown(args);
        if (!IsEnabled)
        {
            return;
        }

        if (args.Key == Keys.Enter || args.Key == Keys.Space)
        {
            IsExpanded = !IsExpanded;
            args.Handled = true;
        }
    }

    private Vector2 MeasureHeader(Vector2 availableSize)
    {
        var padding = HeaderPadding;
        if (_headerElement is FrameworkElement header)
        {
            header.Measure(availableSize);
            return new Vector2(
                header.DesiredSize.X + padding.Horizontal + 16f,
                MathF.Max(16f, header.DesiredSize.Y) + padding.Vertical);
        }

        var text = Header?.ToString() ?? string.Empty;
        var textWidth = FontStashTextRenderer.MeasureWidth(Font, text);
        var textHeight = FontStashTextRenderer.GetLineHeight(Font);
        return new Vector2(
            textWidth + padding.Horizontal + 16f,
            MathF.Max(16f, textHeight) + padding.Vertical);
    }

    private Vector2 GetContentAvailableSize(Vector2 availableSize, Vector2 headerSize)
    {
        var padding = Padding;
        var available = availableSize;
        switch (ExpandDirection)
        {
            case ExpandDirection.Down:
            case ExpandDirection.Up:
                available.Y = MathF.Max(0f, available.Y - headerSize.Y);
                available.X = MathF.Max(0f, available.X - padding.Horizontal);
                available.Y = MathF.Max(0f, available.Y - padding.Vertical);
                break;
            case ExpandDirection.Left:
            case ExpandDirection.Right:
                available.X = MathF.Max(0f, available.X - headerSize.X);
                available.X = MathF.Max(0f, available.X - padding.Horizontal);
                available.Y = MathF.Max(0f, available.Y - padding.Vertical);
                break;
        }

        return available;
    }

    private Vector2 ComposeDesiredSize(Vector2 headerSize, Vector2 contentSize)
    {
        var padding = Padding;
        var paddedContent = IsExpanded
            ? new Vector2(contentSize.X + padding.Horizontal, contentSize.Y + padding.Vertical)
            : Vector2.Zero;

        return ExpandDirection switch
        {
            ExpandDirection.Down or ExpandDirection.Up => new Vector2(
                MathF.Max(headerSize.X, paddedContent.X),
                headerSize.Y + paddedContent.Y),
            _ => new Vector2(
                headerSize.X + paddedContent.X,
                MathF.Max(headerSize.Y, paddedContent.Y))
        };
    }

    private (LayoutRect HeaderRect, LayoutRect ContentRect) ComputeSlots(Vector2 finalSize, Vector2 headerSize)
    {
        var slot = LayoutSlot;
        var padding = Padding;
        switch (ExpandDirection)
        {
            case ExpandDirection.Up:
            {
                var contentHeight = MathF.Max(0f, finalSize.Y - headerSize.Y);
                var contentRect = new LayoutRect(
                    slot.X + padding.Left,
                    slot.Y + padding.Top,
                    MathF.Max(0f, finalSize.X - padding.Horizontal),
                    MathF.Max(0f, contentHeight - padding.Vertical));
                var headerRect = new LayoutRect(slot.X, slot.Y + contentHeight, finalSize.X, headerSize.Y);
                return (headerRect, contentRect);
            }
            case ExpandDirection.Left:
            {
                var contentWidth = MathF.Max(0f, finalSize.X - headerSize.X);
                var contentRect = new LayoutRect(
                    slot.X + padding.Left,
                    slot.Y + padding.Top,
                    MathF.Max(0f, contentWidth - padding.Horizontal),
                    MathF.Max(0f, finalSize.Y - padding.Vertical));
                var headerRect = new LayoutRect(slot.X + contentWidth, slot.Y, headerSize.X, finalSize.Y);
                return (headerRect, contentRect);
            }
            case ExpandDirection.Right:
            {
                var headerRect = new LayoutRect(slot.X, slot.Y, headerSize.X, finalSize.Y);
                var contentRect = new LayoutRect(
                    slot.X + headerSize.X + padding.Left,
                    slot.Y + padding.Top,
                    MathF.Max(0f, finalSize.X - headerSize.X - padding.Horizontal),
                    MathF.Max(0f, finalSize.Y - padding.Vertical));
                return (headerRect, contentRect);
            }
            default:
            {
                var headerRect = new LayoutRect(slot.X, slot.Y, finalSize.X, headerSize.Y);
                var contentRect = new LayoutRect(
                    slot.X + padding.Left,
                    slot.Y + headerSize.Y + padding.Top,
                    MathF.Max(0f, finalSize.X - padding.Horizontal),
                    MathF.Max(0f, finalSize.Y - headerSize.Y - padding.Vertical));
                return (headerRect, contentRect);
            }
        }
    }

    private void DrawHeaderText(SpriteBatch spriteBatch, LayoutRect headerRect)
    {
        var text = Header?.ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var padding = HeaderPadding;
        var textX = headerRect.X + padding.Left + 14f;
        var textY = headerRect.Y + padding.Top;
        FontStashTextRenderer.DrawString(
            spriteBatch,
            Font,
            text,
            new Vector2(textX, textY),
            Foreground * Opacity);
    }

    private void DrawChevron(SpriteBatch spriteBatch, LayoutRect headerRect)
    {
        var center = new Vector2(headerRect.X + 8f, headerRect.Y + (headerRect.Height / 2f));
        var color = Foreground;
        var arm = 3f;
        switch (ExpandDirection)
        {
            case ExpandDirection.Down:
                if (IsExpanded)
                {
                    UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X - arm, center.Y - 1f, arm * 2f, 2f), color, Opacity);
                    UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X - 1f, center.Y - arm, 2f, arm), color, Opacity);
                }
                else
                {
                    UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X - arm, center.Y - 1f, arm * 2f, 2f), color, Opacity);
                }

                break;
            case ExpandDirection.Up:
                if (IsExpanded)
                {
                    UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X - arm, center.Y - 1f, arm * 2f, 2f), color, Opacity);
                    UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X - 1f, center.Y, 2f, arm), color, Opacity);
                }
                else
                {
                    UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X - arm, center.Y - 1f, arm * 2f, 2f), color, Opacity);
                }

                break;
            case ExpandDirection.Left:
            case ExpandDirection.Right:
                UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X - 1f, center.Y - arm, 2f, arm * 2f), color, Opacity);
                UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X - arm, center.Y - 1f, arm * 2f, 2f), color, Opacity);
                break;
        }
    }

    private void UpdateHeaderElement(object? oldHeader, object? newHeader)
    {
        if (oldHeader is UIElement oldElement && ReferenceEquals(_headerElement, oldElement))
        {
            oldElement.SetVisualParent(null);
            oldElement.SetLogicalParent(null);
            _headerElement = null;
        }

        if (newHeader is UIElement newElement)
        {
            _headerElement = newElement;
            _headerElement.SetVisualParent(this);
            _headerElement.SetLogicalParent(this);
        }
        else
        {
            _headerElement = null;
        }
    }

    private static bool Contains(LayoutRect rect, Vector2 point)
    {
        return point.X >= rect.X &&
               point.X <= rect.X + rect.Width &&
               point.Y >= rect.Y &&
               point.Y <= rect.Y + rect.Height;
    }
}
