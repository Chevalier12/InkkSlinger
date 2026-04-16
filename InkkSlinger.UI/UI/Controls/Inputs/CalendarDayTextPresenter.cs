using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public sealed class CalendarDayTextPresenter : FrameworkElement
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(CalendarDayTextPresenter),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(CalendarDayTextPresenter),
            new FrameworkPropertyMetadata(
                Color.White,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public string Text
    {
        get => GetValue<string>(TextProperty) ?? string.Empty;
        set => SetValue(TextProperty, value);
    }

    public Color Foreground
    {
        get => GetValue<Color>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        _ = availableSize;
        return Vector2.Zero;
    }

    protected override bool CanReuseMeasureForAvailableSizeChange(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
    {
        _ = previousAvailableSize;
        _ = nextAvailableSize;
        return true;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        base.OnRender(spriteBatch);

        if (string.IsNullOrEmpty(Text))
        {
            return;
        }

        var fontSize = FrameworkElement.GetFontSize(this);
        var textWidth = UiTextRenderer.MeasureWidth(this, Text, fontSize);
        var lineHeight = UiTextRenderer.GetLineHeight(this, fontSize);
        var textX = LayoutSlot.X + ((LayoutSlot.Width - textWidth) * 0.5f);
        var textY = LayoutSlot.Y + ((LayoutSlot.Height - lineHeight) * 0.5f);
        UiTextRenderer.DrawString(spriteBatch, this, Text, new Vector2(textX, textY), Foreground * Opacity, fontSize);
    }
}
