using System;
using System.Collections.Generic;

namespace InkkSlinger;

internal sealed class TemplateTriggerEngine
{
    private readonly Control _owner;
    private readonly Func<string, UIElement?> _resolveTargetByName;
    private readonly Action _invalidateOwner;
    private readonly List<TriggerBase> _attachedTriggers = new();
    private readonly HashSet<DependencyProperty> _conditionProperties = new();
    private readonly Dictionary<(DependencyObject Target, DependencyProperty Property), object?> _activeTriggerValues = new();
    private readonly Dictionary<TriggerBase, bool> _activeTriggerMatches = new();
    private EventHandler<DependencyPropertyChangedEventArgs>? _ownerChangeHandler;
    private bool _isSubscribed;
    private bool _isApplying;
    private bool _reapplyPending;

    public TemplateTriggerEngine(Control owner, Func<string, UIElement?> resolveTargetByName, Action invalidateOwner)
    {
        _owner = owner;
        _resolveTargetByName = resolveTargetByName;
        _invalidateOwner = invalidateOwner;
    }

    public void Apply(IReadOnlyList<TriggerBase> triggers)
    {
        Clear();

        foreach (var trigger in triggers)
        {
            trigger.CollectConditionProperties(_conditionProperties);
            trigger.Attach(_owner, Reapply);
            _attachedTriggers.Add(trigger);
        }

        if (!_isSubscribed)
        {
            _ownerChangeHandler = (_, args) =>
            {
                if (_conditionProperties.Contains(args.Property))
                {
                    Reapply();
                }
            };
            _owner.DependencyPropertyChanged += _ownerChangeHandler;
            _isSubscribed = true;
        }

        Reapply();
    }

    public void Clear()
    {
        if (_isSubscribed && _ownerChangeHandler != null)
        {
            _owner.DependencyPropertyChanged -= _ownerChangeHandler;
            _isSubscribed = false;
        }

        foreach (var trigger in _attachedTriggers)
        {
            trigger.Detach(_owner);
        }

        _attachedTriggers.Clear();
        _conditionProperties.Clear();
        _activeTriggerMatches.Clear();

        foreach (var pair in _activeTriggerValues.Keys)
        {
            pair.Target.ClearTemplateTriggerValue(pair.Property);
        }

        _activeTriggerValues.Clear();
    }

    private void Reapply()
    {
        if (_isApplying)
        {
            _reapplyPending = true;
            return;
        }

        _isApplying = true;
        try
        {
            do
            {
                _reapplyPending = false;
                var desired = new Dictionary<(DependencyObject Target, DependencyProperty Property), object?>();
                var matches = new Dictionary<TriggerBase, bool>();

                foreach (var trigger in _attachedTriggers)
                {
                    var isMatch = trigger.IsMatch(_owner);
                    matches[trigger] = isMatch;
                    if (!isMatch)
                    {
                        continue;
                    }

                    foreach (var setter in trigger.Setters)
                    {
                        var target = ResolveTarget(setter.TargetName);
                        if (target == null)
                        {
                            continue;
                        }

                        desired[(target, setter.Property)] = setter.Value;
                    }
                }

                foreach (var active in _activeTriggerValues)
                {
                    if (!desired.ContainsKey(active.Key))
                    {
                        active.Key.Target.ClearTemplateTriggerValue(active.Key.Property);
                    }
                }

                foreach (var pair in desired)
                {
                    if (_activeTriggerValues.TryGetValue(pair.Key, out var current) && Equals(current, pair.Value))
                    {
                        continue;
                    }

                    pair.Key.Target.SetTemplateTriggerValue(pair.Key.Property, pair.Value);
                }

                _activeTriggerValues.Clear();
                foreach (var pair in desired)
                {
                    _activeTriggerValues[pair.Key] = pair.Value;
                }

                ApplyActions(matches);
            }
            while (_reapplyPending);
        }
        finally
        {
            _isApplying = false;
            _invalidateOwner();
        }
    }

    private void ApplyActions(IReadOnlyDictionary<TriggerBase, bool> currentMatches)
    {
        foreach (var pair in currentMatches)
        {
            var trigger = pair.Key;
            var isMatch = pair.Value;
            var wasMatch = _activeTriggerMatches.TryGetValue(trigger, out var oldMatch) && oldMatch;
            if (!wasMatch && isMatch)
            {
                InvokeActions(trigger.EnterActions);
            }
            else if (wasMatch && !isMatch)
            {
                InvokeActions(trigger.ExitActions);
            }
        }

        _activeTriggerMatches.Clear();
        foreach (var pair in currentMatches)
        {
            _activeTriggerMatches[pair.Key] = pair.Value;
        }
    }

    private void InvokeActions(IEnumerable<TriggerAction> actions)
    {
        var context = new TriggerActionContext(
            _owner,
            _owner,
            name => ResolveTarget(name));

        foreach (var action in actions)
        {
            action.Invoke(context);
        }
    }

    private DependencyObject? ResolveTarget(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return _owner;
        }

        return _resolveTargetByName(targetName) as DependencyObject;
    }
}
