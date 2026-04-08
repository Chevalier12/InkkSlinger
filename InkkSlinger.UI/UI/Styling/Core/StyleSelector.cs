namespace InkkSlinger;

public abstract class StyleSelector
{
    public virtual Style? SelectStyle(object? item, DependencyObject container)
    {
        return null;
    }
}