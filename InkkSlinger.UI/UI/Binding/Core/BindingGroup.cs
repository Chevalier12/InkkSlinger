using System;
using System.Collections.Generic;
using System.Globalization;

namespace InkkSlinger;

public sealed class BindingGroup
{
    private readonly List<IBindingExpression> _members = new();
    private readonly List<ValidationError> _validationErrors = new();

    public string? Name { get; set; }

    public IList<ValidationRule> ValidationRules { get; } = new List<ValidationRule>();

    public CultureInfo? Culture { get; set; }

    public IReadOnlyList<ValidationError> ValidationErrors => _validationErrors.AsReadOnly();

    public bool ValidateWithoutUpdate()
    {
        return EvaluateMembers(applySourceUpdates: false);
    }

    public bool CommitEdit()
    {
        return EvaluateMembers(applySourceUpdates: true);
    }

    public void CancelEdit()
    {
        _validationErrors.Clear();

        var visitedTargets = new HashSet<DependencyObject>();
        foreach (var member in _members)
        {
            if (visitedTargets.Add(member.Target))
            {
                Validation.ClearErrors(member.Target, this);
            }
        }
    }

    internal void RegisterExpression(IBindingExpression expression)
    {
        if (_members.Contains(expression))
        {
            return;
        }

        _members.Add(expression);
    }

    internal void UnregisterExpression(IBindingExpression expression)
    {
        _ = _members.Remove(expression);
    }

    private bool EvaluateMembers(bool applySourceUpdates)
    {
        _validationErrors.Clear();
        var culture = Culture ?? CultureInfo.CurrentCulture;
        foreach (var rule in ValidationRules)
        {
            var result = rule.Validate(this, culture);
            if (!result.IsValid)
            {
                _validationErrors.Add(new ValidationError(rule, this, result.ErrorContent));
            }
        }

        var errorsByTarget = new Dictionary<DependencyObject, List<ValidationError>>();
        var memberTargets = new HashSet<DependencyObject>();
        var validationPhaseFailed = false;
        foreach (var member in _members)
        {
            _ = memberTargets.Add(member.Target);
            var memberErrors = new List<ValidationError>();
            var success = member.TryValidateForBindingGroup(memberErrors);

            if (!success && !errorsByTarget.TryGetValue(member.Target, out _))
            {
                errorsByTarget[member.Target] = memberErrors;
            }
            else if (!success)
            {
                errorsByTarget[member.Target].AddRange(memberErrors);
            }

            _validationErrors.AddRange(memberErrors);
            if (!success)
            {
                validationPhaseFailed = true;
            }
        }

        if (validationPhaseFailed || !applySourceUpdates)
        {
            PublishValidationErrors(memberTargets, errorsByTarget);
            return _validationErrors.Count == 0;
        }

        var appliedRollbacks = new List<List<PathSnapshot>>();
        foreach (var member in _members)
        {
            var memberErrors = new List<ValidationError>();
            var snapshots = CaptureSnapshots(member);
            var success = member.TryUpdateSourceForBindingGroup(memberErrors);
            if (success)
            {
                appliedRollbacks.Add(snapshots);
                continue;
            }

            RestoreSnapshots(snapshots);
            for (var i = appliedRollbacks.Count - 1; i >= 0; i--)
            {
                RestoreSnapshots(appliedRollbacks[i]);
            }

            if (!errorsByTarget.TryGetValue(member.Target, out var existing))
            {
                existing = new List<ValidationError>();
                errorsByTarget[member.Target] = existing;
            }

            existing.AddRange(memberErrors);
            _validationErrors.AddRange(memberErrors);
            PublishValidationErrors(memberTargets, errorsByTarget);
            return false;
        }

        PublishValidationErrors(memberTargets, errorsByTarget);
        return _validationErrors.Count == 0;
    }

    private void PublishValidationErrors(
        HashSet<DependencyObject> memberTargets,
        Dictionary<DependencyObject, List<ValidationError>> errorsByTarget)
    {
        foreach (var target in memberTargets)
        {
            var targetErrors = errorsByTarget.TryGetValue(target, out var targetSpecificErrors)
                ? new List<ValidationError>(targetSpecificErrors)
                : new List<ValidationError>();
            targetErrors.AddRange(_validationErrors.FindAll(error => ReferenceEquals(error.BindingInError, this)));

            if (targetErrors.Count == 0)
            {
                Validation.ClearErrors(target, this);
                continue;
            }

            Validation.SetErrors(target, this, targetErrors);
        }

        if (_validationErrors.Count == 0)
        {
            var visitedTargets = new HashSet<DependencyObject>();
            foreach (var member in _members)
            {
                if (visitedTargets.Add(member.Target))
                {
                    Validation.ClearErrors(member.Target, this);
                }
            }
        }
    }

    private static List<PathSnapshot> CaptureSnapshots(IBindingExpression member)
    {
        var snapshots = new List<PathSnapshot>();
        switch (member.Binding)
        {
            case Binding binding:
                AddSnapshot(member.Target, binding, snapshots);
                break;
            case MultiBinding multiBinding:
                foreach (var childBinding in multiBinding.Bindings)
                {
                    AddSnapshot(member.Target, childBinding, snapshots);
                }

                break;
            case PriorityBinding priorityBinding:
                foreach (var childBinding in priorityBinding.Bindings)
                {
                    AddSnapshot(member.Target, childBinding, snapshots);
                }

                break;
        }

        return snapshots;
    }

    private static void AddSnapshot(DependencyObject target, Binding binding, List<PathSnapshot> snapshots)
    {
        if (string.IsNullOrWhiteSpace(binding.Path))
        {
            return;
        }

        var source = BindingExpressionUtilities.ResolveSource(target, binding);
        if (source == null)
        {
            return;
        }

        snapshots.Add(new PathSnapshot(source, binding.Path, BindingExpressionUtilities.ResolvePathValue(source, binding.Path)));
    }

    private static void RestoreSnapshots(List<PathSnapshot> snapshots)
    {
        for (var i = snapshots.Count - 1; i >= 0; i--)
        {
            var snapshot = snapshots[i];
            _ = BindingExpressionUtilities.TrySetPathValue(snapshot.Source, snapshot.Path, snapshot.Value);
        }
    }

    private sealed record PathSnapshot(object Source, string Path, object? Value);
}
