using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class UIElement : DependencyObject
{
    private const int RoutePoolMaxSize = 64;

    [ThreadStatic]
    private static Stack<List<UIElement>>? _routePool;
    private static readonly object InheritablePropertyCacheLock = new();
    private static readonly Dictionary<Type, List<DependencyProperty>> InheritablePropertiesByType = new();

    private readonly Dictionary<RoutedEvent, List<RoutedHandlerEntry>> _routedHandlers = new();
    private readonly List<CommandBinding> _commandBindings = new();
    private readonly List<InputBinding> _inputBindings = new();
    private LayoutRect _layoutSlot;
    private int _measureInvalidationCount;
    private int _arrangeInvalidationCount;
    private int _renderInvalidationCount;
    private int _renderCacheRenderVersion;
    private int _renderCacheLayoutVersion;
    private bool _suppressNextLogicalBindingTreeNotify;
    private bool _suppressNextLogicalParentChanged;

    public static readonly DependencyProperty IsVisibleProperty =
        DependencyProperty.Register(
            nameof(IsVisible),
            typeof(bool),
            typeof(UIElement),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.Register(
            nameof(IsEnabled),
            typeof(bool),
            typeof(UIElement),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty IsHitTestVisibleProperty =
        DependencyProperty.Register(
            nameof(IsHitTestVisible),
            typeof(bool),
            typeof(UIElement),
            new FrameworkPropertyMetadata(true));

    public static readonly DependencyProperty OpacityProperty =
        DependencyProperty.Register(
            nameof(Opacity),
            typeof(float),
            typeof(UIElement),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) =>
                {
                    var opacity = value is float v ? v : 1f;
                    if (opacity < 0f)
                    {
                        return 0f;
                    }

                    if (opacity > 1f)
                    {
                        return 1f;
                    }

                    return opacity;
                }));

    public static readonly RoutedEvent PreviewMouseMoveEvent = new(nameof(PreviewMouseMoveEvent), RoutingStrategy.Tunnel);
    public static readonly RoutedEvent MouseMoveEvent = new(nameof(MouseMoveEvent), RoutingStrategy.Bubble);
    public static readonly RoutedEvent PreviewMouseDownEvent = new(nameof(PreviewMouseDownEvent), RoutingStrategy.Tunnel);
    public static readonly RoutedEvent MouseDownEvent = new(nameof(MouseDownEvent), RoutingStrategy.Bubble);
    public static readonly RoutedEvent PreviewMouseUpEvent = new(nameof(PreviewMouseUpEvent), RoutingStrategy.Tunnel);
    public static readonly RoutedEvent MouseUpEvent = new(nameof(MouseUpEvent), RoutingStrategy.Bubble);
    public static readonly RoutedEvent PreviewMouseWheelEvent = new(nameof(PreviewMouseWheelEvent), RoutingStrategy.Tunnel);
    public static readonly RoutedEvent MouseWheelEvent = new(nameof(MouseWheelEvent), RoutingStrategy.Bubble);
    public static readonly RoutedEvent PreviewKeyDownEvent = new(nameof(PreviewKeyDownEvent), RoutingStrategy.Tunnel);
    public static readonly RoutedEvent KeyDownEvent = new(nameof(KeyDownEvent), RoutingStrategy.Bubble);
    public static readonly RoutedEvent PreviewKeyUpEvent = new(nameof(PreviewKeyUpEvent), RoutingStrategy.Tunnel);
    public static readonly RoutedEvent KeyUpEvent = new(nameof(KeyUpEvent), RoutingStrategy.Bubble);
    public static readonly RoutedEvent PreviewTextInputEvent = new(nameof(PreviewTextInputEvent), RoutingStrategy.Tunnel);
    public static readonly RoutedEvent TextInputEvent = new(nameof(TextInputEvent), RoutingStrategy.Bubble);
    public static readonly RoutedEvent GotFocusEvent = new(nameof(GotFocusEvent), RoutingStrategy.Bubble);
    public static readonly RoutedEvent LostFocusEvent = new(nameof(LostFocusEvent), RoutingStrategy.Bubble);

    public UIElement? VisualParent { get; private set; }

    public UIElement? LogicalParent { get; private set; }

    public IList<CommandBinding> CommandBindings => _commandBindings;

    public IList<InputBinding> InputBindings => _inputBindings;

    public bool IsVisible
    {
        get => GetValue<bool>(IsVisibleProperty);
        set => SetValue(IsVisibleProperty, value);
    }

    public bool IsEnabled
    {
        get => GetValue<bool>(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }

    public bool IsHitTestVisible
    {
        get => GetValue<bool>(IsHitTestVisibleProperty);
        set => SetValue(IsHitTestVisibleProperty, value);
    }

    public float Opacity
    {
        get => GetValue<float>(OpacityProperty);
        set => SetValue(OpacityProperty, value);
    }

    public LayoutRect LayoutSlot => _layoutSlot;

    public bool NeedsMeasure { get; private set; }

    public bool NeedsArrange { get; private set; }

    public bool NeedsRender { get; private set; }

    public bool SubtreeDirty { get; private set; }

    public int MeasureInvalidationCount => _measureInvalidationCount;

    public int ArrangeInvalidationCount => _arrangeInvalidationCount;

    public int RenderInvalidationCount => _renderInvalidationCount;

    internal int RenderCacheRenderVersion => _renderCacheRenderVersion;

    internal int RenderCacheLayoutVersion => _renderCacheLayoutVersion;

    public virtual IEnumerable<UIElement> GetVisualChildren()
    {
        yield break;
    }

    public virtual IEnumerable<UIElement> GetLogicalChildren()
    {
        yield break;
    }

    internal virtual bool TryGetRenderBoundsInRootSpace(out LayoutRect bounds)
    {
        var slot = LayoutSlot;
        if (slot.Width <= 0f || slot.Height <= 0f)
        {
            bounds = slot;
            return false;
        }

        if (!TryGetTransformFromThisToRoot(out var transform))
        {
            bounds = slot;
            return true;
        }

        var topLeft = Vector2.Transform(new Vector2(slot.X, slot.Y), transform);
        var topRight = Vector2.Transform(new Vector2(slot.X + slot.Width, slot.Y), transform);
        var bottomLeft = Vector2.Transform(new Vector2(slot.X, slot.Y + slot.Height), transform);
        var bottomRight = Vector2.Transform(new Vector2(slot.X + slot.Width, slot.Y + slot.Height), transform);

        var minX = MathF.Min(MathF.Min(topLeft.X, topRight.X), MathF.Min(bottomLeft.X, bottomRight.X));
        var minY = MathF.Min(MathF.Min(topLeft.Y, topRight.Y), MathF.Min(bottomLeft.Y, bottomRight.Y));
        var maxX = MathF.Max(MathF.Max(topLeft.X, topRight.X), MathF.Max(bottomLeft.X, bottomRight.X));
        var maxY = MathF.Max(MathF.Max(topLeft.Y, topRight.Y), MathF.Max(bottomLeft.Y, bottomRight.Y));

        bounds = new LayoutRect(minX, minY, MathF.Max(0f, maxX - minX), MathF.Max(0f, maxY - minY));
        return bounds.Width > 0f && bounds.Height > 0f;
    }

    internal UIElement GetVisualRoot()
    {
        UIElement current = this;
        while (current.VisualParent != null)
        {
            current = current.VisualParent;
        }

        return current;
    }

    public virtual void Update(GameTime gameTime)
    {
        foreach (var child in GetVisualChildren())
        {
            child.Update(gameTime);
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (!IsVisible)
        {
            return;
        }

        var hasClip = TryGetClipRect(out var clipRect);
        var hasTransform = TryGetLocalRenderTransform(out var localTransform, out _);
        if (hasClip)
        {
            UiDrawing.PushClip(spriteBatch, clipRect);
        }

        if (hasTransform)
        {
            UiDrawing.PushTransform(spriteBatch, localTransform);
        }

        try
        {
            OnRender(spriteBatch);
            var currentClip = spriteBatch.GraphicsDevice.ScissorRectangle;

            foreach (var child in GetVisualChildren())
            {
                if (child.TryGetRenderBoundsInRootSpace(out var childBounds) &&
                    !Intersects(childBounds, currentClip))
                {
                    continue;
                }

                child.Draw(spriteBatch);
            }
        }
        finally
        {
            if (hasTransform)
            {
                UiDrawing.PopTransform(spriteBatch);
            }

            if (hasClip)
            {
                UiDrawing.PopClip(spriteBatch);
            }
        }
    }

    internal void DrawSelf(SpriteBatch spriteBatch)
    {
        OnRender(spriteBatch);
    }

    protected virtual void OnRender(SpriteBatch spriteBatch)
    {
    }

    protected virtual bool TryGetClipRect(out LayoutRect clipRect)
    {
        clipRect = default;
        return false;
    }

    protected virtual bool TryGetLocalRenderTransform(out Matrix transform, out Matrix inverseTransform)
    {
        transform = Matrix.Identity;
        inverseTransform = Matrix.Identity;
        return false;
    }

    internal bool HasLocalRenderTransform()
    {
        return TryGetLocalRenderTransform(out _, out _);
    }

    internal bool TryGetLocalClipSnapshot(out LayoutRect clipRect)
    {
        return TryGetClipRect(out clipRect);
    }

    internal bool TryGetLocalRenderTransformSnapshot(out Matrix transform)
    {
        return TryGetLocalRenderTransform(out transform, out _);
    }

    public virtual void InvalidateMeasure()
    {
        var totalStart = Stopwatch.GetTimestamp();
        Dispatcher.VerifyAccess();
        if (NeedsMeasure)
        {
            return;
        }

        var phaseStart = Stopwatch.GetTimestamp();
        NeedsMeasure = true;
        _measureInvalidationCount++;
        _renderCacheLayoutVersion++;
        MarkSubtreeDirty();
        UiFrameworkPopulationPhaseDiagnostics.Observe("UIElement.InvalidateMeasure.Mark", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);

        phaseStart = Stopwatch.GetTimestamp();
        UiRoot.Current?.NotifyInvalidation(UiInvalidationType.Measure, this);
        UiFrameworkPopulationPhaseDiagnostics.Observe("UIElement.InvalidateMeasure.NotifyRoot", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);

        phaseStart = Stopwatch.GetTimestamp();
        InvalidateArrange();
        UiFrameworkPopulationPhaseDiagnostics.Observe("UIElement.InvalidateMeasure.CascadeArrange", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);

        phaseStart = Stopwatch.GetTimestamp();
        GetInvalidationParent()?.InvalidateMeasure();
        UiFrameworkPopulationPhaseDiagnostics.Observe("UIElement.InvalidateMeasure.PropagateParent", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);
        UiFrameworkPopulationPhaseDiagnostics.Observe("UIElement.InvalidateMeasure.Total", Stopwatch.GetElapsedTime(totalStart).TotalMilliseconds);
    }

    public virtual void InvalidateArrange()
    {
        var totalStart = Stopwatch.GetTimestamp();
        Dispatcher.VerifyAccess();
        if (NeedsArrange)
        {
            return;
        }

        var phaseStart = Stopwatch.GetTimestamp();
        NeedsArrange = true;
        _arrangeInvalidationCount++;
        _renderCacheLayoutVersion++;
        MarkSubtreeDirty();
        UiFrameworkPopulationPhaseDiagnostics.Observe("UIElement.InvalidateArrange.Mark", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);

        phaseStart = Stopwatch.GetTimestamp();
        UiRoot.Current?.NotifyInvalidation(UiInvalidationType.Arrange, this);
        UiFrameworkPopulationPhaseDiagnostics.Observe("UIElement.InvalidateArrange.NotifyRoot", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);

        phaseStart = Stopwatch.GetTimestamp();
        InvalidateVisual();
        UiFrameworkPopulationPhaseDiagnostics.Observe("UIElement.InvalidateArrange.CascadeVisual", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);

        phaseStart = Stopwatch.GetTimestamp();
        GetInvalidationParent()?.InvalidateArrange();
        UiFrameworkPopulationPhaseDiagnostics.Observe("UIElement.InvalidateArrange.PropagateParent", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);
        UiFrameworkPopulationPhaseDiagnostics.Observe("UIElement.InvalidateArrange.Total", Stopwatch.GetElapsedTime(totalStart).TotalMilliseconds);
    }

    public virtual void InvalidateVisual()
    {
        Dispatcher.VerifyAccess();
        if (NeedsRender)
        {
            return;
        }

        NeedsRender = true;
        _renderInvalidationCount++;
        _renderCacheRenderVersion++;
        MarkSubtreeDirty();
        UiRoot.Current?.NotifyInvalidation(UiInvalidationType.Render, this);
    }

    public void InvalidateRender()
    {
        InvalidateVisual();
    }

    public virtual bool HitTest(Vector2 point)
    {
        if (!IsVisible || !IsEnabled || !IsHitTestVisible)
        {
            return false;
        }

        if (!IsPointVisibleThroughClipChain(point))
        {
            return false;
        }

        var probePoint = point;
        if (TryGetTransformFromRootToThisInverse(out var inverseTransform))
        {
            probePoint = Vector2.Transform(point, inverseTransform);
        }

        return probePoint.X >= _layoutSlot.X &&
               probePoint.X <= _layoutSlot.X + _layoutSlot.Width &&
               probePoint.Y >= _layoutSlot.Y &&
               probePoint.Y <= _layoutSlot.Y + _layoutSlot.Height;
    }

    protected bool IsPointVisibleThroughClipChain(Vector2 point)
    {
        var accumulatedTransform = Matrix.Identity;
        return IsPointVisibleThroughClipChain(this, point, ref accumulatedTransform);
    }

    public void AddHandler<TArgs>(RoutedEvent routedEvent, EventHandler<TArgs> handler)
        where TArgs : RoutedEventArgs
    {
        AddHandler(routedEvent, handler, handledEventsToo: false);
    }

    public void AddHandler<TArgs>(
        RoutedEvent routedEvent,
        EventHandler<TArgs> handler,
        bool handledEventsToo)
        where TArgs : RoutedEventArgs
    {
        if (!_routedHandlers.TryGetValue(routedEvent, out var handlers))
        {
            handlers = new List<RoutedHandlerEntry>();
            _routedHandlers[routedEvent] = handlers;
        }

        handlers.Add(new RoutedHandlerEntry(
            handler,
            (sender, routedArgs) =>
            {
                if (routedArgs is TArgs typedArgs)
                {
                    handler(sender, typedArgs);
                }
            },
            handledEventsToo));
    }

    public void RemoveHandler<TArgs>(RoutedEvent routedEvent, EventHandler<TArgs> handler)
        where TArgs : RoutedEventArgs
    {
        if (!_routedHandlers.TryGetValue(routedEvent, out var handlers))
        {
            return;
        }

        for (var i = handlers.Count - 1; i >= 0; i--)
        {
            if (handlers[i].OriginalHandler == (Delegate)handler)
            {
                handlers.RemoveAt(i);
                break;
            }
        }
    }

    internal void SetVisualParent(UIElement? parent)
    {
        var totalStart = Stopwatch.GetTimestamp();
        if (ReferenceEquals(VisualParent, parent))
        {
            return;
        }

        var phaseStart = Stopwatch.GetTimestamp();
        var oldParent = VisualParent;
        VisualParent = parent;
        UiFrameworkPopulationPhaseDiagnostics.Observe("UIElement.SetVisualParent.Assign", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);

        phaseStart = Stopwatch.GetTimestamp();
        OnVisualParentChanged(oldParent, parent);
        UiFrameworkPopulationPhaseDiagnostics.Observe("UIElement.SetVisualParent.OnVisualParentChanged", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);

        phaseStart = Stopwatch.GetTimestamp();
        UiRoot.Current?.NotifyVisualStructureChanged(this, oldParent, parent);
        UiFrameworkPopulationPhaseDiagnostics.Observe("UIElement.SetVisualParent.NotifyVisualStructureChanged", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);

        phaseStart = Stopwatch.GetTimestamp();
        NotifyBindingTreeChanged(this);
        UiFrameworkPopulationPhaseDiagnostics.Observe("UIElement.SetVisualParent.NotifyBindingTreeChanged", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);
        _suppressNextLogicalBindingTreeNotify = true;
        _suppressNextLogicalParentChanged = true;
        UiFrameworkPopulationPhaseDiagnostics.Observe("UIElement.SetVisualParent.Total", Stopwatch.GetElapsedTime(totalStart).TotalMilliseconds);
    }

    internal void SetLogicalParent(UIElement? parent)
    {
        var totalStart = Stopwatch.GetTimestamp();
        if (ReferenceEquals(LogicalParent, parent))
        {
            return;
        }

        var phaseStart = Stopwatch.GetTimestamp();
        var oldParent = LogicalParent;
        LogicalParent = parent;
        UiFrameworkPopulationPhaseDiagnostics.Observe("UIElement.SetLogicalParent.Assign", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);

        phaseStart = Stopwatch.GetTimestamp();
        var shouldRunLogicalParentChanged = !(ReferenceEquals(parent, VisualParent) && _suppressNextLogicalParentChanged);
        if (shouldRunLogicalParentChanged)
        {
            OnLogicalParentChanged(oldParent, parent);
        }

        _suppressNextLogicalParentChanged = false;
        UiFrameworkPopulationPhaseDiagnostics.Observe("UIElement.SetLogicalParent.OnLogicalParentChanged", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);

        phaseStart = Stopwatch.GetTimestamp();
        var shouldNotifyBindingTree = !ReferenceEquals(parent, VisualParent) && !_suppressNextLogicalBindingTreeNotify;
        if (shouldNotifyBindingTree)
        {
            NotifyBindingTreeChanged(this);
        }

        _suppressNextLogicalBindingTreeNotify = false;
        UiFrameworkPopulationPhaseDiagnostics.Observe(
            "UIElement.SetLogicalParent.NotifyBindingTreeChanged",
            Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds,
            shouldNotifyBindingTree ? 1 : 0);
        UiFrameworkPopulationPhaseDiagnostics.Observe("UIElement.SetLogicalParent.Total", Stopwatch.GetElapsedTime(totalStart).TotalMilliseconds);
    }

    internal void SetLayoutSlot(LayoutRect layoutSlot)
    {
        if (!AreRectsEqual(_layoutSlot, layoutSlot))
        {
            _renderCacheLayoutVersion++;
        }

        _layoutSlot = layoutSlot;
    }

    protected virtual void OnVisualParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        var phaseStart = Stopwatch.GetTimestamp();
        var inheritableProperties = new[]
        {
            UIElement.IsEnabledProperty,
            FrameworkElement.DataContextProperty,
            FrameworkElement.BindingGroupProperty
        };
        UiFrameworkPopulationPhaseDiagnostics.Observe("UIElement.OnVisualParentChanged.GetRegisteredProperties", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds, inheritableProperties.Length);

        phaseStart = Stopwatch.GetTimestamp();
        var propagatedCount = 0;
        foreach (var property in inheritableProperties)
        {
            if (!property.IsApplicableTo(this))
            {
                continue;
            }

            var metadata = property.GetMetadata(this);
            object? oldInheritedValue;
            object? newInheritedValue;
            if (oldParent == null)
            {
                oldInheritedValue = metadata.DefaultValue;
                newInheritedValue = ResolveInheritedValueFromParentChain(newParent, property);
            }
            else if (newParent == null)
            {
                oldInheritedValue = ResolveInheritedValueFromParentChain(oldParent, property);
                newInheritedValue = metadata.DefaultValue;
            }
            else
            {
                oldInheritedValue = ResolveInheritedValueFromParentChain(oldParent, property);
                newInheritedValue = ResolveInheritedValueFromParentChain(newParent, property);
            }

            if (!Equals(oldInheritedValue, newInheritedValue))
            {
                NotifyInheritedPropertyChanged(property);
                propagatedCount++;
            }
        }
        UiFrameworkPopulationPhaseDiagnostics.Observe("UIElement.OnVisualParentChanged.PropagateInherited", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds, propagatedCount);
    }

    protected virtual void OnLogicalParentChanged(UIElement? oldParent, UIElement? newParent)
    {
    }

    protected void RaiseRoutedEvent(RoutedEvent routedEvent, RoutedEventArgs args)
    {
        args.OriginalSource ??= this;
        if (routedEvent.RoutingStrategy == RoutingStrategy.Direct)
        {
            InvokeRoutedEvent(this, routedEvent, args);
            return;
        }

        var route = RentRoute();
        try
        {
            BuildRoute(this, route);

            if (routedEvent.RoutingStrategy == RoutingStrategy.Tunnel)
            {
                for (var i = route.Count - 1; i >= 0; i--)
                {
                    InvokeRoutedEvent(route[i], routedEvent, args);
                }

                return;
            }

            for (var i = 0; i < route.Count; i++)
            {
                InvokeRoutedEvent(route[i], routedEvent, args);
            }
        }
        finally
        {
            ReturnRoute(route);
        }
    }

    internal void RaiseRoutedEventInternal(RoutedEvent routedEvent, RoutedEventArgs args)
    {
        RaiseRoutedEvent(routedEvent, args);
    }

    internal bool HasRoutedHandlerForEvent(RoutedEvent routedEvent)
    {
        return _routedHandlers.TryGetValue(routedEvent, out var handlers) && handlers.Count > 0;
    }

    internal int GetRoutedHandlerCountForEvent(RoutedEvent routedEvent)
    {
        return _routedHandlers.TryGetValue(routedEvent, out var handlers) ? handlers.Count : 0;
    }

    private static void NotifyBindingTreeChanged(UIElement root)
    {
        var start = Stopwatch.GetTimestamp();
        var visited = new HashSet<UIElement>();
        NotifyBindingTreeChanged(root, visited);
        UiFrameworkPopulationPhaseDiagnostics.Observe(
            "UIElement.NotifyBindingTreeChanged.Total",
            Stopwatch.GetElapsedTime(start).TotalMilliseconds,
            visited.Count);
    }

    private static List<DependencyProperty> GetInheritablePropertiesForType(Type elementType)
    {
        lock (InheritablePropertyCacheLock)
        {
            if (InheritablePropertiesByType.TryGetValue(elementType, out var cached))
            {
                return cached;
            }

            var properties = new List<DependencyProperty>();
            foreach (var property in DependencyProperty.GetRegisteredProperties())
            {
                if (!property.IsApplicableTo(elementType))
                {
                    continue;
                }

                if (property.GetMetadata(elementType).Inherits)
                {
                    properties.Add(property);
                }
            }

            InheritablePropertiesByType[elementType] = properties;
            return properties;
        }
    }

    private object? ResolveInheritedValueFromParentChain(UIElement? parent, DependencyProperty property)
    {
        var metadata = property.GetMetadata(this);
        for (var current = parent; current != null; current = current.VisualParent)
        {
            if (!property.IsApplicableTo(current))
            {
                continue;
            }

            return current.GetValue(property);
        }

        return metadata.DefaultValue;
    }

    private static void NotifyBindingTreeChanged(UIElement element, HashSet<UIElement> visited)
    {
        if (!visited.Add(element))
        {
            return;
        }

        BindingOperations.NotifyTargetTreeChanged(element);

        foreach (var child in element.GetVisualChildren())
        {
            NotifyBindingTreeChanged(child, visited);
        }

        foreach (var child in element.GetLogicalChildren())
        {
            NotifyBindingTreeChanged(child, visited);
        }
    }

    private static void BuildRoute(UIElement target, List<UIElement> route)
    {
        route.Clear();
        for (var current = target; current != null; current = current.VisualParent)
        {
            route.Add(current);
        }
    }

    private static List<UIElement> RentRoute()
    {
        var pool = _routePool;
        if (pool != null && pool.Count > 0)
        {
            return pool.Pop();
        }

        return new List<UIElement>(8);
    }

    private static void ReturnRoute(List<UIElement> route)
    {
        route.Clear();
        _routePool ??= new Stack<List<UIElement>>();
        if (_routePool.Count < RoutePoolMaxSize)
        {
            _routePool.Push(route);
        }
    }

    private void InvokeRoutedEvent(UIElement target, RoutedEvent routedEvent, RoutedEventArgs args)
    {
        args.Source = target;
        target.DispatchRoutedEvent(routedEvent, args);
    }

    private void DispatchRoutedEvent(RoutedEvent routedEvent, RoutedEventArgs args)
    {
        EventManager.InvokeClassHandlers(this, routedEvent, args);
        InvokeInstanceHandlers(routedEvent, args);
    }

    private void InvokeInstanceHandlers(RoutedEvent routedEvent, RoutedEventArgs args)
    {
        if (!_routedHandlers.TryGetValue(routedEvent, out var handlers))
        {
            return;
        }

        foreach (var handlerEntry in handlers)
        {
            if (args.Handled && !handlerEntry.HandledEventsToo)
            {
                continue;
            }

            handlerEntry.Invoker(this, args);
        }
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        var metadata = args.Property.GetMetadata(this);
        var options = metadata.Options;
        if ((options & FrameworkPropertyMetadataOptions.AffectsMeasure) != 0)
        {
            InvalidateMeasure();
        }

        if ((options & FrameworkPropertyMetadataOptions.AffectsArrange) != 0)
        {
            InvalidateArrange();
        }

        if ((options & FrameworkPropertyMetadataOptions.AffectsRender) != 0)
        {
            InvalidateVisual();
        }

        if (!metadata.Inherits)
        {
            return;
        }

        foreach (var child in GetVisualChildren())
        {
            child.NotifyInheritedPropertyChanged(args.Property);
        }
    }

    private bool TryGetTransformFromRootToThisInverse(out Matrix inverseTransform)
    {
        return TryGetTransformFromRootToThisInverse(this, out inverseTransform);
    }

    private bool TryGetTransformFromThisToRoot(out Matrix transform)
    {
        return TryGetTransformFromThisToRoot(this, out transform);
    }

    private static bool TryGetTransformFromThisToRoot(UIElement? element, out Matrix transform)
    {
        transform = Matrix.Identity;
        var hasTransform = false;
        for (var current = element; current != null; current = current.VisualParent)
        {
            if (!current.TryGetLocalRenderTransform(out var localTransform, out _))
            {
                continue;
            }

            transform *= localTransform;
            hasTransform = true;
        }

        return hasTransform;
    }

    private static bool TryGetTransformFromRootToThisInverse(UIElement? element, out Matrix inverseTransform)
    {
        if (element == null)
        {
            inverseTransform = Matrix.Identity;
            return false;
        }

        var hasTransform = TryGetTransformFromRootToThisInverse(element.VisualParent, out inverseTransform);
        if (!element.TryGetLocalRenderTransform(out _, out var localInverse))
        {
            return hasTransform;
        }

        inverseTransform *= localInverse;
        return true;
    }

    private static bool IsPointVisibleThroughClipChain(UIElement? element, Vector2 point, ref Matrix accumulatedTransform)
    {
        if (element == null)
        {
            return true;
        }

        if (!IsPointVisibleThroughClipChain(element.VisualParent, point, ref accumulatedTransform))
        {
            return false;
        }

        if (element.TryGetClipRect(out var clipRect))
        {
            var transformedClip = TransformRect(clipRect, accumulatedTransform);
            if (!ContainsPoint(transformedClip, point))
            {
                return false;
            }
        }

        if (element.TryGetLocalRenderTransform(out var localTransform, out _))
        {
            accumulatedTransform = localTransform * accumulatedTransform;
        }

        return true;
    }

    internal void ClearMeasureInvalidation()
    {
        NeedsMeasure = false;
    }

    internal void ClearArrangeInvalidation()
    {
        NeedsArrange = false;
    }

    internal void ClearRenderInvalidationRecursive()
    {
        NeedsRender = false;
        SubtreeDirty = false;
        foreach (var child in GetVisualChildren())
        {
            child.ClearRenderInvalidationRecursive();
        }
    }

    private UIElement? GetInvalidationParent()
    {
        return VisualParent ?? LogicalParent;
    }

    private void MarkSubtreeDirty()
    {
        for (var current = this; current != null; current = current.GetInvalidationParent())
        {
            if (current.SubtreeDirty)
            {
                continue;
            }

            current.SubtreeDirty = true;
        }
    }

    private static bool AreRectsEqual(LayoutRect left, LayoutRect right)
    {
        const float epsilon = 0.0001f;
        return MathF.Abs(left.X - right.X) <= epsilon &&
               MathF.Abs(left.Y - right.Y) <= epsilon &&
               MathF.Abs(left.Width - right.Width) <= epsilon &&
               MathF.Abs(left.Height - right.Height) <= epsilon;
    }

    private static LayoutRect TransformRect(LayoutRect rect, Matrix transform)
    {
        if (transform == Matrix.Identity)
        {
            return rect;
        }

        var topLeft = Vector2.Transform(new Vector2(rect.X, rect.Y), transform);
        var topRight = Vector2.Transform(new Vector2(rect.X + rect.Width, rect.Y), transform);
        var bottomLeft = Vector2.Transform(new Vector2(rect.X, rect.Y + rect.Height), transform);
        var bottomRight = Vector2.Transform(new Vector2(rect.X + rect.Width, rect.Y + rect.Height), transform);

        var minX = MathF.Min(MathF.Min(topLeft.X, topRight.X), MathF.Min(bottomLeft.X, bottomRight.X));
        var minY = MathF.Min(MathF.Min(topLeft.Y, topRight.Y), MathF.Min(bottomLeft.Y, bottomRight.Y));
        var maxX = MathF.Max(MathF.Max(topLeft.X, topRight.X), MathF.Max(bottomLeft.X, bottomRight.X));
        var maxY = MathF.Max(MathF.Max(topLeft.Y, topRight.Y), MathF.Max(bottomLeft.Y, bottomRight.Y));
        return new LayoutRect(minX, minY, maxX - minX, maxY - minY);
    }

    private static bool ContainsPoint(LayoutRect rect, Vector2 point)
    {
        if (rect.Width < 0f || rect.Height < 0f)
        {
            return false;
        }

        return point.X >= rect.X &&
               point.X <= rect.X + rect.Width &&
               point.Y >= rect.Y &&
               point.Y <= rect.Y + rect.Height;
    }

    private static bool Intersects(LayoutRect rect, Rectangle clip)
    {
        if (rect.Width <= 0f || rect.Height <= 0f || clip.Width <= 0 || clip.Height <= 0)
        {
            return false;
        }

        var rectRight = rect.X + rect.Width;
        var rectBottom = rect.Y + rect.Height;
        var clipRight = clip.X + clip.Width;
        var clipBottom = clip.Y + clip.Height;
        return rect.X < clipRight &&
               rectRight > clip.X &&
               rect.Y < clipBottom &&
               rectBottom > clip.Y;
    }

    private readonly struct RoutedHandlerEntry
    {
        public RoutedHandlerEntry(
            Delegate originalHandler,
            Action<UIElement, RoutedEventArgs> invoker,
            bool handledEventsToo)
        {
            OriginalHandler = originalHandler;
            Invoker = invoker;
            HandledEventsToo = handledEventsToo;
        }

        public Delegate OriginalHandler { get; }

        public bool HandledEventsToo { get; }

        public Action<UIElement, RoutedEventArgs> Invoker { get; }
    }
}
