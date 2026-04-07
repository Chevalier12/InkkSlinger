using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class Control : FrameworkElement, ICommandSource
{
    private static int _measureTemplateApplyAttemptCount;
    private static long _diagGetVisualChildrenCallCount;
    private static long _diagGetVisualChildrenYieldedTemplateRootCount;
    private static long _diagGetVisualChildrenWithoutTemplateRootCount;
    private static long _diagGetVisualChildCountForTraversalCallCount;
    private static long _diagGetVisualChildCountForTraversalWithTemplateRootCount;
    private static long _diagGetVisualChildCountForTraversalWithoutTemplateRootCount;
    private static long _diagGetVisualChildAtForTraversalCallCount;
    private static long _diagGetVisualChildAtForTraversalTemplateRootPathCount;
    private static long _diagGetVisualChildAtForTraversalOutOfRangeCount;
    private static long _diagApplyTemplateCallCount;
    private static long _diagApplyTemplateElapsedTicks;
    private static long _diagApplyTemplateTemplateNullCount;
    private static long _diagApplyTemplateTargetTypeMismatchCount;
    private static long _diagApplyTemplateBuildReturnedNullCount;
    private static long _diagApplyTemplateSetTemplateTreeCount;
    private static long _diagApplyTemplateBindingsAppliedCount;
    private static long _diagApplyTemplateTriggersAppliedCount;
    private static long _diagApplyTemplateValidationCount;
    private static long _diagApplyTemplateOnApplyTemplateCount;
    private static long _diagApplyTemplateReturnedTrueCount;
    private static long _diagApplyTemplateReturnedFalseCount;
    private static long _diagMeasureOverrideCallCount;
    private static long _diagMeasureOverrideElapsedTicks;
    private static long _diagMeasureOverrideImplicitStyleUpdateCount;
    private static long _diagMeasureOverrideTemplateApplyAttemptCount;
    private static long _diagMeasureOverrideTemplateRootMeasureCount;
    private static long _diagMeasureOverrideReturnedZeroCount;
    private static long _diagCanReuseMeasureCallCount;
    private static long _diagCanReuseMeasureTemplateRootDelegatedCount;
    private static long _diagCanReuseMeasureNoTemplateRootRejectedCount;
    private static long _diagArrangeOverrideCallCount;
    private static long _diagArrangeOverrideElapsedTicks;
    private static long _diagArrangeOverrideTemplateRootArrangeCount;
    private static long _diagArrangeOverrideNoTemplateRootCount;
    private static long _diagDependencyPropertyChangedCallCount;
    private static long _diagDependencyPropertyChangedElapsedTicks;
    private static long _diagDependencyPropertyChangedStylePropertyCount;
    private static long _diagDependencyPropertyChangedTemplatePropertyCount;
    private static long _diagDependencyPropertyChangedCommandPropertyCount;
    private static long _diagDependencyPropertyChangedCommandStatePropertyCount;
    private static long _diagDependencyPropertyChangedIsEnabledPropertyCount;
    private static long _diagDependencyPropertyChangedOtherPropertyCount;
    private static long _diagVisualParentChangedCallCount;
    private static long _diagVisualParentChangedElapsedTicks;
    private static long _diagVisualParentChangedTrackedImplicitStyleScopesCount;
    private static long _diagVisualParentChangedClearedImplicitStyleScopesCount;
    private static long _diagLogicalParentChangedCallCount;
    private static long _diagLogicalParentChangedElapsedTicks;
    private static long _diagLogicalParentChangedSkippedForVisualParentCount;
    private static long _diagLogicalParentChangedTrackedImplicitStyleScopesCount;
    private static long _diagLogicalParentChangedClearedImplicitStyleScopesCount;
    private static long _diagResourceScopeChangedCallCount;
    private static long _diagResourceScopeChangedApplicationSkipCount;
    private static long _diagUpdateImplicitStyleCallCount;
    private static long _diagUpdateImplicitStyleAppliedCount;
    private static long _diagUpdateImplicitStyleClearedCount;
    private static long _diagUpdateImplicitStyleNoChangeCount;
    private static long _diagUpdateImplicitStyleSkippedCount;
    private static long _diagRefreshCommandSubscriptionsCallCount;
    private static long _diagRefreshCommandSubscriptionsDetachedOldCommandCount;
    private static long _diagRefreshCommandSubscriptionsAttachedNewCommandCount;
    private static long _diagUpdateCommandEnabledStateCallCount;
    private static long _diagUpdateCommandEnabledStateNoCommandRestoreCount;
    private static long _diagUpdateCommandEnabledStateCanExecuteRestoreCount;
    private static long _diagUpdateCommandEnabledStateDisableCommandCount;
    private static long _diagUpdateCommandEnabledStateForceLocalDisableCount;
    private static long _diagRestoreIsEnabledIfCommandDisabledItCallCount;
    private static long _diagRestoreIsEnabledIfCommandDisabledItNoOpCount;
    private static long _diagRestoreIsEnabledIfCommandDisabledItClearValueCount;
    private static long _diagRestoreIsEnabledIfCommandDisabledItRestoreStoredValueCount;
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

    public static readonly DependencyProperty HorizontalContentAlignmentProperty =
        DependencyProperty.Register(
            nameof(HorizontalContentAlignment),
            typeof(HorizontalAlignment),
            typeof(Control),
            new FrameworkPropertyMetadata(HorizontalAlignment.Left, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty VerticalContentAlignmentProperty =
        DependencyProperty.Register(
            nameof(VerticalContentAlignment),
            typeof(VerticalAlignment),
            typeof(Control),
            new FrameworkPropertyMetadata(VerticalAlignment.Top, FrameworkPropertyMetadataOptions.AffectsArrange));

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
    private Style? _composedImplicitStyle;
    private Style? _composedImplicitResourceStyle;
    private Style? _composedImplicitFallbackStyle;
    private long _runtimeGetVisualChildrenCallCount;
    private long _runtimeGetVisualChildrenYieldedTemplateRootCount;
    private long _runtimeGetVisualChildrenWithoutTemplateRootCount;
    private long _runtimeGetVisualChildCountForTraversalCallCount;
    private long _runtimeGetVisualChildCountForTraversalWithTemplateRootCount;
    private long _runtimeGetVisualChildCountForTraversalWithoutTemplateRootCount;
    private long _runtimeGetVisualChildAtForTraversalCallCount;
    private long _runtimeGetVisualChildAtForTraversalTemplateRootPathCount;
    private long _runtimeGetVisualChildAtForTraversalOutOfRangeCount;
    private long _runtimeApplyTemplateCallCount;
    private long _runtimeApplyTemplateElapsedTicks;
    private long _runtimeApplyTemplateTemplateNullCount;
    private long _runtimeApplyTemplateTargetTypeMismatchCount;
    private long _runtimeApplyTemplateBuildReturnedNullCount;
    private long _runtimeApplyTemplateSetTemplateTreeCount;
    private long _runtimeApplyTemplateBindingsAppliedCount;
    private long _runtimeApplyTemplateTriggersAppliedCount;
    private long _runtimeApplyTemplateValidationCount;
    private long _runtimeApplyTemplateOnApplyTemplateCount;
    private long _runtimeApplyTemplateReturnedTrueCount;
    private long _runtimeApplyTemplateReturnedFalseCount;
    private long _runtimeMeasureOverrideCallCount;
    private long _runtimeMeasureOverrideElapsedTicks;
    private long _runtimeMeasureOverrideImplicitStyleUpdateCount;
    private long _runtimeMeasureOverrideTemplateApplyAttemptCount;
    private long _runtimeMeasureOverrideTemplateRootMeasureCount;
    private long _runtimeMeasureOverrideReturnedZeroCount;
    private long _runtimeCanReuseMeasureCallCount;
    private long _runtimeCanReuseMeasureTemplateRootDelegatedCount;
    private long _runtimeCanReuseMeasureNoTemplateRootRejectedCount;
    private long _runtimeArrangeOverrideCallCount;
    private long _runtimeArrangeOverrideElapsedTicks;
    private long _runtimeArrangeOverrideTemplateRootArrangeCount;
    private long _runtimeArrangeOverrideNoTemplateRootCount;
    private long _runtimeDependencyPropertyChangedCallCount;
    private long _runtimeDependencyPropertyChangedElapsedTicks;
    private long _runtimeDependencyPropertyChangedStylePropertyCount;
    private long _runtimeDependencyPropertyChangedTemplatePropertyCount;
    private long _runtimeDependencyPropertyChangedCommandPropertyCount;
    private long _runtimeDependencyPropertyChangedCommandStatePropertyCount;
    private long _runtimeDependencyPropertyChangedIsEnabledPropertyCount;
    private long _runtimeDependencyPropertyChangedOtherPropertyCount;
    private long _runtimeVisualParentChangedCallCount;
    private long _runtimeVisualParentChangedElapsedTicks;
    private long _runtimeVisualParentChangedTrackedImplicitStyleScopesCount;
    private long _runtimeVisualParentChangedClearedImplicitStyleScopesCount;
    private long _runtimeLogicalParentChangedCallCount;
    private long _runtimeLogicalParentChangedElapsedTicks;
    private long _runtimeLogicalParentChangedSkippedForVisualParentCount;
    private long _runtimeLogicalParentChangedTrackedImplicitStyleScopesCount;
    private long _runtimeLogicalParentChangedClearedImplicitStyleScopesCount;
    private long _runtimeResourceScopeChangedCallCount;
    private long _runtimeResourceScopeChangedApplicationSkipCount;
    private long _runtimeUpdateImplicitStyleCallCount;
    private long _runtimeUpdateImplicitStyleAppliedCount;
    private long _runtimeUpdateImplicitStyleClearedCount;
    private long _runtimeUpdateImplicitStyleNoChangeCount;
    private long _runtimeUpdateImplicitStyleSkippedCount;
    private long _runtimeRefreshCommandSubscriptionsCallCount;
    private long _runtimeRefreshCommandSubscriptionsDetachedOldCommandCount;
    private long _runtimeRefreshCommandSubscriptionsAttachedNewCommandCount;
    private long _runtimeUpdateCommandEnabledStateCallCount;
    private long _runtimeUpdateCommandEnabledStateNoCommandRestoreCount;
    private long _runtimeUpdateCommandEnabledStateCanExecuteRestoreCount;
    private long _runtimeUpdateCommandEnabledStateDisableCommandCount;
    private long _runtimeUpdateCommandEnabledStateForceLocalDisableCount;
    private long _runtimeRestoreIsEnabledIfCommandDisabledItCallCount;
    private long _runtimeRestoreIsEnabledIfCommandDisabledItNoOpCount;
    private long _runtimeRestoreIsEnabledIfCommandDisabledItClearValueCount;
    private long _runtimeRestoreIsEnabledIfCommandDisabledItRestoreStoredValueCount;

    public Control()
    {
        DefaultStyleKey = GetType();
        Resources.Changed += OnResourceScopeChanged;
        UiApplication.Current.Resources.Changed += OnResourceScopeChanged;
        _templateTriggerEngine = new TemplateTriggerEngine(this, FindTemplateNamedObject);
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

    public HorizontalAlignment HorizontalContentAlignment
    {
        get => GetValue<HorizontalAlignment>(HorizontalContentAlignmentProperty);
        set => SetValue(HorizontalContentAlignmentProperty, value);
    }

    public VerticalAlignment VerticalContentAlignment
    {
        get => GetValue<VerticalAlignment>(VerticalContentAlignmentProperty);
        set => SetValue(VerticalContentAlignmentProperty, value);
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
            _runtimeGetVisualChildrenCallCount++;
            _runtimeGetVisualChildrenYieldedTemplateRootCount++;
            IncrementAggregate(ref _diagGetVisualChildrenCallCount);
            IncrementAggregate(ref _diagGetVisualChildrenYieldedTemplateRootCount);
            yield return _templateRoot;
            yield break;
        }

        _runtimeGetVisualChildrenCallCount++;
        _runtimeGetVisualChildrenWithoutTemplateRootCount++;
        IncrementAggregate(ref _diagGetVisualChildrenCallCount);
        IncrementAggregate(ref _diagGetVisualChildrenWithoutTemplateRootCount);
    }

    internal override int GetVisualChildCountForTraversal()
    {
        _runtimeGetVisualChildCountForTraversalCallCount++;
        IncrementAggregate(ref _diagGetVisualChildCountForTraversalCallCount);

        if (_templateRoot != null)
        {
            _runtimeGetVisualChildCountForTraversalWithTemplateRootCount++;
            IncrementAggregate(ref _diagGetVisualChildCountForTraversalWithTemplateRootCount);
        }
        else
        {
            _runtimeGetVisualChildCountForTraversalWithoutTemplateRootCount++;
            IncrementAggregate(ref _diagGetVisualChildCountForTraversalWithoutTemplateRootCount);
        }

        return _templateRoot != null ? 1 : 0;
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        _runtimeGetVisualChildAtForTraversalCallCount++;
        IncrementAggregate(ref _diagGetVisualChildAtForTraversalCallCount);

        if (index == 0 && _templateRoot != null)
        {
            _runtimeGetVisualChildAtForTraversalTemplateRootPathCount++;
            IncrementAggregate(ref _diagGetVisualChildAtForTraversalTemplateRootPathCount);
            return _templateRoot;
        }

        _runtimeGetVisualChildAtForTraversalOutOfRangeCount++;
        IncrementAggregate(ref _diagGetVisualChildAtForTraversalOutOfRangeCount);
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
        var start = Stopwatch.GetTimestamp();
        _runtimeApplyTemplateCallCount++;

        try
        {
            ClearTemplateBindings();
            _templateTriggerEngine.Clear();

            if (Template == null)
            {
                _runtimeApplyTemplateTemplateNullCount++;
                _runtimeApplyTemplateReturnedFalseCount++;
                IncrementAggregate(ref _diagApplyTemplateTemplateNullCount);
                IncrementAggregate(ref _diagApplyTemplateReturnedFalseCount);
                ClearTemplateTree();
                return false;
            }

            if (Template.TargetType != null && !Template.TargetType.IsInstanceOfType(this))
            {
                _runtimeApplyTemplateTargetTypeMismatchCount++;
                IncrementAggregate(ref _diagApplyTemplateTargetTypeMismatchCount);
                throw new InvalidOperationException(
                    $"ControlTemplate target type '{Template.TargetType.Name}' is not compatible with '{GetType().Name}'.");
            }

            var built = Template.Build(this);
            if (built == null)
            {
                _runtimeApplyTemplateBuildReturnedNullCount++;
                _runtimeApplyTemplateReturnedFalseCount++;
                IncrementAggregate(ref _diagApplyTemplateBuildReturnedNullCount);
                IncrementAggregate(ref _diagApplyTemplateReturnedFalseCount);
                ClearTemplateTree();
                return false;
            }

            SetTemplateTree(built);
            _runtimeApplyTemplateSetTemplateTreeCount++;
            IncrementAggregate(ref _diagApplyTemplateSetTemplateTreeCount);

            ApplyTemplateBindings();
            _runtimeApplyTemplateBindingsAppliedCount++;
            IncrementAggregate(ref _diagApplyTemplateBindingsAppliedCount);

            ApplyTemplateTriggers();
            _runtimeApplyTemplateTriggersAppliedCount++;
            IncrementAggregate(ref _diagApplyTemplateTriggersAppliedCount);

            ValidateTemplateParts();
            _runtimeApplyTemplateValidationCount++;
            IncrementAggregate(ref _diagApplyTemplateValidationCount);

            OnApplyTemplate();
            _runtimeApplyTemplateOnApplyTemplateCount++;
            _runtimeApplyTemplateReturnedTrueCount++;
            IncrementAggregate(ref _diagApplyTemplateOnApplyTemplateCount);
            IncrementAggregate(ref _diagApplyTemplateReturnedTrueCount);
            return true;
        }
        finally
        {
            _runtimeApplyTemplateElapsedTicks += Stopwatch.GetTimestamp() - start;
            RecordAggregateElapsed(ref _diagApplyTemplateCallCount, ref _diagApplyTemplateElapsedTicks, start);
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var start = Stopwatch.GetTimestamp();
        _runtimeMeasureOverrideCallCount++;

        try
        {
            if (_templateRoot == null)
            {
                if (Template == null)
                {
                    _runtimeMeasureOverrideImplicitStyleUpdateCount++;
                    IncrementAggregate(ref _diagMeasureOverrideImplicitStyleUpdateCount);
                    UpdateImplicitStyle();
                    if (Template == null)
                    {
                        _runtimeMeasureOverrideReturnedZeroCount++;
                        IncrementAggregate(ref _diagMeasureOverrideReturnedZeroCount);
                        return Vector2.Zero;
                    }
                }

                _measureTemplateApplyAttemptCount++;
                _runtimeMeasureOverrideTemplateApplyAttemptCount++;
                IncrementAggregate(ref _diagMeasureOverrideTemplateApplyAttemptCount);
                ApplyTemplate();
            }

            if (_templateRoot is FrameworkElement element)
            {
                _runtimeMeasureOverrideTemplateRootMeasureCount++;
                IncrementAggregate(ref _diagMeasureOverrideTemplateRootMeasureCount);
                element.Measure(availableSize);
                return element.DesiredSize;
            }

            _runtimeMeasureOverrideReturnedZeroCount++;
            IncrementAggregate(ref _diagMeasureOverrideReturnedZeroCount);
            return Vector2.Zero;
        }
        finally
        {
            _runtimeMeasureOverrideElapsedTicks += Stopwatch.GetTimestamp() - start;
            RecordAggregateElapsed(ref _diagMeasureOverrideCallCount, ref _diagMeasureOverrideElapsedTicks, start);
        }
    }

    protected override bool CanReuseMeasureForAvailableSizeChange(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
    {
        _runtimeCanReuseMeasureCallCount++;
        IncrementAggregate(ref _diagCanReuseMeasureCallCount);

        if (_templateRoot is not FrameworkElement element)
        {
            _runtimeCanReuseMeasureNoTemplateRootRejectedCount++;
            IncrementAggregate(ref _diagCanReuseMeasureNoTemplateRootRejectedCount);
            return false;
        }

        _runtimeCanReuseMeasureTemplateRootDelegatedCount++;
        IncrementAggregate(ref _diagCanReuseMeasureTemplateRootDelegatedCount);
        return element.CanReuseMeasureForAvailableSizeChangeForParentLayout(previousAvailableSize, nextAvailableSize);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var start = Stopwatch.GetTimestamp();
        _runtimeArrangeOverrideCallCount++;

        try
        {
            if (_templateRoot is FrameworkElement element)
            {
                _runtimeArrangeOverrideTemplateRootArrangeCount++;
                IncrementAggregate(ref _diagArrangeOverrideTemplateRootArrangeCount);
                element.Arrange(new LayoutRect(LayoutSlot.X, LayoutSlot.Y, finalSize.X, finalSize.Y));
            }
            else
            {
                _runtimeArrangeOverrideNoTemplateRootCount++;
                IncrementAggregate(ref _diagArrangeOverrideNoTemplateRootCount);
            }

            return finalSize;
        }
        finally
        {
            _runtimeArrangeOverrideElapsedTicks += Stopwatch.GetTimestamp() - start;
            RecordAggregateElapsed(ref _diagArrangeOverrideCallCount, ref _diagArrangeOverrideElapsedTicks, start);
        }
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        var start = Stopwatch.GetTimestamp();
        _runtimeDependencyPropertyChangedCallCount++;
        base.OnDependencyPropertyChanged(args);

        try
        {
            if (args.Property == StyleProperty)
            {
                _runtimeDependencyPropertyChangedStylePropertyCount++;
                IncrementAggregate(ref _diagDependencyPropertyChangedStylePropertyCount);

                if (!_isApplyingImplicitStyle)
                {
                    _activeImplicitStyle = null;
                }
            }
            else if (args.Property == TemplateProperty)
            {
                _runtimeDependencyPropertyChangedTemplatePropertyCount++;
                IncrementAggregate(ref _diagDependencyPropertyChangedTemplatePropertyCount);
                ApplyTemplate();
            }
            else if (args.Property == CommandProperty)
            {
                _runtimeDependencyPropertyChangedCommandPropertyCount++;
                IncrementAggregate(ref _diagDependencyPropertyChangedCommandPropertyCount);
                RefreshCommandSubscriptions(args.OldValue as System.Windows.Input.ICommand, args.NewValue as System.Windows.Input.ICommand);
                UpdateCommandEnabledState();
            }
            else if (args.Property == CommandParameterProperty || args.Property == CommandTargetProperty)
            {
                _runtimeDependencyPropertyChangedCommandStatePropertyCount++;
                IncrementAggregate(ref _diagDependencyPropertyChangedCommandStatePropertyCount);
                UpdateCommandEnabledState();
            }
            else if (args.Property == IsEnabledProperty)
            {
                _runtimeDependencyPropertyChangedIsEnabledPropertyCount++;
                IncrementAggregate(ref _diagDependencyPropertyChangedIsEnabledPropertyCount);

                // If user toggles IsEnabled while command-gated, remember intent but keep disabled.
                if (_isCommandDisablingIsEnabled && !_isUpdatingIsEnabled)
                {
                    _storedIsEnabledLocalValue = ReadLocalValue(IsEnabledProperty);
                    UpdateCommandEnabledState();
                }
            }
            else
            {
                _runtimeDependencyPropertyChangedOtherPropertyCount++;
                IncrementAggregate(ref _diagDependencyPropertyChangedOtherPropertyCount);
            }
        }
        finally
        {
            _runtimeDependencyPropertyChangedElapsedTicks += Stopwatch.GetTimestamp() - start;
            RecordAggregateElapsed(ref _diagDependencyPropertyChangedCallCount, ref _diagDependencyPropertyChangedElapsedTicks, start);
        }
    }

    protected override void OnVisualParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        var start = Stopwatch.GetTimestamp();
        _runtimeVisualParentChangedCallCount++;
        base.OnVisualParentChanged(oldParent, newParent);

        if (ShouldTrackImplicitStyleScopes())
        {
            _runtimeVisualParentChangedTrackedImplicitStyleScopesCount++;
            IncrementAggregate(ref _diagVisualParentChangedTrackedImplicitStyleScopesCount);
            RefreshResourceScopeSubscriptions();
            UpdateImplicitStyle();
        }
        else
        {
            _runtimeVisualParentChangedClearedImplicitStyleScopesCount++;
            IncrementAggregate(ref _diagVisualParentChangedClearedImplicitStyleScopesCount);
            ClearResourceScopeSubscriptions();
        }

        UpdateCommandEnabledState();
        _runtimeVisualParentChangedElapsedTicks += Stopwatch.GetTimestamp() - start;
        RecordAggregateElapsed(ref _diagVisualParentChangedCallCount, ref _diagVisualParentChangedElapsedTicks, start);
    }

    protected override void OnLogicalParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        var start = Stopwatch.GetTimestamp();
        _runtimeLogicalParentChangedCallCount++;
        base.OnLogicalParentChanged(oldParent, newParent);
        if (VisualParent != null)
        {
            _runtimeLogicalParentChangedSkippedForVisualParentCount++;
            IncrementAggregate(ref _diagLogicalParentChangedSkippedForVisualParentCount);
            _runtimeLogicalParentChangedElapsedTicks += Stopwatch.GetTimestamp() - start;
            RecordAggregateElapsed(ref _diagLogicalParentChangedCallCount, ref _diagLogicalParentChangedElapsedTicks, start);
            return;
        }

        if (ShouldTrackImplicitStyleScopes())
        {
            _runtimeLogicalParentChangedTrackedImplicitStyleScopesCount++;
            IncrementAggregate(ref _diagLogicalParentChangedTrackedImplicitStyleScopesCount);
            RefreshResourceScopeSubscriptions();
            UpdateImplicitStyle();
        }
        else
        {
            _runtimeLogicalParentChangedClearedImplicitStyleScopesCount++;
            IncrementAggregate(ref _diagLogicalParentChangedClearedImplicitStyleScopesCount);
            ClearResourceScopeSubscriptions();
        }

        UpdateCommandEnabledState();
        _runtimeLogicalParentChangedElapsedTicks += Stopwatch.GetTimestamp() - start;
        RecordAggregateElapsed(ref _diagLogicalParentChangedCallCount, ref _diagLogicalParentChangedElapsedTicks, start);
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
        _runtimeRefreshCommandSubscriptionsCallCount++;
        IncrementAggregate(ref _diagRefreshCommandSubscriptionsCallCount);

        if (ReferenceEquals(_subscribedCommand, oldCommand) && oldCommand != null)
        {
            oldCommand.CanExecuteChanged -= OnCommandCanExecuteChanged;
            _subscribedCommand = null;
            _runtimeRefreshCommandSubscriptionsDetachedOldCommandCount++;
            IncrementAggregate(ref _diagRefreshCommandSubscriptionsDetachedOldCommandCount);
        }

        if (newCommand == null)
        {
            return;
        }

        newCommand.CanExecuteChanged += OnCommandCanExecuteChanged;
        _subscribedCommand = newCommand;
        _runtimeRefreshCommandSubscriptionsAttachedNewCommandCount++;
        IncrementAggregate(ref _diagRefreshCommandSubscriptionsAttachedNewCommandCount);
    }

    private void OnCommandCanExecuteChanged(object? sender, EventArgs e)
    {
        UpdateCommandEnabledState();
    }

    private void UpdateCommandEnabledState()
    {
        _runtimeUpdateCommandEnabledStateCallCount++;
        IncrementAggregate(ref _diagUpdateCommandEnabledStateCallCount);

        if (Command == null)
        {
            _runtimeUpdateCommandEnabledStateNoCommandRestoreCount++;
            IncrementAggregate(ref _diagUpdateCommandEnabledStateNoCommandRestoreCount);
            RestoreIsEnabledIfCommandDisabledIt();
            return;
        }

        if (CommandSourceExecution.CanExecute(this, this))
        {
            _runtimeUpdateCommandEnabledStateCanExecuteRestoreCount++;
            IncrementAggregate(ref _diagUpdateCommandEnabledStateCanExecuteRestoreCount);
            RestoreIsEnabledIfCommandDisabledIt();
            return;
        }

        _runtimeUpdateCommandEnabledStateDisableCommandCount++;
        IncrementAggregate(ref _diagUpdateCommandEnabledStateDisableCommandCount);

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
                _runtimeUpdateCommandEnabledStateForceLocalDisableCount++;
                IncrementAggregate(ref _diagUpdateCommandEnabledStateForceLocalDisableCount);
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
        _runtimeRestoreIsEnabledIfCommandDisabledItCallCount++;
        IncrementAggregate(ref _diagRestoreIsEnabledIfCommandDisabledItCallCount);

        if (!_isCommandDisablingIsEnabled)
        {
            _runtimeRestoreIsEnabledIfCommandDisabledItNoOpCount++;
            IncrementAggregate(ref _diagRestoreIsEnabledIfCommandDisabledItNoOpCount);
            return;
        }

        _isUpdatingIsEnabled = true;
        try
        {
            if (ReferenceEquals(_storedIsEnabledLocalValue, DependencyObject.UnsetValue))
            {
                _runtimeRestoreIsEnabledIfCommandDisabledItClearValueCount++;
                IncrementAggregate(ref _diagRestoreIsEnabledIfCommandDisabledItClearValueCount);
                ClearValue(IsEnabledProperty);
            }
            else
            {
                _runtimeRestoreIsEnabledIfCommandDisabledItRestoreStoredValueCount++;
                IncrementAggregate(ref _diagRestoreIsEnabledIfCommandDisabledItRestoreStoredValueCount);
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
            VisualStateManager.ClearState(_templateRoot as FrameworkElement);
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

    internal object? FindTemplateNamedObject(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return this;
        }

        if (_namedTemplateChildren.TryGetValue(name, out var child))
        {
            return child;
        }

        return FindNamedObjectInTemplateScopes(_templateRoot, name);
    }

    private static object? FindNamedObjectInTemplateScopes(UIElement? root, string name)
    {
        if (root is FrameworkElement frameworkElement)
        {
            var scoped = frameworkElement.GetLocalNameScope()?.FindName(name);
            if (scoped != null)
            {
                return scoped;
            }
        }

        if (root == null)
        {
            return null;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindNamedObjectInTemplateScopes(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
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
        _runtimeResourceScopeChangedCallCount++;
        IncrementAggregate(ref _diagResourceScopeChangedCallCount);

        if (ReferenceEquals(sender, UiApplication.Current.Resources) && !ShouldProcessApplicationResourceChange())
        {
            _runtimeResourceScopeChangedApplicationSkipCount++;
            IncrementAggregate(ref _diagResourceScopeChangedApplicationSkipCount);
            return;
        }

        UpdateImplicitStyle();
    }

    private void UpdateImplicitStyle()
    {
        _runtimeUpdateImplicitStyleCallCount++;
        IncrementAggregate(ref _diagUpdateImplicitStyleCallCount);

        if (!ShouldApplyImplicitStyle())
        {
            _runtimeUpdateImplicitStyleSkippedCount++;
            IncrementAggregate(ref _diagUpdateImplicitStyleSkippedCount);
            return;
        }

        var targetStyle = ResolveImplicitStyleTarget();
        if (ReferenceEquals(targetStyle, _activeImplicitStyle) &&
            ReferenceEquals(Style, targetStyle))
        {
            _runtimeUpdateImplicitStyleNoChangeCount++;
            IncrementAggregate(ref _diagUpdateImplicitStyleNoChangeCount);
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

                _runtimeUpdateImplicitStyleClearedCount++;
                IncrementAggregate(ref _diagUpdateImplicitStyleClearedCount);
            }
            else
            {
                _runtimeUpdateImplicitStyleNoChangeCount++;
                IncrementAggregate(ref _diagUpdateImplicitStyleNoChangeCount);
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

            _runtimeUpdateImplicitStyleAppliedCount++;
            IncrementAggregate(ref _diagUpdateImplicitStyleAppliedCount);
        }
        else
        {
            _runtimeUpdateImplicitStyleNoChangeCount++;
            IncrementAggregate(ref _diagUpdateImplicitStyleNoChangeCount);
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

    private bool ShouldProcessApplicationResourceChange()
    {
        var visualRoot = GetVisualRoot();
        return UiRoot.Current != null && ReferenceEquals(visualRoot, UiRoot.Current.VisualRoot);
    }

    private bool ShouldTrackImplicitStyleScopes()
    {
        return ShouldApplyImplicitStyle();
    }

    private Style? ResolveImplicitStyleTarget()
    {
        var fallbackStyle = GetFallbackStyle();
        Style? resourceStyle = null;
        if (DefaultStyleKey != null &&
            TryFindResource(DefaultStyleKey, out var resource) &&
            resource is Style style)
        {
            resourceStyle = style;
        }

        if (resourceStyle == null)
        {
            return fallbackStyle;
        }

        if (fallbackStyle == null ||
            ReferenceEquals(resourceStyle, fallbackStyle) ||
            StyleChainContains(resourceStyle, fallbackStyle) ||
            StyleDefinesProperty(resourceStyle, TemplateProperty))
        {
            return resourceStyle;
        }

        if (ReferenceEquals(_composedImplicitResourceStyle, resourceStyle) &&
            ReferenceEquals(_composedImplicitFallbackStyle, fallbackStyle) &&
            _composedImplicitStyle != null)
        {
            return _composedImplicitStyle;
        }

        var composedStyle = new Style(resourceStyle.TargetType)
        {
            BasedOn = fallbackStyle
        };

        foreach (var setter in resourceStyle.Setters)
        {
            composedStyle.Setters.Add(setter);
        }

        foreach (var trigger in resourceStyle.Triggers)
        {
            composedStyle.Triggers.Add(trigger);
        }

        _composedImplicitResourceStyle = resourceStyle;
        _composedImplicitFallbackStyle = fallbackStyle;
        _composedImplicitStyle = composedStyle;
        return composedStyle;
    }

    private static bool StyleDefinesProperty(Style style, DependencyProperty property)
    {
        for (Style? current = style; current != null; current = current.BasedOn)
        {
            foreach (var setterBase in current.Setters)
            {
                if (setterBase is Setter setter &&
                    string.IsNullOrWhiteSpace(setter.TargetName) &&
                    ReferenceEquals(setter.Property, property))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool StyleChainContains(Style style, Style candidate)
    {
        for (Style? current = style; current != null; current = current.BasedOn)
        {
            if (ReferenceEquals(current, candidate))
            {
                return true;
            }
        }

        return false;
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

    internal ControlRuntimeDiagnosticsSnapshot GetControlSnapshotForDiagnostics()
    {
        return new ControlRuntimeDiagnosticsSnapshot(
            Template != null,
            _templateRoot != null,
            _templateRoot?.GetType().Name ?? string.Empty,
            _subscribedCommand != null,
            _isCommandDisablingIsEnabled,
            !ReferenceEquals(_storedIsEnabledLocalValue, DependencyObject.UnsetValue),
            _styleResourceAncestors.Count,
            LayoutSlot.Width,
            LayoutSlot.Height,
            _runtimeGetVisualChildrenCallCount,
            _runtimeGetVisualChildrenYieldedTemplateRootCount,
            _runtimeGetVisualChildrenWithoutTemplateRootCount,
            _runtimeGetVisualChildCountForTraversalCallCount,
            _runtimeGetVisualChildCountForTraversalWithTemplateRootCount,
            _runtimeGetVisualChildCountForTraversalWithoutTemplateRootCount,
            _runtimeGetVisualChildAtForTraversalCallCount,
            _runtimeGetVisualChildAtForTraversalTemplateRootPathCount,
            _runtimeGetVisualChildAtForTraversalOutOfRangeCount,
            _runtimeApplyTemplateCallCount,
            TicksToMilliseconds(_runtimeApplyTemplateElapsedTicks),
            _runtimeApplyTemplateTemplateNullCount,
            _runtimeApplyTemplateTargetTypeMismatchCount,
            _runtimeApplyTemplateBuildReturnedNullCount,
            _runtimeApplyTemplateSetTemplateTreeCount,
            _runtimeApplyTemplateBindingsAppliedCount,
            _runtimeApplyTemplateTriggersAppliedCount,
            _runtimeApplyTemplateValidationCount,
            _runtimeApplyTemplateOnApplyTemplateCount,
            _runtimeApplyTemplateReturnedTrueCount,
            _runtimeApplyTemplateReturnedFalseCount,
            _runtimeMeasureOverrideCallCount,
            TicksToMilliseconds(_runtimeMeasureOverrideElapsedTicks),
            _runtimeMeasureOverrideImplicitStyleUpdateCount,
            _runtimeMeasureOverrideTemplateApplyAttemptCount,
            _runtimeMeasureOverrideTemplateRootMeasureCount,
            _runtimeMeasureOverrideReturnedZeroCount,
            _runtimeCanReuseMeasureCallCount,
            _runtimeCanReuseMeasureTemplateRootDelegatedCount,
            _runtimeCanReuseMeasureNoTemplateRootRejectedCount,
            _runtimeArrangeOverrideCallCount,
            TicksToMilliseconds(_runtimeArrangeOverrideElapsedTicks),
            _runtimeArrangeOverrideTemplateRootArrangeCount,
            _runtimeArrangeOverrideNoTemplateRootCount,
            _runtimeDependencyPropertyChangedCallCount,
            TicksToMilliseconds(_runtimeDependencyPropertyChangedElapsedTicks),
            _runtimeDependencyPropertyChangedStylePropertyCount,
            _runtimeDependencyPropertyChangedTemplatePropertyCount,
            _runtimeDependencyPropertyChangedCommandPropertyCount,
            _runtimeDependencyPropertyChangedCommandStatePropertyCount,
            _runtimeDependencyPropertyChangedIsEnabledPropertyCount,
            _runtimeDependencyPropertyChangedOtherPropertyCount,
            _runtimeVisualParentChangedCallCount,
            TicksToMilliseconds(_runtimeVisualParentChangedElapsedTicks),
            _runtimeVisualParentChangedTrackedImplicitStyleScopesCount,
            _runtimeVisualParentChangedClearedImplicitStyleScopesCount,
            _runtimeLogicalParentChangedCallCount,
            TicksToMilliseconds(_runtimeLogicalParentChangedElapsedTicks),
            _runtimeLogicalParentChangedSkippedForVisualParentCount,
            _runtimeLogicalParentChangedTrackedImplicitStyleScopesCount,
            _runtimeLogicalParentChangedClearedImplicitStyleScopesCount,
            _runtimeResourceScopeChangedCallCount,
            _runtimeResourceScopeChangedApplicationSkipCount,
            _runtimeUpdateImplicitStyleCallCount,
            _runtimeUpdateImplicitStyleAppliedCount,
            _runtimeUpdateImplicitStyleClearedCount,
            _runtimeUpdateImplicitStyleNoChangeCount,
            _runtimeUpdateImplicitStyleSkippedCount,
            _runtimeRefreshCommandSubscriptionsCallCount,
            _runtimeRefreshCommandSubscriptionsDetachedOldCommandCount,
            _runtimeRefreshCommandSubscriptionsAttachedNewCommandCount,
            _runtimeUpdateCommandEnabledStateCallCount,
            _runtimeUpdateCommandEnabledStateNoCommandRestoreCount,
            _runtimeUpdateCommandEnabledStateCanExecuteRestoreCount,
            _runtimeUpdateCommandEnabledStateDisableCommandCount,
            _runtimeUpdateCommandEnabledStateForceLocalDisableCount,
            _runtimeRestoreIsEnabledIfCommandDisabledItCallCount,
            _runtimeRestoreIsEnabledIfCommandDisabledItNoOpCount,
            _runtimeRestoreIsEnabledIfCommandDisabledItClearValueCount,
            _runtimeRestoreIsEnabledIfCommandDisabledItRestoreStoredValueCount);
    }

    internal new static ControlTelemetrySnapshot GetTelemetryAndReset()
    {
        return CreateAggregateTelemetrySnapshot(reset: true);
    }

    internal new static ControlTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return CreateAggregateTelemetrySnapshot(reset: false);
    }

    internal static ControlTelemetrySnapshot GetTelemetrySnapshotForDiagnostics()
    {
        return GetAggregateTelemetrySnapshotForDiagnostics();
    }

    internal static void ResetMeasureTemplateApplyAttemptCountForTests()
    {
        _measureTemplateApplyAttemptCount = 0;
    }

    private static ControlTelemetrySnapshot CreateAggregateTelemetrySnapshot(bool reset)
    {
        return new ControlTelemetrySnapshot(
            ReadOrReset(ref _diagGetVisualChildrenCallCount, reset),
            ReadOrReset(ref _diagGetVisualChildrenYieldedTemplateRootCount, reset),
            ReadOrReset(ref _diagGetVisualChildrenWithoutTemplateRootCount, reset),
            ReadOrReset(ref _diagGetVisualChildCountForTraversalCallCount, reset),
            ReadOrReset(ref _diagGetVisualChildCountForTraversalWithTemplateRootCount, reset),
            ReadOrReset(ref _diagGetVisualChildCountForTraversalWithoutTemplateRootCount, reset),
            ReadOrReset(ref _diagGetVisualChildAtForTraversalCallCount, reset),
            ReadOrReset(ref _diagGetVisualChildAtForTraversalTemplateRootPathCount, reset),
            ReadOrReset(ref _diagGetVisualChildAtForTraversalOutOfRangeCount, reset),
            ReadOrReset(ref _diagApplyTemplateCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagApplyTemplateElapsedTicks, reset)),
            ReadOrReset(ref _diagApplyTemplateTemplateNullCount, reset),
            ReadOrReset(ref _diagApplyTemplateTargetTypeMismatchCount, reset),
            ReadOrReset(ref _diagApplyTemplateBuildReturnedNullCount, reset),
            ReadOrReset(ref _diagApplyTemplateSetTemplateTreeCount, reset),
            ReadOrReset(ref _diagApplyTemplateBindingsAppliedCount, reset),
            ReadOrReset(ref _diagApplyTemplateTriggersAppliedCount, reset),
            ReadOrReset(ref _diagApplyTemplateValidationCount, reset),
            ReadOrReset(ref _diagApplyTemplateOnApplyTemplateCount, reset),
            ReadOrReset(ref _diagApplyTemplateReturnedTrueCount, reset),
            ReadOrReset(ref _diagApplyTemplateReturnedFalseCount, reset),
            ReadOrReset(ref _diagMeasureOverrideCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagMeasureOverrideElapsedTicks, reset)),
            ReadOrReset(ref _diagMeasureOverrideImplicitStyleUpdateCount, reset),
            ReadOrReset(ref _diagMeasureOverrideTemplateApplyAttemptCount, reset),
            ReadOrReset(ref _diagMeasureOverrideTemplateRootMeasureCount, reset),
            ReadOrReset(ref _diagMeasureOverrideReturnedZeroCount, reset),
            ReadOrReset(ref _diagCanReuseMeasureCallCount, reset),
            ReadOrReset(ref _diagCanReuseMeasureTemplateRootDelegatedCount, reset),
            ReadOrReset(ref _diagCanReuseMeasureNoTemplateRootRejectedCount, reset),
            ReadOrReset(ref _diagArrangeOverrideCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagArrangeOverrideElapsedTicks, reset)),
            ReadOrReset(ref _diagArrangeOverrideTemplateRootArrangeCount, reset),
            ReadOrReset(ref _diagArrangeOverrideNoTemplateRootCount, reset),
            ReadOrReset(ref _diagDependencyPropertyChangedCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagDependencyPropertyChangedElapsedTicks, reset)),
            ReadOrReset(ref _diagDependencyPropertyChangedStylePropertyCount, reset),
            ReadOrReset(ref _diagDependencyPropertyChangedTemplatePropertyCount, reset),
            ReadOrReset(ref _diagDependencyPropertyChangedCommandPropertyCount, reset),
            ReadOrReset(ref _diagDependencyPropertyChangedCommandStatePropertyCount, reset),
            ReadOrReset(ref _diagDependencyPropertyChangedIsEnabledPropertyCount, reset),
            ReadOrReset(ref _diagDependencyPropertyChangedOtherPropertyCount, reset),
            ReadOrReset(ref _diagVisualParentChangedCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagVisualParentChangedElapsedTicks, reset)),
            ReadOrReset(ref _diagVisualParentChangedTrackedImplicitStyleScopesCount, reset),
            ReadOrReset(ref _diagVisualParentChangedClearedImplicitStyleScopesCount, reset),
            ReadOrReset(ref _diagLogicalParentChangedCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagLogicalParentChangedElapsedTicks, reset)),
            ReadOrReset(ref _diagLogicalParentChangedSkippedForVisualParentCount, reset),
            ReadOrReset(ref _diagLogicalParentChangedTrackedImplicitStyleScopesCount, reset),
            ReadOrReset(ref _diagLogicalParentChangedClearedImplicitStyleScopesCount, reset),
            ReadOrReset(ref _diagResourceScopeChangedCallCount, reset),
            ReadOrReset(ref _diagResourceScopeChangedApplicationSkipCount, reset),
            ReadOrReset(ref _diagUpdateImplicitStyleCallCount, reset),
            ReadOrReset(ref _diagUpdateImplicitStyleAppliedCount, reset),
            ReadOrReset(ref _diagUpdateImplicitStyleClearedCount, reset),
            ReadOrReset(ref _diagUpdateImplicitStyleNoChangeCount, reset),
            ReadOrReset(ref _diagUpdateImplicitStyleSkippedCount, reset),
            ReadOrReset(ref _diagRefreshCommandSubscriptionsCallCount, reset),
            ReadOrReset(ref _diagRefreshCommandSubscriptionsDetachedOldCommandCount, reset),
            ReadOrReset(ref _diagRefreshCommandSubscriptionsAttachedNewCommandCount, reset),
            ReadOrReset(ref _diagUpdateCommandEnabledStateCallCount, reset),
            ReadOrReset(ref _diagUpdateCommandEnabledStateNoCommandRestoreCount, reset),
            ReadOrReset(ref _diagUpdateCommandEnabledStateCanExecuteRestoreCount, reset),
            ReadOrReset(ref _diagUpdateCommandEnabledStateDisableCommandCount, reset),
            ReadOrReset(ref _diagUpdateCommandEnabledStateForceLocalDisableCount, reset),
            ReadOrReset(ref _diagRestoreIsEnabledIfCommandDisabledItCallCount, reset),
            ReadOrReset(ref _diagRestoreIsEnabledIfCommandDisabledItNoOpCount, reset),
            ReadOrReset(ref _diagRestoreIsEnabledIfCommandDisabledItClearValueCount, reset),
            ReadOrReset(ref _diagRestoreIsEnabledIfCommandDisabledItRestoreStoredValueCount, reset));
    }

    private static void IncrementAggregate(ref long counter)
    {
        Interlocked.Increment(ref counter);
    }

    private static long ReadAggregate(ref long counter)
    {
        return Interlocked.Read(ref counter);
    }

    private static long ResetAggregate(ref long counter)
    {
        return Interlocked.Exchange(ref counter, 0);
    }

    private static long ReadOrReset(ref long counter, bool reset)
    {
        return reset ? ResetAggregate(ref counter) : ReadAggregate(ref counter);
    }

    private static void RecordAggregateElapsed(ref long callCount, ref long elapsedTicks, long start)
    {
        IncrementAggregate(ref callCount);
        Interlocked.Add(ref elapsedTicks, Stopwatch.GetTimestamp() - start);
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }
}
