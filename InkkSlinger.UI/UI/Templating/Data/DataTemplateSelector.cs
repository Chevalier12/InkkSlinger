namespace InkkSlinger;

public abstract class DataTemplateSelector
{
    public virtual DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        return null;
    }
}
