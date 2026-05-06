using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class InkkOopsRegistryAndRunnerTests
{
    public InkkOopsRegistryAndRunnerTests()
    {
        ResetRegistryTestState();
    }

    [Fact]
    public void Registry_Discovers_BuiltInScript()
    {
        var registry = new InkkOopsScriptRegistry(typeof(ControlsCatalogView).Assembly);

        Assert.Contains("buttons-resize-hover-repro", registry.ListScripts());
        Assert.True(registry.TryResolve("buttons-resize-hover-repro", out var script));
        Assert.NotNull(script);
        Assert.Equal("buttons-resize-hover-repro", script!.CreateScript().Name);
    }

    [Fact]
    public void Registry_Rejects_Duplicate_ScriptNames()
    {
        const string source = """
using InkkSlinger;
public sealed class ScriptOne : IInkkOopsBuiltinScript
{
    public string Name => "dup";
    public InkkOopsScript CreateScript() => new InkkOopsScript("dup");
}
public sealed class ScriptTwo : IInkkOopsBuiltinScript
{
    public string Name => "dup";
    public InkkOopsScript CreateScript() => new InkkOopsScript("dup");
}
""";

        var assembly = CompileTestAssembly(source);
        var ex = Assert.Throws<InvalidOperationException>(() => new InkkOopsScriptRegistry(assembly));
        Assert.Contains("Duplicate InkkOops script name 'dup'", ex.Message);
    }

    [Fact]
    public async Task Runner_Returns_Failed_Status_And_CommandIndex()
    {
        using var host = new InkkOopsTestHost(new Canvas());
        using var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "runner-failure");
        var session = new InkkOopsSession(host, artifacts);
        var script = new InkkOopsScript("runner-failure")
            .Add(new TestCommand("first"))
            .Add(new ThrowingCommand("boom"))
            .Add(new TestCommand("never"));

        var runner = new InkkOopsScriptRunner();
        var result = await runner.RunAsync(script, session, CancellationToken.None);

        Assert.Equal(InkkOopsRunStatus.Failed, result.Status);
        Assert.Equal(1, result.FailedCommandIndex);
        Assert.Contains("Throw(boom)", result.FailedCommandDescription);
        Assert.Contains("boom", result.FailureMessage);
    }

    [Fact]
    public async Task Runner_Writes_Action_Log_With_Explicit_Pointer_Actions()
    {
        var button = new Button
        {
            Name = "TargetButton",
            Content = "Click Me",
            Width = 120f,
            Height = 32f
        };

        var root = new Canvas();
        root.AddChild(button);

        using var host = new InkkOopsTestHost(root);
        string actionLogPath;

        using (var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "runner-action-log"))
        {
            var session = new InkkOopsSession(host, artifacts);
            var script = new InkkOopsScript("runner-action-log")
                .Add(new InkkOopsHoverTargetCommand(new InkkOopsTargetReference("TargetButton")))
                .Add(new InkkOopsClickTargetCommand(new InkkOopsTargetReference("TargetButton")));

            var runner = new InkkOopsScriptRunner();
            var result = await runner.RunAsync(script, session, CancellationToken.None);

            Assert.Equal(InkkOopsRunStatus.Completed, result.Status);
            actionLogPath = artifacts.GetActionLogPath();
        }

        var actionLogLines = File.ReadAllLines(actionLogPath);

        Assert.Contains("Button#TargetButton", actionLogLines);
        Assert.Contains(actionLogLines, static line => line.Contains("action[0] pointer enter", StringComparison.Ordinal));
        Assert.Contains(actionLogLines, static line => line.Contains("action[0] pointer over", StringComparison.Ordinal));
        Assert.Contains(actionLogLines, static line => line.Contains("action[1] pointer down", StringComparison.Ordinal));
        Assert.Contains(actionLogLines, static line => line.Contains("action[1] pointer up", StringComparison.Ordinal));
        Assert.Contains(actionLogLines, static line => line.Contains("fps=60.0", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Runner_Uses_Control_Text_For_Unnamed_Action_Log_Subjects()
    {
        var button = new Button
        {
            Name = "SaveDraftButton",
            Content = "Save Draft",
            Width = 120f,
            Height = 32f
        };
        AutomationProperties.SetName(button, "Save Draft");
        var root = new Canvas();
        root.AddChild(button);

        using var host = new InkkOopsTestHost(root);
        string actionLogPath;

        using (var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "runner-action-log-unnamed-subject"))
        {
            var session = new InkkOopsSession(host, artifacts);
            var script = new InkkOopsScript("runner-action-log-unnamed-subject")
                .Add(new InkkOopsHoverTargetCommand(new InkkOopsTargetReference(InkkOopsTargetSelector.Name("SaveDraftButton"))));

            var runner = new InkkOopsScriptRunner();
            var result = await runner.RunAsync(script, session, CancellationToken.None);

            Assert.Equal(InkkOopsRunStatus.Completed, result.Status);
            actionLogPath = artifacts.GetActionLogPath();
        }

        var actionLogLines = File.ReadAllLines(actionLogPath);

        Assert.True(
            actionLogLines.Contains("Button[Save Draft]") ||
            actionLogLines.Contains("Button#SaveDraftButton"));
        Assert.DoesNotContain("Button", actionLogLines, StringComparer.Ordinal);
    }

    [Fact]
    public async Task PipeMessages_RoundTrip_Through_Json()
    {
        var request = new InkkOopsPipeRequest
        {
            RequestKind = InkkOopsPipeRequestKinds.RunScript,
            ScriptName = "buttons-resize-hover-repro",
            ScopeTargetName = "CatalogSidebarScrollViewer",
            TimeoutMilliseconds = 5000,
            ArtifactRootOverride = "artifacts/custom"
        };
        var response = new InkkOopsPipeResponse
        {
            Status = InkkOopsRunStatus.Completed.ToString(),
            ScriptName = request.ScriptName,
            ArtifactDirectory = "artifacts/inkkoops/run-1",
            Message = string.Empty,
            Value = "ok"
        };

        var requestJson = System.Text.Json.JsonSerializer.Serialize(request);
        var responseJson = System.Text.Json.JsonSerializer.Serialize(response);
        var roundTripRequest = System.Text.Json.JsonSerializer.Deserialize<InkkOopsPipeRequest>(requestJson);
        var roundTripResponse = System.Text.Json.JsonSerializer.Deserialize<InkkOopsPipeResponse>(responseJson);

        Assert.NotNull(roundTripRequest);
        Assert.NotNull(roundTripResponse);
        Assert.Equal(InkkOopsPipeRequestKinds.RunScript, roundTripRequest!.RequestKind);
        Assert.Equal(request.ScriptName, roundTripRequest!.ScriptName);
        Assert.Equal(request.ScopeTargetName, roundTripRequest.ScopeTargetName);
        Assert.Equal(response.ArtifactDirectory, roundTripResponse!.ArtifactDirectory);
        Assert.Equal(response.Value, roundTripResponse.Value);
    }

    [Fact]
    public async Task LiveRequestDispatcher_GetHostInfo_Returns_Reconnect_Metadata()
    {
        using var host = new InkkOopsTestHost(new Canvas());
        var dispatcher = new InkkOopsLiveRequestDispatcher(host, new InkkOopsScriptRegistry(typeof(ControlsCatalogView).Assembly), host.ArtifactRoot);

        var response = await dispatcher.SubmitAsync(
            new InkkOopsPipeRequest
            {
                RequestKind = InkkOopsPipeRequestKinds.GetHostInfo
            },
            CancellationToken.None);

        Assert.Equal(InkkOopsRunStatus.Completed.ToString(), response.Status);
        Assert.Contains("default_pipe=InkkOops", response.Value);
        Assert.Contains("artifact_root=", response.Value);
        Assert.Contains("script_count=", response.Value);
    }

    [Fact]
    public async Task LiveRequestDispatcher_RunScenario_Writes_Trace_Report_Diff_And_Hints()
    {
        var button = new Button
        {
            Name = "ScenarioButton",
            Content = "Trace Me",
            Width = 120f,
            Height = 32f
        };
        Canvas.SetLeft(button, 40f);
        Canvas.SetTop(button, 30f);
        var root = new Canvas { Width = 400f, Height = 240f };
        root.AddChild(button);
        using var host = new InkkOopsTestHost(root, width: 400, height: 240);
        using var dispatcher = new InkkOopsLiveRequestDispatcher(host, new InkkOopsScriptRegistry(typeof(ControlsCatalogView).Assembly), host.ArtifactRoot);
        var scenarioJson = """
        {
          "name": "button-trace",
          "steps": [
            { "command": "wait-for-visible", "target": "ScenarioButton", "frames": 2 },
            { "command": "capture-frame", "artifact": "button-held" },
            { "command": "get-telemetry", "artifact": "button-mid" },
            { "command": "assert-nonblank", "target": "ScenarioButton", "minBrightPixels": 1 }
          ]
        }
        """;

        var response = await dispatcher.SubmitAsync(
            new InkkOopsPipeRequest
            {
                RequestKind = InkkOopsPipeRequestKinds.RunScenario,
                Text = scenarioJson
            },
            CancellationToken.None);

        Assert.Equal(InkkOopsRunStatus.Completed.ToString(), response.Status);
        Assert.Contains("scenario=button-trace", response.Value);
        Assert.Contains("hints=", response.Value);
        Assert.True(File.Exists(Path.Combine(response.ArtifactDirectory, "button-trace-report.md")));
        Assert.True(File.Exists(Path.Combine(response.ArtifactDirectory, "button-trace-trace.json")));
        Assert.True(File.Exists(Path.Combine(response.ArtifactDirectory, "button-trace-artifact-index.json")));
        Assert.True(File.Exists(Path.Combine(response.ArtifactDirectory, "button-trace-step-001-wait-for-visible-frame-window.md")));
        var report = File.ReadAllText(Path.Combine(response.ArtifactDirectory, "button-trace-report.md"));
        var trace = File.ReadAllText(Path.Combine(response.ArtifactDirectory, "button-trace-trace.json"));
        Assert.Contains("Telemetry Diff", report);
        Assert.Contains("Hints", report);
        Assert.Contains("frameWindow:", report);
        Assert.Contains("sampleCount=", report);
        Assert.Contains("FrameWindowPath", trace);
        Assert.Contains("FrameSummary", trace);
    }

    [Fact]
    public async Task LiveRequestDispatcher_ProbeDuringDrag_Captures_Transient_Report()
    {
        var thumb = new Thumb
        {
            Name = "DragThumb",
            Width = 24f,
            Height = 24f
        };
        Canvas.SetLeft(thumb, 40f);
        Canvas.SetTop(thumb, 30f);
        var root = new Canvas { Width = 400f, Height = 240f };
        root.AddChild(thumb);
        using var host = new InkkOopsTestHost(root, width: 400, height: 240);
        using var dispatcher = new InkkOopsLiveRequestDispatcher(host, new InkkOopsScriptRegistry(typeof(ControlsCatalogView).Assembly), host.ArtifactRoot);

        var response = await dispatcher.SubmitAsync(
            new InkkOopsPipeRequest
            {
                RequestKind = InkkOopsPipeRequestKinds.ProbeDuringDrag,
                TargetName = "DragThumb",
                DeltaX = 40f,
                DeltaY = 20f,
                TravelFrames = 2,
                ArtifactName = "thumb-drag-probe",
                MinBrightPixels = 1
            },
            CancellationToken.None);

        Assert.Equal(InkkOopsRunStatus.Completed.ToString(), response.Status);
        Assert.Contains("scenario=thumb-drag-probe", response.Value);
        Assert.True(File.Exists(Path.Combine(response.ArtifactDirectory, "thumb-drag-probe-report.md")));
        Assert.True(host.PointerTrace.Count > 0);
    }

    [Fact]
    public async Task LiveRequestDispatcher_ProbeAction_Captures_Frame_Timing_Window()
    {
        var button = new Button
        {
            Name = "ProbeButton",
            Content = "Probe",
            Width = 120f,
            Height = 32f
        };
        var root = new Canvas { Width = 400f, Height = 240f };
        root.AddChild(button);
        using var host = new InkkOopsTestHost(root, width: 400, height: 240);
        using var dispatcher = new InkkOopsLiveRequestDispatcher(host, new InkkOopsScriptRegistry(typeof(ControlsCatalogView).Assembly), host.ArtifactRoot);

        var response = await dispatcher.SubmitAsync(
            new InkkOopsPipeRequest
            {
                RequestKind = InkkOopsPipeRequestKinds.ProbeAction,
                ArtifactName = "button-probe",
                FrameCount = 3,
                Text = """{"command":"click","target":"ProbeButton"}"""
            },
            CancellationToken.None);

        Assert.Equal(InkkOopsRunStatus.Completed.ToString(), response.Status);
        Assert.Contains("probe=button-probe", response.Value);
        Assert.Contains("maxFrameTotalMs=", response.Value);
        Assert.True(File.Exists(Path.Combine(response.ArtifactDirectory, "button-probe-frame-window.md")));
        Assert.True(File.Exists(Path.Combine(response.ArtifactDirectory, "button-probe-report.md")));
    }

    [Fact]
    public async Task LiveRequestDispatcher_DiffTelemetry_Returns_Report_With_Hypothesis_Hints()
    {
        using var host = new InkkOopsTestHost(new Canvas());
        using var dispatcher = new InkkOopsLiveRequestDispatcher(host, new InkkOopsScriptRegistry(typeof(ControlsCatalogView).Assembly), host.ArtifactRoot);
        var before = """
        artifact_name=before
        lastPointerMotion=motionStep=1/2 inputMs=0.1 routeMs=0.1
        scrollViewerSetOffsetsDeferredLayoutPathCount=0
        lastRenderRetainedNodesDrawn=10
        """;
        var after = """
        artifact_name=after
        lastPointerMotion=motionStep=1/2 inputMs=12 routeMs=11
        scrollViewerSetOffsetsDeferredLayoutPathCount=3
        lastRenderRetainedNodesDrawn=0
        """;

        var response = await dispatcher.SubmitAsync(
            new InkkOopsPipeRequest
            {
                RequestKind = InkkOopsPipeRequestKinds.DiffTelemetry,
                ArtifactName = "diff-proof",
                Text = before + "\n---INKKOOPS-AFTER---\n" + after
            },
            CancellationToken.None);

        Assert.Equal(InkkOopsRunStatus.Completed.ToString(), response.Status);
        Assert.Contains("Input route is hot", response.Value);
        Assert.Contains("deferred layout", response.Value);
        Assert.Contains("No retained nodes", response.Value);
        Assert.True(File.Exists(Path.Combine(host.ArtifactRoot, "live-session", "diff-proof.md")) || Directory.GetFiles(host.ArtifactRoot, "diff-proof.md", SearchOption.AllDirectories).Length == 1);
    }

    [Fact]
    public async Task LiveRequestDispatcher_AssertNonBlank_Samples_Target_Frame_Region()
    {
        var button = new Button
        {
            Name = "NonBlankButton",
            Content = "Pixels",
            Width = 120f,
            Height = 32f
        };
        var root = new Canvas { Width = 400f, Height = 240f };
        root.AddChild(button);
        using var host = new InkkOopsTestHost(root, width: 400, height: 240);
        using var dispatcher = new InkkOopsLiveRequestDispatcher(host, new InkkOopsScriptRegistry(typeof(ControlsCatalogView).Assembly), host.ArtifactRoot);

        var response = await dispatcher.SubmitAsync(
            new InkkOopsPipeRequest
            {
                RequestKind = InkkOopsPipeRequestKinds.AssertNonBlank,
                TargetName = "NonBlankButton",
                MinBrightPixels = 1
            },
            CancellationToken.None);

        Assert.Equal(InkkOopsRunStatus.Completed.ToString(), response.Status);
        Assert.Contains("nonblank=True", response.Value);
    }

    [Fact]
    public async Task LiveRequestDispatcher_GetProperty_Returns_Value_From_Running_Host()
    {
        var button = new Button
        {
            Name = "LiveButton",
            Content = "Push Me",
            Width = 120f,
            Height = 32f
        };
        var root = new Canvas();
        root.AddChild(button);
        using var host = new InkkOopsTestHost(root);
        var dispatcher = new InkkOopsLiveRequestDispatcher(host, new InkkOopsScriptRegistry(typeof(ControlsCatalogView).Assembly), host.ArtifactRoot);

        var response = await dispatcher.SubmitAsync(
            new InkkOopsPipeRequest
            {
                RequestKind = InkkOopsPipeRequestKinds.GetProperty,
                TargetName = "LiveButton",
                PropertyName = "Content"
            },
            CancellationToken.None);

        Assert.Equal(InkkOopsRunStatus.Completed.ToString(), response.Status);
        Assert.Equal("Push Me", response.Value);
    }

    [Fact]
    public async Task LiveRequestDispatcher_Hover_Then_GetProperty_Uses_Live_App_State()
    {
        var button = new Button
        {
            Name = "HoverButton",
            Content = "Hover Me",
            Width = 120f,
            Height = 32f
        };
        Canvas.SetLeft(button, 640f);
        Canvas.SetTop(button, 480f);
        var root = new Canvas { Width = 800f, Height = 600f };
        root.AddChild(button);
        using var host = new InkkOopsTestHost(root);
        var dispatcher = new InkkOopsLiveRequestDispatcher(host, new InkkOopsScriptRegistry(typeof(ControlsCatalogView).Assembly), host.ArtifactRoot);

        var hoverResponse = await dispatcher.SubmitAsync(
            new InkkOopsPipeRequest
            {
                RequestKind = InkkOopsPipeRequestKinds.HoverTarget,
                TargetName = "HoverButton"
            },
            CancellationToken.None);
        var propertyResponse = await dispatcher.SubmitAsync(
            new InkkOopsPipeRequest
            {
                RequestKind = InkkOopsPipeRequestKinds.GetProperty,
                TargetName = "HoverButton",
                PropertyName = "IsMouseOver"
            },
            CancellationToken.None);

        Assert.Equal(InkkOopsRunStatus.Completed.ToString(), hoverResponse.Status);
        Assert.Equal(InkkOopsRunStatus.Completed.ToString(), propertyResponse.Status);
        Assert.Equal("True", propertyResponse.Value);
    }

    [Fact]
    public async Task LiveRequestDispatcher_MovePointer_TargetOffset_Uses_Anchor_And_Motion()
    {
        var button = new Button
        {
            Name = "OffsetButton",
            Content = "Offset",
            Width = 120f,
            Height = 40f
        };
        Canvas.SetLeft(button, 100f);
        Canvas.SetTop(button, 50f);
        var root = new Canvas { Width = 800f, Height = 600f };
        root.AddChild(button);
        using var host = new InkkOopsTestHost(root);
        var dispatcher = new InkkOopsLiveRequestDispatcher(host, new InkkOopsScriptRegistry(typeof(ControlsCatalogView).Assembly), host.ArtifactRoot);

        var response = await dispatcher.SubmitAsync(
            new InkkOopsPipeRequest
            {
                RequestKind = InkkOopsPipeRequestKinds.MovePointer,
                TargetName = "OffsetButton",
                Anchor = "offset",
                OffsetX = 10f,
                OffsetY = 12f,
                TravelFrames = 3,
                StepDistance = 8f,
                Easing = "ease-in-out"
            },
            CancellationToken.None);

        var lastPoint = host.PointerTrace.Last();
        Assert.Equal(InkkOopsRunStatus.Completed.ToString(), response.Status);
        Assert.True(button.IsMouseOver);
        Assert.InRange(lastPoint.X, 109.99f, 110.01f);
        Assert.InRange(lastPoint.Y, 61.99f, 62.01f);
    }

    [Fact]
    public async Task LiveRequestDispatcher_MovePointer_AbsoluteCoordinates_Uses_X_And_Y()
    {
        var root = new Canvas { Width = 800f, Height = 600f };
        using var host = new InkkOopsTestHost(root);
        var dispatcher = new InkkOopsLiveRequestDispatcher(host, new InkkOopsScriptRegistry(typeof(ControlsCatalogView).Assembly), host.ArtifactRoot);

        var response = await dispatcher.SubmitAsync(
            new InkkOopsPipeRequest
            {
                RequestKind = InkkOopsPipeRequestKinds.MovePointer,
                X = 321f,
                Y = 234f,
                TravelFrames = 2
            },
            CancellationToken.None);

        var lastPoint = host.PointerTrace.Last();
        Assert.Equal(InkkOopsRunStatus.Completed.ToString(), response.Status);
        Assert.InRange(lastPoint.X, 320.99f, 321.01f);
        Assert.InRange(lastPoint.Y, 233.99f, 234.01f);
    }

    [Fact]
    public async Task LiveRequestDispatcher_GetProperty_Uses_Scope_To_Resolve_Owning_Button_By_Text()
    {
        var sidebarButton = new Button
        {
            Name = "SidebarBorderButton",
            Content = "Border",
            Width = 120f,
            Height = 32f
        };
        var sidebarHost = new StackPanel
        {
            Name = "SidebarHost"
        };
        sidebarHost.AddChild(sidebarButton);

        var outsideButton = new Button
        {
            Name = "MainBorderButton",
            Content = "Border",
            Width = 120f,
            Height = 32f
        };

        var root = new Grid();
        root.AddChild(sidebarHost);
        root.AddChild(outsideButton);

        using var host = new InkkOopsTestHost(root);
        var dispatcher = new InkkOopsLiveRequestDispatcher(host, new InkkOopsScriptRegistry(typeof(ControlsCatalogView).Assembly), host.ArtifactRoot);

        var response = await dispatcher.SubmitAsync(
            new InkkOopsPipeRequest
            {
                RequestKind = InkkOopsPipeRequestKinds.GetProperty,
                ScopeTargetName = "SidebarHost",
                TargetName = "Border",
                PropertyName = nameof(FrameworkElement.Name)
            },
            CancellationToken.None);

        Assert.Equal(InkkOopsRunStatus.Completed.ToString(), response.Status);
        Assert.Equal("SidebarBorderButton", response.Value);
    }

    [Fact]
    public async Task LiveRequestDispatcher_Invoke_Uses_Scope_To_Trigger_Button()
    {
        var invoked = false;
        var button = new Button
        {
            Name = "SidebarBorderButton",
            Content = "Border",
            Width = 120f,
            Height = 32f
        };
        button.Click += (_, _) => invoked = true;

        var sidebarHost = new StackPanel
        {
            Name = "SidebarHost"
        };
        sidebarHost.AddChild(button);

        var root = new Grid();
        root.AddChild(sidebarHost);

        using var host = new InkkOopsTestHost(root);
        var dispatcher = new InkkOopsLiveRequestDispatcher(host, new InkkOopsScriptRegistry(typeof(ControlsCatalogView).Assembly), host.ArtifactRoot);

        var response = await dispatcher.SubmitAsync(
            new InkkOopsPipeRequest
            {
                RequestKind = InkkOopsPipeRequestKinds.InvokeTarget,
                ScopeTargetName = "SidebarHost",
                TargetName = "Border"
            },
            CancellationToken.None);

        Assert.Equal(InkkOopsRunStatus.Completed.ToString(), response.Status);
        Assert.True(invoked);
    }

    [Fact]
    public async Task LiveRequestDispatcher_ScrollIntoView_Brings_Target_Into_Viewport()
    {
        var root = new ScrollViewer
        {
            Name = "OwnerScrollViewer",
            Width = 320f,
            Height = 140f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var stack = new StackPanel();
        for (var i = 0; i < 8; i++)
        {
            stack.AddChild(new Border
            {
                Height = 50f,
                Margin = new Thickness(0, 0, 0, 6)
            });
        }

        var target = new CheckBox
        {
            Name = "ClipToBoundsCheckBox",
            Content = "ClipToBounds = True",
            Height = 32f
        };
        stack.AddChild(target);
        root.Content = stack;

        using var host = new InkkOopsTestHost(root);
        var dispatcher = new InkkOopsLiveRequestDispatcher(host, new InkkOopsScriptRegistry(typeof(ControlsCatalogView).Assembly), host.ArtifactRoot);

        var response = await dispatcher.SubmitAsync(
            new InkkOopsPipeRequest
            {
                RequestKind = InkkOopsPipeRequestKinds.ScrollIntoView,
                OwnerTargetName = "OwnerScrollViewer",
                TargetName = "ClipToBoundsCheckBox",
                Padding = 8f
            },
            CancellationToken.None);

        var stateResponse = await dispatcher.SubmitAsync(
            new InkkOopsPipeRequest
            {
                RequestKind = InkkOopsPipeRequestKinds.WaitForInViewport,
                TargetName = "ClipToBoundsCheckBox",
                FrameCount = 1
            },
            CancellationToken.None);

        Assert.Equal(InkkOopsRunStatus.Completed.ToString(), response.Status);
        Assert.Equal(InkkOopsRunStatus.Completed.ToString(), stateResponse.Status);
    }

    [Fact]
    public async Task LiveRequestDispatcher_ScrollIntoView_Infers_Nearest_ScrollViewer_When_Owner_Is_Omitted()
    {
        var root = new ScrollViewer
        {
            Name = "OwnerScrollViewer",
            Width = 320f,
            Height = 140f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var stack = new StackPanel();
        for (var i = 0; i < 8; i++)
        {
            stack.AddChild(new Border
            {
                Height = 50f,
                Margin = new Thickness(0, 0, 0, 6)
            });
        }

        var target = new CheckBox
        {
            Name = "ClipToBoundsCheckBox",
            Content = "ClipToBounds = True",
            Height = 32f
        };
        stack.AddChild(target);
        root.Content = stack;

        using var host = new InkkOopsTestHost(root);
        var dispatcher = new InkkOopsLiveRequestDispatcher(host, new InkkOopsScriptRegistry(typeof(ControlsCatalogView).Assembly), host.ArtifactRoot);

        var response = await dispatcher.SubmitAsync(
            new InkkOopsPipeRequest
            {
                RequestKind = InkkOopsPipeRequestKinds.ScrollIntoView,
                TargetName = "ClipToBoundsCheckBox",
                Padding = 8f
            },
            CancellationToken.None);

        var stateResponse = await dispatcher.SubmitAsync(
            new InkkOopsPipeRequest
            {
                RequestKind = InkkOopsPipeRequestKinds.WaitForInViewport,
                TargetName = "ClipToBoundsCheckBox",
                FrameCount = 1
            },
            CancellationToken.None);

        Assert.Equal(InkkOopsRunStatus.Completed.ToString(), response.Status);
        Assert.Equal(InkkOopsRunStatus.Completed.ToString(), stateResponse.Status);
    }

    [Fact]
    public async Task LiveRequestDispatcher_ScrollTo_Accepts_Owner_As_ScrollProvider_Alias()
    {
        var root = new ScrollViewer
        {
            Name = "OwnerScrollViewer",
            Width = 320f,
            Height = 140f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var stack = new StackPanel();
        for (var i = 0; i < 12; i++)
        {
            stack.AddChild(new Border
            {
                Height = 50f,
                Margin = new Thickness(0, 0, 0, 6)
            });
        }

        root.Content = stack;

        using var host = new InkkOopsTestHost(root);
        var dispatcher = new InkkOopsLiveRequestDispatcher(host, new InkkOopsScriptRegistry(typeof(ControlsCatalogView).Assembly), host.ArtifactRoot);

        var response = await dispatcher.SubmitAsync(
            new InkkOopsPipeRequest
            {
                RequestKind = InkkOopsPipeRequestKinds.ScrollTo,
                OwnerTargetName = "OwnerScrollViewer",
                VerticalPercent = 100f
            },
            CancellationToken.None);

        Assert.Equal(InkkOopsRunStatus.Completed.ToString(), response.Status);
        Assert.True(root.VerticalOffset > 0f);
    }

    [Fact]
    public async Task LiveRequestDispatcher_GetTargetDiagnostics_Returns_Target_State_And_Runtime_Snapshots()
    {
        var checkBox = new CheckBox
        {
            Name = "DiagCheckBox",
            Content = "Diag",
            Width = 120f,
            Height = 32f,
            IsChecked = true
        };

        var root = new Canvas();
        root.AddChild(checkBox);

        using var host = new InkkOopsTestHost(root);
        var dispatcher = new InkkOopsLiveRequestDispatcher(host, new InkkOopsScriptRegistry(typeof(ControlsCatalogView).Assembly), host.ArtifactRoot);

        var response = await dispatcher.SubmitAsync(
            new InkkOopsPipeRequest
            {
                RequestKind = InkkOopsPipeRequestKinds.GetTargetDiagnostics,
                TargetName = "DiagCheckBox"
            },
            CancellationToken.None);

        Assert.Equal(InkkOopsRunStatus.Completed.ToString(), response.Status);
        Assert.Contains("resolution.status=Resolved", response.Value);
        Assert.Contains("element=CheckBox#DiagCheckBox", response.Value);
        Assert.Contains("CheckBoxRuntimeDiagnosticsSnapshot.IsChecked=True", response.Value);
        Assert.Contains("CheckBoxRuntimeDiagnosticsSnapshot.RenderCallCount=", response.Value);
    }

    [Fact]
    public async Task LiveRequestDispatcher_GetTargetDiagnostics_Compact_Returns_Requested_Counters_Only()
    {
        var checkBox = new CheckBox
        {
            Name = "DiagCheckBox",
            Content = "Diag",
            Width = 120f,
            Height = 32f,
            IsChecked = true
        };

        var root = new Canvas();
        root.AddChild(checkBox);

        using var host = new InkkOopsTestHost(root);
        var dispatcher = new InkkOopsLiveRequestDispatcher(host, new InkkOopsScriptRegistry(typeof(ControlsCatalogView).Assembly), host.ArtifactRoot);

        var response = await dispatcher.SubmitAsync(
            new InkkOopsPipeRequest
            {
                RequestKind = InkkOopsPipeRequestKinds.GetTargetDiagnostics,
                TargetName = "DiagCheckBox",
                Compact = true,
                CounterNames = "IsChecked,MeasureOverrideCallCount,RenderCallCount,DrawTextCallCount"
            },
            CancellationToken.None);

        Assert.Equal(InkkOopsRunStatus.Completed.ToString(), response.Status);
        Assert.Contains("resolution.status=Resolved", response.Value);
        Assert.Contains("element=CheckBox#DiagCheckBox", response.Value);
        Assert.Contains("CheckBoxRuntimeDiagnosticsSnapshot.IsChecked=True", response.Value);
        Assert.Contains("CheckBoxRuntimeDiagnosticsSnapshot.MeasureOverrideCallCount=", response.Value);
        Assert.Contains("CheckBoxRuntimeDiagnosticsSnapshot.RenderCallCount=", response.Value);
        Assert.Contains("CheckBoxRuntimeDiagnosticsSnapshot.DrawTextCallCount=", response.Value);
        Assert.DoesNotContain("CheckBoxRuntimeDiagnosticsSnapshot.GetFallbackStyleCallCount=", response.Value);
        Assert.DoesNotContain("ButtonRuntimeDiagnosticsSnapshot.DependencyPropertyChangedCallCount=", response.Value);
    }

    [Theory]
    [InlineData(InkkOopsRunStatus.Completed, InkkOopsExitCodes.Success)]
    [InlineData(InkkOopsRunStatus.Failed, InkkOopsExitCodes.Failed)]
    [InlineData(InkkOopsRunStatus.Busy, InkkOopsExitCodes.Busy)]
    [InlineData(InkkOopsRunStatus.NotFound, InkkOopsExitCodes.NotFound)]
    public void ExitCodes_Map_RunStatuses_To_ProcessExitCodes(InkkOopsRunStatus status, int expectedExitCode)
    {
        Assert.Equal(expectedExitCode, InkkOopsExitCodes.FromStatus(status));
        Assert.Equal(expectedExitCode, InkkOopsExitCodes.FromStatus(status.ToString()));
    }

    [Fact]
    public void ExitCodes_Unknown_Status_Maps_To_Failed()
    {
        Assert.Equal(InkkOopsExitCodes.Failed, InkkOopsExitCodes.FromStatus("bogus"));
    }

    private static Assembly CompileTestAssembly(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(static assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(static assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Cast<MetadataReference>()
            .ToArray();
        var compilation = CSharpCompilation.Create(
            $"InkkOopsRegistryTests_{Guid.NewGuid():N}",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        return Assembly.Load(stream.ToArray());
    }

    private static void ResetRegistryTestState()
    {
        UiRoot.ResetForTests();
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        AnimationManager.Current.ResetForTests();
        UiApplication.Current.ResetForTests();
        FocusManager.ClearFocus();
        FocusManager.ClearPointerCapture();
        InputGestureService.Clear();
        Popup.ResetForTests();
        TextClipboard.ResetForTests();
    }

    private sealed class TestCommand : IInkkOopsCommand
    {
        private readonly string _name;

        public TestCommand(string name)
        {
            _name = name;
        }

        public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Diagnostic;

        public string Describe() => $"Test({_name})";

        public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingCommand : IInkkOopsCommand
    {
        private readonly string _message;

        public ThrowingCommand(string message)
        {
            _message = message;
        }

        public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Diagnostic;

        public string Describe() => $"Throw({_message})";

        public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(_message);
        }
    }
}
