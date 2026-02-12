using System;

namespace InkkSlinger;

public sealed class DataTemplate
{
    public DataTemplate()
        : this((dataItem, _) => dataItem is UIElement element ? element : null)
    {
    }

    public DataTemplate(Func<object?, UIElement?> factory)
        : this((dataItem, _) => factory(dataItem))
    {
    }

    public DataTemplate(Func<object?, FrameworkElement?, UIElement?> factory)
    {
        Factory = factory;
    }

    public Func<object?, FrameworkElement?, UIElement?> Factory { get; }

    public Type? DataType { get; set; }

    public UIElement? Build(object? dataItem, FrameworkElement? resourceScope = null)
    {
        var built = Factory(dataItem, resourceScope);
        if (built is FrameworkElement element)
        {
            element.DataContext = dataItem;
        }

        return built;
    }
}
