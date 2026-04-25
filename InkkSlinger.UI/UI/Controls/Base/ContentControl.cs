using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class ContentControl : Control
{
    private static long _diagDependencyPropertyChangedCallCount;
    private static long _diagDependencyPropertyChangedElapsedTicks;
    private static long _diagDependencyPropertyChangedContentPropertyCount;
    private static long _diagDependencyPropertyChangedTemplatePropertyCount;
    private static long _diagDependencyPropertyChangedOtherPropertyCount;
    private static long _diagVisualParentChangedCallCount;
    private static long _diagLogicalParentChangedCallCount;
    private static long _diagGetVisualChildrenCallCount;
    private static long _diagGetVisualChildrenYieldedBaseChildCount;
    private static long _diagGetVisualChildrenYieldedContentChildCount;
    private static long _diagGetVisualChildCountForTraversalCallCount;
    private static long _diagGetVisualChildCountForTraversalWithContentElementCount;
    private static long _diagGetVisualChildCountForTraversalWithoutContentElementCount;
    private static long _diagGetVisualChildAtForTraversalCallCount;
    private static long _diagGetVisualChildAtForTraversalBasePathCount;
    private static long _diagGetVisualChildAtForTraversalContentPathCount;
    private static long _diagGetVisualChildAtForTraversalOutOfRangeCount;
    private static long _diagGetLogicalChildrenCallCount;
    private static long _diagGetLogicalChildrenYieldedBaseChildCount;
    private static long _diagGetLogicalChildrenYieldedContentChildCount;
    private static long _diagMeasureOverrideCallCount;
    private static long _diagMeasureOverrideElapsedTicks;
    private static long _diagMeasureOverrideActivePresenterSkipCount;
    private static long _diagMeasureOverrideContentMeasureCount;
    private static long _diagMeasureOverrideNoContentCount;
    private static long _diagCanReuseMeasureCallCount;
    private static long _diagCanReuseMeasureBaseRejectedCount;
    private static long _diagCanReuseMeasureDelegatedCount;
    private static long _diagCanReuseMeasureActivePresenterOrNoContentTrueCount;
    private static long _diagArrangeOverrideCallCount;
    private static long _diagArrangeOverrideElapsedTicks;
    private static long _diagArrangeOverrideActivePresenterSkipCount;
    private static long _diagArrangeOverrideContentArrangeCount;
    private static long _diagArrangeOverrideNoContentCount;
    private static long _diagAttachContentPresenterCallCount;
    private static long _diagAttachContentPresenterNoOpCount;
    private static long _diagAttachContentPresenterInvalidateMeasureCount;
    private static long _diagDetachContentPresenterCallCount;
    private static long _diagDetachContentPresenterIgnoredCount;
    private static long _diagDetachContentPresenterInvalidateMeasureCount;
    private static long _diagUpdateContentElementCallCount;
    private static long _diagUpdateContentElementElapsedTicks;
    private static long _diagUpdateContentElementReusedExistingElementCount;
    private static long _diagUpdateContentElementNullNoOpCount;
    private static long _diagUpdateContentElementDetachedOldElementCount;
    private static long _diagUpdateContentElementPresenterNotifyCount;
    private static long _diagUpdateContentElementLabelBypassCount;
    private static long _diagUpdateContentElementUiElementPathCount;
    private static long _diagUpdateContentElementTemplateSelectedCount;
    private static long _diagUpdateContentElementTemplateBuiltElementCount;
    private static long _diagUpdateContentElementTemplateReturnedNullCount;
    private static long _diagUpdateContentElementImplicitCreationSuppressedCount;
    private static long _diagUpdateContentElementImplicitLabelCreatedCount;
    private static long _diagUpdateContentElementNullContentTerminalCount;
    private static long _diagUpdateContentElementAttachedNewElementCount;

    private long _runtimeDependencyPropertyChangedCallCount;
    private long _runtimeDependencyPropertyChangedElapsedTicks;
    private long _runtimeDependencyPropertyChangedContentPropertyCount;
    private long _runtimeDependencyPropertyChangedTemplatePropertyCount;
    private long _runtimeDependencyPropertyChangedOtherPropertyCount;
    private long _runtimeVisualParentChangedCallCount;
    private long _runtimeLogicalParentChangedCallCount;
    private long _runtimeGetVisualChildrenCallCount;
    private long _runtimeGetVisualChildrenYieldedBaseChildCount;
    private long _runtimeGetVisualChildrenYieldedContentChildCount;
    private long _runtimeGetVisualChildCountForTraversalCallCount;
    private long _runtimeGetVisualChildCountForTraversalWithContentElementCount;
    private long _runtimeGetVisualChildCountForTraversalWithoutContentElementCount;
    private long _runtimeGetVisualChildAtForTraversalCallCount;
    private long _runtimeGetVisualChildAtForTraversalBasePathCount;
    private long _runtimeGetVisualChildAtForTraversalContentPathCount;
    private long _runtimeGetVisualChildAtForTraversalOutOfRangeCount;
    private long _runtimeGetLogicalChildrenCallCount;
    private long _runtimeGetLogicalChildrenYieldedBaseChildCount;
    private long _runtimeGetLogicalChildrenYieldedContentChildCount;
    private long _runtimeMeasureOverrideCallCount;
    private long _runtimeMeasureOverrideElapsedTicks;
    private long _runtimeMeasureOverrideActivePresenterSkipCount;
    private long _runtimeMeasureOverrideContentMeasureCount;
    private long _runtimeMeasureOverrideNoContentCount;
    private long _runtimeCanReuseMeasureCallCount;
    private long _runtimeCanReuseMeasureBaseRejectedCount;
    private long _runtimeCanReuseMeasureDelegatedCount;
    private long _runtimeCanReuseMeasureActivePresenterOrNoContentTrueCount;
    private long _runtimeArrangeOverrideCallCount;
    private long _runtimeArrangeOverrideElapsedTicks;
    private long _runtimeArrangeOverrideActivePresenterSkipCount;
    private long _runtimeArrangeOverrideContentArrangeCount;
    private long _runtimeArrangeOverrideNoContentCount;
    private long _runtimeAttachContentPresenterCallCount;
    private long _runtimeAttachContentPresenterNoOpCount;
    private long _runtimeAttachContentPresenterInvalidateMeasureCount;
    private long _runtimeDetachContentPresenterCallCount;
    private long _runtimeDetachContentPresenterIgnoredCount;
    private long _runtimeDetachContentPresenterInvalidateMeasureCount;
    private long _runtimeUpdateContentElementCallCount;
    private long _runtimeUpdateContentElementElapsedTicks;
    private long _runtimeUpdateContentElementReusedExistingElementCount;
    private long _runtimeUpdateContentElementNullNoOpCount;
    private long _runtimeUpdateContentElementDetachedOldElementCount;
    private long _runtimeUpdateContentElementPresenterNotifyCount;
    private long _runtimeUpdateContentElementLabelBypassCount;
    private long _runtimeUpdateContentElementUiElementPathCount;
    private long _runtimeUpdateContentElementTemplateSelectedCount;
    private long _runtimeUpdateContentElementTemplateBuiltElementCount;
    private long _runtimeUpdateContentElementTemplateReturnedNullCount;
    private long _runtimeUpdateContentElementImplicitCreationSuppressedCount;
    private long _runtimeUpdateContentElementImplicitLabelCreatedCount;
    private long _runtimeUpdateContentElementNullContentTerminalCount;
    private long _runtimeUpdateContentElementAttachedNewElementCount;

    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(
            nameof(Content),
            typeof(object),
            typeof(ContentControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ContentTemplateProperty =
        DependencyProperty.Register(
            nameof(ContentTemplate),
            typeof(DataTemplate),
            typeof(ContentControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ContentTemplateSelectorProperty =
        DependencyProperty.Register(
            nameof(ContentTemplateSelector),
            typeof(DataTemplateSelector),
            typeof(ContentControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    private UIElement? _contentElement;
    private ContentPresenter? _activeContentPresenter;
    private bool _isForcingDeferredContentElementBuild;
    protected UIElement? ContentElement => _contentElement;

    protected virtual bool ShouldCreateImplicitContentElement(object? content, DataTemplate? selectedTemplate)
    {
        _ = content;
        _ = selectedTemplate;
        return true;
    }

    protected virtual bool ShouldDeferContentElementBuild(object? content, DataTemplate? selectedTemplate)
    {
        _ = content;
        _ = selectedTemplate;
        return false;
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

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        _runtimeGetVisualChildrenCallCount++;
        IncrementAggregate(ref _diagGetVisualChildrenCallCount);

        foreach (var element in base.GetVisualChildren())
        {
            _runtimeGetVisualChildrenYieldedBaseChildCount++;
            IncrementAggregate(ref _diagGetVisualChildrenYieldedBaseChildCount);
            yield return element;
        }

        if (_contentElement != null)
        {
            _runtimeGetVisualChildrenYieldedContentChildCount++;
            IncrementAggregate(ref _diagGetVisualChildrenYieldedContentChildCount);
            yield return _contentElement;
        }
    }

    internal override int GetVisualChildCountForTraversal()
    {
        _runtimeGetVisualChildCountForTraversalCallCount++;
        IncrementAggregate(ref _diagGetVisualChildCountForTraversalCallCount);

        if (_contentElement != null)
        {
            _runtimeGetVisualChildCountForTraversalWithContentElementCount++;
            IncrementAggregate(ref _diagGetVisualChildCountForTraversalWithContentElementCount);
        }
        else
        {
            _runtimeGetVisualChildCountForTraversalWithoutContentElementCount++;
            IncrementAggregate(ref _diagGetVisualChildCountForTraversalWithoutContentElementCount);
        }

        return base.GetVisualChildCountForTraversal() + (_contentElement != null ? 1 : 0);
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        _runtimeGetVisualChildAtForTraversalCallCount++;
        IncrementAggregate(ref _diagGetVisualChildAtForTraversalCallCount);

        var baseCount = base.GetVisualChildCountForTraversal();
        if (index < baseCount)
        {
            _runtimeGetVisualChildAtForTraversalBasePathCount++;
            IncrementAggregate(ref _diagGetVisualChildAtForTraversalBasePathCount);
            return base.GetVisualChildAtForTraversal(index);
        }

        if (index == baseCount && _contentElement != null)
        {
            _runtimeGetVisualChildAtForTraversalContentPathCount++;
            IncrementAggregate(ref _diagGetVisualChildAtForTraversalContentPathCount);
            return _contentElement;
        }

        _runtimeGetVisualChildAtForTraversalOutOfRangeCount++;
        IncrementAggregate(ref _diagGetVisualChildAtForTraversalOutOfRangeCount);
        throw new ArgumentOutOfRangeException(nameof(index));
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        _runtimeGetLogicalChildrenCallCount++;
        IncrementAggregate(ref _diagGetLogicalChildrenCallCount);

        foreach (var element in base.GetLogicalChildren())
        {
            _runtimeGetLogicalChildrenYieldedBaseChildCount++;
            IncrementAggregate(ref _diagGetLogicalChildrenYieldedBaseChildCount);
            yield return element;
        }

        if (_contentElement != null)
        {
            _runtimeGetLogicalChildrenYieldedContentChildCount++;
            IncrementAggregate(ref _diagGetLogicalChildrenYieldedContentChildCount);
            yield return _contentElement;
        }
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        var start = Stopwatch.GetTimestamp();
        _runtimeDependencyPropertyChangedCallCount++;
        base.OnDependencyPropertyChanged(args);

        try
        {
            if (args.Property == ContentProperty)
            {
                _runtimeDependencyPropertyChangedContentPropertyCount++;
                IncrementAggregate(ref _diagDependencyPropertyChangedContentPropertyCount);
                UpdateContentElement(args.NewValue);
            }
            else if (args.Property == ContentTemplateProperty || args.Property == ContentTemplateSelectorProperty)
            {
                _runtimeDependencyPropertyChangedTemplatePropertyCount++;
                IncrementAggregate(ref _diagDependencyPropertyChangedTemplatePropertyCount);
                UpdateContentElement(Content);
            }
            else
            {
                _runtimeDependencyPropertyChangedOtherPropertyCount++;
                IncrementAggregate(ref _diagDependencyPropertyChangedOtherPropertyCount);
            }
        }
        finally
        {
            _runtimeDependencyPropertyChangedElapsedTicks += Stopwatch.GetTimestamp() - start;
            RecordAggregateElapsed(ref _diagDependencyPropertyChangedCallCount, ref _diagDependencyPropertyChangedElapsedTicks, start);
        }
    }

    protected override void OnVisualParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        base.OnVisualParentChanged(oldParent, newParent);
        _runtimeVisualParentChangedCallCount++;
        IncrementAggregate(ref _diagVisualParentChangedCallCount);
        UpdateContentElement(Content);
    }

    protected override void OnLogicalParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        base.OnLogicalParentChanged(oldParent, newParent);
        _runtimeLogicalParentChangedCallCount++;
        IncrementAggregate(ref _diagLogicalParentChangedCallCount);
        UpdateContentElement(Content);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var start = Stopwatch.GetTimestamp();
        _runtimeMeasureOverrideCallCount++;
        try
        {
            EnsureContentElementForLayout();
            var templateSize = base.MeasureOverride(availableSize);

            if (_activeContentPresenter != null)
            {
                _runtimeMeasureOverrideActivePresenterSkipCount++;
                IncrementAggregate(ref _diagMeasureOverrideActivePresenterSkipCount);
                return templateSize;
            }

            if (_contentElement is FrameworkElement content)
            {
                _runtimeMeasureOverrideContentMeasureCount++;
                IncrementAggregate(ref _diagMeasureOverrideContentMeasureCount);
                content.Measure(availableSize);
                templateSize.X = MathF.Max(templateSize.X, content.DesiredSize.X);
                templateSize.Y = MathF.Max(templateSize.Y, content.DesiredSize.Y);
            }
            else
            {
                _runtimeMeasureOverrideNoContentCount++;
                IncrementAggregate(ref _diagMeasureOverrideNoContentCount);
            }

            return templateSize;
        }
        finally
        {
            _runtimeMeasureOverrideElapsedTicks += Stopwatch.GetTimestamp() - start;
            RecordAggregateElapsed(ref _diagMeasureOverrideCallCount, ref _diagMeasureOverrideElapsedTicks, start);
        }
    }

    protected internal override bool ShouldSuppressMeasureInvalidationFromDescendantDuringMeasure(FrameworkElement descendant)
    {
        return base.ShouldSuppressMeasureInvalidationFromDescendantDuringMeasure(descendant) ||
               ((IsMeasuring || IsArrangingOverride) && IsDescendantOfContentSubtree(descendant));
    }

    protected override bool CanReuseMeasureForAvailableSizeChange(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
    {
        _runtimeCanReuseMeasureCallCount++;
        IncrementAggregate(ref _diagCanReuseMeasureCallCount);

        if (!base.CanReuseMeasureForAvailableSizeChange(previousAvailableSize, nextAvailableSize))
        {
            _runtimeCanReuseMeasureBaseRejectedCount++;
            IncrementAggregate(ref _diagCanReuseMeasureBaseRejectedCount);
            return false;
        }

        if (_activeContentPresenter == null && _contentElement is FrameworkElement content)
        {
            _runtimeCanReuseMeasureDelegatedCount++;
            IncrementAggregate(ref _diagCanReuseMeasureDelegatedCount);
            return content.CanReuseMeasureForAvailableSizeChangeForParentLayout(previousAvailableSize, nextAvailableSize);
        }

        _runtimeCanReuseMeasureActivePresenterOrNoContentTrueCount++;
        IncrementAggregate(ref _diagCanReuseMeasureActivePresenterOrNoContentTrueCount);
        return true;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var start = Stopwatch.GetTimestamp();
        _runtimeArrangeOverrideCallCount++;
        try
        {
            EnsureContentElementForLayout();
            base.ArrangeOverride(finalSize);

            if (_activeContentPresenter != null)
            {
                _runtimeArrangeOverrideActivePresenterSkipCount++;
                IncrementAggregate(ref _diagArrangeOverrideActivePresenterSkipCount);
                return finalSize;
            }

            if (_contentElement is FrameworkElement content)
            {
                _runtimeArrangeOverrideContentArrangeCount++;
                IncrementAggregate(ref _diagArrangeOverrideContentArrangeCount);
                content.Arrange(new LayoutRect(LayoutSlot.X, LayoutSlot.Y, finalSize.X, finalSize.Y));
            }
            else
            {
                _runtimeArrangeOverrideNoContentCount++;
                IncrementAggregate(ref _diagArrangeOverrideNoContentCount);
            }

            return finalSize;
        }
        finally
        {
            _runtimeArrangeOverrideElapsedTicks += Stopwatch.GetTimestamp() - start;
            RecordAggregateElapsed(ref _diagArrangeOverrideCallCount, ref _diagArrangeOverrideElapsedTicks, start);
        }
    }

    private bool IsDescendantOfContentSubtree(UIElement descendant)
    {
        if (_contentElement == null)
        {
            return false;
        }

        for (UIElement? current = descendant; current != null; current = current.GetInvalidationParent())
        {
            if (ReferenceEquals(current, this) || ReferenceEquals(current, _contentElement))
            {
                return true;
            }
        }

        return false;
    }

    internal void AttachContentPresenter(ContentPresenter presenter)
    {
        _runtimeAttachContentPresenterCallCount++;
        IncrementAggregate(ref _diagAttachContentPresenterCallCount);

        if (ReferenceEquals(_activeContentPresenter, presenter))
        {
            _runtimeAttachContentPresenterNoOpCount++;
            IncrementAggregate(ref _diagAttachContentPresenterNoOpCount);
            return;
        }

        _activeContentPresenter = presenter;
        UpdateContentElement(Content);
        _runtimeAttachContentPresenterInvalidateMeasureCount++;
        IncrementAggregate(ref _diagAttachContentPresenterInvalidateMeasureCount);
        InvalidateMeasure();
    }

    internal void DetachContentPresenter(ContentPresenter presenter)
    {
        _runtimeDetachContentPresenterCallCount++;
        IncrementAggregate(ref _diagDetachContentPresenterCallCount);

        if (!ReferenceEquals(_activeContentPresenter, presenter))
        {
            _runtimeDetachContentPresenterIgnoredCount++;
            IncrementAggregate(ref _diagDetachContentPresenterIgnoredCount);
            return;
        }

        _activeContentPresenter = null;
        UpdateContentElement(Content);
        _runtimeDetachContentPresenterInvalidateMeasureCount++;
        IncrementAggregate(ref _diagDetachContentPresenterInvalidateMeasureCount);
        InvalidateMeasure();
    }

    internal InkkSlinger.UI.Telemetry.ContentControlRuntimeDiagnosticsSnapshot GetContentControlSnapshotForDiagnostics()
    {
        return new InkkSlinger.UI.Telemetry.ContentControlRuntimeDiagnosticsSnapshot(
            _contentElement != null,
            _contentElement?.GetType().Name ?? string.Empty,
            _activeContentPresenter != null,
            _activeContentPresenter?.GetType().Name ?? string.Empty,
            Content != null,
            Content?.GetType().Name ?? string.Empty,
            ContentTemplate != null,
            ContentTemplateSelector != null,
            this is Label,
            LayoutSlot.Width,
            LayoutSlot.Height,
            _runtimeDependencyPropertyChangedCallCount,
            TicksToMilliseconds(_runtimeDependencyPropertyChangedElapsedTicks),
            _runtimeDependencyPropertyChangedContentPropertyCount,
            _runtimeDependencyPropertyChangedTemplatePropertyCount,
            _runtimeDependencyPropertyChangedOtherPropertyCount,
            _runtimeVisualParentChangedCallCount,
            _runtimeLogicalParentChangedCallCount,
            _runtimeGetVisualChildrenCallCount,
            _runtimeGetVisualChildrenYieldedBaseChildCount,
            _runtimeGetVisualChildrenYieldedContentChildCount,
            _runtimeGetVisualChildCountForTraversalCallCount,
            _runtimeGetVisualChildCountForTraversalWithContentElementCount,
            _runtimeGetVisualChildCountForTraversalWithoutContentElementCount,
            _runtimeGetVisualChildAtForTraversalCallCount,
            _runtimeGetVisualChildAtForTraversalBasePathCount,
            _runtimeGetVisualChildAtForTraversalContentPathCount,
            _runtimeGetVisualChildAtForTraversalOutOfRangeCount,
            _runtimeGetLogicalChildrenCallCount,
            _runtimeGetLogicalChildrenYieldedBaseChildCount,
            _runtimeGetLogicalChildrenYieldedContentChildCount,
            _runtimeMeasureOverrideCallCount,
            TicksToMilliseconds(_runtimeMeasureOverrideElapsedTicks),
            _runtimeMeasureOverrideActivePresenterSkipCount,
            _runtimeMeasureOverrideContentMeasureCount,
            _runtimeMeasureOverrideNoContentCount,
            _runtimeCanReuseMeasureCallCount,
            _runtimeCanReuseMeasureBaseRejectedCount,
            _runtimeCanReuseMeasureDelegatedCount,
            _runtimeCanReuseMeasureActivePresenterOrNoContentTrueCount,
            _runtimeArrangeOverrideCallCount,
            TicksToMilliseconds(_runtimeArrangeOverrideElapsedTicks),
            _runtimeArrangeOverrideActivePresenterSkipCount,
            _runtimeArrangeOverrideContentArrangeCount,
            _runtimeArrangeOverrideNoContentCount,
            _runtimeAttachContentPresenterCallCount,
            _runtimeAttachContentPresenterNoOpCount,
            _runtimeAttachContentPresenterInvalidateMeasureCount,
            _runtimeDetachContentPresenterCallCount,
            _runtimeDetachContentPresenterIgnoredCount,
            _runtimeDetachContentPresenterInvalidateMeasureCount,
            _runtimeUpdateContentElementCallCount,
            TicksToMilliseconds(_runtimeUpdateContentElementElapsedTicks),
            _runtimeUpdateContentElementReusedExistingElementCount,
            _runtimeUpdateContentElementNullNoOpCount,
            _runtimeUpdateContentElementDetachedOldElementCount,
            _runtimeUpdateContentElementPresenterNotifyCount,
            _runtimeUpdateContentElementLabelBypassCount,
            _runtimeUpdateContentElementUiElementPathCount,
            _runtimeUpdateContentElementTemplateSelectedCount,
            _runtimeUpdateContentElementTemplateBuiltElementCount,
            _runtimeUpdateContentElementTemplateReturnedNullCount,
            _runtimeUpdateContentElementImplicitCreationSuppressedCount,
            _runtimeUpdateContentElementImplicitLabelCreatedCount,
            _runtimeUpdateContentElementNullContentTerminalCount,
            _runtimeUpdateContentElementAttachedNewElementCount);
    }

    internal new static InkkSlinger.UI.Telemetry.ContentControlTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return CreateAggregateTelemetrySnapshot(reset: false);
    }

    internal static InkkSlinger.UI.Telemetry.ContentControlTelemetrySnapshot GetTelemetrySnapshotForDiagnostics()
    {
        return GetAggregateTelemetrySnapshotForDiagnostics();
    }

    internal new static InkkSlinger.UI.Telemetry.ContentControlTelemetrySnapshot GetTelemetryAndReset()
    {
        return CreateAggregateTelemetrySnapshot(reset: true);
    }

    protected void EnsureContentElementForLayout()
    {
        if (_activeContentPresenter != null || _contentElement != null)
        {
            return;
        }

        if (Content == null && ContentTemplate == null && ContentTemplateSelector == null)
        {
            return;
        }

        _isForcingDeferredContentElementBuild = true;
        try
        {
            UpdateContentElement(Content);
        }
        finally
        {
            _isForcingDeferredContentElementBuild = false;
        }
    }

    private void UpdateContentElement(object? content)
    {
        var start = Stopwatch.GetTimestamp();
        _runtimeUpdateContentElementCallCount++;
        try
        {
        if (_activeContentPresenter == null &&
            content is UIElement existingElement &&
            ReferenceEquals(_contentElement, existingElement) &&
            ReferenceEquals(existingElement.VisualParent, this) &&
            ReferenceEquals(existingElement.LogicalParent, this))
        {
            _runtimeUpdateContentElementReusedExistingElementCount++;
            IncrementAggregate(ref _diagUpdateContentElementReusedExistingElementCount);
            return;
        }

        if (_activeContentPresenter == null && content == null && _contentElement == null)
        {
            _runtimeUpdateContentElementNullNoOpCount++;
            IncrementAggregate(ref _diagUpdateContentElementNullNoOpCount);
            return;
        }

        if (_contentElement != null)
        {
            _runtimeUpdateContentElementDetachedOldElementCount++;
            IncrementAggregate(ref _diagUpdateContentElementDetachedOldElementCount);
            _contentElement.SetVisualParent(null);
            _contentElement.SetLogicalParent(null);
            _contentElement = null;
        }

        if (_activeContentPresenter != null)
        {
            _runtimeUpdateContentElementPresenterNotifyCount++;
            IncrementAggregate(ref _diagUpdateContentElementPresenterNotifyCount);
            _activeContentPresenter.NotifyOwnerContentChanged();
            return;
        }

        if (this is Label)
        {
            _runtimeUpdateContentElementLabelBypassCount++;
            IncrementAggregate(ref _diagUpdateContentElementLabelBypassCount);
            return;
        }

        if (content is UIElement element)
        {
            _runtimeUpdateContentElementUiElementPathCount++;
            IncrementAggregate(ref _diagUpdateContentElementUiElementPathCount);
            AttachContentElement(element);
            return;
        }

        var selectedTemplate = DataTemplateResolver.ResolveTemplateForContent(
            this,
            content,
            ContentTemplate,
            ContentTemplateSelector,
            this);
        if (selectedTemplate != null)
        {
            if (!_isForcingDeferredContentElementBuild && ShouldDeferContentElementBuild(content, selectedTemplate))
            {
                return;
            }

            _runtimeUpdateContentElementTemplateSelectedCount++;
            IncrementAggregate(ref _diagUpdateContentElementTemplateSelectedCount);
            var builtElement = selectedTemplate.Build(content, this);
            if (builtElement != null)
            {
                _runtimeUpdateContentElementTemplateBuiltElementCount++;
                IncrementAggregate(ref _diagUpdateContentElementTemplateBuiltElementCount);
                AttachContentElement(builtElement);
            }
            else
            {
                _runtimeUpdateContentElementTemplateReturnedNullCount++;
                IncrementAggregate(ref _diagUpdateContentElementTemplateReturnedNullCount);
            }
            return;
        }

        if (!ShouldCreateImplicitContentElement(content, selectedTemplate))
        {
            _runtimeUpdateContentElementImplicitCreationSuppressedCount++;
            IncrementAggregate(ref _diagUpdateContentElementImplicitCreationSuppressedCount);
            return;
        }

        if (content != null)
        {
            if (!_isForcingDeferredContentElementBuild && ShouldDeferContentElementBuild(content, selectedTemplate: null))
            {
                return;
            }

            _runtimeUpdateContentElementImplicitLabelCreatedCount++;
            IncrementAggregate(ref _diagUpdateContentElementImplicitLabelCreatedCount);
            AttachContentElement(new Label
            {
                Content = content.ToString() ?? string.Empty
            });
            return;
        }

        _runtimeUpdateContentElementNullContentTerminalCount++;
        IncrementAggregate(ref _diagUpdateContentElementNullContentTerminalCount);
        }
        finally
        {
            _runtimeUpdateContentElementElapsedTicks += Stopwatch.GetTimestamp() - start;
            RecordAggregateElapsed(ref _diagUpdateContentElementCallCount, ref _diagUpdateContentElementElapsedTicks, start);
        }
    }

    private void AttachContentElement(UIElement element)
    {
        _contentElement = element;
        _contentElement.SetVisualParent(this);
        _contentElement.SetLogicalParent(this);
        _runtimeUpdateContentElementAttachedNewElementCount++;
        IncrementAggregate(ref _diagUpdateContentElementAttachedNewElementCount);
    }

    private static InkkSlinger.UI.Telemetry.ContentControlTelemetrySnapshot CreateAggregateTelemetrySnapshot(bool reset)
    {
        return new InkkSlinger.UI.Telemetry.ContentControlTelemetrySnapshot(
            ReadOrReset(ref _diagDependencyPropertyChangedCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagDependencyPropertyChangedElapsedTicks, reset)),
            ReadOrReset(ref _diagDependencyPropertyChangedContentPropertyCount, reset),
            ReadOrReset(ref _diagDependencyPropertyChangedTemplatePropertyCount, reset),
            ReadOrReset(ref _diagDependencyPropertyChangedOtherPropertyCount, reset),
            ReadOrReset(ref _diagVisualParentChangedCallCount, reset),
            ReadOrReset(ref _diagLogicalParentChangedCallCount, reset),
            ReadOrReset(ref _diagGetVisualChildrenCallCount, reset),
            ReadOrReset(ref _diagGetVisualChildrenYieldedBaseChildCount, reset),
            ReadOrReset(ref _diagGetVisualChildrenYieldedContentChildCount, reset),
            ReadOrReset(ref _diagGetVisualChildCountForTraversalCallCount, reset),
            ReadOrReset(ref _diagGetVisualChildCountForTraversalWithContentElementCount, reset),
            ReadOrReset(ref _diagGetVisualChildCountForTraversalWithoutContentElementCount, reset),
            ReadOrReset(ref _diagGetVisualChildAtForTraversalCallCount, reset),
            ReadOrReset(ref _diagGetVisualChildAtForTraversalBasePathCount, reset),
            ReadOrReset(ref _diagGetVisualChildAtForTraversalContentPathCount, reset),
            ReadOrReset(ref _diagGetVisualChildAtForTraversalOutOfRangeCount, reset),
            ReadOrReset(ref _diagGetLogicalChildrenCallCount, reset),
            ReadOrReset(ref _diagGetLogicalChildrenYieldedBaseChildCount, reset),
            ReadOrReset(ref _diagGetLogicalChildrenYieldedContentChildCount, reset),
            ReadOrReset(ref _diagMeasureOverrideCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagMeasureOverrideElapsedTicks, reset)),
            ReadOrReset(ref _diagMeasureOverrideActivePresenterSkipCount, reset),
            ReadOrReset(ref _diagMeasureOverrideContentMeasureCount, reset),
            ReadOrReset(ref _diagMeasureOverrideNoContentCount, reset),
            ReadOrReset(ref _diagCanReuseMeasureCallCount, reset),
            ReadOrReset(ref _diagCanReuseMeasureBaseRejectedCount, reset),
            ReadOrReset(ref _diagCanReuseMeasureDelegatedCount, reset),
            ReadOrReset(ref _diagCanReuseMeasureActivePresenterOrNoContentTrueCount, reset),
            ReadOrReset(ref _diagArrangeOverrideCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagArrangeOverrideElapsedTicks, reset)),
            ReadOrReset(ref _diagArrangeOverrideActivePresenterSkipCount, reset),
            ReadOrReset(ref _diagArrangeOverrideContentArrangeCount, reset),
            ReadOrReset(ref _diagArrangeOverrideNoContentCount, reset),
            ReadOrReset(ref _diagAttachContentPresenterCallCount, reset),
            ReadOrReset(ref _diagAttachContentPresenterNoOpCount, reset),
            ReadOrReset(ref _diagAttachContentPresenterInvalidateMeasureCount, reset),
            ReadOrReset(ref _diagDetachContentPresenterCallCount, reset),
            ReadOrReset(ref _diagDetachContentPresenterIgnoredCount, reset),
            ReadOrReset(ref _diagDetachContentPresenterInvalidateMeasureCount, reset),
            ReadOrReset(ref _diagUpdateContentElementCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagUpdateContentElementElapsedTicks, reset)),
            ReadOrReset(ref _diagUpdateContentElementReusedExistingElementCount, reset),
            ReadOrReset(ref _diagUpdateContentElementNullNoOpCount, reset),
            ReadOrReset(ref _diagUpdateContentElementDetachedOldElementCount, reset),
            ReadOrReset(ref _diagUpdateContentElementPresenterNotifyCount, reset),
            ReadOrReset(ref _diagUpdateContentElementLabelBypassCount, reset),
            ReadOrReset(ref _diagUpdateContentElementUiElementPathCount, reset),
            ReadOrReset(ref _diagUpdateContentElementTemplateSelectedCount, reset),
            ReadOrReset(ref _diagUpdateContentElementTemplateBuiltElementCount, reset),
            ReadOrReset(ref _diagUpdateContentElementTemplateReturnedNullCount, reset),
            ReadOrReset(ref _diagUpdateContentElementImplicitCreationSuppressedCount, reset),
            ReadOrReset(ref _diagUpdateContentElementImplicitLabelCreatedCount, reset),
            ReadOrReset(ref _diagUpdateContentElementNullContentTerminalCount, reset),
            ReadOrReset(ref _diagUpdateContentElementAttachedNewElementCount, reset));
    }

    private static void IncrementAggregate(ref long counter)
    {
        Interlocked.Increment(ref counter);
    }

    private static void AddAggregate(ref long counter, long value)
    {
        Interlocked.Add(ref counter, value);
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

    private static void RecordAggregateElapsed(ref long callCount, ref long elapsedTicks, long start)
    {
        IncrementAggregate(ref callCount);
        AddAggregate(ref elapsedTicks, Stopwatch.GetTimestamp() - start);
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }
}
