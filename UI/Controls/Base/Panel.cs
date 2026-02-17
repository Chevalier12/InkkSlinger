using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class Panel : FrameworkElement
{
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
        var diagnosticsStart = Stopwatch.GetTimestamp();
        var phaseStart = diagnosticsStart;
        var exists = _children.Contains(child);
        UiFrameworkPopulationPhaseDiagnostics.Observe($"{GetType().Name}.AddChild.Contains", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);
        if (exists)
        {
            return;
        }

        phaseStart = Stopwatch.GetTimestamp();
        _children.Add(child);
        UiFrameworkPopulationPhaseDiagnostics.Observe($"{GetType().Name}.AddChild.ListAdd", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);

        phaseStart = Stopwatch.GetTimestamp();
        child.DependencyPropertyChanged += OnChildDependencyPropertyChanged;
        child.SetVisualParent(this);
        child.SetLogicalParent(this);
        UiFrameworkPopulationPhaseDiagnostics.Observe($"{GetType().Name}.AddChild.Parenting", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);

        phaseStart = Stopwatch.GetTimestamp();
        _zOrderCacheDirty = true;
        InvalidateMeasure();
        UiFrameworkPopulationPhaseDiagnostics.Observe($"{GetType().Name}.AddChild.Invalidate", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);
        UiFrameworkFileLoadDiagnostics.Observe($"{GetType().Name}.AddChild", Stopwatch.GetElapsedTime(diagnosticsStart).TotalMilliseconds);
    }

    public virtual void InsertChild(int index, UIElement child)
    {
        var diagnosticsStart = Stopwatch.GetTimestamp();
        var phaseStart = diagnosticsStart;
        var exists = _children.Contains(child);
        UiFrameworkPopulationPhaseDiagnostics.Observe($"{GetType().Name}.InsertChild.Contains", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);
        if (exists)
        {
            return;
        }

        phaseStart = Stopwatch.GetTimestamp();
        var clamped = Math.Clamp(index, 0, _children.Count);
        UiFrameworkPopulationPhaseDiagnostics.Observe($"{GetType().Name}.InsertChild.Clamp", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);

        phaseStart = Stopwatch.GetTimestamp();
        _children.Insert(clamped, child);
        UiFrameworkPopulationPhaseDiagnostics.Observe($"{GetType().Name}.InsertChild.ListInsert", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);

        phaseStart = Stopwatch.GetTimestamp();
        child.DependencyPropertyChanged += OnChildDependencyPropertyChanged;
        child.SetVisualParent(this);
        child.SetLogicalParent(this);
        UiFrameworkPopulationPhaseDiagnostics.Observe($"{GetType().Name}.InsertChild.Parenting", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);

        phaseStart = Stopwatch.GetTimestamp();
        _zOrderCacheDirty = true;
        InvalidateMeasure();
        UiFrameworkPopulationPhaseDiagnostics.Observe($"{GetType().Name}.InsertChild.Invalidate", Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);
        UiFrameworkFileLoadDiagnostics.Observe($"{GetType().Name}.InsertChild", Stopwatch.GetElapsedTime(diagnosticsStart).TotalMilliseconds);
    }

    public virtual bool RemoveChild(UIElement child)
    {
        var diagnosticsStart = Stopwatch.GetTimestamp();
        if (!_children.Remove(child))
        {
            return false;
        }

        child.DependencyPropertyChanged -= OnChildDependencyPropertyChanged;
        child.SetVisualParent(null);
        child.SetLogicalParent(null);
        _zOrderCacheDirty = true;
        InvalidateMeasure();
        UiFrameworkPopulationPhaseDiagnostics.Observe($"{GetType().Name}.RemoveChild.Total", Stopwatch.GetElapsedTime(diagnosticsStart).TotalMilliseconds);
        UiFrameworkFileLoadDiagnostics.Observe($"{GetType().Name}.RemoveChild", Stopwatch.GetElapsedTime(diagnosticsStart).TotalMilliseconds);
        return true;
    }

    public virtual bool RemoveChildAt(int index)
    {
        var diagnosticsStart = Stopwatch.GetTimestamp();
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
        UiFrameworkPopulationPhaseDiagnostics.Observe($"{GetType().Name}.RemoveChildAt.Total", Stopwatch.GetElapsedTime(diagnosticsStart).TotalMilliseconds);
        UiFrameworkFileLoadDiagnostics.Observe($"{GetType().Name}.RemoveChildAt", Stopwatch.GetElapsedTime(diagnosticsStart).TotalMilliseconds);
        return true;
    }

    public virtual bool MoveChildRange(int oldIndex, int count, int newIndex)
    {
        var diagnosticsStart = Stopwatch.GetTimestamp();
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

        var clampedNew = Math.Clamp(newIndex, 0, _children.Count - 1);
        if (clampedNew == oldIndex)
        {
            return true;
        }

        var range = _children.GetRange(oldIndex, actualCount);
        _children.RemoveRange(oldIndex, actualCount);

        if (clampedNew > oldIndex)
        {
            clampedNew = Math.Max(0, clampedNew - actualCount);
        }

        clampedNew = Math.Clamp(clampedNew, 0, _children.Count);
        _children.InsertRange(clampedNew, range);
        _zOrderCacheDirty = true;
        InvalidateMeasure();
        UiFrameworkPopulationPhaseDiagnostics.Observe($"{GetType().Name}.MoveChildRange.Total", Stopwatch.GetElapsedTime(diagnosticsStart).TotalMilliseconds, actualCount);
        UiFrameworkFileLoadDiagnostics.Observe($"{GetType().Name}.MoveChildRange", Stopwatch.GetElapsedTime(diagnosticsStart).TotalMilliseconds, actualCount);
        return true;
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        foreach (var child in GetChildrenOrderedByZIndex())
        {
            yield return child;
        }
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
        var desired = Vector2.Zero;

        foreach (var child in _children)
        {
            if (child is not FrameworkElement frameworkChild)
            {
                continue;
            }

            frameworkChild.Measure(availableSize);
            desired.X = MathF.Max(desired.X, frameworkChild.DesiredSize.X);
            desired.Y = MathF.Max(desired.Y, frameworkChild.DesiredSize.Y);
        }

        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        foreach (var child in GetChildrenOrderedByZIndex())
        {
            if (child is not FrameworkElement frameworkChild)
            {
                continue;
            }

            frameworkChild.Arrange(new LayoutRect(LayoutSlot.X, LayoutSlot.Y, finalSize.X, finalSize.Y));
        }

        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        base.OnRender(spriteBatch);

        UiDrawing.DrawFilledRect(spriteBatch, LayoutSlot, Background, Opacity);
    }

    private void OnChildDependencyPropertyChanged(object? sender, DependencyPropertyChangedEventArgs args)
    {
        if (ReferenceEquals(args.Property, ZIndexProperty))
        {
            _zOrderCacheDirty = true;
            InvalidateArrange();
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
        _childOrderLookup.Clear();
        for (var i = 0; i < _children.Count; i++)
        {
            _childOrderLookup[_children[i]] = i;
        }

        _zOrderedChildrenCache.Sort(CompareChildrenByZIndex);
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
}
