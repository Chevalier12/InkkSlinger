using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public sealed class TickBar : FrameworkElement
{
    private const float DefaultTickLength = 4f;
    private const float DefaultThickness = 6f;
    private const float ValueEpsilon = 0.0001f;

    public static readonly DependencyProperty PlacementProperty =
        DependencyProperty.Register(
            nameof(Placement),
            typeof(TickBarPlacement),
            typeof(TickBar),
            new FrameworkPropertyMetadata(TickBarPlacement.Bottom, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(
            nameof(Minimum),
            typeof(float),
            typeof(TickBar),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            nameof(Maximum),
            typeof(float),
            typeof(TickBar),
            new FrameworkPropertyMetadata(10f, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TickFrequencyProperty =
        DependencyProperty.Register(
            nameof(TickFrequency),
            typeof(float),
            typeof(TickBar),
            new FrameworkPropertyMetadata(1f, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ReservedSpaceProperty =
        DependencyProperty.Register(
            nameof(ReservedSpace),
            typeof(float),
            typeof(TickBar),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsDirectionReversedProperty =
        DependencyProperty.Register(
            nameof(IsDirectionReversed),
            typeof(bool),
            typeof(TickBar),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty =
        DependencyProperty.Register(
            nameof(Fill),
            typeof(Color),
            typeof(TickBar),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TicksProperty =
        DependencyProperty.Register(
            nameof(Ticks),
            typeof(DoubleCollection),
            typeof(TickBar),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                static (dependencyObject, args) =>
                {
                    if (dependencyObject is TickBar tickBar)
                    {
                        tickBar.OnTicksCollectionChanged(args.OldValue as DoubleCollection, args.NewValue as DoubleCollection);
                    }
                }));

    public TickBarPlacement Placement
    {
        get => GetValue<TickBarPlacement>(PlacementProperty);
        set => SetValue(PlacementProperty, value);
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

    public float TickFrequency
    {
        get => GetValue<float>(TickFrequencyProperty);
        set => SetValue(TickFrequencyProperty, value);
    }

    public float ReservedSpace
    {
        get => GetValue<float>(ReservedSpaceProperty);
        set => SetValue(ReservedSpaceProperty, value);
    }

    public bool IsDirectionReversed
    {
        get => GetValue<bool>(IsDirectionReversedProperty);
        set => SetValue(IsDirectionReversedProperty, value);
    }

    public Color Fill
    {
        get => GetValue<Color>(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public DoubleCollection Ticks
    {
        get => GetValue<DoubleCollection>(TicksProperty) ?? [];
        set => SetValue(TicksProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        _ = availableSize;
        return Placement is TickBarPlacement.Top or TickBarPlacement.Bottom
            ? new Vector2(MathF.Max(ReservedSpace + 16f, 16f), DefaultThickness)
            : new Vector2(DefaultThickness, MathF.Max(ReservedSpace + 16f, 16f));
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        base.OnRender(spriteBatch);

        if (!IsVisible || Fill.A == 0)
        {
            return;
        }

        foreach (var value in EnumerateTickValues())
        {
            DrawTick(spriteBatch, value);
        }
    }

    private void OnTicksCollectionChanged(DoubleCollection? oldTicks, DoubleCollection? newTicks)
    {
        if (oldTicks != null)
        {
            oldTicks.Changed -= OnTickValuesChanged;
        }

        if (newTicks != null)
        {
            newTicks.Changed += OnTickValuesChanged;
        }

        InvalidateVisual();
    }

    private void OnTickValuesChanged()
    {
        InvalidateVisual();
    }

    private IEnumerable<float> EnumerateTickValues()
    {
        var minimum = Minimum;
        var maximum = Maximum;
        if (maximum < minimum)
        {
            (minimum, maximum) = (maximum, minimum);
        }

        var unique = new List<float>();
        AddTick(unique, minimum);
        AddTick(unique, maximum);

        var ticks = GetValue<DoubleCollection>(TicksProperty);
        if (ticks != null && ticks.Count > 0)
        {
            for (var index = 0; index < ticks.Count; index++)
            {
                AddTick(unique, MathF.Max(minimum, MathF.Min(maximum, (float)ticks[index])));
            }
        }
        else if (TickFrequency > ValueEpsilon)
        {
            var current = minimum + TickFrequency;
            while (current < maximum - ValueEpsilon)
            {
                AddTick(unique, current);
                current += TickFrequency;
            }
        }

        unique.Sort();
        return unique;
    }

    private void DrawTick(SpriteBatch spriteBatch, float value)
    {
        var slot = LayoutSlot;
        var horizontal = Placement is TickBarPlacement.Top or TickBarPlacement.Bottom;
        var axisLength = horizontal ? slot.Width : slot.Height;
        if (axisLength <= 0f)
        {
            return;
        }

        var travel = MathF.Max(0f, axisLength - MathF.Max(0f, ReservedSpace));
        var start = horizontal ? slot.X : slot.Y;
        var centerOffset = MathF.Max(0f, ReservedSpace) / 2f;
        var range = Maximum - Minimum;
        var normalized = MathF.Abs(range) <= ValueEpsilon ? 0f : (value - Minimum) / range;
        normalized = MathF.Max(0f, MathF.Min(1f, normalized));
        var positionFraction = IsDirectionReversed ? 1f - normalized : normalized;
        var coordinate = start + centerOffset + (travel * positionFraction);

        Vector2 tickStart;
        Vector2 tickEnd;
        switch (Placement)
        {
            case TickBarPlacement.Top:
                tickStart = new Vector2(coordinate, slot.Y + slot.Height);
                tickEnd = new Vector2(coordinate, slot.Y + slot.Height - DefaultTickLength);
                break;
            case TickBarPlacement.Bottom:
                tickStart = new Vector2(coordinate, slot.Y);
                tickEnd = new Vector2(coordinate, slot.Y + DefaultTickLength);
                break;
            case TickBarPlacement.Left:
                tickStart = new Vector2(slot.X + slot.Width, coordinate);
                tickEnd = new Vector2(slot.X + slot.Width - DefaultTickLength, coordinate);
                break;
            default:
                tickStart = new Vector2(slot.X, coordinate);
                tickEnd = new Vector2(slot.X + DefaultTickLength, coordinate);
                break;
        }

        UiDrawing.DrawLine(spriteBatch, tickStart, tickEnd, 1f, Fill, Opacity);
    }

    private static void AddTick(List<float> ticks, float value)
    {
        for (var index = 0; index < ticks.Count; index++)
        {
            if (MathF.Abs(ticks[index] - value) <= ValueEpsilon)
            {
                return;
            }
        }

        ticks.Add(value);
    }
}