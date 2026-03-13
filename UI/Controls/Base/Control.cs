using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class Control : FrameworkElement, ICommandSource
{
    private static int _measureTemplateApplyAttemptCount;
    public static readonly DependencyProperty DefaultStyleKeyProperty =
        DependencyProperty.Register(nameof(DefaultStyleKey), typeof(System.Type), typeof(Control), new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty TemplateProperty =
        DependencyProperty.Register(
            nameof(Template),
            typeof(ControlTemplate),
            typeof(Control),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    // Parse-first compatibility shims for theme styles targeting controls that do not
    // define these dependency properties yet.
    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(Control),
            new FrameworkPropertyMetadata(Color.Transparent));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(Control),
            new FrameworkPropertyMetadata(Color.White));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(Control),
            new FrameworkPropertyMetadata(Color.Transparent));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(Thickness),
            typeof(Control),
            new FrameworkPropertyMetadata(Thickness.Empty));

    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(Control),
            new FrameworkPropertyMetadata(Thickness.Empty));

    public static readonly DependencyProperty IsMouseOverProperty =
        DependencyProperty.Register(
            nameof(IsMouseOver),
            typeof(bool),
            typeof(Control),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty IsPressedProperty =
        DependencyProperty.Register(
            nameof(IsPressed),
            typeof(bool),
            typeof(Control),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty IsFocusedProperty =
        DependencyProperty.Register(
            nameof(IsFocused),
            typeof(bool),
            typeof(Control),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(
            nameof(IsSelected),
            typeof(bool),
            typeof(Control),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty IsCheckedProperty =
        DependencyProperty.Register(
            nameof(IsChecked),
            typeof(bool),
            typeof(Control),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(System.Windows.Input.ICommand), typeof(Control), new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(Control), new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty CommandTargetProperty =
        DependencyProperty.Register(nameof(CommandTarget), typeof(UIElement), typeof(Control), new FrameworkPropertyMetadata(null));

    private UIElement? _templateRoot;
    private readonly Dictionary<string, UIElement> _namedTemplateChildren = new(StringComparer.Ordinal);
    private readonly List<(DependencyProperty SourceProperty, EventHandler<DependencyPropertyChangedEventArgs> Handler)> _templateBindingHandlers = new();
    private readonly List<FrameworkElement> _styleResourceAncestors = new();
    private readonly TemplateTriggerEngine _templateTriggerEngine;
    private System.Windows.Input.ICommand? _subscribedCommand;
    private object? _storedIsEnabledLocalValue = DependencyObject.UnsetValue;
    private bool _isCommandDisablingIsEnabled;
    private bool _isUpdatingIsEnabled;
    private bool _isApplyingImplicitStyle;
    private Style? _activeImplicitStyle;

    public Control()
    {
        DefaultStyleKey = GetType();
        Resources.Changed += OnResourceScopeChanged;
        UiApplication.Current.Resources.Changed += OnResourceScopeChanged;
        _templateTriggerEngine = new TemplateTriggerEngine(this, GetTemplateChild, InvalidateVisual);
    }

    public Type? DefaultStyleKey
    {
        get => GetValue<Type>(DefaultStyleKeyProperty);
        set => SetValue(DefaultStyleKeyProperty, value);
    }

    public ControlTemplate? Template
    {
        get => GetValue<ControlTemplate>(TemplateProperty);
        set => SetValue(TemplateProperty, value);
    }

    public Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Color Foreground
    {
        get => GetValue<Color>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public Color BorderBrush
    {
        get => GetValue<Color>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public Thickness BorderThickness
    {
        get => GetValue<Thickness>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public Thickness Padding
    {
        get => GetValue<Thickness>(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    public bool IsMouseOver
    {
        get => GetValue<bool>(IsMouseOverProperty);
        set => SetValue(IsMouseOverProperty, value);
    }

    public bool IsPressed
    {
        get => GetValue<bool>(IsPressedProperty);
        set => SetValue(IsPressedProperty, value);
    }

    public bool IsFocused
    {
        get => GetValue<bool>(IsFocusedProperty);
        set => SetValue(IsFocusedProperty, value);
    }

    public bool IsSelected
    {
        get => GetValue<bool>(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public bool IsChecked
    {
        get => GetValue<bool>(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public System.Windows.Input.ICommand? Command
    {
        get => GetValue<System.Windows.Input.ICommand>(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public UIElement? CommandTarget
    {
        get => GetValue<UIElement>(CommandTargetProperty);
        set => SetValue(CommandTargetProperty, value);
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        if (_templateRoot != null)
        {
            yield return _templateRoot;
        }
    }

    internal override int GetVisualChildCountForTraversal()
    {
        return _templateRoot != null ? 1 : 0;
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        if (index == 0 && _templateRoot != null)
        {
            return _templateRoot;
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    public virtual void OnApplyTemplate()
    {
    }

    protected UIElement? GetTemplateChild(string name)
    {
        return _namedTemplateChildren.TryGetValue(name, out var element) ? element : null;
    }

    protected bool HasTemplateRoot => _templateRoot != null;

    public bool ApplyTemplate()
    {
        ClearTemplateBindings();
        _templateTriggerEngine.Clear();

        if (Template == null)
        {
            ClearTemplateTree();
            return false;
        }

        if (Template.TargetType != null && !Template.TargetType.IsInstanceOfType(this))
        {
            throw new InvalidOperationException(
                $"ControlTemplate target type '{Template.TargetType.Name}' is not compatible with '{GetType().Name}'.");
        }

        var built = Template.Build(this);
        if (built == null)
        {
            ClearTemplateTree();
            return false;
        }

        SetTemplateTree(built);
        ApplyTemplateBindings();
        ApplyTemplateTriggers();
        ValidateTemplateParts();

        OnApplyTemplate();
        return true;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        if (_templateRoot == null)
        {
            if (Template == null)
            {
                return Vector2.Zero;
            }

            _measureTemplateApplyAttemptCount++;
            ApplyTemplate();
        }

        if (_templateRoot is FrameworkElement element)
        {
            element.Measure(availableSize);
            return element.DesiredSize;
        }

        return Vector2.Zero;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (_templateRoot is FrameworkElement element)
        {
            element.Arrange(new LayoutRect(LayoutSlot.X, LayoutSlot.Y, finalSize.X, finalSize.Y));
        }

        return finalSize;
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (args.Property == StyleProperty && !_isApplyingImplicitStyle)
        {
            _activeImplicitStyle = null;
        }

        if (args.Property == TemplateProperty)
        {
            ApplyTemplate();
        }

        if (args.Property == CommandProperty)
        {
            RefreshCommandSubscriptions(args.OldValue as System.Windows.Input.ICommand, args.NewValue as System.Windows.Input.ICommand);
            UpdateCommandEnabledState();
        }
        else if (args.Property == CommandParameterProperty || args.Property == CommandTargetProperty)
        {
            UpdateCommandEnabledState();
        }
        else if (args.Property == IsEnabledProperty)
        {
            // If user toggles IsEnabled while command-gated, remember intent but keep disabled.
            if (_isCommandDisablingIsEnabled && !_isUpdatingIsEnabled)
            {
                _storedIsEnabledLocalValue = ReadLocalValue(IsEnabledProperty);
                UpdateCommandEnabledState();
            }
        }
    }

    protected override void OnVisualParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        var start = Stopwatch.GetTimestamp();
        base.OnVisualParentChanged(oldParent, newParent);

        if (ShouldTrackImplicitStyleScopes())
        {
            RefreshResourceScopeSubscriptions();
            UpdateImplicitStyle();
        }
        else
        {
            ClearResourceScopeSubscriptions();
        }

        UpdateCommandEnabledState();
    }

    protected override void OnLogicalParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        var start = Stopwatch.GetTimestamp();
        base.OnLogicalParentChanged(oldParent, newParent);
        if (VisualParent != null)
        {
            return;
        }

        if (ShouldTrackImplicitStyleScopes())
        {
            RefreshResourceScopeSubscriptions();
            UpdateImplicitStyle();
        }
        else
        {
            ClearResourceScopeSubscriptions();
        }

        UpdateCommandEnabledState();
    }

    protected virtual Style? GetFallbackStyle()
    {
        return null;
    }

    protected bool ExecuteCommand()
    {
        return CommandSourceExecution.TryExecute(this, this);
    }

    private void RefreshCommandSubscriptions(System.Windows.Input.ICommand? oldCommand, System.Windows.Input.ICommand? newCommand)
    {
        if (ReferenceEquals(_subscribedCommand, oldCommand) && oldCommand != null)
        {
            oldCommand.CanExecuteChanged -= OnCommandCanExecuteChanged;
            _subscribedCommand = null;
        }

        if (newCommand == null)
        {
            return;
        }

        newCommand.CanExecuteChanged += OnCommandCanExecuteChanged;
        _subscribedCommand = newCommand;
    }

    private void OnCommandCanExecuteChanged(object? sender, EventArgs e)
    {
        UpdateCommandEnabledState();
    }

    private void UpdateCommandEnabledState()
    {
        if (Command == null)
        {
            RestoreIsEnabledIfCommandDisabledIt();
            return;
        }

        if (CommandSourceExecution.CanExecute(this, this))
        {
            RestoreIsEnabledIfCommandDisabledIt();
            return;
        }

        if (!_isCommandDisablingIsEnabled)
        {
            _storedIsEnabledLocalValue = ReadLocalValue(IsEnabledProperty);
            _isCommandDisablingIsEnabled = true;
        }

        if (IsEnabled)
        {
            _isUpdatingIsEnabled = true;
            try
            {
                IsEnabled = false;
            }
            finally
            {
                _isUpdatingIsEnabled = false;
            }
        }
        else
        {
            // Still force a local disable so user enabling is remembered but overridden while CanExecute is false.
            if (!HasLocalValue(IsEnabledProperty))
            {
                _isUpdatingIsEnabled = true;
                try
                {
                    IsEnabled = false;
                }
                finally
                {
                    _isUpdatingIsEnabled = false;
                }
            }
        }
    }

    private void RestoreIsEnabledIfCommandDisabledIt()
    {
        if (!_isCommandDisablingIsEnabled)
        {
            return;
        }

        _isUpdatingIsEnabled = true;
        try
        {
            if (ReferenceEquals(_storedIsEnabledLocalValue, DependencyObject.UnsetValue))
            {
                ClearValue(IsEnabledProperty);
            }
            else
            {
                SetValue(IsEnabledProperty, _storedIsEnabledLocalValue);
            }
        }
        finally
        {
            _isUpdatingIsEnabled = false;
        }

        _storedIsEnabledLocalValue = DependencyObject.UnsetValue;
        _isCommandDisablingIsEnabled = false;
    }

    protected UIElement ResolveCommandTarget()
    {
        return CommandTargetResolver.Resolve(CommandTarget, this);
    }

    private void SetTemplateTree(UIElement root)
    {
        ClearTemplateTree();

        _templateRoot = root;
        _templateRoot.SetVisualParent(this);
        _templateRoot.SetLogicalParent(this);

        IndexTemplateTree(root);
    }

    private void ClearTemplateTree()
    {
        _templateTriggerEngine.Clear();

        if (_templateRoot != null)
        {
            _templateRoot.SetVisualParent(null);
            _templateRoot.SetLogicalParent(null);
        }

        _templateRoot = null;
        _namedTemplateChildren.Clear();
    }

    private void IndexTemplateTree(UIElement root)
    {
        if (root is FrameworkElement element && !string.IsNullOrWhiteSpace(element.Name))
        {
            _namedTemplateChildren[element.Name] = element;
        }

        foreach (var child in root.GetVisualChildren())
        {
            IndexTemplateTree(child);
        }
    }

    private void ApplyTemplateBindings()
    {
        if (Template == null || _templateRoot == null)
        {
            return;
        }

        foreach (var binding in Template.Bindings)
        {
            var target = ResolveTemplateBindingTarget(binding.TargetName);
            if (target == null)
            {
                continue;
            }

            target.SetTemplateValue(binding.TargetProperty, ResolveTemplateBindingValue(binding, target));

            EventHandler<DependencyPropertyChangedEventArgs> handler = (_, args) =>
            {
                if (args.Property == binding.SourceProperty)
                {
                    target.SetTemplateValue(binding.TargetProperty, ResolveTemplateBindingValue(binding, target));
                }
            };

            DependencyPropertyChanged += handler;
            _templateBindingHandlers.Add((binding.SourceProperty, handler));
        }
    }

    private UIElement? ResolveTemplateBindingTarget(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return _templateRoot;
        }

        return GetTemplateChild(targetName);
    }

    private void ClearTemplateBindings()
    {
        foreach (var (_, handler) in _templateBindingHandlers)
        {
            DependencyPropertyChanged -= handler;
        }

        _templateBindingHandlers.Clear();
    }

    private object? ResolveTemplateBindingValue(TemplateBinding binding, DependencyObject target)
    {
        var value = GetValue(binding.SourceProperty);
        var source = GetValueSource(binding.SourceProperty);
        if (value == null && binding.TargetNullValue != null)
        {
            if (ResourceReferenceResolver.TryResolveForType(
                    this,
                    binding.TargetNullValue,
                    binding.TargetProperty.PropertyType,
                    $"TemplateBinding {binding.TargetProperty.Name}.TargetNullValue",
                    out var resolvedTargetNullValue) &&
                !ReferenceEquals(resolvedTargetNullValue, DependencyObject.UnsetValue))
            {
                value = resolvedTargetNullValue;
            }
            else
            {
                value = null;
            }

            return CoerceTemplateBindingValue(value, binding.TargetProperty.PropertyType);
        }

        if (source == DependencyPropertyValueSource.Default && binding.FallbackValue != null)
        {
            if (ResourceReferenceResolver.TryResolveForType(
                    this,
                    binding.FallbackValue,
                    binding.TargetProperty.PropertyType,
                    $"TemplateBinding {binding.TargetProperty.Name}.FallbackValue",
                    out var resolvedFallbackValue) &&
                !ReferenceEquals(resolvedFallbackValue, DependencyObject.UnsetValue))
            {
                value = resolvedFallbackValue;
            }
            else
            {
                value = null;
            }

            return CoerceTemplateBindingValue(value, binding.TargetProperty.PropertyType);
        }

        if (!ResourceReferenceResolver.TryResolve(target, binding.TargetProperty, value, out var resolvedValue))
        {
            return null;
        }

        value = resolvedValue;
        return CoerceTemplateBindingValue(value, binding.TargetProperty.PropertyType);
    }

    private static object? CoerceTemplateBindingValue(object? value, Type targetType)
    {
        if (value == null || targetType.IsInstanceOfType(value))
        {
            return value;
        }

        if (targetType == typeof(Thickness) && value is float uniform)
        {
            return new Thickness(uniform);
        }

        if (DependencyValueCoercion.TryCoerce(value, targetType, out var coerced))
        {
            return coerced;
        }

        return value;
    }

    private void ApplyTemplateTriggers()
    {
        if (Template == null || Template.Triggers.Count == 0)
        {
            _templateTriggerEngine.Clear();
            return;
        }

        _templateTriggerEngine.Apply(Template.Triggers as IReadOnlyList<TriggerBase> ?? Template.Triggers.ToList());
    }

    private void ValidateTemplateParts()
    {
        var partAttributes = GetType()
            .GetCustomAttributes(typeof(TemplatePartAttribute), inherit: true)
            .OfType<TemplatePartAttribute>()
            .ToArray();
        foreach (var part in partAttributes)
        {
            if (string.IsNullOrWhiteSpace(part.Name))
            {
                continue;
            }

            var element = GetTemplateChild(part.Name);
            if (element == null)
            {
                throw new InvalidOperationException(
                    $"Template for '{GetType().Name}' is missing required part '{part.Name}'.");
            }

            if (!part.Type.IsInstanceOfType(element))
            {
                throw new InvalidOperationException(
                    $"Template part '{part.Name}' for '{GetType().Name}' must be of type '{part.Type.Name}', but was '{element.GetType().Name}'.");
            }
        }
    }

    protected virtual void OnResourceScopeChanged(object? sender, ResourceDictionaryChangedEventArgs e)
    {
        UpdateImplicitStyle();
    }

    private void UpdateImplicitStyle()
    {
        if (!ShouldApplyImplicitStyle())
        {
            return;
        }

        var targetStyle = ResolveImplicitStyleTarget();
        if (ReferenceEquals(targetStyle, _activeImplicitStyle) &&
            ReferenceEquals(Style, targetStyle))
        {
            return;
        }

        if (targetStyle == null)
        {
            if (ImplicitStylePolicy.CanClearImplicit(Style, _activeImplicitStyle))
            {
                _isApplyingImplicitStyle = true;
                try
                {
                    Style = null;
                }
                finally
                {
                    _isApplyingImplicitStyle = false;
                }
            }

            _activeImplicitStyle = null;
            return;
        }

        if (!ReferenceEquals(Style, targetStyle))
        {
            _isApplyingImplicitStyle = true;
            try
            {
                Style = targetStyle;
            }
            finally
            {
                _isApplyingImplicitStyle = false;
            }
        }

        _activeImplicitStyle = targetStyle;
    }

    private bool ShouldApplyImplicitStyle()
    {
        return ImplicitStylePolicy.ShouldApply(Style, _activeImplicitStyle, GetFallbackStyle());
    }

    private void RefreshResourceScopeSubscriptions()
    {
        var nextAncestors = new List<FrameworkElement>();
        var visited = new HashSet<FrameworkElement>();
        CollectAncestorScopeSubscriptions(VisualParent, visited, nextAncestors);
        CollectAncestorScopeSubscriptions(LogicalParent, visited, nextAncestors);

        if (nextAncestors.Count == 0)
        {
            ClearResourceScopeSubscriptions();
            return;
        }

        var remainingExistingAncestors = new HashSet<FrameworkElement>(_styleResourceAncestors);
        for (var i = 0; i < nextAncestors.Count; i++)
        {
            var ancestor = nextAncestors[i];
            if (remainingExistingAncestors.Remove(ancestor))
            {
                continue;
            }

            ancestor.Resources.Changed += OnResourceScopeChanged;
            _styleResourceAncestors.Add(ancestor);
        }

        if (remainingExistingAncestors.Count == 0)
        {
            return;
        }

        for (var i = _styleResourceAncestors.Count - 1; i >= 0; i--)
        {
            var ancestor = _styleResourceAncestors[i];
            if (!remainingExistingAncestors.Contains(ancestor))
            {
                continue;
            }

            ancestor.Resources.Changed -= OnResourceScopeChanged;
            _styleResourceAncestors.RemoveAt(i);
        }
    }

    private void ClearResourceScopeSubscriptions()
    {
        foreach (var ancestor in _styleResourceAncestors)
        {
            ancestor.Resources.Changed -= OnResourceScopeChanged;
        }

        _styleResourceAncestors.Clear();
    }

    private bool ShouldTrackImplicitStyleScopes()
    {
        return ShouldApplyImplicitStyle();
    }

    private Style? ResolveImplicitStyleTarget()
    {
        Style? resourceStyle = null;
        if (DefaultStyleKey != null &&
            TryFindResource(DefaultStyleKey, out var resource) &&
            resource is Style style)
        {
            resourceStyle = style;
        }

        return resourceStyle ?? GetFallbackStyle();
    }

    private void CollectAncestorScopeSubscriptions(
        UIElement? start,
        ISet<FrameworkElement> visited,
        ICollection<FrameworkElement> ancestors)
    {
        for (var current = start; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is not FrameworkElement framework || !visited.Add(framework))
            {
                continue;
            }

            ancestors.Add(framework);
        }
    }

    internal static int GetMeasureTemplateApplyAttemptCountForTests()
    {
        return _measureTemplateApplyAttemptCount;
    }

    internal static void ResetMeasureTemplateApplyAttemptCountForTests()
    {
        _measureTemplateApplyAttemptCount = 0;
    }
}
