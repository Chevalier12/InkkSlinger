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
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            nameof(Maximum),
            typeof(float),
            typeof(Track),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(float),
            typeof(Track),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ViewportSizeProperty =
        DependencyProperty.Register(
            nameof(ViewportSize),
            typeof(float),
            typeof(Track),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsViewportSizedThumbProperty =
        DependencyProperty.Register(
            nameof(IsViewportSizedThumb),
            typeof(bool),
            typeof(Track),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsDirectionReversedProperty =
        DependencyProperty.Register(
            nameof(IsDirectionReversed),
            typeof(bool),
            typeof(Track),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThumbLengthProperty =
        DependencyProperty.Register(
            nameof(ThumbLength),
            typeof(float),
            typeof(Track),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float length && length >= 0f ? length : 0f));

    public static readonly DependencyProperty ThumbMinLengthProperty =
        DependencyProperty.Register(
            nameof(ThumbMinLength),
            typeof(float),
            typeof(Track),
            new FrameworkPropertyMetadata(
                DefaultThumbMinLength,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float length && length >= 6f ? length : DefaultThumbMinLength));

    public static readonly DependencyProperty TrackThicknessProperty =
        DependencyProperty.Register(
            nameof(TrackThickness),
            typeof(float),
            typeof(Track),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float thickness && thickness >= 0f ? thickness : 0f));

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

    public bool IsViewportSizedThumb
    {
        get => GetValue<bool>(IsViewportSizedThumbProperty);
        set => SetValue(IsViewportSizedThumbProperty, value);
    }

    public bool IsDirectionReversed
    {
        get => GetValue<bool>(IsDirectionReversedProperty);
        set => SetValue(IsDirectionReversedProperty, value);
    }

    public float ThumbLength
    {
        get => GetValue<float>(ThumbLengthProperty);
        set => SetValue(ThumbLengthProperty, value);
    }

    public float ThumbMinLength
    {
        get => GetValue<float>(ThumbMinLengthProperty);
        set => SetValue(ThumbMinLengthProperty, value);
    }

    public float TrackThickness
    {
        get => GetValue<float>(TrackThicknessProperty);
        set => SetValue(TrackThicknessProperty, value);
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

    internal LayoutRect GetTrackRect()
    {
        return _trackRect;
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
        if (IsDirectionReversed)
        {
            normalized = 1f - normalized;
        }

        return ClampValue(Minimum + (normalized * scrollableRange));
    }

    internal float GetValueFromPoint(Vector2 point, bool useThumbCenterOffset)
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

        var axisPoint = Orientation == Orientation.Vertical ? point.Y : point.X;
        var trackStart = Orientation == Orientation.Vertical ? _trackRect.Y : _trackRect.X;
        var adjustedPoint = axisPoint - trackStart;
        if (useThumbCenterOffset)
        {
            adjustedPoint -= thumbLength / 2f;
        }

        return GetValueFromThumbTravel(adjustedPoint);
    }

    internal float GetValuePosition(float value)
    {
        var scrollableRange = GetScrollableRange();
        var trackLength = GetAxisLength(_trackRect);
        var thumbLength = GetAxisLength(_thumbRect);
        var maxTravel = MathF.Max(0f, trackLength - thumbLength);
        if (maxTravel <= ValueEpsilon || scrollableRange <= ValueEpsilon)
        {
            return Orientation == Orientation.Vertical
                ? _trackRect.Y + (trackLength / 2f)
                : _trackRect.X + (trackLength / 2f);
        }

        var normalized = (ClampValue(value) - Minimum) / scrollableRange;
        var positionFraction = IsDirectionReversed ? 1f - normalized : normalized;
        var travel = maxTravel * MathF.Max(0f, MathF.Min(1f, positionFraction));
        return Orientation == Orientation.Vertical
            ? _trackRect.Y + travel + (thumbLength / 2f)
            : _trackRect.X + travel + (thumbLength / 2f);
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
        var thumbLength = ResolveThumbAxisLength();
        if (Orientation == Orientation.Vertical)
        {
            var cross = MathF.Max(
                MathF.Max(GetCrossDesiredSize(decreaseButton, isVertical: true), GetCrossDesiredSize(increaseButton, isVertical: true)),
                MathF.Max(GetCrossDesiredSize(thumb, isVertical: true), MathF.Max(ResolveTrackCrossLength(MathF.Max(GetCrossDesiredSize(thumb, isVertical: true), 12f)), 12f)));
            var desiredHeight =
                ResolveDesiredButtonLength(decreaseButton, cross, isVertical: true) +
                MathF.Max(thumbLength, ThumbMinLength) +
                ResolveDesiredButtonLength(increaseButton, cross, isVertical: true);
            return new Vector2(MathF.Max(baseDesired.X, cross), MathF.Max(baseDesired.Y, desiredHeight));
        }

        var horizontalCross = MathF.Max(
            MathF.Max(GetCrossDesiredSize(decreaseButton, isVertical: false), GetCrossDesiredSize(increaseButton, isVertical: false)),
            MathF.Max(GetCrossDesiredSize(thumb, isVertical: false), MathF.Max(ResolveTrackCrossLength(MathF.Max(GetCrossDesiredSize(thumb, isVertical: false), 12f)), 12f)));
        var desiredWidth =
            ResolveDesiredButtonLength(decreaseButton, horizontalCross, isVertical: false) +
            MathF.Max(thumbLength, ThumbMinLength) +
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

            if (GetPartRole(child) != TrackPartRole.None)
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
        var slotWidth = MathF.Max(0f, finalSize.X);
        var slotHeight = MathF.Max(0f, finalSize.Y);

        if (IsViewportSizedThumb)
        {
            var decreaseLength = MathF.Min(slotHeight, ResolveArrangedButtonLength(decreaseButton, slotWidth, isVertical: true));
            var increaseLength = MathF.Min(MathF.Max(0f, slotHeight - decreaseLength), ResolveArrangedButtonLength(increaseButton, slotWidth, isVertical: true));

            decreaseButton?.Arrange(new LayoutRect(slot.X, slot.Y, slotWidth, decreaseLength));
            increaseButton?.Arrange(new LayoutRect(slot.X, slot.Y + slotHeight - increaseLength, slotWidth, increaseLength));

            _trackRect = CreateCenteredTrackRect(
                new LayoutRect(slot.X, slot.Y + decreaseLength, slotWidth, MathF.Max(0f, slotHeight - decreaseLength - increaseLength)),
                slotWidth,
                MathF.Max(0f, slotHeight - decreaseLength - increaseLength),
                isVertical: true);
            _thumbRect = ComputeThumbRect(_trackRect);
            var trackStartRegionRect = new LayoutRect(_trackRect.X, _trackRect.Y, _trackRect.Width, MathF.Max(0f, _thumbRect.Y - _trackRect.Y));
            var trackEndRegionRect = new LayoutRect(
                _trackRect.X,
                _thumbRect.Y + _thumbRect.Height,
                _trackRect.Width,
                MathF.Max(0f, (_trackRect.Y + _trackRect.Height) - (_thumbRect.Y + _thumbRect.Height)));

            _decreaseRegionRect = IsDirectionReversed ? trackEndRegionRect : trackStartRegionRect;
            _increaseRegionRect = IsDirectionReversed ? trackStartRegionRect : trackEndRegionRect;
            thumb?.Arrange(_thumbRect);
            return;
        }

        _trackRect = CreateCenteredTrackRect(slot, slotWidth, slotHeight, isVertical: true);
        _thumbRect = ComputeSliderThumbRect(_trackRect, slotWidth, slotHeight);
        var startRegionRect = new LayoutRect(_trackRect.X, _trackRect.Y, _trackRect.Width, MathF.Max(0f, _thumbRect.Y - _trackRect.Y));
        var endRegionRect = new LayoutRect(
            _trackRect.X,
            _thumbRect.Y + _thumbRect.Height,
            _trackRect.Width,
            MathF.Max(0f, (_trackRect.Y + _trackRect.Height) - (_thumbRect.Y + _thumbRect.Height)));

        var startButtonRect = new LayoutRect(slot.X, startRegionRect.Y, slotWidth, startRegionRect.Height);
        var endButtonRect = new LayoutRect(slot.X, endRegionRect.Y, slotWidth, endRegionRect.Height);
        _decreaseRegionRect = IsDirectionReversed ? endButtonRect : startButtonRect;
        _increaseRegionRect = IsDirectionReversed ? startButtonRect : endButtonRect;

        decreaseButton?.Arrange(_decreaseRegionRect);
        increaseButton?.Arrange(_increaseRegionRect);
        thumb?.Arrange(_thumbRect);
    }

    private void ArrangeHorizontal(
        Vector2 finalSize,
        FrameworkElement? decreaseButton,
        FrameworkElement? thumb,
        FrameworkElement? increaseButton)
    {
        var slot = LayoutSlot;
        var slotWidth = MathF.Max(0f, finalSize.X);
        var slotHeight = MathF.Max(0f, finalSize.Y);

        if (IsViewportSizedThumb)
        {
            var decreaseLength = MathF.Min(slotWidth, ResolveArrangedButtonLength(decreaseButton, slotHeight, isVertical: false));
            var increaseLength = MathF.Min(MathF.Max(0f, slotWidth - decreaseLength), ResolveArrangedButtonLength(increaseButton, slotHeight, isVertical: false));

            decreaseButton?.Arrange(new LayoutRect(slot.X, slot.Y, decreaseLength, slotHeight));
            increaseButton?.Arrange(new LayoutRect(slot.X + slotWidth - increaseLength, slot.Y, increaseLength, slotHeight));

            _trackRect = CreateCenteredTrackRect(
                new LayoutRect(slot.X + decreaseLength, slot.Y, MathF.Max(0f, slotWidth - decreaseLength - increaseLength), slotHeight),
                MathF.Max(0f, slotWidth - decreaseLength - increaseLength),
                slotHeight,
                isVertical: false);
            _thumbRect = ComputeThumbRect(_trackRect);
            var trackStartRegionRect = new LayoutRect(_trackRect.X, _trackRect.Y, MathF.Max(0f, _thumbRect.X - _trackRect.X), _trackRect.Height);
            var trackEndRegionRect = new LayoutRect(
                _thumbRect.X + _thumbRect.Width,
                _trackRect.Y,
                MathF.Max(0f, (_trackRect.X + _trackRect.Width) - (_thumbRect.X + _thumbRect.Width)),
                _trackRect.Height);

            _decreaseRegionRect = IsDirectionReversed ? trackEndRegionRect : trackStartRegionRect;
            _increaseRegionRect = IsDirectionReversed ? trackStartRegionRect : trackEndRegionRect;
            thumb?.Arrange(_thumbRect);
            return;
        }

        _trackRect = CreateCenteredTrackRect(slot, slotWidth, slotHeight, isVertical: false);
        _thumbRect = ComputeSliderThumbRect(_trackRect, slotWidth, slotHeight);
        var startRegionRect = new LayoutRect(_trackRect.X, _trackRect.Y, MathF.Max(0f, _thumbRect.X - _trackRect.X), _trackRect.Height);
        var endRegionRect = new LayoutRect(
            _thumbRect.X + _thumbRect.Width,
            _trackRect.Y,
            MathF.Max(0f, (_trackRect.X + _trackRect.Width) - (_thumbRect.X + _thumbRect.Width)),
            _trackRect.Height);

        var startButtonRect = new LayoutRect(startRegionRect.X, slot.Y, startRegionRect.Width, slotHeight);
        var endButtonRect = new LayoutRect(endRegionRect.X, slot.Y, endRegionRect.Width, slotHeight);
        _decreaseRegionRect = IsDirectionReversed ? endButtonRect : startButtonRect;
        _increaseRegionRect = IsDirectionReversed ? startButtonRect : endButtonRect;

        decreaseButton?.Arrange(_decreaseRegionRect);
        increaseButton?.Arrange(_increaseRegionRect);
        thumb?.Arrange(_thumbRect);
    }

    private LayoutRect CreateCenteredTrackRect(LayoutRect slot, float slotWidth, float slotHeight, bool isVertical)
    {
        if (isVertical)
        {
            var trackWidth = ResolveTrackCrossLength(slotWidth);
            return new LayoutRect(slot.X + ((slotWidth - trackWidth) / 2f), slot.Y, trackWidth, slotHeight);
        }

        var trackHeight = ResolveTrackCrossLength(slotHeight);
        return new LayoutRect(slot.X, slot.Y + ((slotHeight - trackHeight) / 2f), slotWidth, trackHeight);
    }

    private float ResolveTrackCrossLength(float slotCrossLength)
    {
        if (TrackThickness <= 0f || float.IsNaN(TrackThickness))
        {
            return MathF.Max(0f, slotCrossLength);
        }

        return MathF.Min(MathF.Max(0f, slotCrossLength), TrackThickness);
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

    private float ResolveThumbAxisLength()
    {
        return IsViewportSizedThumb
            ? ThumbMinLength
            : MathF.Max(ThumbMinLength, ThumbLength > 0f ? ThumbLength : ThumbMinLength);
    }

    private LayoutRect ComputeThumbRect(LayoutRect trackRect)
    {
        if (trackRect.Width <= 0f || trackRect.Height <= 0f)
        {
            return new LayoutRect(trackRect.X, trackRect.Y, 0f, 0f);
        }

        var trackLength = Orientation == Orientation.Vertical ? trackRect.Height : trackRect.Width;
        var thumbAxisLength = ResolveThumbAxisLength(trackLength);
        var maxTravel = MathF.Max(0f, trackLength - thumbAxisLength);
        var scrollableRange = GetScrollableRange();
        var normalized = scrollableRange <= ValueEpsilon ? 0f : (ClampValue(Value) - Minimum) / scrollableRange;
        var travelFraction = IsDirectionReversed ? 1f - normalized : normalized;
        var thumbTravel = maxTravel * MathF.Max(0f, MathF.Min(1f, travelFraction));

        if (Orientation == Orientation.Vertical)
        {
            return new LayoutRect(trackRect.X, trackRect.Y + thumbTravel, trackRect.Width, thumbAxisLength);
        }

        return new LayoutRect(trackRect.X + thumbTravel, trackRect.Y, thumbAxisLength, trackRect.Height);
    }

    private float ResolveThumbAxisLength(float trackLength)
    {
        if (!IsViewportSizedThumb)
        {
            var explicitLength = ThumbLength > 0f ? ThumbLength : ThumbMinLength;
            return MathF.Min(trackLength, MathF.Max(ThumbMinLength, explicitLength));
        }

        var extent = MathF.Max(0f, Maximum - Minimum);
        if (extent <= ValueEpsilon)
        {
            return trackLength;
        }

        var viewport = MathF.Max(0f, ViewportSize);
        var ratio = viewport > 0f
            ? MathF.Max(MinimumThumbRatio, MathF.Min(1f, viewport / MathF.Max(viewport, extent)))
            : FallbackThumbRatio;
        return MathF.Min(trackLength, MathF.Max(ThumbMinLength, trackLength * ratio));
    }

    private LayoutRect ComputeSliderThumbRect(LayoutRect trackRect, float slotWidth, float slotHeight)
    {
        if (trackRect.Width <= 0f || trackRect.Height <= 0f)
        {
            return new LayoutRect(trackRect.X, trackRect.Y, 0f, 0f);
        }

        var trackLength = Orientation == Orientation.Vertical ? trackRect.Height : trackRect.Width;
        var thumbAxisLength = ResolveThumbAxisLength(trackLength);
        var thumbCrossLength = ResolveSliderThumbCrossLength(Orientation == Orientation.Vertical ? slotWidth : slotHeight);
        var maxTravel = MathF.Max(0f, trackLength - thumbAxisLength);
        var scrollableRange = GetScrollableRange();
        var normalized = scrollableRange <= ValueEpsilon ? 0f : (ClampValue(Value) - Minimum) / scrollableRange;
        var travelFraction = IsDirectionReversed ? 1f - normalized : normalized;
        var thumbTravel = maxTravel * MathF.Max(0f, MathF.Min(1f, travelFraction));

        if (Orientation == Orientation.Vertical)
        {
            return new LayoutRect(
                trackRect.X + ((trackRect.Width - thumbCrossLength) / 2f),
                trackRect.Y + thumbTravel,
                thumbCrossLength,
                thumbAxisLength);
        }

        return new LayoutRect(
            trackRect.X + thumbTravel,
            trackRect.Y + ((trackRect.Height - thumbCrossLength) / 2f),
            thumbAxisLength,
            thumbCrossLength);
    }

    private float ResolveSliderThumbCrossLength(float availableCrossLength)
    {
        var explicitLength = ThumbLength > 0f ? ThumbLength : ThumbMinLength;
        var desiredLength = MathF.Max(ThumbMinLength, explicitLength);
        if (availableCrossLength <= 0f)
        {
            return desiredLength;
        }

        return MathF.Min(availableCrossLength, desiredLength);
    }

    private float GetScrollableRange()
    {
        var extent = MathF.Max(0f, Maximum - Minimum);
        return IsViewportSizedThumb
            ? MathF.Max(0f, extent - MathF.Max(0f, ViewportSize))
            : extent;
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
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(rect.X, rect.Y, border.Left, rect.Height), BorderBrush, Opacity);
        }

        if (border.Right > 0f)
        {
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(rect.X + rect.Width - border.Right, rect.Y, border.Right, rect.Height), BorderBrush, Opacity);
        }

        if (border.Top > 0f)
        {
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(rect.X, rect.Y, rect.Width, border.Top), BorderBrush, Opacity);
        }

        if (border.Bottom > 0f)
        {
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(rect.X, rect.Y + rect.Height - border.Bottom, rect.Width, border.Bottom), BorderBrush, Opacity);
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
