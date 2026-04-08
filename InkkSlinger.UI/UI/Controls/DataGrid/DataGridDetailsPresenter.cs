using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class DataGridDetailsPresenter : ContentControl
{
    public static readonly DependencyProperty IsVisibleDetailsProperty =
        DependencyProperty.Register(
            nameof(IsVisibleDetails),
            typeof(bool),
            typeof(DataGridDetailsPresenter),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public bool IsVisibleDetails
    {
        get => GetValue<bool>(IsVisibleDetailsProperty);
        set => SetValue(IsVisibleDetailsProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        if (!IsVisibleDetails)
        {
            return Vector2.Zero;
        }

        return base.MeasureOverride(availableSize);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (!IsVisibleDetails)
        {
            if (ContentElement is FrameworkElement content)
            {
                content.Arrange(new LayoutRect(LayoutSlot.X, LayoutSlot.Y, 0f, 0f));
            }

            return Vector2.Zero;
        }

        return base.ArrangeOverride(finalSize);
    }
}
