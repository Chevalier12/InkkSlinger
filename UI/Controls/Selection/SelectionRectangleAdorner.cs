using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class SelectionRectangleAdorner : Adorner
{
    public static readonly DependencyProperty StrokeProperty =
        DependencyProperty.Register(
            nameof(Stroke),
            typeof(Color),
            typeof(SelectionRectangleAdorner),
            new FrameworkPropertyMetadata(new Color(117, 190, 255), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty =
        DependencyProperty.Register(
            nameof(Fill),
            typeof(Color),
            typeof(SelectionRectangleAdorner),
            new FrameworkPropertyMetadata(new Color(117, 190, 255, 30), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(
            nameof(StrokeThickness),
            typeof(float),
            typeof(SelectionRectangleAdorner),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float v && v >= 0f ? v : 0f));

    public static readonly DependencyProperty InsetProperty =
        DependencyProperty.Register(
            nameof(Inset),
            typeof(float),
            typeof(SelectionRectangleAdorner),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public SelectionRectangleAdorner(UIElement adornedElement)
        : base(adornedElement)
    {
    }

    public Color Stroke
    {
        get => GetValue<Color>(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public Color Fill
    {
        get => GetValue<Color>(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public float StrokeThickness
    {
        get => GetValue<float>(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public float Inset
    {
        get => GetValue<float>(InsetProperty);
        set => SetValue(InsetProperty, value);
    }

    protected override LayoutRect GetAdornedBounds()
    {
        var bounds = AdornedElement.LayoutSlot;
        return new LayoutRect(
            bounds.X - Inset,
            bounds.Y - Inset,
            bounds.Width + (Inset * 2f),
            bounds.Height + (Inset * 2f));
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        if (Fill.A > 0)
        {
            UiDrawing.DrawFilledRect(spriteBatch, LayoutSlot, Fill, Opacity);
        }

        if (StrokeThickness > 0f)
        {
            UiDrawing.DrawRectStroke(spriteBatch, LayoutSlot, StrokeThickness, Stroke, Opacity);
        }
    }
}
