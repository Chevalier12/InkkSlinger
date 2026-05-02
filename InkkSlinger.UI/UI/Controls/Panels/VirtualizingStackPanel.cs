using System;
using System.Collections.Generic;
using System.Diagnostics;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class VirtualizingStackPanel : Panel
{
    private enum ViewerOwnedOffsetChangeHandling
    {
        VisualOnly,
        ArrangeOnly,
        InvalidateMeasure
    }

    private static long _diagMeasureOverrideCallCount;
    private static long _diagMeasureOverrideElapsedTicks;
    private static long _diagMeasureOverrideReusedRangeCount;
    private static long _diagMeasureAllChildrenCallCount;
    private static long _diagMeasureAllChildrenElapsedTicks;
    private static long _diagMeasureRangeCallCount;
    private static long _diagMeasureRangeElapsedTicks;
    private static long _diagArrangeOverrideCallCount;
    private static long _diagArrangeOverrideElapsedTicks;
    private static long _diagArrangeOverrideReusedRangeCount;
    private static long _diagArrangeRangeCallCount;
    private static long _diagArrangeRangeElapsedTicks;
    private static long _diagCanReuseMeasureForAvailableSizeChangeCallCount;
    private static long _diagCanReuseMeasureForAvailableSizeChangeTrueCount;
    private static long _diagResolveViewportContextCallCount;
    private static long _diagViewerOwnedOffsetDecisionCallCount;
    private static long _diagViewerOwnedOffsetDecisionRequireMeasureCount;
    private static long _diagViewerOwnedOffsetDecisionOrientationMismatchCount;
    private static long _diagViewerOwnedOffsetDecisionInactiveOrNoChildrenOrNoDeltaCount;
    private static long _diagViewerOwnedOffsetDecisionNonFiniteViewportCount;
    private static long _diagViewerOwnedOffsetDecisionMissingRealizedRangeCount;
    private static long _diagViewerOwnedOffsetDecisionBeforeGuardBandCount;
    private static long _diagViewerOwnedOffsetDecisionAfterGuardBandCount;
    private static long _diagViewerOwnedOffsetDecisionWithinRealizedWindowCount;
    private static long _diagSetHorizontalOffsetCallCount;
    private static long _diagSetHorizontalOffsetNoOpCount;
    private static long _diagSetHorizontalOffsetRelayoutCount;
    private static long _diagSetHorizontalOffsetVisualOnlyCount;
    private static long _diagSetVerticalOffsetCallCount;
    private static long _diagSetVerticalOffsetNoOpCount;
    private static long _diagSetVerticalOffsetRelayoutCount;
    private static long _diagSetVerticalOffsetVisualOnlyCount;

    private uint _childOrderVersion;
    private uint _lastMeasuredChildOrderVersion;
    private uint _lastArrangedChildOrderVersion;
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
    private readonly List<bool> _hasMeasuredPrimarySizes = new();
    private readonly List<float> _startOffsets = new();

    private float _averagePrimarySize = 28f;
    private float _maxSecondarySize;
    private float _extentWidth;
    private float _extentHeight;
    private float _viewportWidth;
    private float _viewportHeight;
    private float _horizontalOffset;
    private float _verticalOffset;

    private bool _startOffsetsDirty = true;
    private int _startOffsetsDirtyIndex;
    private bool _isVirtualizationActive;
    private bool _relayoutQueuedFromOffset;
    private int _runtimeMeasureOverrideCallCount;
    private long _runtimeMeasureOverrideElapsedTicks;
    private int _runtimeMeasureOverrideReusedRangeCount;
    private int _runtimeMeasureAllChildrenCallCount;
    private long _runtimeMeasureAllChildrenElapsedTicks;
    private int _runtimeMeasureRangeCallCount;
    private long _runtimeMeasureRangeElapsedTicks;
    private int _runtimeArrangeOverrideCallCount;
    private long _runtimeArrangeOverrideElapsedTicks;
    private int _runtimeArrangeOverrideReusedRangeCount;
    private int _runtimeArrangeRangeCallCount;
    private long _runtimeArrangeRangeElapsedTicks;
    private int _runtimeCanReuseMeasureForAvailableSizeChangeCallCount;
    private int _runtimeCanReuseMeasureForAvailableSizeChangeTrueCount;
    private int _runtimeResolveViewportContextCallCount;
    private int _runtimeViewerOwnedOffsetDecisionCallCount;
    private int _runtimeViewerOwnedOffsetDecisionRequireMeasureCount;
    private int _runtimeViewerOwnedOffsetDecisionOrientationMismatchCount;
    private int _runtimeViewerOwnedOffsetDecisionInactiveOrNoChildrenOrNoDeltaCount;
    private int _runtimeViewerOwnedOffsetDecisionNonFiniteViewportCount;
    private int _runtimeViewerOwnedOffsetDecisionMissingRealizedRangeCount;
    private int _runtimeViewerOwnedOffsetDecisionBeforeGuardBandCount;
    private int _runtimeViewerOwnedOffsetDecisionAfterGuardBandCount;
    private int _runtimeViewerOwnedOffsetDecisionWithinRealizedWindowCount;
    private int _runtimeSetHorizontalOffsetCallCount;
    private int _runtimeSetHorizontalOffsetNoOpCount;
    private int _runtimeSetHorizontalOffsetRelayoutCount;
    private int _runtimeSetHorizontalOffsetVisualOnlyCount;
    private int _runtimeSetVerticalOffsetCallCount;
    private int _runtimeSetVerticalOffsetNoOpCount;
    private int _runtimeSetVerticalOffsetRelayoutCount;
    private int _runtimeSetVerticalOffsetVisualOnlyCount;
    private string _runtimeLastOffsetDecisionReason = "none";
    private float _runtimeLastOffsetDecisionOldOffset;
    private float _runtimeLastOffsetDecisionNewOffset;
    private float _runtimeLastOffsetDecisionViewportPrimary;
    private float _runtimeLastOffsetDecisionRealizedStart;
    private float _runtimeLastOffsetDecisionRealizedEnd;
    private float _runtimeLastOffsetDecisionGuardBand;
    private float _runtimeLastViewportContextViewportPrimary;
    private float _runtimeLastViewportContextOffsetPrimary;
    private float _runtimeLastViewportContextStartOffset;
    private float _runtimeLastViewportContextEndOffset;
    private int _lastMeasuredFirst = -1;
    private int _lastMeasuredLast = -1;
    private Vector2 _lastMeasureConstraint;
    private bool _hasMeasuredConstraint;
    private int _lastArrangedFirst = -1;
    private int _lastArrangedLast = -1;
    private Vector2 _lastArrangeSize;
    private Vector2 _lastArrangeOrigin;
    private float _lastArrangeViewportOffset;
    private float _lastTryArrangeHorizontalOffset;
    private float _lastTryArrangeVerticalOffset;
    private bool _hasArrangedRange;
    private int _pendingUnrealizedClearFirst = -1;
    private int _pendingUnrealizedClearLast = -1;

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

    public float ExtentWidth => _extentWidth;

    public float ExtentHeight => _extentHeight;

    public float ViewportWidth => _viewportWidth;

    public float ViewportHeight => _viewportHeight;

    public float HorizontalOffset => _horizontalOffset;

    public float VerticalOffset => _verticalOffset;

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
        foreach (var child in GetChildrenOrderedByZIndex())
        {
            var childIndex = IndexOfChild(child);
            if (childIndex >= first && childIndex <= last)
            {
                yield return child;
            }
        }
    }

    public override void AddChild(UIElement child)
    {
        var countBefore = Children.Count;
        base.AddChild(child);
        if (Children.Count != countBefore)
        {
            OnChildOrderChanged();
        }
    }

    public override void InsertChild(int index, UIElement child)
    {
        var countBefore = Children.Count;
        base.InsertChild(index, child);
        if (Children.Count != countBefore)
        {
            OnChildOrderChanged();
        }
    }

    public override bool RemoveChild(UIElement child)
    {
        var removed = base.RemoveChild(child);
        if (removed)
        {
            OnChildOrderChanged();
        }

        return removed;
    }

    public override bool RemoveChildAt(int index)
    {
        var removed = base.RemoveChildAt(index);
        if (removed)
        {
            OnChildOrderChanged();
        }

        return removed;
    }

    public override bool MoveChildRange(int oldIndex, int count, int newIndex)
    {
        var moved = base.MoveChildRange(oldIndex, count, newIndex);
        if (moved)
        {
            OnChildOrderChanged();
        }

        return moved;
    }

    internal override int GetVisualChildCountForTraversal()
    {
        if (!_isVirtualizationActive || FirstRealizedIndex < 0 || LastRealizedIndex < FirstRealizedIndex)
        {
            return Children.Count;
        }

        var first = Math.Max(0, FirstRealizedIndex);
        var last = Math.Min(Children.Count - 1, LastRealizedIndex);
        return Math.Max(0, last - first + 1);
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        if (!_isVirtualizationActive || FirstRealizedIndex < 0 || LastRealizedIndex < FirstRealizedIndex)
        {
            if ((uint)index < (uint)Children.Count)
            {
                return Children[index];
            }

            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var first = Math.Max(0, FirstRealizedIndex);
        var last = Math.Min(Children.Count - 1, LastRealizedIndex);
        var count = Math.Max(0, last - first + 1);
        if ((uint)index >= (uint)count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var currentIndex = 0;
        foreach (var child in GetChildrenOrderedByZIndex())
        {
            var childIndex = IndexOfChild(child);
            if (childIndex < first || childIndex > last)
            {
                continue;
            }

            if (currentIndex == index)
            {
                return child;
            }

            currentIndex++;
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        foreach (var child in Children)
        {
            yield return child;
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagMeasureOverrideCallCount++;
        _runtimeMeasureOverrideCallCount++;

        _relayoutQueuedFromOffset = false;
        EnsureCacheLength();

        try
        {
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
            var childConstraint = GetChildConstraint(availableSize);
            var hasReusableMeasureConstraint = _hasMeasuredConstraint &&
                                               AreClose(_lastMeasureConstraint, childConstraint);
            var canReuseMeasuredRange = hasReusableMeasureConstraint &&
                                        first == _lastMeasuredFirst &&
                                        last == _lastMeasuredLast &&
                                        _lastMeasuredChildOrderVersion == _childOrderVersion &&
                                        !RangeNeedsMeasure(first, last);
            if (canReuseMeasuredRange)
            {
                _diagMeasureOverrideReusedRangeCount++;
                _runtimeMeasureOverrideReusedRangeCount++;
            }
            else if (TryMeasureShiftedRange(childConstraint, first, last))
            {
                _diagMeasureOverrideReusedRangeCount++;
                _runtimeMeasureOverrideReusedRangeCount++;
            }
            else
            {
                MeasureRange(childConstraint, first, last, forceMeasure: _hasMeasuredConstraint && !hasReusableMeasureConstraint);
            }

            _lastMeasuredFirst = first;
            _lastMeasuredLast = last;
            _lastMeasuredChildOrderVersion = _childOrderVersion;
            _lastMeasureConstraint = childConstraint;
            _hasMeasuredConstraint = true;

            SetRealization(first, last);

            var extentPrimary = GetTotalPrimarySize();
            var extentSecondary = ResolveExtentSecondary(availableSize);

            var desired = Orientation == Orientation.Vertical
                ? new Vector2(extentSecondary, extentPrimary)
                : new Vector2(extentPrimary, extentSecondary);
            UpdateScrollDataFromMeasure(availableSize, desired);
            return desired;
        }
        finally
        {
            var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _diagMeasureOverrideElapsedTicks += elapsedTicks;
            _runtimeMeasureOverrideElapsedTicks += elapsedTicks;
        }
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagArrangeOverrideCallCount++;
        _runtimeArrangeOverrideCallCount++;

        try
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
                return finalSize;
            }

            var context = ResolveViewportContext(finalSize);
            _isVirtualizationActive = IsVirtualizationActiveContext(context);

            if (_isVirtualizationActive)
            {
                var first = ResolveStartIndex(context.StartOffset);
                var last = ResolveEndIndex(context.EndOffset, first);
                SetRealization(first, last);
                var currentOrigin = new Vector2(LayoutSlot.X, LayoutSlot.Y);
                var canReuseArrangeRange = _hasArrangedRange &&
                                           first == _lastArrangedFirst &&
                                           last == _lastArrangedLast &&
                                           _lastArrangedChildOrderVersion == _childOrderVersion &&
                                           AreClose(_lastArrangeSize.X, finalSize.X) &&
                                           AreClose(_lastArrangeSize.Y, finalSize.Y) &&
                                           AreClose(_lastArrangeOrigin.X, currentOrigin.X) &&
                                           AreClose(_lastArrangeOrigin.Y, currentOrigin.Y) &&
                                           AreClose(_lastArrangeViewportOffset, context.OffsetPrimary) &&
                                           !RangeNeedsArrange(first, last);
                if (canReuseArrangeRange)
                {
                    _diagArrangeOverrideReusedRangeCount++;
                    _runtimeArrangeOverrideReusedRangeCount++;
                }
                else if (TryArrangeShiftedRange(finalSize, currentOrigin, context.OffsetPrimary, first, last))
                {
                    _diagArrangeOverrideReusedRangeCount++;
                    _runtimeArrangeOverrideReusedRangeCount++;
                }
                else
                {
                    ArrangeRange(finalSize, first, last, context.OffsetPrimary);
                }

                _hasArrangedRange = true;
                _lastArrangedFirst = first;
                _lastArrangedLast = last;
                _lastArrangeSize = finalSize;
                _lastArrangeOrigin = currentOrigin;
                _lastArrangeViewportOffset = context.OffsetPrimary;
                _lastArrangedChildOrderVersion = _childOrderVersion;
                ClearPendingUnrealizedLayoutSlots();
                SyncScrollDataFromCurrentCaches(finalSize);
            }
            else
            {
                SetRealization(0, Children.Count - 1);
                ArrangeRange(finalSize, 0, Children.Count - 1, viewportOffset: 0f);
                _hasArrangedRange = true;
                _lastArrangedFirst = 0;
                _lastArrangedLast = Children.Count - 1;
                _lastArrangeSize = finalSize;
                _lastArrangeOrigin = new Vector2(LayoutSlot.X, LayoutSlot.Y);
                _lastArrangeViewportOffset = 0f;
                _lastArrangedChildOrderVersion = _childOrderVersion;
                ClearPendingUnrealizedLayoutSlots();
                SyncScrollDataFromCurrentCaches(finalSize);
            }

            var ancestorViewer = FindAncestorScrollViewer();
            if (ancestorViewer != null)
            {
                _lastTryArrangeHorizontalOffset = ancestorViewer.HorizontalOffset;
                _lastTryArrangeVerticalOffset = ancestorViewer.VerticalOffset;
            }

            UpdateViewportFromFinalSize(finalSize);
            return finalSize;
        }
        finally
        {
            var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _diagArrangeOverrideElapsedTicks += elapsedTicks;
            _runtimeArrangeOverrideElapsedTicks += elapsedTicks;
        }
    }

    protected override bool CanReuseMeasureForAvailableSizeChange(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
    {
        _diagCanReuseMeasureForAvailableSizeChangeCallCount++;
        _runtimeCanReuseMeasureForAvailableSizeChangeCallCount++;

        if (GetType() != typeof(VirtualizingStackPanel))
        {
            return false;
        }

        EnsureCacheLength();

        var previousContext = ResolveViewportContext(previousAvailableSize);
        var nextContext = ResolveViewportContext(nextAvailableSize);
        var previousVirtualizationActive = IsVirtualizationActiveContext(previousContext);
        var nextVirtualizationActive = IsVirtualizationActiveContext(nextContext);
        if (previousVirtualizationActive != nextVirtualizationActive)
        {
            return false;
        }

        var previousChildAvailable = GetChildConstraint(previousAvailableSize);
        var nextChildAvailable = GetChildConstraint(nextAvailableSize);

        if (!previousVirtualizationActive)
        {
            foreach (var child in Children)
            {
                if (child is not FrameworkElement frameworkChild)
                {
                    continue;
                }

                if (!frameworkChild.CanReuseMeasureForAvailableSizeChangeForParentLayout(previousChildAvailable, nextChildAvailable))
                {
                    return false;
                }
            }

            _diagCanReuseMeasureForAvailableSizeChangeTrueCount++;
            _runtimeCanReuseMeasureForAvailableSizeChangeTrueCount++;
            return true;
        }

        var previousFirst = ResolveStartIndex(previousContext.StartOffset);
        var previousLast = ResolveEndIndex(previousContext.EndOffset, previousFirst);
        var nextFirst = ResolveStartIndex(nextContext.StartOffset);
        var nextLast = ResolveEndIndex(nextContext.EndOffset, nextFirst);
        if (previousFirst != nextFirst || previousLast != nextLast)
        {
            return false;
        }

        if (_lastMeasuredFirst != previousFirst ||
            _lastMeasuredLast != previousLast ||
            _lastMeasuredChildOrderVersion != _childOrderVersion ||
            RangeNeedsMeasure(previousFirst, previousLast))
        {
            return false;
        }

        var first = Math.Max(0, previousFirst);
        var last = Math.Min(Children.Count - 1, previousLast);
        for (var i = first; i <= last; i++)
        {
            if (Children[i] is not FrameworkElement frameworkChild)
            {
                continue;
            }

            if (!frameworkChild.CanReuseMeasureForAvailableSizeChangeForParentLayout(previousChildAvailable, nextChildAvailable))
            {
                return false;
            }
        }

        _diagCanReuseMeasureForAvailableSizeChangeTrueCount++;
        _runtimeCanReuseMeasureForAvailableSizeChangeTrueCount++;
        return true;
    }

    protected override void OnVisualParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        base.OnVisualParentChanged(oldParent, newParent);
        InvalidateMeasure();
    }

    protected override void OnLogicalParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        base.OnLogicalParentChanged(oldParent, newParent);
        InvalidateMeasure();
    }

    protected override bool TryGetLocalRenderTransform(out Matrix transform, out Matrix inverseTransform)
    {
        var hasBaseTransform = base.TryGetLocalRenderTransform(out var baseTransform, out var baseInverseTransform);
        if (FindAncestorScrollViewer() is { } ownerViewer)
        {
            var ownerOffsetX = Orientation == Orientation.Horizontal
                ? _lastArrangeViewportOffset - ownerViewer.HorizontalOffset
                : 0f;
            var ownerOffsetY = Orientation == Orientation.Vertical
                ? _lastArrangeViewportOffset - ownerViewer.VerticalOffset
                : 0f;
            if (!_isVirtualizationActive ||
                !_hasArrangedRange ||
                (AreClose(ownerOffsetX, 0f) && AreClose(ownerOffsetY, 0f)))
            {
                transform = baseTransform;
                inverseTransform = baseInverseTransform;
                return hasBaseTransform;
            }

            var ownerScrollTransform = Matrix.CreateTranslation(ownerOffsetX, ownerOffsetY, 0f);
            var ownerScrollInverseTransform = Matrix.CreateTranslation(-ownerOffsetX, -ownerOffsetY, 0f);
            return TryComposeLocalTransforms(
                hasPrimaryTransform: ownerScrollTransform != Matrix.Identity,
                primaryTransform: ownerScrollTransform,
                primaryInverse: ownerScrollInverseTransform,
                hasSecondaryTransform: hasBaseTransform,
                secondaryTransform: baseTransform,
                secondaryInverse: baseInverseTransform,
                out transform,
                out inverseTransform);
        }

        var offsetX = -HorizontalOffset;
        var offsetY = -VerticalOffset;
        if (AreClose(offsetX, 0f) && AreClose(offsetY, 0f))
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
        _diagSetHorizontalOffsetCallCount++;
        _runtimeSetHorizontalOffsetCallCount++;

        var next = MathF.Max(0f, MathF.Min(MaxHorizontalOffset(), offset));
        if (AreClose(next, _horizontalOffset))
        {
            _diagSetHorizontalOffsetNoOpCount++;
            _runtimeSetHorizontalOffsetNoOpCount++;
            return;
        }

        var oldOffset = _horizontalOffset;
        _horizontalOffset = next;
        if (ShouldRelayoutForOffsetChange(oldOffset, next, isVertical: false))
        {
            _diagSetHorizontalOffsetRelayoutCount++;
            _runtimeSetHorizontalOffsetRelayoutCount++;
            if (!_relayoutQueuedFromOffset)
            {
                _relayoutQueuedFromOffset = true;
                InvalidateMeasure();
                InvalidateArrange();
            }
        }
        else
        {
            _diagSetHorizontalOffsetVisualOnlyCount++;
            _runtimeSetHorizontalOffsetVisualOnlyCount++;
            InvalidateVisual();
        }
    }

    internal bool RequiresMeasureForViewerOwnedOffsetChange(
        float oldHorizontalOffset,
        float newHorizontalOffset,
        float oldVerticalOffset,
        float newVerticalOffset)
    {
        return HandleViewerOwnedOffsetChange(
                   oldHorizontalOffset,
                   newHorizontalOffset,
                   oldVerticalOffset,
                   newVerticalOffset) == ViewerOwnedOffsetChangeHandling.InvalidateMeasure;
    }

    internal bool TryHandleViewerOwnedOffsetChange(
        float oldHorizontalOffset,
        float newHorizontalOffset,
        float oldVerticalOffset,
        float newVerticalOffset,
        out bool requiresMeasure)
    {
        return TryHandleViewerOwnedOffsetChange(
            oldHorizontalOffset,
            newHorizontalOffset,
            oldVerticalOffset,
            newVerticalOffset,
            out requiresMeasure,
            out _);
    }

    internal bool TryHandleViewerOwnedOffsetChange(
        float oldHorizontalOffset,
        float newHorizontalOffset,
        float oldVerticalOffset,
        float newVerticalOffset,
        out bool requiresMeasure,
        out bool visualOnly)
    {
        var handling = HandleViewerOwnedOffsetChange(
            oldHorizontalOffset,
            newHorizontalOffset,
            oldVerticalOffset,
            newVerticalOffset);
        requiresMeasure = handling == ViewerOwnedOffsetChangeHandling.InvalidateMeasure;
        visualOnly = handling == ViewerOwnedOffsetChangeHandling.VisualOnly;
        return handling == ViewerOwnedOffsetChangeHandling.ArrangeOnly ||
               handling == ViewerOwnedOffsetChangeHandling.VisualOnly;
    }

    internal bool TryArrangeForViewerOwnedOffset(float horizontalOffset, float verticalOffset)
    {
        if (!_isVirtualizationActive ||
            Children.Count == 0 ||
            !_hasArrangedRange ||
            _lastArrangedChildOrderVersion != _childOrderVersion ||
            !_hasMeasuredConstraint)
        {
            return false;
        }

        var viewportPrimary = Orientation == Orientation.Vertical ? _viewportHeight : _viewportWidth;
        if (!IsFinitePositive(viewportPrimary))
        {
            return false;
        }

        var viewer = FindAncestorScrollViewer();
        if (viewer != null)
        {
            if (Orientation == Orientation.Vertical && !AreClose(horizontalOffset, _lastTryArrangeHorizontalOffset))
            {
                return false;
            }

            if (Orientation == Orientation.Horizontal && !AreClose(verticalOffset, _lastTryArrangeVerticalOffset))
            {
                return false;
            }
        }

        var offsetPrimary = Orientation == Orientation.Vertical ? verticalOffset : horizontalOffset;
        var context = CreateViewportContext(viewportPrimary, MathF.Max(0f, offsetPrimary));
        var first = ResolveStartIndex(context.StartOffset);
        var last = ResolveEndIndex(context.EndOffset, first);
        if (first < 0 || last < first)
        {
            return false;
        }

        if (FirstRealizedIndex != first || LastRealizedIndex != last)
        {
            return false;
        }

        var currentOrigin = new Vector2(LayoutSlot.X, LayoutSlot.Y);
        if (!AreClose(_lastArrangeOrigin, currentOrigin))
        {
            return false;
        }

        if (RangeNeedsArrange(first, last))
        {
            ArrangeRange(_lastArrangeSize, first, last, context.OffsetPrimary);
        }
        else if (!TryArrangeShiftedRange(_lastArrangeSize, currentOrigin, context.OffsetPrimary, first, last))
        {
            ArrangeRange(_lastArrangeSize, first, last, context.OffsetPrimary);
        }

        _lastArrangedFirst = first;
        _lastArrangedLast = last;
        _lastArrangeOrigin = currentOrigin;
        _lastArrangeViewportOffset = context.OffsetPrimary;
        _lastArrangedChildOrderVersion = _childOrderVersion;
        ClearPendingUnrealizedLayoutSlots();
        UiRoot.Current?.NotifyDirectRenderInvalidation(this);
        return true;
    }

    private ViewerOwnedOffsetChangeHandling HandleViewerOwnedOffsetChange(
        float oldHorizontalOffset,
        float newHorizontalOffset,
        float oldVerticalOffset,
        float newVerticalOffset)
    {
        _diagViewerOwnedOffsetDecisionCallCount++;
        _runtimeViewerOwnedOffsetDecisionCallCount++;

        var handling = Orientation == Orientation.Vertical
            ? ResolveViewerOwnedOffsetChangeHandling(oldVerticalOffset, newVerticalOffset, isVertical: true)
            : ResolveViewerOwnedOffsetChangeHandling(oldHorizontalOffset, newHorizontalOffset, isVertical: false);

        if (handling == ViewerOwnedOffsetChangeHandling.InvalidateMeasure)
        {
            _diagViewerOwnedOffsetDecisionRequireMeasureCount++;
            _runtimeViewerOwnedOffsetDecisionRequireMeasureCount++;
        }

        return handling;
    }

    public void SetVerticalOffset(float offset)
    {
        _diagSetVerticalOffsetCallCount++;
        _runtimeSetVerticalOffsetCallCount++;

        var next = MathF.Max(0f, MathF.Min(MaxVerticalOffset(), offset));
        if (AreClose(next, _verticalOffset))
        {
            _diagSetVerticalOffsetNoOpCount++;
            _runtimeSetVerticalOffsetNoOpCount++;
            return;
        }

        var oldOffset = _verticalOffset;
        _verticalOffset = next;
        if (ShouldRelayoutForOffsetChange(oldOffset, next, isVertical: true))
        {
            _diagSetVerticalOffsetRelayoutCount++;
            _runtimeSetVerticalOffsetRelayoutCount++;
            if (!_relayoutQueuedFromOffset)
            {
                _relayoutQueuedFromOffset = true;
                InvalidateMeasure();
                InvalidateArrange();
            }
        }
        else
        {
            _diagSetVerticalOffsetVisualOnlyCount++;
            _runtimeSetVerticalOffsetVisualOnlyCount++;
            InvalidateVisual();
        }
    }

    public LayoutRect MakeVisible(UIElement visual, LayoutRect rectangle)
    {
        var ownerViewer = FindAncestorScrollViewer();
        var targetRectangle = ResolveMakeVisibleRectangle(visual, rectangle, ownerViewer);
        if (ownerViewer != null)
        {
            if (Orientation == Orientation.Vertical)
            {
                var start = targetRectangle.Y;
                var end = targetRectangle.Y + targetRectangle.Height;
                if (start < ownerViewer.VerticalOffset)
                {
                    ownerViewer.ScrollToVerticalOffset(start);
                }
                else if (end > ownerViewer.VerticalOffset + ownerViewer.ViewportHeight)
                {
                    ownerViewer.ScrollToVerticalOffset(end - ownerViewer.ViewportHeight);
                }
            }
            else
            {
                var start = targetRectangle.X;
                var end = targetRectangle.X + targetRectangle.Width;
                if (start < ownerViewer.HorizontalOffset)
                {
                    ownerViewer.ScrollToHorizontalOffset(start);
                }
                else if (end > ownerViewer.HorizontalOffset + ownerViewer.ViewportWidth)
                {
                    ownerViewer.ScrollToHorizontalOffset(end - ownerViewer.ViewportWidth);
                }
            }

            return targetRectangle;
        }

        if (Orientation == Orientation.Vertical)
        {
            var start = targetRectangle.Y;
            var end = targetRectangle.Y + targetRectangle.Height;
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
            var start = targetRectangle.X;
            var end = targetRectangle.X + targetRectangle.Width;
            if (start < HorizontalOffset)
            {
                SetHorizontalOffset(start);
            }
            else if (end > HorizontalOffset + ViewportWidth)
            {
                SetHorizontalOffset(end - ViewportWidth);
            }
        }

        return targetRectangle;
    }

    private LayoutRect ResolveMakeVisibleRectangle(UIElement visual, LayoutRect rectangle, ScrollViewer? ownerViewer)
    {
        var childIndex = IndexOfChild(visual);
        if (childIndex >= 0)
        {
            EnsureStartOffsets();
            if (childIndex < _startOffsets.Count)
            {
                return Orientation == Orientation.Vertical
                    ? new LayoutRect(
                        rectangle.X,
                        _startOffsets[childIndex] + rectangle.Y,
                        rectangle.Width,
                        rectangle.Height)
                    : new LayoutRect(
                        _startOffsets[childIndex] + rectangle.X,
                        rectangle.Y,
                        rectangle.Width,
                        rectangle.Height);
            }
        }

        if (visual is not FrameworkElement targetElement)
        {
            return rectangle;
        }

        var targetX = rectangle.X + (targetElement.LayoutSlot.X - LayoutSlot.X);
        var targetY = rectangle.Y + (targetElement.LayoutSlot.Y - LayoutSlot.Y);
        if (ownerViewer != null)
        {
            if (Orientation == Orientation.Vertical)
            {
                targetY += ownerViewer.VerticalOffset;
            }
            else
            {
                targetX += ownerViewer.HorizontalOffset;
            }
        }
        else if (Orientation == Orientation.Vertical)
        {
            targetY += VerticalOffset;
        }
        else
        {
            targetX += HorizontalOffset;
        }

        return new LayoutRect(
            targetX,
            targetY,
            rectangle.Width,
            rectangle.Height);
    }

    private int IndexOfChild(UIElement visual)
    {
        for (var i = 0; i < Children.Count; i++)
        {
            if (ReferenceEquals(Children[i], visual))
            {
                return i;
            }
        }

        return -1;
    }

    private void ArrangeRange(Vector2 finalSize, int firstIndex, int lastIndex, float viewportOffset)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagArrangeRangeCallCount++;
        _runtimeArrangeRangeCallCount++;

        EnsureStartOffsets();

        var first = Math.Max(0, firstIndex);
        var last = Math.Min(Children.Count - 1, lastIndex);

        try
        {
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
                        LayoutSlot.Y + start - viewportOffset,
                        finalSize.X,
                        primary));
                }
                else
                {
                    child.Arrange(new LayoutRect(
                        LayoutSlot.X + start - viewportOffset,
                        LayoutSlot.Y,
                        primary,
                        finalSize.Y));
                }
            }
        }
        finally
        {
            var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _diagArrangeRangeElapsedTicks += elapsedTicks;
            _runtimeArrangeRangeElapsedTicks += elapsedTicks;
        }
    }

    private static bool IsFinitePositive(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }

    private void OnChildOrderChanged()
    {
        _childOrderVersion++;
        _primarySizes.Clear();
        _secondarySizes.Clear();
        _hasMeasuredPrimarySizes.Clear();
        _startOffsets.Clear();
        _averagePrimarySize = 28f;
        _maxSecondarySize = 0f;
        _startOffsetsDirty = true;
        _startOffsetsDirtyIndex = 0;
        _lastMeasuredFirst = -1;
        _lastMeasuredLast = -1;
        _hasMeasuredConstraint = false;
        _lastArrangedFirst = -1;
        _lastArrangedLast = -1;
        _hasArrangedRange = false;
        _pendingUnrealizedClearFirst = -1;
        _pendingUnrealizedClearLast = -1;
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
        var startTicks = Stopwatch.GetTimestamp();
        _diagMeasureAllChildrenCallCount++;
        _runtimeMeasureAllChildrenCallCount++;

        try
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
                UpdateAveragePrimarySize(MathF.Max(1f, measuredPrimaryTotal / measuredPrimaryCount));
            }

            _maxSecondarySize = desiredSecondary;
            SetRealization(0, Children.Count - 1);

            return Orientation == Orientation.Vertical
                ? new Vector2(desiredSecondary, desiredPrimary)
                : new Vector2(desiredPrimary, desiredSecondary);
        }
        finally
        {
            var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _diagMeasureAllChildrenElapsedTicks += elapsedTicks;
            _runtimeMeasureAllChildrenElapsedTicks += elapsedTicks;
        }
    }

    private bool TryArrangeShiftedRange(Vector2 finalSize, Vector2 origin, float viewportOffset, int first, int last)
    {
        if (!_hasArrangedRange ||
            _lastArrangedChildOrderVersion != _childOrderVersion ||
            !AreClose(_lastArrangeSize, finalSize) ||
            !AreClose(_lastArrangeOrigin, origin))
        {
            return false;
        }

        var overlapFirst = Math.Max(first, _lastArrangedFirst);
        var overlapLast = Math.Min(last, _lastArrangedLast);
        if (overlapFirst > overlapLast)
        {
            return false;
        }

        var reusedAny = false;
        if (first < overlapFirst)
        {
            ArrangeRange(finalSize, first, overlapFirst - 1, viewportOffset);
            reusedAny = true;
        }

        if (TryArrangeOrTranslateRange(finalSize, overlapFirst, overlapLast, viewportOffset))
        {
            reusedAny = true;
        }
        else
        {
            ArrangeRange(finalSize, overlapFirst, overlapLast, viewportOffset);
        }

        if (overlapLast < last)
        {
            ArrangeRange(finalSize, overlapLast + 1, last, viewportOffset);
            reusedAny = true;
        }

        return reusedAny;
    }

    private bool TryArrangeOrTranslateRange(Vector2 finalSize, int firstIndex, int lastIndex, float viewportOffset)
    {
        EnsureStartOffsets();

        var first = Math.Max(0, firstIndex);
        var last = Math.Min(Children.Count - 1, lastIndex);
        var handledAny = false;
        for (var i = first; i <= last; i++)
        {
            if (Children[i] is not FrameworkElement child)
            {
                continue;
            }

            var primary = ResolvePrimarySizeForArrange(child, i);
            var start = _startOffsets[i];
            var nextRect = Orientation == Orientation.Vertical
                ? new LayoutRect(
                    LayoutSlot.X,
                    LayoutSlot.Y + start - viewportOffset,
                    finalSize.X,
                    primary)
                : new LayoutRect(
                    LayoutSlot.X + start - viewportOffset,
                    LayoutSlot.Y,
                    primary,
                    finalSize.Y);

            if (child.NeedsArrange || !child.TryTranslateArrangedSubtree(nextRect))
            {
                child.Arrange(nextRect);
            }

            handledAny = true;
        }

        return handledAny;
    }

    private void MeasureRange(Vector2 childConstraint, int first, int last, bool forceMeasure = false)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagMeasureRangeCallCount++;
        _runtimeMeasureRangeCallCount++;

        try
        {
            var measuredPrimaryTotal = 0f;
            var measuredPrimaryCount = 0;
            var measuredSecondaryMax = 0f;

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

                if (forceMeasure || child.NeedsMeasure || RequiresMeasureAfterVirtualizationRelease(child))
                {
                    child.Measure(childConstraint);
                }

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
                UpdateAveragePrimarySize(MathF.Max(1f, measuredPrimaryTotal / measuredPrimaryCount));
            }

            _maxSecondarySize = MathF.Max(measuredSecondaryMax, GetCachedSecondaryMax());
        }
        finally
        {
            var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _diagMeasureRangeElapsedTicks += elapsedTicks;
            _runtimeMeasureRangeElapsedTicks += elapsedTicks;
        }
    }

    private bool TryMeasureShiftedRange(Vector2 childConstraint, int first, int last)
    {
        if (!_hasMeasuredConstraint ||
            _lastMeasuredChildOrderVersion != _childOrderVersion ||
            !AreClose(_lastMeasureConstraint, childConstraint))
        {
            return false;
        }

        var overlapFirst = Math.Max(first, _lastMeasuredFirst);
        var overlapLast = Math.Min(last, _lastMeasuredLast);
        if (overlapFirst > overlapLast)
        {
            return false;
        }

        var reusedAny = false;
        if (first < overlapFirst)
        {
            MeasureRange(childConstraint, first, overlapFirst - 1);
            reusedAny = true;
        }

        if (RangeNeedsMeasure(overlapFirst, overlapLast))
        {
            MeasureRange(childConstraint, overlapFirst, overlapLast);
        }
        else
        {
            reusedAny = true;
        }

        if (overlapLast < last)
        {
            MeasureRange(childConstraint, overlapLast + 1, last);
            reusedAny = true;
        }

        return reusedAny;
    }


    private bool RangeNeedsMeasure(int first, int last)
    {
        var clampedFirst = Math.Max(0, first);
        var clampedLast = Math.Min(Children.Count - 1, last);
        for (var i = clampedFirst; i <= clampedLast; i++)
        {
            if (Children[i] is FrameworkElement child &&
                (child.NeedsMeasure || RequiresMeasureAfterVirtualizationRelease(child)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool RequiresMeasureAfterVirtualizationRelease(FrameworkElement child)
    {
        return child is Control { RequiresMeasureAfterVirtualizationRelease: true } ||
               child is ContentControl { RequiresContentMeasureAfterVirtualizationRelease: true };
    }

    private bool RangeNeedsArrange(int first, int last)
    {
        var clampedFirst = Math.Max(0, first);
        var clampedLast = Math.Min(Children.Count - 1, last);
        for (var i = clampedFirst; i <= clampedLast; i++)
        {
            if (Children[i] is FrameworkElement child && child.NeedsArrange)
            {
                return true;
            }
        }

        return false;
    }

    private bool ShouldRelayoutForOffsetChange(float oldOffset, float newOffset, bool isVertical, bool viewerOwnedDecision = false)
    {
        if ((Orientation == Orientation.Vertical) != isVertical)
        {
            RecordOffsetDecision("orientation-mismatch", viewerOwnedDecision, oldOffset, newOffset, 0f, 0f, 0f, 0f);
            return false;
        }

        if (!_isVirtualizationActive || Children.Count == 0 || AreClose(oldOffset, newOffset))
        {
            RecordOffsetDecision("inactive-or-no-children-or-no-delta", viewerOwnedDecision, oldOffset, newOffset, 0f, 0f, 0f, 0f);
            return false;
        }

        var viewportPrimary = isVertical ? _viewportHeight : _viewportWidth;
        if (!IsFinitePositive(viewportPrimary))
        {
            RecordOffsetDecision("non-finite-viewport", viewerOwnedDecision, oldOffset, newOffset, viewportPrimary, 0f, 0f, 0f);
            return true;
        }

        EnsureStartOffsets();
        if (FirstRealizedIndex < 0 || LastRealizedIndex < FirstRealizedIndex || _startOffsets.Count == 0)
        {
            RecordOffsetDecision("missing-realized-range", viewerOwnedDecision, oldOffset, newOffset, viewportPrimary, 0f, 0f, 0f);
            return true;
        }

        var first = Math.Clamp(FirstRealizedIndex, 0, _startOffsets.Count - 1);
        var last = Math.Clamp(LastRealizedIndex, first, _startOffsets.Count - 1);
        var realizedStart = _startOffsets[first];
        var realizedEnd = _startOffsets[last] + _primarySizes[last];
        var windowStart = newOffset;
        var windowEnd = newOffset + viewportPrimary;
        var guardBand = MathF.Max(MathF.Max(1f, _averagePrimarySize) * 4f, viewportPrimary * 0.15f);
        var scrollingForward = newOffset > oldOffset;
        if (!scrollingForward && windowStart < realizedStart + guardBand)
        {
            RecordOffsetDecision("window-before-guard-band", viewerOwnedDecision, oldOffset, newOffset, viewportPrimary, realizedStart, realizedEnd, guardBand);
            return true;
        }

        if (scrollingForward && windowEnd > realizedEnd - guardBand)
        {
            RecordOffsetDecision("window-after-guard-band", viewerOwnedDecision, oldOffset, newOffset, viewportPrimary, realizedStart, realizedEnd, guardBand);
            return true;
        }

        RecordOffsetDecision("within-realized-window", viewerOwnedDecision, oldOffset, newOffset, viewportPrimary, realizedStart, realizedEnd, guardBand);
        return false;
    }

    private ViewerOwnedOffsetChangeHandling ResolveViewerOwnedOffsetChangeHandling(float oldOffset, float newOffset, bool isVertical)
    {
        if ((Orientation == Orientation.Vertical) != isVertical)
        {
            RecordOffsetDecision("orientation-mismatch", viewerOwnedDecision: true, oldOffset, newOffset, 0f, 0f, 0f, 0f);
            return ViewerOwnedOffsetChangeHandling.ArrangeOnly;
        }

        if (!_isVirtualizationActive || Children.Count == 0 || AreClose(oldOffset, newOffset))
        {
            RecordOffsetDecision("inactive-or-no-children-or-no-delta", viewerOwnedDecision: true, oldOffset, newOffset, 0f, 0f, 0f, 0f);
            return ViewerOwnedOffsetChangeHandling.ArrangeOnly;
        }

        var viewportPrimary = isVertical ? _viewportHeight : _viewportWidth;
        if (!IsFinitePositive(viewportPrimary))
        {
            RecordOffsetDecision("non-finite-viewport", viewerOwnedDecision: true, oldOffset, newOffset, viewportPrimary, 0f, 0f, 0f);
            return ViewerOwnedOffsetChangeHandling.InvalidateMeasure;
        }

        EnsureStartOffsets();
        if (FirstRealizedIndex < 0 || LastRealizedIndex < FirstRealizedIndex || _startOffsets.Count == 0)
        {
            RecordOffsetDecision("missing-realized-range", viewerOwnedDecision: true, oldOffset, newOffset, viewportPrimary, 0f, 0f, 0f);
            return ViewerOwnedOffsetChangeHandling.InvalidateMeasure;
        }

        if (!ShouldRelayoutForOffsetChange(oldOffset, newOffset, isVertical, viewerOwnedDecision: true))
        {
            return ViewerOwnedOffsetChangeHandling.VisualOnly;
        }

        var nextContext = CreateViewportContext(viewportPrimary, newOffset);
        if (TryRefreshForViewerOwnedOffsetChange(nextContext))
        {
            var first = Math.Clamp(FirstRealizedIndex, 0, _startOffsets.Count - 1);
            var last = Math.Clamp(LastRealizedIndex, first, _startOffsets.Count - 1);
            var realizedStart = _startOffsets[first];
            var realizedEnd = _startOffsets[last] + _primarySizes[last];
            RecordOffsetDecision("viewer-owned-range-advance", viewerOwnedDecision: true, oldOffset, newOffset, viewportPrimary, realizedStart, realizedEnd, ResolveCacheLength(viewportPrimary));
            return ViewerOwnedOffsetChangeHandling.ArrangeOnly;
        }

        return ViewerOwnedOffsetChangeHandling.InvalidateMeasure;
    }

    internal bool TryRefreshForViewerOwnedOffsetCandidate(float horizontalOffset, float verticalOffset)
    {
        if (!_isVirtualizationActive || Children.Count == 0)
        {
            return false;
        }

        var viewportPrimary = Orientation == Orientation.Vertical ? _viewportHeight : _viewportWidth;
        if (!IsFinitePositive(viewportPrimary))
        {
            return false;
        }

        var offsetPrimary = Orientation == Orientation.Vertical ? verticalOffset : horizontalOffset;
        var context = CreateViewportContext(viewportPrimary, MathF.Max(0f, offsetPrimary));
        return TryRefreshForViewerOwnedOffsetChange(context);
    }

    internal bool TryAlignViewerOwnedVerticalWheelOffset(
        float proposedVerticalOffset,
        float viewportHeight,
        bool scrollingDown,
        out float alignedVerticalOffset)
    {
        alignedVerticalOffset = proposedVerticalOffset;
        if (Orientation != Orientation.Vertical ||
            Children.Count == 0 ||
            !IsFinitePositive(viewportHeight))
        {
            return false;
        }

        EnsureStartOffsets();
        if (_startOffsets.Count == 0 || _primarySizes.Count == 0)
        {
            return false;
        }

        var maxOffset = MathF.Max(0f, GetTotalPrimarySize() - viewportHeight);
        if (maxOffset <= 0f)
        {
            return false;
        }

        var clampedOffset = MathF.Max(0f, MathF.Min(maxOffset, proposedVerticalOffset));
        var edgeOffset = scrollingDown
            ? clampedOffset + viewportHeight - 0.001f
            : clampedOffset;
        var edgeIndex = ResolveStartIndex(edgeOffset);
        if (edgeIndex < 0 || edgeIndex >= _startOffsets.Count || edgeIndex >= _primarySizes.Count)
        {
            return false;
        }

        var itemStart = _startOffsets[edgeIndex];
        var itemEnd = itemStart + _primarySizes[edgeIndex];
        var alignedOffset = scrollingDown
            ? itemEnd - viewportHeight
            : itemStart;
        alignedOffset = MathF.Max(0f, MathF.Min(maxOffset, alignedOffset));
        if (AreClose(alignedOffset, clampedOffset))
        {
            return false;
        }

        alignedVerticalOffset = alignedOffset;
        return true;
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
            _hasMeasuredPrimarySizes.Add(false);
            _startOffsets.Add(0f);
            _startOffsetsDirty = true;
            _startOffsetsDirtyIndex = 0;
        }

        if (_primarySizes.Count == count)
        {
            return;
        }

        _primarySizes.RemoveRange(count, _primarySizes.Count - count);
        _secondarySizes.RemoveRange(count, _secondarySizes.Count - count);
        _hasMeasuredPrimarySizes.RemoveRange(count, _hasMeasuredPrimarySizes.Count - count);
        _startOffsets.RemoveRange(count, _startOffsets.Count - count);
        _startOffsetsDirty = true;
        _startOffsetsDirtyIndex = 0;
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
            UpdateAveragePrimarySize(MathF.Max(1f, totalPrimary / countPrimary));
        }

        _maxSecondarySize = maxSecondary;
    }

    private void UpdateAveragePrimarySize(float nextAveragePrimarySize)
    {
        var normalized = MathF.Max(1f, nextAveragePrimarySize);
        if (AreClose(_averagePrimarySize, normalized))
        {
            return;
        }

        _averagePrimarySize = normalized;
        RefreshEstimatedPrimarySizes();
    }

    private void RefreshEstimatedPrimarySizes()
    {
        var firstChangedIndex = -1;
        for (var i = 0; i < _primarySizes.Count; i++)
        {
            if (_hasMeasuredPrimarySizes[i] || AreClose(_primarySizes[i], _averagePrimarySize))
            {
                continue;
            }

            _primarySizes[i] = _averagePrimarySize;
            firstChangedIndex = firstChangedIndex < 0 ? i : Math.Min(firstChangedIndex, i);
        }

        if (firstChangedIndex < 0)
        {
            return;
        }

        _startOffsetsDirty = true;
        _startOffsetsDirtyIndex = _startOffsetsDirtyIndex < 0
            ? firstChangedIndex
            : Math.Min(_startOffsetsDirtyIndex, firstChangedIndex);
    }

    private float GetCachedSecondaryMax()
    {
        var maxSecondary = 0f;
        for (var i = 0; i < _secondarySizes.Count; i++)
        {
            var secondary = _secondarySizes[i];
            if (secondary > maxSecondary && !float.IsNaN(secondary) && !float.IsInfinity(secondary))
            {
                maxSecondary = secondary;
            }
        }

        return maxSecondary;
    }

    private void SetSizeCache(int index, float primary, float secondary)
    {
        if (index < 0 || index >= _primarySizes.Count)
        {
            return;
        }

        _hasMeasuredPrimarySizes[index] = true;
        if (!AreClose(_primarySizes[index], primary))
        {
            _primarySizes[index] = primary;
            _startOffsetsDirty = true;
            _startOffsetsDirtyIndex = _startOffsetsDirtyIndex < 0
                ? index
                : Math.Min(_startOffsetsDirtyIndex, index);
        }

        _secondarySizes[index] = secondary;
    }

    private static bool AreClose(float left, float right)
    {
        return MathF.Abs(left - right) <= 0.01f;
    }

    private static bool AreClose(Vector2 left, Vector2 right)
    {
        return AreClose(left.X, right.X) && AreClose(left.Y, right.Y);
    }

    private bool TryRefreshForViewerOwnedOffsetChange(ViewportContext context)
    {
        if (!TryGetViewerOwnedAvailableSize(context, out var availableSize))
        {
            return false;
        }

        var childConstraint = GetChildConstraint(availableSize);
        var first = ResolveStartIndex(context.StartOffset);
        var last = ResolveEndIndex(context.EndOffset, first);
        if (first < 0 || last < first)
        {
            return false;
        }

        if (_hasMeasuredConstraint &&
            _lastMeasuredChildOrderVersion == _childOrderVersion &&
            AreClose(_lastMeasureConstraint, childConstraint) &&
            TryMeasureShiftedRange(childConstraint, first, last))
        {
            // Shifted reuse already measured the newly entered slice.
        }
        else
        {
            MeasureRange(childConstraint, first, last);
        }

        _lastMeasuredFirst = first;
        _lastMeasuredLast = last;
        _lastMeasuredChildOrderVersion = _childOrderVersion;
        _lastMeasureConstraint = childConstraint;
        _hasMeasuredConstraint = true;
        SetRealization(first, last);

        var extentPrimary = GetTotalPrimarySize();
        var extentSecondary = ResolveExtentSecondary(availableSize);
        var desired = Orientation == Orientation.Vertical
            ? new Vector2(extentSecondary, extentPrimary)
            : new Vector2(extentPrimary, extentSecondary);
        UpdateScrollDataFromMeasure(availableSize, desired);
        return true;
    }

    private bool TryGetViewerOwnedAvailableSize(ViewportContext context, out Vector2 availableSize)
    {
        var ancestorViewer = FindAncestorScrollViewer();
        var viewportWidth = _viewportWidth;
        var viewportHeight = _viewportHeight;

        if (!IsFinitePositive(viewportWidth) && ancestorViewer != null && IsFinitePositive(ancestorViewer.ViewportWidth))
        {
            viewportWidth = ancestorViewer.ViewportWidth;
        }

        if (!IsFinitePositive(viewportHeight) && ancestorViewer != null && IsFinitePositive(ancestorViewer.ViewportHeight))
        {
            viewportHeight = ancestorViewer.ViewportHeight;
        }

        if (Orientation == Orientation.Vertical)
        {
            viewportHeight = context.ViewportPrimary;
        }
        else
        {
            viewportWidth = context.ViewportPrimary;
        }

        if (!IsFinitePositive(viewportWidth) || !IsFinitePositive(viewportHeight))
        {
            availableSize = default;
            return false;
        }

        availableSize = new Vector2(viewportWidth, viewportHeight);
        return true;
    }

    private void SetRealization(int first, int last)
    {
        var prevFirst = FirstRealizedIndex;
        var prevLast = LastRealizedIndex;
        if (Children.Count == 0 || first < 0 || last < first)
        {
            FirstRealizedIndex = -1;
            LastRealizedIndex = -1;
            RealizedChildrenCount = 0;
            if (prevFirst != FirstRealizedIndex || prevLast != LastRealizedIndex)
            {
                QueueUnrealizedLayoutSlotClear(prevFirst, prevLast, FirstRealizedIndex, LastRealizedIndex);
                NotifyRealizedVisualRangeChanged();
            }
            return;
        }

        FirstRealizedIndex = Math.Clamp(first, 0, Children.Count - 1);
        LastRealizedIndex = Math.Clamp(last, FirstRealizedIndex, Children.Count - 1);
        RealizedChildrenCount = LastRealizedIndex - FirstRealizedIndex + 1;
        if (prevFirst != FirstRealizedIndex || prevLast != LastRealizedIndex)
        {
            QueueUnrealizedLayoutSlotClear(prevFirst, prevLast, FirstRealizedIndex, LastRealizedIndex);
            NotifyRealizedVisualRangeChanged();
        }
    }

    private void QueueUnrealizedLayoutSlotClear(int prevFirst, int prevLast, int nextFirst, int nextLast)
    {
        if (prevFirst < 0 || prevLast < prevFirst)
        {
            return;
        }

        for (var i = prevFirst; i <= prevLast; i++)
        {
            if (i >= nextFirst && i <= nextLast)
            {
                continue;
            }

            if (_pendingUnrealizedClearFirst < 0)
            {
                _pendingUnrealizedClearFirst = i;
                _pendingUnrealizedClearLast = i;
            }
            else
            {
                _pendingUnrealizedClearFirst = Math.Min(_pendingUnrealizedClearFirst, i);
                _pendingUnrealizedClearLast = Math.Max(_pendingUnrealizedClearLast, i);
            }
        }
    }

    private void ClearPendingUnrealizedLayoutSlots()
    {
        if (_pendingUnrealizedClearFirst < 0 || _pendingUnrealizedClearLast < _pendingUnrealizedClearFirst)
        {
            return;
        }

        var first = Math.Max(0, _pendingUnrealizedClearFirst);
        var last = Math.Min(Children.Count - 1, _pendingUnrealizedClearLast);
        var emptySlot = new LayoutRect(LayoutSlot.X, LayoutSlot.Y, 0f, 0f);
        for (var i = first; i <= last; i++)
        {
            if (i >= FirstRealizedIndex && i <= LastRealizedIndex)
            {
                continue;
            }

            if (Children[i] is FrameworkElement child)
            {
                child.Arrange(emptySlot);
                if (child is ContentControl contentControl)
                {
                    contentControl.ReleaseDeferredContentElementForVirtualization();
                    contentControl.ReleaseTemplateForVirtualization();
                }
            }
        }

        _pendingUnrealizedClearFirst = -1;
        _pendingUnrealizedClearLast = -1;
    }

    private void NotifyRealizedVisualRangeChanged()
    {
        InvalidateVisual();
        UiRoot.Current?.NotifyDirectRenderInvalidation(this, requireDeepSync: true);
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

    private ViewportContext CreateViewportContext(float viewportPrimary, float offsetPrimary)
    {
        var cacheLength = ResolveCacheLength(viewportPrimary);
        var startOffset = MathF.Max(0f, offsetPrimary - cacheLength);
        var endOffset = offsetPrimary + viewportPrimary + cacheLength;
        return new ViewportContext(viewportPrimary, offsetPrimary, startOffset, endOffset);
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

        var startIndex = Math.Clamp(_startOffsetsDirtyIndex, 0, Math.Max(0, _primarySizes.Count - 1));
        var offset = startIndex > 0 ? _startOffsets[startIndex] : 0f;
        for (var i = startIndex; i < _primarySizes.Count; i++)
        {
            _startOffsets[i] = offset;
            offset += _primarySizes[i];
        }

        _startOffsetsDirty = false;
        _startOffsetsDirtyIndex = -1;
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
        _diagResolveViewportContextCallCount++;
        _runtimeResolveViewportContextCallCount++;

        var fallbackViewer = FindAncestorScrollViewer();
        var availablePrimary = Orientation == Orientation.Vertical
            ? availableSize.Y
            : availableSize.X;
        var viewportPrimary = IsFinitePositive(availablePrimary)
            ? availablePrimary
            : Orientation == Orientation.Vertical
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

        if (fallbackViewer != null)
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

        _runtimeLastViewportContextViewportPrimary = viewportPrimary;
        _runtimeLastViewportContextOffsetPrimary = offsetPrimary;
        _runtimeLastViewportContextStartOffset = startOffset;
        _runtimeLastViewportContextEndOffset = endOffset;

        return new ViewportContext(
            viewportPrimary,
            offsetPrimary,
            startOffset,
            endOffset);
    }

    internal VirtualizingStackPanelRuntimeDiagnosticsSnapshot GetVirtualizingStackPanelSnapshotForDiagnostics()
    {
        var realizedStart = 0f;
        var realizedEnd = 0f;
        _ = TryGetRealizedRange(out realizedStart, out realizedEnd);

        return new VirtualizingStackPanelRuntimeDiagnosticsSnapshot(
            Orientation,
            IsVirtualizing,
            VirtualizationMode,
            CacheLength,
            CacheLengthUnit,
            _isVirtualizationActive,
            Children.Count,
            FirstRealizedIndex,
            LastRealizedIndex,
            RealizedChildrenCount,
            realizedStart,
            realizedEnd,
            _extentWidth,
            _extentHeight,
            _viewportWidth,
            _viewportHeight,
            _horizontalOffset,
            _verticalOffset,
            _averagePrimarySize,
            _maxSecondarySize,
            _startOffsetsDirty,
            _relayoutQueuedFromOffset,
            _lastMeasuredFirst,
            _lastMeasuredLast,
            _lastArrangedFirst,
            _lastArrangedLast,
            _hasArrangedRange,
            _runtimeLastOffsetDecisionReason,
            _runtimeLastOffsetDecisionOldOffset,
            _runtimeLastOffsetDecisionNewOffset,
            _runtimeLastOffsetDecisionViewportPrimary,
            _runtimeLastOffsetDecisionRealizedStart,
            _runtimeLastOffsetDecisionRealizedEnd,
            _runtimeLastOffsetDecisionGuardBand,
            _runtimeLastViewportContextViewportPrimary,
            _runtimeLastViewportContextOffsetPrimary,
            _runtimeLastViewportContextStartOffset,
            _runtimeLastViewportContextEndOffset,
            _runtimeMeasureOverrideCallCount,
            TicksToMilliseconds(_runtimeMeasureOverrideElapsedTicks),
            _runtimeMeasureOverrideReusedRangeCount,
            _runtimeMeasureAllChildrenCallCount,
            TicksToMilliseconds(_runtimeMeasureAllChildrenElapsedTicks),
            _runtimeMeasureRangeCallCount,
            TicksToMilliseconds(_runtimeMeasureRangeElapsedTicks),
            _runtimeArrangeOverrideCallCount,
            TicksToMilliseconds(_runtimeArrangeOverrideElapsedTicks),
            _runtimeArrangeOverrideReusedRangeCount,
            _runtimeArrangeRangeCallCount,
            TicksToMilliseconds(_runtimeArrangeRangeElapsedTicks),
            _runtimeCanReuseMeasureForAvailableSizeChangeCallCount,
            _runtimeCanReuseMeasureForAvailableSizeChangeTrueCount,
            _runtimeResolveViewportContextCallCount,
            _runtimeViewerOwnedOffsetDecisionCallCount,
            _runtimeViewerOwnedOffsetDecisionRequireMeasureCount,
            _runtimeViewerOwnedOffsetDecisionOrientationMismatchCount,
            _runtimeViewerOwnedOffsetDecisionInactiveOrNoChildrenOrNoDeltaCount,
            _runtimeViewerOwnedOffsetDecisionNonFiniteViewportCount,
            _runtimeViewerOwnedOffsetDecisionMissingRealizedRangeCount,
            _runtimeViewerOwnedOffsetDecisionBeforeGuardBandCount,
            _runtimeViewerOwnedOffsetDecisionAfterGuardBandCount,
            _runtimeViewerOwnedOffsetDecisionWithinRealizedWindowCount,
            _runtimeSetHorizontalOffsetCallCount,
            _runtimeSetHorizontalOffsetNoOpCount,
            _runtimeSetHorizontalOffsetRelayoutCount,
            _runtimeSetHorizontalOffsetVisualOnlyCount,
            _runtimeSetVerticalOffsetCallCount,
            _runtimeSetVerticalOffsetNoOpCount,
            _runtimeSetVerticalOffsetRelayoutCount,
            _runtimeSetVerticalOffsetVisualOnlyCount);
    }

    internal new static VirtualizingStackPanelTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return CreateAggregateTelemetrySnapshot();
    }

    internal new static VirtualizingStackPanelTelemetrySnapshot GetTelemetryAndReset()
    {
        var snapshot = CreateAggregateTelemetrySnapshot();
        _diagMeasureOverrideCallCount = 0;
        _diagMeasureOverrideElapsedTicks = 0;
        _diagMeasureOverrideReusedRangeCount = 0;
        _diagMeasureAllChildrenCallCount = 0;
        _diagMeasureAllChildrenElapsedTicks = 0;
        _diagMeasureRangeCallCount = 0;
        _diagMeasureRangeElapsedTicks = 0;
        _diagArrangeOverrideCallCount = 0;
        _diagArrangeOverrideElapsedTicks = 0;
        _diagArrangeOverrideReusedRangeCount = 0;
        _diagArrangeRangeCallCount = 0;
        _diagArrangeRangeElapsedTicks = 0;
        _diagCanReuseMeasureForAvailableSizeChangeCallCount = 0;
        _diagCanReuseMeasureForAvailableSizeChangeTrueCount = 0;
        _diagResolveViewportContextCallCount = 0;
        _diagViewerOwnedOffsetDecisionCallCount = 0;
        _diagViewerOwnedOffsetDecisionRequireMeasureCount = 0;
        _diagViewerOwnedOffsetDecisionOrientationMismatchCount = 0;
        _diagViewerOwnedOffsetDecisionInactiveOrNoChildrenOrNoDeltaCount = 0;
        _diagViewerOwnedOffsetDecisionNonFiniteViewportCount = 0;
        _diagViewerOwnedOffsetDecisionMissingRealizedRangeCount = 0;
        _diagViewerOwnedOffsetDecisionBeforeGuardBandCount = 0;
        _diagViewerOwnedOffsetDecisionAfterGuardBandCount = 0;
        _diagViewerOwnedOffsetDecisionWithinRealizedWindowCount = 0;
        _diagSetHorizontalOffsetCallCount = 0;
        _diagSetHorizontalOffsetNoOpCount = 0;
        _diagSetHorizontalOffsetRelayoutCount = 0;
        _diagSetHorizontalOffsetVisualOnlyCount = 0;
        _diagSetVerticalOffsetCallCount = 0;
        _diagSetVerticalOffsetNoOpCount = 0;
        _diagSetVerticalOffsetRelayoutCount = 0;
        _diagSetVerticalOffsetVisualOnlyCount = 0;
        return snapshot;
    }

    private static VirtualizingStackPanelTelemetrySnapshot CreateAggregateTelemetrySnapshot()
    {
        return new VirtualizingStackPanelTelemetrySnapshot(
            _diagMeasureOverrideCallCount,
            TicksToMilliseconds(_diagMeasureOverrideElapsedTicks),
            _diagMeasureOverrideReusedRangeCount,
            _diagMeasureAllChildrenCallCount,
            TicksToMilliseconds(_diagMeasureAllChildrenElapsedTicks),
            _diagMeasureRangeCallCount,
            TicksToMilliseconds(_diagMeasureRangeElapsedTicks),
            _diagArrangeOverrideCallCount,
            TicksToMilliseconds(_diagArrangeOverrideElapsedTicks),
            _diagArrangeOverrideReusedRangeCount,
            _diagArrangeRangeCallCount,
            TicksToMilliseconds(_diagArrangeRangeElapsedTicks),
            _diagCanReuseMeasureForAvailableSizeChangeCallCount,
            _diagCanReuseMeasureForAvailableSizeChangeTrueCount,
            _diagResolveViewportContextCallCount,
            _diagViewerOwnedOffsetDecisionCallCount,
            _diagViewerOwnedOffsetDecisionRequireMeasureCount,
            _diagViewerOwnedOffsetDecisionOrientationMismatchCount,
            _diagViewerOwnedOffsetDecisionInactiveOrNoChildrenOrNoDeltaCount,
            _diagViewerOwnedOffsetDecisionNonFiniteViewportCount,
            _diagViewerOwnedOffsetDecisionMissingRealizedRangeCount,
            _diagViewerOwnedOffsetDecisionBeforeGuardBandCount,
            _diagViewerOwnedOffsetDecisionAfterGuardBandCount,
            _diagViewerOwnedOffsetDecisionWithinRealizedWindowCount,
            _diagSetHorizontalOffsetCallCount,
            _diagSetHorizontalOffsetNoOpCount,
            _diagSetHorizontalOffsetRelayoutCount,
            _diagSetHorizontalOffsetVisualOnlyCount,
            _diagSetVerticalOffsetCallCount,
            _diagSetVerticalOffsetNoOpCount,
            _diagSetVerticalOffsetRelayoutCount,
            _diagSetVerticalOffsetVisualOnlyCount);
    }

    private void RecordOffsetDecision(
        string reason,
        bool viewerOwnedDecision,
        float oldOffset,
        float newOffset,
        float viewportPrimary,
        float realizedStart,
        float realizedEnd,
        float guardBand)
    {
        _runtimeLastOffsetDecisionReason = reason;
        _runtimeLastOffsetDecisionOldOffset = oldOffset;
        _runtimeLastOffsetDecisionNewOffset = newOffset;
        _runtimeLastOffsetDecisionViewportPrimary = viewportPrimary;
        _runtimeLastOffsetDecisionRealizedStart = realizedStart;
        _runtimeLastOffsetDecisionRealizedEnd = realizedEnd;
        _runtimeLastOffsetDecisionGuardBand = guardBand;

        if (!viewerOwnedDecision)
        {
            return;
        }

        switch (reason)
        {
            case "orientation-mismatch":
                _diagViewerOwnedOffsetDecisionOrientationMismatchCount++;
                _runtimeViewerOwnedOffsetDecisionOrientationMismatchCount++;
                break;
            case "inactive-or-no-children-or-no-delta":
                _diagViewerOwnedOffsetDecisionInactiveOrNoChildrenOrNoDeltaCount++;
                _runtimeViewerOwnedOffsetDecisionInactiveOrNoChildrenOrNoDeltaCount++;
                break;
            case "non-finite-viewport":
                _diagViewerOwnedOffsetDecisionNonFiniteViewportCount++;
                _runtimeViewerOwnedOffsetDecisionNonFiniteViewportCount++;
                break;
            case "missing-realized-range":
                _diagViewerOwnedOffsetDecisionMissingRealizedRangeCount++;
                _runtimeViewerOwnedOffsetDecisionMissingRealizedRangeCount++;
                break;
            case "window-before-guard-band":
                _diagViewerOwnedOffsetDecisionBeforeGuardBandCount++;
                _runtimeViewerOwnedOffsetDecisionBeforeGuardBandCount++;
                break;
            case "window-after-guard-band":
                _diagViewerOwnedOffsetDecisionAfterGuardBandCount++;
                _runtimeViewerOwnedOffsetDecisionAfterGuardBandCount++;
                break;
            case "within-realized-window":
                _diagViewerOwnedOffsetDecisionWithinRealizedWindowCount++;
                _runtimeViewerOwnedOffsetDecisionWithinRealizedWindowCount++;
                break;
        }
    }

    private bool TryGetRealizedRange(out float realizedStart, out float realizedEnd)
    {
        realizedStart = 0f;
        realizedEnd = 0f;

        if (FirstRealizedIndex < 0 || LastRealizedIndex < FirstRealizedIndex || Children.Count == 0)
        {
            return false;
        }

        EnsureStartOffsets();
        if (_startOffsets.Count == 0)
        {
            return false;
        }

        var first = Math.Clamp(FirstRealizedIndex, 0, _startOffsets.Count - 1);
        var last = Math.Clamp(LastRealizedIndex, first, _startOffsets.Count - 1);
        realizedStart = _startOffsets[first];
        realizedEnd = _startOffsets[last] + _primarySizes[last];
        return true;
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
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
        var ancestorViewer = FindAncestorScrollViewer();
        var ownerViewportWidth = ancestorViewer?.ViewportWidth ?? 0f;
        var ownerViewportHeight = ancestorViewer?.ViewportHeight ?? 0f;
        _extentWidth = MathF.Max(0f, desiredSize.X);
        _extentHeight = MathF.Max(0f, desiredSize.Y);
        _viewportWidth = Orientation == Orientation.Vertical
            ? (IsFinitePositive(availableSize.X)
                ? availableSize.X
                : (IsFinitePositive(ownerViewportWidth) ? ownerViewportWidth : _maxSecondarySize))
            : (IsFinitePositive(availableSize.X)
                ? availableSize.X
                : (IsFinitePositive(ownerViewportWidth) ? ownerViewportWidth : MathF.Max(1f, _averagePrimarySize * 12f)));
        _viewportHeight = Orientation == Orientation.Vertical
            ? (IsFinitePositive(availableSize.Y)
                ? availableSize.Y
                : (IsFinitePositive(ownerViewportHeight) ? ownerViewportHeight : MathF.Max(1f, _averagePrimarySize * 12f)))
            : (IsFinitePositive(availableSize.Y)
                ? availableSize.Y
                : (IsFinitePositive(ownerViewportHeight) ? ownerViewportHeight : _maxSecondarySize));
        _viewportWidth = MathF.Max(0f, _viewportWidth);
        _viewportHeight = MathF.Max(0f, _viewportHeight);
        CoerceOffsets();
    }

    private void SyncScrollDataFromCurrentCaches(Vector2 viewportSize)
    {
        var extentPrimary = GetTotalPrimarySize();
        var extentSecondary = ResolveExtentSecondary(viewportSize);
        var desired = Orientation == Orientation.Vertical
            ? new Vector2(extentSecondary, extentPrimary)
            : new Vector2(extentPrimary, extentSecondary);
        UpdateScrollDataFromMeasure(viewportSize, desired);
    }

    private void UpdateViewportFromFinalSize(Vector2 finalSize)
    {
        var viewportWidth = MathF.Max(0f, finalSize.X);
        var viewportHeight = MathF.Max(0f, finalSize.Y);
        var ancestorViewer = FindAncestorScrollViewer();
        if (ancestorViewer != null)
        {
            if (IsFinitePositive(ancestorViewer.ViewportWidth))
            {
                viewportWidth = ancestorViewer.ViewportWidth;
            }

            if (IsFinitePositive(ancestorViewer.ViewportHeight))
            {
                viewportHeight = ancestorViewer.ViewportHeight;
            }
        }

        _viewportWidth = viewportWidth;
        _viewportHeight = viewportHeight;
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

    private ScrollViewer? FindAncestorScrollViewer()
    {
        if (VisualParent is ScrollViewer visualViewer)
        {
            return visualViewer;
        }

        if (LogicalParent is ScrollViewer logicalViewer)
        {
            return logicalViewer;
        }

        return null;
    }

    private readonly record struct ViewportContext(
        float ViewportPrimary,
        float OffsetPrimary,
        float StartOffset,
        float EndOffset);
}
