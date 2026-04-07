using System.Linq;
using System.Threading.Tasks;
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

    [Fact]
    public async Task DiagnosticsPipeline_Emits_ContentControlContributorFacts()
    {
        _ = ContentControl.GetTelemetryAndReset();

        var root = new Canvas { Name = "Root", Width = 400f, Height = 240f };
        var host = new ProbeContentControl
        {
            Name = "Probe",
            Width = 180f,
            Height = 64f,
            ContentTemplate = new DataTemplate(static item => new TextBlock
            {
                Text = $"tpl:{item}"
            })
        };
        host.Content = "Alpha";
        root.AddChild(host);

        using var inkkOopsHost = new InkkOopsTestHost(root);
        await inkkOopsHost.AdvanceFrameAsync(1);

        _ = host.GetVisualChildren().ToList();
        _ = host.GetLogicalChildren().ToList();
        _ = host.GetVisualChildCountForTraversal();
        _ = host.GetVisualChildAtForTraversal(0);

        var diagnostics = new InkkOopsVisualTreeDiagnostics([
            new InkkOopsGenericElementDiagnosticsContributor(),
            new InkkOopsFrameworkElementDiagnosticsContributor(),
            new InkkOopsContentControlDiagnosticsContributor()
        ]);

        var snapshot = diagnostics.Capture(
            root,
            new InkkOopsDiagnosticsContext
            {
                UiRoot = inkkOopsHost.UiRoot,
                Viewport = inkkOopsHost.GetViewportBounds(),
                HoveredElement = host,
                FocusedElement = null,
                ArtifactName = "contentcontrol"
            });
        var text = new DefaultInkkOopsDiagnosticsSerializer().SerializeVisualTree(snapshot);

        Assert.Contains("ProbeContentControl#Probe", text);
        Assert.Contains("contentControlHasContentElement=True", text);
        Assert.Contains("contentControlContentElementType=TextBlock", text);
        Assert.Contains("contentControlHasContent=True", text);
        Assert.Contains("contentControlHasContentTemplate=True", text);
        Assert.Contains("contentControlRuntimeMeasureOverrideCalls=", text);
        Assert.Contains("contentControlRuntimeUpdateContentElementTemplateSelected=", text);
        Assert.Contains("contentControlVisualChildrenCalls=", text);
        Assert.Contains("contentControlMeasureOverrideCalls=", text);
        Assert.Contains("contentControlUpdateContentElementTemplateSelected=", text);
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

    [Fact]
    public async Task DiagnosticsPipeline_Emits_ControlContributorFacts()
    {
        _ = Control.GetTelemetryAndReset();

        var root = new Canvas { Name = "Root", Width = 400f, Height = 240f };
        var control = new ProbeControl
        {
            Name = "Probe",
            Width = 180f,
            Height = 64f,
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
        root.AddChild(control);

        using var host = new InkkOopsTestHost(root);
        await host.AdvanceFrameAsync(1);

        _ = control.GetVisualChildren().ToList();
        _ = control.GetVisualChildCountForTraversal();
        _ = control.GetVisualChildAtForTraversal(0);

        var canExecute = false;
        var command = new CallbackCommand(_ => { }, _ => canExecute);
        control.Command = command;
        control.CommandParameter = "payload";
        control.CommandTarget = new Border();
        canExecute = true;
        command.RaiseCanExecuteChanged();

        var diagnostics = new InkkOopsVisualTreeDiagnostics([
            new InkkOopsGenericElementDiagnosticsContributor(),
            new InkkOopsFrameworkElementDiagnosticsContributor(),
            new InkkOopsControlDiagnosticsContributor()
        ]);

        var snapshot = diagnostics.Capture(
            root,
            new InkkOopsDiagnosticsContext
            {
                UiRoot = host.UiRoot,
                Viewport = host.GetViewportBounds(),
                HoveredElement = control,
                FocusedElement = null,
                ArtifactName = "control"
            });
        var text = new DefaultInkkOopsDiagnosticsSerializer().SerializeVisualTree(snapshot);

        Assert.Contains("ProbeControl#Probe", text);
        Assert.Contains("controlHasTemplateAssigned=True", text);
        Assert.Contains("controlHasTemplateRoot=True", text);
        Assert.Contains("controlTemplateRootType=Border", text);
        Assert.Contains("controlRuntimeApplyTemplateCalls=", text);
        Assert.Contains("controlRuntimeMeasureOverrideCalls=", text);
        Assert.Contains("controlRuntimeRefreshCommandSubscriptionsCalls=", text);
        Assert.Contains("controlApplyTemplateCalls=", text);
        Assert.Contains("controlMeasureOverrideCalls=", text);
        Assert.Contains("controlRefreshCommandSubscriptionsCalls=", text);
    }

    private sealed class ProbeControl : Control
    {
        public bool InvokeCanReuseMeasure(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
        {
            return CanReuseMeasureForAvailableSizeChange(previousAvailableSize, nextAvailableSize);
        }
    }
}
