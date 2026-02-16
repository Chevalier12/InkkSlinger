using System.Collections.Generic;

namespace InkkSlinger;

public sealed class VisualState
{
    public VisualState(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public IList<Setter> Setters { get; } = new List<Setter>();

    public Storyboard? Storyboard { get; set; }
}

public sealed class VisualStateGroup
{
    public VisualStateGroup(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public IList<VisualState> States { get; } = new List<VisualState>();

    public string? CurrentStateName { get; private set; }

    private Storyboard? _activeStoryboard;

    public bool GoToState(DependencyObject target, string stateName)
    {
        foreach (var state in States)
        {
            if (!string.Equals(state.Name, stateName, System.StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var setter in state.Setters)
            {
                target.SetValue(setter.Property, setter.Value);
            }

            if (target is FrameworkElement scope)
            {
                _activeStoryboard?.Remove(scope);
                _activeStoryboard = state.Storyboard;
                _activeStoryboard?.Begin(scope, isControllable: true, HandoffBehavior.SnapshotAndReplace);
            }

            CurrentStateName = stateName;
            return true;
        }

        return false;
    }
}
