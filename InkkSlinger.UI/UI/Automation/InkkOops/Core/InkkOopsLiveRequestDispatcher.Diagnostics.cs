using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed partial class InkkOopsLiveRequestDispatcher
{
    private static readonly JsonSerializerOptions ScenarioJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private async Task<InkkOopsPipeResponse> RunScenarioAsync(InkkOopsPipeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            throw new ArgumentException("Scenario JSON is required. Pass --scenario <file> or --text <json>.", nameof(request));
        }

        var scenario = JsonSerializer.Deserialize<InkkOopsScenarioDefinition>(request.Text, ScenarioJsonOptions)
            ?? throw new ArgumentException("Scenario JSON did not deserialize to a scenario definition.", nameof(request));
        var scenarioName = ResolveScenarioName(request, scenario);
        var trace = new InkkOopsScenarioTrace(scenarioName, DateTime.UtcNow);
        var beforeTelemetry = await CaptureTelemetryArtifactAsync($"{scenarioName}-before", cancellationToken).ConfigureAwait(false);

        foreach (var step in scenario.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stepRequest = CreateRequestFromScenarioStep(step, request);
            if (stepRequest.RequestKind == InkkOopsPipeRequestKinds.RunScenario)
            {
                throw new InvalidOperationException("Nested run-scenario steps are not supported.");
            }

            var stepIndex = trace.Steps.Count + 1;
            var frameCursor = _session.Host.GetFrameTimingCursor();
            var stopwatch = Stopwatch.StartNew();
            var response = await SubmitAsync(stepRequest, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            var frameArtifactName = $"{scenarioName}-step-{stepIndex:000}-{SanitizeArtifactName(step.Command)}-frame-window";
            var frameWindow = await _session.Host.CaptureFrameTimingWindowAsync(
                frameArtifactName,
                frameCursor,
                Math.Max(8, step.Frames + 4),
                cancellationToken).ConfigureAwait(false);
            var frameReportPath = _artifacts.WriteTextArtifact($"{frameArtifactName}.md", frameWindow);
            trace.Steps.Add(new InkkOopsTraceStep(
                stepIndex,
                step.Command,
                response.Status,
                stopwatch.Elapsed.TotalMilliseconds,
                response.Message,
                TrimTraceValue(response.Value),
                frameReportPath,
                ExtractFrameWindowSummary(frameWindow).Trim()));

            if (!string.Equals(response.Status, InkkOopsRunStatus.Completed.ToString(), StringComparison.Ordinal))
            {
                break;
            }
        }

        var afterTelemetry = await CaptureTelemetryArtifactAsync($"{scenarioName}-after", cancellationToken).ConfigureAwait(false);
        var diff = InkkOopsTelemetryAnalysis.CreateDiff(beforeTelemetry.Text, afterTelemetry.Text);
        var hints = InkkOopsTelemetryAnalysis.CreateHints(afterTelemetry.Text, diff);
        trace.TelemetryDiff = diff;
        trace.Hints.AddRange(hints);
        var artifacts = WriteTraceArtifacts(scenarioName, trace, beforeTelemetry.Path, afterTelemetry.Path);
        var status = trace.Steps.All(static step => string.Equals(step.Status, InkkOopsRunStatus.Completed.ToString(), StringComparison.Ordinal))
            ? InkkOopsRunStatus.Completed.ToString()
            : InkkOopsRunStatus.Failed.ToString();

        return new InkkOopsPipeResponse
        {
            Status = status,
            RequestKind = InkkOopsPipeRequestKinds.RunScenario,
            ArtifactDirectory = _artifacts.DirectoryPath,
            Value = FormatScenarioSummary(trace, artifacts.ReportPath)
        };
    }

    private async Task<InkkOopsPipeResponse> ProbeDuringDragAsync(InkkOopsPipeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TargetName))
        {
            throw new ArgumentException("TargetName is required for probe-during-drag.", nameof(request));
        }

        var scenarioName = string.IsNullOrWhiteSpace(request.ArtifactName) ? "drag-probe" : request.ArtifactName;
        var heldArtifact = $"{scenarioName}-held";
        var scenario = new InkkOopsScenarioDefinition
        {
            Name = scenarioName,
            Steps =
            [
                new InkkOopsScenarioStep { Command = "wait-for-visible", Target = request.ScopeTargetName.Length > 0 ? request.ScopeTargetName : request.TargetName, Frames = Math.Max(1, request.FrameCount) },
                new InkkOopsScenarioStep { Command = "get-telemetry", Artifact = $"{scenarioName}-pre" },
                new InkkOopsScenarioStep { Command = "pointer-down-target", Scope = request.ScopeTargetName, Target = request.TargetName, Anchor = request.Anchor, OffsetX = request.OffsetX, OffsetY = request.OffsetY, Button = request.ButtonName },
                new InkkOopsScenarioStep { Command = "move-pointer", X = request.X, Y = request.Y, DeltaX = request.DeltaX, DeltaY = request.DeltaY, TravelFrames = Math.Max(1, request.TravelFrames), Easing = request.Easing },
                new InkkOopsScenarioStep { Command = "capture-frame", Artifact = heldArtifact },
                new InkkOopsScenarioStep { Command = "get-telemetry", Artifact = $"{scenarioName}-held-telemetry" },
                new InkkOopsScenarioStep { Command = "assert-nonblank", Target = string.IsNullOrWhiteSpace(request.ScopeTargetName) ? request.TargetName : request.ScopeTargetName, MinBrightPixels = Math.Max(1, request.MinBrightPixels), MinAverageLuma = request.MinAverageLuma },
                new InkkOopsScenarioStep { Command = "pointer-up-target", Scope = request.ScopeTargetName, Target = request.TargetName, Anchor = request.Anchor, OffsetX = request.OffsetX, OffsetY = request.OffsetY, Button = request.ButtonName },
                new InkkOopsScenarioStep { Command = "get-telemetry", Artifact = $"{scenarioName}-post" }
            ]
        };

        var scenarioRequest = new InkkOopsPipeRequest
        {
            RequestKind = InkkOopsPipeRequestKinds.RunScenario,
            Text = JsonSerializer.Serialize(scenario, ScenarioJsonOptions),
            ScenarioName = scenarioName,
            ArtifactRootOverride = request.ArtifactRootOverride,
            TimeoutMilliseconds = request.TimeoutMilliseconds
        };
        return await RunScenarioAsync(scenarioRequest, cancellationToken).ConfigureAwait(false);
    }

    private async Task<InkkOopsPipeResponse> ProbeActionAsync(InkkOopsPipeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            throw new ArgumentException("probe-action requires --text containing one scenario step JSON object.", nameof(request));
        }

        var step = JsonSerializer.Deserialize<InkkOopsScenarioStep>(request.Text, ScenarioJsonOptions)
            ?? throw new ArgumentException("probe-action text did not deserialize to a scenario step.", nameof(request));
        if (string.IsNullOrWhiteSpace(step.Command))
        {
            throw new ArgumentException("probe-action step requires a command.", nameof(request));
        }

        var stepRequest = CreateRequestFromScenarioStep(step, request);
        if (stepRequest.RequestKind is InkkOopsPipeRequestKinds.ProbeAction or InkkOopsPipeRequestKinds.RunScenario)
        {
            throw new InvalidOperationException("probe-action cannot wrap probe-action or run-scenario.");
        }

        var probeName = SanitizeArtifactName(string.IsNullOrWhiteSpace(request.ArtifactName)
            ? $"probe-{step.Command}"
            : request.ArtifactName);
        var frameCount = request.FrameCount > 0 ? request.FrameCount : 12;
        var cursor = _session.Host.GetFrameTimingCursor();
        var beforeTelemetry = await CaptureTelemetryArtifactAsync($"{probeName}-before", cancellationToken).ConfigureAwait(false);
        var stopwatch = Stopwatch.StartNew();
        var response = await SubmitAsync(stepRequest, cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();
        await _session.WaitFramesAsync(frameCount, cancellationToken).ConfigureAwait(false);
        var afterTelemetry = await CaptureTelemetryArtifactAsync($"{probeName}-after", cancellationToken).ConfigureAwait(false);
        var frameWindow = await _session.Host.CaptureFrameTimingWindowAsync(probeName, cursor, frameCount + 4, cancellationToken).ConfigureAwait(false);
        var frameReportPath = _artifacts.WriteTextArtifact($"{probeName}-frame-window.md", frameWindow);
        var diff = InkkOopsTelemetryAnalysis.CreateDiff(beforeTelemetry.Text, afterTelemetry.Text);
        var hints = InkkOopsTelemetryAnalysis.CreateHints(afterTelemetry.Text, diff);
        var report = FormatProbeActionReport(
            probeName,
            step.Command,
            response,
            stopwatch.Elapsed.TotalMilliseconds,
            beforeTelemetry.Path,
            afterTelemetry.Path,
            frameReportPath,
            frameWindow,
            diff,
            hints);
        var reportPath = _artifacts.WriteTextArtifact($"{probeName}-report.md", report);
        var status = string.Equals(response.Status, InkkOopsRunStatus.Completed.ToString(), StringComparison.Ordinal)
            ? InkkOopsRunStatus.Completed.ToString()
            : InkkOopsRunStatus.Failed.ToString();

        return new InkkOopsPipeResponse
        {
            Status = status,
            RequestKind = InkkOopsPipeRequestKinds.ProbeAction,
            ArtifactDirectory = _artifacts.DirectoryPath,
            Value = $"probe={probeName}{Environment.NewLine}wrappedCommand={step.Command}{Environment.NewLine}wrappedStatus={response.Status}{Environment.NewLine}elapsedMs={stopwatch.Elapsed.TotalMilliseconds:0.###}{Environment.NewLine}frameReport={frameReportPath}{Environment.NewLine}report={reportPath}{Environment.NewLine}{ExtractFrameWindowSummary(frameWindow)}"
        };
    }

    private async Task<InkkOopsPipeResponse> AssertNonBlankAsync(InkkOopsPipeRequest request, CancellationToken cancellationToken)
    {
        var region = await ResolveSampleRegionAsync(request, cancellationToken).ConfigureAwait(false);
        var sample = await _session.SampleCurrentFrameRegionAsync(region, cancellationToken).ConfigureAwait(false);
        var minBrightPixels = Math.Max(1, request.MinBrightPixels);
        var minAverageLuma = Math.Max(0f, request.MinAverageLuma);
        if (sample.BrightPixelCount < minBrightPixels || sample.AverageLuma < minAverageLuma)
        {
            throw new InkkOopsCommandException(
                InkkOopsFailureCategory.NotInteractive,
                $"Expected nonblank frame region but sample was too dark. brightPixels={sample.BrightPixelCount}, minBrightPixels={minBrightPixels}, averageLuma={sample.AverageLuma:0.###}, minAverageLuma={minAverageLuma:0.###}, region=({sample.X},{sample.Y},{sample.Width},{sample.Height}).");
        }

        var value = $"nonblank=True brightPixels={sample.BrightPixelCount} averageLuma={sample.AverageLuma:0.###} maxLuma={sample.MaxLuma} region=({sample.X},{sample.Y},{sample.Width},{sample.Height})";
        return Complete(request, value: value);
    }

    private InkkOopsPipeResponse DiffTelemetry(InkkOopsPipeRequest request)
    {
        var pair = ParseTelemetryPair(request.Text);
        var diff = InkkOopsTelemetryAnalysis.CreateDiff(pair.Before, pair.After);
        var hints = InkkOopsTelemetryAnalysis.CreateHints(pair.After, diff);
        var artifactName = string.IsNullOrWhiteSpace(request.ArtifactName) ? "telemetry-diff" : request.ArtifactName;
        var report = InkkOopsTelemetryAnalysis.FormatDiffReport(diff, hints);
        var reportFile = EnsureExtension(artifactName, ".md");
        var reportPath = _artifacts.WriteTextArtifact(reportFile, report);
        return Complete(request, value: $"report={reportPath}{Environment.NewLine}{report}");
    }

    private async Task<(string Text, string Path)> CaptureTelemetryArtifactAsync(string artifactName, CancellationToken cancellationToken)
    {
        var telemetry = await _session.Host.CaptureTelemetryAsync(artifactName, cancellationToken).ConfigureAwait(false);
        var path = _artifacts.WriteTextArtifact(EnsureExtension(artifactName, ".txt"), telemetry);
        return (telemetry, path);
    }

    private async Task<LayoutRect> ResolveSampleRegionAsync(InkkOopsPipeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TargetName))
        {
            return _session.Host.GetViewportBounds();
        }

        _ = cancellationToken;
        var state = _session.EvaluateTargetState(CreateTarget(request), CreateAnchor(request));
        if (!state.HasBounds)
        {
            throw new InkkOopsCommandException(state.FailureCategory, $"Target '{request.TargetName}' does not expose render bounds for frame sampling.");
        }

        return state.Bounds;
    }

    private InkkOopsTraceArtifacts WriteTraceArtifacts(string scenarioName, InkkOopsScenarioTrace trace, string beforeTelemetryPath, string afterTelemetryPath)
    {
        var traceJsonPath = _artifacts.WriteTextArtifact($"{scenarioName}-trace.json", JsonSerializer.Serialize(trace, ScenarioJsonOptions));
        var report = FormatScenarioReport(trace, beforeTelemetryPath, afterTelemetryPath, traceJsonPath);
        var reportPath = _artifacts.WriteTextArtifact($"{scenarioName}-report.md", report);
        var index = JsonSerializer.Serialize(
            new
            {
                scenario = scenarioName,
                generatedUtc = DateTime.UtcNow,
                report = reportPath,
                trace = traceJsonPath,
                telemetry = new[] { beforeTelemetryPath, afterTelemetryPath },
                frameWindows = trace.Steps
                    .Where(static step => !string.IsNullOrWhiteSpace(step.FrameWindowPath))
                    .Select(static step => step.FrameWindowPath)
                    .ToArray(),
                stepCount = trace.Steps.Count,
                hints = trace.Hints
            },
            ScenarioJsonOptions);
        var indexPath = _artifacts.WriteTextArtifact($"{scenarioName}-artifact-index.json", index);
        return new InkkOopsTraceArtifacts(reportPath, traceJsonPath, indexPath);
    }

    private static string FormatScenarioSummary(InkkOopsScenarioTrace trace, string reportPath)
    {
        var status = trace.Steps.All(static step => string.Equals(step.Status, InkkOopsRunStatus.Completed.ToString(), StringComparison.Ordinal))
            ? "completed"
            : "failed";
        return $"scenario={trace.Name}{Environment.NewLine}status={status}{Environment.NewLine}steps={trace.Steps.Count}{Environment.NewLine}report={reportPath}{Environment.NewLine}hints={string.Join(" | ", trace.Hints)}";
    }

    private static string FormatScenarioReport(InkkOopsScenarioTrace trace, string beforeTelemetryPath, string afterTelemetryPath, string traceJsonPath)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# InkkOops Trace Report: {trace.Name}");
        builder.AppendLine();
        builder.AppendLine($"- startedUtc: `{trace.StartedUtc:O}`");
        builder.AppendLine($"- beforeTelemetry: `{beforeTelemetryPath}`");
        builder.AppendLine($"- afterTelemetry: `{afterTelemetryPath}`");
        builder.AppendLine($"- traceJson: `{traceJsonPath}`");
        builder.AppendLine();
        builder.AppendLine("## Steps");
        foreach (var step in trace.Steps)
        {
            builder.AppendLine($"- {step.Index}. `{step.Command}` status=`{step.Status}` elapsedMs=`{step.ElapsedMilliseconds:0.###}`");
            if (!string.IsNullOrWhiteSpace(step.FrameWindowPath))
            {
                builder.AppendLine($"  frameWindow: `{step.FrameWindowPath}`");
            }

            if (!string.IsNullOrWhiteSpace(step.FrameSummary))
            {
                builder.AppendLine("  frameSummary:");
                foreach (var line in step.FrameSummary.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
                {
                    builder.AppendLine($"    {line}");
                }
            }

            if (!string.IsNullOrWhiteSpace(step.Message))
            {
                builder.AppendLine($"  message: {step.Message.ReplaceLineEndings(" ")}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Telemetry Diff");
        builder.Append(InkkOopsTelemetryAnalysis.FormatDiffReport(trace.TelemetryDiff, trace.Hints));
        return builder.ToString();
    }

    private InkkOopsPipeRequest CreateRequestFromScenarioStep(InkkOopsScenarioStep step, InkkOopsPipeRequest parent)
    {
        var command = string.IsNullOrWhiteSpace(step.Command) ? throw new ArgumentException("Every scenario step needs a command.") : step.Command.Trim();
        return new InkkOopsPipeRequest
        {
            RequestKind = NormalizeScenarioCommandKind(command),
            TimeoutMilliseconds = parent.TimeoutMilliseconds,
            ArtifactRootOverride = parent.ArtifactRootOverride,
            ScopeTargetName = step.Scope ?? string.Empty,
            OwnerTargetName = step.Owner ?? string.Empty,
            TargetName = step.Target ?? string.Empty,
            X = ResolveScenarioCoordinate(step.X, step.DeltaX, axis: 'x'),
            Y = ResolveScenarioCoordinate(step.Y, step.DeltaY, axis: 'y'),
            Anchor = step.Anchor ?? string.Empty,
            OffsetX = step.OffsetX,
            OffsetY = step.OffsetY,
            PropertyName = step.Property ?? string.Empty,
            ExpectedValue = step.Expected ?? string.Empty,
            ArtifactName = step.Artifact ?? string.Empty,
            Compact = step.Compact,
            CounterNames = step.Counters ?? string.Empty,
            DeltaX = step.DeltaX,
            DeltaY = step.DeltaY,
            TravelFrames = step.TravelFrames,
            StepDistance = step.StepDistance,
            Easing = step.Easing ?? string.Empty,
            DwellFrames = step.DwellFrames,
            WheelDelta = step.Delta,
            HorizontalPercent = step.Horizontal,
            VerticalPercent = step.Vertical,
            Padding = step.Padding,
            FrameCount = step.Frames,
            KeyName = step.KeyName ?? string.Empty,
            Text = step.Text ?? string.Empty,
            Width = step.Width,
            Height = step.Height,
            EventType = step.EventType ?? string.Empty,
            ButtonName = step.Button ?? string.Empty,
            Waypoints = step.Waypoints == null ? string.Empty : JsonSerializer.Serialize(step.Waypoints, ScenarioJsonOptions),
            MinBrightPixels = step.MinBrightPixels,
            MinAverageLuma = step.MinAverageLuma
        };
    }

    private float? ResolveScenarioCoordinate(float? explicitValue, float delta, char axis)
    {
        if (explicitValue is float value)
        {
            return value;
        }

        if (delta == 0f)
        {
            return null;
        }

        var pointer = _session.Host.UiRoot.GetLastPointerPositionForDiagnostics();
        return axis == 'x' ? pointer.X + delta : pointer.Y + delta;
    }

    private static string NormalizeScenarioCommandKind(string command)
    {
        return command.Trim().ToLowerInvariant() switch
        {
            "ping" => InkkOopsPipeRequestKinds.Ping,
            "get-host-info" => InkkOopsPipeRequestKinds.GetHostInfo,
            "get-property" => InkkOopsPipeRequestKinds.GetProperty,
            "assert-property" => InkkOopsPipeRequestKinds.AssertProperty,
            "assert-exists" => InkkOopsPipeRequestKinds.AssertExists,
            "assert-not-exists" => InkkOopsPipeRequestKinds.AssertNotExists,
            "move-pointer" or "move" => InkkOopsPipeRequestKinds.MovePointer,
            "hover" => InkkOopsPipeRequestKinds.HoverTarget,
            "click" => InkkOopsPipeRequestKinds.ClickTarget,
            "invoke" or "activate" => InkkOopsPipeRequestKinds.InvokeTarget,
            "drag" => InkkOopsPipeRequestKinds.DragTarget,
            "wait-frames" => InkkOopsPipeRequestKinds.WaitFrames,
            "wait-for-element" => InkkOopsPipeRequestKinds.WaitForElement,
            "wait-for-visible" => InkkOopsPipeRequestKinds.WaitForVisible,
            "wait-for-enabled" => InkkOopsPipeRequestKinds.WaitForEnabled,
            "wait-for-in-viewport" => InkkOopsPipeRequestKinds.WaitForInViewport,
            "wait-for-interactive" => InkkOopsPipeRequestKinds.WaitForInteractive,
            "wait-for-idle" => InkkOopsPipeRequestKinds.WaitForIdle,
            "wheel" => InkkOopsPipeRequestKinds.Wheel,
            "scroll-to" => InkkOopsPipeRequestKinds.ScrollTo,
            "scroll-by" => InkkOopsPipeRequestKinds.ScrollBy,
            "scroll-into-view" => InkkOopsPipeRequestKinds.ScrollIntoView,
            "get-telemetry" => InkkOopsPipeRequestKinds.GetTelemetry,
            "get-target-diagnostics" => InkkOopsPipeRequestKinds.GetTargetDiagnostics,
            "screenshot" or "take-screenshot" => InkkOopsPipeRequestKinds.TakeScreenshot,
            "capture-frame" => InkkOopsPipeRequestKinds.CaptureFrame,
            "dump-telemetry" => InkkOopsPipeRequestKinds.DumpTelemetry,
            "double-click-target" or "double-click" => InkkOopsPipeRequestKinds.DoubleClickTarget,
            "right-click-target" or "right-click" => InkkOopsPipeRequestKinds.RightClickTarget,
            "key-down" => InkkOopsPipeRequestKinds.KeyDown,
            "key-up" => InkkOopsPipeRequestKinds.KeyUp,
            "text-input" or "type" => InkkOopsPipeRequestKinds.TextInput,
            "set-clipboard-text" or "set-clipboard" => InkkOopsPipeRequestKinds.SetClipboardText,
            "maximize-window" or "maximize" => InkkOopsPipeRequestKinds.MaximizeWindow,
            "resize-window" or "resize" => InkkOopsPipeRequestKinds.ResizeWindow,
            "leave-target" or "leave" => InkkOopsPipeRequestKinds.LeaveTarget,
            "assert-automation-event" or "assert-event" => InkkOopsPipeRequestKinds.AssertAutomationEvent,
            "pointer-down" => InkkOopsPipeRequestKinds.PointerDown,
            "pointer-up" => InkkOopsPipeRequestKinds.PointerUp,
            "pointer-down-target" => InkkOopsPipeRequestKinds.PointerDownTarget,
            "pointer-up-target" => InkkOopsPipeRequestKinds.PointerUpTarget,
            "move-pointer-path" => InkkOopsPipeRequestKinds.MovePointerPath,
            "drag-path-target" or "drag-path" => InkkOopsPipeRequestKinds.DragPathTarget,
            "probe-scrollbar-thumb-drag" or "probe-scrollbar-drag" => InkkOopsPipeRequestKinds.ProbeScrollbarThumbDrag,
            "probe-action" => InkkOopsPipeRequestKinds.ProbeAction,
            "assert-nonblank" or "assert-frame-nonblank" => InkkOopsPipeRequestKinds.AssertNonBlank,
            "diff-telemetry" => InkkOopsPipeRequestKinds.DiffTelemetry,
            _ => throw new ArgumentException($"Unknown scenario command '{command}'.")
        };
    }

    private static string ResolveScenarioName(InkkOopsPipeRequest request, InkkOopsScenarioDefinition scenario)
    {
        var name = !string.IsNullOrWhiteSpace(request.ScenarioName)
            ? request.ScenarioName
            : !string.IsNullOrWhiteSpace(scenario.Name)
                ? scenario.Name
                : !string.IsNullOrWhiteSpace(request.ArtifactName)
                    ? request.ArtifactName
                    : "scenario";
        return SanitizeArtifactName(name);
    }

    private static string SanitizeArtifactName(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            builder.Append(char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-');
        }

        return builder.Length == 0 ? "artifact" : builder.ToString();
    }

    private static string EnsureExtension(string artifactName, string extension)
    {
        return artifactName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? artifactName
            : artifactName + extension;
    }

    private static string TrimTraceValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= 400 ? normalized : normalized[..400] + "...";
    }

    private static string FormatProbeActionReport(
        string probeName,
        string wrappedCommand,
        InkkOopsPipeResponse wrappedResponse,
        double elapsedMilliseconds,
        string beforeTelemetryPath,
        string afterTelemetryPath,
        string frameReportPath,
        string frameWindow,
        IReadOnlyList<InkkOopsTelemetryDelta> diff,
        IReadOnlyList<string> hints)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# InkkOops Probe Action Report: {probeName}");
        builder.AppendLine();
        builder.AppendLine($"- wrappedCommand: `{wrappedCommand}`");
        builder.AppendLine($"- wrappedStatus: `{wrappedResponse.Status}`");
        builder.AppendLine($"- elapsedMs: `{elapsedMilliseconds:0.###}`");
        builder.AppendLine($"- beforeTelemetry: `{beforeTelemetryPath}`");
        builder.AppendLine($"- afterTelemetry: `{afterTelemetryPath}`");
        builder.AppendLine($"- frameWindow: `{frameReportPath}`");
        if (!string.IsNullOrWhiteSpace(wrappedResponse.Message))
        {
            builder.AppendLine($"- message: `{wrappedResponse.Message.ReplaceLineEndings(" ")}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Frame Window");
        builder.AppendLine("```text");
        builder.Append(ExtractFrameWindowSummary(frameWindow));
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine("## Telemetry Diff");
        builder.Append(InkkOopsTelemetryAnalysis.FormatDiffReport(diff, hints));
        return builder.ToString();
    }

    private static string ExtractFrameWindowSummary(string frameWindow)
    {
        var builder = new StringBuilder();
        foreach (var line in frameWindow.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            if (line.StartsWith("sampleCount=", StringComparison.Ordinal) ||
                line.StartsWith("maxFrameTotalMs=", StringComparison.Ordinal) ||
                line.StartsWith("maxFrameTotalSerial=", StringComparison.Ordinal) ||
                line.StartsWith("maxUpdateMs=", StringComparison.Ordinal) ||
                line.StartsWith("maxUpdateSerial=", StringComparison.Ordinal) ||
                line.StartsWith("maxDrawMs=", StringComparison.Ordinal) ||
                line.StartsWith("maxDrawSerial=", StringComparison.Ordinal) ||
                line.StartsWith("maxLayoutMs=", StringComparison.Ordinal) ||
                line.StartsWith("maxLayoutSerial=", StringComparison.Ordinal) ||
                line.StartsWith("hottestMeasurePath=", StringComparison.Ordinal) ||
                line.StartsWith("hottestArrangePath=", StringComparison.Ordinal) ||
                line.StartsWith("minDisplayedFps=", StringComparison.Ordinal) ||
                line.StartsWith("minDisplayedFpsSerial=", StringComparison.Ordinal))
            {
                builder.AppendLine(line);
            }
        }

        return builder.ToString();
    }

    private static (string Before, string After) ParseTelemetryPair(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("diff-telemetry requires text containing before and after telemetry separated by ---INKKOOPS-AFTER---.");
        }

        const string separator = "---INKKOOPS-AFTER---";
        var index = text.IndexOf(separator, StringComparison.Ordinal);
        if (index < 0)
        {
            throw new ArgumentException("diff-telemetry text must contain separator ---INKKOOPS-AFTER---.");
        }

        return (text[..index], text[(index + separator.Length)..]);
    }

    private sealed class InkkOopsScenarioDefinition
    {
        public string Name { get; set; } = string.Empty;

        public List<InkkOopsScenarioStep> Steps { get; set; } = [];
    }

    private sealed class InkkOopsScenarioStep
    {
        public string Command { get; set; } = string.Empty;
        public string? Scope { get; set; }
        public string? Owner { get; set; }
        public string? Target { get; set; }
        public float? X { get; set; }
        public float? Y { get; set; }
        public string? Anchor { get; set; }
        public float OffsetX { get; set; }
        public float OffsetY { get; set; }
        public string? Property { get; set; }
        public string? Expected { get; set; }
        public string? Artifact { get; set; }
        public bool Compact { get; set; }
        public string? Counters { get; set; }
        public float DeltaX { get; set; }
        public float DeltaY { get; set; }
        public int TravelFrames { get; set; }
        public float StepDistance { get; set; }
        public string? Easing { get; set; }
        public int DwellFrames { get; set; }
        public int Delta { get; set; }
        public float Horizontal { get; set; }
        public float Vertical { get; set; }
        public float Padding { get; set; }
        public int Frames { get; set; }
        public string? KeyName { get; set; }
        public string? Text { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string? EventType { get; set; }
        public string? Button { get; set; }
        public List<InkkOopsWaypoint>? Waypoints { get; set; }
        public int MinBrightPixels { get; set; } = 1;
        public float MinAverageLuma { get; set; }
    }

    private sealed record InkkOopsWaypoint(float X, float Y);

    private sealed class InkkOopsScenarioTrace(string name, DateTime startedUtc)
    {
        public string Name { get; } = name;
        public DateTime StartedUtc { get; } = startedUtc;
        public List<InkkOopsTraceStep> Steps { get; } = [];
        public List<InkkOopsTelemetryDelta> TelemetryDiff { get; set; } = [];
        public List<string> Hints { get; } = [];
    }

    private sealed record InkkOopsTraceStep(
        int Index,
        string Command,
        string Status,
        double ElapsedMilliseconds,
        string Message,
        string Value,
        string FrameWindowPath,
        string FrameSummary);

    private sealed record InkkOopsTraceArtifacts(string ReportPath, string TraceJsonPath, string IndexPath);
}

internal static class InkkOopsTelemetryAnalysis
{
    public static List<InkkOopsTelemetryDelta> CreateDiff(string beforeText, string afterText)
    {
        var before = ParseTelemetry(beforeText);
        var after = ParseTelemetry(afterText);
        var result = new List<InkkOopsTelemetryDelta>();
        foreach (var key in before.Keys.Union(after.Keys).OrderBy(static key => key, StringComparer.Ordinal))
        {
            before.TryGetValue(key, out var beforeValue);
            after.TryGetValue(key, out var afterValue);
            if (string.Equals(beforeValue, afterValue, StringComparison.Ordinal))
            {
                continue;
            }

            var hasNumericBefore = double.TryParse(beforeValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var beforeNumber);
            var hasNumericAfter = double.TryParse(afterValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var afterNumber);
            result.Add(new InkkOopsTelemetryDelta(
                key,
                beforeValue ?? string.Empty,
                afterValue ?? string.Empty,
                hasNumericBefore && hasNumericAfter ? afterNumber - beforeNumber : null));
        }

        return result;
    }

    public static List<string> CreateHints(string telemetryText, IReadOnlyList<InkkOopsTelemetryDelta> diff)
    {
        var telemetry = ParseTelemetry(telemetryText);
        var hints = new List<string>();
        var routeMs = ReadDoubleFromPointerMotion(telemetry.GetValueOrDefault("lastPointerMotion"), "routeMs");
        var inputMs = ReadDoubleFromPointerMotion(telemetry.GetValueOrDefault("lastPointerMotion"), "inputMs");
        var layoutMs = ReadDouble(telemetry, "lastPerformanceLayoutMs");
        var drawMs = ReadDouble(telemetry, "lastDrawVisualTreeMs");
        var deferredPath = ReadDouble(telemetry, "scrollViewerSetOffsetsDeferredLayoutPathCount");
        var transformPath = ReadDouble(telemetry, "scrollViewerSetOffsetsTransformInvalidationPathCount");
        var retainedValidation = telemetry.GetValueOrDefault("retained_tree_validation") ?? string.Empty;
        var drawnNodes = ReadDouble(telemetry, "lastRenderRetainedNodesDrawn");
        var dirtyArea = ReadDouble(telemetry, "lastDirtyAreaPercentage");

        if (routeMs >= 8d || inputMs >= 8d)
        {
            hints.Add($"Input route is hot during the latest pointer motion (inputMs={inputMs:0.###}, routeMs={routeMs:0.###}); inspect captured-pointer routing and event handlers before layout.");
        }

        if (layoutMs >= 8d)
        {
            var hottestMeasure = telemetry.GetValueOrDefault("lastPerformanceHottestMeasure") ?? "unknown";
            var hottestArrange = telemetry.GetValueOrDefault("lastPerformanceHottestArrange") ?? "unknown";
            hints.Add($"Layout is hot (lastPerformanceLayoutMs={layoutMs:0.###}); hottest measure={hottestMeasure}, hottest arrange={hottestArrange}.");
        }

        if (deferredPath > 0d)
        {
            hints.Add($"ScrollViewer is taking deferred layout SetOffsets path (count={deferredPath:0}); prefer transform/direct invalidation for stable viewport scrolling.");
        }
        else if (transformPath > 0d)
        {
            hints.Add($"ScrollViewer SetOffsets used transform invalidation path (count={transformPath:0}); route is likely avoiding full layout.");
        }

        if (!string.Equals(retainedValidation, "ok", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(retainedValidation))
        {
            hints.Add($"Retained tree validation is not ok: {retainedValidation}.");
        }

        if (drawnNodes <= 0d && telemetry.ContainsKey("lastRenderRetainedNodesDrawn"))
        {
            hints.Add("No retained nodes were drawn in the latest frame; inspect clip, dirty bounds, transform, or blank-frame conditions.");
        }

        if (dirtyArea >= 50d)
        {
            hints.Add($"Dirty area is large ({dirtyArea:0.###}%); inspect invalidation source and dirty bounds expansion.");
        }

        if (drawMs >= 8d)
        {
            hints.Add($"Render draw phase is hot (lastDrawVisualTreeMs={drawMs:0.###}); inspect retained traversal, text rendering, and full redraw decisions.");
        }

        foreach (var delta in diff.Where(static item => Math.Abs(item.NumericDelta ?? 0d) > 0d).OrderByDescending(static item => Math.Abs(item.NumericDelta ?? 0d)).Take(5))
        {
            hints.Add($"Largest telemetry delta: {delta.Key} changed {delta.BeforeValue} -> {delta.AfterValue} (delta={delta.NumericDelta:0.###}).");
        }

        if (hints.Count == 0)
        {
            hints.Add("No obvious hotspot hint found; narrow the repro window or add focused target diagnostics counters.");
        }

        return hints;
    }

    public static string FormatDiffReport(IReadOnlyList<InkkOopsTelemetryDelta> diff, IReadOnlyList<string> hints)
    {
        var builder = new StringBuilder();
        builder.AppendLine("### Hints");
        foreach (var hint in hints)
        {
            builder.AppendLine($"- {hint}");
        }

        builder.AppendLine();
        builder.AppendLine("### Changed Telemetry");
        foreach (var item in diff.Take(80))
        {
            var deltaText = item.NumericDelta.HasValue ? $" delta=`{item.NumericDelta.Value:0.###}`" : string.Empty;
            builder.AppendLine($"- `{item.Key}`: `{item.BeforeValue}` -> `{item.AfterValue}`{deltaText}");
        }

        if (diff.Count == 0)
        {
            builder.AppendLine("- No telemetry values changed.");
        }

        return builder.ToString();
    }

    private static Dictionary<string, string> ParseTelemetry(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        using var reader = new StringReader(text ?? string.Empty);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var index = line.IndexOf('=');
            if (index <= 0)
            {
                continue;
            }

            result[line[..index].Trim()] = line[(index + 1)..].Trim();
        }

        return result;
    }

    private static double ReadDouble(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var text) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0d;
    }

    private static double ReadDoubleFromPointerMotion(string? text, string key)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0d;
        }

        var marker = key + "=";
        var index = text.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            return 0d;
        }

        index += marker.Length;
        var end = index;
        while (end < text.Length && !char.IsWhiteSpace(text[end]))
        {
            end++;
        }

        return double.TryParse(text.AsSpan(index, end - index), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0d;
    }
}

public sealed record InkkOopsTelemetryDelta(string Key, string BeforeValue, string AfterValue, double? NumericDelta);
