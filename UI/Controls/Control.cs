using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class Control : FrameworkElement
{
    public static readonly DependencyProperty DefaultStyleKeyProperty =
        DependencyProperty.Register(nameof(DefaultStyleKey), typeof(System.Type), typeof(Control), new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty TemplateProperty =
        DependencyProperty.Register(
            nameof(Template),
            typeof(ControlTemplate),
            typeof(Control),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

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
    private bool _isImplicitStyleActive;

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

    public virtual void OnApplyTemplate()
    {
    }

    protected UIElement? GetTemplateChild(string name)
    {
        return _namedTemplateChildren.TryGetValue(name, out var element) ? element : null;
    }

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
            _isImplicitStyleActive = false;
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
        base.OnVisualParentChanged(oldParent, newParent);

        RefreshResourceScopeSubscriptions();
        UpdateImplicitStyle();
        UpdateCommandEnabledState();
    }

    protected override void OnLogicalParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        base.OnLogicalParentChanged(oldParent, newParent);
        RefreshResourceScopeSubscriptions();
        UpdateImplicitStyle();
        UpdateCommandEnabledState();
    }

    protected virtual Style? GetFallbackStyle()
    {
        return null;
    }

    protected bool ExecuteCommand()
    {
        if (Command == null)
        {
            return false;
        }

        var target = CommandTarget ?? this;

        if (Command is RoutedCommand routedCommand)
        {
            if (!CommandManager.CanExecute(routedCommand, CommandParameter, target))
            {
                return false;
            }

            CommandManager.Execute(routedCommand, CommandParameter, target);
            return true;
        }

        if (!Command.CanExecute(CommandParameter))
        {
            return false;
        }

        Command.Execute(CommandParameter);
        return true;
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

        var target = CommandTarget ?? this;
        var canExecute = Command is RoutedCommand routedCommand
            ? CommandManager.CanExecute(routedCommand, CommandParameter, target)
            : Command.CanExecute(CommandParameter);

        if (canExecute)
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

            target.SetTemplateValue(binding.TargetProperty, ResolveTemplateBindingValue(binding));

            EventHandler<DependencyPropertyChangedEventArgs> handler = (_, args) =>
            {
                if (args.Property == binding.SourceProperty)
                {
                    target.SetTemplateValue(binding.TargetProperty, ResolveTemplateBindingValue(binding));
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

    private object? ResolveTemplateBindingValue(TemplateBinding binding)
    {
        var value = GetValue(binding.SourceProperty);
        var source = GetValueSource(binding.SourceProperty);
        if (value == null && binding.TargetNullValue != null)
        {
            return binding.TargetNullValue;
        }

        if (source == DependencyPropertyValueSource.Default && binding.FallbackValue != null)
        {
            return binding.FallbackValue;
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

    private void OnResourceScopeChanged(object? sender, ResourceDictionaryChangedEventArgs e)
    {
        UpdateImplicitStyle();
    }

    private void UpdateImplicitStyle()
    {
        if (!ShouldApplyImplicitStyle())
        {
            return;
        }

        var fallbackStyle = GetFallbackStyle();
        Style? resourceStyle = null;
        if (DefaultStyleKey != null &&
            TryFindResource(DefaultStyleKey, out var resource) &&
            resource is Style style)
        {
            resourceStyle = style;
        }

        var targetStyle = resourceStyle ?? fallbackStyle;
        if (targetStyle == null)
        {
            _isImplicitStyleActive = false;
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

        _isImplicitStyleActive = true;
    }

    private bool ShouldApplyImplicitStyle()
    {
        if (_isImplicitStyleActive || Style == null)
        {
            return true;
        }

        var fallbackStyle = GetFallbackStyle();
        return fallbackStyle != null && ReferenceEquals(Style, fallbackStyle);
    }

    private void RefreshResourceScopeSubscriptions()
    {
        foreach (var ancestor in _styleResourceAncestors)
        {
            ancestor.Resources.Changed -= OnResourceScopeChanged;
        }

        _styleResourceAncestors.Clear();

        var visited = new HashSet<FrameworkElement>();
        AddAncestorScopeSubscriptions(VisualParent, visited);
        AddAncestorScopeSubscriptions(LogicalParent, visited);
    }

    private void AddAncestorScopeSubscriptions(UIElement? start, ISet<FrameworkElement> visited)
    {
        for (var current = start; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is not FrameworkElement framework || !visited.Add(framework))
            {
                continue;
            }

            framework.Resources.Changed += OnResourceScopeChanged;
            _styleResourceAncestors.Add(framework);
        }
    }
}
