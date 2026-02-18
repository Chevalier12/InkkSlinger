using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace InkkSlinger;

public sealed class MultiBindingExpression : IBindingExpression
{
    private readonly DependencyObject _target;
    private readonly DependencyProperty _targetProperty;
    private readonly MultiBinding _multiBinding;
    private readonly BindingMode _effectiveMode;
    private readonly UpdateSourceTrigger _effectiveUpdateSourceTrigger;
    private readonly List<ChildSubscription> _childSubscriptions = new();
    private readonly List<NotifyDataErrorSubscription> _notifyDataErrorSubscriptions = new();
    private readonly List<ValidationError> _persistentValidationErrors = new();

    private bool _isUpdatingTarget;
    private bool _isUpdatingSource;
    private EventHandler<FocusChangedRoutedEventArgs>? _lostFocusHandler;
    private BindingGroup? _bindingGroup;

    public DependencyObject Target => _target;

    public DependencyProperty TargetProperty => _targetProperty;

    public BindingBase Binding => _multiBinding;

    public MultiBindingExpression(DependencyObject target, DependencyProperty targetProperty, MultiBinding multiBinding)
    {
        _target = target;
        _targetProperty = targetProperty;
        _multiBinding = multiBinding;
        _effectiveMode = BindingExpressionUtilities.ResolveEffectiveMode(multiBinding, target, targetProperty);
        _effectiveUpdateSourceTrigger = BindingExpressionUtilities.ResolveEffectiveUpdateSourceTrigger(multiBinding, target, targetProperty);

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

    public void UpdateTarget()
    {
        if (!BindingExpressionUtilities.SupportsSourceToTarget(_effectiveMode))
        {
            return;
        }

        if (_multiBinding.Converter == null)
        {
            SetPersistentValidationError(new ValidationError(null, _multiBinding, "MultiBinding requires a Converter."));
            PublishValidationErrors();
            return;
        }

        _isUpdatingTarget = true;
        try
        {
            var values = new object?[_multiBinding.Bindings.Count];
            for (var i = 0; i < _multiBinding.Bindings.Count; i++)
            {
                var childBinding = _multiBinding.Bindings[i];
                var source = BindingExpressionUtilities.ResolveSource(_target, childBinding);
                if (source == null)
                {
                    values[i] = childBinding.FallbackValue;
                    continue;
                }

                var value = BindingExpressionUtilities.ResolvePathValue(source, childBinding.Path);
                if (value == null)
                {
                    value = childBinding.TargetNullValue ?? childBinding.FallbackValue;
                }

                values[i] = value;
            }

            object? converted;
            try
            {
                converted = _multiBinding.Converter.Convert(
                    values,
                    _targetProperty.PropertyType,
                    _multiBinding.ConverterParameter,
                    _multiBinding.ConverterCulture ?? CultureInfo.CurrentCulture);
            }
            catch (Exception ex)
            {
                if (!_multiBinding.ValidatesOnExceptions)
                {
                    throw;
                }

                SetPersistentValidationError(
                    BindingExpressionUtilities.CreateExceptionValidationError(
                        _multiBinding,
                        this,
                        ex,
                        _multiBinding.UpdateSourceExceptionFilter));
                ApplyTargetValue(_multiBinding.FallbackValue);
                PublishValidationErrors();
                return;
            }

            if (converted == null)
            {
                converted = _multiBinding.TargetNullValue ?? _multiBinding.FallbackValue;
            }

            _persistentValidationErrors.Clear();
            ApplyTargetValue(converted);
            PublishValidationErrors();
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

        if (_multiBinding.Converter == null)
        {
            SetPersistentValidationError(new ValidationError(null, _multiBinding, "MultiBinding requires a Converter."));
            PublishValidationErrors();
            return;
        }

        var targetValue = _target.GetValue(_targetProperty);

        _isUpdatingSource = true;
        try
        {
            _persistentValidationErrors.Clear();
            foreach (var validationRule in _multiBinding.ValidationRules)
            {
                var validationResult = validationRule.Validate(targetValue, _multiBinding.ConverterCulture ?? CultureInfo.CurrentCulture);
                if (!validationResult.IsValid)
                {
                    _persistentValidationErrors.Add(new ValidationError(validationRule, _multiBinding, validationResult.ErrorContent));
                }
            }

            if (_persistentValidationErrors.Count > 0)
            {
                PublishValidationErrors();
                return;
            }

            var targetTypes = new Type[_multiBinding.Bindings.Count];
            for (var i = 0; i < _multiBinding.Bindings.Count; i++)
            {
                targetTypes[i] = ResolveLeafTargetType(_multiBinding.Bindings[i]);
            }

            object?[] convertedBack;
            try
            {
                convertedBack = _multiBinding.Converter.ConvertBack(
                    targetValue,
                    targetTypes,
                    _multiBinding.ConverterParameter,
                    _multiBinding.ConverterCulture ?? CultureInfo.CurrentCulture);
            }
            catch (Exception ex)
            {
                if (!_multiBinding.ValidatesOnExceptions)
                {
                    throw;
                }

                SetPersistentValidationError(
                    BindingExpressionUtilities.CreateExceptionValidationError(
                        _multiBinding,
                        this,
                        ex,
                        _multiBinding.UpdateSourceExceptionFilter));
                PublishValidationErrors();
                return;
            }

            if (convertedBack.Length != _multiBinding.Bindings.Count)
            {
                SetPersistentValidationError(new ValidationError(null, _multiBinding, "ConvertBack result count does not match child binding count."));
                PublishValidationErrors();
                return;
            }

            for (var i = 0; i < _multiBinding.Bindings.Count; i++)
            {
                var childBinding = _multiBinding.Bindings[i];
                var source = BindingExpressionUtilities.ResolveSource(_target, childBinding);
                if (source == null)
                {
                    continue;
                }

                if (BindingExpressionUtilities.TrySetPathValue(source, childBinding.Path, convertedBack[i]))
                {
                    continue;
                }

                SetPersistentValidationError(new ValidationError(
                    null,
                    _multiBinding,
                    $"Failed to assign ConvertBack value to child binding path '{childBinding.Path}'."));
                PublishValidationErrors();
                return;
            }

            _persistentValidationErrors.Clear();
            PublishValidationErrors();
        }
        finally
        {
            _isUpdatingSource = false;
        }
    }

    public void Dispose()
    {
        _target.DependencyPropertyChanged -= OnTargetDependencyPropertyChanged;
        DetachLostFocusHandler();
        DetachSourceSubscriptions();
        DetachNotifyDataErrorSubscriptions();
        AttachToBindingGroup(null);
        Validation.ClearErrors(_target, this);
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

    private void RebindSourceSubscriptions()
    {
        DetachSourceSubscriptions();
        DetachNotifyDataErrorSubscriptions();

        foreach (var childBinding in _multiBinding.Bindings)
        {
            var source = BindingExpressionUtilities.ResolveSource(_target, childBinding);
            if (source == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(childBinding.Path))
            {
                AddSourceSubscription(source, null);
                AddNotifyDataErrorSubscription(source, null);
                continue;
            }

            var segments = BindingExpressionUtilities.GetPathSegments(childBinding.Path);
            object? current = source;

            for (var i = 0; i < segments.Length && current != null; i++)
            {
                var observedProperty = segments[i];
                AddSourceSubscription(current, observedProperty);
                AddNotifyDataErrorSubscription(current, observedProperty);
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
        foreach (var childSubscription in _childSubscriptions)
        {
            childSubscription.Detach();
        }

        _childSubscriptions.Clear();
    }

    private void AddNotifyDataErrorSubscription(object source, string? observedPropertyName)
    {
        if (!_multiBinding.ValidatesOnNotifyDataErrors || source is not INotifyDataErrorInfo notifier)
        {
            return;
        }

        var normalizedObservedProperty = string.IsNullOrWhiteSpace(observedPropertyName)
            ? null
            : observedPropertyName;

        foreach (var existing in _notifyDataErrorSubscriptions)
        {
            if (ReferenceEquals(existing.Notifier, notifier))
            {
                existing.AddObservedProperty(normalizedObservedProperty);
                return;
            }
        }

        NotifyDataErrorSubscription? subscription = null;
        EventHandler<DataErrorsChangedEventArgs> handler = (_, args) =>
        {
            if (subscription == null || !subscription.ShouldReact(args.PropertyName))
            {
                return;
            }

            PublishValidationErrors();
        };

        subscription = new NotifyDataErrorSubscription(notifier, handler);
        subscription.AddObservedProperty(normalizedObservedProperty);
        notifier.ErrorsChanged += handler;
        _notifyDataErrorSubscriptions.Add(subscription);
    }

    private void DetachNotifyDataErrorSubscriptions()
    {
        foreach (var subscription in _notifyDataErrorSubscriptions)
        {
            subscription.Notifier.ErrorsChanged -= subscription.Handler;
        }

        _notifyDataErrorSubscriptions.Clear();
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

    private bool UsesDataContextSource()
    {
        foreach (var binding in _multiBinding.Bindings)
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

    private Type ResolveLeafTargetType(Binding binding)
    {
        var source = BindingExpressionUtilities.ResolveSource(_target, binding);
        if (source != null &&
            BindingExpressionUtilities.TryGetPathSourceAndLeafProperty(source, binding.Path, out var leafSource, out var leafProperty) &&
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

    private void SetPersistentValidationError(ValidationError validationError)
    {
        _persistentValidationErrors.Clear();
        _persistentValidationErrors.Add(validationError);
    }

    private void PublishValidationErrors()
    {
        var errors = new List<ValidationError>(_persistentValidationErrors);
        errors.AddRange(GetSourceValidationErrors());

        if (errors.Count == 0)
        {
            Validation.ClearErrors(_target, this);
            return;
        }

        Validation.SetErrors(_target, this, errors);
    }

    private IEnumerable<ValidationError> GetSourceValidationErrors()
    {
        foreach (var binding in _multiBinding.Bindings)
        {
            var source = BindingExpressionUtilities.ResolveSource(_target, binding);
            if (source == null)
            {
                continue;
            }

            if (_multiBinding.ValidatesOnDataErrors && source is IDataErrorInfo dataErrorInfo)
            {
                if (BindingExpressionUtilities.TryGetPathSourceAndLeafProperty(source, binding.Path, out _, out var leafProperty) &&
                    !string.IsNullOrWhiteSpace(leafProperty))
                {
                    var propertyError = dataErrorInfo[leafProperty];
                    if (!string.IsNullOrWhiteSpace(propertyError))
                    {
                        yield return new ValidationError(null, _multiBinding, propertyError);
                    }
                }

                if (!string.IsNullOrWhiteSpace(dataErrorInfo.Error))
                {
                    yield return new ValidationError(null, _multiBinding, dataErrorInfo.Error);
                }
            }

            if (!_multiBinding.ValidatesOnNotifyDataErrors)
            {
                continue;
            }

            _ = BindingExpressionUtilities.TryGetPathSourceAndLeafProperty(source, binding.Path, out _, out var notifyLeafProperty);
            foreach (var subscription in _notifyDataErrorSubscriptions)
            {
                foreach (var error in EnumerateErrors(subscription.Notifier, notifyLeafProperty))
                {
                    yield return new ValidationError(null, _multiBinding, error);
                }
            }
        }
    }

    private static IEnumerable<object?> EnumerateErrors(INotifyDataErrorInfo notifier, string? leafProperty)
    {
        if (!string.IsNullOrWhiteSpace(leafProperty))
        {
            foreach (var error in EnumerateErrors(notifier.GetErrors(leafProperty)))
            {
                yield return error;
            }
        }

        foreach (var error in EnumerateErrors(notifier.GetErrors(string.Empty)))
        {
            yield return error;
        }
    }

    private static IEnumerable<object?> EnumerateErrors(IEnumerable? errors)
    {
        if (errors == null)
        {
            yield break;
        }

        foreach (var error in errors)
        {
            yield return error;
        }
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

    private sealed class NotifyDataErrorSubscription
    {
        public NotifyDataErrorSubscription(INotifyDataErrorInfo notifier, EventHandler<DataErrorsChangedEventArgs> handler)
        {
            Notifier = notifier;
            Handler = handler;
        }

        public INotifyDataErrorInfo Notifier { get; }

        public EventHandler<DataErrorsChangedEventArgs> Handler { get; }

        private HashSet<string?> ObservedProperties { get; } = new();

        public void AddObservedProperty(string? observedPropertyName)
        {
            ObservedProperties.Add(observedPropertyName);
        }

        public bool ShouldReact(string? changedPropertyName)
        {
            foreach (var observedPropertyName in ObservedProperties)
            {
                if (BindingExpressionUtilities.ShouldReactToObservedPropertyChange(observedPropertyName, changedPropertyName))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private bool TryUpdateSourceCore(bool applySourceUpdates, List<ValidationError> errors)
    {
        if (_multiBinding.Converter == null)
        {
            errors.Add(new ValidationError(null, _multiBinding, "MultiBinding requires a Converter."));
            return false;
        }

        var targetValue = _target.GetValue(_targetProperty);
        foreach (var validationRule in _multiBinding.ValidationRules)
        {
            var validationResult = validationRule.Validate(targetValue, _multiBinding.ConverterCulture ?? CultureInfo.CurrentCulture);
            if (!validationResult.IsValid)
            {
                errors.Add(new ValidationError(validationRule, _multiBinding, validationResult.ErrorContent));
            }
        }

        if (errors.Count > 0)
        {
            return false;
        }

        var targetTypes = new Type[_multiBinding.Bindings.Count];
        for (var i = 0; i < _multiBinding.Bindings.Count; i++)
        {
            targetTypes[i] = ResolveLeafTargetType(_multiBinding.Bindings[i]);
        }

        object?[] convertedBack;
        try
        {
            convertedBack = _multiBinding.Converter.ConvertBack(
                targetValue,
                targetTypes,
                _multiBinding.ConverterParameter,
                _multiBinding.ConverterCulture ?? CultureInfo.CurrentCulture);
        }
        catch (Exception ex)
        {
            if (!_multiBinding.ValidatesOnExceptions)
            {
                throw;
            }

            errors.Add(BindingExpressionUtilities.CreateExceptionValidationError(
                _multiBinding,
                this,
                ex,
                _multiBinding.UpdateSourceExceptionFilter));
            return false;
        }

        if (convertedBack.Length != _multiBinding.Bindings.Count)
        {
            errors.Add(new ValidationError(null, _multiBinding, "ConvertBack result count does not match child binding count."));
            return false;
        }

        if (!applySourceUpdates)
        {
            return true;
        }

        for (var i = 0; i < _multiBinding.Bindings.Count; i++)
        {
            var childBinding = _multiBinding.Bindings[i];
            var source = BindingExpressionUtilities.ResolveSource(_target, childBinding);
            if (source == null)
            {
                continue;
            }

            if (BindingExpressionUtilities.TrySetPathValue(source, childBinding.Path, convertedBack[i]))
            {
                continue;
            }

            errors.Add(new ValidationError(
                null,
                _multiBinding,
                $"Failed to assign ConvertBack value to child binding path '{childBinding.Path}'."));
            return false;
        }

        return true;
    }

    private void RebindBindingGroup()
    {
        AttachToBindingGroup(BindingExpressionUtilities.ResolveBindingGroup(_target, _multiBinding.BindingGroupName));
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
}
