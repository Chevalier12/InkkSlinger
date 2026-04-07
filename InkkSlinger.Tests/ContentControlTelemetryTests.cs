using System.Linq;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ContentControlTelemetryTests
{
    [Fact]
    public void ContentControl_RuntimeTelemetry_CapturesImplicitUiElementTemplateAndPresenterPaths()
    {
        _ = ContentControl.GetTelemetryAndReset();

        var host = new ProbeContentControl();

        host.Content = "Alpha";
        host.Measure(new Vector2(240f, 40f));
        host.Arrange(new LayoutRect(0f, 0f, 240f, 40f));

        host.Content = new Border { Width = 40f, Height = 18f };
        _ = host.InvokeCanReuseMeasure(new Vector2(240f, 40f), new Vector2(240f, 40f));
        host.Measure(new Vector2(240f, 40f));
        host.Arrange(new LayoutRect(0f, 0f, 240f, 40f));

        host.ContentTemplate = new DataTemplate(static item => new TextBlock
        {
            Text = $"tpl:{item}"
        });
        host.Content = "Beta";
        host.Measure(new Vector2(240f, 40f));
        host.Arrange(new LayoutRect(0f, 0f, 240f, 40f));

        var presenter = new ContentPresenter();
        host.AttachContentPresenter(presenter);
        host.Content = "Gamma";
        host.Measure(new Vector2(240f, 40f));
        host.Arrange(new LayoutRect(0f, 0f, 240f, 40f));
        host.DetachContentPresenter(presenter);
        _ = host.GetVisualChildren().ToList();
        _ = host.GetLogicalChildren().ToList();

        var runtime = host.GetContentControlSnapshotForDiagnostics();
        Assert.True(runtime.HasContentElement);
        Assert.Equal(nameof(TextBlock), runtime.ContentElementType);
        Assert.True(runtime.HasContent);
        Assert.Equal(nameof(String), runtime.ContentType);
        Assert.True(runtime.HasContentTemplate);
        Assert.False(runtime.HasActiveContentPresenter);
        Assert.True(runtime.DependencyPropertyChangedContentPropertyCount >= 3);
        Assert.True(runtime.DependencyPropertyChangedTemplatePropertyCount >= 1);
        Assert.True(runtime.GetVisualChildrenYieldedContentChildCount >= 1);
        Assert.True(runtime.MeasureOverrideContentMeasureCount >= 1);
        Assert.True(runtime.CanReuseMeasureCallCount >= 1);
        Assert.True(
            runtime.CanReuseMeasureBaseRejectedCount +
            runtime.CanReuseMeasureDelegatedCount +
            runtime.CanReuseMeasureActivePresenterOrNoContentTrueCount >= 1);
        Assert.True(runtime.ArrangeOverrideContentArrangeCount >= 1);
        Assert.True(runtime.AttachContentPresenterCallCount >= 1);
        Assert.True(runtime.DetachContentPresenterCallCount >= 1);
        Assert.True(runtime.UpdateContentElementUiElementPathCount >= 1);
        Assert.True(runtime.UpdateContentElementTemplateSelectedCount >= 1);
        Assert.True(runtime.UpdateContentElementTemplateBuiltElementCount >= 1);
        Assert.True(runtime.UpdateContentElementImplicitLabelCreatedCount >= 1);
        Assert.True(runtime.UpdateContentElementPresenterNotifyCount >= 2);
        Assert.True(runtime.UpdateContentElementDetachedOldElementCount >= 2);
        Assert.True(runtime.UpdateContentElementAttachedNewElementCount >= 3);

        var aggregate = ContentControl.GetTelemetryAndReset();
        Assert.True(aggregate.MeasureOverrideCallCount >= 1);
        Assert.True(aggregate.ArrangeOverrideCallCount >= 1);
        Assert.True(
            aggregate.CanReuseMeasureBaseRejectedCount +
            aggregate.CanReuseMeasureDelegatedCount +
            aggregate.CanReuseMeasureActivePresenterOrNoContentTrueCount >= 1);
        Assert.True(aggregate.UpdateContentElementTemplateSelectedCount >= 1);
        Assert.True(aggregate.UpdateContentElementImplicitLabelCreatedCount >= 1);

        var cleared = ContentControl.GetTelemetryAndReset();
        Assert.Equal(0, cleared.MeasureOverrideCallCount);
        Assert.Equal(0, cleared.ArrangeOverrideCallCount);
        Assert.Equal(0, cleared.UpdateContentElementCallCount);
    }

    [Fact]
    public void ContentControl_Telemetry_CapturesLabelBypassAndImplicitSuppression()
    {
        _ = ContentControl.GetTelemetryAndReset();

        var label = new Label
        {
            Content = "Plain text"
        };

        var suppressingHost = new NoImplicitContentControl
        {
            Content = "Blocked"
        };

        var labelRuntime = label.GetContentControlSnapshotForDiagnostics();
        var suppressingRuntime = suppressingHost.GetContentControlSnapshotForDiagnostics();

        Assert.True(labelRuntime.IsLabelInstance);
        Assert.False(labelRuntime.HasContentElement);
        Assert.True(labelRuntime.UpdateContentElementLabelBypassCount >= 1);
        Assert.False(suppressingRuntime.HasContentElement);
        Assert.True(suppressingRuntime.UpdateContentElementImplicitCreationSuppressedCount >= 1);

        var aggregate = ContentControl.GetTelemetryAndReset();
        Assert.True(aggregate.UpdateContentElementLabelBypassCount >= 1);
        Assert.True(aggregate.UpdateContentElementImplicitCreationSuppressedCount >= 1);
        Assert.True(aggregate.UpdateContentElementCallCount >= 2);
    }

    private sealed class ProbeContentControl : ContentControl
    {
        public bool InvokeCanReuseMeasure(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
        {
            return CanReuseMeasureForAvailableSizeChange(previousAvailableSize, nextAvailableSize);
        }
    }

    private sealed class NoImplicitContentControl : ContentControl
    {
        protected override bool ShouldCreateImplicitContentElement(object? content, DataTemplate? selectedTemplate)
        {
            _ = content;
            _ = selectedTemplate;
            return false;
        }
    }
}

public sealed class ControlTelemetryTests
{
    [Fact]
    public void Control_RuntimeTelemetry_CapturesTemplateTraversalAndTemplateLifecycle()
    {
        _ = Control.GetTelemetryAndReset();

        var host = new ProbeControl
        {
            Template = new ControlTemplate(_ => new Border
            {
                Name = "TemplateRoot",
                Width = 44f,
                Height = 18f
            })
            {
                TargetType = typeof(ProbeControl)
            }
        };

        host.Measure(new Vector2(240f, 40f));
        host.Arrange(new LayoutRect(0f, 0f, 240f, 40f));
        _ = host.InvokeCanReuseMeasure(new Vector2(240f, 40f), new Vector2(240f, 40f));

        var visualChildren = host.GetVisualChildren().ToList();
        var traversalCount = host.GetVisualChildCountForTraversal();
        var traversedChild = host.GetVisualChildAtForTraversal(0);
        Assert.Throws<ArgumentOutOfRangeException>(() => host.GetVisualChildAtForTraversal(1));

        var noTemplate = new ProbeControl();
        Assert.False(noTemplate.ApplyTemplate());

        var nullBuild = new ProbeControl
        {
            Template = new ControlTemplate(static _ => (UIElement)null!)
            {
                TargetType = typeof(ProbeControl)
            }
        };
        Assert.False(nullBuild.ApplyTemplate());

        var wrongTarget = new ProbeControl();
        Assert.Throws<InvalidOperationException>(() => wrongTarget.Template = new ControlTemplate(_ => new Border())
        {
            TargetType = typeof(Label)
        });

        var runtime = host.GetControlSnapshotForDiagnostics();

        Assert.True(runtime.HasTemplateAssigned);
        Assert.True(runtime.HasTemplateRoot);
        Assert.Equal(nameof(Border), runtime.TemplateRootType);
        Assert.Single(visualChildren);
        Assert.Equal(1, traversalCount);
        Assert.IsType<Border>(traversedChild);
        Assert.True(runtime.GetVisualChildrenYieldedTemplateRootCount >= 1);
        Assert.True(runtime.GetVisualChildCountForTraversalWithTemplateRootCount >= 1);
        Assert.True(runtime.GetVisualChildAtForTraversalTemplateRootPathCount >= 1);
        Assert.True(runtime.GetVisualChildAtForTraversalOutOfRangeCount >= 1);
        Assert.True(runtime.ApplyTemplateCallCount >= 1);
        Assert.True(runtime.ApplyTemplateSetTemplateTreeCount >= 1);
        Assert.True(runtime.ApplyTemplateBindingsAppliedCount >= 1);
        Assert.True(runtime.ApplyTemplateTriggersAppliedCount >= 1);
        Assert.True(runtime.ApplyTemplateValidationCount >= 1);
        Assert.True(runtime.ApplyTemplateOnApplyTemplateCount >= 1);
        Assert.True(runtime.ApplyTemplateReturnedTrueCount >= 1);
        Assert.True(runtime.MeasureOverrideCallCount >= 1);
        Assert.True(runtime.MeasureOverrideTemplateRootMeasureCount >= 1);
        Assert.True(runtime.CanReuseMeasureCallCount >= 1);
        Assert.True(runtime.CanReuseMeasureTemplateRootDelegatedCount >= 1);
        Assert.True(runtime.ArrangeOverrideCallCount >= 1);
        Assert.True(runtime.ArrangeOverrideTemplateRootArrangeCount >= 1);

        var aggregate = Control.GetTelemetryAndReset();

        Assert.True(aggregate.GetVisualChildrenYieldedTemplateRootCount >= 1);
        Assert.True(aggregate.GetVisualChildCountForTraversalWithTemplateRootCount >= 1);
        Assert.True(aggregate.GetVisualChildAtForTraversalOutOfRangeCount >= 1);
        Assert.True(aggregate.ApplyTemplateCallCount >= 4);
        Assert.True(aggregate.ApplyTemplateTemplateNullCount >= 1);
        Assert.True(aggregate.ApplyTemplateBuildReturnedNullCount >= 1);
        Assert.True(aggregate.ApplyTemplateTargetTypeMismatchCount >= 1);
        Assert.True(aggregate.ApplyTemplateReturnedTrueCount >= 1);
        Assert.True(aggregate.ApplyTemplateReturnedFalseCount >= 2);
        Assert.True(aggregate.MeasureOverrideTemplateRootMeasureCount >= 1);
        Assert.True(aggregate.CanReuseMeasureTemplateRootDelegatedCount >= 1);
        Assert.True(aggregate.ArrangeOverrideTemplateRootArrangeCount >= 1);

        var cleared = Control.GetTelemetryAndReset();

        Assert.Equal(0, cleared.ApplyTemplateCallCount);
        Assert.Equal(0, cleared.MeasureOverrideCallCount);
        Assert.Equal(0, cleared.ArrangeOverrideCallCount);
    }

    [Fact]
    public void Control_Telemetry_CapturesCommandSubscriptionAndEnableStateTransitions()
    {
        _ = Control.GetTelemetryAndReset();

        var host = new ProbeControl
        {
            IsEnabled = true
        };

        var oldCommand = new CallbackCommand(_ => { });
        host.Command = oldCommand;

        var canExecute = false;
        var newCommand = new CallbackCommand(_ => { }, _ => canExecute);

        host.Command = newCommand;
        host.CommandParameter = "payload";
        host.CommandTarget = new Border();

        Assert.False(host.IsEnabled);

        host.IsEnabled = true;
        Assert.False(host.IsEnabled);

        canExecute = true;
        newCommand.RaiseCanExecuteChanged();
        Assert.True(host.IsEnabled);

        host.Command = null;
        Assert.True(host.IsEnabled);

        var runtime = host.GetControlSnapshotForDiagnostics();

        Assert.False(runtime.HasSubscribedCommand);
        Assert.False(runtime.IsCommandDisablingIsEnabled);
        Assert.False(runtime.HasStoredIsEnabledLocalValue);
        Assert.True(runtime.RefreshCommandSubscriptionsCallCount >= 3);
        Assert.True(runtime.RefreshCommandSubscriptionsDetachedOldCommandCount >= 2);
        Assert.True(runtime.RefreshCommandSubscriptionsAttachedNewCommandCount >= 2);
        Assert.True(runtime.UpdateCommandEnabledStateCallCount >= 5);
        Assert.True(runtime.UpdateCommandEnabledStateNoCommandRestoreCount >= 1);
        Assert.True(runtime.UpdateCommandEnabledStateCanExecuteRestoreCount >= 1);
        Assert.True(runtime.UpdateCommandEnabledStateDisableCommandCount >= 1);
        Assert.True(runtime.RestoreIsEnabledIfCommandDisabledItRestoreStoredValueCount >= 1);
        Assert.True(runtime.DependencyPropertyChangedCommandPropertyCount >= 3);
        Assert.True(runtime.DependencyPropertyChangedCommandStatePropertyCount >= 2);
        Assert.True(runtime.DependencyPropertyChangedIsEnabledPropertyCount >= 1);

        var aggregate = Control.GetTelemetryAndReset();

        Assert.True(aggregate.RefreshCommandSubscriptionsCallCount >= 3);
        Assert.True(aggregate.RefreshCommandSubscriptionsDetachedOldCommandCount >= 2);
        Assert.True(aggregate.RefreshCommandSubscriptionsAttachedNewCommandCount >= 2);
        Assert.True(aggregate.UpdateCommandEnabledStateNoCommandRestoreCount >= 1);
        Assert.True(aggregate.UpdateCommandEnabledStateCanExecuteRestoreCount >= 1);
        Assert.True(aggregate.UpdateCommandEnabledStateDisableCommandCount >= 1);
        Assert.True(aggregate.RestoreIsEnabledIfCommandDisabledItRestoreStoredValueCount >= 1);
        Assert.True(aggregate.DependencyPropertyChangedCommandPropertyCount >= 3);
        Assert.True(aggregate.DependencyPropertyChangedCommandStatePropertyCount >= 2);

        var cleared = Control.GetTelemetryAndReset();

        Assert.Equal(0, cleared.RefreshCommandSubscriptionsCallCount);
        Assert.Equal(0, cleared.UpdateCommandEnabledStateCallCount);
        Assert.Equal(0, cleared.DependencyPropertyChangedCallCount);
    }

    private sealed class ProbeControl : Control
    {
        public bool InvokeCanReuseMeasure(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
        {
            return CanReuseMeasureForAvailableSizeChange(previousAvailableSize, nextAvailableSize);
        }
    }
}