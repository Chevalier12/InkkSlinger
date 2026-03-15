using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public enum TrackPartRole
{
    None,
    DecreaseButton,
    Thumb,
    IncreaseButton
}

public class Track : Panel
{
    private const float DefaultThumbMinLength = 14f;
    private const float MinimumThumbRatio = 0.05f;
    private const float FallbackThumbRatio = 0.1f;
    private const float ValueEpsilon = 0.01f;

    public static readonly DependencyProperty PartRoleProperty =
        DependencyProperty.RegisterAttached(
            "PartRole",
            typeof(TrackPartRole),
            typeof(Track),
            new FrameworkPropertyMetadata(
                TrackPartRole.None,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is not UIElement element)
                    {
                        return;
                    }

                    if (element.VisualParent is Track visualTrack)
                    {
                        visualTrack.InvalidateMeasure();
                        visualTrack.InvalidateArrange();
                        visualTrack.InvalidateVisual();
                    }
                    else if (element.LogicalParent is Track logicalTrack)
                    {
                        logicalTrack.InvalidateMeasure();
                        logicalTrack.InvalidateArrange();
                        logicalTrack.InvalidateVisual();
                    }
                }));

    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(Track),
            new FrameworkPropertyMetadata(
                Orientation.Vertical,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(
            nameof(Minimum),
            typeof(float),
            typeof(Track),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            nameof(Maximum),
            typeof(float),
            typeof(Track),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(float),
            typeof(Track),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ViewportSizeProperty =
        DependencyProperty.Register(
            nameof(ViewportSize),
            typeof(float),
            typeof(Track),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThumbMinLengthProperty =
        DependencyProperty.Register(
            nameof(ThumbMinLength),
            typeof(float),
            typeof(Track),
            new FrameworkPropertyMetadata(
                DefaultThumbMinLength,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float length && length >= 6f ? length : DefaultThumbMinLength));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(Track),
            new FrameworkPropertyMetadata(Color.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(Thickness),
            typeof(Track),
            new FrameworkPropertyMetadata(Thickness.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    private LayoutRect _trackRect;
    private LayoutRect _thumbRect;
    private LayoutRect _decreaseRegionRect;
    private LayoutRect _increaseRegionRect;

    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public float Minimum
    {
        get => GetValue<float>(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public float Maximum
    {
        get => GetValue<float>(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public float Value
    {
        get => GetValue<float>(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public float ViewportSize
    {
        get => GetValue<float>(ViewportSizeProperty);
        set => SetValue(ViewportSizeProperty, value);
    }

    public float ThumbMinLength
    {
        get => GetValue<float>(ThumbMinLengthProperty);
        set => SetValue(ThumbMinLengthProperty, value);
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

    public static TrackPartRole GetPartRole(UIElement element)
    {
        return element.GetValue<TrackPartRole>(PartRoleProperty);
    }

    public static void SetPartRole(UIElement element, TrackPartRole role)
    {
        element.SetValue(PartRoleProperty, role);
    }

    internal LayoutRect GetThumbRect()
    {
        return _thumbRect;
    }

    internal float GetThumbTravel()
    {
        return Orientation == Orientation.Vertical
            ? _thumbRect.Y - _trackRect.Y
            : _thumbRect.X - _trackRect.X;
    }

    internal float GetValueFromThumbTravel(float thumbTravel)
    {
        var scrollableRange = GetScrollableRange();
        if (scrollableRange <= ValueEpsilon)
        {
            return Minimum;
        }

        var trackLength = GetAxisLength(_trackRect);
        var thumbLength = GetAxisLength(_thumbRect);
        var maxTravel = MathF.Max(0f, trackLength - thumbLength);
        if (maxTravel <= ValueEpsilon)
        {
            return Minimum;
        }

        var clampedTravel = MathF.Max(0f, MathF.Min(maxTravel, thumbTravel));
        var normalized = clampedTravel / maxTravel;
        return ClampValue(Minimum + (normalized * scrollableRange));
    }

    internal bool HitTestDecreaseRegion(Vector2 point)
    {
        return ContainsPoint(_decreaseRegionRect, point);
    }

    internal bool HitTestIncreaseRegion(Vector2 point)
    {
        return ContainsPoint(_increaseRegionRect, point);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        ResolveParts(out var decreaseButton, out var thumb, out var increaseButton);

        MeasurePart(decreaseButton, availableSize);
        MeasurePart(thumb, availableSize);
        MeasurePart(increaseButton, availableSize);

        var baseDesired = base.MeasureOverride(availableSize);
        var thumbMinLength = MathF.Max(6f, ThumbMinLength);
        if (Orientation == Orientation.Vertical)
        {
            var cross = MathF.Max(
                MathF.Max(GetCrossDesiredSize(decreaseButton, isVertical: true), GetCrossDesiredSize(increaseButton, isVertical: true)),
                MathF.Max(GetCrossDesiredSize(thumb, isVertical: true), 12f));
            var desiredHeight =
                ResolveDesiredButtonLength(decreaseButton, cross, isVertical: true) +
                thumbMinLength +
                ResolveDesiredButtonLength(increaseButton, cross, isVertical: true);
            return new Vector2(MathF.Max(baseDesired.X, cross), MathF.Max(baseDesired.Y, desiredHeight));
        }

        var horizontalCross = MathF.Max(
            MathF.Max(GetCrossDesiredSize(decreaseButton, isVertical: false), GetCrossDesiredSize(increaseButton, isVertical: false)),
            MathF.Max(GetCrossDesiredSize(thumb, isVertical: false), 12f));
        var desiredWidth =
            ResolveDesiredButtonLength(decreaseButton, horizontalCross, isVertical: false) +
            thumbMinLength +
            ResolveDesiredButtonLength(increaseButton, horizontalCross, isVertical: false);
        return new Vector2(MathF.Max(baseDesired.X, desiredWidth), MathF.Max(baseDesired.Y, horizontalCross));
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        ResolveParts(out var decreaseButton, out var thumb, out var increaseButton);

        if (Orientation == Orientation.Vertical)
        {
            ArrangeVertical(finalSize, decreaseButton, thumb, increaseButton);
        }
        else
        {
            ArrangeHorizontal(finalSize, decreaseButton, thumb, increaseButton);
        }

        for (var i = 0; i < Children.Count; i++)
        {
            if (Children[i] is not FrameworkElement child)
            {
                continue;
            }

            var role = GetPartRole(child);
            if (role != TrackPartRole.None)
            {
                continue;
            }

            child.Arrange(new LayoutRect(LayoutSlot.X, LayoutSlot.Y, finalSize.X, finalSize.Y));
        }

        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        if (_trackRect.Width <= 0f || _trackRect.Height <= 0f)
        {
            return;
        }

        UiDrawing.DrawFilledRect(spriteBatch, _trackRect, Background, Opacity);
        DrawBorder(spriteBatch, _trackRect);
    }

    private void ArrangeVertical(
        Vector2 finalSize,
        FrameworkElement? decreaseButton,
        FrameworkElement? thumb,
        FrameworkElement? increaseButton)
    {
        var slot = LayoutSlot;
        var cross = MathF.Max(0f, finalSize.X);
        var totalHeight = MathF.Max(0f, finalSize.Y);
        var decreaseLength = MathF.Min(totalHeight, ResolveArrangedButtonLength(decreaseButton, cross, isVertical: true));
        var increaseLength = MathF.Min(MathF.Max(0f, totalHeight - decreaseLength), ResolveArrangedButtonLength(increaseButton, cross, isVertical: true));

        if (decreaseButton != null)
        {
            decreaseButton.Arrange(new LayoutRect(slot.X, slot.Y, cross, decreaseLength));
        }

        if (increaseButton != null)
        {
            increaseButton.Arrange(new LayoutRect(slot.X, slot.Y + totalHeight - increaseLength, cross, increaseLength));
        }

        _trackRect = new LayoutRect(
            slot.X,
            slot.Y + decreaseLength,
            cross,
            MathF.Max(0f, totalHeight - decreaseLength - increaseLength));

        _thumbRect = ComputeThumbRect(_trackRect);
        _decreaseRegionRect = new LayoutRect(_trackRect.X, _trackRect.Y, _trackRect.Width, MathF.Max(0f, _thumbRect.Y - _trackRect.Y));
        _increaseRegionRect = new LayoutRect(
            _trackRect.X,
            _thumbRect.Y + _thumbRect.Height,
            _trackRect.Width,
            MathF.Max(0f, (_trackRect.Y + _trackRect.Height) - (_thumbRect.Y + _thumbRect.Height)));

        if (thumb != null)
        {
            thumb.Arrange(_thumbRect);
        }
    }

    private void ArrangeHorizontal(
        Vector2 finalSize,
        FrameworkElement? decreaseButton,
        FrameworkElement? thumb,
        FrameworkElement? increaseButton)
    {
        var slot = LayoutSlot;
        var cross = MathF.Max(0f, finalSize.Y);
        var totalWidth = MathF.Max(0f, finalSize.X);
        var decreaseLength = MathF.Min(totalWidth, ResolveArrangedButtonLength(decreaseButton, cross, isVertical: false));
        var increaseLength = MathF.Min(MathF.Max(0f, totalWidth - decreaseLength), ResolveArrangedButtonLength(increaseButton, cross, isVertical: false));

        if (decreaseButton != null)
        {
            decreaseButton.Arrange(new LayoutRect(slot.X, slot.Y, decreaseLength, cross));
        }

        if (increaseButton != null)
        {
            increaseButton.Arrange(new LayoutRect(slot.X + totalWidth - increaseLength, slot.Y, increaseLength, cross));
        }

        _trackRect = new LayoutRect(
            slot.X + decreaseLength,
            slot.Y,
            MathF.Max(0f, totalWidth - decreaseLength - increaseLength),
            cross);

        _thumbRect = ComputeThumbRect(_trackRect);
        _decreaseRegionRect = new LayoutRect(_trackRect.X, _trackRect.Y, MathF.Max(0f, _thumbRect.X - _trackRect.X), _trackRect.Height);
        _increaseRegionRect = new LayoutRect(
            _thumbRect.X + _thumbRect.Width,
            _trackRect.Y,
            MathF.Max(0f, (_trackRect.X + _trackRect.Width) - (_thumbRect.X + _thumbRect.Width)),
            _trackRect.Height);

        if (thumb != null)
        {
            thumb.Arrange(_thumbRect);
        }
    }

    private void ResolveParts(out FrameworkElement? decreaseButton, out FrameworkElement? thumb, out FrameworkElement? increaseButton)
    {
        decreaseButton = null;
        thumb = null;
        increaseButton = null;

        for (var i = 0; i < Children.Count; i++)
        {
            if (Children[i] is not FrameworkElement child)
            {
                continue;
            }

            switch (GetPartRole(child))
            {
                case TrackPartRole.DecreaseButton when decreaseButton == null:
                    decreaseButton = child;
                    break;
                case TrackPartRole.Thumb when thumb == null:
                    thumb = child;
                    break;
                case TrackPartRole.IncreaseButton when increaseButton == null:
                    increaseButton = child;
                    break;
            }
        }
    }

    private static void MeasurePart(FrameworkElement? element, Vector2 availableSize)
    {
        element?.Measure(availableSize);
    }

    private static float GetCrossDesiredSize(FrameworkElement? element, bool isVertical)
    {
        if (element == null)
        {
            return 0f;
        }

        return isVertical ? element.DesiredSize.X : element.DesiredSize.Y;
    }

    private static float ResolveDesiredButtonLength(FrameworkElement? element, float crossAxisLength, bool isVertical)
    {
        if (element == null)
        {
            return 0f;
        }

        return isVertical
            ? MathF.Max(crossAxisLength, element.DesiredSize.Y)
            : MathF.Max(crossAxisLength, element.DesiredSize.X);
    }

    private static float ResolveArrangedButtonLength(FrameworkElement? element, float crossAxisLength, bool isVertical)
    {
        if (element == null)
        {
            return 0f;
        }

        if (isVertical &&
            element.GetValueSource(FrameworkElement.HeightProperty) != DependencyPropertyValueSource.Default &&
            float.IsFinite(element.Height) &&
            element.Height > 0f)
        {
            return element.Height;
        }

        if (!isVertical &&
            element.GetValueSource(FrameworkElement.WidthProperty) != DependencyPropertyValueSource.Default &&
            float.IsFinite(element.Width) &&
            element.Width > 0f)
        {
            return element.Width;
        }

        return ResolveDesiredButtonLength(element, crossAxisLength, isVertical);
    }

    private LayoutRect ComputeThumbRect(LayoutRect trackRect)
    {
        if (trackRect.Width <= 0f || trackRect.Height <= 0f)
        {
            return new LayoutRect(trackRect.X, trackRect.Y, 0f, 0f);
        }

        var extent = MathF.Max(0f, Maximum - Minimum);
        if (extent <= ValueEpsilon)
        {
            return trackRect;
        }

        var viewport = MathF.Max(0f, ViewportSize);
        var scrollableRange = GetScrollableRange();
        var offset = ClampValue(Value) - Minimum;

        if (Orientation == Orientation.Vertical)
        {
            var trackLength = MathF.Max(1f, trackRect.Height);
            var ratio = viewport > 0f
                ? MathF.Max(MinimumThumbRatio, MathF.Min(1f, viewport / MathF.Max(viewport, extent)))
                : FallbackThumbRatio;
            var thumbLength = MathF.Min(trackLength, MathF.Max(ThumbMinLength, trackLength * ratio));
            var maxTravel = MathF.Max(0f, trackLength - thumbLength);
            var normalized = scrollableRange <= ValueEpsilon ? 0f : MathF.Max(0f, MathF.Min(1f, offset / scrollableRange));
            return new LayoutRect(trackRect.X, trackRect.Y + (maxTravel * normalized), trackRect.Width, thumbLength);
        }

        var width = MathF.Max(1f, trackRect.Width);
        var horizontalRatio = viewport > 0f
            ? MathF.Max(MinimumThumbRatio, MathF.Min(1f, viewport / MathF.Max(viewport, extent)))
            : FallbackThumbRatio;
        var thumbWidth = MathF.Min(width, MathF.Max(ThumbMinLength, width * horizontalRatio));
        var horizontalTravel = MathF.Max(0f, width - thumbWidth);
        var horizontalNormalized = scrollableRange <= ValueEpsilon ? 0f : MathF.Max(0f, MathF.Min(1f, offset / scrollableRange));
        return new LayoutRect(trackRect.X + (horizontalTravel * horizontalNormalized), trackRect.Y, thumbWidth, trackRect.Height);
    }

    private float GetScrollableRange()
    {
        var extent = MathF.Max(0f, Maximum - Minimum);
        return MathF.Max(0f, extent - MathF.Max(0f, ViewportSize));
    }

    private float ClampValue(float value)
    {
        var maxValue = Minimum + GetScrollableRange();
        if (maxValue < Minimum)
        {
            maxValue = Minimum;
        }

        return MathF.Max(Minimum, MathF.Min(maxValue, value));
    }

    private float GetAxisLength(LayoutRect rect)
    {
        return Orientation == Orientation.Vertical ? rect.Height : rect.Width;
    }

    private void DrawBorder(SpriteBatch spriteBatch, LayoutRect rect)
    {
        var border = BorderThickness;
        if (border.Left > 0f)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(rect.X, rect.Y, border.Left, rect.Height),
                BorderBrush,
                Opacity);
        }

        if (border.Right > 0f)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(rect.X + rect.Width - border.Right, rect.Y, border.Right, rect.Height),
                BorderBrush,
                Opacity);
        }

        if (border.Top > 0f)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(rect.X, rect.Y, rect.Width, border.Top),
                BorderBrush,
                Opacity);
        }

        if (border.Bottom > 0f)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(rect.X, rect.Y + rect.Height - border.Bottom, rect.Width, border.Bottom),
                BorderBrush,
                Opacity);
        }
    }

    private static bool ContainsPoint(LayoutRect rect, Vector2 point)
    {
        return rect.Width > 0f &&
               rect.Height > 0f &&
               point.X >= rect.X &&
               point.X <= rect.X + rect.Width &&
               point.Y >= rect.Y &&
               point.Y <= rect.Y + rect.Height;
    }
}
