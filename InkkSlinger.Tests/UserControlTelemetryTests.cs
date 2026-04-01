using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class UserControlTelemetryTests
{
    [Fact]
    public void RuntimeTelemetry_CapturesTemplateState_AndFilteredTraversalBehavior()
    {
        _ = UserControl.GetTelemetryAndReset();

        var payload = new Border
        {
            Width = 36f,
            Height = 18f
        };

        var userControl = new UserControl
        {
            Width = 140f,
            Height = 90f,
            BorderThickness = new Thickness(2f, 3f, 4f, 5f),
            Padding = new Thickness(6f, 7f, 8f, 9f),
            Content = payload,
            Template = new ControlTemplate(_ => new Border { Name = "TemplateRoot" })
            {
                TargetType = typeof(UserControl)
            }
        };

        RunLayout(userControl, 320, 200);
        var visualChildren = userControl.GetVisualChildren().ToList();
        var logicalChildren = userControl.GetLogicalChildren().ToList();
        var traversalCount = userControl.GetVisualChildCountForTraversal();
        var traversedChild = userControl.GetVisualChildAtForTraversal(0);
        Assert.Throws<ArgumentOutOfRangeException>(() => userControl.GetVisualChildAtForTraversal(1));

        var snapshot = userControl.GetUserControlSnapshotForDiagnostics();

        Assert.True(snapshot.HasTemplateAssigned);
        Assert.True(snapshot.HasTemplateRoot);
        Assert.True(snapshot.HasCachedTemplateRoot);
        Assert.Equal(nameof(Border), snapshot.CachedTemplateRootType);
        Assert.True(snapshot.HasContentElement);
        Assert.Equal(nameof(Border), snapshot.ContentElementType);
        Assert.Single(visualChildren);
        Assert.Empty(logicalChildren);
        Assert.Equal(1, traversalCount);
        Assert.IsType<Border>(traversedChild);
        Assert.True(snapshot.DependencyPropertyChangedTemplatePropertyCount > 0);
        Assert.True(snapshot.GetVisualChildrenTemplatePathCount > 0);
        Assert.True(snapshot.GetVisualChildrenFilteredContentCount > 0);
        Assert.True(snapshot.GetVisualChildCountForTraversalTemplatePathCount > 0);
        Assert.True(snapshot.GetVisualChildCountForTraversalFilteredContentCount > 0);
        Assert.True(snapshot.GetVisualChildAtForTraversalTemplatePathCount > 0);
        Assert.True(snapshot.GetVisualChildAtForTraversalFilteredContentCount > 0);
        Assert.True(snapshot.GetVisualChildAtForTraversalOutOfRangeCount > 0);
        Assert.True(snapshot.GetLogicalChildrenTemplatePathCount > 0);
        Assert.True(snapshot.GetLogicalChildrenFilteredContentCount > 0);
        Assert.True(snapshot.MeasureOverrideTemplatePathCount > 0);
        Assert.True(snapshot.MeasureOverrideTemplateRootMeasureCount > 0);
        Assert.True(snapshot.ArrangeOverrideTemplatePathCount > 0);
        Assert.True(snapshot.ArrangeOverrideTemplateRootArrangeCount > 0);
        Assert.True(snapshot.EnsureTemplateAppliedIfNeededCallCount > 0);
        Assert.True(snapshot.EnsureTemplateAppliedApplyTemplateCount > 0 ||
                snapshot.EnsureTemplateAppliedRefreshCachedRootCount > 0 ||
                snapshot.EnsureTemplateAppliedNoOpCount > 0);
        Assert.True(snapshot.RefreshCachedTemplateRootCallCount > 0);
        Assert.True(snapshot.RefreshCachedTemplateRootHitCount > 0);
    }

    [Fact]
    public void AggregateTelemetry_CapturesTemplateTransitions_AndResets()
    {
        _ = UserControl.GetTelemetryAndReset();

        var templatedWithPresenter = new UserControl
        {
            Width = 150f,
            Height = 100f,
            Content = new Border { Width = 30f, Height = 20f },
            Template = new ControlTemplate(_ =>
            {
                var root = new Border();
                root.Child = new ContentPresenter();
                return root;
            })
            {
                TargetType = typeof(UserControl)
            }
        };

        RunLayout(templatedWithPresenter, 320, 200);
        _ = templatedWithPresenter.GetVisualChildren().ToList();
        _ = templatedWithPresenter.GetLogicalChildren().ToList();
        templatedWithPresenter.Template = null;
        RunLayout(templatedWithPresenter, 320, 200);

        var templatedWithoutPresenter = new UserControl
        {
            Width = 150f,
            Height = 100f,
            BorderThickness = new Thickness(1f),
            Padding = new Thickness(2f),
            Content = new Border { Width = 40f, Height = 24f },
            Template = new ControlTemplate(_ => new Border())
            {
                TargetType = typeof(UserControl)
            }
        };

        RunLayout(templatedWithoutPresenter, 320, 200);
        _ = templatedWithoutPresenter.GetVisualChildren().ToList();
        _ = templatedWithoutPresenter.GetLogicalChildren().ToList();
        _ = templatedWithoutPresenter.GetVisualChildCountForTraversal();
        _ = templatedWithoutPresenter.GetVisualChildAtForTraversal(0);
        Assert.Throws<ArgumentOutOfRangeException>(() => templatedWithoutPresenter.GetVisualChildAtForTraversal(1));

        var nonTemplated = new UserControl
        {
            Width = 120f,
            Height = 80f,
            BorderThickness = new Thickness(2f, 3f, 4f, 5f),
            Padding = new Thickness(6f, 7f, 8f, 9f),
            Content = new Border { Width = 50f, Height = 28f }
        };

        RunLayout(nonTemplated, 320, 200);
        _ = nonTemplated.GetVisualChildren().ToList();
        _ = nonTemplated.GetLogicalChildren().ToList();
        _ = nonTemplated.GetVisualChildCountForTraversal();
        _ = nonTemplated.GetVisualChildAtForTraversal(0);

        var aggregate = UserControl.GetTelemetryAndReset();

        Assert.True(aggregate.DependencyPropertyChangedTemplatePropertyCount > 0);
        Assert.True(aggregate.DependencyPropertyChangedTemplateDetachCount > 0);
        Assert.True(aggregate.DependencyPropertyChangedTemplateCacheClearCount > 0);
        Assert.True(aggregate.DependencyPropertyChangedTemplateRefreshCount > 0);
        Assert.True(aggregate.GetVisualChildrenTemplatePathCount > 0);
        Assert.True(aggregate.GetVisualChildrenNonTemplatePathCount > 0);
        Assert.True(aggregate.GetVisualChildrenFilteredContentCount > 0);
        Assert.True(aggregate.GetVisualChildCountForTraversalTemplatePathCount > 0);
        Assert.True(aggregate.GetVisualChildCountForTraversalNonTemplatePathCount > 0);
        Assert.True(aggregate.GetVisualChildAtForTraversalTemplatePathCount > 0);
        Assert.True(aggregate.GetVisualChildAtForTraversalNonTemplatePathCount > 0);
        Assert.True(aggregate.GetVisualChildAtForTraversalFilteredContentCount > 0);
        Assert.True(aggregate.GetVisualChildAtForTraversalOutOfRangeCount > 0);
        Assert.True(aggregate.GetLogicalChildrenTemplatePathCount > 0);
        Assert.True(aggregate.GetLogicalChildrenNonTemplatePathCount > 0);
        Assert.True(aggregate.GetLogicalChildrenFilteredContentCount > 0);
        Assert.True(aggregate.MeasureOverrideTemplatePathCount > 0);
        Assert.True(aggregate.MeasureOverrideNonTemplatePathCount > 0);
        Assert.True(aggregate.MeasureOverrideTemplateRootMeasureCount > 0);
        Assert.True(aggregate.MeasureOverrideContentMeasureCount > 0);
        Assert.True(aggregate.ArrangeOverrideTemplatePathCount > 0);
        Assert.True(aggregate.ArrangeOverrideNonTemplatePathCount > 0);
        Assert.True(aggregate.ArrangeOverrideTemplateRootArrangeCount > 0);
        Assert.True(aggregate.ArrangeOverrideContentArrangeCount > 0);
        Assert.True(aggregate.GetChromeThicknessCallCount > 0);
        Assert.True(aggregate.EnsureTemplateAppliedIfNeededCallCount > 0);
        Assert.True(aggregate.EnsureTemplateAppliedApplyTemplateCount > 0 ||
                aggregate.EnsureTemplateAppliedRefreshCachedRootCount > 0 ||
                aggregate.EnsureTemplateAppliedNoOpCount > 0);
        Assert.True(aggregate.RefreshCachedTemplateRootCallCount > 0);
        Assert.True(aggregate.RefreshCachedTemplateRootHitCount > 0);
        Assert.True(aggregate.DetachTemplateContentPresentersCallCount > 0);
        Assert.True(aggregate.DetachTemplateContentPresentersFallbackSearchCount > 0 ||
                aggregate.DetachTemplateContentPresentersRootNotFoundCount > 0 ||
                aggregate.DetachTemplateContentPresentersVisitedElementCount > 0);

        var cleared = UserControl.GetTelemetryAndReset();

        Assert.Equal(0, cleared.DependencyPropertyChangedCallCount);
        Assert.Equal(0, cleared.GetVisualChildrenCallCount);
        Assert.Equal(0, cleared.MeasureOverrideCallCount);
        Assert.Equal(0, cleared.ArrangeOverrideCallCount);
        Assert.Equal(0, cleared.EnsureTemplateAppliedIfNeededCallCount);
        Assert.Equal(0, cleared.DetachTemplateContentPresentersCallCount);
    }

    [Fact]
    public async Task DiagnosticsPipeline_Emits_UserControlContributorFacts()
    {
        _ = UserControl.GetTelemetryAndReset();

        var root = new Canvas { Name = "Root", Width = 400f, Height = 240f };
        var userControl = new UserControl
        {
            Name = "Probe",
            Width = 180f,
            Height = 96f,
            BorderThickness = new Thickness(2f, 3f, 4f, 5f),
            Padding = new Thickness(6f, 7f, 8f, 9f),
            Content = new Border { Width = 48f, Height = 22f },
            Template = new ControlTemplate(_ =>
            {
                var templateRoot = new Border { Name = "TemplateRoot" };
                templateRoot.Child = new ContentPresenter();
                return templateRoot;
            })
            {
                TargetType = typeof(UserControl)
            }
        };
        root.AddChild(userControl);

        using var host = new InkkOopsTestHost(root);
        await host.AdvanceFrameAsync(1);

        _ = userControl.GetVisualChildren().ToList();
        _ = userControl.GetLogicalChildren().ToList();
        _ = userControl.GetVisualChildCountForTraversal();
        _ = userControl.GetVisualChildAtForTraversal(0);

        var diagnostics = new InkkOopsVisualTreeDiagnostics([
            new InkkOopsGenericElementDiagnosticsContributor(),
            new InkkOopsFrameworkElementDiagnosticsContributor(),
            new InkkOopsUserControlDiagnosticsContributor()
        ]);

        var snapshot = diagnostics.Capture(
            root,
            new InkkOopsDiagnosticsContext
            {
                UiRoot = host.UiRoot,
                Viewport = host.GetViewportBounds(),
                HoveredElement = userControl,
                FocusedElement = null,
                ArtifactName = "usercontrol"
            });
        var text = new DefaultInkkOopsDiagnosticsSerializer().SerializeVisualTree(snapshot);

        Assert.Contains("UserControl#Probe", text);
        Assert.Contains("userControlHasTemplateAssigned=True", text);
        Assert.Contains("userControlHasCachedTemplateRoot=True", text);
        Assert.Contains("userControlCachedTemplateRootType=Border", text);
        Assert.Contains("userControlRuntimeMeasureOverrideCalls=", text);
        Assert.Contains("userControlRuntimeEnsureTemplateCalls=", text);
        Assert.Contains("userControlRuntimeDetachPresenterCalls=", text);
        Assert.Contains("userControlMeasureOverrideCalls=", text);
        Assert.Contains("userControlEnsureTemplateCalls=", text);
        Assert.Contains("userControlDetachPresenterCalls=", text);
        Assert.Contains("userControlBorderThickness=2,3,4,5", text);
        Assert.Contains("userControlPadding=6,7,8,9", text);
    }

    private static void RunLayout(FrameworkElement element, int width, int height)
    {
        var uiRoot = BuildUiRootWithSingleChild(element, width, height, x: 10f, y: 15f);
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, width, height));
    }

    private static UiRoot BuildUiRootWithSingleChild(FrameworkElement element, int width, int height, float x, float y)
    {
        if (element.VisualParent is Panel existingPanel)
        {
            _ = existingPanel.RemoveChild(element);
        }
        else
        {
            element.SetVisualParent(null);
            element.SetLogicalParent(null);
        }

        var host = new Canvas
        {
            Width = width,
            Height = height
        };
        host.AddChild(element);
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        return new UiRoot(host);
    }
}