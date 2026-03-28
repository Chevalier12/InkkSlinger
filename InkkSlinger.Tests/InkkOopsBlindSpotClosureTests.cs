using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class InkkOopsBlindSpotClosureTests
{
    [Fact]
    public void TargetResolver_GlobalAutomationNameCanBeAmbiguous_AndScopedSelectorDisambiguates()
    {
        var leftContainer = new StackPanel { Name = "LeftRegion" };
        var rightContainer = new StackPanel { Name = "RightRegion" };
        var leftButton = new Button { Content = "Left" };
        var rightButton = new Button { Content = "Right" };
        AutomationProperties.SetName(leftButton, "SharedAction");
        AutomationProperties.SetName(rightButton, "SharedAction");
        leftContainer.AddChild(leftButton);
        rightContainer.AddChild(rightButton);

        var root = new StackPanel();
        root.AddChild(leftContainer);
        root.AddChild(rightContainer);

        using var host = new InkkOopsTestHost(root);

        var ambiguous = InkkOopsTargetResolver.Resolve(
            host,
            InkkOopsTargetSelector.AutomationName("SharedAction"));
        var scoped = InkkOopsTargetResolver.Resolve(
            host,
            InkkOopsTargetSelector.Within(
                InkkOopsTargetSelector.Name("LeftRegion"),
                InkkOopsTargetSelector.AutomationName("SharedAction")));

        Assert.Equal(InkkOopsTargetResolutionStatus.Ambiguous, ambiguous.Status);
        Assert.Equal(2, ambiguous.Candidates.Count);
        Assert.Equal(InkkOopsTargetResolutionStatus.Resolved, scoped.Status);
        Assert.Same(leftButton, scoped.Element);
    }

    [Fact]
    public async Task WaitForElement_CanPass_While_TargetState_Remains_Offscreen()
    {
        var button = new Button
        {
            Name = "OffscreenButton",
            Content = "Offscreen",
            Width = 120f,
            Height = 32f
        };

        Canvas.SetTop(button, 500f);
        var root = new Canvas();
        root.AddChild(button);

        using var host = new InkkOopsTestHost(root, width: 200, height: 120);
        using var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "wait-state");
        var session = new InkkOopsSession(host, artifacts);

        await new InkkOopsWaitForElementCommand(new InkkOopsTargetReference("OffscreenButton"), maxFrames: 1)
            .ExecuteAsync(session);

        var state = session.EvaluateTargetState(new InkkOopsTargetReference("OffscreenButton"));
        var waitEx = await Assert.ThrowsAsync<InkkOopsCommandException>(() =>
            new InkkOopsWaitForElementCommand(
                    new InkkOopsTargetReference("OffscreenButton"),
                    maxFrames: 1,
                    condition: InkkOopsWaitCondition.Interactive)
                .ExecuteAsync(session));

        Assert.Equal(InkkOopsFailureCategory.Offscreen, state.FailureCategory);
        Assert.Equal(InkkOopsFailureCategory.Timeout, waitEx.Category);
    }

    [Fact]
    public async Task Click_Command_Uses_Configured_Anchor_And_Writes_Command_Diagnostics()
    {
        var button = new Button
        {
            Name = "AnchorButton",
            Content = "Anchor",
            Width = 100f,
            Height = 40f
        };

        Canvas.SetLeft(button, 10f);
        Canvas.SetTop(button, 20f);
        var root = new Canvas();
        root.AddChild(button);

        using var host = new InkkOopsTestHost(root);
        using var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "anchor-click");
        var session = new InkkOopsSession(host, artifacts);
        var script = new InkkOopsScript("anchor-click")
            .Add(new InkkOopsClickTargetCommand(new InkkOopsTargetReference("AnchorButton"), InkkOopsPointerAnchor.BottomRight));

        var runner = new InkkOopsScriptRunner();
        var result = await runner.RunAsync(script, session);

        Assert.Equal(InkkOopsRunStatus.Completed, result.Status);

        var diagnostics = File.ReadAllText(artifacts.GetPath("command-000.json"));
        Assert.Contains("\"Anchor\": \"BottomRight\"", diagnostics);
        Assert.Contains("\"ActionPointX\": 110", diagnostics);
        Assert.Contains("\"ActionPointY\": 60", diagnostics);
    }

    [Fact]
    public async Task Invoke_Can_Succeed_When_Pointer_Click_Fails_For_Offscreen_Target()
    {
        var button = new Button
        {
            Name = "InvokeOffscreen",
            Content = "Offscreen",
            Width = 120f,
            Height = 32f
        };

        Canvas.SetTop(button, 500f);
        var root = new Canvas();
        root.AddChild(button);

        using var host = new InkkOopsTestHost(root, width: 200, height: 120);
        using var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "semantic-vs-pointer");
        var session = new InkkOopsSession(host, artifacts);
        var clicks = 0;
        button.Click += (_, _) => clicks++;

        var clickEx = await Assert.ThrowsAsync<InkkOopsCommandException>(() =>
            new InkkOopsClickTargetCommand(new InkkOopsTargetReference("InvokeOffscreen")).ExecuteAsync(session));

        await new InkkOopsInvokeTargetCommand(new InkkOopsTargetReference("InvokeOffscreen")).ExecuteAsync(session);

        Assert.Equal(InkkOopsFailureCategory.Offscreen, clickEx.Category);
        Assert.Equal(1, clicks);
    }

    [Fact]
    public async Task ScrollIntoView_ItemIndex_Works_For_DataGrid()
    {
        var grid = new DataGrid
        {
            Name = "VirtualGrid",
            Width = 180f,
            Height = 120f,
            AutoGenerateColumns = false
        };

        grid.Columns.Add(new DataGridColumn
        {
            Header = "Value",
            BindingPath = nameof(GridRow.Value),
            Width = 140f
        });

        for (var i = 0; i < 80; i++)
        {
            grid.Items.Add(new GridRow { Value = $"Item {i:000}" });
        }

        var root = new Canvas();
        root.AddChild(grid);

        using var host = new InkkOopsTestHost(root);
        using var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "datagrid-scroll-into-view");
        var session = new InkkOopsSession(host, artifacts);

        await new InkkOopsScrollIntoViewCommand(
            new InkkOopsTargetReference("VirtualGrid"),
            InkkOopsScrollLocator.ByItemIndex(60))
            .ExecuteAsync(session);

        Assert.True(grid.ScrollViewerForTesting.VerticalOffset > 0f);
    }

    [Fact]
    public async Task Runner_Writes_Failure_Diagnostics_With_Category_And_Resolution()
    {
        var button = new Button
        {
            Name = "FailureButton",
            Content = "Failure",
            Width = 120f,
            Height = 32f
        };

        Canvas.SetTop(button, 500f);
        var root = new Canvas();
        root.AddChild(button);

        using var host = new InkkOopsTestHost(root, width: 200, height: 120);
        using var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "failure-diagnostics");
        var session = new InkkOopsSession(host, artifacts);
        var script = new InkkOopsScript("failure-diagnostics")
            .Add(new InkkOopsClickTargetCommand(new InkkOopsTargetReference("FailureButton")));

        var runner = new InkkOopsScriptRunner();
        var result = await runner.RunAsync(script, session);

        Assert.Equal(InkkOopsRunStatus.Failed, result.Status);

        var diagnostics = File.ReadAllText(artifacts.GetPath("command-000.json"));
        Assert.Contains("\"FailureCategory\": 4", diagnostics); // Offscreen
        Assert.Contains("\"ResolutionStatus\": \"Resolved\"", diagnostics);
        Assert.Contains("\"ExecutionMode\": 1", diagnostics); // Pointer
    }

    [Fact]
    public async Task WaitForIdle_DiagnosticsStable_Completes()
    {
        using var host = new InkkOopsTestHost(new Canvas());
        await host.WaitForIdleAsync(InkkOopsIdlePolicy.DiagnosticsStable);
    }

    private static T? FindDescendant<T>(UIElement root)
        where T : UIElement
    {
        if (root is T typed)
        {
            return typed;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var match = FindDescendant<T>(child);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private sealed class GridRow
    {
        public string Value { get; init; } = string.Empty;
    }
}
