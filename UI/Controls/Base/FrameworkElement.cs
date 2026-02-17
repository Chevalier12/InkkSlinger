using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class FrameworkElement : UIElement
{
    private readonly Dictionary<DependencyProperty, object> _dynamicResourceBindings = new();
    private FrameworkElement? _resourceParent;
    private NameScope? _nameScope;

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

    private bool _isMeasureValid;
    private bool _isArrangeValid;
    private Vector2 _previousAvailableSize = new(float.NaN, float.NaN);
    private LayoutRect _arrangeRect;

    public FrameworkElement()
    {
        Resources.Changed += OnResourcesChanged;
    }

    public event EventHandler? Initialized;

    public event EventHandler? Loaded;

    public event EventHandler? Unloaded;

    public event EventHandler? LayoutUpdated;

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

    public ResourceDictionary Resources { get; } = new();

    public Vector2 DesiredSize { get; private set; }

    public Vector2 RenderSize { get; private set; }

    public float ActualWidth => RenderSize.X;

    public float ActualHeight => RenderSize.Y;

    public bool IsLoaded { get; private set; }

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
        if (!IsVisible)
        {
            DesiredSize = Vector2.Zero;
            _previousAvailableSize = availableSize;
            _isMeasureValid = true;
            ClearMeasureInvalidation();
            return;
        }

        if (_isMeasureValid && _previousAvailableSize == availableSize)
        {
            return;
        }

        _previousAvailableSize = availableSize;

        var margin = Margin;
        var innerAvailable = new Vector2(
            MathF.Max(0f, availableSize.X - margin.Horizontal),
            MathF.Max(0f, availableSize.Y - margin.Vertical));

        var measured = MeasureOverride(innerAvailable);
        measured = ApplyExplicitConstraints(measured);

        DesiredSize = new Vector2(
            measured.X + margin.Horizontal,
            measured.Y + margin.Vertical);

        _isMeasureValid = true;
        ClearMeasureInvalidation();
    }

    public void Arrange(LayoutRect finalRect)
    {
        Dispatcher.VerifyAccess();
        if (_isArrangeValid &&
            _isMeasureValid &&
            AreRectsEqual(_arrangeRect, finalRect))
        {
            return;
        }

        _arrangeRect = finalRect;

        if (!IsVisible)
        {
            SetLayoutSlot(finalRect);
            RenderSize = Vector2.Zero;
            _isArrangeValid = true;
            ClearArrangeInvalidation();
            return;
        }

        if (!_isMeasureValid)
        {
            Measure(new Vector2(finalRect.Width, finalRect.Height));
        }

        var margin = Margin;
        var clientX = finalRect.X + margin.Left;
        var clientY = finalRect.Y + margin.Top;
        var clientWidth = MathF.Max(0f, finalRect.Width - margin.Horizontal);
        var clientHeight = MathF.Max(0f, finalRect.Height - margin.Vertical);

        var arrangedWidth = ResolveAlignedSize(clientWidth, DesiredSize.X - margin.Horizontal, Width, HorizontalAlignment);
        var arrangedHeight = ResolveAlignedSize(clientHeight, DesiredSize.Y - margin.Vertical, Height, VerticalAlignment);

        arrangedWidth = Clamp(arrangedWidth, MinWidth, MaxWidth);
        arrangedHeight = Clamp(arrangedHeight, MinHeight, MaxHeight);

        var arrangedX = ResolveAlignedPosition(clientX, clientWidth, arrangedWidth, HorizontalAlignment);
        var arrangedY = ResolveAlignedPosition(clientY, clientHeight, arrangedHeight, VerticalAlignment);

        // ArrangeOverride needs the final aligned origin for child layout decisions.
        SetLayoutSlot(new LayoutRect(arrangedX, arrangedY, arrangedWidth, arrangedHeight));
        RenderSize = ArrangeOverride(new Vector2(arrangedWidth, arrangedHeight));
        SetLayoutSlot(new LayoutRect(arrangedX, arrangedY, RenderSize.X, RenderSize.Y));

        _isArrangeValid = true;
        ClearArrangeInvalidation();
        LayoutUpdated?.Invoke(this, EventArgs.Empty);
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

    public void UpdateLayout()
    {
        Dispatcher.VerifyAccess();
        if (_isMeasureValid && _isArrangeValid)
        {
            return;
        }

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

    protected virtual Vector2 ArrangeOverride(Vector2 finalSize)
    {
        return finalSize;
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (ReferenceEquals(args.Property, IsVisibleProperty))
        {
            InvalidateMeasure();
        }

        if (args.Property == StyleProperty)
        {
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
        var totalStart = Stopwatch.GetTimestamp();
        base.OnVisualParentChanged(oldParent, newParent);

        var phaseStart = Stopwatch.GetTimestamp();
        DetachResourceParent();
        AttachResourceParent(newParent as FrameworkElement);
        RefreshResourceBindings();
        UiFrameworkPopulationPhaseDiagnostics.Observe("FrameworkElement.OnVisualParentChanged.Resources", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);

        phaseStart = Stopwatch.GetTimestamp();
        if (oldParent is FrameworkElement oldFrameworkParent && oldFrameworkParent.IsLoaded && IsLoaded)
        {
            RaiseUnloaded();
        }

        if (newParent is FrameworkElement newFrameworkParent && newFrameworkParent.IsLoaded && !IsLoaded)
        {
            RaiseLoaded();
        }
        UiFrameworkPopulationPhaseDiagnostics.Observe("FrameworkElement.OnVisualParentChanged.LoadState", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);
        UiFrameworkPopulationPhaseDiagnostics.Observe("FrameworkElement.OnVisualParentChanged.Total", Stopwatch.GetElapsedTime(totalStart).TotalMilliseconds);
    }

    protected override void OnLogicalParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        var totalStart = Stopwatch.GetTimestamp();
        base.OnLogicalParentChanged(oldParent, newParent);

        if (VisualParent != null)
        {
            return;
        }

        var phaseStart = Stopwatch.GetTimestamp();
        DetachResourceParent();
        AttachResourceParent(newParent as FrameworkElement);
        RefreshResourceBindings();
        UiFrameworkPopulationPhaseDiagnostics.Observe("FrameworkElement.OnLogicalParentChanged.Resources", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);

        phaseStart = Stopwatch.GetTimestamp();
        if (oldParent is FrameworkElement oldFrameworkParent && oldFrameworkParent.IsLoaded && IsLoaded)
        {
            RaiseUnloaded();
        }

        if (newParent is FrameworkElement newFrameworkParent && newFrameworkParent.IsLoaded && !IsLoaded)
        {
            RaiseLoaded();
        }
        UiFrameworkPopulationPhaseDiagnostics.Observe("FrameworkElement.OnLogicalParentChanged.LoadState", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);
        UiFrameworkPopulationPhaseDiagnostics.Observe("FrameworkElement.OnLogicalParentChanged.Total", Stopwatch.GetElapsedTime(totalStart).TotalMilliseconds);
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
        NotifyDescendantResourcesChanged();
    }

    private void OnParentResourcesChanged(object? sender, ResourceDictionaryChangedEventArgs e)
    {
        RefreshResourceBindings();
        NotifyDescendantResourcesChanged();
    }

    private void OnApplicationResourcesChanged(object? sender, ResourceDictionaryChangedEventArgs e)
    {
        RefreshResourceBindings();
        NotifyDescendantResourcesChanged();
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
                frameworkChild.RefreshResourceBindings();
                frameworkChild.NotifyDescendantResourcesChanged();
            }
        }
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

}
