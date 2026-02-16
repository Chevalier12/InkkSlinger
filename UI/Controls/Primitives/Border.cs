using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class Border : FrameworkElement
{
    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(Border),
            new FrameworkPropertyMetadata(Color.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(Border),
            new FrameworkPropertyMetadata(Color.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(Thickness),
            typeof(Border),
            new FrameworkPropertyMetadata(Thickness.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(Border),
            new FrameworkPropertyMetadata(Thickness.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    private UIElement? _child;

    public UIElement? Child
    {
        get => _child;
        set
        {
            if (ReferenceEquals(_child, value))
            {
                return;
            }

            if (_child != null)
            {
                _child.SetVisualParent(null);
                _child.SetLogicalParent(null);
            }

            _child = value;
            if (_child != null)
            {
                _child.SetVisualParent(this);
                _child.SetLogicalParent(this);
            }

            InvalidateMeasure();
        }
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

    public Thickness BorderThickness
    {
        get => GetValue<Thickness>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public Thickness Padding
    {
        get => GetValue<Thickness>(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        if (_child != null)
        {
            yield return _child;
        }
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        if (_child != null)
        {
            yield return _child;
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var chrome = GetChromeThickness();
        var innerAvailable = new Vector2(
            System.MathF.Max(0f, availableSize.X - chrome.Horizontal),
            System.MathF.Max(0f, availableSize.Y - chrome.Vertical));

        if (_child is not FrameworkElement childElement)
        {
            return new Vector2(chrome.Horizontal, chrome.Vertical);
        }

        childElement.Measure(innerAvailable);
        return new Vector2(
            childElement.DesiredSize.X + chrome.Horizontal,
            childElement.DesiredSize.Y + chrome.Vertical);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (_child is not FrameworkElement childElement)
        {
            return finalSize;
        }

        var border = BorderThickness;
        var padding = Padding;
        var left = border.Left + padding.Left;
        var top = border.Top + padding.Top;
        var right = border.Right + padding.Right;
        var bottom = border.Bottom + padding.Bottom;

        var childRect = new LayoutRect(
            LayoutSlot.X + left,
            LayoutSlot.Y + top,
            System.MathF.Max(0f, finalSize.X - left - right),
            System.MathF.Max(0f, finalSize.Y - top - bottom));

        childElement.Arrange(childRect);
        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        base.OnRender(spriteBatch);

        var slot = LayoutSlot;
        var border = BorderThickness;

        UiDrawing.DrawFilledRect(spriteBatch, slot, Background, Opacity);

        if (border.Left > 0f)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(slot.X, slot.Y, border.Left, slot.Height),
                BorderBrush,
                Opacity);
        }

        if (border.Right > 0f)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(slot.X + slot.Width - border.Right, slot.Y, border.Right, slot.Height),
                BorderBrush,
                Opacity);
        }

        if (border.Top > 0f)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(slot.X, slot.Y, slot.Width, border.Top),
                BorderBrush,
                Opacity);
        }

        if (border.Bottom > 0f)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(slot.X, slot.Y + slot.Height - border.Bottom, slot.Width, border.Bottom),
                BorderBrush,
                Opacity);
        }
    }

    private Thickness GetChromeThickness()
    {
        var border = BorderThickness;
        var padding = Padding;
        return new Thickness(
            border.Left + padding.Left,
            border.Top + padding.Top,
            border.Right + padding.Right,
            border.Bottom + padding.Bottom);
    }
}
