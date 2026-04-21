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
    public async Task Runner_Writes_Filtered_PerAction_Diagnostics_File()
    {
        var button = new Button
        {
            Name = "PlaygroundButton",
            Content = "Playground",
            Width = 120f,
            Height = 32f
        };
        var root = new Canvas { Name = "RootCanvas" };
        root.AddChild(button);

        using var host = new InkkOopsTestHost(root);
        string actionDiagnosticsPath;

        using (var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "runner-action-diagnostics"))
        {
            var session = new InkkOopsSession(host, artifacts, [0]);
            var script = new InkkOopsScript("runner-action-diagnostics")
                .Add(new InkkOopsHoverTargetCommand(new InkkOopsTargetReference("PlaygroundButton")));

            var runner = new InkkOopsScriptRunner();
            var result = await runner.RunAsync(script, session, CancellationToken.None);

            Assert.Equal(InkkOopsRunStatus.Completed, result.Status);
            actionDiagnosticsPath = artifacts.GetPath("action[0].txt");
            Assert.False(File.Exists(actionDiagnosticsPath));
        }

        var diagnosticsText = File.ReadAllText(actionDiagnosticsPath);

        Assert.Contains("action_index=0", diagnosticsText, StringComparison.Ordinal);
        Assert.Contains("action_description=Hover(Name('PlaygroundButton'), anchor:", diagnosticsText, StringComparison.Ordinal);
        Assert.Contains("visual_tree_begin", diagnosticsText, StringComparison.Ordinal);
        Assert.Contains("Canvas#RootCanvas", diagnosticsText, StringComparison.Ordinal);
        Assert.Contains("Button#PlaygroundButton", diagnosticsText, StringComparison.Ordinal);
        Assert.Contains("buttonDisplayText=Playground", diagnosticsText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Runner_Skips_PerAction_Diagnostics_When_Action_Index_Is_Not_Selected()
    {
        var button = new Button
        {
            Name = "PlaygroundButton",
            Content = "Playground",
            Width = 120f,
            Height = 32f
        };
        var root = new Canvas { Name = "RootCanvas" };
        root.AddChild(button);

        using var host = new InkkOopsTestHost(root);
        string actionDiagnosticsPath;

        using (var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "runner-action-diagnostics-threshold"))
        {
            var session = new InkkOopsSession(host, artifacts);
            var script = new InkkOopsScript("runner-action-diagnostics-threshold")
                .Add(new InkkOopsHoverTargetCommand(new InkkOopsTargetReference("PlaygroundButton")));

            var runner = new InkkOopsScriptRunner();
            var result = await runner.RunAsync(script, session, CancellationToken.None);

            Assert.Equal(InkkOopsRunStatus.Completed, result.Status);
            actionDiagnosticsPath = artifacts.GetPath("action[0].txt");
            Assert.False(File.Exists(actionDiagnosticsPath));
        }

        Assert.False(File.Exists(actionDiagnosticsPath));
    }

    [Fact]
    public async Task Runner_Captures_Only_Selected_Action_Diagnostics()
    {
        var firstButton = new Button
        {
            Name = "FirstButton",
            Content = "First",
            Width = 120f,
            Height = 32f
        };
        var secondButton = new Button
        {
            Name = "SecondButton",
            Content = "Second",
            Width = 120f,
            Height = 32f
        };
        var root = new Canvas { Name = "RootCanvas" };
        root.AddChild(firstButton);
        root.AddChild(secondButton);

        using var host = new InkkOopsTestHost(root);
        string firstActionDiagnosticsPath;
        string secondActionDiagnosticsPath;

        using (var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "runner-action-diagnostics-selected-indexes"))
        {
            var session = new InkkOopsSession(host, artifacts, [1]);
            var script = new InkkOopsScript("runner-action-diagnostics-selected-indexes")
                .Add(new InkkOopsHoverTargetCommand(new InkkOopsTargetReference("FirstButton")))
                .Add(new InkkOopsHoverTargetCommand(new InkkOopsTargetReference("SecondButton")));

            var runner = new InkkOopsScriptRunner();
            var result = await runner.RunAsync(script, session, CancellationToken.None);

            Assert.Equal(InkkOopsRunStatus.Completed, result.Status);
            firstActionDiagnosticsPath = artifacts.GetPath("action[0].txt");
            secondActionDiagnosticsPath = artifacts.GetPath("action[1].txt");
            Assert.False(File.Exists(firstActionDiagnosticsPath));
            Assert.False(File.Exists(secondActionDiagnosticsPath));
        }

        Assert.False(File.Exists(firstActionDiagnosticsPath));
        Assert.True(File.Exists(secondActionDiagnosticsPath));
    }

    [Fact]
    public async Task Runner_Writes_ObjectObserver_Dump_Per_Action()
    {
        var button = new Button
        {
            Name = "PlaygroundButton",
            Content = "Playground",
            Width = 120f,
            Height = 32f
        };
        var root = new Canvas { Name = "RootCanvas" };
        root.AddChild(button);

        using var host = new InkkOopsTestHost(root);
        string observerDumpPath;

        using (var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "runner-object-observer"))
        {
            var session = new InkkOopsSession(host, artifacts, objectObservers: [new TestSizeObserver("PlaygroundButton")]);
            var script = new InkkOopsScript("runner-object-observer")
                .Add(new InkkOopsHoverTargetCommand(new InkkOopsTargetReference("PlaygroundButton")))
                .Add(new InkkOopsClickTargetCommand(new InkkOopsTargetReference("PlaygroundButton")));

            var runner = new InkkOopsScriptRunner();
            var result = await runner.RunAsync(script, session, CancellationToken.None);

            Assert.Equal(InkkOopsRunStatus.Completed, result.Status);
            observerDumpPath = artifacts.GetPath("PlaygroundButtonObserverDump.txt");
            Assert.False(File.Exists(observerDumpPath));
        }

        var dumpLines = File.ReadAllLines(observerDumpPath);
        Assert.Equal(2, dumpLines.Length);
        Assert.Contains("action[0] PlaygroundButton", dumpLines[0], StringComparison.Ordinal);
        Assert.Contains("status=\"resolved\"", dumpLines[0], StringComparison.Ordinal);
        Assert.Contains("width=120", dumpLines[0], StringComparison.Ordinal);
        Assert.Contains("height=32", dumpLines[0], StringComparison.Ordinal);
        Assert.Contains("action[1] PlaygroundButton", dumpLines[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Runner_Writes_Unresolved_ObjectObserver_Dump_When_Target_Is_Missing()
    {
        using var host = new InkkOopsTestHost(new Canvas { Name = "RootCanvas" });
        string observerDumpPath;

        using (var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "runner-object-observer-missing"))
        {
            var session = new InkkOopsSession(host, artifacts, objectObservers: [new TestSizeObserver("MissingButton")]);
            var script = new InkkOopsScript("runner-object-observer-missing")
                .Add(new TestCommand("noop"));

            var runner = new InkkOopsScriptRunner();
            var result = await runner.RunAsync(script, session, CancellationToken.None);

            Assert.Equal(InkkOopsRunStatus.Completed, result.Status);
            observerDumpPath = artifacts.GetPath("MissingButtonObserverDump.txt");
            Assert.False(File.Exists(observerDumpPath));
        }

        var dumpText = File.ReadAllText(observerDumpPath);
        Assert.Contains("action[0] MissingButton", dumpText, StringComparison.Ordinal);
        Assert.Contains("status=\"unresolved\"", dumpText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Runner_Writes_SourceEditor_GutterObserver_Dump_With_Editor_And_Gutter_Metrics()
    {
        var editor = new IDE_Editor
        {
            Name = "SourceEditor",
            Width = 320f,
            Height = 160f,
            FontSize = 16f,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        DocumentEditing.ReplaceAllText(editor.Document, string.Join("\n", Enumerable.Range(1, 40).Select(static index => $"Line {index:000}")));

        var root = new Canvas { Name = "RootCanvas" };
        root.AddChild(editor);

        using var host = new InkkOopsTestHost(root, width: 480, height: 260);
        string observerDumpPath;

        using (var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "runner-source-editor-observer"))
        {
            var session = new InkkOopsSession(host, artifacts, objectObservers: [new SourceEditorGutterObjectObserver()]);
            var script = new InkkOopsScript("runner-source-editor-observer")
                .Add(new InkkOopsScrollToCommand(new InkkOopsTargetReference("SourceEditor"), 0f, 60f))
                .Add(new TestCommand("noop"));

            var runner = new InkkOopsScriptRunner();
            var result = await runner.RunAsync(script, session, CancellationToken.None);

            Assert.Equal(InkkOopsRunStatus.Completed, result.Status);
            observerDumpPath = artifacts.GetPath("SourceEditorObserverDump.txt");
            Assert.False(File.Exists(observerDumpPath));
        }

        var dumpLines = File.ReadAllLines(observerDumpPath);
        Assert.Equal(2, dumpLines.Length);
        Assert.Contains("action[0] SourceEditor", dumpLines[0], StringComparison.Ordinal);
        Assert.Contains("elementType=\"IDE_Editor\"", dumpLines[0], StringComparison.Ordinal);
        Assert.Contains("estimatedLineHeight=", dumpLines[0], StringComparison.Ordinal);
        Assert.Contains("gutterLineHeight=", dumpLines[0], StringComparison.Ordinal);
        Assert.Contains("gutterVerticalLineOffset=", dumpLines[0], StringComparison.Ordinal);
        Assert.Contains("gutterFirstVisibleLine=", dumpLines[0], StringComparison.Ordinal);
        Assert.Contains("extentHeightPerLine=", dumpLines[0], StringComparison.Ordinal);
        Assert.Contains("hasCaretBounds=", dumpLines[0], StringComparison.Ordinal);
        Assert.Contains("action[1] SourceEditor", dumpLines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void ObjectObserverParser_Resolves_SourceEditorGutter_Alias()
    {
        var observer = Assert.Single(InkkOopsObjectObserverParser.Parse("source-editor-gutter"));

        var typedObserver = Assert.IsType<SourceEditorGutterObjectObserver>(observer);
        Assert.Equal("SourceEditor", typedObserver.TargetName);
    }

    [Fact]
    public void ObjectObserverParser_Rejects_Unknown_Observer_Name()
    {
        var exception = Assert.Throws<ArgumentException>(() => InkkOopsObjectObserverParser.Parse("missing-observer"));

        Assert.Contains("Unknown InkkOops object observer", exception.Message, StringComparison.Ordinal);
        Assert.Contains("source-editor-gutter", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PipeMessages_RoundTrip_Through_Json()
    {
        var request = new InkkOopsPipeRequest
        {
            ScriptName = "buttons-resize-hover-repro",
            ActionDiagnosticsIndexes = [1, 3, 5],
            TimeoutMilliseconds = 5000,
            ArtifactRootOverride = "artifacts/custom"
        };
        var response = new InkkOopsPipeResponse
        {
            Status = InkkOopsRunStatus.Completed.ToString(),
            ScriptName = request.ScriptName,
            ArtifactDirectory = "artifacts/inkkoops/run-1",
            Message = string.Empty
        };

        var requestJson = System.Text.Json.JsonSerializer.Serialize(request);
        var responseJson = System.Text.Json.JsonSerializer.Serialize(response);
        var roundTripRequest = System.Text.Json.JsonSerializer.Deserialize<InkkOopsPipeRequest>(requestJson);
        var roundTripResponse = System.Text.Json.JsonSerializer.Deserialize<InkkOopsPipeResponse>(responseJson);

        Assert.NotNull(roundTripRequest);
        Assert.NotNull(roundTripResponse);
        Assert.Equal(request.ScriptName, roundTripRequest!.ScriptName);
        Assert.Equal(request.ActionDiagnosticsIndexes, roundTripRequest.ActionDiagnosticsIndexes);
        Assert.Equal(response.ArtifactDirectory, roundTripResponse!.ArtifactDirectory);
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

    private sealed class TestSizeObserver : InkkOopsObjectObserver
    {
        public TestSizeObserver(string targetName)
            : base(targetName)
        {
        }

        protected override void Observe(InkkOopsObjectObserverContext context, UIElement element, InkkOopsObjectObserverDumpBuilder builder)
        {
            _ = context;
            if (element is not FrameworkElement frameworkElement)
            {
                builder.Add("elementType", element.GetType().Name);
                return;
            }

            builder.Add("width", frameworkElement.Width);
            builder.Add("height", frameworkElement.Height);
            builder.Add("isVisible", frameworkElement.IsVisible);
        }
    }
}
