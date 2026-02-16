using System.Collections.Generic;

namespace InkkSlinger;

public abstract class TriggerBase
{
    private readonly List<Setter> _setters = new();
    private readonly List<TriggerAction> _enterActions = new();
    private readonly List<TriggerAction> _exitActions = new();

    public IList<Setter> Setters => _setters;

    public IList<TriggerAction> EnterActions => _enterActions;

    public IList<TriggerAction> ExitActions => _exitActions;

    public abstract bool IsMatch(DependencyObject target);

    public virtual void Attach(DependencyObject target, System.Action invalidate)
    {
    }

    public virtual void Detach(DependencyObject target)
    {
    }

    public virtual void CollectConditionProperties(ISet<DependencyProperty> conditionProperties)
    {
    }
}
