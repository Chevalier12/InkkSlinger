using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class ScrollBar : Control
{
    private const float ValueEpsilon = 0.01f;
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(Orientation.Vertical, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(
            nameof(Minimum),
            typeof(float),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            nameof(Maximum),
            typeof(float),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(float),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ViewportSizeProperty =
        DependencyProperty.Register(
            nameof(ViewportSize),
            typeof(float),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SmallChangeProperty =
        DependencyProperty.Register(
            nameof(SmallChange),
            typeof(float),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(16f));

    public static readonly DependencyProperty LargeChangeProperty =
        DependencyProperty.Register(
            nameof(LargeChange),
            typeof(float),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(32f));

    public static readonly DependencyProperty ThicknessProperty =
        DependencyProperty.Register(
            nameof(Thickness),
            typeof(float),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(12f, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty =
        DependencyProperty.Register(
            nameof(TrackBrush),
            typeof(Color),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(new Color(42, 42, 42), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThumbBrushProperty =
        DependencyProperty.Register(
            nameof(ThumbBrush),
            typeof(Color),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(new Color(112, 112, 112), FrameworkPropertyMetadataOptions.AffectsRender));

    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetIfChanged(OrientationProperty, value);
    }

    public float Minimum
    {
        get => GetValue<float>(MinimumProperty);
        set => SetIfChanged(MinimumProperty, value);
    }

    public float Maximum
    {
        get => GetValue<float>(MaximumProperty);
        set => SetIfChanged(MaximumProperty, value);
    }

    public float Value
    {
        get => GetValue<float>(ValueProperty);
        set => SetIfChanged(ValueProperty, value);
    }

    public float ViewportSize
    {
        get => GetValue<float>(ViewportSizeProperty);
        set => SetIfChanged(ViewportSizeProperty, value);
    }

    public float SmallChange
    {
        get => GetValue<float>(SmallChangeProperty);
        set => SetIfChanged(SmallChangeProperty, value);
    }

    public float LargeChange
    {
        get => GetValue<float>(LargeChangeProperty);
        set => SetIfChanged(LargeChangeProperty, value);
    }

    public float Thickness
    {
        get => GetValue<float>(ThicknessProperty);
        set => SetIfChanged(ThicknessProperty, value);
    }

    public Color TrackBrush
    {
        get => GetValue<Color>(TrackBrushProperty);
        set => SetIfChanged(TrackBrushProperty, value);
    }

    public Color ThumbBrush
    {
        get => GetValue<Color>(ThumbBrushProperty);
        set => SetIfChanged(ThumbBrushProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var thickness = MathF.Max(8f, Thickness);
        if (Orientation == Orientation.Vertical)
        {
            return new Vector2(thickness, MathF.Max(0f, availableSize.Y));
        }

        return new Vector2(MathF.Max(0f, availableSize.X), thickness);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var track = LayoutSlot;
        if (track.Width <= 0f || track.Height <= 0f)
        {
            return;
        }

        UiDrawing.DrawFilledRect(spriteBatch, track, TrackBrush, Opacity);

        var thumb = GetThumbRect(track);
        if (thumb.Width > 0f && thumb.Height > 0f)
        {
            UiDrawing.DrawFilledRect(spriteBatch, thumb, ThumbBrush, Opacity);
        }
    }

    internal LayoutRect GetThumbRectForInput()
    {
        return GetThumbRect(LayoutSlot);
    }

    internal bool HitTestTrack(Vector2 point)
    {
        var track = LayoutSlot;
        return point.X >= track.X &&
               point.X <= track.X + track.Width &&
               point.Y >= track.Y &&
               point.Y <= track.Y + track.Height;
    }

    internal bool TryHandlePointerDown(Vector2 pointerPosition, out float value, out bool startDrag, out float dragOffset)
    {
        value = Value;
        startDrag = false;
        dragOffset = 0f;

        if (!HitTestTrack(pointerPosition))
        {
            return false;
        }

        var thumb = GetThumbRectForInput();
        var pointerAxis = GetPointerAxis(pointerPosition);
        var thumbStart = GetAxisStart(thumb);
        var thumbEnd = GetAxisEnd(thumb);

        if (pointerAxis >= thumbStart && pointerAxis <= thumbEnd)
        {
            startDrag = true;
            dragOffset = pointerAxis - thumbStart;
            value = CoerceValue(Value);
            return true;
        }

        var next = Value + (pointerAxis < thumbStart ? -MathF.Max(1f, LargeChange) : MathF.Max(1f, LargeChange));
        value = CoerceValue(next);
        return true;
    }

    internal float GetValueFromDragPointer(Vector2 pointerPosition, float dragOffset)
    {
        var track = LayoutSlot;
        var thumb = GetThumbRect(track);
        var range = GetScrollableRange();
        if (range <= ValueEpsilon)
        {
            return Minimum;
        }

        var trackLength = GetAxisLength(track);
        var thumbLength = GetAxisLength(thumb);
        var travel = MathF.Max(1f, trackLength - thumbLength);
        var pointerAxis = GetPointerAxis(pointerPosition);
        var trackStart = GetAxisStart(track);
        var thumbStart = MathF.Max(0f, MathF.Min(travel, pointerAxis - trackStart - dragOffset));
        var normalized = thumbStart / travel;
        return CoerceValue(Minimum + (normalized * range));
    }

    private LayoutRect GetThumbRect(LayoutRect track)
    {
        var extent = MathF.Max(0f, Maximum - Minimum);
        if (extent <= 0.01f)
        {
            return track;
        }

        var viewport = MathF.Max(0f, ViewportSize);
        var range = MathF.Max(0f, extent - viewport);
        var offset = MathF.Max(Minimum, MathF.Min(Maximum, Value)) - Minimum;

        if (Orientation == Orientation.Vertical)
        {
            var trackLength = MathF.Max(1f, track.Height);
            var ratio = viewport > 0f ? MathF.Max(0.05f, MathF.Min(1f, viewport / MathF.Max(viewport, extent))) : 0.1f;
            var thumbLength = MathF.Max(14f, trackLength * ratio);
            var travel = MathF.Max(0f, trackLength - thumbLength);
            var t = range <= 0.01f ? 0f : MathF.Max(0f, MathF.Min(1f, offset / range));
            return new LayoutRect(track.X, track.Y + (travel * t), track.Width, thumbLength);
        }

        var trackWidth = MathF.Max(1f, track.Width);
        var widthRatio = viewport > 0f ? MathF.Max(0.05f, MathF.Min(1f, viewport / MathF.Max(viewport, extent))) : 0.1f;
        var thumbWidth = MathF.Max(14f, trackWidth * widthRatio);
        var horizontalTravel = MathF.Max(0f, trackWidth - thumbWidth);
        var horizontalT = range <= 0.01f ? 0f : MathF.Max(0f, MathF.Min(1f, offset / range));
        return new LayoutRect(track.X + (horizontalTravel * horizontalT), track.Y, thumbWidth, track.Height);
    }

    private float GetScrollableRange()
    {
        var extent = MathF.Max(0f, Maximum - Minimum);
        return MathF.Max(0f, extent - MathF.Max(0f, ViewportSize));
    }

    private float CoerceValue(float value)
    {
        var maxValue = Minimum + GetScrollableRange();
        if (maxValue < Minimum)
        {
            maxValue = Minimum;
        }

        return MathF.Max(Minimum, MathF.Min(maxValue, value));
    }

    private float GetPointerAxis(Vector2 pointerPosition)
    {
        return Orientation == Orientation.Vertical ? pointerPosition.Y : pointerPosition.X;
    }

    private float GetAxisStart(LayoutRect rect)
    {
        return Orientation == Orientation.Vertical ? rect.Y : rect.X;
    }

    private float GetAxisEnd(LayoutRect rect)
    {
        return Orientation == Orientation.Vertical ? rect.Y + rect.Height : rect.X + rect.Width;
    }

    private float GetAxisLength(LayoutRect rect)
    {
        return Orientation == Orientation.Vertical ? rect.Height : rect.Width;
    }

    private void SetIfChanged(DependencyProperty property, float value)
    {
        if (AreClose(GetValue<float>(property), value))
        {
            return;
        }

        SetValue(property, value);
    }

    private void SetIfChanged<T>(DependencyProperty property, T value)
    {
        if (EqualityComparer<T>.Default.Equals(GetValue<T>(property), value))
        {
            return;
        }

        SetValue(property, value);
    }

    private static bool AreClose(float left, float right)
    {
        return MathF.Abs(left - right) <= ValueEpsilon;
    }
}
