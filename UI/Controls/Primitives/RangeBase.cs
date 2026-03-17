using System;

namespace InkkSlinger;

public abstract class RangeBase : Control
{
    private const float ValueChangeEpsilon = 0.0001f;

    public static readonly RoutedEvent ValueChangedEvent =
        new(nameof(ValueChanged), RoutingStrategy.Bubble);

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(
            nameof(Minimum),
            typeof(float),
            typeof(RangeBase),
            new FrameworkPropertyMetadata(
                0f,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is not RangeBase rangeBase ||
                        args.OldValue is not float oldMinimum ||
                        args.NewValue is not float newMinimum)
                    {
                        return;
                    }

                    rangeBase.SetValue(MaximumProperty!, rangeBase.Maximum);
                    rangeBase.SetValue(ValueProperty!, rangeBase.Value);
                    rangeBase.OnMinimumChanged(oldMinimum, newMinimum);
                },
                coerceValueCallback: static (dependencyObject, value) =>
                {
                    var numeric = CoerceFiniteFloat(value, 0f);
                    return dependencyObject is RangeBase rangeBase
                        ? rangeBase.CoerceMinimumCore(numeric)
                        : numeric;
                }));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            nameof(Maximum),
            typeof(float),
            typeof(RangeBase),
            new FrameworkPropertyMetadata(
                1f,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is not RangeBase rangeBase ||
                        args.OldValue is not float oldMaximum ||
                        args.NewValue is not float newMaximum)
                    {
                        return;
                    }

                    rangeBase.SetValue(ValueProperty!, rangeBase.Value);
                    rangeBase.OnMaximumChanged(oldMaximum, newMaximum);
                },
                coerceValueCallback: static (dependencyObject, value) =>
                {
                    var numeric = CoerceFiniteFloat(value, 1f);
                    return dependencyObject is RangeBase rangeBase
                        ? rangeBase.CoerceMaximumCore(numeric)
                        : numeric;
                }));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(float),
            typeof(RangeBase),
            new FrameworkPropertyMetadata(
                0f,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is not RangeBase rangeBase ||
                        args.OldValue is not float oldValue ||
                        args.NewValue is not float newValue)
                    {
                        return;
                    }

                    rangeBase.OnValueChanged(oldValue, newValue);
                },
                coerceValueCallback: static (dependencyObject, value) =>
                {
                    var numeric = CoerceFiniteFloat(value, 0f);
                    return dependencyObject is RangeBase rangeBase
                        ? rangeBase.CoerceValueCore(numeric)
                        : numeric;
                }));

    public static readonly DependencyProperty SmallChangeProperty =
        DependencyProperty.Register(
            nameof(SmallChange),
            typeof(float),
            typeof(RangeBase),
            new FrameworkPropertyMetadata(
                0.1f,
                coerceValueCallback: static (dependencyObject, value) =>
                {
                    var numeric = CoerceFiniteFloat(value, 0f);
                    return dependencyObject is RangeBase rangeBase
                        ? rangeBase.CoerceSmallChangeCore(numeric)
                        : numeric;
                }));

    public static readonly DependencyProperty LargeChangeProperty =
        DependencyProperty.Register(
            nameof(LargeChange),
            typeof(float),
            typeof(RangeBase),
            new FrameworkPropertyMetadata(
                1f,
                coerceValueCallback: static (dependencyObject, value) =>
                {
                    var numeric = CoerceFiniteFloat(value, 0f);
                    return dependencyObject is RangeBase rangeBase
                        ? rangeBase.CoerceLargeChangeCore(numeric)
                        : numeric;
                }));

    public event EventHandler<RoutedSimpleEventArgs> ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
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

    public float SmallChange
    {
        get => GetValue<float>(SmallChangeProperty);
        set => SetValue(SmallChangeProperty, value);
    }

    public float LargeChange
    {
        get => GetValue<float>(LargeChangeProperty);
        set => SetValue(LargeChangeProperty, value);
    }

    protected static bool AreClose(float left, float right)
    {
        return MathF.Abs(left - right) <= ValueChangeEpsilon;
    }

    protected static float CoerceFiniteFloat(object? value, float fallback)
    {
        return value is float numeric && float.IsFinite(numeric)
            ? numeric
            : fallback;
    }

    protected float ConstrainToRange(float value)
    {
        if (value < Minimum)
        {
            return Minimum;
        }

        if (value > Maximum)
        {
            return Maximum;
        }

        return value;
    }

    protected virtual float CoerceMinimumCore(float value)
    {
        return value;
    }

    protected virtual float CoerceMaximumCore(float value)
    {
        return value < Minimum ? Minimum : value;
    }

    protected virtual float CoerceValueCore(float value)
    {
        return ConstrainToRange(value);
    }

    protected virtual float CoerceSmallChangeCore(float value)
    {
        return MathF.Max(0f, value);
    }

    protected virtual float CoerceLargeChangeCore(float value)
    {
        return MathF.Max(0f, value);
    }

    protected virtual void OnMinimumChanged(float oldMinimum, float newMinimum)
    {
    }

    protected virtual void OnMaximumChanged(float oldMaximum, float newMaximum)
    {
    }

    protected virtual void OnValueChanged(float oldValue, float newValue)
    {
        if (AreClose(oldValue, newValue))
        {
            return;
        }

        RaiseRoutedEvent(ValueChangedEvent, new RoutedSimpleEventArgs(ValueChangedEvent));
    }

    internal static FrameworkPropertyMetadata CreateDerivedMetadata(
        DependencyProperty property,
        object defaultValue,
        FrameworkPropertyMetadataOptions options)
    {
        return CreateDerivedMetadata(property, defaultValue, options, null, null);
    }

    internal static FrameworkPropertyMetadata CreateDerivedMetadata(
        DependencyProperty property,
        object defaultValue,
        FrameworkPropertyMetadataOptions options,
        PropertyChangedCallback? propertyChangedCallback,
        CoerceValueCallback? coerceValueCallback)
    {
        var baseMetadata = property.GetMetadata(typeof(RangeBase));
        return new FrameworkPropertyMetadata(
            defaultValue,
            options,
            propertyChangedCallback is null
                ? baseMetadata.PropertyChangedCallback
                : (dependencyObject, args) =>
                {
                    baseMetadata.PropertyChangedCallback?.Invoke(dependencyObject, args);
                    propertyChangedCallback(dependencyObject, args);
                },
            coerceValueCallback is null
                ? baseMetadata.CoerceValueCallback
                : (dependencyObject, value) =>
                {
                    var coerced = baseMetadata.CoerceValueCallback?.Invoke(dependencyObject, value) ?? value;
                    return coerceValueCallback(dependencyObject, coerced);
                });
    }
}