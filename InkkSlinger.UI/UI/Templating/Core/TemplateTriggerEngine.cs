using System;
using System.Collections.Generic;
using System.Diagnostics;
using InkkSlinger.UI.Telemetry;

namespace InkkSlinger;

internal sealed class TemplateTriggerEngine
{
    private static long s_applyCallCount;
    private static long s_applyTicks;
    private static long s_reapplyCallCount;
    private static long s_reapplyTicks;
    private static long s_triggerMatchCount;
    private static long s_matchedTriggerCount;
    private static long s_triggerMatchTicks;
    private static long s_setterResolveCount;
    private static long s_setterResolveTicks;
    private static long s_setTemplateTriggerValueCount;
    private static long s_setTemplateTriggerValueTicks;
    private static long s_clearTemplateTriggerValueCount;
    private static long s_clearTemplateTriggerValueTicks;
    private static long s_applyActionsTicks;
    private static long s_invokeActionsCount;
    private static long s_invokeActionsTicks;
    private static long s_prewarmStoryboardTicks;
    private static long s_prewarmSetterTicks;

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

    internal static void ResetTelemetryForTests()
    {
        s_applyCallCount = 0;
        s_applyTicks = 0;
        s_reapplyCallCount = 0;
        s_reapplyTicks = 0;
        s_triggerMatchCount = 0;
        s_matchedTriggerCount = 0;
        s_triggerMatchTicks = 0;
        s_setterResolveCount = 0;
        s_setterResolveTicks = 0;
        s_setTemplateTriggerValueCount = 0;
        s_setTemplateTriggerValueTicks = 0;
        s_clearTemplateTriggerValueCount = 0;
        s_clearTemplateTriggerValueTicks = 0;
        s_applyActionsTicks = 0;
        s_invokeActionsCount = 0;
        s_invokeActionsTicks = 0;
        s_prewarmStoryboardTicks = 0;
        s_prewarmSetterTicks = 0;
    }

    internal static TemplateTriggerTelemetrySnapshot GetTelemetrySnapshotForTests()
    {
        return new TemplateTriggerTelemetrySnapshot(
            s_applyCallCount,
            TicksToMilliseconds(s_applyTicks),
            s_reapplyCallCount,
            TicksToMilliseconds(s_reapplyTicks),
            s_triggerMatchCount,
            s_matchedTriggerCount,
            TicksToMilliseconds(s_triggerMatchTicks),
            s_setterResolveCount,
            TicksToMilliseconds(s_setterResolveTicks),
            s_setTemplateTriggerValueCount,
            TicksToMilliseconds(s_setTemplateTriggerValueTicks),
            s_clearTemplateTriggerValueCount,
            TicksToMilliseconds(s_clearTemplateTriggerValueTicks),
            TicksToMilliseconds(s_applyActionsTicks),
            s_invokeActionsCount,
            TicksToMilliseconds(s_invokeActionsTicks),
            TicksToMilliseconds(s_prewarmStoryboardTicks),
            TicksToMilliseconds(s_prewarmSetterTicks));
    }

    public void Apply(IReadOnlyList<TriggerBase> triggers)
    {
        s_applyCallCount++;
        var applyStart = Stopwatch.GetTimestamp();
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

        var prewarmStoryboardStart = Stopwatch.GetTimestamp();
        PrewarmStoryboardMetadata();
        s_prewarmStoryboardTicks += Stopwatch.GetTimestamp() - prewarmStoryboardStart;

        var prewarmSetterStart = Stopwatch.GetTimestamp();
        PrewarmSetterValues();
        s_prewarmSetterTicks += Stopwatch.GetTimestamp() - prewarmSetterStart;

        Reapply();
        s_applyTicks += Stopwatch.GetTimestamp() - applyStart;
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

        s_reapplyCallCount++;
        var reapplyStart = Stopwatch.GetTimestamp();
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
                    var triggerMatchStart = Stopwatch.GetTimestamp();
                    var isMatch = trigger.IsMatch(_owner);
                    s_triggerMatchTicks += Stopwatch.GetTimestamp() - triggerMatchStart;
                    s_triggerMatchCount++;
                    _matchesScratch[trigger] = isMatch;
                    if (!isMatch)
                    {
                        continue;
                    }

                    s_matchedTriggerCount++;

                    foreach (var setter in trigger.Setters)
                    {
                        var setterResolveStart = Stopwatch.GetTimestamp();
                        var target = ResolveTarget(setter.TargetName);
                        if (target == null)
                        {
                            s_setterResolveTicks += Stopwatch.GetTimestamp() - setterResolveStart;
                            continue;
                        }

                        if (_preparedSetterValues.TryGetValue((setter, target), out var preparedValue))
                        {
                            _desiredScratch[(target, setter.Property)] = preparedValue;
                            s_setterResolveCount++;
                            s_setterResolveTicks += Stopwatch.GetTimestamp() - setterResolveStart;
                            continue;
                        }

                        if (!ResourceReferenceResolver.TryResolve(target, setter.Property, setter.Value, out var resolvedValue))
                        {
                            s_setterResolveTicks += Stopwatch.GetTimestamp() - setterResolveStart;
                            continue;
                        }

                        _desiredScratch[(target, setter.Property)] = PrepareSetterAssignmentValue(resolvedValue);
                        s_setterResolveCount++;
                        s_setterResolveTicks += Stopwatch.GetTimestamp() - setterResolveStart;
                    }
                }

                foreach (var active in _activeTriggerValues)
                {
                    if (!_desiredScratch.ContainsKey(active.Key))
                    {
                        var clearStart = Stopwatch.GetTimestamp();
                        active.Key.Target.ClearTemplateTriggerValue(active.Key.Property);
                        s_clearTemplateTriggerValueCount++;
                        s_clearTemplateTriggerValueTicks += Stopwatch.GetTimestamp() - clearStart;
                        TrackRenderInvalidationTarget(_owner, active.Key.Target, active.Key.Property, changedRenderTargets);
                    }
                }

                foreach (var pair in _desiredScratch)
                {
                    if (_activeTriggerValues.TryGetValue(pair.Key, out var current) && Equals(current, pair.Value))
                    {
                        continue;
                    }

                    var setStart = Stopwatch.GetTimestamp();
                    pair.Key.Target.SetTemplateTriggerValue(pair.Key.Property, pair.Value);
                    s_setTemplateTriggerValueCount++;
                    s_setTemplateTriggerValueTicks += Stopwatch.GetTimestamp() - setStart;
                    TrackRenderInvalidationTarget(_owner, pair.Key.Target, pair.Key.Property, changedRenderTargets);
                }

                _activeTriggerValues.Clear();
                foreach (var pair in _desiredScratch)
                {
                    _activeTriggerValues[pair.Key] = pair.Value;
                }

                var applyActionsStart = Stopwatch.GetTimestamp();
                ApplyActions(_matchesScratch);
                s_applyActionsTicks += Stopwatch.GetTimestamp() - applyActionsStart;

                foreach (var changedTarget in changedRenderTargets)
                {
                    UiRoot.Current?.NotifyDirectRenderInvalidation(_owner.ResolveTemplateTriggerInvalidationTarget(changedTarget));
                }
            }
            while (_reapplyPending);
        }
        finally
        {
            s_reapplyTicks += Stopwatch.GetTimestamp() - reapplyStart;
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
        var invokeStart = Stopwatch.GetTimestamp();
        var context = new TriggerActionContext(
            _owner,
            _owner,
            name => ResolveTarget(name));

        foreach (var action in actions)
        {
            s_invokeActionsCount++;
            action.Invoke(context);
        }

        s_invokeActionsTicks += Stopwatch.GetTimestamp() - invokeStart;
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
        Control owner,
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

        changedRenderTargets.Add(ResolveRenderInvalidationTarget(owner, uiElement));
    }

    private static UIElement ResolveRenderInvalidationTarget(Control owner, UIElement target)
    {
        for (var current = target; current != null; current = current.GetInvalidationParent())
        {
            if (ReferenceEquals(current, owner))
            {
                return owner;
            }
        }

        return target;
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

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }

}


