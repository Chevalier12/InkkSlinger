using System;
using System.Globalization;
using System.Linq;

namespace InkkSlinger;

public sealed class DelimitedMultiValueConverter : IMultiValueConverter
{
    public string Separator { get; set; } = "|";

    public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        return string.Join(Separator, values.Select(v => v?.ToString() ?? string.Empty));
    }

    public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        var text = value?.ToString() ?? string.Empty;
        var parts = text.Split(Separator, StringSplitOptions.None);
        var result = new object?[targetTypes.Length];
        for (var i = 0; i < targetTypes.Length; i++)
        {
            result[i] = i < parts.Length ? parts[i] : string.Empty;
        }

        return result;
    }
}
