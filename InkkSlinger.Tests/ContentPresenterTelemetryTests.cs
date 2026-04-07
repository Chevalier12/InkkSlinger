using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ContentPresenterTelemetryTests
{
    [Fact]
    public void ContentPresenter_RuntimeTelemetry_CapturesOwnerFallbackRefreshAndAggregateReset()
    {
        _ = ContentPresenter.GetTelemetryAndReset();

        var owner = new Button
        {
            Content = "Alpha"
        };
        var presenter = new ContentPresenter();

        AttachParent(presenter, owner);
        presenter.Measure(new Vector2(240f, 40f));
        presenter.Arrange(new LayoutRect(0f, 0f, 240f, 40f));

        owner.Content = "Beta";
        presenter.Measure(new Vector2(240f, 40f));
        presenter.Arrange(new LayoutRect(0f, 0f, 240f, 40f));
        owner.Foreground = new Color(255, 140, 0);

        var runtime = presenter.GetContentPresenterSnapshotForDiagnostics();
        Assert.True(runtime.HasPresentedElement);
        Assert.Equal(nameof(Label), runtime.PresentedElementType);
        Assert.True(runtime.HasSourceOwner);
        Assert.Equal(nameof(Button), runtime.SourceOwnerType);
        Assert.True(runtime.EnsureSourceBindingCallCount >= 1);
        Assert.True(runtime.FindSourceOwnerFoundCount >= 1);
        Assert.True(runtime.RefreshPresentedElementChangedCount >= 1);
        Assert.True(runtime.BuildContentElementLabelPathCount >= 1);
        Assert.True(runtime.OnSourceOwnerPropertyChangedCallCount >= 2);
        Assert.True(runtime.OnSourceOwnerPropertyChangedRebuiltPresentedElementCount >= 1);
        Assert.True(runtime.OnSourceOwnerPropertyChangedRefreshedFallbackTextCount >= 1);
        Assert.True(runtime.TryRefreshFallbackTextStylingLabelPathCount >= 1);
        Assert.True(runtime.MeasureOverrideCallCount >= 1);
        Assert.True(runtime.ArrangeOverrideCallCount >= 1);

        var aggregate = ContentPresenter.GetAggregateTelemetrySnapshotForDiagnostics();
        Assert.True(aggregate.MeasureOverrideCallCount >= 1);
        Assert.True(aggregate.ArrangeOverrideCallCount >= 1);
        Assert.True(aggregate.BuildContentElementLabelPathCount >= 1);
        Assert.True(aggregate.FindSourceOwnerFoundCount >= 1);
        Assert.True(aggregate.TryRefreshFallbackTextStylingLabelPathCount >= 1);

        var reset = ContentPresenter.GetTelemetryAndReset();
        Assert.True(reset.MeasureOverrideCallCount >= 1);
        Assert.True(reset.BuildContentElementLabelPathCount >= 1);

        var cleared = ContentPresenter.GetTelemetryAndReset();
        Assert.Equal(0, cleared.MeasureOverrideCallCount);
        Assert.Equal(0, cleared.ArrangeOverrideCallCount);
        Assert.Equal(0, cleared.BuildContentElementLabelPathCount);
        Assert.Equal(0, cleared.FindSourceOwnerFoundCount);
    }

    [Fact]
    public void ContentPresenter_Telemetry_CapturesCycleGuardPath()
    {
        _ = ContentPresenter.GetTelemetryAndReset();

        var presenter = new ContentPresenter
        {
            Content = null
        };

        presenter.Content = presenter;
        presenter.Measure(new Vector2(180f, 48f));
        presenter.Arrange(new LayoutRect(0f, 0f, 180f, 48f));

        var runtime = presenter.GetContentPresenterSnapshotForDiagnostics();
        Assert.True(runtime.HasPresentedElement);
        Assert.Equal(nameof(Label), runtime.PresentedElementType);
        Assert.True(runtime.BuildContentElementUiElementPathCount >= 1);
        Assert.True(runtime.BuildContentElementCycleGuardCount >= 1);
        Assert.True(runtime.WouldCreatePresentationCycleSelfCount >= 1);

        var presented = Assert.IsType<Label>(Assert.Single(presenter.GetVisualChildren()));
        Assert.Contains("cycle guard", presented.Content?.ToString(), System.StringComparison.OrdinalIgnoreCase);

        var aggregate = ContentPresenter.GetTelemetryAndReset();
        Assert.True(aggregate.BuildContentElementUiElementPathCount >= 1);
        Assert.True(aggregate.BuildContentElementCycleGuardCount >= 1);
        Assert.True(aggregate.WouldCreatePresentationCycleSelfCount >= 1);
    }

    [Fact]
    public async Task DiagnosticsPipeline_Emits_ContentPresenterContributorFacts()
    {
        _ = ContentPresenter.GetTelemetryAndReset();

        var root = new Canvas { Name = "Root", Width = 400f, Height = 240f };
        var presenter = new ContentPresenter
        {
            Name = "Probe",
            Width = 180f,
            Height = 64f,
            Content = "Alpha"
        };
        root.AddChild(presenter);

        using var host = new InkkOopsTestHost(root);
        await host.AdvanceFrameAsync(1);

        _ = presenter.GetVisualChildren().ToList();
        _ = presenter.GetLogicalChildren().ToList();
        _ = presenter.GetVisualChildCountForTraversal();
        _ = presenter.GetVisualChildAtForTraversal(0);

        var diagnostics = new InkkOopsVisualTreeDiagnostics([
            new InkkOopsGenericElementDiagnosticsContributor(),
            new InkkOopsFrameworkElementDiagnosticsContributor(),
            new InkkOopsContentPresenterDiagnosticsContributor()
        ]);

        var snapshot = diagnostics.Capture(
            root,
            new InkkOopsDiagnosticsContext
            {
                UiRoot = host.UiRoot,
                Viewport = host.GetViewportBounds(),
                HoveredElement = presenter,
                FocusedElement = null,
                ArtifactName = "contentpresenter"
            });
        var text = new DefaultInkkOopsDiagnosticsSerializer().SerializeVisualTree(snapshot);

        Assert.Contains("ContentPresenter#Probe", text);
        Assert.Contains("contentPresenterHasPresentedElement=True", text);
        Assert.Contains("contentPresenterPresentedElementType=Label", text);
        Assert.Contains("contentPresenterHasLocalContent=True", text);
        Assert.Contains("contentPresenterRuntimeMeasureOverrideCalls=", text);
        Assert.Contains("contentPresenterRuntimeBuildContentElementLabelPath=", text);
        Assert.Contains("contentPresenterVisualChildrenCalls=", text);
        Assert.Contains("contentPresenterMeasureOverrideCalls=", text);
        Assert.Contains("contentPresenterBuildContentElementLabelPath=", text);
        Assert.Contains("contentPresenterFindReadablePropertyCalls=", text);
    }

    private static void AttachParent(UIElement child, UIElement parent)
    {
        var visualParentMethod = typeof(UIElement).GetMethod("SetVisualParent", BindingFlags.Instance | BindingFlags.NonPublic);
        var logicalParentMethod = typeof(UIElement).GetMethod("SetLogicalParent", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(visualParentMethod);
        Assert.NotNull(logicalParentMethod);

        _ = visualParentMethod!.Invoke(child, new object?[] { parent });
        _ = logicalParentMethod!.Invoke(child, new object?[] { parent });
    }
}
