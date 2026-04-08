namespace InkkSlinger;

public abstract class TriggerAction
{
    public abstract void Invoke(DependencyObject target);

    internal virtual void Invoke(TriggerActionContext context)
    {
        Invoke(context.Target);
    }
}
