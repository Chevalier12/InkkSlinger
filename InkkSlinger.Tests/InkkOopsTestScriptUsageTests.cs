using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class InkkOopsTestScriptUsageTests
{
    private static readonly TimeSpan RuntimeProcessTimeout = TimeSpan.FromSeconds(90);
    private const string SidebarScrollViewerName = "CatalogSidebarScrollViewer";
    private const string ButtonHostName = "ControlButtonsHost";
    private const string CalendarPreviousMonthButtonName = "CalendarPreviousMonthButton";
    private const string CalendarNextMonthButtonName = "CalendarNextMonthButton";
    private const string GridSplitterViewRootName = "GridSplitterViewRootGrid";
    private const string GridSplitterWorkbenchScrollViewerName = "GridSplitterWorkbenchScrollViewer";
    private const string NavigationSplitterName = "NavigationSplitter";
    private const string PreviewDockSplitterName = "PreviewDockSplitter";
    private const string PreviewSourceSplitterName = "PreviewSourceSplitter";
    private const string SourceEditorName = "SourceEditor";
    private const string SourcePropertyInspectorSplitterName = "SourcePropertyInspectorSplitter";
    private const string SourcePropertyInspectorScrollViewerName = "SourcePropertyInspectorScrollViewer";

    [Fact]
    public async Task RuntimePlayback_Opens_Real_App_And_Writes_Result()
    {
        var runtimeRoot = Path.Combine(Path.GetTempPath(), $"inkkoops-runtime-replay-{Guid.NewGuid():N}");
        var artifactsRoot = Path.Combine(runtimeRoot, "artifacts");
        var recordingPath = Path.Combine(runtimeRoot, "recording.json");
        Directory.CreateDirectory(runtimeRoot);
        Directory.CreateDirectory(artifactsRoot);

        try
        {
            File.WriteAllText(
                recordingPath,
                """
                {
                  "actions": [
                    { "kind": 0, "frameCount": 2 }
                  ]
                }
                """);

            var runDirectory = await RunRuntimeRecordingAsync(recordingPath, artifactsRoot);
            var resultPath = Path.Combine(runDirectory, "result.json");

            Assert.True(File.Exists(resultPath));

            var resultJson = File.ReadAllText(resultPath);
            Assert.Contains("\"status\": \"Completed\"", resultJson);
            Assert.Contains("\"scriptName\": \"recording-playback\"", resultJson);
        }
        finally
        {
            if (Directory.Exists(runtimeRoot))
            {
                Directory.Delete(runtimeRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RuntimePlayback_Accepts_RecordedSession_Directory_Path()
    {
        var runtimeRoot = Path.Combine(Path.GetTempPath(), $"inkkoops-runtime-replay-dir-{Guid.NewGuid():N}");
        var recordingDirectory = Path.Combine(runtimeRoot, "recorded-session");
        var artifactsRoot = Path.Combine(runtimeRoot, "artifacts");
        Directory.CreateDirectory(recordingDirectory);
        Directory.CreateDirectory(artifactsRoot);

        try
        {
            File.WriteAllText(
                Path.Combine(recordingDirectory, "recording.json"),
                """
                {
                  "actions": [
                    { "kind": 0, "frameCount": 2 }
                  ]
                }
                """);

            var runDirectory = await RunRuntimeRecordingAsync(recordingDirectory, artifactsRoot);
            var resultPath = Path.Combine(runDirectory, "result.json");

            Assert.True(File.Exists(resultPath));

            var resultJson = File.ReadAllText(resultPath);
            Assert.Contains("\"status\": \"Completed\"", resultJson);
            Assert.Contains("\"scriptName\": \"recording-playback\"", resultJson);
        }
        finally
        {
            if (Directory.Exists(runtimeRoot))
            {
                Directory.Delete(runtimeRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RuntimePlayback_Writes_ActionLogText_Next_To_Inkkr_File()
    {
        var runtimeRoot = Path.Combine(Path.GetTempPath(), $"inkkoops-runtime-replay-inkkr-{Guid.NewGuid():N}");
        var recordingDirectory = Path.Combine(runtimeRoot, "recorded-session");
        var artifactsRoot = Path.Combine(runtimeRoot, "artifacts");
        var recordingPath = Path.Combine(recordingDirectory, "recording.inkkr");
        Directory.CreateDirectory(recordingDirectory);
        Directory.CreateDirectory(artifactsRoot);

        try
        {
            File.WriteAllText(
                recordingPath,
                """
                {
                  "actions": [
                    { "kind": 0, "frameCount": 2 }
                  ]
                }
                """);

            var runDirectory = await RunRuntimeRecordingAsync(recordingPath, artifactsRoot);
            var mirroredActionLogPath = Path.Combine(recordingDirectory, "actionlog.txt");

            Assert.True(File.Exists(Path.Combine(runDirectory, "action.log")));
            Assert.True(File.Exists(mirroredActionLogPath));

            var mirroredActionLog = File.ReadAllText(mirroredActionLogPath);
            Assert.Contains("action[0]", mirroredActionLog);
            Assert.Contains("wait frames", mirroredActionLog);
        }
        finally
        {
            if (Directory.Exists(runtimeRoot))
            {
                Directory.Delete(runtimeRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CliPlayback_Uses_Recorded_Project_Path_From_Recording()
    {
        var runtimeRoot = Path.Combine(Path.GetTempPath(), $"inkkoops-cli-replay-{Guid.NewGuid():N}");
        var artifactsRoot = Path.Combine(runtimeRoot, "artifacts");
        var recordingPath = Path.Combine(runtimeRoot, "recording.inkkr");
        Directory.CreateDirectory(runtimeRoot);
        Directory.CreateDirectory(artifactsRoot);

        try
        {
            var repositoryRoot = FindRepositoryRoot();
            var demoProjectPath = Path.Combine(repositoryRoot, "InkkSlinger.DemoApp", "InkkSlinger.DemoApp.csproj");
            File.WriteAllText(
                recordingPath,
                $$"""
                {
                  "recordedProjectPath": "{{demoProjectPath.Replace("\\", "\\\\")}}",
                  "actions": [
                    { "kind": 0, "frameCount": 2 }
                  ]
                }
                """);

            var cliProjectPath = Path.Combine(repositoryRoot, "InkkOops.Cli", "InkkOops.Cli.csproj");
            await RunDotNetProcessAsync(
                repositoryRoot,
                $"run --project \"{cliProjectPath}\" --no-restore -- \"{recordingPath}\" --artifacts \"{artifactsRoot}\"",
                "CLI playback launch failed.");

            var runDirectory = Assert.Single(Directory.GetDirectories(artifactsRoot));
            var resultJson = File.ReadAllText(Path.Combine(runDirectory, "result.json"));

            Assert.Contains("\"status\": \"Completed\"", resultJson);
            Assert.Contains("\"scriptName\": \"recording-playback\"", resultJson);
        }
        finally
        {
            if (Directory.Exists(runtimeRoot))
            {
                Directory.Delete(runtimeRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RuntimeRun_Calendar_Month_Navigation_Path_Passes_And_Preserves_Artifacts()
    {
        var artifactsRoot = CreatePreservedArtifactsRoot("runtime-calendar-month-navigation-fps-drop");
        var runDirectory = await RunRuntimeScenarioFromTestAssemblyAsync(
            "runtime-calendar-month-navigation-fps-drop-scenario",
            artifactsRoot);

        var resultJson = File.ReadAllText(Path.Combine(runDirectory, "result.json"));
        var actionLogPath = Path.Combine(runDirectory, "action.log");
        var actionLog = File.ReadAllText(actionLogPath);

        Assert.Contains("\"status\": \"Completed\"", resultJson);
        Assert.Contains("\"scriptName\": \"runtime-calendar-month-navigation-fps-drop-scenario\"", resultJson);
        Assert.True(File.Exists(actionLogPath));
        Assert.Contains(CalendarNextMonthButtonName, actionLog);
        Assert.Contains(CalendarPreviousMonthButtonName, actionLog);
    }

    [Fact]
    public async Task RuntimeScenario_ApplicationMainWindowWidth_Changes_Live_App_WindowSize()
    {
        var runtimeRoot = Path.Combine(Path.GetTempPath(), $"inkkoops-runtime-mainwindow-width-{Guid.NewGuid():N}");
        var artifactsRoot = Path.Combine(runtimeRoot, "artifacts");
        Directory.CreateDirectory(runtimeRoot);
        Directory.CreateDirectory(artifactsRoot);

        try
        {
            var runDirectory = await RunRuntimeScenarioFromTestAssemblyAsync(
                "runtime-test-mainwindow-width-scenario",
                artifactsRoot,
                new Dictionary<string, string>
                {
                    ["INKKSLINGER_TEST_MAINWINDOW_WIDTH"] = "1377"
                });

            var resultJson = File.ReadAllText(Path.Combine(runDirectory, "result.json"));
            var telemetryPath = Path.Combine(runDirectory, "mainwindow-width.txt");
            var telemetry = File.ReadAllText(telemetryPath);

            Assert.Contains("\"status\": \"Completed\"", resultJson);
            Assert.True(File.Exists(telemetryPath));
            Assert.Contains("window_client=1377x", telemetry);
            Assert.Contains("window_backbuffer=1377x", telemetry);
            Assert.Contains("viewport=1377x", telemetry);
        }
        finally
        {
            if (Directory.Exists(runtimeRoot))
            {
                Directory.Delete(runtimeRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RuntimeRun_Designer_PreviewDock_Then_Source_Splitter_Drag_Path_Completes_Without_Hanging()
    {
        var artifactsRoot = CreatePreservedArtifactsRoot("runtime-designer-preview-dock-then-source-splitter-drag-hang");
        var runDirectory = await RunRuntimeScenarioFromTestAssemblyAsync(
            "runtime-designer-preview-dock-then-source-splitter-drag-hang-scenario",
            GetDesignerProjectPath(),
            artifactsRoot);

        var resultJson = File.ReadAllText(Path.Combine(runDirectory, "result.json"));
        var actionLogPath = Path.Combine(runDirectory, "action.log");
        var actionLog = File.ReadAllText(actionLogPath);

        Assert.Contains("\"status\": \"Completed\"", resultJson);
        Assert.Contains("\"scriptName\": \"runtime-designer-preview-dock-then-source-splitter-drag-hang-scenario\"", resultJson);
        Assert.True(File.Exists(actionLogPath));
        Assert.Contains(PreviewDockSplitterName, actionLog);
        Assert.Contains(PreviewSourceSplitterName, actionLog);
    }

    [Fact]
    public async Task RuntimeRun_Designer_PreviewSourceSplitter_Repeated_Vertical_Drag_Fps_Drop_Path_Passes_And_Preserves_Artifacts()
    {
        var artifactsRoot = CreatePreservedArtifactsRoot("runtime-designer-preview-source-splitter-repeated-vertical-drag-fps-drop");
        var runDirectory = await RunRuntimeScenarioFromTestAssemblyAllowCompletedArtifactsAsync(
            "runtime-designer-preview-source-splitter-repeated-vertical-drag-fps-drop-scenario",
            GetDesignerProjectPath(),
            artifactsRoot);

        var resultJson = File.ReadAllText(Path.Combine(runDirectory, "result.json"));
        var actionLogPath = Path.Combine(runDirectory, "action.log");
        var actionLog = File.ReadAllText(actionLogPath);

        Assert.Contains("\"status\": \"Completed\"", resultJson);
        Assert.Contains("\"scriptName\": \"runtime-designer-preview-source-splitter-repeated-vertical-drag-fps-drop-scenario\"", resultJson);
        Assert.True(File.Exists(actionLogPath));
        Assert.Contains(PreviewSourceSplitterName, actionLog);
    }

    [Fact]
    public async Task RuntimeRun_Designer_SourceEditor_CtrlSpace_Completion_Fps_Drop_Path_Passes_And_Preserves_Artifacts()
    {
        var artifactsRoot = CreatePreservedArtifactsRoot("runtime-designer-source-editor-ctrl-space-completion-fps-drop");
        var runDirectory = await RunRuntimeScenarioFromTestAssemblyAllowCompletedArtifactsAsync(
            "runtime-designer-source-editor-ctrl-space-completion-fps-drop-scenario",
            GetDesignerProjectPath(),
            artifactsRoot);

        var resultJson = File.ReadAllText(Path.Combine(runDirectory, "result.json"));
        var actionLogPath = Path.Combine(runDirectory, "action.log");
        var actionLog = File.ReadAllText(actionLogPath);

        Assert.Contains("\"status\": \"Completed\"", resultJson);
        Assert.Contains("\"scriptName\": \"runtime-designer-source-editor-ctrl-space-completion-fps-drop-scenario\"", resultJson);
        Assert.True(File.Exists(actionLogPath));
        Assert.Contains(SourceEditorName, actionLog);
        Assert.Contains("TextInput(<)", actionLog);
        Assert.Contains("KeyDown(LeftControl)", actionLog);
        Assert.Contains("KeyDown(Space)", actionLog);
        Assert.Contains("KeyUp(Space)", actionLog);
        Assert.True(
            TryFindLoggedFpsForAction(actionLog, "KeyDown(Space)", out var ctrlSpaceFps),
            "Action log did not contain an fps entry for KeyDown(Space).");
        Assert.True(ctrlSpaceFps >= 0d, "Ctrl+Space fps entry should be non-negative.");
    }

    [Fact]
    public async Task RuntimeRun_Designer_SourcePropertyInspector_Resize_Then_Maximize_Preserves_Repro_Artifacts()
    {
        var artifactsRoot = CreatePreservedArtifactsRoot("runtime-designer-source-property-inspector-resize-then-maximize");
        var runDirectory = await RunRuntimeScenarioFromTestAssemblyAllowCompletedArtifactsAsync(
            "runtime-designer-source-property-inspector-resize-then-maximize-repro-scenario",
            GetDesignerProjectPath(),
            artifactsRoot);

        var resultJson = File.ReadAllText(Path.Combine(runDirectory, "result.json"));
        var actionLogPath = Path.Combine(runDirectory, "action.log");
        var actionLog = File.ReadAllText(actionLogPath);

        Assert.Contains("\"status\": \"Completed\"", resultJson);
        Assert.Contains("\"scriptName\": \"runtime-designer-source-property-inspector-resize-then-maximize-repro-scenario\"", resultJson);
        Assert.True(File.Exists(actionLogPath));
        Assert.Contains(SourceEditorName, actionLog);
        Assert.Contains(SourcePropertyInspectorSplitterName, actionLog);
        Assert.Contains(PreviewSourceSplitterName, actionLog);
        Assert.Contains("window", actionLog, StringComparison.Ordinal);
        Assert.Contains("- action[99] maximize", actionLog, StringComparison.Ordinal);

        Assert.Contains(
            Directory.GetFiles(runDirectory, "*.png").Select(Path.GetFileName),
            fileName => fileName != null && fileName.Contains("source-property-inspector-after-maximize", StringComparison.Ordinal));
        Assert.Contains(
            Directory.GetFiles(runDirectory, "*.txt").Select(Path.GetFileName),
            fileName => fileName != null && fileName.Contains("source-property-inspector-after-maximize", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RuntimeRun_Designer_ColorPicker_FirstClick_FrameEvidence_Preserves_Repro_Artifacts()
    {
        var artifactsRoot = CreatePreservedArtifactsRoot("runtime-designer-color-picker-first-click-frame-evidence");
        var runDirectory = await RunRuntimeScenarioFromTestAssemblyAllowCompletedArtifactsAsync(
            "runtime-designer-color-picker-first-click-frame-evidence-scenario",
            GetDesignerProjectPath(),
            artifactsRoot);

        var resultJson = File.ReadAllText(Path.Combine(runDirectory, "result.json"));
        var actionLogPath = Path.Combine(runDirectory, "action.log");
        var actionLog = File.ReadAllText(actionLogPath);
        var pngFiles = Directory.GetFiles(runDirectory, "*.png").Select(Path.GetFileName).ToArray();

        Assert.Contains("\"status\": \"Completed\"", resultJson);
        Assert.Contains("\"scriptName\": \"runtime-designer-color-picker-first-click-frame-evidence-scenario\"", resultJson);
        Assert.True(File.Exists(actionLogPath));
        Assert.Contains("capture frame", actionLog, StringComparison.Ordinal);
        Assert.Contains(pngFiles, fileName => string.Equals(fileName, "color-picker-before-action-303.png", StringComparison.Ordinal));
        Assert.Contains(pngFiles, fileName => string.Equals(fileName, "color-picker-after-action-303.png", StringComparison.Ordinal));
        Assert.Contains(pngFiles, fileName => string.Equals(fileName, "color-picker-after-action-304.png", StringComparison.Ordinal));
        Assert.Contains(pngFiles, fileName => string.Equals(fileName, "color-picker-after-action-305.png", StringComparison.Ordinal));
        Assert.Contains(pngFiles, fileName => string.Equals(fileName, "color-picker-after-action-306.png", StringComparison.Ordinal));
    }

    private static async Task<string> RunRuntimeScenarioFromTestAssemblyAsync(string scriptName, string artifactsRoot)
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repositoryRoot, "InkkSlinger.DemoApp", "InkkSlinger.DemoApp.csproj");
        return await RunRuntimeScenarioFromTestAssemblyAsync(scriptName, projectPath, artifactsRoot, environmentVariables: null);
    }

    private static async Task<string> RunRuntimeScenarioFromTestAssemblyAsync(
        string scriptName,
        string artifactsRoot,
        IReadOnlyDictionary<string, string>? environmentVariables)
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repositoryRoot, "InkkSlinger.DemoApp", "InkkSlinger.DemoApp.csproj");
        return await RunRuntimeScenarioFromTestAssemblyAsync(scriptName, projectPath, artifactsRoot, environmentVariables);
    }

    private static async Task<string> RunRuntimeScenarioFromTestAssemblyAsync(
        string scriptName,
        string projectPath,
        string artifactsRoot,
        IReadOnlyDictionary<string, string>? environmentVariables = null)
    {
        var repositoryRoot = FindRepositoryRoot();
        var testAssemblyPath = typeof(InkkOopsTestScriptUsageTests).Assembly.Location;
        await RunDotNetProcessAsync(
            repositoryRoot,
            $"run --project \"{projectPath}\" --no-restore -- --inkkoops-script-assembly \"{testAssemblyPath}\" --inkkoops-script \"{scriptName}\" --inkkoops-artifacts \"{artifactsRoot}\"",
            "Runtime scenario launch failed.",
            environmentVariables);

        return Assert.Single(Directory.GetDirectories(artifactsRoot));
    }

    private static async Task<string> RunRuntimeScenarioFromTestAssemblyAllowCompletedArtifactsAsync(
        string scriptName,
        string projectPath,
        string artifactsRoot)
    {
        var repositoryRoot = FindRepositoryRoot();
        var testAssemblyPath = typeof(InkkOopsTestScriptUsageTests).Assembly.Location;
        var arguments =
            $"run --project \"{projectPath}\" --no-restore -- --inkkoops-script-assembly \"{testAssemblyPath}\" --inkkoops-script \"{scriptName}\" --inkkoops-artifacts \"{artifactsRoot}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        var timeoutAtUtc = DateTime.UtcNow + RuntimeProcessTimeout;
        while (DateTime.UtcNow < timeoutAtUtc)
        {
            if (TryFindCompletedRunDirectory(artifactsRoot, scriptName, out var completedRunDirectory))
            {
                TryTerminateProcess(process);
                return completedRunDirectory;
            }

            if (process.HasExited)
            {
                var stdout = await process.StandardOutput.ReadToEndAsync();
                var stderr = await process.StandardError.ReadToEndAsync();

                Assert.True(
                    process.ExitCode == 0,
                    $"Runtime scenario launch failed.{Environment.NewLine}ExitCode: {process.ExitCode}{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}");

                return Assert.Single(Directory.GetDirectories(artifactsRoot));
            }

            await Task.Delay(200);
        }

        if (TryFindCompletedRunDirectory(artifactsRoot, scriptName, out var timedOutCompletedRunDirectory))
        {
            TryTerminateProcess(process);
            return timedOutCompletedRunDirectory;
        }

        var timedOutStdout = await process.StandardOutput.ReadToEndAsync();
        var timedOutStderr = await process.StandardError.ReadToEndAsync();
        TryTerminateProcess(process);
        Assert.Fail(
            $"Runtime scenario launch failed.{Environment.NewLine}Process timed out after {RuntimeProcessTimeout.TotalSeconds:0} seconds.{Environment.NewLine}Arguments: {arguments}{Environment.NewLine}STDOUT:{Environment.NewLine}{timedOutStdout}{Environment.NewLine}STDERR:{Environment.NewLine}{timedOutStderr}");
        return string.Empty;
    }

    private static async Task<string> RunRuntimeRecordingAsync(string recordingPath, string artifactsRoot)
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repositoryRoot, "InkkSlinger.DemoApp", "InkkSlinger.DemoApp.csproj");

        await RunDotNetProcessAsync(
            repositoryRoot,
            $"run --project \"{projectPath}\" --no-restore -- --inkkoops-recording \"{recordingPath}\" --inkkoops-artifacts \"{artifactsRoot}\"",
            "Runtime replay launch failed.");

        return Assert.Single(Directory.GetDirectories(artifactsRoot));
    }

    private static async Task RunDotNetProcessAsync(string workingDirectory, string arguments, string failurePrefix)
    {
        await RunDotNetProcessAsync(workingDirectory, arguments, failurePrefix, environmentVariables: null);
    }

    private static async Task RunDotNetProcessAsync(
        string workingDirectory,
        string arguments,
        string failurePrefix,
        IReadOnlyDictionary<string, string>? environmentVariables)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if (environmentVariables != null)
        {
            foreach (var pair in environmentVariables)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }
        }

        using var process = Process.Start(startInfo);

        Assert.NotNull(process);

        string stdout;
        string stderr;

        try
        {
            using var timeout = new CancellationTokenSource(RuntimeProcessTimeout);
            await process.WaitForExitAsync(timeout.Token);
            stdout = await process.StandardOutput.ReadToEndAsync(timeout.Token);
            stderr = await process.StandardError.ReadToEndAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            stdout = await process.StandardOutput.ReadToEndAsync();
            stderr = await process.StandardError.ReadToEndAsync();

            Assert.Fail(
                $"{failurePrefix}{Environment.NewLine}Process timed out after {RuntimeProcessTimeout.TotalSeconds:0} seconds.{Environment.NewLine}Arguments: {arguments}{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}");
            return;
        }

        Assert.True(
            process.ExitCode == 0,
            $"{failurePrefix}{Environment.NewLine}ExitCode: {process.ExitCode}{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}");
    }

    private static string CreatePreservedArtifactsRoot(string scenarioSlug)
    {
        var repositoryRoot = FindRepositoryRoot();
        var artifactsRoot = Path.Combine(
            repositoryRoot,
            "artifacts",
            "inkkoops-runtime-runs",
            scenarioSlug,
            $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(artifactsRoot);
        return artifactsRoot;
    }

    private static string GetDesignerProjectPath()
    {
        var repositoryRoot = FindRepositoryRoot();
        return Path.Combine(repositoryRoot, "InkkSlinger.Designer", "InkkSlinger.Designer.csproj");
    }

    private static string GetPhase4ColorPickerRecordingPath()
    {
        return Path.GetFullPath(Path.Combine(
            Environment.CurrentDirectory,
            "artifacts",
            "inkkoops-recordings",
            "20260419-130339449-recorded-session",
            "recording.json"));
    }

    private static IReadOnlyList<InkkOopsInteractionRecorder.RecordedAction> LoadRecordedActions(string recordingPath)
    {
        if (!File.Exists(recordingPath))
        {
            throw new FileNotFoundException($"Could not find phase 4 recording at '{recordingPath}'.", recordingPath);
        }

        var json = File.ReadAllText(recordingPath);
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("actions", out var actionsElement) || actionsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Recording '{recordingPath}' does not contain an actions array.");
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var actions = new List<InkkOopsInteractionRecorder.RecordedAction>();
        foreach (var actionElement in actionsElement.EnumerateArray())
        {
            var action = actionElement.Deserialize<InkkOopsInteractionRecorder.RecordedAction>(options);
            if (action == null)
            {
                throw new InvalidOperationException($"Could not deserialize a recorded action from '{recordingPath}'.");
            }

            actions.Add(action);
        }

        return actions;
    }

    private static void AppendRecordedAction(InkkOopsScriptBuilder builder, InkkOopsInteractionRecorder.RecordedAction action)
    {
        switch (action.Kind)
        {
            case InkkOopsInteractionRecorder.RecordedActionKind.WaitFrames:
                builder.WaitFrames(action.FrameCount ?? 1);
                break;
            case InkkOopsInteractionRecorder.RecordedActionKind.ResizeWindow:
                builder.ResizeWindow(action.Width ?? 1, action.Height ?? 1);
                break;
            case InkkOopsInteractionRecorder.RecordedActionKind.MovePointer:
                builder.MovePointer(new System.Numerics.Vector2(action.X ?? 0, action.Y ?? 0));
                break;
            case InkkOopsInteractionRecorder.RecordedActionKind.PointerDown:
                builder.PointerDown(new System.Numerics.Vector2(action.X ?? 0, action.Y ?? 0));
                break;
            case InkkOopsInteractionRecorder.RecordedActionKind.PointerUp:
                builder.PointerUp(new System.Numerics.Vector2(action.X ?? 0, action.Y ?? 0));
                break;
            case InkkOopsInteractionRecorder.RecordedActionKind.Wheel:
                builder.Wheel(action.WheelDelta ?? 0);
                break;
            case InkkOopsInteractionRecorder.RecordedActionKind.KeyDown:
                if (action.Key is Keys keyDown)
                {
                    builder.KeyDown(keyDown);
                }
                break;
            case InkkOopsInteractionRecorder.RecordedActionKind.KeyUp:
                if (action.Key is Keys keyUp)
                {
                    builder.KeyUp(keyUp);
                }
                break;
            case InkkOopsInteractionRecorder.RecordedActionKind.TextInput:
                if (action.Character is char character)
                {
                    builder.TextInput(character);
                }
                break;
            default:
                throw new InvalidOperationException($"Unsupported recorded action kind '{action.Kind}'.");
        }
    }

    private static bool TryFindLoggedFpsForAction(string actionLog, string actionDescription, out double fps)
    {
        fps = -1d;

        using var reader = new StringReader(actionLog);
        while (reader.ReadLine() is { } line)
        {
            if (!line.Contains(actionDescription, StringComparison.Ordinal))
            {
                continue;
            }

            var fpsIndex = line.IndexOf("fps=", StringComparison.Ordinal);
            if (fpsIndex < 0)
            {
                continue;
            }

            fpsIndex += 4;
            var fpsEndIndex = line.IndexOf(' ', fpsIndex);
            var fpsText = fpsEndIndex >= 0
                ? line.Substring(fpsIndex, fpsEndIndex - fpsIndex)
                : line.Substring(fpsIndex);

            if (double.TryParse(fpsText, NumberStyles.Float, CultureInfo.InvariantCulture, out fps))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindCompletedRunDirectory(string artifactsRoot, string scriptName, out string runDirectory)
    {
        runDirectory = string.Empty;

        if (!Directory.Exists(artifactsRoot))
        {
            return false;
        }

        var candidateDirectories = Directory.GetDirectories(artifactsRoot);
        if (candidateDirectories.Length != 1)
        {
            return false;
        }

        var resultPath = Path.Combine(candidateDirectories[0], "result.json");
        if (!File.Exists(resultPath))
        {
            return false;
        }

        var resultJson = File.ReadAllText(resultPath);
        if (!resultJson.Contains("\"status\": \"Completed\"", StringComparison.Ordinal) ||
            !resultJson.Contains($"\"scriptName\": \"{scriptName}\"", StringComparison.Ordinal))
        {
            return false;
        }

        runDirectory = candidateDirectories[0];
        return true;
    }

    private static void TryTerminateProcess(Process process)
    {
        if (process.HasExited)
        {
            return;
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "InkkSlinger.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root for the InkkOops runtime launch test.");
    }

    public sealed class RuntimeGridSplitterScenario : InkkOopsRuntimeScenario
    {
        public override string Name => "runtime-test-gridsplitter-scenario";

        protected override void Build(InkkOopsScriptBuilder builder)
        {
            var gridSplitterButton = InkkOopsTargetSelector.Within(
                InkkOopsTargetSelector.Name(ButtonHostName),
                InkkOopsTargetSelector.AutomationName("GridSplitter").WithIndex(0));

            builder
                .WaitForElement(SidebarScrollViewerName)
                .ScrollIntoView(InkkOopsTargetSelector.Name(SidebarScrollViewerName), gridSplitterButton, padding: 12f)
                .Click(gridSplitterButton)
                .WaitForElement(GridSplitterViewRootName)
                .Hover(gridSplitterButton, dwellFrames: 1);
        }
    }

    public sealed class RuntimeNavigationSplitterDragScenario : InkkOopsRuntimeScenario
    {
        public override string Name => "runtime-test-navigation-splitter-drag-scenario";

        protected override void Build(InkkOopsScriptBuilder builder)
        {
            var gridSplitterButton = InkkOopsTargetSelector.Within(
                InkkOopsTargetSelector.Name(ButtonHostName),
                InkkOopsTargetSelector.AutomationName("GridSplitter").WithIndex(0));

            builder
                .WaitForElement(SidebarScrollViewerName)
                .ScrollIntoView(InkkOopsTargetSelector.Name(SidebarScrollViewerName), gridSplitterButton, padding: 12f)
                .Click(gridSplitterButton)
                .WaitForElement(GridSplitterViewRootName)
                .WaitFrames(10)
                .ScrollIntoView(
                    InkkOopsTargetSelector.Name(GridSplitterWorkbenchScrollViewerName),
                    InkkOopsTargetSelector.Name(NavigationSplitterName),
                    padding: 24f)
                .WaitForInteractive(NavigationSplitterName)
                .Drag(NavigationSplitterName, 140f, 0f)
                .Drag(NavigationSplitterName, -220f, 0f)
                .Drag(NavigationSplitterName, 180f, 0f)
                .Drag(NavigationSplitterName, -120f, 0f);
        }
    }

    public sealed class RuntimeGridSplitterSidebarDragFpsDropScenario : InkkOopsRuntimeScenario
    {
        public override string Name => "runtime-grid-splitter-sidebar-drag-fps-drop-scenario";

        protected override IEnumerable<int>? ActionDiagnosticsIndexes => [10, 11, 12, 13, 14];

        protected override void Build(InkkOopsScriptBuilder builder)
        {
            var gridSplitterButton = InkkOopsTargetSelector.Within(
                InkkOopsTargetSelector.Name(ButtonHostName),
                InkkOopsTargetSelector.AutomationName("GridSplitter").WithIndex(0));
            var dragMotion = InkkOopsPointerMotion.WithTravelFrames(14, stepDistance: 8f);

            builder
                .ResizeWindow(1600, 960)
                .WaitFrames(6)
                .WaitForElement(SidebarScrollViewerName)
                .ScrollIntoView(InkkOopsTargetSelector.Name(SidebarScrollViewerName), gridSplitterButton, padding: 12f)
                .WaitForInteractive(gridSplitterButton)
                .Click(gridSplitterButton)
                .WaitForElement(GridSplitterViewRootName)
                .WaitFrames(10)
                .ScrollIntoView(
                    InkkOopsTargetSelector.Name(GridSplitterWorkbenchScrollViewerName),
                    InkkOopsTargetSelector.Name(NavigationSplitterName),
                    padding: 24f)
                .WaitForInteractive(NavigationSplitterName)
                .Drag(NavigationSplitterName, 180f, 0f, InkkOopsPointerAnchor.Center, MouseButton.Left, dragMotion)
                .Drag(NavigationSplitterName, -240f, 0f, InkkOopsPointerAnchor.Center, MouseButton.Left, dragMotion)
                .Drag(NavigationSplitterName, 220f, 0f, InkkOopsPointerAnchor.Center, MouseButton.Left, dragMotion)
                .Drag(NavigationSplitterName, -200f, 0f, InkkOopsPointerAnchor.Center, MouseButton.Left, dragMotion)
                .Drag(NavigationSplitterName, 160f, 0f, InkkOopsPointerAnchor.Center, MouseButton.Left, dragMotion)
                .WaitFrames(12);
        }
    }

    public sealed class RuntimeCalendarMonthNavigationFpsDropScenario : InkkOopsRuntimeScenario
    {
        public override string Name => "runtime-calendar-month-navigation-fps-drop-scenario";

        protected override IEnumerable<int>? ActionDiagnosticsIndexes => [7, 8, 9, 10, 11];

        protected override void Build(InkkOopsScriptBuilder builder)
        {
            var calendarButton = InkkOopsTargetSelector.Within(
                InkkOopsTargetSelector.Name(ButtonHostName),
                InkkOopsTargetSelector.AutomationName("Calendar").WithIndex(0));
            var clickMotion = InkkOopsPointerMotion.WithTravelFrames(8, stepDistance: 5f);

            builder
                .ResizeWindow(1600, 960)
                .WaitFrames(6)
                .WaitForElement(SidebarScrollViewerName)
                .ScrollIntoView(InkkOopsTargetSelector.Name(SidebarScrollViewerName), calendarButton, padding: 12f)
                .WaitForInteractive(calendarButton)
                .Click(calendarButton)
                .WaitForInteractive(CalendarNextMonthButtonName)
                .Click(CalendarNextMonthButtonName, InkkOopsPointerAnchor.Center, clickMotion)
                .WaitFrames(6)
                .Click(CalendarNextMonthButtonName, InkkOopsPointerAnchor.Center, clickMotion)
                .WaitFrames(6)
                .Click(CalendarPreviousMonthButtonName, InkkOopsPointerAnchor.Center, clickMotion)
                .WaitFrames(12);
        }
    }

    public sealed class RuntimeMainWindowWidthScenario : InkkOopsRuntimeScenario
    {
        public override string Name => "runtime-test-mainwindow-width-scenario";

        protected override void Build(InkkOopsScriptBuilder builder)
        {
            builder
                .WaitFrames(8)
                .DumpTelemetry("mainwindow-width");
        }
    }

    public sealed class RuntimeDesignerPreviewDockThenSourceSplitterDragHangScenario : InkkOopsRuntimeScenario
    {
        public override string Name => "runtime-designer-preview-dock-then-source-splitter-drag-hang-scenario";

        protected override void Build(InkkOopsScriptBuilder builder)
        {
            var dragMotion = InkkOopsPointerMotion.WithTravelFrames(18, stepDistance: 8f);

            builder
                .ResizeWindow(1280, 820)
                .WaitFrames(24)
                .WaitForInteractive(PreviewDockSplitterName)
                .Drag(PreviewDockSplitterName, -650f, 0f, InkkOopsPointerAnchor.Center, MouseButton.Left, dragMotion)
                .WaitFrames(12)
                .WaitForInteractive(PreviewSourceSplitterName)
                .Drag(PreviewSourceSplitterName, 96f, -560f, InkkOopsPointerAnchor.Center, MouseButton.Left, dragMotion)
                .WaitFrames(12);
        }
    }

    public sealed class RuntimeDesignerPreviewSourceSplitterRepeatedVerticalDragFpsDropScenario : InkkOopsRuntimeScenario
    {
        public override string Name => "runtime-designer-preview-source-splitter-repeated-vertical-drag-fps-drop-scenario";

        protected override IEnumerable<int>? ActionDiagnosticsIndexes => [7, 8];

        protected override void Build(InkkOopsScriptBuilder builder)
        {
            var dragMotion = InkkOopsPointerMotion.WithTravelFrames(18, stepDistance: 8f);

            builder
                .ResizeWindow(1280, 820)
                .WaitFrames(24)
                .WaitForInteractive(PreviewSourceSplitterName)
                .Drag(PreviewSourceSplitterName, 96f, -560f, InkkOopsPointerAnchor.Center, MouseButton.Left, dragMotion)
                .WaitFrames(12)
                .Drag(PreviewSourceSplitterName, -96f, 700f, InkkOopsPointerAnchor.Center, MouseButton.Left, dragMotion)
                .WaitFrames(12)
                .Drag(PreviewSourceSplitterName, 96f, -620f, InkkOopsPointerAnchor.Center, MouseButton.Left, dragMotion)
                .WaitFrames(12)
                .Drag(PreviewSourceSplitterName, -96f, 640f, InkkOopsPointerAnchor.Center, MouseButton.Left, dragMotion)
                .WaitFrames(12)
                .Drag(PreviewSourceSplitterName, 96f, -420f, InkkOopsPointerAnchor.Center, MouseButton.Left, dragMotion)
                .WaitFrames(12);
        }
    }

    public sealed class RuntimeDesignerSourceEditorCtrlSpaceCompletionFpsDropScenario : InkkOopsRuntimeScenario
    {
        public override string Name => "runtime-designer-source-editor-ctrl-space-completion-fps-drop-scenario";

        protected override IEnumerable<int>? ActionDiagnosticsIndexes => [16, 17, 18, 19];

        protected override void Build(InkkOopsScriptBuilder builder)
        {
            builder
                .ResizeWindow(1280, 820)
                .WaitFrames(24)
                .WaitForInteractive(SourceEditorName)
                .Click(SourceEditorName)
                .WaitFrames(8)
                .PressKey(Keys.End, ModifierKeys.Control)
                .WaitFrames(4)
                .PressKey(Keys.Enter)
                .WaitFrames(4)
                .TextInput('<')
                .WaitFrames(4)
                .PressKey(Keys.Space, ModifierKeys.Control)
                .WaitFrames(24)
                .PressKey(Keys.Escape)
                .WaitFrames(12);
        }
    }

    public sealed class RuntimeDesignerSourcePropertyInspectorResizeThenMaximizeReproScenario : InkkOopsRuntimeScenario
    {
        private const int PreviewButtonLineMoveCount = 28;
        private const int PreviewButtonTagCharacterMoveCount = 10;

        public override string Name => "runtime-designer-source-property-inspector-resize-then-maximize-repro-scenario";

        protected override IEnumerable<int>? ActionDiagnosticsIndexes => [97, 98, 99, 100, 101, 102, 103, 104];

        protected override void Build(InkkOopsScriptBuilder builder)
        {
            var dragMotion = InkkOopsPointerMotion.WithTravelFrames(18, stepDistance: 8f);

            builder
                .WaitFrames(24)
                .WaitForInteractive(SourceEditorName)
                .CaptureFrame("source-property-inspector-before-selection")
                .Click(SourceEditorName, InkkOopsPointerAnchor.TopLeft)
                .WaitFrames(4)
                .PressKey(Keys.Home, ModifierKeys.Control);

            for (var i = 0; i < PreviewButtonLineMoveCount; i++)
            {
                builder.PressKey(Keys.Down);
            }

            builder.PressKey(Keys.Home);

            for (var i = 0; i < PreviewButtonTagCharacterMoveCount; i++)
            {
                builder.PressKey(Keys.Right);
            }

            builder
                .WaitFrames(8)
                .WaitForVisible(SourcePropertyInspectorScrollViewerName)
                .CaptureFrame("source-property-inspector-after-selection")
                .DumpTelemetry("source-property-inspector-after-selection")
                .WaitForInteractive(PreviewSourceSplitterName)
                .Drag(PreviewSourceSplitterName, 0f, -560f, InkkOopsPointerAnchor.Center, MouseButton.Left, dragMotion)
                .WaitFrames(12)
                .WaitForInteractive(SourcePropertyInspectorSplitterName)
                .Drag(SourcePropertyInspectorSplitterName, -520f, 0f, InkkOopsPointerAnchor.Center, MouseButton.Left, dragMotion)
                .WaitFrames(12)
                .CaptureFrame("source-property-inspector-after-resize")
                .DumpTelemetry("source-property-inspector-after-resize")
                .MaximizeWindow()
                .WaitFrames(24)
                .CaptureFrame("source-property-inspector-after-maximize")
                .DumpTelemetry("source-property-inspector-after-maximize")
                .WaitFrames(12)
                .CaptureFrame("source-property-inspector-after-maximize-settled")
                .DumpTelemetry("source-property-inspector-after-maximize-settled");
        }
    }

    public sealed class RuntimeDesignerColorPickerFirstClickFrameEvidenceScenario : InkkOopsRuntimeScenario
    {
        private const int FirstSuspiciousActionIndex = 303;
        private const int FinalCapturedActionIndex = 306;

        public override string Name => "runtime-designer-color-picker-first-click-frame-evidence-scenario";

        protected override void Build(InkkOopsScriptBuilder builder)
        {
            var actions = LoadRecordedActions(GetPhase4ColorPickerRecordingPath());

            if (actions.Count <= FinalCapturedActionIndex)
            {
                throw new InvalidOperationException($"Expected at least {FinalCapturedActionIndex + 1} recorded actions, found {actions.Count}.");
            }

            for (var i = 0; i < FirstSuspiciousActionIndex; i++)
            {
                AppendRecordedAction(builder, actions[i]);
            }

            builder.CaptureFrame("color-picker-before-action-303");

            for (var i = FirstSuspiciousActionIndex; i <= FinalCapturedActionIndex; i++)
            {
                AppendRecordedAction(builder, actions[i]);
                builder.CaptureFrame($"color-picker-after-action-{i}");
            }
        }
    }
}