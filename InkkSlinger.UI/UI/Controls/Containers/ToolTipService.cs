namespace InkkSlinger;

public static class ToolTipService
{
    public const int DefaultInitialShowDelayMs = 500;
    public const int DefaultBetweenShowDelayMs = 100;
    public const int DefaultShowDurationMs = 5000;

    public static readonly DependencyProperty ToolTipProperty =
        DependencyProperty.RegisterAttached(
            "ToolTip",
            typeof(object),
            typeof(ToolTipService),
            new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(ToolTipService),
            new FrameworkPropertyMetadata(true));

    public static readonly DependencyProperty InitialShowDelayProperty =
        DependencyProperty.RegisterAttached(
            "InitialShowDelay",
            typeof(int),
            typeof(ToolTipService),
            new FrameworkPropertyMetadata(
                DefaultInitialShowDelayMs,
                coerceValueCallback: static (_, value) => value is int delay && delay >= 0
                    ? delay
                    : DefaultInitialShowDelayMs));

    public static readonly DependencyProperty BetweenShowDelayProperty =
        DependencyProperty.RegisterAttached(
            "BetweenShowDelay",
            typeof(int),
            typeof(ToolTipService),
            new FrameworkPropertyMetadata(
                DefaultBetweenShowDelayMs,
                coerceValueCallback: static (_, value) => value is int delay && delay >= 0
                    ? delay
                    : DefaultBetweenShowDelayMs));

    public static readonly DependencyProperty ShowDurationProperty =
        DependencyProperty.RegisterAttached(
            "ShowDuration",
            typeof(int),
            typeof(ToolTipService),
            new FrameworkPropertyMetadata(
                DefaultShowDurationMs,
                coerceValueCallback: static (_, value) => value is int duration && duration > 0
                    ? duration
                    : DefaultShowDurationMs));

    public static object? GetToolTip(UIElement element)
    {
        return element.GetValue<object>(ToolTipProperty);
    }

    public static void SetToolTip(UIElement element, object? value)
    {
        element.SetValue(ToolTipProperty, value);
    }

    public static bool GetIsEnabled(UIElement element)
    {
        return element.GetValue<bool>(IsEnabledProperty);
    }

    public static void SetIsEnabled(UIElement element, bool value)
    {
        element.SetValue(IsEnabledProperty, value);
    }

    public static int GetInitialShowDelay(UIElement element)
    {
        return element.GetValue<int>(InitialShowDelayProperty);
    }

    public static void SetInitialShowDelay(UIElement element, int value)
    {
        element.SetValue(InitialShowDelayProperty, value);
    }

    public static int GetBetweenShowDelay(UIElement element)
    {
        return element.GetValue<int>(BetweenShowDelayProperty);
    }

    public static void SetBetweenShowDelay(UIElement element, int value)
    {
        element.SetValue(BetweenShowDelayProperty, value);
    }

    public static int GetShowDuration(UIElement element)
    {
        return element.GetValue<int>(ShowDurationProperty);
    }

    public static void SetShowDuration(UIElement element, int value)
    {
        element.SetValue(ShowDurationProperty, value);
    }
}
