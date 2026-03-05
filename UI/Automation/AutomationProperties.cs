using System;

namespace InkkSlinger;

public static class AutomationProperties
{
    public static readonly DependencyProperty NameProperty =
        DependencyProperty.RegisterAttached(
            "Name",
            typeof(string),
            typeof(AutomationProperties),
            new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty AutomationIdProperty =
        DependencyProperty.RegisterAttached(
            "AutomationId",
            typeof(string),
            typeof(AutomationProperties),
            new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty HelpTextProperty =
        DependencyProperty.RegisterAttached(
            "HelpText",
            typeof(string),
            typeof(AutomationProperties),
            new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty ItemTypeProperty =
        DependencyProperty.RegisterAttached(
            "ItemType",
            typeof(string),
            typeof(AutomationProperties),
            new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty ItemStatusProperty =
        DependencyProperty.RegisterAttached(
            "ItemStatus",
            typeof(string),
            typeof(AutomationProperties),
            new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty LabeledByProperty =
        DependencyProperty.RegisterAttached(
            "LabeledBy",
            typeof(UIElement),
            typeof(AutomationProperties),
            new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty IsRequiredForFormProperty =
        DependencyProperty.RegisterAttached(
            "IsRequiredForForm",
            typeof(bool),
            typeof(AutomationProperties),
            new FrameworkPropertyMetadata(false));

    public static string GetName(DependencyObject element)
    {
        return element.GetValue<string>(NameProperty) ?? string.Empty;
    }

    public static void SetName(DependencyObject element, string value)
    {
        element.SetValue(NameProperty, value);
    }

    public static string GetAutomationId(DependencyObject element)
    {
        return element.GetValue<string>(AutomationIdProperty) ?? string.Empty;
    }

    public static void SetAutomationId(DependencyObject element, string value)
    {
        element.SetValue(AutomationIdProperty, value);
    }

    public static string GetHelpText(DependencyObject element)
    {
        return element.GetValue<string>(HelpTextProperty) ?? string.Empty;
    }

    public static void SetHelpText(DependencyObject element, string value)
    {
        element.SetValue(HelpTextProperty, value);
    }

    public static string GetItemType(DependencyObject element)
    {
        return element.GetValue<string>(ItemTypeProperty) ?? string.Empty;
    }

    public static void SetItemType(DependencyObject element, string value)
    {
        element.SetValue(ItemTypeProperty, value);
    }

    public static string GetItemStatus(DependencyObject element)
    {
        return element.GetValue<string>(ItemStatusProperty) ?? string.Empty;
    }

    public static void SetItemStatus(DependencyObject element, string value)
    {
        element.SetValue(ItemStatusProperty, value);
    }

    public static UIElement? GetLabeledBy(DependencyObject element)
    {
        return element.GetValue<UIElement>(LabeledByProperty);
    }

    public static void SetLabeledBy(DependencyObject element, UIElement? value)
    {
        element.SetValue(LabeledByProperty, value);
    }

    public static bool GetIsRequiredForForm(DependencyObject element)
    {
        return element.GetValue<bool>(IsRequiredForFormProperty);
    }

    public static void SetIsRequiredForForm(DependencyObject element, bool value)
    {
        element.SetValue(IsRequiredForFormProperty, value);
    }
}
