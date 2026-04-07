using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class UIElement : DependencyObject
{
    private const int RoutePoolMaxSize = 64;

    [ThreadStatic]
    private static Stack<List<UIElement>>? _routePool;
    [ThreadStatic]
    private static int _freezableInvalidationBatchDepth;
    [ThreadStatic]
    private static HashSet<UIElement>? _batchedFreezableInvalidationTargets;
    [ThreadStatic]
    private static string _lastFreezableBatchFlushTargetSummary = "none";
    [ThreadStatic]
    private static int _retainedSelfDrawDepth;
    [ThreadStatic]
    private static InvalidationContext? _currentInvalidationContext;
    private static int _freezableBatchFlushCount;
    private static int _freezableBatchFlushTargetCount;
    private static int _freezableBatchQueuedTargetCount;
    private static long _freezableBatchFlushElapsedTicks;
    private static int _freezableBatchMaxPendingTargetCount;
    private static int _valueChangedRaiseCount;
    private static long _valueChangedRaiseElapsedTicks;
    private static long _valueChangedRouteBuildElapsedTicks;
    private static long _valueChangedRouteTraverseElapsedTicks;
    private static long _valueChangedClassHandlerElapsedTicks;
    private static long _valueChangedInstanceDispatchElapsedTicks;
    private static long _valueChangedInstancePrepareElapsedTicks;
    private static long _valueChangedInstanceInvokeElapsedTicks;
    private static int _valueChangedMaxRouteLength;
    private static long _renderSelfElapsedTicks;
    private static int _renderSelfCallCount;
    private static long _hottestRenderSelfElapsedTicks;
    private static string _hottestRenderSelfType = "none";
    private static string _hottestRenderSelfName = string.Empty;
    private static readonly Dictionary<string, (int Count, long Ticks)> RenderSelfByType = new(StringComparer.Ordinal);
    private static readonly object InheritablePropertyCacheLock = new();
    private static readonly Dictionary<Type, List<DependencyProperty>> InheritablePropertiesByType = new();

    private readonly Dictionary<RoutedEvent, List<RoutedHandlerEntry>> _routedHandlers = new();
    private readonly List<CommandBinding> _commandBindings = new();
    private readonly List<InputBinding> _inputBindings = new();
    private LayoutRect _layoutSlot;
    private int _measureInvalidationCount;
    private int _arrangeInvalidationCount;
    private int _renderInvalidationCount;
    private ElementInvalidationDiagnostics? _invalidationDiagnostics;
    private int _renderVersionStamp;
    private int _layoutVersionStamp;
    private int _updateCallCount;
    private int _drawCallCount;
    private bool _suppressNextLogicalBindingTreeNotify;
    private bool _suppressNextLogicalParentChanged;

    public static readonly DependencyProperty IsVisibleProperty =
        DependencyProperty.Register(
            nameof(IsVisible),
            typeof(bool),
            typeof(UIElement),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty VisibilityProperty =
        DependencyProperty.Register(
            nameof(Visibility),
            typeof(Visibility),
            typeof(UIElement),
            new FrameworkPropertyMetadata(
                Visibility.Visible,
                FrameworkPropertyMetadataOptions.VisibilityAffectsMeasure,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is not UIElement element || args.NewValue is not Visibility visibility)
                    {
                        return;
                    }

                    var shouldBeVisible = visibility == Visibility.Visible;
                    if (element.IsVisible != shouldBeVisible)
                    {
                        element.IsVisible = shouldBeVisible;
                    }
                }));

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

    public static readonly DependencyProperty ClipToBoundsProperty =
        DependencyProperty.Register(
            nameof(ClipToBounds),
            typeof(bool),
            typeof(UIElement),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

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

    public static readonly DependencyProperty RenderTransformProperty =
        DependencyProperty.Register(
            nameof(RenderTransform),
            typeof(Transform),
            typeof(UIElement),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                static (dependencyObject, args) =>
                {
                    if (dependencyObject is not UIElement element)
                    {
                        return;
                    }

                    if (args.OldValue is Transform oldTransform)
                    {
                        oldTransform.Changed -= element.OnRenderTransformChanged;
                    }

                    if (args.NewValue is Transform newTransform)
                    {
                        newTransform.Changed += element.OnRenderTransformChanged;
                    }
                }));

    public static readonly DependencyProperty RenderTransformOriginProperty =
        DependencyProperty.Register(
            nameof(RenderTransformOrigin),
            typeof(Vector2),
            typeof(UIElement),
            new FrameworkPropertyMetadata(Vector2.Zero, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EffectProperty =
        DependencyProperty.Register(
            nameof(Effect),
            typeof(Effect),
            typeof(UIElement),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                static (dependencyObject, args) =>
                {
                    if (dependencyObject is not UIElement element)
                    {
                        return;
                    }

                    if (args.OldValue is Effect oldEffect)
                    {
                        oldEffect.Changed -= element.OnEffectChanged;
                    }

                    if (args.NewValue is Effect newEffect)
                    {
                        newEffect.Changed += element.OnEffectChanged;
                    }
                }));

    public static readonly RoutedEvent PreviewMouseMoveEvent = new(nameof(PreviewMouseMoveEvent), RoutingStrategy.Tunnel);
    public static readonly RoutedEvent MouseMoveEvent = new(nameof(MouseMoveEvent), RoutingStrategy.Bubble);
    public static readonly RoutedEvent MouseEnterEvent = new(nameof(MouseEnterEvent), RoutingStrategy.Direct);
    public static readonly RoutedEvent MouseLeaveEvent = new(nameof(MouseLeaveEvent), RoutingStrategy.Direct);
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

    public Visibility Visibility
    {
        get => GetValue<Visibility>(VisibilityProperty);
        set => SetValue(VisibilityProperty, value);
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

    public bool ClipToBounds
    {
        get => GetValue<bool>(ClipToBoundsProperty);
        set => SetValue(ClipToBoundsProperty, value);
    }

    public float Opacity
    {
        get => GetValue<float>(OpacityProperty);
        set => SetValue(OpacityProperty, value);
    }

    public Transform? RenderTransform
    {
        get => GetValue<Transform>(RenderTransformProperty);
        set => SetValue(RenderTransformProperty, value);
    }

    public Vector2 RenderTransformOrigin
    {
        get => GetValue<Vector2>(RenderTransformOriginProperty);
        set => SetValue(RenderTransformOriginProperty, value);
    }

    public Effect? Effect
    {
        get => GetValue<Effect>(EffectProperty);
        set => SetValue(EffectProperty, value);
    }

    public LayoutRect LayoutSlot => _layoutSlot;

    public bool NeedsMeasure { get; private set; }

    public bool NeedsArrange { get; private set; }

    public bool NeedsRender { get; private set; }

    public bool SubtreeDirty { get; private set; }

    public int MeasureInvalidationCount => _measureInvalidationCount;

    public int ArrangeInvalidationCount => _arrangeInvalidationCount;

    public int RenderInvalidationCount => _renderInvalidationCount;

    public int UpdateCallCount => _updateCallCount;

    public int DrawCallCount => _drawCallCount;

    internal UIElementInvalidationDiagnosticsSnapshot InvalidationDiagnosticsForTests
    {
        get
        {
            if (_invalidationDiagnostics is null)
            {
                return UIElementInvalidationDiagnosticsSnapshot.Empty;
            }

            return new UIElementInvalidationDiagnosticsSnapshot(
                _invalidationDiagnostics.DirectMeasureInvalidationCount,
                _invalidationDiagnostics.PropagatedMeasureInvalidationCount,
                _invalidationDiagnostics.LastMeasureInvalidationSummary,
                SummarizeInvalidationSourceCounts(_invalidationDiagnostics.MeasureSources, limit: 4),
                _invalidationDiagnostics.LastMeasureInvalidationLayoutFrame,
                _invalidationDiagnostics.LastMeasureInvalidationDrawFrame,
                _invalidationDiagnostics.DirectArrangeInvalidationCount,
                _invalidationDiagnostics.PropagatedArrangeInvalidationCount,
                _invalidationDiagnostics.LastArrangeInvalidationSummary,
                SummarizeInvalidationSourceCounts(_invalidationDiagnostics.ArrangeSources, limit: 4),
                _invalidationDiagnostics.LastArrangeInvalidationLayoutFrame,
                _invalidationDiagnostics.LastArrangeInvalidationDrawFrame,
                _invalidationDiagnostics.DirectRenderInvalidationCount,
                _invalidationDiagnostics.PropagatedRenderInvalidationCount,
                _invalidationDiagnostics.LastRenderInvalidationSummary,
                SummarizeInvalidationSourceCounts(_invalidationDiagnostics.RenderSources, limit: 4),
                _invalidationDiagnostics.LastRenderInvalidationLayoutFrame,
                _invalidationDiagnostics.LastRenderInvalidationDrawFrame);
        }
    }

    internal int RenderVersionStamp => _renderVersionStamp;

    internal int LayoutVersionStamp => _layoutVersionStamp;

    public virtual IEnumerable<UIElement> GetVisualChildren()
    {
        yield break;
    }

    internal virtual int GetVisualChildCountForTraversal()
    {
        return -1;
    }

    internal virtual UIElement GetVisualChildAtForTraversal(int index)
    {
        throw new ArgumentOutOfRangeException(nameof(index));
    }

    internal virtual IEnumerable<UIElement> GetRetainedRenderChildren()
    {
        return GetVisualChildren();
    }

    public virtual IEnumerable<UIElement> GetLogicalChildren()
    {
        yield break;
    }

    internal virtual bool TryGetRenderBoundsInRootSpace(out LayoutRect bounds)
    {
        if (!TryGetLocalRenderBounds(out var localBounds))
        {
            bounds = localBounds;
            return false;
        }

        if (!TryGetTransformFromThisToRoot(out var transform))
        {
            bounds = localBounds;
            return true;
        }

        var topLeft = Vector2.Transform(new Vector2(localBounds.X, localBounds.Y), transform);
        var topRight = Vector2.Transform(new Vector2(localBounds.X + localBounds.Width, localBounds.Y), transform);
        var bottomLeft = Vector2.Transform(new Vector2(localBounds.X, localBounds.Y + localBounds.Height), transform);
        var bottomRight = Vector2.Transform(new Vector2(localBounds.X + localBounds.Width, localBounds.Y + localBounds.Height), transform);

        var minX = MathF.Min(MathF.Min(topLeft.X, topRight.X), MathF.Min(bottomLeft.X, bottomRight.X));
        var minY = MathF.Min(MathF.Min(topLeft.Y, topRight.Y), MathF.Min(bottomLeft.Y, bottomRight.Y));
        var maxX = MathF.Max(MathF.Max(topLeft.X, topRight.X), MathF.Max(bottomLeft.X, bottomRight.X));
        var maxY = MathF.Max(MathF.Max(topLeft.Y, topRight.Y), MathF.Max(bottomLeft.Y, bottomRight.Y));

        bounds = new LayoutRect(minX, minY, MathF.Max(0f, maxX - minX), MathF.Max(0f, maxY - minY));
        return bounds.Width > 0f && bounds.Height > 0f;
    }

    internal bool TryGetLocalRenderBoundsSnapshot(out LayoutRect bounds)
    {
        return TryGetLocalRenderBounds(out bounds);
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
        _updateCallCount++;
        foreach (var child in GetVisualChildren())
        {
            child.Update(gameTime);
        }
    }

    internal void RecordUpdateCallFromUiRoot()
    {
        _updateCallCount++;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (!IsVisible)
        {
            return;
        }

        _drawCallCount++;

        var hasClip = TryGetClipRect(out var clipRect);
        var hasTransform = TryGetLocalRenderTransform(out var localTransform, out _);
        UiDrawing.PushLocalState(spriteBatch, hasTransform, localTransform, hasClip, clipRect);

        try
        {
            var renderStart = Stopwatch.GetTimestamp();
            Effect?.Render(this, spriteBatch, Opacity);
            OnRender(spriteBatch);
            RecordRenderSelfTiming(this, Stopwatch.GetTimestamp() - renderStart);
            var currentClip = spriteBatch.GraphicsDevice.ScissorRectangle;

            if (ShouldAutoDrawVisualChildren)
            {
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
        }
        finally
        {
            UiDrawing.PopLocalState(spriteBatch, hasTransform, hasClip);
        }
    }

    internal void DrawSelf(SpriteBatch spriteBatch)
    {
        _drawCallCount++;
        var renderStart = Stopwatch.GetTimestamp();
        _retainedSelfDrawDepth++;
        try
        {
            Effect?.Render(this, spriteBatch, Opacity);
            OnRender(spriteBatch);
            RecordRenderSelfTiming(this, Stopwatch.GetTimestamp() - renderStart);
        }
        finally
        {
            _retainedSelfDrawDepth--;
        }
    }

    protected virtual void OnRender(SpriteBatch spriteBatch)
    {
    }

    internal static bool IsRetainedDrawPassForCurrentThread => _retainedSelfDrawDepth > 0;

    protected virtual bool ShouldAutoDrawVisualChildren => true;

    protected virtual bool TryGetClipRect(out LayoutRect clipRect)
    {
        if (ClipToBounds)
        {
            clipRect = LayoutSlot;
            return true;
        }

        clipRect = default;
        return false;
    }

    protected virtual bool TryGetLocalRenderTransform(out Matrix transform, out Matrix inverseTransform)
    {
        var localRenderTransform = RenderTransform;
        if (localRenderTransform == null)
        {
            transform = Matrix.Identity;
            inverseTransform = Matrix.Identity;
            return false;
        }

        transform = localRenderTransform.ToMatrix();
        if (RenderTransformOrigin != Vector2.Zero)
        {
            var origin = RenderTransformOrigin;
            var pivotX = LayoutSlot.X + (LayoutSlot.Width * origin.X);
            var pivotY = LayoutSlot.Y + (LayoutSlot.Height * origin.Y);
            transform = Matrix.CreateTranslation(-pivotX, -pivotY, 0f)
                        * transform
                        * Matrix.CreateTranslation(pivotX, pivotY, 0f);
        }

        if (!TryInvertMatrix(transform, out inverseTransform))
        {
            inverseTransform = Matrix.Identity;
        }

        return transform != Matrix.Identity;
    }

    protected static bool TryComposeLocalTransforms(
        bool hasPrimaryTransform,
        Matrix primaryTransform,
        Matrix primaryInverse,
        bool hasSecondaryTransform,
        Matrix secondaryTransform,
        Matrix secondaryInverse,
        out Matrix combinedTransform,
        out Matrix combinedInverse)
    {
        if (!hasPrimaryTransform && !hasSecondaryTransform)
        {
            combinedTransform = Matrix.Identity;
            combinedInverse = Matrix.Identity;
            return false;
        }

        if (!hasPrimaryTransform)
        {
            combinedTransform = secondaryTransform;
            combinedInverse = secondaryInverse;
            return true;
        }

        if (!hasSecondaryTransform)
        {
            combinedTransform = primaryTransform;
            combinedInverse = primaryInverse;
            return true;
        }

        combinedTransform = primaryTransform * secondaryTransform;
        combinedInverse = secondaryInverse * primaryInverse;
        return true;
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

    internal bool TryGetLocalRenderTransformSnapshot(out Matrix transform, out Matrix inverseTransform)
    {
        return TryGetLocalRenderTransform(out transform, out inverseTransform);
    }

    public virtual void InvalidateMeasure()
    {
        var context = _currentInvalidationContext;
        InvalidateMeasureCore(
            context?.Origin ?? this,
            context?.ImmediateSource,
            context?.Reason ?? "direct-call");
    }

    public virtual void InvalidateArrange()
    {
        var context = _currentInvalidationContext;
        InvalidateArrangeCore(
            context?.Origin ?? this,
            context?.ImmediateSource,
            context?.Reason ?? "direct-call");
    }

    public virtual void InvalidateVisual()
    {
        var context = _currentInvalidationContext;
        InvalidateVisualCore(
            context?.Origin ?? this,
            context?.ImmediateSource,
            context?.Reason ?? "direct-call");
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

        var transformedBounds = _layoutSlot;
        if (TryGetTransformFromThisToRoot(out var transform))
        {
            transformedBounds = TransformRect(transformedBounds, transform);
        }

        return point.X >= transformedBounds.X &&
               point.X <= transformedBounds.X + transformedBounds.Width &&
               point.Y >= transformedBounds.Y &&
               point.Y <= transformedBounds.Y + transformedBounds.Height;
    }

    internal bool HitTestRect(Vector2 point, LayoutRect rect)
    {
        if (!IsVisible || !IsEnabled || !IsHitTestVisible)
        {
            return false;
        }

        if (!IsPointVisibleThroughClipChain(point))
        {
            return false;
        }

        var transformedBounds = rect;
        if (TryGetTransformFromThisToRoot(out var transform))
        {
            transformedBounds = TransformRect(transformedBounds, transform);
        }

        return point.X >= transformedBounds.X &&
               point.X <= transformedBounds.X + transformedBounds.Width &&
               point.Y >= transformedBounds.Y &&
               point.Y <= transformedBounds.Y + transformedBounds.Height;
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
        if (ReferenceEquals(VisualParent, parent))
        {
            return;
        }

        var oldParent = VisualParent;
        VisualParent = parent;
        OnVisualParentChanged(oldParent, parent);
        UiRoot.Current?.NotifyVisualStructureChanged(this, oldParent, parent);
        NotifyBindingTreeChanged(this);
        _suppressNextLogicalBindingTreeNotify = true;
        _suppressNextLogicalParentChanged = true;
    }

    internal void SetLogicalParent(UIElement? parent)
    {
        if (ReferenceEquals(LogicalParent, parent))
        {
            return;
        }

        var oldParent = LogicalParent;
        LogicalParent = parent;
        var shouldRunLogicalParentChanged = !(ReferenceEquals(parent, VisualParent) && _suppressNextLogicalParentChanged);
        if (shouldRunLogicalParentChanged)
        {
            OnLogicalParentChanged(oldParent, parent);
        }

        _suppressNextLogicalParentChanged = false;

        var shouldNotifyBindingTree = !ReferenceEquals(parent, VisualParent) && !_suppressNextLogicalBindingTreeNotify;
        if (shouldNotifyBindingTree)
        {
            NotifyBindingTreeChanged(this);
        }

        _suppressNextLogicalBindingTreeNotify = false;
    }

    internal void SetLayoutSlot(LayoutRect layoutSlot)
    {
        if (!AreRectsEqual(_layoutSlot, layoutSlot))
        {
            _layoutVersionStamp++;
        }

        _layoutSlot = layoutSlot;
    }

    protected virtual void OnVisualParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        var inheritableProperties = new[]
        {
            UIElement.IsEnabledProperty,
            FrameworkElement.DataContextProperty,
            FrameworkElement.BindingGroupProperty,
            FrameworkElement.UseLayoutRoundingProperty
        };

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
            }
        }
    }

    protected virtual void OnLogicalParentChanged(UIElement? oldParent, UIElement? newParent)
    {
    }

    protected void RaiseRoutedEvent(RoutedEvent routedEvent, RoutedEventArgs args)
    {
        var trackValueChanged = string.Equals(routedEvent.Name, "ValueChanged", StringComparison.Ordinal);
        var valueChangedStartTicks = trackValueChanged ? Stopwatch.GetTimestamp() : 0L;
        args.OriginalSource ??= this;
        if (routedEvent.RoutingStrategy == RoutingStrategy.Direct)
        {
            var classHandlerMilliseconds = 0d;
            var instanceHandlerMilliseconds = 0d;
            var instancePrepareMilliseconds = 0d;
            var instanceInvokeMilliseconds = 0d;
            InvokeRoutedEvent(this, routedEvent, args, ref classHandlerMilliseconds, ref instanceHandlerMilliseconds, ref instancePrepareMilliseconds, ref instanceInvokeMilliseconds);
            if (trackValueChanged)
            {
                _valueChangedRaiseCount++;
                _valueChangedRaiseElapsedTicks += Stopwatch.GetTimestamp() - valueChangedStartTicks;
                _valueChangedClassHandlerElapsedTicks += MillisecondsToTicks(classHandlerMilliseconds);
                _valueChangedInstanceDispatchElapsedTicks += MillisecondsToTicks(instanceHandlerMilliseconds);
                _valueChangedInstancePrepareElapsedTicks += MillisecondsToTicks(instancePrepareMilliseconds);
                _valueChangedInstanceInvokeElapsedTicks += MillisecondsToTicks(instanceInvokeMilliseconds);
                if (_valueChangedMaxRouteLength < 1)
                {
                    _valueChangedMaxRouteLength = 1;
                }
            }
            return;
        }

        var route = RentRoute();
        var routeBuildStart = Stopwatch.GetTimestamp();
        var classHandlerElapsedMilliseconds = 0d;
        var instanceHandlerElapsedMilliseconds = 0d;
        var instancePrepareElapsedMilliseconds = 0d;
        var instanceInvokeElapsedMilliseconds = 0d;
        var routeBuildElapsedTicks = 0L;
        var routeTraverseElapsedTicks = 0L;
        try
        {
            if (CanDispatchRoutedEventOnSourceOnly(this, routedEvent))
            {
                var sourceOnlyTraverseStart = Stopwatch.GetTimestamp();
                InvokeRoutedEvent(this, routedEvent, args, ref classHandlerElapsedMilliseconds, ref instanceHandlerElapsedMilliseconds, ref instancePrepareElapsedMilliseconds, ref instanceInvokeElapsedMilliseconds);
                routeTraverseElapsedTicks = Stopwatch.GetTimestamp() - sourceOnlyTraverseStart;
                return;
            }

            BuildRoute(this, route);
            routeBuildElapsedTicks = Stopwatch.GetTimestamp() - routeBuildStart;
            var routeTraverseStart = Stopwatch.GetTimestamp();

            if (routedEvent.RoutingStrategy == RoutingStrategy.Tunnel)
            {
                for (var i = route.Count - 1; i >= 0; i--)
                {
                    if (!HasAnyRoutedEventHandlers(route[i], routedEvent))
                    {
                        continue;
                    }

                    InvokeRoutedEvent(route[i], routedEvent, args, ref classHandlerElapsedMilliseconds, ref instanceHandlerElapsedMilliseconds, ref instancePrepareElapsedMilliseconds, ref instanceInvokeElapsedMilliseconds);
                }
            }
            else
            {
                for (var i = 0; i < route.Count; i++)
                {
                    if (!HasAnyRoutedEventHandlers(route[i], routedEvent))
                    {
                        continue;
                    }

                    InvokeRoutedEvent(route[i], routedEvent, args, ref classHandlerElapsedMilliseconds, ref instanceHandlerElapsedMilliseconds, ref instancePrepareElapsedMilliseconds, ref instanceInvokeElapsedMilliseconds);
                }
            }

            routeTraverseElapsedTicks = Stopwatch.GetTimestamp() - routeTraverseStart;

        }
        finally
        {
            if (trackValueChanged)
            {
                _valueChangedRaiseCount++;
                _valueChangedRaiseElapsedTicks += Stopwatch.GetTimestamp() - valueChangedStartTicks;
                _valueChangedRouteBuildElapsedTicks += routeBuildElapsedTicks;
                _valueChangedRouteTraverseElapsedTicks += routeTraverseElapsedTicks;
                _valueChangedClassHandlerElapsedTicks += MillisecondsToTicks(classHandlerElapsedMilliseconds);
                _valueChangedInstanceDispatchElapsedTicks += MillisecondsToTicks(instanceHandlerElapsedMilliseconds);
                _valueChangedInstancePrepareElapsedTicks += MillisecondsToTicks(instancePrepareElapsedMilliseconds);
                _valueChangedInstanceInvokeElapsedTicks += MillisecondsToTicks(instanceInvokeElapsedMilliseconds);
                if (route.Count > _valueChangedMaxRouteLength)
                {
                    _valueChangedMaxRouteLength = route.Count;
                }
            }

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
        var visited = new HashSet<UIElement>();
        NotifyBindingTreeChanged(root, visited);
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

    private void InvokeRoutedEvent(
        UIElement target,
        RoutedEvent routedEvent,
        RoutedEventArgs args,
        ref double classHandlerMilliseconds,
        ref double instanceHandlerMilliseconds,
        ref double instancePrepareMilliseconds,
        ref double instanceInvokeMilliseconds)
    {
        args.Source = target;
        target.DispatchRoutedEvent(routedEvent, args, ref classHandlerMilliseconds, ref instanceHandlerMilliseconds, ref instancePrepareMilliseconds, ref instanceInvokeMilliseconds);
    }

    private void DispatchRoutedEvent(
        RoutedEvent routedEvent,
        RoutedEventArgs args,
        ref double classHandlerMilliseconds,
        ref double instanceHandlerMilliseconds,
        ref double instancePrepareMilliseconds,
        ref double instanceInvokeMilliseconds)
    {
        var classHandlerStart = Stopwatch.GetTimestamp();
        EventManager.InvokeClassHandlers(this, routedEvent, args);
        classHandlerMilliseconds += Stopwatch.GetElapsedTime(classHandlerStart).TotalMilliseconds;
        var instanceHandlerStart = Stopwatch.GetTimestamp();
        InvokeInstanceHandlers(routedEvent, args, ref instancePrepareMilliseconds, ref instanceInvokeMilliseconds);
        instanceHandlerMilliseconds += Stopwatch.GetElapsedTime(instanceHandlerStart).TotalMilliseconds;
    }

    private static bool HasAnyRoutedEventHandlers(UIElement target, RoutedEvent routedEvent)
    {
        return target.GetRoutedHandlerCountForEvent(routedEvent) > 0 ||
               EventManager.HasClassHandlers(target.GetType(), routedEvent);
    }

    private static bool CanDispatchRoutedEventOnSourceOnly(UIElement target, RoutedEvent routedEvent)
    {
        if (!HasAnyRoutedEventHandlers(target, routedEvent))
        {
            return false;
        }

        for (var current = target.VisualParent; current != null; current = current.VisualParent)
        {
            if (HasAnyRoutedEventHandlers(current, routedEvent))
            {
                return false;
            }
        }

        return true;
    }

    private static int CountClassHandlers(List<UIElement> route, RoutedEvent routedEvent)
    {
        var count = 0;
        for (var i = 0; i < route.Count; i++)
        {
            count += EventManager.GetClassHandlerCount(route[i].GetType(), routedEvent);
        }

        return count;
    }

    private static int CountInstanceHandlers(List<UIElement> route, RoutedEvent routedEvent)
    {
        var count = 0;
        for (var i = 0; i < route.Count; i++)
        {
            count += route[i].GetRoutedHandlerCountForEvent(routedEvent);
        }

        return count;
    }

    private void InvokeInstanceHandlers(RoutedEvent routedEvent, RoutedEventArgs args, ref double instancePrepareMilliseconds, ref double instanceInvokeMilliseconds)
    {
        var prepareStart = Stopwatch.GetTimestamp();
        if (!_routedHandlers.TryGetValue(routedEvent, out var handlers))
        {
            instancePrepareMilliseconds += Stopwatch.GetElapsedTime(prepareStart).TotalMilliseconds;
            return;
        }

        var handlerCount = handlers.Count;
        if (handlerCount == 0)
        {
            instancePrepareMilliseconds += Stopwatch.GetElapsedTime(prepareStart).TotalMilliseconds;
            return;
        }

        if (handlerCount == 1)
        {
            var handlerEntry = handlers[0];
            instancePrepareMilliseconds += Stopwatch.GetElapsedTime(prepareStart).TotalMilliseconds;
            if (!args.Handled || handlerEntry.HandledEventsToo)
            {
                var invokeStart = Stopwatch.GetTimestamp();
                handlerEntry.Invoker(this, args);
                instanceInvokeMilliseconds += Stopwatch.GetElapsedTime(invokeStart).TotalMilliseconds;
            }

            return;
        }

        // Handlers are allowed to add/remove routed handlers while events dispatch.
        // Iterate over a snapshot to avoid collection-modified exceptions.
        var snapshot = ArrayPool<RoutedHandlerEntry>.Shared.Rent(handlerCount);
        try
        {
            handlers.CopyTo(snapshot, 0);
            instancePrepareMilliseconds += Stopwatch.GetElapsedTime(prepareStart).TotalMilliseconds;
            for (var i = 0; i < handlerCount; i++)
            {
                var handlerEntry = snapshot[i];
                if (args.Handled && !handlerEntry.HandledEventsToo)
                {
                    continue;
                }

                var invokeStart = Stopwatch.GetTimestamp();
                handlerEntry.Invoker(this, args);
                instanceInvokeMilliseconds += Stopwatch.GetElapsedTime(invokeStart).TotalMilliseconds;
            }
        }
        finally
        {
            Array.Clear(snapshot, 0, handlerCount);
            ArrayPool<RoutedHandlerEntry>.Shared.Return(snapshot, clearArray: false);
        }
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        var metadata = args.Property.GetMetadata(this);
        var options = metadata.Options;
        if ((options & FrameworkPropertyMetadataOptions.AffectsMeasure) != 0 &&
            ShouldInvalidateMeasureForPropertyChange(args, metadata))
        {
            InvalidateMeasureCore(this, source: null, reason: $"property:{args.Property.Name}");
        }

        if ((options & FrameworkPropertyMetadataOptions.AffectsArrange) != 0 &&
            ShouldInvalidateArrangeForPropertyChange(args, metadata))
        {
            InvalidateArrangeCore(this, source: null, reason: $"property:{args.Property.Name}");
        }

        if ((options & FrameworkPropertyMetadataOptions.AffectsRender) != 0 &&
            ShouldInvalidateVisualForPropertyChange(args, metadata))
        {
            InvalidateVisualCore(this, source: null, reason: $"property:{args.Property.Name}");
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

    protected virtual bool ShouldInvalidateMeasureForPropertyChange(
        DependencyPropertyChangedEventArgs args,
        FrameworkPropertyMetadata metadata)
    {
        _ = args;
        _ = metadata;
        return true;
    }

    protected virtual bool ShouldInvalidateArrangeForPropertyChange(
        DependencyPropertyChangedEventArgs args,
        FrameworkPropertyMetadata metadata)
    {
        _ = args;
        _ = metadata;
        return true;
    }

    protected virtual bool ShouldInvalidateVisualForPropertyChange(
        DependencyPropertyChangedEventArgs args,
        FrameworkPropertyMetadata metadata)
    {
        _ = args;
        _ = metadata;
        return true;
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

        var currentTransform = accumulatedTransform;
        if (element.TryGetLocalRenderTransform(out var localTransform, out _))
        {
            currentTransform = localTransform * accumulatedTransform;
        }

        if (element.TryGetClipRect(out var clipRect))
        {
            var transformedClip = TransformRect(clipRect, currentTransform);
            if (!ContainsPoint(transformedClip, point))
            {
                return false;
            }
        }

        accumulatedTransform = currentTransform;

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

    internal void PrepareArrangeForDirectLayoutOnly()
    {
        PrepareArrangeForDirectLayoutCore(invalidateRender: true);
    }

    internal void PrepareArrangeForDirectLayoutWithoutRenderInvalidation()
    {
        PrepareArrangeForDirectLayoutCore(invalidateRender: false);
    }

    internal void ClearRenderInvalidationShallow()
    {
        NeedsRender = false;
        SubtreeDirty = false;
    }

    internal void ClearRenderInvalidationRecursive()
    {
        ClearRenderInvalidationShallow();
        foreach (var child in GetVisualChildren())
        {
            child.ClearRenderInvalidationRecursive();
        }
    }

    internal UIElement? GetInvalidationParent()
    {
        return VisualParent ?? LogicalParent;
    }

    private void MarkSubtreeDirty()
    {
        for (var current = this; current != null; current = current.GetInvalidationParent())
        {
            if (current.SubtreeDirty)
            {
                break;
            }

            current.SubtreeDirty = true;
        }
    }

    private void PrepareArrangeForDirectLayoutCore(bool invalidateRender)
    {
        Dispatcher.VerifyAccess();
        if (NeedsArrange)
        {
            if (invalidateRender)
            {
                InvalidateVisual();
            }

            return;
        }

        NeedsArrange = true;
        _arrangeInvalidationCount++;
        _layoutVersionStamp++;
        MarkSubtreeDirty();
        if (invalidateRender)
        {
            InvalidateVisual();
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

    private void OnEffectChanged()
    {
        if (TryQueueFreezableBatchInvalidation(this))
        {
            return;
        }

        InvalidateVisual();
    }

    private void OnRenderTransformChanged()
    {
        if (TryQueueFreezableBatchInvalidation(this))
        {
            return;
        }

        InvalidateVisual();
    }

    internal static void BeginFreezableInvalidationBatch()
    {
        _freezableInvalidationBatchDepth++;
    }

    internal static void EndFreezableInvalidationBatch(UIElement? aggregateInvalidationTarget = null, bool requireDeepSync = false)
    {
        var startTicks = Stopwatch.GetTimestamp();
        if (_freezableInvalidationBatchDepth <= 0)
        {
            return;
        }

        _freezableInvalidationBatchDepth--;
        if (_freezableInvalidationBatchDepth != 0)
        {
            return;
        }

        if (_batchedFreezableInvalidationTargets == null || _batchedFreezableInvalidationTargets.Count == 0)
        {
            return;
        }

        var pending = _batchedFreezableInvalidationTargets.ToArray();
        _batchedFreezableInvalidationTargets.Clear();
        _freezableBatchFlushCount++;
        _freezableBatchFlushTargetCount += pending.Length;
        _lastFreezableBatchFlushTargetSummary = SummarizeElements(pending, limit: 6);
        for (var i = 0; i < pending.Length; i++)
        {
            pending[i].InvalidateVisual();
        }

        if (aggregateInvalidationTarget != null)
        {
            UiRoot.Current?.NotifyDirectRenderInvalidation(aggregateInvalidationTarget, requireDeepSync);
        }

        _freezableBatchFlushElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
    }

    private static bool TryQueueFreezableBatchInvalidation(UIElement element)
    {
        if (_freezableInvalidationBatchDepth <= 0)
        {
            return false;
        }

        var targets = _batchedFreezableInvalidationTargets ??= new HashSet<UIElement>();
        if (targets.Add(element))
        {
            _freezableBatchQueuedTargetCount++;
            if (targets.Count > _freezableBatchMaxPendingTargetCount)
            {
                _freezableBatchMaxPendingTargetCount = targets.Count;
            }
        }

        return true;
    }

    internal static UiFreezableInvalidationBatchSnapshot GetFreezableInvalidationBatchSnapshotForTests()
    {
        return new UiFreezableInvalidationBatchSnapshot(
            _freezableBatchFlushCount,
            _freezableBatchFlushTargetCount,
            _freezableBatchQueuedTargetCount,
            _freezableBatchMaxPendingTargetCount,
            (double)_freezableBatchFlushElapsedTicks * 1000d / Stopwatch.Frequency,
            _lastFreezableBatchFlushTargetSummary);
    }

    internal static UIElementRenderTimingSnapshot GetRenderTimingSnapshotForTests()
    {
        return new UIElementRenderTimingSnapshot(
            _renderSelfElapsedTicks,
            _renderSelfCallCount,
            _hottestRenderSelfType,
            _hottestRenderSelfName,
            (double)_hottestRenderSelfElapsedTicks * 1000d / Stopwatch.Frequency,
            SummarizeRenderSelfTypes(limit: 4));
    }

    internal static ValueChangedRoutedEventTelemetrySnapshot GetValueChangedRoutedEventTelemetryAndReset()
    {
        var snapshot = new ValueChangedRoutedEventTelemetrySnapshot(
            _valueChangedRaiseCount,
            (double)_valueChangedRaiseElapsedTicks * 1000d / Stopwatch.Frequency,
            (double)_valueChangedRouteBuildElapsedTicks * 1000d / Stopwatch.Frequency,
            (double)_valueChangedRouteTraverseElapsedTicks * 1000d / Stopwatch.Frequency,
            (double)_valueChangedClassHandlerElapsedTicks * 1000d / Stopwatch.Frequency,
            (double)_valueChangedInstanceDispatchElapsedTicks * 1000d / Stopwatch.Frequency,
            (double)_valueChangedInstancePrepareElapsedTicks * 1000d / Stopwatch.Frequency,
            (double)_valueChangedInstanceInvokeElapsedTicks * 1000d / Stopwatch.Frequency,
            _valueChangedMaxRouteLength);
        _valueChangedRaiseCount = 0;
        _valueChangedRaiseElapsedTicks = 0L;
        _valueChangedRouteBuildElapsedTicks = 0L;
        _valueChangedRouteTraverseElapsedTicks = 0L;
        _valueChangedClassHandlerElapsedTicks = 0L;
        _valueChangedInstanceDispatchElapsedTicks = 0L;
        _valueChangedInstancePrepareElapsedTicks = 0L;
        _valueChangedInstanceInvokeElapsedTicks = 0L;
        _valueChangedMaxRouteLength = 0;
        return snapshot;
    }

    internal static void ResetFreezableInvalidationBatchTelemetryForTests()
    {
        _freezableBatchFlushCount = 0;
        _freezableBatchFlushTargetCount = 0;
        _freezableBatchQueuedTargetCount = 0;
        _freezableBatchFlushElapsedTicks = 0L;
        _freezableBatchMaxPendingTargetCount = 0;
        _valueChangedRaiseCount = 0;
        _valueChangedRaiseElapsedTicks = 0L;
        _valueChangedRouteBuildElapsedTicks = 0L;
        _valueChangedRouteTraverseElapsedTicks = 0L;
        _valueChangedClassHandlerElapsedTicks = 0L;
        _valueChangedInstanceDispatchElapsedTicks = 0L;
        _valueChangedInstancePrepareElapsedTicks = 0L;
        _valueChangedInstanceInvokeElapsedTicks = 0L;
        _valueChangedMaxRouteLength = 0;
        _lastFreezableBatchFlushTargetSummary = "none";
        _renderSelfElapsedTicks = 0L;
        _renderSelfCallCount = 0;
        _hottestRenderSelfElapsedTicks = 0L;
        _hottestRenderSelfType = "none";
        _hottestRenderSelfName = string.Empty;
        RenderSelfByType.Clear();
    }

    private static void RecordRenderSelfTiming(UIElement element, long ticks)
    {
        _renderSelfElapsedTicks += ticks;
        _renderSelfCallCount++;

        var typeName = element.GetType().Name;
        if (RenderSelfByType.TryGetValue(typeName, out var existing))
        {
            RenderSelfByType[typeName] = (existing.Count + 1, existing.Ticks + ticks);
        }
        else
        {
            RenderSelfByType[typeName] = (1, ticks);
        }

        if (ticks <= _hottestRenderSelfElapsedTicks)
        {
            return;
        }

        _hottestRenderSelfElapsedTicks = ticks;
        _hottestRenderSelfType = typeName;
        _hottestRenderSelfName = element is FrameworkElement frameworkElement
            ? frameworkElement.Name ?? string.Empty
            : string.Empty;
    }

    private static string SummarizeRenderSelfTypes(int limit)
    {
        if (RenderSelfByType.Count == 0)
        {
            return "none";
        }

        return string.Join(
            ", ",
            RenderSelfByType
                .OrderByDescending(static pair => pair.Value.Ticks)
                .Take(limit)
                .Select(static pair =>
                    $"{pair.Key}(n={pair.Value.Count},ms={(double)pair.Value.Ticks * 1000d / Stopwatch.Frequency:0.###})"));
    }

    private static string SummarizeElements(IReadOnlyList<UIElement> elements, int limit)
    {
        if (elements.Count == 0)
        {
            return "none";
        }

        var count = Math.Min(limit, elements.Count);
        var summaries = new string[count];
        for (var i = 0; i < count; i++)
        {
            summaries[i] = elements[i] switch
            {
                FrameworkElement { Name.Length: > 0 } frameworkElement => $"{frameworkElement.GetType().Name}#{frameworkElement.Name}",
                _ => elements[i].GetType().Name
            };
        }

        return string.Join(" | ", summaries);
    }

    private void InvalidateMeasureCore(UIElement origin, UIElement? source, string reason)
    {
        Dispatcher.VerifyAccess();
        if (NeedsMeasure)
        {
            return;
        }

        NeedsMeasure = true;
        _measureInvalidationCount++;
        _layoutVersionStamp++;
        RecordInvalidationDiagnostics(UiInvalidationType.Measure, origin, source, reason);
        MarkSubtreeDirty();
        UiRoot.Current?.NotifyInvalidation(UiInvalidationType.Measure, this);
        RunWithInvalidationContext(origin, this, $"measure<={reason}", InvalidateArrange);
        var invalidationParent = GetInvalidationParent();
        if (invalidationParent != null)
        {
            RunWithInvalidationContext(origin, this, reason, invalidationParent.InvalidateMeasure);
        }
    }

    private void InvalidateArrangeCore(UIElement origin, UIElement? source, string reason)
    {
        Dispatcher.VerifyAccess();
        if (NeedsArrange)
        {
            return;
        }

        NeedsArrange = true;
        _arrangeInvalidationCount++;
        _layoutVersionStamp++;
        RecordInvalidationDiagnostics(UiInvalidationType.Arrange, origin, source, reason);
        MarkSubtreeDirty();
        UiRoot.Current?.NotifyInvalidation(UiInvalidationType.Arrange, this);
        RunWithInvalidationContext(origin, this, $"arrange<={reason}", InvalidateVisual);
        var invalidationParent = GetInvalidationParent();
        if (invalidationParent != null)
        {
            RunWithInvalidationContext(origin, this, reason, invalidationParent.InvalidateArrange);
        }
    }

    private void InvalidateVisualCore(UIElement origin, UIElement? source, string reason)
    {
        Dispatcher.VerifyAccess();
        var renderInvalidationSource = ReferenceEquals(origin, this)
            ? this
            : origin;
        if (NeedsRender)
        {
            UiRoot.Current?.EnsureRenderInvalidationTracked(renderInvalidationSource);
            return;
        }

        NeedsRender = true;
        _renderInvalidationCount++;
        _renderVersionStamp++;
        RecordInvalidationDiagnostics(UiInvalidationType.Render, origin, source, reason);
        MarkSubtreeDirty();
        UiRoot.Current?.NotifyInvalidation(UiInvalidationType.Render, renderInvalidationSource);
    }

    private void RecordInvalidationDiagnostics(UiInvalidationType type, UIElement origin, UIElement? source, string reason)
    {
        var diagnostics = _invalidationDiagnostics ??= new ElementInvalidationDiagnostics();
        var immediateSource = source ?? this;
        var summary = BuildInvalidationSummary(origin, immediateSource, reason);
        var currentRoot = UiRoot.Current;
        var layoutFrame = currentRoot?.LayoutExecutedFrameCount ?? -1;
        var drawFrame = currentRoot?.DrawExecutedFrameCount ?? -1;
        var propagated = !ReferenceEquals(immediateSource, this);

        switch (type)
        {
            case UiInvalidationType.Measure:
                if (propagated)
                {
                    diagnostics.PropagatedMeasureInvalidationCount++;
                }
                else
                {
                    diagnostics.DirectMeasureInvalidationCount++;
                }

                diagnostics.LastMeasureInvalidationSummary = summary;
                diagnostics.LastMeasureInvalidationLayoutFrame = layoutFrame;
                diagnostics.LastMeasureInvalidationDrawFrame = drawFrame;
                RecordInvalidationSource(diagnostics.MeasureSources, summary);
                break;

            case UiInvalidationType.Arrange:
                if (propagated)
                {
                    diagnostics.PropagatedArrangeInvalidationCount++;
                }
                else
                {
                    diagnostics.DirectArrangeInvalidationCount++;
                }

                diagnostics.LastArrangeInvalidationSummary = summary;
                diagnostics.LastArrangeInvalidationLayoutFrame = layoutFrame;
                diagnostics.LastArrangeInvalidationDrawFrame = drawFrame;
                RecordInvalidationSource(diagnostics.ArrangeSources, summary);
                break;

            case UiInvalidationType.Render:
                if (propagated)
                {
                    diagnostics.PropagatedRenderInvalidationCount++;
                }
                else
                {
                    diagnostics.DirectRenderInvalidationCount++;
                }

                diagnostics.LastRenderInvalidationSummary = summary;
                diagnostics.LastRenderInvalidationLayoutFrame = layoutFrame;
                diagnostics.LastRenderInvalidationDrawFrame = drawFrame;
                RecordInvalidationSource(diagnostics.RenderSources, summary);
                break;
        }
    }

    private static string BuildInvalidationSummary(UIElement origin, UIElement source, string reason)
    {
        var originSummary = DescribeElementForInvalidation(origin);
        if (ReferenceEquals(origin, source))
        {
            return $"{reason}@{originSummary}";
        }

        return $"{reason}@{originSummary} via {DescribeElementForInvalidation(source)}";
    }

    private static void RecordInvalidationSource(Dictionary<string, int> sourceCounts, string summary)
    {
        if (sourceCounts.TryGetValue(summary, out var existing))
        {
            sourceCounts[summary] = existing + 1;
            return;
        }

        if (sourceCounts.Count < 8)
        {
            sourceCounts[summary] = 1;
            return;
        }

        sourceCounts["other"] = sourceCounts.TryGetValue("other", out var otherCount)
            ? otherCount + 1
            : 1;
    }

    private static string SummarizeInvalidationSourceCounts(IReadOnlyDictionary<string, int> sourceCounts, int limit)
    {
        if (sourceCounts.Count == 0)
        {
            return "none";
        }

        return string.Join(
            " | ",
            sourceCounts
                .OrderByDescending(static pair => pair.Value)
                .ThenBy(static pair => pair.Key, StringComparer.Ordinal)
                .Take(limit)
                .Select(static pair => $"{pair.Value}x {pair.Key}"));
    }

    private static string DescribeElementForInvalidation(UIElement element)
    {
        return element is FrameworkElement { Name.Length: > 0 } frameworkElement
            ? $"{frameworkElement.GetType().Name}#{frameworkElement.Name}"
            : element.GetType().Name;
    }

    private static void RunWithInvalidationContext(UIElement origin, UIElement immediateSource, string reason, Action action)
    {
        var previous = _currentInvalidationContext;
        _currentInvalidationContext = new InvalidationContext(origin, immediateSource, reason);
        try
        {
            action();
        }
        finally
        {
            _currentInvalidationContext = previous;
        }
    }

    private static bool TryInvertMatrix(Matrix matrix, out Matrix inverse)
    {
        inverse = Matrix.Invert(matrix);
        return IsFinite(inverse.M11) &&
               IsFinite(inverse.M12) &&
               IsFinite(inverse.M13) &&
               IsFinite(inverse.M14) &&
               IsFinite(inverse.M21) &&
               IsFinite(inverse.M22) &&
               IsFinite(inverse.M23) &&
               IsFinite(inverse.M24) &&
               IsFinite(inverse.M31) &&
               IsFinite(inverse.M32) &&
               IsFinite(inverse.M33) &&
               IsFinite(inverse.M34) &&
               IsFinite(inverse.M41) &&
               IsFinite(inverse.M42) &&
               IsFinite(inverse.M43) &&
               IsFinite(inverse.M44);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private bool TryGetLocalRenderBounds(out LayoutRect bounds)
    {
        var slot = LayoutSlot;
        if (slot.Width <= 0f || slot.Height <= 0f)
        {
            bounds = slot;
            return false;
        }

        bounds = Effect?.GetRenderBounds(this) ?? slot;
        return bounds.Width > 0f && bounds.Height > 0f;
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

    private static long MillisecondsToTicks(double milliseconds)
    {
        return (long)Math.Round(milliseconds * Stopwatch.Frequency / 1000d);
    }

    private sealed class ElementInvalidationDiagnostics
    {
        public int DirectMeasureInvalidationCount;
        public int PropagatedMeasureInvalidationCount;
        public string LastMeasureInvalidationSummary = "none";
        public int LastMeasureInvalidationLayoutFrame = -1;
        public int LastMeasureInvalidationDrawFrame = -1;
        public Dictionary<string, int> MeasureSources { get; } = new(StringComparer.Ordinal);
        public int DirectArrangeInvalidationCount;
        public int PropagatedArrangeInvalidationCount;
        public string LastArrangeInvalidationSummary = "none";
        public int LastArrangeInvalidationLayoutFrame = -1;
        public int LastArrangeInvalidationDrawFrame = -1;
        public Dictionary<string, int> ArrangeSources { get; } = new(StringComparer.Ordinal);
        public int DirectRenderInvalidationCount;
        public int PropagatedRenderInvalidationCount;
        public string LastRenderInvalidationSummary = "none";
        public int LastRenderInvalidationLayoutFrame = -1;
        public int LastRenderInvalidationDrawFrame = -1;
        public Dictionary<string, int> RenderSources { get; } = new(StringComparer.Ordinal);
    }

    private sealed class InvalidationContext(UIElement origin, UIElement immediateSource, string reason)
    {
        public UIElement Origin { get; } = origin;

        public UIElement ImmediateSource { get; } = immediateSource;

        public string Reason { get; } = reason;
    }
}
