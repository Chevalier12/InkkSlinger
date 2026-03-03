using System;
using System.Collections.Generic;

namespace InkkSlinger;

public sealed class MultiTrigger : TriggerBase
{
    private readonly List<Condition> _conditions = new();

    public IList<Condition> Conditions => _conditions;

    public override bool IsMatch(DependencyObject target)
    {
        ValidateConditions();

        for (var i = 0; i < _conditions.Count; i++)
        {
            var condition = _conditions[i];
            var currentValue = target.GetValue(condition.Property!);
            if (!Equals(currentValue, condition.Value))
            {
                return false;
            }
        }

        return true;
    }

    public override void CollectConditionProperties(ISet<DependencyProperty> conditionProperties)
    {
        ValidateConditions();

        for (var i = 0; i < _conditions.Count; i++)
        {
            conditionProperties.Add(_conditions[i].Property!);
        }
    }

    private void ValidateConditions()
    {
        if (_conditions.Count == 0)
        {
            throw new InvalidOperationException("MultiTrigger requires at least one Condition.");
        }

        for (var i = 0; i < _conditions.Count; i++)
        {
            _conditions[i].ValidateForMultiTrigger(i);
        }
    }
}
