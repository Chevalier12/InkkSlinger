using System.Collections.Generic;

namespace InkkSlinger;

public sealed class VisualState
{
    public VisualState()
        : this(string.Empty)
    {
    }

    public VisualState(string name)
    {
        Name = name;
    }

    public string Name { get; set; }

    public IList<Setter> Setters { get; } = new List<Setter>();

    public Storyboard? Storyboard { get; set; }
}

public sealed class VisualStateGroupCollection : List<VisualStateGroup>
{
}

public sealed class VisualStateGroup
{
    private readonly Dictionary<(DependencyObject Target, DependencyProperty Property), object?> _activeValues = new();

    public VisualStateGroup()
        : this(string.Empty)
    {
    }

    public VisualStateGroup(string name)
    {
        Name = name;
    }

    public string Name { get; set; }

    public IList<VisualState> States { get; } = new List<VisualState>();

    public string? CurrentStateName { get; private set; }

    private Storyboard? _activeStoryboard;

    public bool ContainsState(string stateName)
    {
        for (var i = 0; i < States.Count; i++)
        {
            if (string.Equals(States[i].Name, stateName, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public bool GoToState(TriggerActionContext context, string stateName)
    {
        for (var i = 0; i < States.Count; i++)
        {
            var state = States[i];
            if (!string.Equals(state.Name, stateName, System.StringComparison.Ordinal))
            {
                continue;
            }

            ApplySetters(context, state);

            if (context.Scope != null)
            {
                _activeStoryboard?.Remove(context.Scope);
                _activeStoryboard = state.Storyboard;
                _activeStoryboard?.Begin(context.Scope, isControllable: true, HandoffBehavior.SnapshotAndReplace);
            }

            CurrentStateName = stateName;
            return true;
        }

        return false;
    }

    internal void ClearState(FrameworkElement? scope)
    {
        foreach (var pair in _activeValues)
        {
            pair.Key.Target.ClearTemplateTriggerValue(pair.Key.Property);
        }

        _activeValues.Clear();

        if (scope != null)
        {
            _activeStoryboard?.Remove(scope);
        }

        _activeStoryboard = null;
        CurrentStateName = null;
    }

    private void ApplySetters(TriggerActionContext context, VisualState state)
    {
        var desiredValues = new Dictionary<(DependencyObject Target, DependencyProperty Property), object?>();
        for (var i = 0; i < state.Setters.Count; i++)
        {
            var setter = state.Setters[i];
            var target = ResolveTarget(context, setter.TargetName);
            if (target == null)
            {
                continue;
            }

            var value = StyleValueCloneUtility.CloneForAssignment(setter.Value);
            if (value is Freezable freezable && !freezable.IsFrozen)
            {
                freezable.Freeze();
            }

            desiredValues[(target, setter.Property)] = value;
        }

        foreach (var pair in _activeValues)
        {
            if (!desiredValues.ContainsKey(pair.Key))
            {
                pair.Key.Target.ClearTemplateTriggerValue(pair.Key.Property);
            }
        }

        foreach (var pair in desiredValues)
        {
            if (_activeValues.TryGetValue(pair.Key, out var activeValue) && Equals(activeValue, pair.Value))
            {
                continue;
            }

            pair.Key.Target.SetTemplateTriggerValue(pair.Key.Property, pair.Value);
        }

        _activeValues.Clear();
        foreach (var pair in desiredValues)
        {
            _activeValues[pair.Key] = pair.Value;
        }
    }

    private static DependencyObject? ResolveTarget(TriggerActionContext context, string? targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return context.Target;
        }

        return context.ResolveByName?.Invoke(targetName) as DependencyObject;
    }
}
