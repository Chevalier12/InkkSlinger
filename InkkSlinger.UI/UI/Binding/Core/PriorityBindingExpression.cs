using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace InkkSlinger;

public sealed class PriorityBindingExpression : IBindingExpression
{
    private readonly DependencyObject _target;
    private readonly DependencyProperty _targetProperty;
    private readonly PriorityBinding _priorityBinding;
    private readonly BindingMode _effectiveMode;
    private readonly UpdateSourceTrigger _effectiveUpdateSourceTrigger;
    private readonly List<ChildSubscription> _childSubscriptions = new();
    private readonly List<ValidationError> _persistentValidationErrors = new();
    private EventHandler<FocusChangedRoutedEventArgs>? _lostFocusHandler;
    private bool _isUpdatingTarget;
    private bool _isUpdatingSource;
    private int _activeBindingIndex = -1;
    private BindingGroup? _bindingGroup;

    public PriorityBindingExpression(DependencyObject target, DependencyProperty targetProperty, PriorityBinding priorityBinding)
    {
        _target = target;
        _targetProperty = targetProperty;
        _priorityBinding = priorityBinding;
        _effectiveMode = BindingExpressionUtilities.ResolveEffectiveMode(priorityBinding, target, targetProperty);
        _effectiveUpdateSourceTrigger = BindingExpressionUtilities.ResolveEffectiveUpdateSourceTrigger(priorityBinding, target, targetProperty);

        _target.DependencyPropertyChanged += OnTargetDependencyPropertyChanged;
        AttachLostFocusHandlerIfNeeded();
        RebindSourceSubscriptions();
        RebindBindingGroup();

        if (_effectiveMode == BindingMode.OneWayToSource)
        {
            UpdateSource();
        }
        else
        {
            UpdateTarget();
        }
    }

    public DependencyObject Target => _target;

    public DependencyProperty TargetProperty => _targetProperty;

    public BindingBase Binding => _priorityBinding;

    public void UpdateTarget()
    {
        if (!BindingExpressionUtilities.SupportsSourceToTarget(_effectiveMode))
        {
            return;
        }

        _isUpdatingTarget = true;
        try
        {
            _activeBindingIndex = -1;
            _persistentValidationErrors.Clear();

            for (var i = 0; i < _priorityBinding.Bindings.Count; i++)
            {
                if (!TryResolveBindingValue(_priorityBinding.Bindings[i], out var resolvedValue))
                {
                    continue;
                }

                _activeBindingIndex = i;
                ApplyTargetValue(resolvedValue);
                Validation.ClearErrors(_target, this);
                return;
            }

            ApplyTargetValue(_priorityBinding.FallbackValue);
            _persistentValidationErrors.Add(new ValidationError(null, _priorityBinding, "PriorityBinding could not resolve a value."));
            Validation.SetErrors(_target, this, new List<ValidationError>(_persistentValidationErrors));
        }
        finally
        {
            _isUpdatingTarget = false;
        }
    }

    public void UpdateSource()
    {
        if (!BindingExpressionUtilities.SupportsTargetToSource(_effectiveMode))
        {
            return;
        }

        _isUpdatingSource = true;
        try
        {
            var errors = new List<ValidationError>();
            if (!TryUpdateSourceCore(applySourceUpdates: true, errors))
            {
                Validation.SetErrors(_target, this, errors);
                return;
            }

            Validation.ClearErrors(_target, this);
        }
        finally
        {
            _isUpdatingSource = false;
        }
    }

    public void OnTargetTreeChanged()
    {
        RebindSourceSubscriptions();
        RebindBindingGroup();
        if (BindingExpressionUtilities.SupportsSourceToTarget(_effectiveMode))
        {
            UpdateTarget();
            return;
        }

        if (_effectiveMode == BindingMode.OneWayToSource)
        {
            UpdateSource();
        }
    }

    public bool TryValidateForBindingGroup(List<ValidationError> errors)
    {
        if (!BindingExpressionUtilities.SupportsTargetToSource(_effectiveMode))
        {
            return true;
        }

        return TryUpdateSourceCore(applySourceUpdates: false, errors);
    }

    public bool TryUpdateSourceForBindingGroup(List<ValidationError> errors)
    {
        if (!BindingExpressionUtilities.SupportsTargetToSource(_effectiveMode))
        {
            return true;
        }

        return TryUpdateSourceCore(applySourceUpdates: true, errors);
    }

    public void Dispose()
    {
        _target.DependencyPropertyChanged -= OnTargetDependencyPropertyChanged;
        DetachLostFocusHandler();
        DetachSourceSubscriptions();
        AttachToBindingGroup(null);
        Validation.ClearErrors(_target, this);
    }

    private bool TryUpdateSourceCore(bool applySourceUpdates, List<ValidationError> errors)
    {
        var activeBinding = GetActiveBinding();
        if (activeBinding == null)
        {
            UpdateTarget();
            activeBinding = GetActiveBinding();
        }

        if (activeBinding == null)
        {
            errors.Add(new ValidationError(null, _priorityBinding, "PriorityBinding has no active child binding to update source."));
            return false;
        }

        var source = BindingExpressionUtilities.ResolveSource(_target, activeBinding);
        if (source == null)
        {
            return true;
        }

        object? candidateValue = _target.GetValue(_targetProperty);
        if (activeBinding.Converter != null)
        {
            try
            {
                candidateValue = activeBinding.Converter.ConvertBack(
                    candidateValue,
                    ResolveLeafTargetType(source, activeBinding),
                    activeBinding.ConverterParameter,
                    activeBinding.ConverterCulture ?? CultureInfo.CurrentCulture);
            }
            catch (Exception ex)
            {
                if (!activeBinding.ValidatesOnExceptions)
                {
                    throw;
                }

                errors.Add(BindingExpressionUtilities.CreateExceptionValidationError(
                    activeBinding,
                    this,
                    ex,
                    activeBinding.UpdateSourceExceptionFilter ?? _priorityBinding.UpdateSourceExceptionFilter));
                return false;
            }
        }

        foreach (var validationRule in activeBinding.ValidationRules)
        {
            var validationResult = validationRule.Validate(candidateValue, activeBinding.ConverterCulture ?? CultureInfo.CurrentCulture);
            if (!validationResult.IsValid)
            {
                errors.Add(new ValidationError(validationRule, activeBinding, validationResult.ErrorContent));
            }
        }

        if (errors.Count > 0 || !applySourceUpdates)
        {
            return errors.Count == 0;
        }

        try
        {
            if (!BindingExpressionUtilities.TrySetPathValue(source, activeBinding.Path, candidateValue))
            {
                errors.Add(new ValidationError(null, activeBinding, $"Failed to assign value to path '{activeBinding.Path}'."));
                return false;
            }
        }
        catch (Exception ex)
        {
            if (!activeBinding.ValidatesOnExceptions)
            {
                throw;
            }

            errors.Add(BindingExpressionUtilities.CreateExceptionValidationError(
                activeBinding,
                this,
                ex,
                activeBinding.UpdateSourceExceptionFilter ?? _priorityBinding.UpdateSourceExceptionFilter));
            return false;
        }

        return true;
    }

    private void RebindSourceSubscriptions()
    {
        DetachSourceSubscriptions();

        foreach (var binding in _priorityBinding.Bindings)
        {
            var source = BindingExpressionUtilities.ResolveSource(_target, binding);
            if (source == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(binding.Path))
            {
                AddSourceSubscription(source, null);
                continue;
            }

            var segments = BindingExpressionUtilities.GetPathSegments(binding.Path);
            object? current = source;
            for (var i = 0; i < segments.Length && current != null; i++)
            {
                var observedProperty = segments[i];
                AddSourceSubscription(current, observedProperty);
                current = BindingExpressionUtilities.TryGetPropertyValue(current, observedProperty, out var next) ? next : null;
            }
        }
    }

    private void AddSourceSubscription(object source, string? observedPropertyName)
    {
        if (source is INotifyPropertyChanged notifier)
        {
            PropertyChangedEventHandler handler = (_, args) =>
            {
                if (_effectiveMode == BindingMode.OneTime || _isUpdatingSource)
                {
                    return;
                }

                if (!BindingExpressionUtilities.ShouldReactToObservedPropertyChange(observedPropertyName, args.PropertyName))
                {
                    return;
                }

                RebindSourceSubscriptions();
                UpdateTarget();
            };

            notifier.PropertyChanged += handler;
            _childSubscriptions.Add(new ChildSubscription(notifier, handler));
        }

        if (source is DependencyObject dependencyObject)
        {
            EventHandler<DependencyPropertyChangedEventArgs> handler = (_, args) =>
            {
                if (_effectiveMode == BindingMode.OneTime || _isUpdatingSource)
                {
                    return;
                }

                if (!BindingExpressionUtilities.ShouldReactToObservedPropertyChange(observedPropertyName, args.Property.Name))
                {
                    return;
                }

                RebindSourceSubscriptions();
                UpdateTarget();
            };

            dependencyObject.DependencyPropertyChanged += handler;
            _childSubscriptions.Add(new ChildSubscription(dependencyObject, handler));
        }
    }

    private void DetachSourceSubscriptions()
    {
        foreach (var subscription in _childSubscriptions)
        {
            subscription.Detach();
        }

        _childSubscriptions.Clear();
    }

    private void OnTargetDependencyPropertyChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Property, _targetProperty))
        {
            if (_target is FrameworkElement && ReferenceEquals(e.Property, FrameworkElement.BindingGroupProperty))
            {
                RebindBindingGroup();
            }

            if (UsesDataContextSource() &&
                _target is FrameworkElement &&
                ReferenceEquals(e.Property, FrameworkElement.DataContextProperty))
            {
                RebindSourceSubscriptions();
                if (BindingExpressionUtilities.SupportsSourceToTarget(_effectiveMode))
                {
                    UpdateTarget();
                }
                else if (_effectiveMode == BindingMode.OneWayToSource)
                {
                    UpdateSource();
                }
            }

            return;
        }

        if (_isUpdatingTarget || !BindingExpressionUtilities.SupportsTargetToSource(_effectiveMode))
        {
            return;
        }

        if (_effectiveUpdateSourceTrigger == UpdateSourceTrigger.PropertyChanged)
        {
            UpdateSource();
        }
    }

    private bool UsesDataContextSource()
    {
        foreach (var binding in _priorityBinding.Bindings)
        {
            if (binding.Source != null ||
                !string.IsNullOrWhiteSpace(binding.ElementName) ||
                binding.RelativeSourceMode != RelativeSourceMode.None)
            {
                return false;
            }
        }

        return true;
    }

    private Binding? GetActiveBinding()
    {
        if (_activeBindingIndex >= 0 && _activeBindingIndex < _priorityBinding.Bindings.Count)
        {
            return _priorityBinding.Bindings[_activeBindingIndex];
        }

        return null;
    }

    private bool TryResolveBindingValue(Binding binding, out object? resolvedValue)
    {
        var source = BindingExpressionUtilities.ResolveSource(_target, binding);
        if (source == null)
        {
            resolvedValue = null;
            return false;
        }

        var value = BindingExpressionUtilities.ResolvePathValue(source, binding.Path);
        if (binding.Converter != null)
        {
            try
            {
                value = binding.Converter.Convert(
                    value,
                    _targetProperty.PropertyType,
                    binding.ConverterParameter,
                    binding.ConverterCulture ?? CultureInfo.CurrentCulture);
            }
            catch (Exception ex)
            {
                if (!binding.ValidatesOnExceptions)
                {
                    throw;
                }

                _persistentValidationErrors.Add(BindingExpressionUtilities.CreateExceptionValidationError(
                    binding,
                    this,
                    ex,
                    binding.UpdateSourceExceptionFilter ?? _priorityBinding.UpdateSourceExceptionFilter));
                resolvedValue = null;
                return false;
            }
        }

        if (value == null)
        {
            value = binding.TargetNullValue ?? binding.FallbackValue;
        }

        if (value == null &&
            _targetProperty.PropertyType.IsValueType &&
            Nullable.GetUnderlyingType(_targetProperty.PropertyType) == null)
        {
            resolvedValue = null;
            return false;
        }

        if (value != null && !_targetProperty.PropertyType.IsInstanceOfType(value))
        {
            resolvedValue = null;
            return false;
        }

        resolvedValue = value;
        return true;
    }

    private void ApplyTargetValue(object? value)
    {
        if (value == null &&
            _targetProperty.PropertyType.IsValueType &&
            Nullable.GetUnderlyingType(_targetProperty.PropertyType) == null)
        {
            return;
        }

        var current = _target.GetValue(_targetProperty);
        if (Equals(current, value))
        {
            return;
        }

        _target.SetValue(_targetProperty, value);
    }

    private static Type ResolveLeafTargetType(object source, Binding binding)
    {
        if (BindingExpressionUtilities.TryGetPathSourceAndLeafProperty(source, binding.Path, out var leafSource, out var leafProperty) &&
            leafSource != null &&
            !string.IsNullOrWhiteSpace(leafProperty))
        {
            var property = leafSource.GetType().GetProperty(leafProperty);
            if (property != null)
            {
                return property.PropertyType;
            }
        }

        return typeof(object);
    }

    private void AttachLostFocusHandlerIfNeeded()
    {
        if (_effectiveUpdateSourceTrigger != UpdateSourceTrigger.LostFocus ||
            !BindingExpressionUtilities.SupportsTargetToSource(_effectiveMode) ||
            _target is not UIElement targetElement)
        {
            return;
        }

        _lostFocusHandler = (_, _) =>
        {
            if (!_isUpdatingTarget)
            {
                UpdateSource();
            }
        };

        targetElement.AddHandler(UIElement.LostFocusEvent, _lostFocusHandler);
    }

    private void DetachLostFocusHandler()
    {
        if (_lostFocusHandler == null || _target is not UIElement targetElement)
        {
            return;
        }

        targetElement.RemoveHandler(UIElement.LostFocusEvent, _lostFocusHandler);
        _lostFocusHandler = null;
    }

    private void RebindBindingGroup()
    {
        AttachToBindingGroup(BindingExpressionUtilities.ResolveBindingGroup(_target, _priorityBinding.BindingGroupName));
    }

    private void AttachToBindingGroup(BindingGroup? bindingGroup)
    {
        if (ReferenceEquals(_bindingGroup, bindingGroup))
        {
            return;
        }

        if (_bindingGroup != null)
        {
            _bindingGroup.UnregisterExpression(this);
        }

        _bindingGroup = bindingGroup;
        _bindingGroup?.RegisterExpression(this);
    }

    private sealed class ChildSubscription
    {
        private readonly INotifyPropertyChanged? _notifier;
        private readonly PropertyChangedEventHandler? _propertyChangedHandler;
        private readonly DependencyObject? _dependencyObject;
        private readonly EventHandler<DependencyPropertyChangedEventArgs>? _dependencyPropertyChangedHandler;

        public ChildSubscription(INotifyPropertyChanged notifier, PropertyChangedEventHandler propertyChangedHandler)
        {
            _notifier = notifier;
            _propertyChangedHandler = propertyChangedHandler;
        }

        public ChildSubscription(DependencyObject dependencyObject, EventHandler<DependencyPropertyChangedEventArgs> dependencyPropertyChangedHandler)
        {
            _dependencyObject = dependencyObject;
            _dependencyPropertyChangedHandler = dependencyPropertyChangedHandler;
        }

        public void Detach()
        {
            if (_notifier != null && _propertyChangedHandler != null)
            {
                _notifier.PropertyChanged -= _propertyChangedHandler;
            }

            if (_dependencyObject != null && _dependencyPropertyChangedHandler != null)
            {
                _dependencyObject.DependencyPropertyChanged -= _dependencyPropertyChangedHandler;
            }
        }
    }
}
