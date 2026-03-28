using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class FrameworkElement : UIElement
{
    static FrameworkElement()
    {
        ClipToBoundsProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));
    }

    [ThreadStatic]
    private static List<long>? _activeMeasureChildTickStack;

    private readonly Dictionary<DependencyProperty, object> _dynamicResourceBindings = new();
    private FrameworkElement? _resourceParent;
    private NameScope? _nameScope;
    private Style? _activeImplicitStyle;
    private bool _isApplyingImplicitStyle;

    public static readonly DependencyProperty NameProperty =
        DependencyProperty.Register(nameof(Name), typeof(string), typeof(FrameworkElement), new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty DataContextProperty =
        DependencyProperty.Register(
            nameof(DataContext),
            typeof(object),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty WidthProperty =
        DependencyProperty.Register(
            nameof(Width),
            typeof(float),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(float.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure),
            static value => value is float f && (float.IsNaN(f) || f >= 0f));

    public static readonly DependencyProperty HeightProperty =
        DependencyProperty.Register(
            nameof(Height),
            typeof(float),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(float.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure),
            static value => value is float f && (float.IsNaN(f) || f >= 0f));

    public static readonly DependencyProperty MinWidthProperty =
        DependencyProperty.Register(
            nameof(MinWidth),
            typeof(float),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsMeasure),
            static value => value is float f && f >= 0f);

    public static readonly DependencyProperty MinHeightProperty =
        DependencyProperty.Register(
            nameof(MinHeight),
            typeof(float),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsMeasure),
            static value => value is float f && f >= 0f);

    public static readonly DependencyProperty MaxWidthProperty =
        DependencyProperty.Register(
            nameof(MaxWidth),
            typeof(float),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(float.PositiveInfinity, FrameworkPropertyMetadataOptions.AffectsMeasure),
            static value => value is float f && f > 0f);

    public static readonly DependencyProperty MaxHeightProperty =
        DependencyProperty.Register(
            nameof(MaxHeight),
            typeof(float),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(float.PositiveInfinity, FrameworkPropertyMetadataOptions.AffectsMeasure),
            static value => value is float f && f > 0f);

    public static readonly DependencyProperty MarginProperty =
        DependencyProperty.Register(
            nameof(Margin),
            typeof(Thickness),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(Thickness.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty HorizontalAlignmentProperty =
        DependencyProperty.Register(
            nameof(HorizontalAlignment),
            typeof(HorizontalAlignment),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(HorizontalAlignment.Stretch, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty VerticalAlignmentProperty =
        DependencyProperty.Register(
            nameof(VerticalAlignment),
            typeof(VerticalAlignment),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(VerticalAlignment.Stretch, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty StyleProperty =
        DependencyProperty.Register(
            nameof(Style),
            typeof(Style),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BindingGroupProperty =
        DependencyProperty.Register(
            nameof(BindingGroup),
            typeof(BindingGroup),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty FontFamilyProperty =
        DependencyProperty.Register(
            nameof(FontFamily),
            typeof(string),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty FontSizeProperty =
        DependencyProperty.Register(
            nameof(FontSize),
            typeof(float),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(
                12f,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontWeightProperty =
        DependencyProperty.Register(
            nameof(FontWeight),
            typeof(string),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata("Normal", FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty FontStyleProperty =
        DependencyProperty.Register(
            nameof(FontStyle),
            typeof(string),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata("Normal", FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty CursorProperty =
        DependencyProperty.Register(
            nameof(Cursor),
            typeof(string),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty SnapsToDevicePixelsProperty =
        DependencyProperty.Register(
            nameof(SnapsToDevicePixels),
            typeof(bool),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty UseLayoutRoundingProperty =
        DependencyProperty.Register(
            nameof(UseLayoutRounding),
            typeof(bool),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty FocusableProperty =
        DependencyProperty.Register(
            nameof(Focusable),
            typeof(bool),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty RecognizesAccessKeyProperty =
        DependencyProperty.Register(
            nameof(RecognizesAccessKey),
            typeof(bool),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(false));

    private bool _isMeasureValid;
    private bool _isArrangeValid;
    private Vector2 _previousAvailableSize = new(float.NaN, float.NaN);
    private LayoutRect _arrangeRect;
    private int _measureCallCount;
    private int _arrangeCallCount;
    private int _measureWorkCount;
    private int _arrangeWorkCount;
    private long _measureElapsedTicks;
    private long _measureExclusiveElapsedTicks;
    private long _arrangeElapsedTicks;
    private static long _frameMeasureElapsedTicks;
    private static long _frameMeasureExclusiveElapsedTicks;
    private static long _frameArrangeElapsedTicks;
    private static string _frameHottestMeasureElementType = "none";
    private static string _frameHottestMeasureElementName = string.Empty;
    private static long _frameHottestMeasureElapsedTicks;
    private static string _frameHottestArrangeElementType = "none";
    private static string _frameHottestArrangeElementName = string.Empty;
    private static long _frameHottestArrangeElapsedTicks;

    public FrameworkElement()
    {
        Resources.Changed += OnResourcesChanged;
    }

    public event EventHandler? Initialized;

    public event EventHandler? Loaded;

    public event EventHandler? Unloaded;

    public event EventHandler? LayoutUpdated;

    internal event EventHandler? ResourceScopeInvalidated;

    public string Name
    {
        get => GetValue<string>(NameProperty) ?? string.Empty;
        set => SetValue(NameProperty, value);
    }

    public object? DataContext
    {
        get => GetValue(DataContextProperty);
        set => SetValue(DataContextProperty, value);
    }

    public float Width
    {
        get => GetValue<float>(WidthProperty);
        set => SetValue(WidthProperty, value);
    }

    public float Height
    {
        get => GetValue<float>(HeightProperty);
        set => SetValue(HeightProperty, value);
    }

    public float MinWidth
    {
        get => GetValue<float>(MinWidthProperty);
        set => SetValue(MinWidthProperty, value);
    }

    public float MinHeight
    {
        get => GetValue<float>(MinHeightProperty);
        set => SetValue(MinHeightProperty, value);
    }

    public float MaxWidth
    {
        get => GetValue<float>(MaxWidthProperty);
        set => SetValue(MaxWidthProperty, value);
    }

    public float MaxHeight
    {
        get => GetValue<float>(MaxHeightProperty);
        set => SetValue(MaxHeightProperty, value);
    }

    public Thickness Margin
    {
        get => GetValue<Thickness>(MarginProperty);
        set => SetValue(MarginProperty, value);
    }

    public HorizontalAlignment HorizontalAlignment
    {
        get => GetValue<HorizontalAlignment>(HorizontalAlignmentProperty);
        set => SetValue(HorizontalAlignmentProperty, value);
    }

    public VerticalAlignment VerticalAlignment
    {
        get => GetValue<VerticalAlignment>(VerticalAlignmentProperty);
        set => SetValue(VerticalAlignmentProperty, value);
    }

    public Style? Style
    {
        get => GetValue<Style>(StyleProperty);
        set => SetValue(StyleProperty, value);
    }

    public BindingGroup? BindingGroup
    {
        get => GetValue<BindingGroup>(BindingGroupProperty);
        set => SetValue(BindingGroupProperty, value);
    }

    public string FontFamily
    {
        get => GetValue<string>(FontFamilyProperty) ?? string.Empty;
        set => SetValue(FontFamilyProperty, value);
    }

    public float FontSize
    {
        get => GetValue<float>(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public string FontWeight
    {
        get => GetValue<string>(FontWeightProperty) ?? "Normal";
        set => SetValue(FontWeightProperty, value);
    }

    public string FontStyle
    {
        get => GetValue<string>(FontStyleProperty) ?? "Normal";
        set => SetValue(FontStyleProperty, value);
    }

    public string Cursor
    {
        get => GetValue<string>(CursorProperty) ?? string.Empty;
        set => SetValue(CursorProperty, value);
    }

    public bool SnapsToDevicePixels
    {
        get => GetValue<bool>(SnapsToDevicePixelsProperty);
        set => SetValue(SnapsToDevicePixelsProperty, value);
    }

    public bool UseLayoutRounding
    {
        get => GetValue<bool>(UseLayoutRoundingProperty);
        set => SetValue(UseLayoutRoundingProperty, value);
    }

    public bool Focusable
    {
        get => GetValue<bool>(FocusableProperty);
        set => SetValue(FocusableProperty, value);
    }

    public bool RecognizesAccessKey
    {
        get => GetValue<bool>(RecognizesAccessKeyProperty);
        set => SetValue(RecognizesAccessKeyProperty, value);
    }

    public InkkSlinger.ContextMenu? ContextMenu
    {
        get => InkkSlinger.ContextMenu.GetContextMenu(this);
        set => InkkSlinger.ContextMenu.SetContextMenu(this, value);
    }

    public ResourceDictionary Resources { get; } = new();

    public Vector2 DesiredSize { get; private set; }

    public Vector2 RenderSize { get; private set; }

    public float ActualWidth => RenderSize.X;

    public float ActualHeight => RenderSize.Y;

    public bool IsLoaded { get; private set; }

    public int MeasureCallCount => _measureCallCount;

    public int ArrangeCallCount => _arrangeCallCount;

    internal int MeasureWorkCount => _measureWorkCount;

    internal int ArrangeWorkCount => _arrangeWorkCount;

    internal Vector2 PreviousAvailableSizeForTests => _previousAvailableSize;

    internal bool IsMeasureValidForTests => _isMeasureValid;

    internal bool IsArrangeValidForTests => _isArrangeValid;

    internal long MeasureElapsedTicksForTests => _measureElapsedTicks;

    internal long MeasureExclusiveElapsedTicksForTests => _measureExclusiveElapsedTicks;

    internal long ArrangeElapsedTicksForTests => _arrangeElapsedTicks;

    internal static FrameworkLayoutTimingSnapshot GetFrameTimingSnapshotForTests()
    {
        return new FrameworkLayoutTimingSnapshot(
            _frameMeasureElapsedTicks,
            _frameMeasureExclusiveElapsedTicks,
            _frameArrangeElapsedTicks,
            _frameHottestMeasureElementType,
            _frameHottestMeasureElementName,
            _frameHottestMeasureElapsedTicks,
            _frameHottestArrangeElementType,
            _frameHottestArrangeElementName,
            _frameHottestArrangeElapsedTicks);
    }

    internal static void ResetFrameTimingForTests()
    {
        _frameMeasureElapsedTicks = 0L;
        _frameMeasureExclusiveElapsedTicks = 0L;
        _frameArrangeElapsedTicks = 0L;
        _frameHottestMeasureElementType = "none";
        _frameHottestMeasureElementName = string.Empty;
        _frameHottestMeasureElapsedTicks = 0L;
        _frameHottestArrangeElementType = "none";
        _frameHottestArrangeElementName = string.Empty;
        _frameHottestArrangeElapsedTicks = 0L;
    }

    internal NameScope? GetLocalNameScope()
    {
        return _nameScope;
    }

    internal void EnsureNameScope()
    {
        _nameScope ??= new NameScope();
    }

    internal void RegisterNameInLocalScope(string name, object value)
    {
        EnsureNameScope();
        _nameScope!.RegisterName(name, value);
    }

    public object FindResource(object key)
    {
        if (TryFindResource(key, out var value))
        {
            return value!;
        }

        throw new KeyNotFoundException($"Resource key '{key}' was not found.");
    }

    public bool TryFindResource(object key, out object? resource)
    {
        return ResourceResolver.TryFindResource(this, key, out resource);
    }

    public void SetResourceReference(DependencyProperty dependencyProperty, object resourceKey)
    {
        _dynamicResourceBindings[dependencyProperty] = resourceKey;
        UpdateResourceBinding(dependencyProperty, resourceKey);
    }

    public void Measure(Vector2 availableSize)
    {
        Dispatcher.VerifyAccess();
        _measureCallCount++;
        var useLayoutRounding = UseLayoutRounding;
        var effectiveAvailableSize = useLayoutRounding
            ? RoundLayoutSize(availableSize)
            : availableSize;

        if (!IsVisible)
        {
            _measureWorkCount++;
            DesiredSize = new Vector2(
                0f,
                0f);
            _previousAvailableSize = effectiveAvailableSize;
            _isMeasureValid = true;
            ClearMeasureInvalidation();
            return;
        }

        if (_isMeasureValid && _previousAvailableSize == effectiveAvailableSize)
        {
            return;
        }

        if (_isMeasureValid &&
            CanReuseMeasureForAvailableSizeChange(_previousAvailableSize, effectiveAvailableSize))
        {
            _previousAvailableSize = effectiveAvailableSize;
            return;
        }

        _measureWorkCount++;
        _previousAvailableSize = effectiveAvailableSize;
        var previousDesiredSize = DesiredSize;
        var measureStart = Stopwatch.GetTimestamp();
        var measureChildTickStack = _activeMeasureChildTickStack ??= new List<long>();
        measureChildTickStack.Add(0L);

        try
        {
            var measureInvalidationCountBeforeOverride = MeasureInvalidationCount;
            var arrangeInvalidationCountBeforeOverride = ArrangeInvalidationCount;
            var margin = Margin;
            var innerAvailable = new Vector2(
                MathF.Max(0f, effectiveAvailableSize.X - margin.Horizontal),
                MathF.Max(0f, effectiveAvailableSize.Y - margin.Vertical));
            innerAvailable = ConstrainMeasureAvailableSize(innerAvailable);

            var measured = MeasureOverride(innerAvailable);
            measured = ApplyExplicitConstraints(measured);
            if (useLayoutRounding)
            {
                measured = RoundLayoutSize(measured);
            }

            var desired = new Vector2(
                measured.X + margin.Horizontal,
                measured.Y + margin.Vertical);
            if (useLayoutRounding)
            {
                desired = RoundLayoutSize(desired);
            }

            DesiredSize = desired;
            if (!AreSizesEqual(previousDesiredSize, DesiredSize) &&
                _measureCallCount > 1 &&
                VisualParent is FrameworkElement parent)
            {
                parent.InvalidateMeasure();
            }

            var invalidatedDuringMeasure =
                MeasureInvalidationCount != measureInvalidationCountBeforeOverride ||
                ArrangeInvalidationCount != arrangeInvalidationCountBeforeOverride;
            if (!invalidatedDuringMeasure)
            {
                _isMeasureValid = true;
                ClearMeasureInvalidation();
            }
        }
        finally
        {
            var totalMeasureTicks = Stopwatch.GetTimestamp() - measureStart;
            var lastIndex = measureChildTickStack.Count - 1;
            var childMeasureTicks = measureChildTickStack[lastIndex];
            measureChildTickStack.RemoveAt(lastIndex);

            _measureElapsedTicks += totalMeasureTicks;
            var exclusiveMeasureTicks = Math.Max(0L, totalMeasureTicks - childMeasureTicks);
            _measureExclusiveElapsedTicks += exclusiveMeasureTicks;
            _frameMeasureElapsedTicks += totalMeasureTicks;
            _frameMeasureExclusiveElapsedTicks += exclusiveMeasureTicks;
            if (totalMeasureTicks > _frameHottestMeasureElapsedTicks)
            {
                _frameHottestMeasureElapsedTicks = totalMeasureTicks;
                _frameHottestMeasureElementType = GetType().Name;
                _frameHottestMeasureElementName = Name;
            }

            if (measureChildTickStack.Count > 0)
            {
                measureChildTickStack[^1] += totalMeasureTicks;
            }
        }
    }

    public void Arrange(LayoutRect finalRect)
    {
        Dispatcher.VerifyAccess();
        _arrangeCallCount++;
        var useLayoutRounding = UseLayoutRounding;
        var effectiveFinalRect = useLayoutRounding
            ? RoundLayoutRect(finalRect)
            : finalRect;
        var arrangeAvailableSize = new Vector2(effectiveFinalRect.Width, effectiveFinalRect.Height);
        var requiresArrangeRemeasure =
            _isMeasureValid &&
            ShouldReMeasureForArrange(_previousAvailableSize, arrangeAvailableSize) &&
            !CanReuseMeasureForAvailableSizeChange(_previousAvailableSize, arrangeAvailableSize);

        if (_isArrangeValid &&
            _isMeasureValid &&
            AreRectsEqual(_arrangeRect, effectiveFinalRect) &&
            !requiresArrangeRemeasure)
        {
            return;
        }

        _arrangeWorkCount++;
        _arrangeRect = effectiveFinalRect;
        var arrangeStart = Stopwatch.GetTimestamp();

        if (!IsVisible)
        {
            SetLayoutSlot(effectiveFinalRect);
            RenderSize = Vector2.Zero;
            _isArrangeValid = true;
            ClearArrangeInvalidation();
            var invisibleArrangeTicks = Stopwatch.GetTimestamp() - arrangeStart;
            _arrangeElapsedTicks += invisibleArrangeTicks;
            _frameArrangeElapsedTicks += invisibleArrangeTicks;
            if (invisibleArrangeTicks > _frameHottestArrangeElapsedTicks)
            {
                _frameHottestArrangeElapsedTicks = invisibleArrangeTicks;
                _frameHottestArrangeElementType = GetType().Name;
                _frameHottestArrangeElementName = Name;
            }
            return;
        }

        if (!_isMeasureValid)
        {
            Measure(new Vector2(effectiveFinalRect.Width, effectiveFinalRect.Height));
        }
        else
        {
            if (ShouldReMeasureForArrange(_previousAvailableSize, arrangeAvailableSize) &&
                !CanReuseMeasureForAvailableSizeChange(_previousAvailableSize, arrangeAvailableSize))
            {
                var previousDesiredSize = DesiredSize;
                Measure(arrangeAvailableSize);
                if (!AreSizesEqual(previousDesiredSize, DesiredSize) &&
                    VisualParent is FrameworkElement parent)
                {
                    parent.InvalidateMeasure();
                }
            }
        }

        var margin = Margin;
        var clientX = effectiveFinalRect.X + margin.Left;
        var clientY = effectiveFinalRect.Y + margin.Top;
        var clientWidth = MathF.Max(0f, effectiveFinalRect.Width - margin.Horizontal);
        var clientHeight = MathF.Max(0f, effectiveFinalRect.Height - margin.Vertical);

        var arrangedWidth = ResolveAlignedSize(clientWidth, DesiredSize.X - margin.Horizontal, Width, HorizontalAlignment);
        var arrangedHeight = ResolveAlignedSize(clientHeight, DesiredSize.Y - margin.Vertical, Height, VerticalAlignment);

        arrangedWidth = Clamp(arrangedWidth, MinWidth, MaxWidth);
        arrangedHeight = Clamp(arrangedHeight, MinHeight, MaxHeight);

        var arrangedX = ResolveAlignedPosition(clientX, clientWidth, arrangedWidth, HorizontalAlignment);
        var arrangedY = ResolveAlignedPosition(clientY, clientHeight, arrangedHeight, VerticalAlignment);
        if (useLayoutRounding)
        {
            var roundedAlignedRect = RoundLayoutRect(new LayoutRect(arrangedX, arrangedY, arrangedWidth, arrangedHeight));
            arrangedX = roundedAlignedRect.X;
            arrangedY = roundedAlignedRect.Y;
            arrangedWidth = roundedAlignedRect.Width;
            arrangedHeight = roundedAlignedRect.Height;
        }

        // ArrangeOverride needs the final aligned origin for child layout decisions.
        SetLayoutSlot(new LayoutRect(arrangedX, arrangedY, arrangedWidth, arrangedHeight));
        var measureInvalidationCountBeforeOverride = MeasureInvalidationCount;
        var arrangeInvalidationCountBeforeOverride = ArrangeInvalidationCount;
        RenderSize = ArrangeOverride(new Vector2(arrangedWidth, arrangedHeight));
        if (useLayoutRounding)
        {
            RenderSize = RoundLayoutSize(RenderSize);
        }

        var finalLayoutSlot = new LayoutRect(arrangedX, arrangedY, RenderSize.X, RenderSize.Y);
        if (useLayoutRounding)
        {
            finalLayoutSlot = RoundLayoutRect(finalLayoutSlot);
            RenderSize = new Vector2(finalLayoutSlot.Width, finalLayoutSlot.Height);
        }

        SetLayoutSlot(finalLayoutSlot);

        var invalidatedDuringArrange =
            MeasureInvalidationCount != measureInvalidationCountBeforeOverride ||
            ArrangeInvalidationCount != arrangeInvalidationCountBeforeOverride;
        if (!invalidatedDuringArrange)
        {
            _isArrangeValid = true;
            ClearArrangeInvalidation();
            LayoutUpdated?.Invoke(this, EventArgs.Empty);
        }
        var arrangeTicks = Stopwatch.GetTimestamp() - arrangeStart;
        _arrangeElapsedTicks += arrangeTicks;
        _frameArrangeElapsedTicks += arrangeTicks;
        if (arrangeTicks > _frameHottestArrangeElapsedTicks)
        {
            _frameHottestArrangeElapsedTicks = arrangeTicks;
            _frameHottestArrangeElementType = GetType().Name;
            _frameHottestArrangeElementName = Name;
        }
    }

    public override void InvalidateMeasure()
    {
        Dispatcher.VerifyAccess();
        _isMeasureValid = false;
        base.InvalidateMeasure();
    }

    public override void InvalidateArrange()
    {
        Dispatcher.VerifyAccess();
        _isArrangeValid = false;
        base.InvalidateArrange();
    }

    public override void InvalidateVisual()
    {
        Dispatcher.VerifyAccess();
        base.InvalidateVisual();
    }

    internal void InvalidateArrangeForDirectLayoutOnly(bool invalidateRender = true)
    {
        _isArrangeValid = false;
        if (invalidateRender)
        {
            PrepareArrangeForDirectLayoutOnly();
            return;
        }

        PrepareArrangeForDirectLayoutWithoutRenderInvalidation();
    }

    public void UpdateLayout()
    {
        Dispatcher.VerifyAccess();
        const int maxPasses = 8;
        for (var pass = 0; pass < maxPasses; pass++)
        {
            if (!_isMeasureValid)
            {
                Measure(new Vector2(_arrangeRect.Width, _arrangeRect.Height));
            }

            if (!_isArrangeValid)
            {
                Arrange(_arrangeRect);
            }

            foreach (var child in GetVisualChildren())
            {
                if (child is FrameworkElement frameworkChild)
                {
                    frameworkChild.UpdateLayout();
                }
            }

            if (_isMeasureValid && _isArrangeValid)
            {
                return;
            }
        }
    }

    public void RaiseInitialized()
    {
        Dispatcher.VerifyAccess();
        Initialized?.Invoke(this, EventArgs.Empty);
    }

    public void RaiseLoaded()
    {
        Dispatcher.VerifyAccess();
        if (IsLoaded)
        {
            return;
        }

        IsLoaded = true;
        AttachResourceParent(VisualParent as FrameworkElement);
        UiApplication.Current.Resources.Changed += OnApplicationResourcesChanged;
        RefreshResourceBindings();
        UpdateImplicitStyle();
        Loaded?.Invoke(this, EventArgs.Empty);
    }

    public void RaiseUnloaded()
    {
        Dispatcher.VerifyAccess();
        if (!IsLoaded)
        {
            return;
        }

        IsLoaded = false;
        DetachResourceParent();
        UiApplication.Current.Resources.Changed -= OnApplicationResourcesChanged;
        Unloaded?.Invoke(this, EventArgs.Empty);
    }

    protected virtual Vector2 MeasureOverride(Vector2 availableSize)
    {
        return Vector2.Zero;
    }

    protected virtual bool CanReuseMeasureForAvailableSizeChange(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
    {
        _ = previousAvailableSize;
        _ = nextAvailableSize;
        return false;
    }

    internal bool CanReuseMeasureForAvailableSizeChangeForParentLayout(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
    {
        return CanReuseMeasureForAvailableSizeChange(previousAvailableSize, nextAvailableSize);
    }

    protected virtual Vector2 ArrangeOverride(Vector2 finalSize)
    {
        return finalSize;
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (ReferenceEquals(args.Property, IsVisibleProperty))
        {
            var visibilityMetadata = VisibilityProperty.GetMetadata(this);
            if (visibilityMetadata.VisibilityAffectsMeasure)
            {
                InvalidateMeasure();
            }
        }

        if (args.Property == StyleProperty)
        {
            if (!IsControlType() && !_isApplyingImplicitStyle)
            {
                _activeImplicitStyle = null;
            }

            if (args.OldValue is Style oldStyle)
            {
                oldStyle.Detach(this);
            }

            if (args.NewValue is Style newStyle)
            {
                newStyle.Apply(this);
            }
        }
    }

    protected override void OnVisualParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        base.OnVisualParentChanged(oldParent, newParent);

        var resourceScopeChanged = HasMaterialResourceScopeChange(oldParent, newParent);
        DetachResourceParent();
        AttachResourceParent(newParent as FrameworkElement);
        if (resourceScopeChanged)
        {
            RefreshResourceBindings();
            UpdateImplicitStyle();
            RaiseResourceScopeInvalidated();
        }
        if (oldParent is FrameworkElement oldFrameworkParent && oldFrameworkParent.IsLoaded && IsLoaded)
        {
            RaiseUnloaded();
        }

        if (newParent is FrameworkElement newFrameworkParent && newFrameworkParent.IsLoaded && !IsLoaded)
        {
            RaiseLoaded();
        }
    }

    protected override void OnLogicalParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        base.OnLogicalParentChanged(oldParent, newParent);

        if (VisualParent != null)
        {
            return;
        }

        var resourceScopeChanged = HasMaterialResourceScopeChange(oldParent, newParent);
        DetachResourceParent();
        AttachResourceParent(newParent as FrameworkElement);
        if (resourceScopeChanged)
        {
            RefreshResourceBindings();
            UpdateImplicitStyle();
            RaiseResourceScopeInvalidated();
        }
        if (oldParent is FrameworkElement oldFrameworkParent && oldFrameworkParent.IsLoaded && IsLoaded)
        {
            RaiseUnloaded();
        }

        if (newParent is FrameworkElement newFrameworkParent && newFrameworkParent.IsLoaded && !IsLoaded)
        {
            RaiseLoaded();
        }
    }

    private void RefreshResourceBindings()
    {
        foreach (var pair in _dynamicResourceBindings)
        {
            UpdateResourceBinding(pair.Key, pair.Value);
        }
    }

    private void UpdateResourceBinding(DependencyProperty dependencyProperty, object resourceKey)
    {
        if (TryFindResource(resourceKey, out var value))
        {
            SetValue(dependencyProperty, value);
        }
    }

    private void OnResourcesChanged(object? sender, ResourceDictionaryChangedEventArgs e)
    {
        RefreshResourceBindings();
        UpdateImplicitStyle();
        ResourceScopeInvalidated?.Invoke(this, EventArgs.Empty);
        NotifyDescendantResourcesChanged();
    }

    private void OnParentResourcesChanged(object? sender, ResourceDictionaryChangedEventArgs e)
    {
        RefreshResourceBindings();
        UpdateImplicitStyle();
        ResourceScopeInvalidated?.Invoke(this, EventArgs.Empty);
        NotifyDescendantResourcesChanged();
    }

    private void OnApplicationResourcesChanged(object? sender, ResourceDictionaryChangedEventArgs e)
    {
        RefreshResourceBindings();
        UpdateImplicitStyle();
        ResourceScopeInvalidated?.Invoke(this, EventArgs.Empty);
        NotifyDescendantResourcesChanged();
    }

    private void UpdateImplicitStyle()
    {
        if (IsControlType() || !ImplicitStylePolicy.ShouldApply(Style, _activeImplicitStyle))
        {
            return;
        }

        if (TryFindResource(GetType(), out var resource) && resource is Style style)
        {
            if (!ReferenceEquals(Style, style))
            {
                _isApplyingImplicitStyle = true;
                try
                {
                    Style = style;
                }
                finally
                {
                    _isApplyingImplicitStyle = false;
                }
            }

            _activeImplicitStyle = style;
            return;
        }

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
    }

    private bool IsControlType()
    {
        return this is Control;
    }

    private void AttachResourceParent(FrameworkElement? parent)
    {
        if (ReferenceEquals(_resourceParent, parent))
        {
            return;
        }

        if (_resourceParent != null)
        {
            _resourceParent.Resources.Changed -= OnParentResourcesChanged;
        }

        _resourceParent = parent;

        if (_resourceParent != null)
        {
            _resourceParent.Resources.Changed += OnParentResourcesChanged;
        }
    }

    private void DetachResourceParent()
    {
        if (_resourceParent == null)
        {
            return;
        }

        _resourceParent.Resources.Changed -= OnParentResourcesChanged;
        _resourceParent = null;
    }

    private void NotifyDescendantResourcesChanged()
    {
        foreach (var child in GetVisualChildren())
        {
            if (child is FrameworkElement frameworkChild)
            {
                if (frameworkChild.RequiresDirectResourceScopeRefresh())
                {
                    frameworkChild.RefreshResourceBindings();
                    frameworkChild.RaiseResourceScopeInvalidated();
                }

                frameworkChild.NotifyDescendantResourcesChanged();
            }
        }
    }

    private bool RequiresDirectResourceScopeRefresh()
    {
        return _dynamicResourceBindings.Count > 0 ||
               HasResourceScopeInvalidatedSubscribers() ||
               ShouldRefreshImplicitStyleFromResourceScope();
    }

    private bool HasResourceScopeInvalidatedSubscribers()
    {
        return ResourceScopeInvalidated != null;
    }

    private void RaiseResourceScopeInvalidated()
    {
        ResourceScopeInvalidated?.Invoke(this, EventArgs.Empty);
    }

    private bool ShouldRefreshImplicitStyleFromResourceScope()
    {
        return !IsControlType() && ImplicitStylePolicy.ShouldApply(Style, _activeImplicitStyle);
    }

    private static bool HasMaterialResourceScopeChange(UIElement? oldParent, UIElement? newParent)
    {
        var oldAncestors = GetEffectiveResourceAncestors(oldParent);
        var newAncestors = GetEffectiveResourceAncestors(newParent);
        if (oldAncestors.Count != newAncestors.Count)
        {
            return true;
        }

        for (var index = 0; index < oldAncestors.Count; index++)
        {
            if (!ReferenceEquals(oldAncestors[index], newAncestors[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static List<FrameworkElement> GetEffectiveResourceAncestors(UIElement? parent)
    {
        var ancestors = new List<FrameworkElement>();
        for (var current = parent; current != null; current = current.VisualParent)
        {
            if (current is not FrameworkElement frameworkElement)
            {
                continue;
            }

            if (!HasMaterialResources(frameworkElement.Resources))
            {
                continue;
            }

            ancestors.Add(frameworkElement);
        }

        return ancestors;
    }

    private static bool HasMaterialResources(ResourceDictionary resources)
    {
        return resources.Count > 0 || resources.MergedDictionaries.Count > 0;
    }

    private static float ResolveAlignedSize(
        float available,
        float desired,
        float explicitSize,
        HorizontalAlignment alignment)
    {
        if (!float.IsNaN(explicitSize))
        {
            return explicitSize;
        }

        if (alignment == HorizontalAlignment.Stretch)
        {
            return available;
        }

        return MathF.Min(available, MathF.Max(0f, desired));
    }

    private static float ResolveAlignedSize(
        float available,
        float desired,
        float explicitSize,
        VerticalAlignment alignment)
    {
        if (!float.IsNaN(explicitSize))
        {
            return explicitSize;
        }

        if (alignment == VerticalAlignment.Stretch)
        {
            return available;
        }

        return MathF.Min(available, MathF.Max(0f, desired));
    }

    private static float ResolveAlignedPosition(float start, float available, float size, HorizontalAlignment alignment)
    {
        return alignment switch
        {
            HorizontalAlignment.Center => start + ((available - size) / 2f),
            HorizontalAlignment.Right => start + (available - size),
            _ => start
        };
    }

    private static float ResolveAlignedPosition(float start, float available, float size, VerticalAlignment alignment)
    {
        return alignment switch
        {
            VerticalAlignment.Center => start + ((available - size) / 2f),
            VerticalAlignment.Bottom => start + (available - size),
            _ => start
        };
    }

    private Vector2 ApplyExplicitConstraints(Vector2 measured)
    {
        var width = float.IsNaN(Width) ? measured.X : Width;
        var height = float.IsNaN(Height) ? measured.Y : Height;

        width = Clamp(width, MinWidth, MaxWidth);
        height = Clamp(height, MinHeight, MaxHeight);

        return new Vector2(width, height);
    }

    private Vector2 ConstrainMeasureAvailableSize(Vector2 available)
    {
        return new Vector2(
            ResolveMeasureConstraint(available.X, Width, MinWidth, MaxWidth),
            ResolveMeasureConstraint(available.Y, Height, MinHeight, MaxHeight));
    }

    private static float ResolveMeasureConstraint(float available, float explicitSize, float min, float max)
    {
        var constraint = float.IsNaN(explicitSize) ? available : explicitSize;

        if (float.IsFinite(max))
        {
            constraint = MathF.Min(constraint, max);
        }

        if (float.IsFinite(constraint))
        {
            constraint = MathF.Max(constraint, min);
        }

        return constraint;
    }

    private static float Clamp(float value, float min, float max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private static bool AreRectsEqual(LayoutRect left, LayoutRect right)
    {
        const float epsilon = 0.0001f;
        return MathF.Abs(left.X - right.X) <= epsilon &&
               MathF.Abs(left.Y - right.Y) <= epsilon &&
               MathF.Abs(left.Width - right.Width) <= epsilon &&
               MathF.Abs(left.Height - right.Height) <= epsilon;
    }

    private static bool AreSizesEqual(Vector2 left, Vector2 right)
    {
        const float epsilon = 0.0001f;
        return MathF.Abs(left.X - right.X) <= epsilon &&
               MathF.Abs(left.Y - right.Y) <= epsilon;
    }

    private static bool ShouldReMeasureForArrange(Vector2 measuredAvailableSize, Vector2 arrangedAvailableSize)
    {
        return IsFiniteShrink(measuredAvailableSize.X, arrangedAvailableSize.X) ||
               IsFiniteShrink(measuredAvailableSize.Y, arrangedAvailableSize.Y);
    }

    private static bool IsFiniteShrink(float previous, float next)
    {
        return float.IsFinite(previous) &&
               float.IsFinite(next) &&
               next + 0.0001f < previous;
    }

    private static LayoutRect RoundLayoutRect(LayoutRect rect)
    {
        if (!IsFinite(rect.X) || !IsFinite(rect.Y) || !IsFinite(rect.Width) || !IsFinite(rect.Height))
        {
            return new LayoutRect(
                RoundLayoutScalar(rect.X),
                RoundLayoutScalar(rect.Y),
                RoundLayoutScalar(rect.Width),
                RoundLayoutScalar(rect.Height));
        }

        var left = RoundLayoutScalar(rect.X);
        var top = RoundLayoutScalar(rect.Y);
        var right = RoundLayoutScalar(rect.X + rect.Width);
        var bottom = RoundLayoutScalar(rect.Y + rect.Height);
        return new LayoutRect(
            left,
            top,
            MathF.Max(0f, right - left),
            MathF.Max(0f, bottom - top));
    }

    private static Vector2 RoundLayoutSize(Vector2 size)
    {
        return new Vector2(
            RoundLayoutSizeScalar(size.X),
            RoundLayoutSizeScalar(size.Y));
    }

    private static float RoundLayoutSizeScalar(float value)
    {
        if (!IsFinite(value))
        {
            return value;
        }

        return MathF.Max(0f, MathF.Round(value));
    }

    private static float RoundLayoutScalar(float value)
    {
        if (!IsFinite(value))
        {
            return value;
        }

        return MathF.Round(value);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

}

internal readonly record struct FrameworkLayoutTimingSnapshot(
    long MeasureElapsedTicks,
    long MeasureExclusiveElapsedTicks,
    long ArrangeElapsedTicks,
    string HottestMeasureElementType,
    string HottestMeasureElementName,
    long HottestMeasureElapsedTicks,
    string HottestArrangeElementType,
    string HottestArrangeElementName,
    long HottestArrangeElapsedTicks);
