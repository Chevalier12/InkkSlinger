using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class ContentPresenter : FrameworkElement
{
    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(
            nameof(Content),
            typeof(object),
            typeof(ContentPresenter),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ContentTemplateProperty =
        DependencyProperty.Register(
            nameof(ContentTemplate),
            typeof(DataTemplate),
            typeof(ContentPresenter),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ContentTemplateSelectorProperty =
        DependencyProperty.Register(
            nameof(ContentTemplateSelector),
            typeof(DataTemplateSelector),
            typeof(ContentPresenter),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ContentSourceProperty =
        DependencyProperty.Register(
            nameof(ContentSource),
            typeof(string),
            typeof(ContentPresenter),
            new FrameworkPropertyMetadata(
                "Content",
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty HorizontalContentAlignmentProperty =
        DependencyProperty.Register(
            nameof(HorizontalContentAlignment),
            typeof(HorizontalAlignment),
            typeof(ContentPresenter),
            new FrameworkPropertyMetadata(HorizontalAlignment.Left, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty VerticalContentAlignmentProperty =
        DependencyProperty.Register(
            nameof(VerticalContentAlignment),
            typeof(VerticalAlignment),
            typeof(ContentPresenter),
            new FrameworkPropertyMetadata(VerticalAlignment.Top, FrameworkPropertyMetadataOptions.AffectsArrange));

    private static long _diagConstructorCallCount;
    private static long _diagGetVisualChildrenCallCount;
    private static long _diagGetVisualChildrenYieldedChildCount;
    private static long _diagGetVisualChildCountForTraversalCallCount;
    private static long _diagGetVisualChildAtForTraversalCallCount;
    private static long _diagGetVisualChildAtForTraversalOutOfRangeCount;
    private static long _diagGetLogicalChildrenCallCount;
    private static long _diagGetLogicalChildrenYieldedChildCount;
    private static long _diagNotifyOwnerContentChangedCallCount;
    private static long _diagNotifyOwnerContentChangedInvalidatedMeasureCount;
    private static long _diagDependencyPropertyChangedCallCount;
    private static long _diagDependencyPropertyChangedRelevantPropertyCount;
    private static long _diagVisualParentChangedCallCount;
    private static long _diagLogicalParentChangedCallCount;
    private static long _diagMeasureOverrideCallCount;
    private static long _diagMeasureOverrideElapsedTicks;
    private static long _diagMeasureOverridePresentedElementPathCount;
    private static long _diagMeasureOverrideNoPresentedElementCount;
    private static long _diagCanReuseMeasureCallCount;
    private static long _diagCanReuseMeasureNoPresentedElementCount;
    private static long _diagCanReuseMeasureDelegatedCount;
    private static long _diagArrangeOverrideCallCount;
    private static long _diagArrangeOverrideElapsedTicks;
    private static long _diagArrangeOverridePresentedElementPathCount;
    private static long _diagArrangeOverrideNoPresentedElementCount;
    private static long _diagRefreshSourceBindingCallCount;
    private static long _diagEnsureSourceBindingCallCount;
    private static long _diagEnsureSourceBindingElapsedTicks;
    private static long _diagEnsureSourceBindingOwnerUnchangedCount;
    private static long _diagEnsureSourceBindingDetachedOwnerCount;
    private static long _diagEnsureSourceBindingAttachedOwnerCount;
    private static long _diagEnsureSourceBindingAttachedContentControlCount;
    private static long _diagOnSourceOwnerPropertyChangedCallCount;
    private static long _diagOnSourceOwnerPropertyChangedElapsedTicks;
    private static long _diagOnSourceOwnerPropertyChangedIrrelevantPropertyCount;
    private static long _diagOnSourceOwnerPropertyChangedRebuiltPresentedElementCount;
    private static long _diagOnSourceOwnerPropertyChangedRefreshedFallbackTextCount;
    private static long _diagOnSourceOwnerPropertyChangedInvalidatedArrangeCount;
    private static long _diagRefreshPresentedElementCallCount;
    private static long _diagRefreshPresentedElementElapsedTicks;
    private static long _diagRefreshPresentedElementStateCacheHitCount;
    private static long _diagRefreshPresentedElementSelectedTemplateCount;
    private static long _diagRefreshPresentedElementBuiltSameInstanceCount;
    private static long _diagRefreshPresentedElementDetachedOldElementCount;
    private static long _diagRefreshPresentedElementAttachedNewElementCount;
    private static long _diagRefreshPresentedElementChangedCount;
    private static long _diagIsSourceOwnerPropertyRelevantCallCount;
    private static long _diagIsSourceOwnerPropertyRelevantContentMatchCount;
    private static long _diagIsSourceOwnerPropertyRelevantTemplateMatchCount;
    private static long _diagIsSourceOwnerPropertyRelevantTemplateSelectorMatchCount;
    private static long _diagIsSourceOwnerPropertyRelevantStyleMatchCount;
    private static long _diagIsSourceOwnerPropertyRelevantFalseCount;
    private static long _diagBuildContentElementCallCount;
    private static long _diagBuildContentElementElapsedTicks;
    private static long _diagBuildContentElementUiElementPathCount;
    private static long _diagBuildContentElementTemplatePathCount;
    private static long _diagBuildContentElementAccessTextPathCount;
    private static long _diagBuildContentElementLabelPathCount;
    private static long _diagBuildContentElementNullPathCount;
    private static long _diagBuildContentElementCycleGuardCount;
    private static long _diagResolveAccessKeyTargetCallCount;
    private static long _diagResolveAccessKeyTargetLabelPathCount;
    private static long _diagResolveAccessKeyTargetRecognizesAccessKeyPathCount;
    private static long _diagResolveAccessKeyTargetNoTargetCount;
    private static long _diagTryRefreshFallbackTextStylingCallCount;
    private static long _diagTryRefreshFallbackTextStylingNoContentCount;
    private static long _diagTryRefreshFallbackTextStylingUiElementPathCount;
    private static long _diagTryRefreshFallbackTextStylingTemplatePathCount;
    private static long _diagTryRefreshFallbackTextStylingLabelPathCount;
    private static long _diagTryRefreshFallbackTextStylingTextBlockPathCount;
    private static long _diagTryRefreshFallbackTextStylingNoMatchCount;
    private static long _diagWouldCreatePresentationCycleCallCount;
    private static long _diagWouldCreatePresentationCycleCurrentPresentedReuseCount;
    private static long _diagWouldCreatePresentationCycleSelfCount;
    private static long _diagWouldCreatePresentationCycleAncestorMatchCount;
    private static long _diagWouldCreatePresentationCycleDescendantMatchCount;
    private static long _diagWouldCreatePresentationCycleFalseCount;
    private static long _diagFindSourceOwnerCallCount;
    private static long _diagFindSourceOwnerElapsedTicks;
    private static long _diagFindSourceOwnerAncestorProbeCount;
    private static long _diagFindSourceOwnerPropertyMatchCount;
    private static long _diagFindSourceOwnerSelfOwnerSkipCount;
    private static long _diagFindSourceOwnerGetterFailureCount;
    private static long _diagFindSourceOwnerFoundCount;
    private static long _diagFindSourceOwnerNotFoundCount;
    private static long _diagFindReadablePropertyCallCount;
    private static long _diagFindReadablePropertyElapsedTicks;
    private static long _diagFindReadablePropertyMatchedCount;
    private static long _diagFindReadablePropertyNotFoundCount;

    private UIElement? _presentedElement;
    private DependencyObject? _sourceOwner;
    private object? _lastEffectiveContent;
    private DataTemplate? _lastEffectiveTemplate;
    private DataTemplateSelector? _lastEffectiveTemplateSelector;
    private bool _hasEffectivePresentationState;
    private long _runtimeGetVisualChildrenCallCount;
    private long _runtimeGetVisualChildrenYieldedChildCount;
    private long _runtimeGetVisualChildCountForTraversalCallCount;
    private long _runtimeGetVisualChildAtForTraversalCallCount;
    private long _runtimeGetVisualChildAtForTraversalOutOfRangeCount;
    private long _runtimeGetLogicalChildrenCallCount;
    private long _runtimeGetLogicalChildrenYieldedChildCount;
    private long _runtimeNotifyOwnerContentChangedCallCount;
    private long _runtimeNotifyOwnerContentChangedInvalidatedMeasureCount;
    private long _runtimeDependencyPropertyChangedCallCount;
    private long _runtimeDependencyPropertyChangedRelevantPropertyCount;
    private long _runtimeVisualParentChangedCallCount;
    private long _runtimeLogicalParentChangedCallCount;
    private long _runtimeMeasureOverrideCallCount;
    private long _runtimeMeasureOverrideElapsedTicks;
    private long _runtimeMeasureOverridePresentedElementPathCount;
    private long _runtimeMeasureOverrideNoPresentedElementCount;
    private long _runtimeCanReuseMeasureCallCount;
    private long _runtimeCanReuseMeasureNoPresentedElementCount;
    private long _runtimeCanReuseMeasureDelegatedCount;
    private long _runtimeArrangeOverrideCallCount;
    private long _runtimeArrangeOverrideElapsedTicks;
    private long _runtimeArrangeOverridePresentedElementPathCount;
    private long _runtimeArrangeOverrideNoPresentedElementCount;
    private long _runtimeRefreshSourceBindingCallCount;
    private long _runtimeEnsureSourceBindingCallCount;
    private long _runtimeEnsureSourceBindingElapsedTicks;
    private long _runtimeEnsureSourceBindingOwnerUnchangedCount;
    private long _runtimeEnsureSourceBindingDetachedOwnerCount;
    private long _runtimeEnsureSourceBindingAttachedOwnerCount;
    private long _runtimeEnsureSourceBindingAttachedContentControlCount;
    private long _runtimeOnSourceOwnerPropertyChangedCallCount;
    private long _runtimeOnSourceOwnerPropertyChangedElapsedTicks;
    private long _runtimeOnSourceOwnerPropertyChangedIrrelevantPropertyCount;
    private long _runtimeOnSourceOwnerPropertyChangedRebuiltPresentedElementCount;
    private long _runtimeOnSourceOwnerPropertyChangedRefreshedFallbackTextCount;
    private long _runtimeOnSourceOwnerPropertyChangedInvalidatedArrangeCount;
    private long _runtimeRefreshPresentedElementCallCount;
    private long _runtimeRefreshPresentedElementElapsedTicks;
    private long _runtimeRefreshPresentedElementStateCacheHitCount;
    private long _runtimeRefreshPresentedElementSelectedTemplateCount;
    private long _runtimeRefreshPresentedElementBuiltSameInstanceCount;
    private long _runtimeRefreshPresentedElementDetachedOldElementCount;
    private long _runtimeRefreshPresentedElementAttachedNewElementCount;
    private long _runtimeRefreshPresentedElementChangedCount;
    private long _runtimeBuildContentElementCallCount;
    private long _runtimeBuildContentElementElapsedTicks;
    private long _runtimeBuildContentElementUiElementPathCount;
    private long _runtimeBuildContentElementTemplatePathCount;
    private long _runtimeBuildContentElementAccessTextPathCount;
    private long _runtimeBuildContentElementLabelPathCount;
    private long _runtimeBuildContentElementNullPathCount;
    private long _runtimeBuildContentElementCycleGuardCount;
    private long _runtimeTryRefreshFallbackTextStylingCallCount;
    private long _runtimeTryRefreshFallbackTextStylingNoContentCount;
    private long _runtimeTryRefreshFallbackTextStylingUiElementPathCount;
    private long _runtimeTryRefreshFallbackTextStylingTemplatePathCount;
    private long _runtimeTryRefreshFallbackTextStylingLabelPathCount;
    private long _runtimeTryRefreshFallbackTextStylingTextBlockPathCount;
    private long _runtimeTryRefreshFallbackTextStylingNoMatchCount;
    private long _runtimeWouldCreatePresentationCycleCallCount;
    private long _runtimeWouldCreatePresentationCycleCurrentPresentedReuseCount;
    private long _runtimeWouldCreatePresentationCycleSelfCount;
    private long _runtimeWouldCreatePresentationCycleAncestorMatchCount;
    private long _runtimeWouldCreatePresentationCycleDescendantMatchCount;
    private long _runtimeWouldCreatePresentationCycleFalseCount;
    private long _runtimeFindSourceOwnerCallCount;
    private long _runtimeFindSourceOwnerElapsedTicks;
    private long _runtimeFindSourceOwnerAncestorProbeCount;
    private long _runtimeFindSourceOwnerPropertyMatchCount;
    private long _runtimeFindSourceOwnerSelfOwnerSkipCount;
    private long _runtimeFindSourceOwnerGetterFailureCount;
    private long _runtimeFindSourceOwnerFoundCount;
    private long _runtimeFindSourceOwnerNotFoundCount;

    public ContentPresenter()
    {
        IncrementAggregate(ref _diagConstructorCallCount);
    }

    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public DataTemplate? ContentTemplate
    {
        get => GetValue<DataTemplate>(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    public DataTemplateSelector? ContentTemplateSelector
    {
        get => GetValue<DataTemplateSelector>(ContentTemplateSelectorProperty);
        set => SetValue(ContentTemplateSelectorProperty, value);
    }

    public string ContentSource
    {
        get => GetValue<string>(ContentSourceProperty) ?? "Content";
        set => SetValue(ContentSourceProperty, value);
    }

    public HorizontalAlignment HorizontalContentAlignment
    {
        get => GetValue<HorizontalAlignment>(HorizontalContentAlignmentProperty);
        set => SetValue(HorizontalContentAlignmentProperty, value);
    }

    public VerticalAlignment VerticalContentAlignment
    {
        get => GetValue<VerticalAlignment>(VerticalContentAlignmentProperty);
        set => SetValue(VerticalContentAlignmentProperty, value);
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        IncrementMetric(ref _runtimeGetVisualChildrenCallCount, ref _diagGetVisualChildrenCallCount);
        if (_presentedElement != null)
        {
            IncrementMetric(ref _runtimeGetVisualChildrenYieldedChildCount, ref _diagGetVisualChildrenYieldedChildCount);
            yield return _presentedElement;
        }
    }

    internal override int GetVisualChildCountForTraversal()
    {
        IncrementMetric(ref _runtimeGetVisualChildCountForTraversalCallCount, ref _diagGetVisualChildCountForTraversalCallCount);
        return _presentedElement != null ? 1 : 0;
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        IncrementMetric(ref _runtimeGetVisualChildAtForTraversalCallCount, ref _diagGetVisualChildAtForTraversalCallCount);
        if (index == 0 && _presentedElement != null)
        {
            return _presentedElement;
        }

        IncrementMetric(ref _runtimeGetVisualChildAtForTraversalOutOfRangeCount, ref _diagGetVisualChildAtForTraversalOutOfRangeCount);

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        IncrementMetric(ref _runtimeGetLogicalChildrenCallCount, ref _diagGetLogicalChildrenCallCount);
        if (_presentedElement != null)
        {
            IncrementMetric(ref _runtimeGetLogicalChildrenYieldedChildCount, ref _diagGetLogicalChildrenYieldedChildCount);
            yield return _presentedElement;
        }
    }

    internal void NotifyOwnerContentChanged()
    {
        IncrementMetric(ref _runtimeNotifyOwnerContentChangedCallCount, ref _diagNotifyOwnerContentChangedCallCount);
        if (RefreshPresentedElement())
        {
            IncrementMetric(ref _runtimeNotifyOwnerContentChangedInvalidatedMeasureCount, ref _diagNotifyOwnerContentChangedInvalidatedMeasureCount);
            InvalidateMeasure();
        }
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        IncrementMetric(ref _runtimeDependencyPropertyChangedCallCount, ref _diagDependencyPropertyChangedCallCount);
        base.OnDependencyPropertyChanged(args);
        if (args.Property == ContentProperty ||
            args.Property == ContentTemplateProperty ||
            args.Property == ContentTemplateSelectorProperty ||
            args.Property == ContentSourceProperty)
        {
            IncrementMetric(ref _runtimeDependencyPropertyChangedRelevantPropertyCount, ref _diagDependencyPropertyChangedRelevantPropertyCount);
            RefreshSourceBinding();
            if (RefreshPresentedElement())
            {
                InvalidateMeasure();
            }
        }
    }

    protected override void OnVisualParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        IncrementMetric(ref _runtimeVisualParentChangedCallCount, ref _diagVisualParentChangedCallCount);
        base.OnVisualParentChanged(oldParent, newParent);
        RefreshSourceBinding();
        if (RefreshPresentedElement())
        {
            InvalidateMeasure();
        }
    }

    protected override void OnLogicalParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        IncrementMetric(ref _runtimeLogicalParentChangedCallCount, ref _diagLogicalParentChangedCallCount);
        base.OnLogicalParentChanged(oldParent, newParent);
        RefreshSourceBinding();
        if (RefreshPresentedElement())
        {
            InvalidateMeasure();
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeMeasureOverrideCallCount, ref _diagMeasureOverrideCallCount);
        EnsureSourceBinding();
        try
        {
            if (_presentedElement is FrameworkElement element)
            {
                IncrementMetric(ref _runtimeMeasureOverridePresentedElementPathCount, ref _diagMeasureOverridePresentedElementPathCount);
                element.Measure(availableSize);
                return element.DesiredSize;
            }

            IncrementMetric(ref _runtimeMeasureOverrideNoPresentedElementCount, ref _diagMeasureOverrideNoPresentedElementCount);
            return Vector2.Zero;
        }
        finally
        {
            AddMetric(ref _runtimeMeasureOverrideElapsedTicks, ref _diagMeasureOverrideElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    protected override bool CanReuseMeasureForAvailableSizeChange(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
    {
        IncrementMetric(ref _runtimeCanReuseMeasureCallCount, ref _diagCanReuseMeasureCallCount);
        EnsureSourceBinding();
        if (_presentedElement is not FrameworkElement element)
        {
            IncrementMetric(ref _runtimeCanReuseMeasureNoPresentedElementCount, ref _diagCanReuseMeasureNoPresentedElementCount);
            return true;
        }

        IncrementMetric(ref _runtimeCanReuseMeasureDelegatedCount, ref _diagCanReuseMeasureDelegatedCount);
        return element.CanReuseMeasureForAvailableSizeChangeForParentLayout(previousAvailableSize, nextAvailableSize);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeArrangeOverrideCallCount, ref _diagArrangeOverrideCallCount);
        EnsureSourceBinding();
        try
        {
            if (_presentedElement is FrameworkElement element)
            {
                IncrementMetric(ref _runtimeArrangeOverridePresentedElementPathCount, ref _diagArrangeOverridePresentedElementPathCount);
                var horizontalAlignment = ResolveEffectiveHorizontalContentAlignment();
                var verticalAlignment = ResolveEffectiveVerticalContentAlignment();
                var childWidth = ResolveAlignedSize(finalSize.X, element.DesiredSize.X, horizontalAlignment);
                var childHeight = ResolveAlignedSize(finalSize.Y, element.DesiredSize.Y, verticalAlignment);
                var childX = ResolveAlignedPosition(LayoutSlot.X, finalSize.X, childWidth, horizontalAlignment);
                var childY = ResolveAlignedPosition(LayoutSlot.Y, finalSize.Y, childHeight, verticalAlignment);
                element.Arrange(new LayoutRect(childX, childY, childWidth, childHeight));
            }
            else
            {
                IncrementMetric(ref _runtimeArrangeOverrideNoPresentedElementCount, ref _diagArrangeOverrideNoPresentedElementCount);
            }

            return finalSize;
        }
        finally
        {
            AddMetric(ref _runtimeArrangeOverrideElapsedTicks, ref _diagArrangeOverrideElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    private void RefreshSourceBinding()
    {
        IncrementMetric(ref _runtimeRefreshSourceBindingCallCount, ref _diagRefreshSourceBindingCallCount);
        EnsureSourceBinding();
    }

    private void EnsureSourceBinding()
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeEnsureSourceBindingCallCount, ref _diagEnsureSourceBindingCallCount);
        var foundOwner = FindSourceOwner();
        try
        {
            if (ReferenceEquals(foundOwner, _sourceOwner))
            {
                IncrementMetric(ref _runtimeEnsureSourceBindingOwnerUnchangedCount, ref _diagEnsureSourceBindingOwnerUnchangedCount);
                return;
            }

            if (_sourceOwner != null)
            {
                IncrementMetric(ref _runtimeEnsureSourceBindingDetachedOwnerCount, ref _diagEnsureSourceBindingDetachedOwnerCount);
                _sourceOwner.DependencyPropertyChanged -= OnSourceOwnerPropertyChanged;
                if (_sourceOwner is ContentControl oldContentControl)
                {
                    oldContentControl.DetachContentPresenter(this);
                }
            }

            _sourceOwner = foundOwner;
            if (_sourceOwner != null)
            {
                IncrementMetric(ref _runtimeEnsureSourceBindingAttachedOwnerCount, ref _diagEnsureSourceBindingAttachedOwnerCount);
                _sourceOwner.DependencyPropertyChanged += OnSourceOwnerPropertyChanged;
                if (_sourceOwner is ContentControl contentControl && string.Equals(ContentSource, "Content", StringComparison.Ordinal))
                {
                    IncrementMetric(ref _runtimeEnsureSourceBindingAttachedContentControlCount, ref _diagEnsureSourceBindingAttachedContentControlCount);
                    contentControl.AttachContentPresenter(this);
                }
            }

            RefreshPresentedElement();
        }
        finally
        {
            AddMetric(ref _runtimeEnsureSourceBindingElapsedTicks, ref _diagEnsureSourceBindingElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    private void OnSourceOwnerPropertyChanged(object? sender, DependencyPropertyChangedEventArgs args)
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeOnSourceOwnerPropertyChangedCallCount, ref _diagOnSourceOwnerPropertyChangedCallCount);
        try
        {
            if (!IsSourceOwnerPropertyRelevant(args.Property))
            {
                IncrementMetric(ref _runtimeOnSourceOwnerPropertyChangedIrrelevantPropertyCount, ref _diagOnSourceOwnerPropertyChangedIrrelevantPropertyCount);
                return;
            }

            if (TryRefreshCalendarDayButtonTextPresentation(args.Property))
            {
                IncrementMetric(ref _runtimeOnSourceOwnerPropertyChangedRefreshedFallbackTextCount, ref _diagOnSourceOwnerPropertyChangedRefreshedFallbackTextCount);
                return;
            }

            var rebuiltPresentedElement = RefreshPresentedElement();
            if (rebuiltPresentedElement)
            {
                IncrementMetric(ref _runtimeOnSourceOwnerPropertyChangedRebuiltPresentedElementCount, ref _diagOnSourceOwnerPropertyChangedRebuiltPresentedElementCount);
                InvalidateMeasure();
                return;
            }

            var refreshedFallbackText = TryRefreshFallbackTextStyling(args.Property);
            if (refreshedFallbackText)
            {
                IncrementMetric(ref _runtimeOnSourceOwnerPropertyChangedRefreshedFallbackTextCount, ref _diagOnSourceOwnerPropertyChangedRefreshedFallbackTextCount);
                if (!IsForegroundProperty(args.Property))
                {
                    InvalidateMeasure();
                    InvalidateVisual();
                }

                return;
            }

            if (IsContentAlignmentProperty(args.Property))
            {
                IncrementMetric(ref _runtimeOnSourceOwnerPropertyChangedInvalidatedArrangeCount, ref _diagOnSourceOwnerPropertyChangedInvalidatedArrangeCount);
                InvalidateArrange();
            }
        }
        finally
        {
            AddMetric(ref _runtimeOnSourceOwnerPropertyChangedElapsedTicks, ref _diagOnSourceOwnerPropertyChangedElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    private bool RefreshPresentedElement()
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeRefreshPresentedElementCallCount, ref _diagRefreshPresentedElementCallCount);
        var content = ResolveEffectiveContent();
        var template = ResolveEffectiveTemplate();
        var selector = ResolveEffectiveTemplateSelector();
        try
        {
            if (_hasEffectivePresentationState &&
                Equals(_lastEffectiveContent, content) &&
                ReferenceEquals(_lastEffectiveTemplate, template) &&
                ReferenceEquals(_lastEffectiveTemplateSelector, selector))
            {
                IncrementMetric(ref _runtimeRefreshPresentedElementStateCacheHitCount, ref _diagRefreshPresentedElementStateCacheHitCount);
                return false;
            }

            var selectedTemplate = DataTemplateResolver.ResolveTemplateForContent(
                this,
                content,
                template,
                selector,
                this);
            if (selectedTemplate != null)
            {
                IncrementMetric(ref _runtimeRefreshPresentedElementSelectedTemplateCount, ref _diagRefreshPresentedElementSelectedTemplateCount);
            }

            var built = BuildContentElement(content, selectedTemplate);
            if (ReferenceEquals(_presentedElement, built))
            {
                IncrementMetric(ref _runtimeRefreshPresentedElementBuiltSameInstanceCount, ref _diagRefreshPresentedElementBuiltSameInstanceCount);
                CacheEffectivePresentationState(content, template, selector);
                return false;
            }

            if (_presentedElement != null)
            {
                IncrementMetric(ref _runtimeRefreshPresentedElementDetachedOldElementCount, ref _diagRefreshPresentedElementDetachedOldElementCount);
                _presentedElement.SetVisualParent(null);
                _presentedElement.SetLogicalParent(null);
            }

            _presentedElement = built;
            if (_presentedElement != null)
            {
                IncrementMetric(ref _runtimeRefreshPresentedElementAttachedNewElementCount, ref _diagRefreshPresentedElementAttachedNewElementCount);
                _presentedElement.SetVisualParent(this);
                _presentedElement.SetLogicalParent(this);
            }

            CacheEffectivePresentationState(content, template, selector);
            IncrementMetric(ref _runtimeRefreshPresentedElementChangedCount, ref _diagRefreshPresentedElementChangedCount);
            return true;
        }
        finally
        {
            AddMetric(ref _runtimeRefreshPresentedElementElapsedTicks, ref _diagRefreshPresentedElementElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    private void CacheEffectivePresentationState(object? content, DataTemplate? template, DataTemplateSelector? selector)
    {
        _lastEffectiveContent = content;
        _lastEffectiveTemplate = template;
        _lastEffectiveTemplateSelector = selector;
        _hasEffectivePresentationState = true;
    }

    private bool IsSourceOwnerPropertyRelevant(DependencyProperty property)
    {
        IncrementAggregate(ref _diagIsSourceOwnerPropertyRelevantCallCount);
        var contentSource = GetEffectiveContentSourcePropertyName();

        if (!HasLocalValue(ContentProperty) &&
            string.Equals(property.Name, contentSource, StringComparison.Ordinal))
        {
            IncrementAggregate(ref _diagIsSourceOwnerPropertyRelevantContentMatchCount);
            return true;
        }

        if (!HasLocalValue(ContentTemplateProperty) &&
            string.Equals(property.Name, contentSource + "Template", StringComparison.Ordinal))
        {
            IncrementAggregate(ref _diagIsSourceOwnerPropertyRelevantTemplateMatchCount);
            return true;
        }

        if (!HasLocalValue(ContentTemplateSelectorProperty) &&
            string.Equals(property.Name, contentSource + "TemplateSelector", StringComparison.Ordinal))
        {
            IncrementAggregate(ref _diagIsSourceOwnerPropertyRelevantTemplateSelectorMatchCount);
            return true;
        }

        if (string.Equals(property.Name, nameof(FrameworkElement.FontFamily), StringComparison.Ordinal) ||
            string.Equals(property.Name, nameof(FrameworkElement.FontSize), StringComparison.Ordinal) ||
            string.Equals(property.Name, nameof(FrameworkElement.FontWeight), StringComparison.Ordinal) ||
            string.Equals(property.Name, nameof(FrameworkElement.FontStyle), StringComparison.Ordinal) ||
            string.Equals(property.Name, nameof(Control.Foreground), StringComparison.Ordinal) ||
            string.Equals(property.Name, nameof(Control.HorizontalContentAlignment), StringComparison.Ordinal) ||
            string.Equals(property.Name, nameof(Control.VerticalContentAlignment), StringComparison.Ordinal))
        {
            IncrementAggregate(ref _diagIsSourceOwnerPropertyRelevantStyleMatchCount);
            return true;
        }

        IncrementAggregate(ref _diagIsSourceOwnerPropertyRelevantFalseCount);
        return false;
    }

    private object? ResolveEffectiveContent()
    {
        if (HasLocalValue(ContentProperty))
        {
            return Content;
        }

        if (_sourceOwner == null)
        {
            return Content;
        }

            if (TryGetCalendarDayButtonContentOwner(out var dayButton))
            {
                return dayButton.DayText;
            }

        var property = FindReadableProperty(_sourceOwner.GetType(), GetEffectiveContentSourcePropertyName());
        return property?.GetValue(_sourceOwner);
    }

    private string GetEffectiveContentSourcePropertyName()
    {
        var contentSource = ContentSource;
        if (string.IsNullOrEmpty(contentSource))
        {
            contentSource = "Content";
        }

        if (!HasLocalValue(ContentSourceProperty) &&
            string.Equals(contentSource, "Content", StringComparison.Ordinal) &&
            _sourceOwner is CalendarDayButton)
        {
            return nameof(CalendarDayButton.DayText);
        }

        return contentSource;
    }

    private DataTemplate? ResolveEffectiveTemplate()
    {
        if (HasLocalValue(ContentTemplateProperty))
        {
            return ContentTemplate;
        }

        if (_sourceOwner == null)
        {
            return ContentTemplate;
        }

        var templatePropertyName = ContentSource + "Template";
        var property = FindReadableProperty(_sourceOwner.GetType(), templatePropertyName);
        if (property?.PropertyType == typeof(DataTemplate))
        {
            return property.GetValue(_sourceOwner) as DataTemplate;
        }

        return ContentTemplate;
    }

    private DataTemplateSelector? ResolveEffectiveTemplateSelector()
    {
        if (HasLocalValue(ContentTemplateSelectorProperty))
        {
            return ContentTemplateSelector;
        }

        if (_sourceOwner == null)
        {
            return ContentTemplateSelector;
        }

        var selectorPropertyName = ContentSource + "TemplateSelector";
        var property = FindReadableProperty(_sourceOwner.GetType(), selectorPropertyName);
        if (property?.PropertyType == typeof(DataTemplateSelector))
        {
            return property.GetValue(_sourceOwner) as DataTemplateSelector;
        }

        return ContentTemplateSelector;
    }

    private UIElement? BuildContentElement(object? content, DataTemplate? template)
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeBuildContentElementCallCount, ref _diagBuildContentElementCallCount);
        if (content is UIElement uiElement)
        {
            try
            {
                IncrementMetric(ref _runtimeBuildContentElementUiElementPathCount, ref _diagBuildContentElementUiElementPathCount);
                if (WouldCreatePresentationCycle(uiElement))
                {
                    IncrementMetric(ref _runtimeBuildContentElementCycleGuardCount, ref _diagBuildContentElementCycleGuardCount);
                    return BuildCycleGuardLabel(content);
                }

                return uiElement;
            }
            finally
            {
                AddMetric(ref _runtimeBuildContentElementElapsedTicks, ref _diagBuildContentElementElapsedTicks, Stopwatch.GetTimestamp() - start);
            }
        }

        if (template != null)
        {
            try
            {
                IncrementMetric(ref _runtimeBuildContentElementTemplatePathCount, ref _diagBuildContentElementTemplatePathCount);
                var built = template.Build(content, this);
                if (built != null && WouldCreatePresentationCycle(built))
                {
                    IncrementMetric(ref _runtimeBuildContentElementCycleGuardCount, ref _diagBuildContentElementCycleGuardCount);
                    return BuildCycleGuardLabel(content);
                }

                return built;
            }
            finally
            {
                AddMetric(ref _runtimeBuildContentElementElapsedTicks, ref _diagBuildContentElementElapsedTicks, Stopwatch.GetTimestamp() - start);
            }
        }

        if (ShouldUseCalendarDayButtonTextPresentation(template))
        {
            try
            {
                return BuildCalendarDayButtonTextElement(content);
            }
            finally
            {
                AddMetric(ref _runtimeBuildContentElementElapsedTicks, ref _diagBuildContentElementElapsedTicks, Stopwatch.GetTimestamp() - start);
            }
        }

        if (content != null)
        {
            try
            {
                if (RecognizesAccessKey)
                {
                    IncrementMetric(ref _runtimeBuildContentElementAccessTextPathCount, ref _diagBuildContentElementAccessTextPathCount);
                    var accessText = new AccessText
                    {
                        Text = content.ToString() ?? string.Empty
                    };

                    ApplyFallbackTextBlockStyling(accessText, changedProperty: null);
                    return accessText;
                }

                IncrementMetric(ref _runtimeBuildContentElementLabelPathCount, ref _diagBuildContentElementLabelPathCount);
                var label = new Label
                {
                    Content = content.ToString() ?? string.Empty
                };
                ApplyFallbackLabelStyling(label, changedProperty: null);
                return label;
            }
            finally
            {
                AddMetric(ref _runtimeBuildContentElementElapsedTicks, ref _diagBuildContentElementElapsedTicks, Stopwatch.GetTimestamp() - start);
            }
        }

        IncrementMetric(ref _runtimeBuildContentElementNullPathCount, ref _diagBuildContentElementNullPathCount);
        AddMetric(ref _runtimeBuildContentElementElapsedTicks, ref _diagBuildContentElementElapsedTicks, Stopwatch.GetTimestamp() - start);
        return null;
    }

    internal UIElement? ResolveAccessKeyTarget()
    {
        IncrementAggregate(ref _diagResolveAccessKeyTargetCallCount);
        if (_sourceOwner is Label label)
        {
            IncrementAggregate(ref _diagResolveAccessKeyTargetLabelPathCount);
            return label.ResolveAccessKeyTarget();
        }

        if (_sourceOwner is FrameworkElement frameworkElement &&
            frameworkElement.RecognizesAccessKey)
        {
            IncrementAggregate(ref _diagResolveAccessKeyTargetRecognizesAccessKeyPathCount);
            return frameworkElement;
        }

        IncrementAggregate(ref _diagResolveAccessKeyTargetNoTargetCount);
        return null;
    }

    private bool TryRefreshFallbackTextStyling(DependencyProperty? changedProperty = null)
    {
        IncrementMetric(ref _runtimeTryRefreshFallbackTextStylingCallCount, ref _diagTryRefreshFallbackTextStylingCallCount);
        var content = ResolveEffectiveContent();
        if (content == null)
        {
            IncrementMetric(ref _runtimeTryRefreshFallbackTextStylingNoContentCount, ref _diagTryRefreshFallbackTextStylingNoContentCount);
            return false;
        }

        if (content is UIElement)
        {
            IncrementMetric(ref _runtimeTryRefreshFallbackTextStylingUiElementPathCount, ref _diagTryRefreshFallbackTextStylingUiElementPathCount);
            return false;
        }

        if (ResolveEffectiveTemplate() != null)
        {
            IncrementMetric(ref _runtimeTryRefreshFallbackTextStylingTemplatePathCount, ref _diagTryRefreshFallbackTextStylingTemplatePathCount);
            return false;
        }

        if (_presentedElement is Label label && !RecognizesAccessKey)
        {
            IncrementMetric(ref _runtimeTryRefreshFallbackTextStylingLabelPathCount, ref _diagTryRefreshFallbackTextStylingLabelPathCount);
            ApplyFallbackLabelStyling(label, changedProperty);
            return true;
        }

        if (_presentedElement is TextBlock textBlock && RecognizesAccessKey)
        {
            IncrementMetric(ref _runtimeTryRefreshFallbackTextStylingTextBlockPathCount, ref _diagTryRefreshFallbackTextStylingTextBlockPathCount);
            ApplyFallbackTextBlockStyling(textBlock, changedProperty);
            return true;
        }

        IncrementMetric(ref _runtimeTryRefreshFallbackTextStylingNoMatchCount, ref _diagTryRefreshFallbackTextStylingNoMatchCount);
        return false;
    }

    private bool TryRefreshCalendarDayButtonTextPresentation(DependencyProperty? changedProperty)
    {
        if (!ShouldUseCalendarDayButtonTextPresentation(ResolveEffectiveTemplate()) ||
            _presentedElement is not CalendarDayTextPresenter dayTextPresenter ||
            changedProperty == null)
        {
            return false;
        }

        if (IsCalendarDayButtonTextProperty(changedProperty))
        {
                var nextText = ResolveCalendarDayButtonDayText();
            if (!string.Equals(dayTextPresenter.Text, nextText, StringComparison.Ordinal))
            {
                dayTextPresenter.Text = nextText;
            }

            return true;
        }

        if (IsForegroundProperty(changedProperty))
        {
            ApplyCalendarDayTextPresenterStyling(dayTextPresenter, changedProperty);
            return true;
        }

        if (IsCalendarDayTextPresentationVisualProperty(changedProperty))
        {
            dayTextPresenter.InvalidateVisual();
            return true;
        }

        return false;
    }

    private bool ShouldUseCalendarDayButtonTextPresentation(DataTemplate? template)
    {
        return template == null &&
               _sourceOwner is CalendarDayButton &&
               !HasLocalValue(ContentProperty) &&
               !HasLocalValue(ContentTemplateProperty) &&
               !HasLocalValue(ContentTemplateSelectorProperty) &&
               string.Equals(GetEffectiveContentSourcePropertyName(), nameof(CalendarDayButton.DayText), StringComparison.Ordinal);
    }

    private UIElement BuildCalendarDayButtonTextElement(object? content)
    {
        var dayTextPresenter = _presentedElement as CalendarDayTextPresenter ?? new CalendarDayTextPresenter();
        var nextText = content?.ToString() ?? string.Empty;
        if (!string.Equals(dayTextPresenter.Text, nextText, StringComparison.Ordinal))
        {
            dayTextPresenter.Text = nextText;
        }

        ApplyCalendarDayTextPresenterStyling(dayTextPresenter, changedProperty: null);
        return dayTextPresenter;
    }

    private void ApplyCalendarDayTextPresenterStyling(CalendarDayTextPresenter dayTextPresenter, DependencyProperty? changedProperty)
    {
        if (IsForegroundProperty(changedProperty))
        {
            if (TryGetOwnerForeground(out var foregroundOnly))
            {
                TryAssignIfChanged(
                    dayTextPresenter,
                    static currentPresenter => currentPresenter.Foreground,
                    static (currentPresenter, value) => currentPresenter.Foreground = value,
                    foregroundOnly);
            }

            return;
        }

        if (TryGetOwnerForeground(out var foreground))
        {
            TryAssignIfChanged(
                dayTextPresenter,
                static currentPresenter => currentPresenter.Foreground,
                static (currentPresenter, value) => currentPresenter.Foreground = value,
                foreground);
        }
    }

    private void ApplyFallbackLabelStyling(Label label, DependencyProperty? changedProperty)
    {
        if (IsForegroundProperty(changedProperty))
        {
            if (_sourceOwner != null && TryGetOwnerPropertyValue<Color>(_sourceOwner, nameof(Control.Foreground), out var foregroundOnly))
            {
                ApplyFallbackLabelAssignment(
                    label,
                    "Foreground",
                    static currentLabel => currentLabel.Foreground,
                    static (currentLabel, value) => currentLabel.Foreground = value,
                    foregroundOnly);
            }

            return;
        }

        if (_sourceOwner is FrameworkElement frameworkElement)
        {
            ApplyFallbackLabelAssignment(
                label,
                "FontFamily",
                static currentLabel => currentLabel.FontFamily,
                static (currentLabel, value) => currentLabel.FontFamily = value,
                frameworkElement.FontFamily);
            ApplyFallbackLabelAssignment(
                label,
                "FontSize",
                static currentLabel => currentLabel.FontSize,
                static (currentLabel, value) => currentLabel.FontSize = value,
                frameworkElement.FontSize);
            ApplyFallbackLabelAssignment(
                label,
                "FontWeight",
                static currentLabel => currentLabel.FontWeight,
                static (currentLabel, value) => currentLabel.FontWeight = value,
                frameworkElement.FontWeight);
            ApplyFallbackLabelAssignment(
                label,
                "FontStyle",
                static currentLabel => currentLabel.FontStyle,
                static (currentLabel, value) => currentLabel.FontStyle = value,
                frameworkElement.FontStyle);
        }

        if (_sourceOwner != null && TryGetOwnerPropertyValue<Color>(_sourceOwner, nameof(Control.Foreground), out var foreground))
        {
            ApplyFallbackLabelAssignment(
                label,
                "Foreground",
                static currentLabel => currentLabel.Foreground,
                static (currentLabel, value) => currentLabel.Foreground = value,
                foreground);
        }
    }

    private void ApplyFallbackTextBlockStyling(TextBlock textBlock, DependencyProperty? changedProperty)
    {
        if (IsForegroundProperty(changedProperty))
        {
            if (_sourceOwner != null && TryGetOwnerPropertyValue<Color>(_sourceOwner, nameof(Control.Foreground), out var foregroundOnly))
            {
                ApplyFallbackTextBlockAssignment(
                    textBlock,
                    static currentTextBlock => currentTextBlock.Foreground,
                    static (currentTextBlock, value) => currentTextBlock.Foreground = value,
                    foregroundOnly);
            }

            return;
        }

        if (_sourceOwner is FrameworkElement frameworkElement)
        {
            ApplyFallbackTextBlockAssignment(
                textBlock,
                static currentTextBlock => currentTextBlock.FontFamily,
                static (currentTextBlock, value) => currentTextBlock.FontFamily = value,
                frameworkElement.FontFamily);
            ApplyFallbackTextBlockAssignment(
                textBlock,
                static currentTextBlock => currentTextBlock.FontSize,
                static (currentTextBlock, value) => currentTextBlock.FontSize = value,
                frameworkElement.FontSize);
            ApplyFallbackTextBlockAssignment(
                textBlock,
                static currentTextBlock => currentTextBlock.FontWeight,
                static (currentTextBlock, value) => currentTextBlock.FontWeight = value,
                frameworkElement.FontWeight);
            ApplyFallbackTextBlockAssignment(
                textBlock,
                static currentTextBlock => currentTextBlock.FontStyle,
                static (currentTextBlock, value) => currentTextBlock.FontStyle = value,
                frameworkElement.FontStyle);
        }

        if (_sourceOwner != null && TryGetOwnerPropertyValue<Color>(_sourceOwner, nameof(Control.Foreground), out var foreground))
        {
            ApplyFallbackTextBlockAssignment(
                textBlock,
                static currentTextBlock => currentTextBlock.Foreground,
                static (currentTextBlock, value) => currentTextBlock.Foreground = value,
                foreground);
        }
    }

    private static bool IsForegroundProperty(DependencyProperty? property)
    {
        return property != null &&
               string.Equals(property.Name, nameof(Control.Foreground), StringComparison.Ordinal);
    }

    private static bool IsCalendarDayButtonTextProperty(DependencyProperty property)
    {
        return string.Equals(property.Name, nameof(CalendarDayButton.DayText), StringComparison.Ordinal);
    }

    private static bool IsCalendarDayTextPresentationVisualProperty(DependencyProperty property)
    {
        return string.Equals(property.Name, nameof(FrameworkElement.FontFamily), StringComparison.Ordinal) ||
               string.Equals(property.Name, nameof(FrameworkElement.FontSize), StringComparison.Ordinal) ||
               string.Equals(property.Name, nameof(FrameworkElement.FontWeight), StringComparison.Ordinal) ||
               string.Equals(property.Name, nameof(FrameworkElement.FontStyle), StringComparison.Ordinal);
    }

    private void ApplyFallbackLabelAssignment<TValue>(
        Label label,
        string propertyName,
        Func<Label, TValue> getter,
        Action<Label, TValue> setter,
        TValue value)
    {
            _ = propertyName;
            TryAssignIfChanged(label, getter, setter, value);
    }

    private void ApplyFallbackTextBlockAssignment<TValue>(
        TextBlock textBlock,
        Func<TextBlock, TValue> getter,
        Action<TextBlock, TValue> setter,
        TValue value)
    {
            TryAssignIfChanged(textBlock, getter, setter, value);
        }

        private string ResolveCalendarDayButtonDayText()
        {
            return TryGetCalendarDayButtonContentOwner(out var dayButton)
                ? dayButton.DayText
                : ResolveEffectiveContent()?.ToString() ?? string.Empty;
        }

        private bool TryGetOwnerForeground(out Color foreground)
        {
            if (_sourceOwner is Control control)
            {
                foreground = control.Foreground;
                return true;
            }

            if (_sourceOwner != null && TryGetOwnerPropertyValue<Color>(_sourceOwner, nameof(Control.Foreground), out foreground))
            {
                return true;
            }

            foreground = default;
            return false;
        }

        private bool TryGetCalendarDayButtonContentOwner(out CalendarDayButton dayButton)
        {
            if (_sourceOwner is CalendarDayButton candidate &&
                !HasLocalValue(ContentProperty) &&
                string.Equals(GetEffectiveContentSourcePropertyName(), nameof(CalendarDayButton.DayText), StringComparison.Ordinal))
            {
                dayButton = candidate;
                return true;
            }

            dayButton = null!;
            return false;
        }

        private static void TryAssignIfChanged<TElement, TValue>(
            TElement element,
            Func<TElement, TValue> getter,
            Action<TElement, TValue> setter,
            TValue value)
        {
            if (EqualityComparer<TValue>.Default.Equals(getter(element), value))
            {
                return;
            }

            setter(element, value);
    }

    private static bool TryGetOwnerPropertyValue<TValue>(DependencyObject owner, string propertyName, out TValue value)
    {
        var property = FindReadableProperty(owner.GetType(), propertyName);
        if (property?.PropertyType == typeof(TValue))
        {
            var resolved = property.GetValue(owner);
            if (resolved is TValue typed)
            {
                value = typed;
                return true;
            }
        }

        value = default!;
        return false;
    }

    private bool WouldCreatePresentationCycle(UIElement candidate)
    {
        IncrementMetric(ref _runtimeWouldCreatePresentationCycleCallCount, ref _diagWouldCreatePresentationCycleCallCount);
        // Reusing the currently presented element is valid; this happens during
        // refresh passes where content did not materially change.
        if (ReferenceEquals(candidate, _presentedElement))
        {
            IncrementMetric(ref _runtimeWouldCreatePresentationCycleCurrentPresentedReuseCount, ref _diagWouldCreatePresentationCycleCurrentPresentedReuseCount);
            return false;
        }

        if (ReferenceEquals(candidate, this))
        {
            IncrementMetric(ref _runtimeWouldCreatePresentationCycleSelfCount, ref _diagWouldCreatePresentationCycleSelfCount);
            return true;
        }

        for (UIElement? current = this; current != null; current = current.LogicalParent ?? current.VisualParent)
        {
            if (ReferenceEquals(current, candidate))
            {
                IncrementMetric(ref _runtimeWouldCreatePresentationCycleAncestorMatchCount, ref _diagWouldCreatePresentationCycleAncestorMatchCount);
                return true;
            }
        }

        for (UIElement? current = candidate; current != null; current = current.LogicalParent ?? current.VisualParent)
        {
            if (ReferenceEquals(current, this))
            {
                IncrementMetric(ref _runtimeWouldCreatePresentationCycleDescendantMatchCount, ref _diagWouldCreatePresentationCycleDescendantMatchCount);
                return true;
            }
        }

        IncrementMetric(ref _runtimeWouldCreatePresentationCycleFalseCount, ref _diagWouldCreatePresentationCycleFalseCount);
        return false;
    }

    private static Label BuildCycleGuardLabel(object? content)
    {
        var contentType = content?.GetType().Name ?? "null";
        return new Label { Content = $"ContentPresenter cycle guard ({contentType})" };
    }

    private DependencyObject? FindSourceOwner()
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeFindSourceOwnerCallCount, ref _diagFindSourceOwnerCallCount);
        try
        {
            for (var current = LogicalParent ?? VisualParent; current != null; current = current.LogicalParent ?? current.VisualParent)
            {
                if (current is not DependencyObject dependencyObject)
                {
                    continue;
                }

                IncrementMetric(ref _runtimeFindSourceOwnerAncestorProbeCount, ref _diagFindSourceOwnerAncestorProbeCount);

                var contentSource = ContentSource;
                if (string.IsNullOrEmpty(contentSource))
                {
                    contentSource = "Content";
                }

                if (!HasLocalValue(ContentSourceProperty) &&
                    string.Equals(contentSource, "Content", StringComparison.Ordinal) &&
                    current is CalendarDayButton)
                {
                    IncrementMetric(ref _runtimeFindSourceOwnerPropertyMatchCount, ref _diagFindSourceOwnerPropertyMatchCount);
                    IncrementMetric(ref _runtimeFindSourceOwnerFoundCount, ref _diagFindSourceOwnerFoundCount);
                    return dependencyObject;
                }

                var property = FindReadableProperty(current.GetType(), contentSource);
                if (property != null)
                {
                    IncrementMetric(ref _runtimeFindSourceOwnerPropertyMatchCount, ref _diagFindSourceOwnerPropertyMatchCount);
                    try
                    {
                        var value = property.GetValue(current);
                        if (ReferenceEquals(value, this))
                        {
                            IncrementMetric(ref _runtimeFindSourceOwnerSelfOwnerSkipCount, ref _diagFindSourceOwnerSelfOwnerSkipCount);
                            // Avoid self-owner cycles (for example host.Content == this presenter).
                            continue;
                        }
                    }
                    catch
                    {
                        IncrementMetric(ref _runtimeFindSourceOwnerGetterFailureCount, ref _diagFindSourceOwnerGetterFailureCount);
                        // Ignore reflective getter failures and continue owner probing.
                    }

                    IncrementMetric(ref _runtimeFindSourceOwnerFoundCount, ref _diagFindSourceOwnerFoundCount);
                    return dependencyObject;
                }
            }

            IncrementMetric(ref _runtimeFindSourceOwnerNotFoundCount, ref _diagFindSourceOwnerNotFoundCount);
            return null;
        }
        finally
        {
            AddMetric(ref _runtimeFindSourceOwnerElapsedTicks, ref _diagFindSourceOwnerElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    private static PropertyInfo? FindReadableProperty(Type type, string propertyName)
    {
        var start = Stopwatch.GetTimestamp();
        IncrementAggregate(ref _diagFindReadablePropertyCallCount);
        try
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var direct = current.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                for (var i = 0; i < direct.Length; i++)
                {
                    var property = direct[i];
                    if (!string.Equals(property.Name, propertyName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (property.GetIndexParameters().Length != 0)
                    {
                        continue;
                    }

                    if (property.GetMethod == null)
                    {
                        continue;
                    }

                    IncrementAggregate(ref _diagFindReadablePropertyMatchedCount);
                    return property;
                }
            }

            IncrementAggregate(ref _diagFindReadablePropertyNotFoundCount);
            return null;
        }
        finally
        {
            AddAggregate(ref _diagFindReadablePropertyElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    internal ContentPresenterRuntimeDiagnosticsSnapshot GetContentPresenterSnapshotForDiagnostics()
    {
        return new ContentPresenterRuntimeDiagnosticsSnapshot(
            _presentedElement is not null,
            _presentedElement?.GetType().Name ?? string.Empty,
            _sourceOwner is not null,
            _sourceOwner?.GetType().Name ?? string.Empty,
            ContentSource,
            _hasEffectivePresentationState,
            HasLocalValue(ContentProperty),
            HasLocalValue(ContentTemplateProperty),
            HasLocalValue(ContentTemplateSelectorProperty),
            LayoutSlot.Width,
            LayoutSlot.Height,
            _runtimeRefreshSourceBindingCallCount,
            _runtimeEnsureSourceBindingCallCount,
            TicksToMilliseconds(_runtimeEnsureSourceBindingElapsedTicks),
            _runtimeEnsureSourceBindingOwnerUnchangedCount,
            _runtimeEnsureSourceBindingDetachedOwnerCount,
            _runtimeEnsureSourceBindingAttachedOwnerCount,
            _runtimeEnsureSourceBindingAttachedContentControlCount,
            _runtimeOnSourceOwnerPropertyChangedCallCount,
            TicksToMilliseconds(_runtimeOnSourceOwnerPropertyChangedElapsedTicks),
            _runtimeOnSourceOwnerPropertyChangedIrrelevantPropertyCount,
            _runtimeOnSourceOwnerPropertyChangedRebuiltPresentedElementCount,
            _runtimeOnSourceOwnerPropertyChangedRefreshedFallbackTextCount,
            _runtimeOnSourceOwnerPropertyChangedInvalidatedArrangeCount,
            _runtimeRefreshPresentedElementCallCount,
            TicksToMilliseconds(_runtimeRefreshPresentedElementElapsedTicks),
            _runtimeRefreshPresentedElementStateCacheHitCount,
            _runtimeRefreshPresentedElementSelectedTemplateCount,
            _runtimeRefreshPresentedElementBuiltSameInstanceCount,
            _runtimeRefreshPresentedElementDetachedOldElementCount,
            _runtimeRefreshPresentedElementAttachedNewElementCount,
            _runtimeRefreshPresentedElementChangedCount,
            _runtimeBuildContentElementCallCount,
            TicksToMilliseconds(_runtimeBuildContentElementElapsedTicks),
            _runtimeBuildContentElementUiElementPathCount,
            _runtimeBuildContentElementTemplatePathCount,
            _runtimeBuildContentElementAccessTextPathCount,
            _runtimeBuildContentElementLabelPathCount,
            _runtimeBuildContentElementNullPathCount,
            _runtimeBuildContentElementCycleGuardCount,
            _runtimeTryRefreshFallbackTextStylingCallCount,
            _runtimeTryRefreshFallbackTextStylingNoContentCount,
            _runtimeTryRefreshFallbackTextStylingUiElementPathCount,
            _runtimeTryRefreshFallbackTextStylingTemplatePathCount,
            _runtimeTryRefreshFallbackTextStylingLabelPathCount,
            _runtimeTryRefreshFallbackTextStylingTextBlockPathCount,
            _runtimeTryRefreshFallbackTextStylingNoMatchCount,
            _runtimeWouldCreatePresentationCycleCallCount,
            _runtimeWouldCreatePresentationCycleCurrentPresentedReuseCount,
            _runtimeWouldCreatePresentationCycleSelfCount,
            _runtimeWouldCreatePresentationCycleAncestorMatchCount,
            _runtimeWouldCreatePresentationCycleDescendantMatchCount,
            _runtimeWouldCreatePresentationCycleFalseCount,
            _runtimeFindSourceOwnerCallCount,
            TicksToMilliseconds(_runtimeFindSourceOwnerElapsedTicks),
            _runtimeFindSourceOwnerAncestorProbeCount,
            _runtimeFindSourceOwnerPropertyMatchCount,
            _runtimeFindSourceOwnerSelfOwnerSkipCount,
            _runtimeFindSourceOwnerGetterFailureCount,
            _runtimeFindSourceOwnerFoundCount,
            _runtimeFindSourceOwnerNotFoundCount,
            _runtimeMeasureOverrideCallCount,
            TicksToMilliseconds(_runtimeMeasureOverrideElapsedTicks),
            _runtimeMeasureOverridePresentedElementPathCount,
            _runtimeMeasureOverrideNoPresentedElementCount,
            _runtimeArrangeOverrideCallCount,
            TicksToMilliseconds(_runtimeArrangeOverrideElapsedTicks),
            _runtimeArrangeOverridePresentedElementPathCount,
            _runtimeArrangeOverrideNoPresentedElementCount);
    }

    internal new static ContentPresenterTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return CreateAggregateTelemetrySnapshot(reset: false);
    }

    internal static ContentPresenterTelemetrySnapshot GetTelemetrySnapshotForDiagnostics()
    {
        return GetAggregateTelemetrySnapshotForDiagnostics();
    }

    internal new static ContentPresenterTelemetrySnapshot GetTelemetryAndReset()
    {
        return CreateAggregateTelemetrySnapshot(reset: true);
    }

    private static ContentPresenterTelemetrySnapshot CreateAggregateTelemetrySnapshot(bool reset)
    {
        return new ContentPresenterTelemetrySnapshot(
            ReadOrReset(ref _diagConstructorCallCount, reset),
            ReadOrReset(ref _diagGetVisualChildrenCallCount, reset),
            ReadOrReset(ref _diagGetVisualChildrenYieldedChildCount, reset),
            ReadOrReset(ref _diagGetVisualChildCountForTraversalCallCount, reset),
            ReadOrReset(ref _diagGetVisualChildAtForTraversalCallCount, reset),
            ReadOrReset(ref _diagGetVisualChildAtForTraversalOutOfRangeCount, reset),
            ReadOrReset(ref _diagGetLogicalChildrenCallCount, reset),
            ReadOrReset(ref _diagGetLogicalChildrenYieldedChildCount, reset),
            ReadOrReset(ref _diagNotifyOwnerContentChangedCallCount, reset),
            ReadOrReset(ref _diagNotifyOwnerContentChangedInvalidatedMeasureCount, reset),
            ReadOrReset(ref _diagDependencyPropertyChangedCallCount, reset),
            ReadOrReset(ref _diagDependencyPropertyChangedRelevantPropertyCount, reset),
            ReadOrReset(ref _diagVisualParentChangedCallCount, reset),
            ReadOrReset(ref _diagLogicalParentChangedCallCount, reset),
            ReadOrReset(ref _diagMeasureOverrideCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagMeasureOverrideElapsedTicks, reset)),
            ReadOrReset(ref _diagMeasureOverridePresentedElementPathCount, reset),
            ReadOrReset(ref _diagMeasureOverrideNoPresentedElementCount, reset),
            ReadOrReset(ref _diagCanReuseMeasureCallCount, reset),
            ReadOrReset(ref _diagCanReuseMeasureNoPresentedElementCount, reset),
            ReadOrReset(ref _diagCanReuseMeasureDelegatedCount, reset),
            ReadOrReset(ref _diagArrangeOverrideCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagArrangeOverrideElapsedTicks, reset)),
            ReadOrReset(ref _diagArrangeOverridePresentedElementPathCount, reset),
            ReadOrReset(ref _diagArrangeOverrideNoPresentedElementCount, reset),
            ReadOrReset(ref _diagRefreshSourceBindingCallCount, reset),
            ReadOrReset(ref _diagEnsureSourceBindingCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagEnsureSourceBindingElapsedTicks, reset)),
            ReadOrReset(ref _diagEnsureSourceBindingOwnerUnchangedCount, reset),
            ReadOrReset(ref _diagEnsureSourceBindingDetachedOwnerCount, reset),
            ReadOrReset(ref _diagEnsureSourceBindingAttachedOwnerCount, reset),
            ReadOrReset(ref _diagEnsureSourceBindingAttachedContentControlCount, reset),
            ReadOrReset(ref _diagOnSourceOwnerPropertyChangedCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagOnSourceOwnerPropertyChangedElapsedTicks, reset)),
            ReadOrReset(ref _diagOnSourceOwnerPropertyChangedIrrelevantPropertyCount, reset),
            ReadOrReset(ref _diagOnSourceOwnerPropertyChangedRebuiltPresentedElementCount, reset),
            ReadOrReset(ref _diagOnSourceOwnerPropertyChangedRefreshedFallbackTextCount, reset),
            ReadOrReset(ref _diagOnSourceOwnerPropertyChangedInvalidatedArrangeCount, reset),
            ReadOrReset(ref _diagRefreshPresentedElementCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagRefreshPresentedElementElapsedTicks, reset)),
            ReadOrReset(ref _diagRefreshPresentedElementStateCacheHitCount, reset),
            ReadOrReset(ref _diagRefreshPresentedElementSelectedTemplateCount, reset),
            ReadOrReset(ref _diagRefreshPresentedElementBuiltSameInstanceCount, reset),
            ReadOrReset(ref _diagRefreshPresentedElementDetachedOldElementCount, reset),
            ReadOrReset(ref _diagRefreshPresentedElementAttachedNewElementCount, reset),
            ReadOrReset(ref _diagRefreshPresentedElementChangedCount, reset),
            ReadOrReset(ref _diagIsSourceOwnerPropertyRelevantCallCount, reset),
            ReadOrReset(ref _diagIsSourceOwnerPropertyRelevantContentMatchCount, reset),
            ReadOrReset(ref _diagIsSourceOwnerPropertyRelevantTemplateMatchCount, reset),
            ReadOrReset(ref _diagIsSourceOwnerPropertyRelevantTemplateSelectorMatchCount, reset),
            ReadOrReset(ref _diagIsSourceOwnerPropertyRelevantStyleMatchCount, reset),
            ReadOrReset(ref _diagIsSourceOwnerPropertyRelevantFalseCount, reset),
            ReadOrReset(ref _diagBuildContentElementCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagBuildContentElementElapsedTicks, reset)),
            ReadOrReset(ref _diagBuildContentElementUiElementPathCount, reset),
            ReadOrReset(ref _diagBuildContentElementTemplatePathCount, reset),
            ReadOrReset(ref _diagBuildContentElementAccessTextPathCount, reset),
            ReadOrReset(ref _diagBuildContentElementLabelPathCount, reset),
            ReadOrReset(ref _diagBuildContentElementNullPathCount, reset),
            ReadOrReset(ref _diagBuildContentElementCycleGuardCount, reset),
            ReadOrReset(ref _diagResolveAccessKeyTargetCallCount, reset),
            ReadOrReset(ref _diagResolveAccessKeyTargetLabelPathCount, reset),
            ReadOrReset(ref _diagResolveAccessKeyTargetRecognizesAccessKeyPathCount, reset),
            ReadOrReset(ref _diagResolveAccessKeyTargetNoTargetCount, reset),
            ReadOrReset(ref _diagTryRefreshFallbackTextStylingCallCount, reset),
            ReadOrReset(ref _diagTryRefreshFallbackTextStylingNoContentCount, reset),
            ReadOrReset(ref _diagTryRefreshFallbackTextStylingUiElementPathCount, reset),
            ReadOrReset(ref _diagTryRefreshFallbackTextStylingTemplatePathCount, reset),
            ReadOrReset(ref _diagTryRefreshFallbackTextStylingLabelPathCount, reset),
            ReadOrReset(ref _diagTryRefreshFallbackTextStylingTextBlockPathCount, reset),
            ReadOrReset(ref _diagTryRefreshFallbackTextStylingNoMatchCount, reset),
            ReadOrReset(ref _diagWouldCreatePresentationCycleCallCount, reset),
            ReadOrReset(ref _diagWouldCreatePresentationCycleCurrentPresentedReuseCount, reset),
            ReadOrReset(ref _diagWouldCreatePresentationCycleSelfCount, reset),
            ReadOrReset(ref _diagWouldCreatePresentationCycleAncestorMatchCount, reset),
            ReadOrReset(ref _diagWouldCreatePresentationCycleDescendantMatchCount, reset),
            ReadOrReset(ref _diagWouldCreatePresentationCycleFalseCount, reset),
            ReadOrReset(ref _diagFindSourceOwnerCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagFindSourceOwnerElapsedTicks, reset)),
            ReadOrReset(ref _diagFindSourceOwnerAncestorProbeCount, reset),
            ReadOrReset(ref _diagFindSourceOwnerPropertyMatchCount, reset),
            ReadOrReset(ref _diagFindSourceOwnerSelfOwnerSkipCount, reset),
            ReadOrReset(ref _diagFindSourceOwnerGetterFailureCount, reset),
            ReadOrReset(ref _diagFindSourceOwnerFoundCount, reset),
            ReadOrReset(ref _diagFindSourceOwnerNotFoundCount, reset),
            ReadOrReset(ref _diagFindReadablePropertyCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagFindReadablePropertyElapsedTicks, reset)),
            ReadOrReset(ref _diagFindReadablePropertyMatchedCount, reset),
            ReadOrReset(ref _diagFindReadablePropertyNotFoundCount, reset));
    }

    private static void IncrementMetric(ref long runtimeCounter, ref long aggregateCounter)
    {
        runtimeCounter++;
        Interlocked.Increment(ref aggregateCounter);
    }

    private static void AddMetric(ref long runtimeCounter, ref long aggregateCounter, long value)
    {
        runtimeCounter += value;
        if (value != 0)
        {
            Interlocked.Add(ref aggregateCounter, value);
        }
    }

    private static void IncrementAggregate(ref long counter)
    {
        Interlocked.Increment(ref counter);
    }

    private static void AddAggregate(ref long counter, long value)
    {
        if (value != 0)
        {
            Interlocked.Add(ref counter, value);
        }
    }

    private static long ReadAggregate(ref long counter)
    {
        return Interlocked.Read(ref counter);
    }

    private static long ResetAggregate(ref long counter)
    {
        return Interlocked.Exchange(ref counter, 0);
    }

    private static long ReadOrReset(ref long counter, bool reset)
    {
        return reset ? ResetAggregate(ref counter) : ReadAggregate(ref counter);
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }

    private HorizontalAlignment ResolveEffectiveHorizontalContentAlignment()
    {
        if (HasLocalValue(HorizontalContentAlignmentProperty))
        {
            return HorizontalContentAlignment;
        }

        if (_sourceOwner is Control control)
        {
            return control.HorizontalContentAlignment;
        }

        return HorizontalContentAlignment;
    }

    private VerticalAlignment ResolveEffectiveVerticalContentAlignment()
    {
        if (HasLocalValue(VerticalContentAlignmentProperty))
        {
            return VerticalContentAlignment;
        }

        if (_sourceOwner is Control control)
        {
            return control.VerticalContentAlignment;
        }

        return VerticalContentAlignment;
    }

    private static float ResolveAlignedSize(float available, float desired, HorizontalAlignment alignment)
    {
        if (alignment == HorizontalAlignment.Stretch)
        {
            return available;
        }

        return MathF.Min(available, desired);
    }

    private static float ResolveAlignedSize(float available, float desired, VerticalAlignment alignment)
    {
        if (alignment == VerticalAlignment.Stretch)
        {
            return available;
        }

        return MathF.Min(available, desired);
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

    private static bool IsContentAlignmentProperty(DependencyProperty property)
    {
        return string.Equals(property.Name, nameof(Control.HorizontalContentAlignment), StringComparison.Ordinal) ||
               string.Equals(property.Name, nameof(Control.VerticalContentAlignment), StringComparison.Ordinal);
    }
}

public class ItemsPresenter : FrameworkElement
{
    private static readonly bool EnableItemsPresenterTrace = false;
    private ItemsControl? _itemsOwner;
    private ItemsControl? _explicitItemsOwner;

    internal void SetExplicitItemsOwner(ItemsControl? owner)
    {
        if (ReferenceEquals(_explicitItemsOwner, owner))
        {
            return;
        }

        _explicitItemsOwner = owner;
        RefreshOwner();
    }

    internal bool TryGetItemContainersForHitTest(out IReadOnlyList<UIElement> containers)
    {
        EnsureOwner();
        if (_itemsOwner == null)
        {
            containers = Array.Empty<UIElement>();
            return false;
        }

        containers = _itemsOwner.GetItemContainersForPresenter();
        return true;
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        if (_itemsOwner == null)
        {
            yield break;
        }

        foreach (var child in _itemsOwner.GetItemContainersForPresenter())
        {
            yield return child;
        }
    }

    internal override int GetVisualChildCountForTraversal()
    {
        EnsureOwner();
        return _itemsOwner?.GetItemContainersForPresenter().Count ?? 0;
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        EnsureOwner();
        var items = _itemsOwner?.GetItemContainersForPresenter();
        if (items != null && (uint)index < (uint)items.Count)
        {
            return items[index];
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        if (_itemsOwner == null)
        {
            yield break;
        }

        foreach (var child in _itemsOwner.GetItemContainersForPresenter())
        {
            yield return child;
        }
    }

    protected override void OnVisualParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        base.OnVisualParentChanged(oldParent, newParent);
        RefreshOwner();
    }

    protected override void OnLogicalParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        base.OnLogicalParentChanged(oldParent, newParent);
        RefreshOwner();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        EnsureOwner();
        if (_itemsOwner == null)
        {
            return Vector2.Zero;
        }

        Trace($"Measure start available={availableSize} owner={_itemsOwner.GetType().Name} items={_itemsOwner.GetItemContainersForPresenter().Count}");

        var desired = Vector2.Zero;
        foreach (var child in _itemsOwner.GetItemContainersForPresenter())
        {
            if (child is not FrameworkElement element)
            {
                continue;
            }

            element.Measure(availableSize);
            desired.X = MathF.Max(desired.X, element.DesiredSize.X);
            desired.Y += element.DesiredSize.Y;
        }

        Trace($"Measure end desired={desired}");
        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        EnsureOwner();
        if (_itemsOwner == null)
        {
            return finalSize;
        }

        Trace($"Arrange start final={finalSize} owner={_itemsOwner.GetType().Name} items={_itemsOwner.GetItemContainersForPresenter().Count}");

        var y = LayoutSlot.Y;
        foreach (var child in _itemsOwner.GetItemContainersForPresenter())
        {
            if (child is not FrameworkElement element)
            {
                continue;
            }

            var height = element.DesiredSize.Y;
            element.Arrange(new LayoutRect(LayoutSlot.X, y, finalSize.X, height));
            y += height;
        }

        Trace($"Arrange end final={finalSize}");
        return finalSize;
    }

    private void RefreshOwner()
    {
        EnsureOwner(force: true);
    }

    private void EnsureOwner(bool force = false)
    {
        var foundOwner = _explicitItemsOwner ?? FindItemsOwner();
        if (!force && ReferenceEquals(foundOwner, _itemsOwner))
        {
            return;
        }

        if (_itemsOwner != null)
        {
            _itemsOwner.DetachItemsHost(this);
            _itemsOwner = null;
        }

        _itemsOwner = foundOwner;
        _itemsOwner?.AttachItemsHost(this);
        InvalidateMeasure();
    }

    private ItemsControl? FindItemsOwner()
    {
        for (var current = LogicalParent ?? VisualParent; current != null; current = current.LogicalParent ?? current.VisualParent)
        {
            if (current is ItemsControl itemsControl)
            {
                return itemsControl;
            }
        }

        return null;
    }

    private void Trace(string message)
    {
        if (!EnableItemsPresenterTrace)
        {
            return;
        }

        Console.WriteLine($"[ItemsPresenter#{GetHashCode():X8}] t={Environment.TickCount64} {message}");
    }
}

public class HeaderedContentControl : ContentControl
{
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(
            nameof(Header),
            typeof(object),
            typeof(HeaderedContentControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty HeaderTemplateProperty =
        DependencyProperty.Register(
            nameof(HeaderTemplate),
            typeof(DataTemplate),
            typeof(HeaderedContentControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty HeaderTemplateSelectorProperty =
        DependencyProperty.Register(
            nameof(HeaderTemplateSelector),
            typeof(DataTemplateSelector),
            typeof(HeaderedContentControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public DataTemplate? HeaderTemplate
    {
        get => GetValue<DataTemplate>(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    public DataTemplateSelector? HeaderTemplateSelector
    {
        get => GetValue<DataTemplateSelector>(HeaderTemplateSelectorProperty);
        set => SetValue(HeaderTemplateSelectorProperty, value);
    }
}

public class HeaderedItemsControl : ItemsControl
{
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(
            nameof(Header),
            typeof(object),
            typeof(HeaderedItemsControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty HeaderTemplateProperty =
        DependencyProperty.Register(
            nameof(HeaderTemplate),
            typeof(DataTemplate),
            typeof(HeaderedItemsControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty HeaderTemplateSelectorProperty =
        DependencyProperty.Register(
            nameof(HeaderTemplateSelector),
            typeof(DataTemplateSelector),
            typeof(HeaderedItemsControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    private UIElement? _headerElement;

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public DataTemplate? HeaderTemplate
    {
        get => GetValue<DataTemplate>(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    public DataTemplateSelector? HeaderTemplateSelector
    {
        get => GetValue<DataTemplateSelector>(HeaderTemplateSelectorProperty);
        set => SetValue(HeaderTemplateSelectorProperty, value);
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        foreach (var child in base.GetVisualChildren())
        {
            yield return child;
        }

        if (_headerElement != null)
        {
            yield return _headerElement;
        }
    }

    internal override int GetVisualChildCountForTraversal()
    {
        return base.GetVisualChildCountForTraversal() + (_headerElement != null ? 1 : 0);
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        var baseCount = base.GetVisualChildCountForTraversal();
        if (index < baseCount)
        {
            return base.GetVisualChildAtForTraversal(index);
        }

        if (index == baseCount && _headerElement != null)
        {
            return _headerElement;
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        foreach (var child in base.GetLogicalChildren())
        {
            yield return child;
        }

        if (_headerElement != null)
        {
            yield return _headerElement;
        }
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);
        if (args.Property == HeaderProperty ||
            args.Property == HeaderTemplateProperty ||
            args.Property == HeaderTemplateSelectorProperty)
        {
            UpdateHeaderElement();
            InvalidateMeasure();
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        if (_headerElement is FrameworkElement header)
        {
            header.Measure(availableSize);
            desired.X = MathF.Max(desired.X, header.DesiredSize.X);
            desired.Y += header.DesiredSize.Y;
        }

        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        if (_headerElement is FrameworkElement header)
        {
            var headerHeight = header.DesiredSize.Y;
            header.Arrange(new LayoutRect(LayoutSlot.X, LayoutSlot.Y, finalSize.X, headerHeight));

            var offset = headerHeight;
            foreach (var child in GetItemContainersForPresenter())
            {
                if (child is FrameworkElement element)
                {
                    var rect = element.LayoutSlot;
                    element.Arrange(new LayoutRect(rect.X, rect.Y + offset, rect.Width, rect.Height));
                }
            }
        }

        return arranged;
    }

    private void UpdateHeaderElement()
    {
        if (_headerElement != null)
        {
            _headerElement.SetVisualParent(null);
            _headerElement.SetLogicalParent(null);
            _headerElement = null;
        }

        if (Header is UIElement headerElement)
        {
            _headerElement = headerElement;
        }
        else
        {
            var template = DataTemplateResolver.ResolveTemplateForContent(
                this,
                Header,
                HeaderTemplate,
                HeaderTemplateSelector,
                this);
            if (template != null)
            {
                _headerElement = template.Build(Header, this);
            }
            else if (Header != null)
            {
                _headerElement = new Label { Content = Header.ToString() ?? string.Empty };
            }
        }

        if (_headerElement != null)
        {
            _headerElement.SetVisualParent(this);
            _headerElement.SetLogicalParent(this);
        }
    }
}
