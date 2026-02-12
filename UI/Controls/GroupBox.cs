using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class GroupBox : ContentControl
{
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(
            nameof(Header),
            typeof(object),
            typeof(GroupBox),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (d, args) =>
                {
                    if (d is GroupBox groupBox)
                    {
                        groupBox.UpdateHeaderElement(args.OldValue, args.NewValue);
                    }
                }));

    public static readonly DependencyProperty HeaderTemplateProperty =
        DependencyProperty.Register(
            nameof(HeaderTemplate),
            typeof(DataTemplate),
            typeof(GroupBox),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty HeaderTemplateSelectorProperty =
        DependencyProperty.Register(
            nameof(HeaderTemplateSelector),
            typeof(DataTemplateSelector),
            typeof(GroupBox),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty FontProperty =
        DependencyProperty.Register(
            nameof(Font),
            typeof(SpriteFont),
            typeof(GroupBox),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(GroupBox),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(GroupBox),
            new FrameworkPropertyMetadata(new Color(18, 26, 38), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(GroupBox),
            new FrameworkPropertyMetadata(new Color(67, 96, 130), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(GroupBox),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float v && v >= 0f ? v : 0f));

    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(GroupBox),
            new FrameworkPropertyMetadata(
                new Thickness(8f),
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty HeaderPaddingProperty =
        DependencyProperty.Register(
            nameof(HeaderPadding),
            typeof(Thickness),
            typeof(GroupBox),
            new FrameworkPropertyMetadata(
                new Thickness(10f, 0f, 10f, 0f),
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    private UIElement? _headerElement;
    private LayoutRect _headerRect;
    private LayoutRect _contentRect;

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public DataTemplate? HeaderTemplate
    {
        get => GetValue<DataTemplate>(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    public DataTemplateSelector? HeaderTemplateSelector
    {
        get => GetValue<DataTemplateSelector>(HeaderTemplateSelectorProperty);
        set => SetValue(HeaderTemplateSelectorProperty, value);
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

    public Thickness HeaderPadding
    {
        get => GetValue<Thickness>(HeaderPaddingProperty);
        set => SetValue(HeaderPaddingProperty, value);
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        foreach (var child in base.GetVisualChildren())
        {
            yield return child;
        }

        if (_headerElement != null && !ReferenceEquals(_headerElement, ContentElement))
        {
            yield return _headerElement;
        }
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        foreach (var child in base.GetLogicalChildren())
        {
            yield return child;
        }

        if (_headerElement != null && !ReferenceEquals(_headerElement, ContentElement))
        {
            yield return _headerElement;
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var border = BorderThickness;
        var padding = Padding;
        var headerPadding = HeaderPadding;

        var headerSize = MeasureHeader(availableSize);
        var headerHeight = MathF.Max(0f, headerSize.Y);

        var contentAvailable = new Vector2(
            MathF.Max(0f, availableSize.X - (border * 2f) - padding.Horizontal),
            MathF.Max(0f, availableSize.Y - (border * 2f) - padding.Vertical - headerHeight));

        var contentDesired = Vector2.Zero;
        if (ContentElement is FrameworkElement content)
        {
            content.Measure(contentAvailable);
            contentDesired = content.DesiredSize;
        }

        var desiredWidth = MathF.Max(
            contentDesired.X + (border * 2f) + padding.Horizontal,
            headerSize.X + (border * 2f) + headerPadding.Horizontal);
        var desiredHeight = headerHeight + contentDesired.Y + (border * 2f) + padding.Vertical;

        return new Vector2(desiredWidth, desiredHeight);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var border = BorderThickness;
        var padding = Padding;

        var headerSize = MeasureHeader(finalSize);
        var headerHeight = MathF.Max(0f, headerSize.Y);

        _headerRect = new LayoutRect(
            LayoutSlot.X + border + HeaderPadding.Left,
            LayoutSlot.Y,
            MathF.Max(0f, headerSize.X),
            headerHeight);

        if (_headerElement is FrameworkElement header)
        {
            header.Arrange(_headerRect);
        }

        _contentRect = new LayoutRect(
            LayoutSlot.X + border + padding.Left,
            LayoutSlot.Y + border + padding.Top + headerHeight,
            MathF.Max(0f, finalSize.X - (border * 2f) - padding.Horizontal),
            MathF.Max(0f, finalSize.Y - (border * 2f) - padding.Vertical - headerHeight));

        if (ContentElement is FrameworkElement content)
        {
            content.Arrange(_contentRect);
        }

        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        UiDrawing.DrawFilledRect(spriteBatch, LayoutSlot, Background, Opacity);

        if (BorderThickness > 0f)
        {
            var topY = LayoutSlot.Y;
            var leftX = LayoutSlot.X;
            var rightX = LayoutSlot.X + LayoutSlot.Width - BorderThickness;
            var bottomY = LayoutSlot.Y + LayoutSlot.Height - BorderThickness;

            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(leftX, topY, BorderThickness, LayoutSlot.Height), BorderBrush, Opacity);
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(rightX, topY, BorderThickness, LayoutSlot.Height), BorderBrush, Opacity);
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(leftX, bottomY, LayoutSlot.Width, BorderThickness), BorderBrush, Opacity);

            var topLineLeft = leftX;
            var topLineRight = leftX + LayoutSlot.Width;
            if (_headerRect.Width > 0f)
            {
                var gapStart = _headerRect.X - 4f;
                var gapEnd = _headerRect.X + _headerRect.Width + 4f;
                var leftSegmentWidth = MathF.Max(0f, gapStart - topLineLeft);
                if (leftSegmentWidth > 0f)
                {
                    UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(topLineLeft, topY, leftSegmentWidth, BorderThickness), BorderBrush, Opacity);
                }

                var rightSegmentX = MathF.Max(gapEnd, topLineLeft);
                var rightSegmentWidth = MathF.Max(0f, topLineRight - rightSegmentX);
                if (rightSegmentWidth > 0f)
                {
                    UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(rightSegmentX, topY, rightSegmentWidth, BorderThickness), BorderBrush, Opacity);
                }
            }
            else
            {
                UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(topLineLeft, topY, LayoutSlot.Width, BorderThickness), BorderBrush, Opacity);
            }
        }

        if (_headerElement == null && Header is string text && !string.IsNullOrWhiteSpace(text))
        {
            var textY = LayoutSlot.Y;
            FontStashTextRenderer.DrawString(spriteBatch, Font, text, new Vector2(_headerRect.X, textY), Foreground * Opacity);
        }
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (args.Property == HeaderTemplateProperty || args.Property == HeaderTemplateSelectorProperty)
        {
            UpdateHeaderElement(Header, Header);
        }
    }

    private Vector2 MeasureHeader(Vector2 availableSize)
    {
        if (_headerElement is FrameworkElement header)
        {
            header.Measure(availableSize);
            return header.DesiredSize;
        }

        if (Header is string text && !string.IsNullOrWhiteSpace(text))
        {
            return new Vector2(FontStashTextRenderer.MeasureWidth(Font, text), FontStashTextRenderer.GetLineHeight(Font));
        }

        return Vector2.Zero;
    }

    private void UpdateHeaderElement(object? oldHeader, object? newHeader)
    {
        if (_headerElement != null)
        {
            _headerElement.SetVisualParent(null);
            _headerElement.SetLogicalParent(null);
            _headerElement = null;
        }

        if (newHeader is UIElement headerElement)
        {
            _headerElement = headerElement;
        }
        else
        {
            var template = DataTemplateResolver.ResolveTemplateForContent(
                this,
                newHeader,
                HeaderTemplate,
                HeaderTemplateSelector,
                this);
            if (template != null)
            {
                _headerElement = template.Build(newHeader, this);
            }
        }

        if (_headerElement != null)
        {
            _headerElement.SetVisualParent(this);
            _headerElement.SetLogicalParent(this);
        }
    }
}
