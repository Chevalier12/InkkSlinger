using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace InkkSlinger;

public sealed class BindingExpression : IBindingExpression
{
    private readonly DependencyObject _target;
    private readonly DependencyProperty _targetProperty;
    private readonly Binding _binding;
    private readonly BindingMode _effectiveMode;
    private readonly UpdateSourceTrigger _effectiveUpdateSourceTrigger;

    private readonly List<SourceSubscription> _sourceSubscriptions = new();
    private readonly List<NotifyDataErrorSubscription> _notifyDataErrorSubscriptions = new();
    private readonly List<ValidationError> _persistentValidationErrors = new();

    private bool _isUpdatingTarget;
    private bool _isUpdatingSource;
    private EventHandler<FocusChangedRoutedEventArgs>? _lostFocusHandler;

    public BindingExpression(DependencyObject target, DependencyProperty targetProperty, Binding binding)
    {
        _target = target;
        _targetProperty = targetProperty;
        _binding = binding;
        _effectiveMode = BindingExpressionUtilities.ResolveEffectiveMode(binding, target, targetProperty);
        _effectiveUpdateSourceTrigger = BindingExpressionUtilities.ResolveEffectiveUpdateSourceTrigger(binding, target, targetProperty);

        _target.DependencyPropertyChanged += OnTargetDependencyPropertyChanged;
        AttachLostFocusHandlerIfNeeded();

        RebindSourceSubscriptions();

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

        _isUpdatingTarget = true;
        try
        {
            var source = ResolveSource();
            if (source == null)
            {
                ApplyTargetValue(_binding.FallbackValue);
                PublishValidationErrors(source);
                return;
            }

            var value = BindingExpressionUtilities.ResolvePathValue(source, _binding.Path);
            if (_binding.Converter != null)
            {
                try
                {
                    value = _binding.Converter.Convert(
                        value,
                        _targetProperty.PropertyType,
                        _binding.ConverterParameter,
                        _binding.ConverterCulture ?? CultureInfo.CurrentCulture);
                }
                catch (Exception ex)
                {
                    if (!_binding.ValidatesOnExceptions)
                    {
                        throw;
                    }

                    SetPersistentValidationError(new ValidationError(null, _binding, ex));
                    ApplyTargetValue(_binding.FallbackValue);
                    PublishValidationErrors(source);
                    return;
                }
            }

            if (value == null)
            {
                value = _binding.TargetNullValue ?? _binding.FallbackValue;
            }

            ApplyTargetValue(value);
            PublishValidationErrors(source);
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

        var source = ResolveSource();
        if (source == null)
        {
            return;
        }

        var targetValue = _target.GetValue(_targetProperty);

        _isUpdatingSource = true;
        try
        {
            var candidateValue = targetValue;

            if (_binding.Converter != null)
            {
                try
                {
                    candidateValue = _binding.Converter.ConvertBack(
                        targetValue,
                        ResolveLeafTargetType(source),
                        _binding.ConverterParameter,
                        _binding.ConverterCulture ?? CultureInfo.CurrentCulture);
                }
                catch (Exception ex)
                {
                    if (!_binding.ValidatesOnExceptions)
                    {
                        throw;
                    }

                    SetPersistentValidationError(new ValidationError(null, _binding, ex));
                    PublishValidationErrors(source);
                    return;
                }
            }

            if (!ValidateCandidateValue(candidateValue))
            {
                PublishValidationErrors(source);
                return;
            }

            try
            {
                _ = BindingExpressionUtilities.TrySetPathValue(source, _binding.Path, candidateValue);
            }
            catch (Exception ex)
            {
                if (!_binding.ValidatesOnExceptions)
                {
                    throw;
                }

                SetPersistentValidationError(new ValidationError(null, _binding, ex));
                PublishValidationErrors(source);
                return;
            }

            _persistentValidationErrors.Clear();
            PublishValidationErrors(source);
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
        Validation.ClearErrors(_target, this);
    }

    public void OnTargetTreeChanged()
    {
        RebindSourceSubscriptions();

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

    private object? ResolveSource()
    {
        return BindingExpressionUtilities.ResolveSource(_target, _binding);
    }

    private void RebindSourceSubscriptions()
    {
        DetachSourceSubscriptions();
        DetachNotifyDataErrorSubscriptions();

        var source = ResolveSource();
        if (source == null)
        {
            PublishValidationErrors(source);
            return;
        }

        if (string.IsNullOrWhiteSpace(_binding.Path))
        {
            AddSourceSubscription(source, observedPropertyName: null);
            AddNotifyDataErrorSubscription(source, observedPropertyName: null);
            PublishValidationErrors(source);
            return;
        }

        var segments = BindingExpressionUtilities.GetPathSegments(_binding.Path);
        object? current = source;

        for (var i = 0; i < segments.Length && current != null; i++)
        {
            var observedProperty = segments[i];
            AddSourceSubscription(current, observedProperty);
            AddNotifyDataErrorSubscription(current, observedProperty);
            current = BindingExpressionUtilities.TryGetPropertyValue(current, observedProperty, out var next) ? next : null;
        }

        PublishValidationErrors(source);
    }

    private void DetachSourceSubscriptions()
    {
        foreach (var subscription in _sourceSubscriptions)
        {
            if (subscription.Notifier != null && subscription.PropertyChangedHandler != null)
            {
                subscription.Notifier.PropertyChanged -= subscription.PropertyChangedHandler;
            }

            if (subscription.DependencyObject != null && subscription.DependencyPropertyChangedHandler != null)
            {
                subscription.DependencyObject.DependencyPropertyChanged -= subscription.DependencyPropertyChangedHandler;
            }
        }

        _sourceSubscriptions.Clear();
    }

    private void AddSourceSubscription(object source, string? observedPropertyName)
    {
        var subscription = new SourceSubscription();

        if (source is INotifyPropertyChanged notifier)
        {
            subscription.Notifier = notifier;
            subscription.PropertyChangedHandler = (_, args) => OnObservedSourcePropertyChanged(observedPropertyName, args.PropertyName);
            notifier.PropertyChanged += subscription.PropertyChangedHandler;
        }

        if (source is DependencyObject dependencyObject)
        {
            subscription.DependencyObject = dependencyObject;
            subscription.DependencyPropertyChangedHandler = (_, args) => OnObservedSourceDependencyPropertyChanged(observedPropertyName, args.Property.Name);
            dependencyObject.DependencyPropertyChanged += subscription.DependencyPropertyChangedHandler;
        }

        if (subscription.HasHandlers)
        {
            _sourceSubscriptions.Add(subscription);
        }
    }

    private void AddNotifyDataErrorSubscription(object source, string? observedPropertyName)
    {
        if (!_binding.ValidatesOnNotifyDataErrors || source is not INotifyDataErrorInfo notifier)
        {
            return;
        }

        foreach (var existing in _notifyDataErrorSubscriptions)
        {
            if (ReferenceEquals(existing.Notifier, notifier))
            {
                return;
            }
        }

        EventHandler<DataErrorsChangedEventArgs> handler = (_, args) =>
        {
            if (!BindingExpressionUtilities.ShouldReactToObservedPropertyChange(observedPropertyName, args.PropertyName))
            {
                return;
            }

            PublishValidationErrors(ResolveSource());
        };

        notifier.ErrorsChanged += handler;
        _notifyDataErrorSubscriptions.Add(new NotifyDataErrorSubscription(notifier, handler));
    }

    private void DetachNotifyDataErrorSubscriptions()
    {
        foreach (var subscription in _notifyDataErrorSubscriptions)
        {
            subscription.Notifier.ErrorsChanged -= subscription.Handler;
        }

        _notifyDataErrorSubscriptions.Clear();
    }

    private void OnObservedSourcePropertyChanged(string? observedPropertyName, string? changedPropertyName)
    {
        if (_effectiveMode == BindingMode.OneTime || _isUpdatingSource)
        {
            return;
        }

        if (BindingExpressionUtilities.SupportsSourceToTarget(_effectiveMode) &&
            BindingExpressionUtilities.ShouldReactToObservedPropertyChange(observedPropertyName, changedPropertyName))
        {
            RebindSourceSubscriptions();
            UpdateTarget();
            return;
        }

        if (_binding.ValidatesOnNotifyDataErrors)
        {
            PublishValidationErrors(ResolveSource());
        }
    }

    private void OnObservedSourceDependencyPropertyChanged(string? observedPropertyName, string changedPropertyName)
    {
        if (_effectiveMode == BindingMode.OneTime || _isUpdatingSource)
        {
            return;
        }

        if (BindingExpressionUtilities.SupportsSourceToTarget(_effectiveMode) &&
            BindingExpressionUtilities.ShouldReactToObservedPropertyChange(observedPropertyName, changedPropertyName))
        {
            RebindSourceSubscriptions();
            UpdateTarget();
            return;
        }

        if (_binding.ValidatesOnNotifyDataErrors)
        {
            PublishValidationErrors(ResolveSource());
        }
    }

    private void OnTargetDependencyPropertyChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Property, _targetProperty))
        {
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

    private bool UsesDataContextSource()
    {
        return _binding.Source == null &&
               string.IsNullOrWhiteSpace(_binding.ElementName) &&
               _binding.RelativeSourceMode == RelativeSourceMode.None;
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

    private Type ResolveLeafTargetType(object source)
    {
        if (BindingExpressionUtilities.TryGetPathSourceAndLeafProperty(source, _binding.Path, out var leafSource, out var leafProperty) &&
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

    private bool ValidateCandidateValue(object? candidateValue)
    {
        _persistentValidationErrors.Clear();

        foreach (var validationRule in _binding.ValidationRules)
        {
            var validationResult = validationRule.Validate(candidateValue, _binding.ConverterCulture ?? CultureInfo.CurrentCulture);
            if (validationResult.IsValid)
            {
                continue;
            }

            _persistentValidationErrors.Add(new ValidationError(validationRule, _binding, validationResult.ErrorContent));
        }

        return _persistentValidationErrors.Count == 0;
    }

    private void SetPersistentValidationError(ValidationError validationError)
    {
        _persistentValidationErrors.Clear();
        _persistentValidationErrors.Add(validationError);
    }

    private void PublishValidationErrors(object? source)
    {
        var errors = new List<ValidationError>(_persistentValidationErrors);

        if (source != null)
        {
            if (_binding.ValidatesOnDataErrors)
            {
                errors.AddRange(GetDataErrors(source));
            }

            if (_binding.ValidatesOnNotifyDataErrors)
            {
                errors.AddRange(GetNotifyDataErrors(source));
            }
        }

        if (errors.Count == 0)
        {
            Validation.ClearErrors(_target, this);
            return;
        }

        Validation.SetErrors(_target, this, errors);
    }

    private IEnumerable<ValidationError> GetDataErrors(object source)
    {
        if (source is not IDataErrorInfo dataErrorInfo)
        {
            yield break;
        }

        if (BindingExpressionUtilities.TryGetPathSourceAndLeafProperty(source, _binding.Path, out _, out var leafProperty) &&
            !string.IsNullOrWhiteSpace(leafProperty))
        {
            var propertyError = dataErrorInfo[leafProperty];
            if (!string.IsNullOrWhiteSpace(propertyError))
            {
                yield return new ValidationError(null, _binding, propertyError);
            }
        }

        if (!string.IsNullOrWhiteSpace(dataErrorInfo.Error))
        {
            yield return new ValidationError(null, _binding, dataErrorInfo.Error);
        }
    }

    private IEnumerable<ValidationError> GetNotifyDataErrors(object source)
    {
        _ = BindingExpressionUtilities.TryGetPathSourceAndLeafProperty(source, _binding.Path, out _, out var leafProperty);

        foreach (var subscription in _notifyDataErrorSubscriptions)
        {
            foreach (var error in EnumerateErrors(subscription.Notifier, leafProperty))
            {
                yield return new ValidationError(null, _binding, error);
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

    private sealed class SourceSubscription
    {
        public INotifyPropertyChanged? Notifier { get; set; }

        public PropertyChangedEventHandler? PropertyChangedHandler { get; set; }

        public DependencyObject? DependencyObject { get; set; }

        public EventHandler<DependencyPropertyChangedEventArgs>? DependencyPropertyChangedHandler { get; set; }

        public bool HasHandlers => PropertyChangedHandler != null || DependencyPropertyChangedHandler != null;
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
    }
}
