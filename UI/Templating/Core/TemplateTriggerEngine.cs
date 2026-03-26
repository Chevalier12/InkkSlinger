using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace InkkSlinger;

internal sealed class TemplateTriggerEngine
{
    private readonly Control _owner;
    private readonly Func<string, object?> _resolveTargetByName;
    private readonly List<TriggerBase> _attachedTriggers = new();
    private readonly HashSet<DependencyProperty> _conditionProperties = new();
    private readonly Dictionary<(DependencyObject Target, DependencyProperty Property), object?> _activeTriggerValues = new();
    private readonly Dictionary<TriggerBase, bool> _activeTriggerMatches = new();
    private readonly Dictionary<(DependencyObject Target, DependencyProperty Property), object?> _desiredScratch = new();
    private readonly Dictionary<TriggerBase, bool> _matchesScratch = new();
    private readonly Dictionary<string, DependencyObject?> _targetCache = new(StringComparer.Ordinal);
    private readonly Dictionary<(Setter Setter, DependencyObject Target), object?> _preparedSetterValues = new();
    private EventHandler<DependencyPropertyChangedEventArgs>? _ownerChangeHandler;
    private EventHandler? _resourceScopeHandler;
    private bool _isSubscribed;
    private bool _isApplying;
    private bool _reapplyPending;

    public TemplateTriggerEngine(Control owner, Func<string, object?> resolveTargetByName)
    {
        _owner = owner;
        _resolveTargetByName = resolveTargetByName;
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

        if (_resourceScopeHandler == null)
        {
            _resourceScopeHandler = (_, _) =>
            {
                _preparedSetterValues.Clear();
                PrewarmSetterValues();
                Reapply();
            };
            _owner.ResourceScopeInvalidated += _resourceScopeHandler;
        }

        PrewarmStoryboardMetadata();
        PrewarmSetterValues();
        Reapply();
    }

    public void Clear()
    {
        if (_isSubscribed && _ownerChangeHandler != null)
        {
            _owner.DependencyPropertyChanged -= _ownerChangeHandler;
            _isSubscribed = false;
        }

        if (_resourceScopeHandler != null)
        {
            _owner.ResourceScopeInvalidated -= _resourceScopeHandler;
            _resourceScopeHandler = null;
        }

        foreach (var trigger in _attachedTriggers)
        {
            trigger.Detach(_owner);
        }

        _attachedTriggers.Clear();
        _conditionProperties.Clear();
        _activeTriggerMatches.Clear();
        _desiredScratch.Clear();
        _matchesScratch.Clear();
        _targetCache.Clear();
        _preparedSetterValues.Clear();

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
                _desiredScratch.Clear();
                _matchesScratch.Clear();
                var changedRenderTargets = new HashSet<UIElement>();

                foreach (var trigger in _attachedTriggers)
                {
                    var isMatch = trigger.IsMatch(_owner);
                    _matchesScratch[trigger] = isMatch;
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

                        if (_preparedSetterValues.TryGetValue((setter, target), out var preparedValue))
                        {
                            _desiredScratch[(target, setter.Property)] = preparedValue;
                            continue;
                        }

                        if (!ResourceReferenceResolver.TryResolve(target, setter.Property, setter.Value, out var resolvedValue))
                        {
                            continue;
                        }

                        _desiredScratch[(target, setter.Property)] = PrepareSetterAssignmentValue(resolvedValue);
                    }
                }

                foreach (var active in _activeTriggerValues)
                {
                    if (!_desiredScratch.ContainsKey(active.Key))
                    {
                        active.Key.Target.ClearTemplateTriggerValue(active.Key.Property);
                        TrackRenderInvalidationTarget(active.Key.Target, active.Key.Property, changedRenderTargets);
                    }
                }

                foreach (var pair in _desiredScratch)
                {
                    if (_activeTriggerValues.TryGetValue(pair.Key, out var current) && Equals(current, pair.Value))
                    {
                        continue;
                    }

                    pair.Key.Target.SetTemplateTriggerValue(pair.Key.Property, pair.Value);
                    TrackRenderInvalidationTarget(pair.Key.Target, pair.Key.Property, changedRenderTargets);
                }

                _activeTriggerValues.Clear();
                foreach (var pair in _desiredScratch)
                {
                    _activeTriggerValues[pair.Key] = pair.Value;
                }

                ApplyActions(_matchesScratch);

                foreach (var changedTarget in changedRenderTargets)
                {
                    UiRoot.Current?.NotifyDirectRenderInvalidation(changedTarget);
                }
            }
            while (_reapplyPending);
        }
        finally
        {
            _isApplying = false;
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

    private void PrewarmStoryboardMetadata()
    {
        if (_owner is not FrameworkElement scope)
        {
            return;
        }

        var context = new TriggerActionContext(
            _owner,
            scope,
            name => ResolveTarget(name));

        foreach (var trigger in _attachedTriggers)
        {
            PrewarmActions(trigger.EnterActions, context);
            PrewarmActions(trigger.ExitActions, context);
        }
    }

    private static void PrewarmActions(IEnumerable<TriggerAction> actions, TriggerActionContext context)
    {
        foreach (var action in actions)
        {
            if (action is BeginStoryboard beginStoryboard)
            {
                beginStoryboard.PrepareMetadata(context);
                beginStoryboard.WarmResolutionPath(context);
            }
        }
    }

    private void PrewarmSetterValues()
    {
        foreach (var trigger in _attachedTriggers)
        {
            foreach (var setter in trigger.Setters)
            {
                var target = ResolveTarget(setter.TargetName);
                if (target == null)
                {
                    continue;
                }

                if (!ResourceReferenceResolver.TryResolve(target, setter.Property, setter.Value, out var resolvedValue))
                {
                    continue;
                }

                _preparedSetterValues[(setter, target)] = PrepareSetterAssignmentValue(resolvedValue);
            }
        }
    }

    private static object? PrepareSetterAssignmentValue(object? value)
    {
        var prepared = StyleValueCloneUtility.CloneForAssignment(value);
        if (prepared is Freezable freezable && !freezable.IsFrozen)
        {
            freezable.Freeze();
        }

        return prepared;
    }

    private static void TrackRenderInvalidationTarget(
        DependencyObject target,
        DependencyProperty property,
        ISet<UIElement> changedRenderTargets)
    {
        if (target is not UIElement uiElement)
        {
            return;
        }

        var metadata = property.GetMetadata(target);
        if ((metadata.Options & FrameworkPropertyMetadataOptions.AffectsRender) == 0)
        {
            return;
        }

        changedRenderTargets.Add(uiElement);
    }

    private DependencyObject? ResolveTarget(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return _owner;
        }

        if (_targetCache.TryGetValue(targetName, out var cached))
        {
            return cached;
        }

        var resolved = _resolveTargetByName(targetName) as DependencyObject;
        _targetCache[targetName] = resolved;
        return resolved;
    }

}
