using System;
using System.Collections.Generic;
using System.Diagnostics;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class Panel : FrameworkElement
{
    private static int _diagMeasureCallCount;
    private static long _diagMeasureElapsedTicks;
    private static int _diagMeasureChildCount;
    private static int _diagArrangeCallCount;
    private static long _diagArrangeElapsedTicks;
    private static int _diagArrangeChildCount;

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(Panel),
            new FrameworkPropertyMetadata(Color.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ZIndexProperty =
        DependencyProperty.RegisterAttached(
            "ZIndex",
            typeof(int),
            typeof(Panel),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly List<UIElement> _children = new();
    private readonly List<UIElement> _zOrderedChildrenCache = new();
    private readonly Dictionary<UIElement, int> _childOrderLookup = new();
    private bool _zOrderCacheDirty = true;

    public IReadOnlyList<UIElement> Children => _children;

    public Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public static int GetZIndex(UIElement element)
    {
        return element.GetValue<int>(ZIndexProperty);
    }

    public static void SetZIndex(UIElement element, int zIndex)
    {
        element.SetValue(ZIndexProperty, zIndex);
    }

    public virtual void AddChild(UIElement child)
    {
        if (_children.Contains(child))
        {
            return;
        }

        ValidateChildOwnership(child);
        AttachChild(_children.Count, child);
    }

    public virtual void InsertChild(int index, UIElement child)
    {
        if (_children.Contains(child))
        {
            return;
        }

        ValidateChildOwnership(child);
        AttachChild(index, child);
    }

    public virtual bool RemoveChild(UIElement child)
    {
        if (!_children.Remove(child))
        {
            return false;
        }

        child.DependencyPropertyChanged -= OnChildDependencyPropertyChanged;
        child.SetVisualParent(null);
        child.SetLogicalParent(null);
        _zOrderCacheDirty = true;
        InvalidateMeasure();
        return true;
    }

    public virtual bool RemoveChildAt(int index)
    {
        if (index < 0 || index >= _children.Count)
        {
            return false;
        }

        var child = _children[index];
        _children.RemoveAt(index);
        child.DependencyPropertyChanged -= OnChildDependencyPropertyChanged;
        child.SetVisualParent(null);
        child.SetLogicalParent(null);
        _zOrderCacheDirty = true;
        InvalidateMeasure();
        return true;
    }

    public virtual bool MoveChildRange(int oldIndex, int count, int newIndex)
    {
        if (count <= 0 || _children.Count == 0)
        {
            return false;
        }

        if (oldIndex < 0 || oldIndex >= _children.Count)
        {
            return false;
        }

        var actualCount = Math.Min(count, _children.Count - oldIndex);
        if (actualCount <= 0)
        {
            return false;
        }

        var clampedNew = Math.Clamp(newIndex, 0, _children.Count);
        if (actualCount == 1 && clampedNew == oldIndex)
        {
            return true;
        }

        var range = _children.GetRange(oldIndex, actualCount);
        _children.RemoveRange(oldIndex, actualCount);
        clampedNew = Math.Clamp(clampedNew, 0, _children.Count);

        if (clampedNew == oldIndex)
        {
            _children.InsertRange(oldIndex, range);
            return true;
        }

        _children.InsertRange(clampedNew, range);
        NotifyVisualChildOrderChanged(invalidateMeasure: true);
        return true;
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        foreach (var child in GetChildrenOrderedByZIndex())
        {
            yield return child;
        }
    }

    internal override int GetVisualChildCountForTraversal()
    {
        return _children.Count;
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        if ((uint)index < (uint)_children.Count)
        {
            return _children[index];
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        foreach (var child in _children)
        {
            yield return child;
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var startTicks = Stopwatch.GetTimestamp();
        var desired = Vector2.Zero;
        var measuredChildren = 0;

        foreach (var child in _children)
        {
            if (child is not FrameworkElement frameworkChild)
            {
                continue;
            }

            frameworkChild.Measure(availableSize);
            measuredChildren++;
            desired.X = MathF.Max(desired.X, frameworkChild.DesiredSize.X);
            desired.Y = MathF.Max(desired.Y, frameworkChild.DesiredSize.Y);
        }

        _diagMeasureCallCount++;
        _diagMeasureElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _diagMeasureChildCount += measuredChildren;

        return desired;
    }

    protected override bool CanReuseMeasureForAvailableSizeChange(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
    {
        if (GetType() != typeof(Panel))
        {
            return false;
        }

        foreach (var child in _children)
        {
            if (child is FrameworkElement frameworkChild &&
                !frameworkChild.CanReuseMeasureForAvailableSizeChangeForParentLayout(previousAvailableSize, nextAvailableSize))
            {
                return false;
            }
        }

        return true;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var startTicks = Stopwatch.GetTimestamp();
        var arrangedChildren = 0;
        foreach (var child in GetChildrenOrderedByZIndex())
        {
            if (child is not FrameworkElement frameworkChild)
            {
                continue;
            }

            arrangedChildren++;
            frameworkChild.Arrange(new LayoutRect(LayoutSlot.X, LayoutSlot.Y, finalSize.X, finalSize.Y));
        }

        _diagArrangeCallCount++;
        _diagArrangeElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _diagArrangeChildCount += arrangedChildren;

        return finalSize;
    }

    protected internal override bool ShouldSuppressMeasureInvalidationFromDescendantDuringMeasure(FrameworkElement descendant)
    {
        return (IsMeasuring || IsArrangingOverride) && IsDescendantOfOwnedChildSubtree(descendant);
    }

    internal new static PanelTelemetrySnapshot GetTelemetryAndReset()
    {
        var snapshot = new PanelTelemetrySnapshot(
            _diagMeasureCallCount,
            (double)_diagMeasureElapsedTicks * 1000d / Stopwatch.Frequency,
            _diagMeasureChildCount,
            _diagArrangeCallCount,
            (double)_diagArrangeElapsedTicks * 1000d / Stopwatch.Frequency,
            _diagArrangeChildCount);
        _diagMeasureCallCount = 0;
        _diagMeasureElapsedTicks = 0L;
        _diagMeasureChildCount = 0;
        _diagArrangeCallCount = 0;
        _diagArrangeElapsedTicks = 0L;
        _diagArrangeChildCount = 0;
        return snapshot;
    }

    private bool IsDescendantOfOwnedChildSubtree(FrameworkElement descendant)
    {
        for (UIElement? current = descendant; current != null; current = current.GetInvalidationParent())
        {
            if (ReferenceEquals(current, this))
            {
                return true;
            }

            if (_children.Contains(current))
            {
                return true;
            }
        }

        return false;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        base.OnRender(spriteBatch);

        UiDrawing.DrawFilledRect(spriteBatch, LayoutSlot, Background, Opacity);
    }

    protected override bool TryGetLocalRenderTransform(out Matrix transform, out Matrix inverseTransform)
    {
        var hasBaseTransform = base.TryGetLocalRenderTransform(out var baseTransform, out var baseInverseTransform);
        if (this is VirtualizingStackPanel)
        {
            transform = baseTransform;
            inverseTransform = baseInverseTransform;
            return hasBaseTransform;
        }

        if (!ScrollViewer.GetUseTransformContentScrolling(this))
        {
            transform = baseTransform;
            inverseTransform = baseInverseTransform;
            return hasBaseTransform;
        }

        var viewer = FindDirectOwningScrollViewer();
        if (viewer == null)
        {
            transform = baseTransform;
            inverseTransform = baseInverseTransform;
            return hasBaseTransform;
        }

        var offsetX = -viewer.HorizontalOffset;
        var offsetY = -viewer.VerticalOffset;
        if (MathF.Abs(offsetX) <= 0.01f && MathF.Abs(offsetY) <= 0.01f)
        {
            transform = baseTransform;
            inverseTransform = baseInverseTransform;
            return hasBaseTransform;
        }

        var localScrollTransform = Matrix.CreateTranslation(offsetX, offsetY, 0f);
        var localScrollInverseTransform = Matrix.CreateTranslation(-offsetX, -offsetY, 0f);
        return TryComposeLocalTransforms(
            hasPrimaryTransform: localScrollTransform != Matrix.Identity,
            primaryTransform: localScrollTransform,
            primaryInverse: localScrollInverseTransform,
            hasSecondaryTransform: hasBaseTransform,
            secondaryTransform: baseTransform,
            secondaryInverse: baseInverseTransform,
            out transform,
            out inverseTransform);
    }

    private void OnChildDependencyPropertyChanged(object? sender, DependencyPropertyChangedEventArgs args)
    {
        if (ReferenceEquals(args.Property, ZIndexProperty))
        {
            NotifyVisualChildOrderChanged(invalidateMeasure: false);
        }
    }

    internal IReadOnlyList<UIElement> GetChildrenOrderedByZIndex()
    {
        if (!_zOrderCacheDirty)
        {
            return _zOrderedChildrenCache;
        }

        _zOrderedChildrenCache.Clear();
        _zOrderedChildrenCache.AddRange(_children);
        if (!IsZOrderCacheAlreadySorted())
        {
            _childOrderLookup.Clear();
            for (var i = 0; i < _children.Count; i++)
            {
                _childOrderLookup[_children[i]] = i;
            }

            _zOrderedChildrenCache.Sort(CompareChildrenByZIndex);
        }
        else
        {
            _childOrderLookup.Clear();
        }

        _zOrderCacheDirty = false;
        return _zOrderedChildrenCache;
    }

    private int CompareChildrenByZIndex(UIElement first, UIElement second)
    {
        var zIndexComparison = GetZIndex(first).CompareTo(GetZIndex(second));
        if (zIndexComparison != 0)
        {
            return zIndexComparison;
        }

        var firstOrder = _childOrderLookup.TryGetValue(first, out var firstIndex) ? firstIndex : int.MaxValue;
        var secondOrder = _childOrderLookup.TryGetValue(second, out var secondIndex) ? secondIndex : int.MaxValue;
        return firstOrder.CompareTo(secondOrder);
    }

    private ScrollViewer? FindDirectOwningScrollViewer()
    {
        if (VisualParent is ScrollViewer visualOwner && ReferenceEquals(visualOwner.Content, this))
        {
            return visualOwner;
        }

        if (LogicalParent is ScrollViewer logicalOwner && ReferenceEquals(logicalOwner.Content, this))
        {
            return logicalOwner;
        }

        return null;
    }

    private void ValidateChildOwnership(UIElement child)
    {
        if ((child.VisualParent != null && !ReferenceEquals(child.VisualParent, this)) ||
            (child.LogicalParent != null && !ReferenceEquals(child.LogicalParent, this)))
        {
            throw new InvalidOperationException("UIElement already has a parent.");
        }
    }

    private void AttachChild(int index, UIElement child)
    {
        _children.Insert(Math.Clamp(index, 0, _children.Count), child);
        child.DependencyPropertyChanged += OnChildDependencyPropertyChanged;
        child.SetVisualParent(this);
        child.SetLogicalParent(this);
        NotifyVisualChildOrderChanged(invalidateMeasure: true);
    }

    private void NotifyVisualChildOrderChanged(bool invalidateMeasure)
    {
        _zOrderCacheDirty = true;
        if (invalidateMeasure)
        {
            InvalidateMeasure();
        }
        else
        {
            InvalidateArrange();
        }

        UiRoot.Current?.NotifyVisualStructureChanged(this, VisualParent, VisualParent);
    }

    private bool IsZOrderCacheAlreadySorted()
    {
        if (_zOrderedChildrenCache.Count <= 1)
        {
            return true;
        }

        var previousZIndex = GetZIndex(_zOrderedChildrenCache[0]);
        for (var i = 1; i < _zOrderedChildrenCache.Count; i++)
        {
            var currentZIndex = GetZIndex(_zOrderedChildrenCache[i]);
            if (currentZIndex < previousZIndex)
            {
                return false;
            }

            previousZIndex = currentZIndex;
        }

        return true;
    }
}


