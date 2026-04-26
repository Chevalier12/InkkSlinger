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
