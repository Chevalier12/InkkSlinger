using System;
using System.Collections.Generic;

namespace InkkSlinger;

public static class SpellCheck
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(SpellCheck),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty CustomDictionariesProperty =
        DependencyProperty.RegisterAttached(
            "CustomDictionaries",
            typeof(IList<Uri>),
            typeof(SpellCheck),
            new FrameworkPropertyMetadata(null));

    public static bool GetIsEnabled(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue<bool>(IsEnabledProperty);
    }

    public static void SetIsEnabled(DependencyObject element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(IsEnabledProperty, value);
    }

    public static IList<Uri> GetCustomDictionaries(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        var dictionaries = element.GetValue<IList<Uri>>(CustomDictionariesProperty);
        if (dictionaries is not null)
        {
            return dictionaries;
        }

        dictionaries = new List<Uri>();
        element.SetValue(CustomDictionariesProperty, dictionaries);
        return dictionaries;
    }
}