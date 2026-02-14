using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public class UIElement : DependencyObject
{
    private const int RoutePoolMaxSize = 64;
    [ThreadStatic]
    private static Stack<List<UIElement>>? _routePool;

    static UIElement()
    {
        EventManager.RegisterClassHandler<UIElement, RoutedKeyEventArgs>(
            PreviewKeyDownEvent,
            static (element, args) => element.TryHandleInputBindings(args));
    }

    private readonly Dictionary<RoutedEvent, List<RoutedHandlerEntry>> _routedHandlers = new();
    private readonly List<CommandBinding> _commandBindings = new();
    private readonly List<InputBinding> _inputBindings = new();
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

    public static readonly DependencyProperty FocusableProperty =
        DependencyProperty.Register(
            nameof(Focusable),
            typeof(bool),
            typeof(UIElement),
            new FrameworkPropertyMetadata(false));

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

    public static readonly DependencyProperty CursorProperty =
        DependencyProperty.Register(
            nameof(Cursor),
            typeof(UiCursor),
            typeof(UIElement),
            new FrameworkPropertyMetadata(UiCursor.Arrow, FrameworkPropertyMetadataOptions.Inherits));

    public static readonly RoutedEvent MouseEnterEvent =
        new(nameof(MouseEnter), RoutingStrategy.Direct);

    public static readonly RoutedEvent MouseLeaveEvent =
        new(nameof(MouseLeave), RoutingStrategy.Direct);

    public static readonly RoutedEvent MouseMoveEvent =
        new(nameof(MouseMove), RoutingStrategy.Bubble);

    public static readonly RoutedEvent PreviewMouseMoveEvent =
        new(nameof(PreviewMouseMove), RoutingStrategy.Tunnel);

    public static readonly RoutedEvent MouseDownEvent =
        new(nameof(MouseDown), RoutingStrategy.Bubble);

    public static readonly RoutedEvent PreviewMouseDownEvent =
        new(nameof(PreviewMouseDown), RoutingStrategy.Tunnel);

    public static readonly RoutedEvent MouseUpEvent =
        new(nameof(MouseUp), RoutingStrategy.Bubble);

    public static readonly RoutedEvent PreviewMouseUpEvent =
        new(nameof(PreviewMouseUp), RoutingStrategy.Tunnel);

    public static readonly RoutedEvent MouseWheelEvent =
        new(nameof(MouseWheel), RoutingStrategy.Bubble);

    public static readonly RoutedEvent PreviewMouseWheelEvent =
        new(nameof(PreviewMouseWheel), RoutingStrategy.Tunnel);

    public static readonly RoutedEvent MouseDoubleClickEvent =
        new(nameof(MouseDoubleClick), RoutingStrategy.Bubble);

    public static readonly RoutedEvent MouseLeftButtonDownEvent =
        new(nameof(MouseLeftButtonDown), RoutingStrategy.Bubble);

    public static readonly RoutedEvent MouseLeftButtonUpEvent =
        new(nameof(MouseLeftButtonUp), RoutingStrategy.Bubble);

    public static readonly RoutedEvent MouseRightButtonDownEvent =
        new(nameof(MouseRightButtonDown), RoutingStrategy.Bubble);

    public static readonly RoutedEvent MouseRightButtonUpEvent =
        new(nameof(MouseRightButtonUp), RoutingStrategy.Bubble);

    public static readonly RoutedEvent GotMouseCaptureEvent =
        new(nameof(GotMouseCapture), RoutingStrategy.Direct);

    public static readonly RoutedEvent LostMouseCaptureEvent =
        new(nameof(LostMouseCapture), RoutingStrategy.Direct);

    public static readonly RoutedEvent KeyDownEvent =
        new(nameof(KeyDown), RoutingStrategy.Bubble);

    public static readonly RoutedEvent PreviewKeyDownEvent =
        new(nameof(PreviewKeyDown), RoutingStrategy.Tunnel);

    public static readonly RoutedEvent KeyUpEvent =
        new(nameof(KeyUp), RoutingStrategy.Bubble);

    public static readonly RoutedEvent PreviewKeyUpEvent =
        new(nameof(PreviewKeyUp), RoutingStrategy.Tunnel);

    public static readonly RoutedEvent TextInputEvent =
        new(nameof(TextInput), RoutingStrategy.Bubble);

    public static readonly RoutedEvent PreviewTextInputEvent =
        new(nameof(PreviewTextInput), RoutingStrategy.Tunnel);

    public static readonly RoutedEvent GotFocusEvent =
        new(nameof(GotFocus), RoutingStrategy.Bubble);

    public static readonly RoutedEvent LostFocusEvent =
        new(nameof(LostFocus), RoutingStrategy.Bubble);

    public static readonly RoutedEvent GotKeyboardFocusEvent =
        new(nameof(GotKeyboardFocus), RoutingStrategy.Bubble);

    public static readonly RoutedEvent LostKeyboardFocusEvent =
        new(nameof(LostKeyboardFocus), RoutingStrategy.Bubble);

    private LayoutRect _layoutSlot;

    public event System.EventHandler<RoutedMouseEventArgs>? MouseEnter;

    public event System.EventHandler<RoutedMouseEventArgs>? MouseLeave;

    public event System.EventHandler<RoutedMouseEventArgs>? MouseMove;

    public event System.EventHandler<RoutedMouseEventArgs>? PreviewMouseMove;

    public event System.EventHandler<RoutedMouseButtonEventArgs>? MouseDown;

    public event System.EventHandler<RoutedMouseButtonEventArgs>? PreviewMouseDown;

    public event System.EventHandler<RoutedMouseButtonEventArgs>? MouseUp;

    public event System.EventHandler<RoutedMouseButtonEventArgs>? PreviewMouseUp;

    public event System.EventHandler<RoutedMouseWheelEventArgs>? MouseWheel;

    public event System.EventHandler<RoutedMouseWheelEventArgs>? PreviewMouseWheel;

    public event System.EventHandler<RoutedMouseButtonEventArgs>? MouseDoubleClick;

    public event System.EventHandler<RoutedMouseButtonEventArgs>? MouseLeftButtonDown;

    public event System.EventHandler<RoutedMouseButtonEventArgs>? MouseLeftButtonUp;

    public event System.EventHandler<RoutedMouseButtonEventArgs>? MouseRightButtonDown;

    public event System.EventHandler<RoutedMouseButtonEventArgs>? MouseRightButtonUp;

    public event System.EventHandler<RoutedMouseCaptureEventArgs>? GotMouseCapture;

    public event System.EventHandler<RoutedMouseCaptureEventArgs>? LostMouseCapture;

    public event System.EventHandler<RoutedKeyEventArgs>? KeyDown;

    public event System.EventHandler<RoutedKeyEventArgs>? PreviewKeyDown;

    public event System.EventHandler<RoutedKeyEventArgs>? KeyUp;

    public event System.EventHandler<RoutedKeyEventArgs>? PreviewKeyUp;

    public event System.EventHandler<RoutedTextInputEventArgs>? TextInput;

    public event System.EventHandler<RoutedTextInputEventArgs>? PreviewTextInput;

    public event System.EventHandler<RoutedFocusEventArgs>? GotFocus;

    public event System.EventHandler<RoutedFocusEventArgs>? LostFocus;

    public event System.EventHandler<RoutedFocusEventArgs>? GotKeyboardFocus;

    public event System.EventHandler<RoutedFocusEventArgs>? LostKeyboardFocus;

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

    public bool Focusable
    {
        get => GetValue<bool>(FocusableProperty);
        set => SetValue(FocusableProperty, value);
    }

    public float Opacity
    {
        get => GetValue<float>(OpacityProperty);
        set => SetValue(OpacityProperty, value);
    }

    public UiCursor Cursor
    {
        get => GetValue<UiCursor>(CursorProperty);
        set => SetValue(CursorProperty, value);
    }

    public LayoutRect LayoutSlot => _layoutSlot;

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

    internal UiRoot? TryGetOwningUiRoot()
    {
        var currentRoot = UiRoot.Current;
        if (currentRoot == null)
        {
            return null;
        }

        return ReferenceEquals(GetVisualRoot(), currentRoot.RootElement)
            ? currentRoot
            : null;
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

            foreach (var child in GetVisualChildren())
            {
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

    public virtual bool HitTest(Vector2 point)
    {
        if (!IsVisible || !IsEnabled || !IsHitTestVisible)
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

    public bool CaptureMouse()
    {
        return InputManager.CaptureMouse(this);
    }

    public void ReleaseMouseCapture()
    {
        InputManager.ReleaseMouseCapture(this);
    }

    public bool Focus()
    {
        if (!Focusable)
        {
            return false;
        }

        return FocusManager.SetFocusedElement(this);
    }

    public void AddHandler<TArgs>(RoutedEvent routedEvent, System.EventHandler<TArgs> handler)
        where TArgs : RoutedEventArgs
    {
        AddHandler(routedEvent, handler, handledEventsToo: false);
    }

    public void AddHandler<TArgs>(
        RoutedEvent routedEvent,
        System.EventHandler<TArgs> handler,
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

    public void RemoveHandler<TArgs>(RoutedEvent routedEvent, System.EventHandler<TArgs> handler)
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

    internal void NotifyMouseEnter(Vector2 position, ModifierKeys modifiers)
    {
        RaiseMouseEnter(position, modifiers);
    }

    internal void NotifyMouseLeave(Vector2 position, ModifierKeys modifiers)
    {
        RaiseMouseLeave(position, modifiers);
    }

    internal void NotifyMouseMove(Vector2 position, ModifierKeys modifiers)
    {
        RaisePreviewMouseMove(position, modifiers);
        RaiseMouseMove(position, modifiers);
    }

    internal void NotifyMouseDown(Vector2 position, MouseButton button, int clickCount, ModifierKeys modifiers)
    {
        RaisePreviewMouseDown(position, button, clickCount, modifiers);
        RaiseMouseDown(position, button, clickCount, modifiers);
    }

    internal void NotifyMouseUp(Vector2 position, MouseButton button, int clickCount, ModifierKeys modifiers)
    {
        RaisePreviewMouseUp(position, button, clickCount, modifiers);
        RaiseMouseUp(position, button, clickCount, modifiers);
    }

    internal void NotifyMouseWheel(Vector2 position, int delta, ModifierKeys modifiers)
    {
        RaisePreviewMouseWheel(position, delta, modifiers);
        RaiseMouseWheel(position, delta, modifiers);
    }

    internal void NotifyKeyDown(Keys key, bool isRepeat, ModifierKeys modifiers)
    {
        var previewArgs = new RoutedKeyEventArgs(PreviewKeyDownEvent, key, isRepeat, modifiers);
        RaiseRoutedEvent(PreviewKeyDownEvent, previewArgs);
        if (previewArgs.Handled)
        {
            return;
        }

        var args = new RoutedKeyEventArgs(KeyDownEvent, key, isRepeat, modifiers);
        RaiseRoutedEvent(KeyDownEvent, args);
    }

    internal void NotifyKeyUp(Keys key, ModifierKeys modifiers)
    {
        RaisePreviewKeyUp(key, modifiers);
        RaiseKeyUp(key, modifiers);
    }

    internal void NotifyTextInput(char character)
    {
        RaisePreviewTextInput(character);
        RaiseTextInput(character);
    }

    internal void NotifyGotFocus(UIElement? relatedTarget)
    {
        RaiseGotFocus(relatedTarget);
        RaiseGotKeyboardFocus(relatedTarget);
    }

    internal void NotifyLostFocus(UIElement? relatedTarget)
    {
        RaiseLostFocus(relatedTarget);
        RaiseLostKeyboardFocus(relatedTarget);
    }

    internal void NotifyGotMouseCapture()
    {
        RaiseGotMouseCapture();
    }

    internal void NotifyLostMouseCapture()
    {
        RaiseLostMouseCapture();
    }

    internal void SetVisualParent(UIElement? parent)
    {
        if (ReferenceEquals(VisualParent, parent))
        {
            return;
        }

        var previousRoot = TryGetOwningUiRoot();
        var hadPreviousBounds = TryGetRenderBoundsInRootSpace(out var previousBounds);
        var oldParent = VisualParent;
        VisualParent = parent;
        FocusManager.NotifyFocusGraphInvalidated();
        OnVisualParentChanged(oldParent, parent);
        NotifyBindingTreeChanged(this);
        MarkParentTransitionVisualDirty(previousRoot, hadPreviousBounds, previousBounds);
    }

    internal void SetLogicalParent(UIElement? parent)
    {
        if (ReferenceEquals(LogicalParent, parent))
        {
            return;
        }

        var previousRoot = TryGetOwningUiRoot();
        var hadPreviousBounds = TryGetRenderBoundsInRootSpace(out var previousBounds);
        var oldParent = LogicalParent;
        LogicalParent = parent;
        FocusManager.NotifyFocusGraphInvalidated();
        OnLogicalParentChanged(oldParent, parent);
        NotifyBindingTreeChanged(this);
        MarkParentTransitionVisualDirty(previousRoot, hadPreviousBounds, previousBounds);
    }

    internal void SetLayoutSlot(LayoutRect layoutSlot)
    {
        _layoutSlot = layoutSlot;
    }

    protected virtual void OnVisualParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        var registeredProperties = new List<DependencyProperty>(DependencyProperty.GetRegisteredProperties());
        foreach (var property in registeredProperties)
        {
            if (!property.IsApplicableTo(this))
            {
                continue;
            }

            if (property.GetMetadata(this).Inherits)
            {
                NotifyInheritedPropertyChanged(property);
            }
        }
    }

    protected virtual void OnLogicalParentChanged(UIElement? oldParent, UIElement? newParent)
    {
    }

    private static void NotifyBindingTreeChanged(UIElement root)
    {
        NotifyBindingTreeChanged(root, new HashSet<UIElement>());
    }

    private void MarkParentTransitionVisualDirty(
        UiRoot? previousRoot,
        bool hadPreviousBounds,
        LayoutRect previousBounds)
    {
        if (hadPreviousBounds)
        {
            previousRoot?.MarkVisualDirty(previousBounds);
        }

        if (!TryGetRenderBoundsInRootSpace(out var currentBounds))
        {
            return;
        }

        TryGetOwningUiRoot()?.MarkVisualDirty(currentBounds);
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

    protected void RaiseMouseEnter(Vector2 position, ModifierKeys modifiers = ModifierKeys.None)
    {
        var args = new RoutedMouseEventArgs(MouseEnterEvent, position, modifiers);
        RaiseRoutedEvent(MouseEnterEvent, args);
    }

    protected void RaiseMouseLeave(Vector2 position, ModifierKeys modifiers = ModifierKeys.None)
    {
        var args = new RoutedMouseEventArgs(MouseLeaveEvent, position, modifiers);
        RaiseRoutedEvent(MouseLeaveEvent, args);
    }

    protected void RaiseMouseMove(Vector2 position, ModifierKeys modifiers = ModifierKeys.None)
    {
        var args = new RoutedMouseEventArgs(MouseMoveEvent, position, modifiers);
        RaiseRoutedEvent(MouseMoveEvent, args);
    }

    protected void RaisePreviewMouseMove(Vector2 position, ModifierKeys modifiers = ModifierKeys.None)
    {
        var args = new RoutedMouseEventArgs(PreviewMouseMoveEvent, position, modifiers);
        RaiseRoutedEvent(PreviewMouseMoveEvent, args);
    }

    protected void RaiseMouseDown(
        Vector2 position,
        MouseButton button,
        int clickCount = 1,
        ModifierKeys modifiers = ModifierKeys.None)
    {
        var mouseDownArgs = new RoutedMouseButtonEventArgs(MouseDownEvent, position, button, clickCount, modifiers);
        RaiseRoutedEvent(MouseDownEvent, mouseDownArgs);

        if (button == MouseButton.Left)
        {
            var leftArgs = new RoutedMouseButtonEventArgs(
                MouseLeftButtonDownEvent,
                position,
                button,
                clickCount,
                modifiers);
            RaiseRoutedEvent(MouseLeftButtonDownEvent, leftArgs);
        }
        else if (button == MouseButton.Right)
        {
            var rightArgs = new RoutedMouseButtonEventArgs(
                MouseRightButtonDownEvent,
                position,
                button,
                clickCount,
                modifiers);
            RaiseRoutedEvent(MouseRightButtonDownEvent, rightArgs);
        }

        if (clickCount == 2)
        {
            var doubleClickArgs = new RoutedMouseButtonEventArgs(
                MouseDoubleClickEvent,
                position,
                button,
                clickCount,
                modifiers);
            RaiseRoutedEvent(MouseDoubleClickEvent, doubleClickArgs);
        }
    }

    protected void RaisePreviewMouseDown(
        Vector2 position,
        MouseButton button,
        int clickCount = 1,
        ModifierKeys modifiers = ModifierKeys.None)
    {
        var args = new RoutedMouseButtonEventArgs(PreviewMouseDownEvent, position, button, clickCount, modifiers);
        RaiseRoutedEvent(PreviewMouseDownEvent, args);
    }

    protected void RaiseMouseUp(
        Vector2 position,
        MouseButton button,
        int clickCount = 1,
        ModifierKeys modifiers = ModifierKeys.None)
    {
        var mouseUpArgs = new RoutedMouseButtonEventArgs(MouseUpEvent, position, button, clickCount, modifiers);
        RaiseRoutedEvent(MouseUpEvent, mouseUpArgs);

        if (button == MouseButton.Left)
        {
            var leftArgs = new RoutedMouseButtonEventArgs(MouseLeftButtonUpEvent, position, button, clickCount, modifiers);
            RaiseRoutedEvent(MouseLeftButtonUpEvent, leftArgs);
        }
        else if (button == MouseButton.Right)
        {
            var rightArgs = new RoutedMouseButtonEventArgs(MouseRightButtonUpEvent, position, button, clickCount, modifiers);
            RaiseRoutedEvent(MouseRightButtonUpEvent, rightArgs);
        }
    }

    protected void RaisePreviewMouseUp(
        Vector2 position,
        MouseButton button,
        int clickCount = 1,
        ModifierKeys modifiers = ModifierKeys.None)
    {
        var args = new RoutedMouseButtonEventArgs(PreviewMouseUpEvent, position, button, clickCount, modifiers);
        RaiseRoutedEvent(PreviewMouseUpEvent, args);
    }

    protected void RaiseMouseWheel(Vector2 position, int delta, ModifierKeys modifiers = ModifierKeys.None)
    {
        var args = new RoutedMouseWheelEventArgs(MouseWheelEvent, position, delta, modifiers);
        RaiseRoutedEvent(MouseWheelEvent, args);
    }

    protected void RaisePreviewMouseWheel(Vector2 position, int delta, ModifierKeys modifiers = ModifierKeys.None)
    {
        var args = new RoutedMouseWheelEventArgs(PreviewMouseWheelEvent, position, delta, modifiers);
        RaiseRoutedEvent(PreviewMouseWheelEvent, args);
    }

    protected void RaiseGotMouseCapture()
    {
        var args = new RoutedMouseCaptureEventArgs(GotMouseCaptureEvent);
        RaiseRoutedEvent(GotMouseCaptureEvent, args);
    }

    protected void RaiseLostMouseCapture()
    {
        var args = new RoutedMouseCaptureEventArgs(LostMouseCaptureEvent);
        RaiseRoutedEvent(LostMouseCaptureEvent, args);
    }

    protected void RaiseKeyDown(Keys key, bool isRepeat = false, ModifierKeys modifiers = ModifierKeys.None)
    {
        var args = new RoutedKeyEventArgs(KeyDownEvent, key, isRepeat, modifiers);
        RaiseRoutedEvent(KeyDownEvent, args);
    }

    protected void RaisePreviewKeyDown(Keys key, bool isRepeat = false, ModifierKeys modifiers = ModifierKeys.None)
    {
        var args = new RoutedKeyEventArgs(PreviewKeyDownEvent, key, isRepeat, modifiers);
        RaiseRoutedEvent(PreviewKeyDownEvent, args);
    }

    protected void RaiseKeyUp(Keys key, ModifierKeys modifiers = ModifierKeys.None)
    {
        var args = new RoutedKeyEventArgs(KeyUpEvent, key, false, modifiers);
        RaiseRoutedEvent(KeyUpEvent, args);
    }

    protected void RaisePreviewKeyUp(Keys key, ModifierKeys modifiers = ModifierKeys.None)
    {
        var args = new RoutedKeyEventArgs(PreviewKeyUpEvent, key, false, modifiers);
        RaiseRoutedEvent(PreviewKeyUpEvent, args);
    }

    protected void RaiseTextInput(char character)
    {
        var args = new RoutedTextInputEventArgs(TextInputEvent, character);
        RaiseRoutedEvent(TextInputEvent, args);
    }

    protected void RaisePreviewTextInput(char character)
    {
        var args = new RoutedTextInputEventArgs(PreviewTextInputEvent, character);
        RaiseRoutedEvent(PreviewTextInputEvent, args);
    }

    protected void RaiseGotFocus(UIElement? relatedTarget = null)
    {
        var args = new RoutedFocusEventArgs(GotFocusEvent, relatedTarget);
        RaiseRoutedEvent(GotFocusEvent, args);
    }

    protected void RaiseLostFocus(UIElement? relatedTarget = null)
    {
        var args = new RoutedFocusEventArgs(LostFocusEvent, relatedTarget);
        RaiseRoutedEvent(LostFocusEvent, args);
    }

    protected void RaiseGotKeyboardFocus(UIElement? relatedTarget = null)
    {
        var args = new RoutedFocusEventArgs(GotKeyboardFocusEvent, relatedTarget);
        RaiseRoutedEvent(GotKeyboardFocusEvent, args);
    }

    protected void RaiseLostKeyboardFocus(UIElement? relatedTarget = null)
    {
        var args = new RoutedFocusEventArgs(LostKeyboardFocusEvent, relatedTarget);
        RaiseRoutedEvent(LostKeyboardFocusEvent, args);
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

    protected virtual void OnMouseEnter(RoutedMouseEventArgs args)
    {
        MouseEnter?.Invoke(this, args);
    }

    protected virtual void OnMouseLeave(RoutedMouseEventArgs args)
    {
        MouseLeave?.Invoke(this, args);
    }

    protected virtual void OnMouseMove(RoutedMouseEventArgs args)
    {
        MouseMove?.Invoke(this, args);
    }

    protected virtual void OnPreviewMouseMove(RoutedMouseEventArgs args)
    {
        PreviewMouseMove?.Invoke(this, args);
    }

    protected virtual void OnMouseDown(RoutedMouseButtonEventArgs args)
    {
        MouseDown?.Invoke(this, args);
    }

    protected virtual void OnPreviewMouseDown(RoutedMouseButtonEventArgs args)
    {
        PreviewMouseDown?.Invoke(this, args);
    }

    protected virtual void OnMouseUp(RoutedMouseButtonEventArgs args)
    {
        MouseUp?.Invoke(this, args);
    }

    protected virtual void OnPreviewMouseUp(RoutedMouseButtonEventArgs args)
    {
        PreviewMouseUp?.Invoke(this, args);
    }

    protected virtual void OnMouseWheel(RoutedMouseWheelEventArgs args)
    {
        MouseWheel?.Invoke(this, args);
    }

    protected virtual void OnPreviewMouseWheel(RoutedMouseWheelEventArgs args)
    {
        PreviewMouseWheel?.Invoke(this, args);
    }

    protected virtual void OnMouseDoubleClick(RoutedMouseButtonEventArgs args)
    {
        MouseDoubleClick?.Invoke(this, args);
    }

    protected virtual void OnMouseLeftButtonDown(RoutedMouseButtonEventArgs args)
    {
        MouseLeftButtonDown?.Invoke(this, args);
    }

    protected virtual void OnMouseLeftButtonUp(RoutedMouseButtonEventArgs args)
    {
        MouseLeftButtonUp?.Invoke(this, args);
    }

    protected virtual void OnMouseRightButtonDown(RoutedMouseButtonEventArgs args)
    {
        MouseRightButtonDown?.Invoke(this, args);
    }

    protected virtual void OnMouseRightButtonUp(RoutedMouseButtonEventArgs args)
    {
        MouseRightButtonUp?.Invoke(this, args);
    }

    protected virtual void OnGotMouseCapture(RoutedMouseCaptureEventArgs args)
    {
        GotMouseCapture?.Invoke(this, args);
    }

    protected virtual void OnLostMouseCapture(RoutedMouseCaptureEventArgs args)
    {
        LostMouseCapture?.Invoke(this, args);
    }

    protected virtual void OnKeyDown(RoutedKeyEventArgs args)
    {
        KeyDown?.Invoke(this, args);
    }

    protected virtual void OnPreviewKeyDown(RoutedKeyEventArgs args)
    {
        PreviewKeyDown?.Invoke(this, args);
    }

    protected virtual void OnKeyUp(RoutedKeyEventArgs args)
    {
        KeyUp?.Invoke(this, args);
    }

    protected virtual void OnPreviewKeyUp(RoutedKeyEventArgs args)
    {
        PreviewKeyUp?.Invoke(this, args);
    }

    protected virtual void OnTextInput(RoutedTextInputEventArgs args)
    {
        TextInput?.Invoke(this, args);
    }

    protected virtual void OnPreviewTextInput(RoutedTextInputEventArgs args)
    {
        PreviewTextInput?.Invoke(this, args);
    }

    protected virtual void OnGotFocus(RoutedFocusEventArgs args)
    {
        GotFocus?.Invoke(this, args);
    }

    protected virtual void OnLostFocus(RoutedFocusEventArgs args)
    {
        LostFocus?.Invoke(this, args);
    }

    protected virtual void OnGotKeyboardFocus(RoutedFocusEventArgs args)
    {
        GotKeyboardFocus?.Invoke(this, args);
    }

    protected virtual void OnLostKeyboardFocus(RoutedFocusEventArgs args)
    {
        LostKeyboardFocus?.Invoke(this, args);
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

        if (args.Handled)
        {
            return;
        }

        if (routedEvent == MouseEnterEvent)
        {
            OnMouseEnter((RoutedMouseEventArgs)args);
            return;
        }

        if (routedEvent == MouseLeaveEvent)
        {
            OnMouseLeave((RoutedMouseEventArgs)args);
            return;
        }

        if (routedEvent == MouseMoveEvent)
        {
            OnMouseMove((RoutedMouseEventArgs)args);
            return;
        }

        if (routedEvent == PreviewMouseMoveEvent)
        {
            OnPreviewMouseMove((RoutedMouseEventArgs)args);
            return;
        }

        if (routedEvent == MouseDownEvent)
        {
            OnMouseDown((RoutedMouseButtonEventArgs)args);
            return;
        }

        if (routedEvent == PreviewMouseDownEvent)
        {
            OnPreviewMouseDown((RoutedMouseButtonEventArgs)args);
            return;
        }

        if (routedEvent == MouseUpEvent)
        {
            OnMouseUp((RoutedMouseButtonEventArgs)args);
            return;
        }

        if (routedEvent == PreviewMouseUpEvent)
        {
            OnPreviewMouseUp((RoutedMouseButtonEventArgs)args);
            return;
        }

        if (routedEvent == MouseWheelEvent)
        {
            OnMouseWheel((RoutedMouseWheelEventArgs)args);
            return;
        }

        if (routedEvent == PreviewMouseWheelEvent)
        {
            OnPreviewMouseWheel((RoutedMouseWheelEventArgs)args);
            return;
        }

        if (routedEvent == MouseDoubleClickEvent)
        {
            OnMouseDoubleClick((RoutedMouseButtonEventArgs)args);
            return;
        }

        if (routedEvent == MouseLeftButtonDownEvent)
        {
            OnMouseLeftButtonDown((RoutedMouseButtonEventArgs)args);
            return;
        }

        if (routedEvent == MouseLeftButtonUpEvent)
        {
            OnMouseLeftButtonUp((RoutedMouseButtonEventArgs)args);
            return;
        }

        if (routedEvent == MouseRightButtonDownEvent)
        {
            OnMouseRightButtonDown((RoutedMouseButtonEventArgs)args);
            return;
        }

        if (routedEvent == MouseRightButtonUpEvent)
        {
            OnMouseRightButtonUp((RoutedMouseButtonEventArgs)args);
            return;
        }

        if (routedEvent == GotMouseCaptureEvent)
        {
            OnGotMouseCapture((RoutedMouseCaptureEventArgs)args);
            return;
        }

        if (routedEvent == LostMouseCaptureEvent)
        {
            OnLostMouseCapture((RoutedMouseCaptureEventArgs)args);
            return;
        }

        if (routedEvent == KeyDownEvent)
        {
            OnKeyDown((RoutedKeyEventArgs)args);
            return;
        }

        if (routedEvent == PreviewKeyDownEvent)
        {
            OnPreviewKeyDown((RoutedKeyEventArgs)args);
            return;
        }

        if (routedEvent == KeyUpEvent)
        {
            OnKeyUp((RoutedKeyEventArgs)args);
            return;
        }

        if (routedEvent == PreviewKeyUpEvent)
        {
            OnPreviewKeyUp((RoutedKeyEventArgs)args);
            return;
        }

        if (routedEvent == TextInputEvent)
        {
            OnTextInput((RoutedTextInputEventArgs)args);
            return;
        }

        if (routedEvent == PreviewTextInputEvent)
        {
            OnPreviewTextInput((RoutedTextInputEventArgs)args);
            return;
        }

        if (routedEvent == GotFocusEvent)
        {
            OnGotFocus((RoutedFocusEventArgs)args);
            return;
        }

        if (routedEvent == LostFocusEvent)
        {
            OnLostFocus((RoutedFocusEventArgs)args);
            return;
        }

        if (routedEvent == GotKeyboardFocusEvent)
        {
            OnGotKeyboardFocus((RoutedFocusEventArgs)args);
            return;
        }

        if (routedEvent == LostKeyboardFocusEvent)
        {
            OnLostKeyboardFocus((RoutedFocusEventArgs)args);
        }
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

    private void TryHandleInputBindings(RoutedKeyEventArgs args)
    {
        if (args.Handled || args.IsRepeat)
        {
            return;
        }

        if (_inputBindings.Count == 0)
        {
            return;
        }

        for (var i = 0; i < _inputBindings.Count; i++)
        {
            var binding = _inputBindings[i];
            if (!binding.Gesture.Matches(args))
            {
                continue;
            }

            var target = binding.CommandTarget ?? this;
            if (binding.Command is RoutedCommand routedCommand)
            {
                if (!CommandManager.CanExecute(routedCommand, binding.CommandParameter, target))
                {
                    continue;
                }

                CommandManager.Execute(routedCommand, binding.CommandParameter, target);
                args.Handled = true;
                return;
            }

            if (!binding.Command.CanExecute(binding.CommandParameter))
            {
                continue;
            }

            binding.Command.Execute(binding.CommandParameter);
            args.Handled = true;
            return;
        }
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (ReferenceEquals(args.Property, IsEnabledProperty) ||
            ReferenceEquals(args.Property, IsVisibleProperty) ||
            ReferenceEquals(args.Property, FocusableProperty))
        {
            FocusManager.NotifyFocusGraphInvalidated();
            if (args.NewValue is bool state && !state)
            {
                InputManager.NotifyElementStateInvalidated(this);
            }
        }

        if (!args.Property.GetMetadata(this).Inherits)
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
