using System;
using System.Reflection;

namespace InkkSlinger;

public sealed class PropertyGroupDescription : GroupDescription
{
    public string PropertyName { get; set; } = string.Empty;

    public override object? GroupNameFromItem(object? item)
    {
        if (item == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(PropertyName))
        {
            return item;
        }

        object? current = item;
        var segments = PropertyName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length; i++)
        {
            if (current == null)
            {
                return null;
            }

            var property = current.GetType().GetProperty(
                segments[i],
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property == null)
            {
                return null;
            }

            current = property.GetValue(current);
        }

        return current;
    }
}
