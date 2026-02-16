using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class ToggleButton : Button
{
    private static readonly System.Lazy<Style> DefaultToggleButtonStyle = new(BuildDefaultToggleButtonStyle);

    public static readonly RoutedEvent CheckedEvent =
        new(nameof(Checked), RoutingStrategy.Bubble);

    public static readonly RoutedEvent UncheckedEvent =
        new(nameof(Unchecked), RoutingStrategy.Bubble);

    public static readonly RoutedEvent IndeterminateEvent =
        new(nameof(Indeterminate), RoutingStrategy.Bubble);

    public static readonly DependencyProperty IsCheckedProperty =
        DependencyProperty.Register(
            nameof(IsChecked),
            typeof(bool?),
            typeof(ToggleButton),
            new FrameworkPropertyMetadata(
                false as bool?,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is not ToggleButton toggleButton)
                    {
                        return;
                    }

                    toggleButton.OnIsCheckedChanged((bool?)args.NewValue);
                }));

    public static readonly DependencyProperty IsThreeStateProperty =
        DependencyProperty.Register(
            nameof(IsThreeState),
            typeof(bool),
            typeof(ToggleButton),
            new FrameworkPropertyMetadata(false));

    public event System.EventHandler<RoutedSimpleEventArgs> Checked
    {
        add => AddHandler(CheckedEvent, value);
        remove => RemoveHandler(CheckedEvent, value);
    }

    public event System.EventHandler<RoutedSimpleEventArgs> Unchecked
    {
        add => AddHandler(UncheckedEvent, value);
        remove => RemoveHandler(UncheckedEvent, value);
    }

    public event System.EventHandler<RoutedSimpleEventArgs> Indeterminate
    {
        add => AddHandler(IndeterminateEvent, value);
        remove => RemoveHandler(IndeterminateEvent, value);
    }

    public bool IsThreeState
    {
        get => GetValue<bool>(IsThreeStateProperty);
        set => SetValue(IsThreeStateProperty, value);
    }

    public bool? IsChecked
    {
        get => GetValue<bool?>(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    protected virtual void OnToggle()
    {
        IsChecked = IsChecked switch
        {
            true => IsThreeState ? null : false,
            false => true,
            null => false
        };
    }

    protected override void OnClick()
    {
        OnToggle();
        base.OnClick();
    }

    protected virtual void OnIsCheckedChanged(bool? isChecked)
    {
        var routedEvent = isChecked switch
        {
            true => CheckedEvent,
            false => UncheckedEvent,
            null => IndeterminateEvent
        };
        RaiseRoutedEvent(routedEvent, new RoutedSimpleEventArgs(routedEvent));
    }

    protected override Style? GetFallbackStyle()
    {
        return DefaultToggleButtonStyle.Value;
    }

    private static Style BuildDefaultToggleButtonStyle()
    {
        var style = new Style(typeof(ToggleButton));

        var checkedTrigger = new Trigger(IsCheckedProperty, true);
        checkedTrigger.Setters.Add(new Setter(BackgroundProperty, new Color(48, 85, 58)));
        checkedTrigger.Setters.Add(new Setter(BorderBrushProperty, new Color(131, 217, 161)));

        var hoverTrigger = new Trigger(IsMouseOverProperty, true);
        hoverTrigger.Setters.Add(new Setter(BackgroundProperty, new Color(66, 66, 66)));

        var pressedTrigger = new Trigger(IsPressedProperty, true);
        pressedTrigger.Setters.Add(new Setter(BackgroundProperty, new Color(28, 28, 28)));

        var disabledTrigger = new Trigger(IsEnabledProperty, false);
        disabledTrigger.Setters.Add(new Setter(BackgroundProperty, new Color(34, 34, 34)));
        disabledTrigger.Setters.Add(new Setter(ForegroundProperty, new Color(180, 180, 180)));

        style.Triggers.Add(checkedTrigger);
        style.Triggers.Add(hoverTrigger);
        style.Triggers.Add(pressedTrigger);
        style.Triggers.Add(disabledTrigger);

        return style;
    }
}
