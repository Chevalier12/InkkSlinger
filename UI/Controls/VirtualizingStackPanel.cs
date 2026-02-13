using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class VirtualizingStackPanel : Panel, IScrollInfo
{
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(VirtualizingStackPanel),
            new FrameworkPropertyMetadata(
                Orientation.Vertical,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty IsVirtualizingProperty =
        DependencyProperty.RegisterAttached(
            nameof(IsVirtualizing),
            typeof(bool),
            typeof(VirtualizingStackPanel),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty VirtualizationModeProperty =
        DependencyProperty.RegisterAttached(
            nameof(VirtualizationMode),
            typeof(VirtualizationMode),
            typeof(VirtualizingStackPanel),
            new FrameworkPropertyMetadata(
                VirtualizationMode.Standard,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty CacheLengthProperty =
        DependencyProperty.RegisterAttached(
            nameof(CacheLength),
            typeof(float),
            typeof(VirtualizingStackPanel),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                coerceValueCallback: static (_, value) => value is float v && v >= 0f ? v : 0f));

    public static readonly DependencyProperty CacheLengthUnitProperty =
        DependencyProperty.RegisterAttached(
            nameof(CacheLengthUnit),
            typeof(VirtualizationCacheLengthUnit),
            typeof(VirtualizingStackPanel),
            new FrameworkPropertyMetadata(
                VirtualizationCacheLengthUnit.Page,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    private readonly List<float> _primarySizes = new();
    private readonly List<float> _secondarySizes = new();
    private readonly List<float> _startOffsets = new();

    private float _averagePrimarySize = 28f;
    private float _maxSecondarySize;
    private float _extentWidth;
    private float _extentHeight;
    private float _viewportWidth;
    private float _viewportHeight;
    private float _horizontalOffset;
    private float _verticalOffset;
    private bool _canHorizontallyScroll;
    private bool _canVerticallyScroll = true;
    private ScrollViewer? _scrollOwner;

    private bool _startOffsetsDirty = true;
    private bool _isVirtualizationActive;

    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public bool IsVirtualizing
    {
        get => GetIsVirtualizing(this);
        set => SetIsVirtualizing(this, value);
    }

    public VirtualizationMode VirtualizationMode
    {
        get => GetVirtualizationMode(this);
        set => SetVirtualizationMode(this, value);
    }

    public float CacheLength
    {
        get => GetCacheLength(this);
        set => SetCacheLength(this, value);
    }

    public VirtualizationCacheLengthUnit CacheLengthUnit
    {
        get => GetCacheLengthUnit(this);
        set => SetCacheLengthUnit(this, value);
    }

    public int FirstRealizedIndex { get; private set; } = -1;

    public int LastRealizedIndex { get; private set; } = -1;

    public int RealizedChildrenCount { get; private set; }

    public bool IsVirtualizationActive => _isVirtualizationActive;

    public bool CanHorizontallyScroll
    {
        get => _canHorizontallyScroll;
        set => _canHorizontallyScroll = value;
    }

    public bool CanVerticallyScroll
    {
        get => _canVerticallyScroll;
        set => _canVerticallyScroll = value;
    }

    public float ExtentWidth => _extentWidth;

    public float ExtentHeight => _extentHeight;

    public float ViewportWidth => _viewportWidth;

    public float ViewportHeight => _viewportHeight;

    public float HorizontalOffset => _horizontalOffset;

    public float VerticalOffset => _verticalOffset;

    public ScrollViewer? ScrollOwner
    {
        get => _scrollOwner;
        set => _scrollOwner = value;
    }

    public static bool GetIsVirtualizing(UIElement element)
    {
        return element.GetValue<bool>(IsVirtualizingProperty);
    }

    public static void SetIsVirtualizing(UIElement element, bool value)
    {
        element.SetValue(IsVirtualizingProperty, value);
    }

    public static VirtualizationMode GetVirtualizationMode(UIElement element)
    {
        return element.GetValue<VirtualizationMode>(VirtualizationModeProperty);
    }

    public static void SetVirtualizationMode(UIElement element, VirtualizationMode value)
    {
        element.SetValue(VirtualizationModeProperty, value);
    }

    public static float GetCacheLength(UIElement element)
    {
        return element.GetValue<float>(CacheLengthProperty);
    }

    public static void SetCacheLength(UIElement element, float value)
    {
        element.SetValue(CacheLengthProperty, value);
    }

    public static VirtualizationCacheLengthUnit GetCacheLengthUnit(UIElement element)
    {
        return element.GetValue<VirtualizationCacheLengthUnit>(CacheLengthUnitProperty);
    }

    public static void SetCacheLengthUnit(UIElement element, VirtualizationCacheLengthUnit value)
    {
        element.SetValue(CacheLengthUnitProperty, value);
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        if (!_isVirtualizationActive || FirstRealizedIndex < 0 || LastRealizedIndex < FirstRealizedIndex)
        {
            foreach (var child in Children)
            {
                yield return child;
            }

            yield break;
        }

        var first = Math.Max(0, FirstRealizedIndex);
        var last = Math.Min(Children.Count - 1, LastRealizedIndex);
        for (var i = first; i <= last; i++)
        {
            yield return Children[i];
        }
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        if (!_isVirtualizationActive || FirstRealizedIndex < 0 || LastRealizedIndex < FirstRealizedIndex)
        {
            foreach (var child in Children)
            {
                yield return child;
            }

            yield break;
        }

        var first = Math.Max(0, FirstRealizedIndex);
        var last = Math.Min(Children.Count - 1, LastRealizedIndex);
        for (var i = first; i <= last; i++)
        {
            yield return Children[i];
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        EnsureCacheLength();

        if (Children.Count == 0)
        {
            SetRealization(-1, -1);
            _isVirtualizationActive = false;
            _maxSecondarySize = 0f;
            _extentWidth = 0f;
            _extentHeight = 0f;
            _viewportWidth = 0f;
            _viewportHeight = 0f;
            CoerceOffsets();
            NotifyScrollOwner();
            return Vector2.Zero;
        }

        var context = ResolveViewportContext(availableSize);
        _isVirtualizationActive = IsVirtualizationActiveContext(context);

        if (!_isVirtualizationActive)
        {
            return MeasureAllChildren(availableSize);
        }

        var first = ResolveStartIndex(context.StartOffset);
        var last = ResolveEndIndex(context.EndOffset, first);
        MeasureRange(availableSize, first, last);
        SetRealization(first, last);

        var extentPrimary = GetTotalPrimarySize();
        var extentSecondary = ResolveExtentSecondary(availableSize);

        var desired = Orientation == Orientation.Vertical
            ? new Vector2(extentSecondary, extentPrimary)
            : new Vector2(extentPrimary, extentSecondary);
        UpdateScrollDataFromMeasure(availableSize, desired);
        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (Children.Count == 0)
        {
            SetRealization(-1, -1);
            _isVirtualizationActive = false;
            _extentWidth = 0f;
            _extentHeight = 0f;
            _viewportWidth = MathF.Max(0f, finalSize.X);
            _viewportHeight = MathF.Max(0f, finalSize.Y);
            CoerceOffsets();
            NotifyScrollOwner();
            return finalSize;
        }

        var context = ResolveViewportContext(finalSize);
        _isVirtualizationActive = IsVirtualizationActiveContext(context);

        if (_isVirtualizationActive)
        {
            var first = ResolveStartIndex(context.StartOffset);
            var last = ResolveEndIndex(context.EndOffset, first);
            SetRealization(first, last);
            ArrangeRange(finalSize, first, last);
        }
        else
        {
            SetRealization(0, Children.Count - 1);
            ArrangeRange(finalSize, 0, Children.Count - 1);
        }

        UpdateViewportFromFinalSize(finalSize);
        NotifyScrollOwner();
        return finalSize;
    }

    protected override void OnVisualParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        base.OnVisualParentChanged(oldParent, newParent);
        InvalidateMeasure();
    }

    protected override void OnLogicalParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        base.OnLogicalParentChanged(oldParent, newParent);
        if (oldParent == null && newParent == null)
        {
            ScrollOwner = null;
        }
        InvalidateMeasure();
    }

    public void LineUp()
    {
        SetVerticalOffset(VerticalOffset - MathF.Max(1f, _averagePrimarySize));
    }

    public void LineDown()
    {
        SetVerticalOffset(VerticalOffset + MathF.Max(1f, _averagePrimarySize));
    }

    public void LineLeft()
    {
        SetHorizontalOffset(HorizontalOffset - MathF.Max(1f, _averagePrimarySize));
    }

    public void LineRight()
    {
        SetHorizontalOffset(HorizontalOffset + MathF.Max(1f, _averagePrimarySize));
    }

    public void PageUp()
    {
        SetVerticalOffset(VerticalOffset - MathF.Max(1f, ViewportHeight));
    }

    public void PageDown()
    {
        SetVerticalOffset(VerticalOffset + MathF.Max(1f, ViewportHeight));
    }

    public void PageLeft()
    {
        SetHorizontalOffset(HorizontalOffset - MathF.Max(1f, ViewportWidth));
    }

    public void PageRight()
    {
        SetHorizontalOffset(HorizontalOffset + MathF.Max(1f, ViewportWidth));
    }

    public void MouseWheelUp()
    {
        LineUp();
    }

    public void MouseWheelDown()
    {
        LineDown();
    }

    public void MouseWheelLeft()
    {
        LineLeft();
    }

    public void MouseWheelRight()
    {
        LineRight();
    }

    public void SetHorizontalOffset(float offset)
    {
        var next = MathF.Max(0f, MathF.Min(MaxHorizontalOffset(), offset));
        if (AreClose(next, _horizontalOffset))
        {
            return;
        }

        _horizontalOffset = next;
        InvalidateMeasure();
        InvalidateArrange();
        NotifyScrollOwner();
    }

    public void SetVerticalOffset(float offset)
    {
        var next = MathF.Max(0f, MathF.Min(MaxVerticalOffset(), offset));
        if (AreClose(next, _verticalOffset))
        {
            return;
        }

        _verticalOffset = next;
        InvalidateMeasure();
        InvalidateArrange();
        NotifyScrollOwner();
    }

    public LayoutRect MakeVisible(UIElement visual, LayoutRect rectangle)
    {
        if (Orientation == Orientation.Vertical)
        {
            var start = rectangle.Y;
            var end = rectangle.Y + rectangle.Height;
            if (start < VerticalOffset)
            {
                SetVerticalOffset(start);
            }
            else if (end > VerticalOffset + ViewportHeight)
            {
                SetVerticalOffset(end - ViewportHeight);
            }
        }
        else
        {
            var start = rectangle.X;
            var end = rectangle.X + rectangle.Width;
            if (start < HorizontalOffset)
            {
                SetHorizontalOffset(start);
            }
            else if (end > HorizontalOffset + ViewportWidth)
            {
                SetHorizontalOffset(end - ViewportWidth);
            }
        }

        return rectangle;
    }

    private void ArrangeRange(Vector2 finalSize, int firstIndex, int lastIndex)
    {
        EnsureStartOffsets();

        var first = Math.Max(0, firstIndex);
        var last = Math.Min(Children.Count - 1, lastIndex);

        for (var i = first; i <= last; i++)
        {
            if (Children[i] is not FrameworkElement child)
            {
                continue;
            }

            var primary = ResolvePrimarySizeForArrange(child, i);
            var start = _startOffsets[i];

            if (Orientation == Orientation.Vertical)
            {
                child.Arrange(new LayoutRect(
                    LayoutSlot.X,
                    LayoutSlot.Y + start,
                    finalSize.X,
                    primary));
            }
            else
            {
                child.Arrange(new LayoutRect(
                    LayoutSlot.X + start,
                    LayoutSlot.Y,
                    primary,
                    finalSize.Y));
            }
        }
    }

    private static bool IsFinitePositive(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }

    private bool IsVirtualizationActiveContext(ViewportContext context)
    {
        return IsVirtualizing && IsFinitePositive(context.ViewportPrimary);
    }

    private float ResolvePrimarySizeForArrange(FrameworkElement child, int index)
    {
        var desired = Orientation == Orientation.Vertical ? child.DesiredSize.Y : child.DesiredSize.X;
        if (desired > 0f && !float.IsNaN(desired) && !float.IsInfinity(desired))
        {
            SetSizeCache(index, desired, Orientation == Orientation.Vertical ? child.DesiredSize.X : child.DesiredSize.Y);
            return desired;
        }

        return _primarySizes[index];
    }

    private Vector2 MeasureAllChildren(Vector2 availableSize)
    {
        var desiredPrimary = 0f;
        var desiredSecondary = 0f;
        var measuredPrimaryTotal = 0f;
        var measuredPrimaryCount = 0;

        var childConstraint = GetChildConstraint(availableSize);
        for (var i = 0; i < Children.Count; i++)
        {
            if (Children[i] is not FrameworkElement child)
            {
                continue;
            }

            child.Measure(childConstraint);

            var childPrimary = Orientation == Orientation.Vertical ? child.DesiredSize.Y : child.DesiredSize.X;
            var childSecondary = Orientation == Orientation.Vertical ? child.DesiredSize.X : child.DesiredSize.Y;

            if (childPrimary <= 0f || float.IsNaN(childPrimary) || float.IsInfinity(childPrimary))
            {
                childPrimary = MathF.Max(1f, _averagePrimarySize);
            }

            SetSizeCache(i, childPrimary, childSecondary);

            desiredPrimary += childPrimary;
            desiredSecondary = MathF.Max(desiredSecondary, childSecondary);
            measuredPrimaryTotal += childPrimary;
            measuredPrimaryCount++;
        }

        if (measuredPrimaryCount > 0)
        {
            _averagePrimarySize = MathF.Max(1f, measuredPrimaryTotal / measuredPrimaryCount);
        }

        _maxSecondarySize = desiredSecondary;
        SetRealization(0, Children.Count - 1);

        return Orientation == Orientation.Vertical
            ? new Vector2(desiredSecondary, desiredPrimary)
            : new Vector2(desiredPrimary, desiredSecondary);
    }

    private void MeasureRange(Vector2 availableSize, int first, int last)
    {
        var childConstraint = GetChildConstraint(availableSize);

        var measuredPrimaryTotal = 0f;
        var measuredPrimaryCount = 0;
        var measuredSecondaryMax = _maxSecondarySize;

        for (var i = first; i <= last; i++)
        {
            if (i < 0 || i >= Children.Count)
            {
                continue;
            }

            if (Children[i] is not FrameworkElement child)
            {
                continue;
            }

            child.Measure(childConstraint);

            var childPrimary = Orientation == Orientation.Vertical ? child.DesiredSize.Y : child.DesiredSize.X;
            var childSecondary = Orientation == Orientation.Vertical ? child.DesiredSize.X : child.DesiredSize.Y;

            if (childPrimary <= 0f || float.IsNaN(childPrimary) || float.IsInfinity(childPrimary))
            {
                childPrimary = MathF.Max(1f, _averagePrimarySize);
            }

            SetSizeCache(i, childPrimary, childSecondary);

            measuredPrimaryTotal += childPrimary;
            measuredPrimaryCount++;
            measuredSecondaryMax = MathF.Max(measuredSecondaryMax, childSecondary);
        }

        if (measuredPrimaryCount > 0)
        {
            _averagePrimarySize = MathF.Max(1f, measuredPrimaryTotal / measuredPrimaryCount);
        }

        _maxSecondarySize = measuredSecondaryMax;
    }

    private Vector2 GetChildConstraint(Vector2 availableSize)
    {
        if (Orientation == Orientation.Vertical)
        {
            return new Vector2(availableSize.X, float.PositiveInfinity);
        }

        return new Vector2(float.PositiveInfinity, availableSize.Y);
    }

    private float ResolveExtentSecondary(Vector2 availableSize)
    {
        if (IsFinitePositive(_maxSecondarySize))
        {
            return _maxSecondarySize;
        }

        return Orientation == Orientation.Vertical
            ? (IsFinitePositive(availableSize.X) ? availableSize.X : 0f)
            : (IsFinitePositive(availableSize.Y) ? availableSize.Y : 0f);
    }

    private void EnsureCacheLength()
    {
        var count = Children.Count;
        while (_primarySizes.Count < count)
        {
            _primarySizes.Add(_averagePrimarySize);
            _secondarySizes.Add(0f);
            _startOffsets.Add(0f);
            _startOffsetsDirty = true;
        }

        if (_primarySizes.Count == count)
        {
            return;
        }

        _primarySizes.RemoveRange(count, _primarySizes.Count - count);
        _secondarySizes.RemoveRange(count, _secondarySizes.Count - count);
        _startOffsets.RemoveRange(count, _startOffsets.Count - count);
        _startOffsetsDirty = true;
        RecomputeCachedEstimates();
    }

    private void RecomputeCachedEstimates()
    {
        if (_primarySizes.Count == 0)
        {
            _averagePrimarySize = 28f;
            _maxSecondarySize = 0f;
            return;
        }

        var totalPrimary = 0f;
        var countPrimary = 0;
        var maxSecondary = 0f;

        for (var i = 0; i < _primarySizes.Count; i++)
        {
            var primary = _primarySizes[i];
            if (primary > 0f && !float.IsNaN(primary) && !float.IsInfinity(primary))
            {
                totalPrimary += primary;
                countPrimary++;
            }

            var secondary = _secondarySizes[i];
            if (secondary > maxSecondary && !float.IsNaN(secondary) && !float.IsInfinity(secondary))
            {
                maxSecondary = secondary;
            }
        }

        if (countPrimary > 0)
        {
            _averagePrimarySize = MathF.Max(1f, totalPrimary / countPrimary);
        }

        _maxSecondarySize = maxSecondary;
    }

    private void SetSizeCache(int index, float primary, float secondary)
    {
        if (index < 0 || index >= _primarySizes.Count)
        {
            return;
        }

        if (!AreClose(_primarySizes[index], primary))
        {
            _primarySizes[index] = primary;
            _startOffsetsDirty = true;
        }

        _secondarySizes[index] = secondary;
    }

    private static bool AreClose(float left, float right)
    {
        return MathF.Abs(left - right) <= 0.01f;
    }

    private void SetRealization(int first, int last)
    {
        if (Children.Count == 0 || first < 0 || last < first)
        {
            FirstRealizedIndex = -1;
            LastRealizedIndex = -1;
            RealizedChildrenCount = 0;
            return;
        }

        FirstRealizedIndex = Math.Clamp(first, 0, Children.Count - 1);
        LastRealizedIndex = Math.Clamp(last, FirstRealizedIndex, Children.Count - 1);
        RealizedChildrenCount = LastRealizedIndex - FirstRealizedIndex + 1;
    }

    private int ResolveStartIndex(float startOffset)
    {
        EnsureStartOffsets();

        if (Children.Count == 0)
        {
            return -1;
        }

        var candidate = UpperBound(_startOffsets, startOffset) - 1;
        if (candidate < 0)
        {
            candidate = 0;
        }

        while (candidate < Children.Count - 1 && _startOffsets[candidate] + _primarySizes[candidate] < startOffset)
        {
            candidate++;
        }

        while (candidate > 0 && _startOffsets[candidate - 1] + _primarySizes[candidate - 1] >= startOffset)
        {
            candidate--;
        }

        return candidate;
    }

    private int ResolveEndIndex(float endOffset, int firstIndex)
    {
        EnsureStartOffsets();

        if (Children.Count == 0)
        {
            return -1;
        }

        var candidate = UpperBound(_startOffsets, endOffset) - 1;
        if (candidate < firstIndex)
        {
            candidate = firstIndex;
        }

        return Math.Clamp(candidate, firstIndex, Children.Count - 1);
    }

    private float GetTotalPrimarySize()
    {
        EnsureStartOffsets();
        if (_primarySizes.Count == 0)
        {
            return 0f;
        }

        var last = _primarySizes.Count - 1;
        return _startOffsets[last] + _primarySizes[last];
    }

    private void EnsureStartOffsets()
    {
        if (!_startOffsetsDirty && _startOffsets.Count == Children.Count)
        {
            return;
        }

        if (_startOffsets.Count < Children.Count)
        {
            while (_startOffsets.Count < Children.Count)
            {
                _startOffsets.Add(0f);
            }
        }
        else if (_startOffsets.Count > Children.Count)
        {
            _startOffsets.RemoveRange(Children.Count, _startOffsets.Count - Children.Count);
        }

        var offset = 0f;
        for (var i = 0; i < _primarySizes.Count; i++)
        {
            _startOffsets[i] = offset;
            offset += _primarySizes[i];
        }

        _startOffsetsDirty = false;
    }

    private static int UpperBound(IReadOnlyList<float> values, float value)
    {
        var low = 0;
        var high = values.Count;

        while (low < high)
        {
            var mid = low + ((high - low) / 2);
            if (values[mid] <= value)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }

    private ViewportContext ResolveViewportContext(Vector2 availableSize)
    {
        var fallbackViewer = ScrollOwner ?? FindAncestorScrollViewer();
        var viewportPrimary = Orientation == Orientation.Vertical
            ? ViewportHeight
            : ViewportWidth;

        var offsetPrimary = Orientation == Orientation.Vertical
            ? VerticalOffset
            : HorizontalOffset;

        if (!IsFinitePositive(viewportPrimary) && fallbackViewer != null)
        {
            viewportPrimary = Orientation == Orientation.Vertical
                ? fallbackViewer.ViewportHeight
                : fallbackViewer.ViewportWidth;
        }

        if (AreClose(offsetPrimary, 0f) && fallbackViewer != null)
        {
            offsetPrimary = Orientation == Orientation.Vertical
                ? fallbackViewer.VerticalOffset
                : fallbackViewer.HorizontalOffset;
        }

        if (!IsFinitePositive(viewportPrimary))
        {
            var fallback = Orientation == Orientation.Vertical ? availableSize.Y : availableSize.X;
            if (IsFinitePositive(fallback))
            {
                viewportPrimary = fallback;
            }
        }

        if (!IsFinitePositive(viewportPrimary))
        {
            viewportPrimary = MathF.Max(_averagePrimarySize * 12f, 320f);
        }

        var cacheLength = ResolveCacheLength(viewportPrimary);
        var startOffset = MathF.Max(0f, offsetPrimary - cacheLength);
        var endOffset = offsetPrimary + viewportPrimary + cacheLength;

        return new ViewportContext(
            fallbackViewer != null,
            viewportPrimary,
            offsetPrimary,
            startOffset,
            endOffset);
    }

    private float ResolveCacheLength(float viewportPrimary)
    {
        var cacheLength = MathF.Max(0f, CacheLength);
        return CacheLengthUnit switch
        {
            VirtualizationCacheLengthUnit.Pixel => cacheLength,
            VirtualizationCacheLengthUnit.Item => cacheLength * MathF.Max(1f, _averagePrimarySize),
            VirtualizationCacheLengthUnit.Page => cacheLength * MathF.Max(1f, viewportPrimary),
            _ => cacheLength
        };
    }

    private void UpdateScrollDataFromMeasure(Vector2 availableSize, Vector2 desiredSize)
    {
        _extentWidth = MathF.Max(0f, desiredSize.X);
        _extentHeight = MathF.Max(0f, desiredSize.Y);
        _viewportWidth = Orientation == Orientation.Vertical
            ? (IsFinitePositive(availableSize.X) ? availableSize.X : _maxSecondarySize)
            : (IsFinitePositive(availableSize.X) ? availableSize.X : MathF.Max(1f, _averagePrimarySize * 12f));
        _viewportHeight = Orientation == Orientation.Vertical
            ? (IsFinitePositive(availableSize.Y) ? availableSize.Y : MathF.Max(1f, _averagePrimarySize * 12f))
            : (IsFinitePositive(availableSize.Y) ? availableSize.Y : _maxSecondarySize);
        _viewportWidth = MathF.Max(0f, _viewportWidth);
        _viewportHeight = MathF.Max(0f, _viewportHeight);
        CoerceOffsets();
        NotifyScrollOwner();
    }

    private void UpdateViewportFromFinalSize(Vector2 finalSize)
    {
        _viewportWidth = MathF.Max(0f, finalSize.X);
        _viewportHeight = MathF.Max(0f, finalSize.Y);
        CoerceOffsets();
    }

    private float MaxHorizontalOffset()
    {
        return MathF.Max(0f, ExtentWidth - ViewportWidth);
    }

    private float MaxVerticalOffset()
    {
        return MathF.Max(0f, ExtentHeight - ViewportHeight);
    }

    private void CoerceOffsets()
    {
        _horizontalOffset = MathF.Max(0f, MathF.Min(MaxHorizontalOffset(), _horizontalOffset));
        _verticalOffset = MathF.Max(0f, MathF.Min(MaxVerticalOffset(), _verticalOffset));
    }

    private void NotifyScrollOwner()
    {
        ScrollOwner?.InvalidateScrollInfo();
    }

    private ScrollViewer? FindAncestorScrollViewer()
    {
        for (var current = VisualParent ?? LogicalParent; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is ScrollViewer scrollViewer)
            {
                return scrollViewer;
            }
        }

        return null;
    }

    private readonly record struct ViewportContext(
        bool IsInScrollViewer,
        float ViewportPrimary,
        float OffsetPrimary,
        float StartOffset,
        float EndOffset);
}
