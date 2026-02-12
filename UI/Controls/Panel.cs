using System;
using System.Collections.Generic;
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

    public void AddChild(UIElement child)
    {
        if (_children.Contains(child))
        {
            return;
        }

        _children.Add(child);
        child.DependencyPropertyChanged += OnChildDependencyPropertyChanged;
        child.SetVisualParent(this);
        child.SetLogicalParent(this);
        InvalidateMeasure();
    }

    public bool RemoveChild(UIElement child)
    {
        if (!_children.Remove(child))
        {
            return false;
        }

        child.DependencyPropertyChanged -= OnChildDependencyPropertyChanged;
        child.SetVisualParent(null);
        child.SetLogicalParent(null);
        InvalidateMeasure();
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
            InvalidateArrange();
        }
    }

    private IReadOnlyList<UIElement> GetChildrenOrderedByZIndex()
    {
        _zOrderedChildrenCache.Clear();
        _zOrderedChildrenCache.AddRange(_children);
        _zOrderedChildrenCache.Sort(CompareChildrenByZIndex);
        return _zOrderedChildrenCache;
    }

    private int CompareChildrenByZIndex(UIElement first, UIElement second)
    {
        var zIndexComparison = GetZIndex(first).CompareTo(GetZIndex(second));
        if (zIndexComparison != 0)
        {
            return zIndexComparison;
        }

        return _children.IndexOf(first).CompareTo(_children.IndexOf(second));
    }
}
