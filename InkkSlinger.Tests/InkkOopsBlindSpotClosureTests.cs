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
    public void ResolveRequiredActionPoint_Uses_Configured_Anchor()
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
        var actionPoint = session.ResolveRequiredActionPoint(new InkkOopsTargetReference("AnchorButton"), InkkOopsPointerAnchor.BottomRight);

        Assert.Equal(110f, actionPoint.X, 0.01f);
        Assert.Equal(60f, actionPoint.Y, 0.01f);
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
}
