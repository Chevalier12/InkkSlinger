using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace InkkSlinger;

public sealed class BindingExpression : IDisposable
{
    private readonly DependencyObject _target;
    private readonly DependencyProperty _targetProperty;
    private readonly Binding _binding;

    private readonly List<SourceSubscription> _sourceSubscriptions = new();
    private bool _isUpdatingTarget;
    private bool _isUpdatingSource;

    public BindingExpression(DependencyObject target, DependencyProperty targetProperty, Binding binding)
    {
        _target = target;
        _targetProperty = targetProperty;
        _binding = binding;

        _target.DependencyPropertyChanged += OnTargetDependencyPropertyChanged;

        RebindSourceSubscriptions();
        UpdateTarget();
    }

    public void UpdateTarget()
    {
        _isUpdatingTarget = true;
        try
        {
            var source = ResolveSource();
            if (source == null)
            {
                ApplyTargetValue(_binding.FallbackValue);
                return;
            }

            var value = ResolvePathValue(source, _binding.Path);
            if (value == null)
            {
                value = _binding.TargetNullValue ?? _binding.FallbackValue;
            }

            ApplyTargetValue(value);
        }
        finally
        {
            _isUpdatingTarget = false;
        }
    }

    public void UpdateSource()
    {
        if (_binding.Mode != BindingMode.TwoWay)
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
            TrySetPathValue(source, _binding.Path, targetValue);
        }
        finally
        {
            _isUpdatingSource = false;
        }
    }

    public void Dispose()
    {
        _target.DependencyPropertyChanged -= OnTargetDependencyPropertyChanged;
        DetachSourceSubscriptions();
    }

    public void OnTargetTreeChanged()
    {
        RebindSourceSubscriptions();
        UpdateTarget();
    }

    private object? ResolveSource()
    {
        if (_binding.Source != null)
        {
            return _binding.Source;
        }

        if (!string.IsNullOrWhiteSpace(_binding.ElementName))
        {
            return ResolveElementNameSource(_binding.ElementName);
        }

        if (_binding.RelativeSourceMode != RelativeSourceMode.None)
        {
            return ResolveRelativeSource();
        }

        if (_target is FrameworkElement element)
        {
            return element.DataContext;
        }

        return null;
    }

    private void RebindSourceSubscriptions()
    {
        DetachSourceSubscriptions();

        var source = ResolveSource();
        if (source == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_binding.Path))
        {
            AddSourceSubscription(source, observedPropertyName: null);
            return;
        }

        var segments = GetPathSegments(_binding.Path);
        object? current = source;

        for (var i = 0; i < segments.Length && current != null; i++)
        {
            var observedProperty = segments[i];
            AddSourceSubscription(current, observedProperty);
            current = TryGetPropertyValue(current, observedProperty, out var next) ? next : null;
        }
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

    private object? ResolveElementNameSource(string name)
    {
        if (_target is not FrameworkElement targetElement)
        {
            return null;
        }

        var root = GetElementTreeRoot(targetElement);
        return root?.FindName(name);
    }

    private object? ResolveRelativeSource()
    {
        if (_target is not UIElement targetElement)
        {
            return null;
        }

        if (_binding.RelativeSourceMode == RelativeSourceMode.Self)
        {
            return _target;
        }

        if (_binding.RelativeSourceMode == RelativeSourceMode.TemplatedParent)
        {
            for (var current = targetElement.VisualParent; current != null; current = current.VisualParent)
            {
                if (current is Control control)
                {
                    return control;
                }
            }

            return null;
        }

        if (_binding.RelativeSourceMode == RelativeSourceMode.FindAncestor)
        {
            var ancestorType = _binding.RelativeSourceAncestorType ?? typeof(UIElement);
            var remainingMatches = Math.Max(1, _binding.RelativeSourceAncestorLevel);

            for (var current = targetElement.VisualParent; current != null; current = current.VisualParent)
            {
                if (!ancestorType.IsInstanceOfType(current))
                {
                    continue;
                }

                remainingMatches--;
                if (remainingMatches == 0)
                {
                    return current;
                }
            }
        }

        return null;
    }

    private static FrameworkElement? GetElementTreeRoot(FrameworkElement element)
    {
        UIElement current = element;
        UIElement? next = current.VisualParent ?? current.LogicalParent;
        while (next != null)
        {
            current = next;
            next = current.VisualParent ?? current.LogicalParent;
        }

        return current as FrameworkElement;
    }

    private static object? ResolvePathValue(object source, string path)
    {
        var segments = GetPathSegments(path);
        if (segments.Length == 0)
        {
            return source;
        }

        object? current = source;

        foreach (var segment in segments)
        {
            if (current == null)
            {
                return null;
            }

            var property = current.GetType().GetProperty(segment, BindingFlags.Instance | BindingFlags.Public);
            if (property == null)
            {
                return null;
            }

            current = property.GetValue(current);
        }

        return current;
    }

    private static bool TrySetPathValue(object source, string path, object? value)
    {
        var segments = GetPathSegments(path);
        if (segments.Length == 0)
        {
            return false;
        }

        object? current = source;

        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (current == null)
            {
                return false;
            }

            var navigation = current.GetType().GetProperty(segments[i], BindingFlags.Instance | BindingFlags.Public);
            if (navigation == null)
            {
                return false;
            }

            current = navigation.GetValue(current);
        }

        if (current == null)
        {
            return false;
        }

        var leaf = current.GetType().GetProperty(segments[^1], BindingFlags.Instance | BindingFlags.Public);
        if (leaf == null || !leaf.CanWrite)
        {
            return false;
        }

        var assignable = value == null || leaf.PropertyType.IsInstanceOfType(value);
        if (!assignable)
        {
            return false;
        }

        leaf.SetValue(current, value);
        return true;
    }

    private void OnObservedSourcePropertyChanged(string? observedPropertyName, string? changedPropertyName)
    {
        if (_binding.Mode == BindingMode.OneTime || _isUpdatingSource)
        {
            return;
        }

        if (ShouldReactToObservedPropertyChange(observedPropertyName, changedPropertyName))
        {
            RebindSourceSubscriptions();
            UpdateTarget();
        }
    }

    private void OnObservedSourceDependencyPropertyChanged(string? observedPropertyName, string changedPropertyName)
    {
        if (_binding.Mode == BindingMode.OneTime || _isUpdatingSource)
        {
            return;
        }

        if (ShouldReactToObservedPropertyChange(observedPropertyName, changedPropertyName))
        {
            RebindSourceSubscriptions();
            UpdateTarget();
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
                UpdateTarget();
            }

            return;
        }

        if (_isUpdatingTarget || _binding.Mode != BindingMode.TwoWay)
        {
            return;
        }

        if (_binding.UpdateSourceTrigger == UpdateSourceTrigger.PropertyChanged)
        {
            UpdateSource();
        }
    }

    private static bool ShouldReactToObservedPropertyChange(string? observedPropertyName, string? changedPropertyName)
    {
        if (string.IsNullOrWhiteSpace(observedPropertyName))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(changedPropertyName))
        {
            return true;
        }

        return string.Equals(observedPropertyName, changedPropertyName, StringComparison.Ordinal);
    }

    private static string[] GetPathSegments(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return [];
        }

        return path.Split('.', StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool TryGetPropertyValue(object source, string propertyName, out object? value)
    {
        var property = source.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property == null)
        {
            value = null;
            return false;
        }

        value = property.GetValue(source);
        return true;
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

    private sealed class SourceSubscription
    {
        public INotifyPropertyChanged? Notifier { get; set; }

        public PropertyChangedEventHandler? PropertyChangedHandler { get; set; }

        public DependencyObject? DependencyObject { get; set; }

        public EventHandler<DependencyPropertyChangedEventArgs>? DependencyPropertyChangedHandler { get; set; }

        public bool HasHandlers => PropertyChangedHandler != null || DependencyPropertyChangedHandler != null;
    }
}
