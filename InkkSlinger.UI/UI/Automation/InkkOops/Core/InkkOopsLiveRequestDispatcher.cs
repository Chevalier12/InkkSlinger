using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public sealed partial class InkkOopsLiveRequestDispatcher : IDisposable
{
    private readonly IInkkOopsScriptCatalog _scriptCatalog;
    private readonly InkkOopsArtifacts _artifacts;
    private readonly InkkOopsSession _session;

    public InkkOopsLiveRequestDispatcher(
        IInkkOopsHost host,
        IInkkOopsScriptCatalog scriptCatalog,
        string artifactRoot,
        IInkkOopsArtifactNamingPolicy? artifactNamingPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        _scriptCatalog = scriptCatalog ?? throw new ArgumentNullException(nameof(scriptCatalog));
        _artifacts = artifactNamingPolicy == null
            ? new InkkOopsArtifacts(artifactRoot, "live-session")
            : new InkkOopsArtifacts(artifactRoot, "live-session", artifactNamingPolicy);
        _session = new InkkOopsSession(host, _artifacts);
    }

    public async Task<InkkOopsPipeResponse> SubmitAsync(InkkOopsPipeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var kind = NormalizeKind(request.RequestKind);
            return kind switch
            {
                InkkOopsPipeRequestKinds.Ping => Complete(request, value: "pong"),
                InkkOopsPipeRequestKinds.GetProperty => await GetPropertyAsync(request, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.AssertProperty => await AssertPropertyAsync(request, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.AssertExists => await ExecuteCommandAsync(request, new InkkOopsAssertExistsCommand(CreateTarget(request)), cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.AssertNotExists => await ExecuteCommandAsync(request, new InkkOopsAssertNotExistsCommand(CreateTarget(request)), cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.MovePointer => await MovePointerAsync(request, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.HoverTarget => await ExecuteCommandAsync(request, new InkkOopsHoverTargetCommand(CreateTarget(request), CreateAnchor(request), request.DwellFrames, CreateMotion(request)), cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.ClickTarget => await ExecuteCommandAsync(request, new InkkOopsClickTargetCommand(CreateTarget(request), CreateAnchor(request), motion: CreateMotion(request)), cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.InvokeTarget => await ExecuteCommandAsync(request, new InkkOopsInvokeTargetCommand(CreateTarget(request)), cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.WaitFrames => await WaitFramesAsync(request, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.WaitForElement => await WaitForTargetAsync(request, InkkOopsWaitCondition.Exists, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.WaitForVisible => await WaitForTargetAsync(request, InkkOopsWaitCondition.Visible, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.WaitForEnabled => await WaitForTargetAsync(request, InkkOopsWaitCondition.Enabled, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.WaitForInViewport => await WaitForTargetAsync(request, InkkOopsWaitCondition.InViewport, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.WaitForInteractive => await WaitForTargetAsync(request, InkkOopsWaitCondition.Interactive, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.WaitForIdle => await ExecuteCommandAsync(request, new InkkOopsWaitForIdleCommand(), cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.Wheel => await WheelAsync(request, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.ScrollTo => await ExecuteCommandAsync(request, new InkkOopsScrollToCommand(CreateScrollProviderTarget(request), request.HorizontalPercent, request.VerticalPercent), cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.ScrollBy => await ExecuteCommandAsync(request, new InkkOopsScrollByCommand(CreateScrollProviderTarget(request), request.HorizontalPercent, request.VerticalPercent), cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.ScrollIntoView => await ScrollIntoViewAsync(request, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.GetTelemetry => await GetTelemetryAsync(request, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.GetTargetDiagnostics => await GetTargetDiagnosticsAsync(request, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.GetHostInfo => GetHostInfo(request),
                InkkOopsPipeRequestKinds.DragTarget => await ExecuteCommandAsync(request, new InkkOopsDragTargetCommand(CreateTarget(request), request.DeltaX, request.DeltaY, CreateAnchor(request), motion: CreateMotion(request)), cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.TakeScreenshot => await TakeScreenshotAsync(request, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.RunScript => await RunScriptAsync(request, cancellationToken).ConfigureAwait(false),

                // ── Newly exposed commands ───────────────────────────────
                InkkOopsPipeRequestKinds.DoubleClickTarget => await ExecuteCommandAsync(request,
                    new InkkOopsDoubleClickTargetCommand(CreateTarget(request), CreateAnchor(request),
                        Math.Max(1, request.DwellFrames), CreateMotion(request)), cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.RightClickTarget => await ExecuteCommandAsync(request,
                    new InkkOopsRightClickTargetCommand(CreateTarget(request), CreateAnchor(request),
                        CreateMotion(request)), cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.KeyDown => await ExecuteCommandAsync(request,
                    new InkkOopsKeyDownCommand(ParseKey(request)), cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.KeyUp => await ExecuteCommandAsync(request,
                    new InkkOopsKeyUpCommand(ParseKey(request)), cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.TextInput => await TextInputAsync(request, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.SetClipboardText => await ExecuteCommandAsync(request,
                    new InkkOopsSetClipboardTextCommand(request.Text), cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.MaximizeWindow => await ExecuteCommandAsync(request,
                    new InkkOopsMaximizeWindowCommand(), cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.ResizeWindow => await ExecuteCommandAsync(request,
                    new InkkOopsResizeWindowCommand(Math.Max(1, request.Width), Math.Max(1, request.Height)),
                    cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.LeaveTarget => await ExecuteCommandAsync(request,
                    new InkkOopsLeaveTargetCommand(CreateTarget(request), request.Padding, CreateMotion(request)),
                    cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.CaptureFrame => await ExecuteCommandAsync(request,
                    new InkkOopsCaptureFrameCommand(ResolveArtifactName(request, "frame")),
                    cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.DumpTelemetry => await ExecuteCommandAsync(request,
                    new InkkOopsDumpTelemetryCommand(ResolveArtifactName(request, "telemetry")),
                    cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.AssertAutomationEvent => await ExecuteCommandAsync(request,
                    new InkkOopsAssertAutomationEventCommand(ParseAutomationEventType(request), request.TargetName, request.PropertyName),
                    cancellationToken).ConfigureAwait(false),

                // ── Pointer state / path commands ─────────────────────────
                InkkOopsPipeRequestKinds.PointerDown => await PointerDownAsync(request, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.PointerUp => await PointerUpAsync(request, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.PointerDownTarget => await ExecuteCommandAsync(request,
                    new InkkOopsPointerDownTargetCommand(CreateTarget(request), CreateAnchor(request), ParseMouseButton(request), CreateMotion(request)),
                    cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.PointerUpTarget => await ExecuteCommandAsync(request,
                    new InkkOopsPointerUpTargetCommand(CreateTarget(request), CreateAnchor(request), ParseMouseButton(request), CreateMotion(request)),
                    cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.MovePointerPath => await MovePointerPathAsync(request, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.RunScenario => await RunScenarioAsync(request, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.ProbeDuringDrag => await ProbeDuringDragAsync(request, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.ProbeScrollbarThumbDrag => await ProbeScrollbarThumbDragAsync(request, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.AssertNonBlank => await AssertNonBlankAsync(request, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.DiffTelemetry => DiffTelemetry(request),
                InkkOopsPipeRequestKinds.DragPathTarget => await ExecuteCommandAsync(request,
                    new InkkOopsDragPathTargetCommand(CreateTarget(request), ParseWaypoints(request), CreateAnchor(request), ParseMouseButton(request), CreateMotion(request)),
                    cancellationToken).ConfigureAwait(false),
                _ => Fail(request, $"Unknown live request kind '{request.RequestKind}'.")
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Fail(request, ex.ToString());
        }
    }

    public void Dispose()
    {
        _artifacts.Dispose();
    }

    private async Task<InkkOopsPipeResponse> GetPropertyAsync(InkkOopsPipeRequest request, CancellationToken cancellationToken)
    {
        ValidateTargetAndProperty(request);
        var value = await _session.QueryOnUiThreadAsync(
            () =>
            {
                var element = _session.ResolveRequiredTarget(CreateTarget(request));
                var property = element.GetType().GetProperty(request.PropertyName);
                if (property?.CanRead != true)
                {
                    throw new InvalidOperationException($"Property '{request.PropertyName}' is not readable on '{element.GetType().Name}'.");
                }

                return InkkOopsCommandUtilities.FormatObject(property.GetValue(element));
            },
            cancellationToken).ConfigureAwait(false);
        return Complete(request, value: value);
    }

    private async Task<InkkOopsPipeResponse> AssertPropertyAsync(InkkOopsPipeRequest request, CancellationToken cancellationToken)
    {
        ValidateTargetAndProperty(request);
        var expectedValue = await _session.QueryOnUiThreadAsync(
            () =>
            {
                var element = _session.ResolveRequiredTarget(CreateTarget(request));
                var property = element.GetType().GetProperty(request.PropertyName);
                if (property?.CanRead != true)
                {
                    throw new InvalidOperationException($"Property '{request.PropertyName}' is not readable on '{element.GetType().Name}'.");
                }

                return ConvertStringValue(request.ExpectedValue, property.PropertyType);
            },
            cancellationToken).ConfigureAwait(false);
        await new InkkOopsAssertPropertyCommand(CreateTarget(request), request.PropertyName, expectedValue)
            .ExecuteAsync(_session, cancellationToken)
            .ConfigureAwait(false);
        return Complete(request, value: InkkOopsCommandUtilities.FormatObject(expectedValue));
    }

    private async Task<InkkOopsPipeResponse> ExecuteCommandAsync(InkkOopsPipeRequest request, IInkkOopsCommand command, CancellationToken cancellationToken)
    {
        await command.ExecuteAsync(_session, cancellationToken).ConfigureAwait(false);
        return Complete(request);
    }

    private async Task<InkkOopsPipeResponse> MovePointerAsync(InkkOopsPipeRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.TargetName))
        {
            await new InkkOopsMovePointerTargetCommand(CreateTarget(request), CreateAnchor(request), CreateMotion(request))
                .ExecuteAsync(_session, cancellationToken)
                .ConfigureAwait(false);
            return Complete(request);
        }

        if (request.X is not float x || request.Y is not float y)
        {
            throw new ArgumentException("Either TargetName or both X and Y are required for move-pointer.", nameof(request));
        }

        await new InkkOopsMovePointerCommand(new Vector2(x, y), CreateMotion(request))
            .ExecuteAsync(_session, cancellationToken)
            .ConfigureAwait(false);
        return Complete(request);
    }

    private async Task<InkkOopsPipeResponse> WaitFramesAsync(InkkOopsPipeRequest request, CancellationToken cancellationToken)
    {
        await _session.WaitFramesAsync(Math.Max(0, request.FrameCount), cancellationToken).ConfigureAwait(false);
        return Complete(request, value: request.FrameCount.ToString(CultureInfo.InvariantCulture));
    }

    private async Task<InkkOopsPipeResponse> WaitForTargetAsync(InkkOopsPipeRequest request, InkkOopsWaitCondition condition, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TargetName))
        {
            throw new ArgumentException("TargetName is required for this request.", nameof(request));
        }

        var command = new InkkOopsWaitForElementCommand(CreateTarget(request), Math.Max(1, request.FrameCount), condition, CreateAnchor(request));
        await command.ExecuteAsync(_session, cancellationToken).ConfigureAwait(false);
        return Complete(request, value: request.FrameCount.ToString(CultureInfo.InvariantCulture));
    }

    private async Task<InkkOopsPipeResponse> WheelAsync(InkkOopsPipeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TargetName))
        {
            await _session.WheelAsync(request.WheelDelta, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await new InkkOopsWheelTargetCommand(CreateTarget(request), request.WheelDelta, CreateAnchor(request), CreateMotion(request))
                .ExecuteAsync(_session, cancellationToken)
                .ConfigureAwait(false);
        }

        return Complete(request, value: request.WheelDelta.ToString(CultureInfo.InvariantCulture));
    }

    private async Task<InkkOopsPipeResponse> ScrollIntoViewAsync(InkkOopsPipeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TargetName))
        {
            throw new ArgumentException("TargetName is required for this request.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.OwnerTargetName))
        {
            await ScrollIntoViewUsingNearestOwnerAsync(request, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var command = new InkkOopsScrollIntoViewCommand(
                CreateOwnerTarget(request),
                CreateTarget(request),
                request.Padding <= 0f ? 8f : request.Padding);
            await command.ExecuteAsync(_session, cancellationToken).ConfigureAwait(false);
        }

        return Complete(request);
    }

    private async Task ScrollIntoViewUsingNearestOwnerAsync(InkkOopsPipeRequest request, CancellationToken cancellationToken)
    {
        var padding = request.Padding <= 0f ? 8f : request.Padding;
        await _session.ExecuteOnUiThreadAsync(
            () =>
            {
                var targetElement = _session.ResolveRequiredTarget(CreateTarget(request));
                for (var current = targetElement.VisualParent ?? targetElement.LogicalParent;
                     current != null;
                     current = current.VisualParent ?? current.LogicalParent)
                {
                    if (current is ScrollViewer scrollViewer)
                    {
                        ScrollRealizedElementIntoView(scrollViewer, targetElement, padding, request.TargetName);
                        return;
                    }
                }

                throw new InkkOopsCommandException(
                    InkkOopsFailureCategory.SemanticProviderMissing,
                    $"Target '{request.TargetName}' does not have a ScrollViewer ancestor. Specify --owner explicitly.");
            },
            cancellationToken).ConfigureAwait(false);
        await _session.WaitFramesAsync(4, cancellationToken).ConfigureAwait(false);
    }

    private static void ScrollRealizedElementIntoView(ScrollViewer scrollViewer, UIElement itemElement, float padding, string targetName)
    {
        if (!itemElement.TryGetRenderBoundsInRootSpace(out var itemBounds))
        {
            throw new InkkOopsCommandException(InkkOopsFailureCategory.Unrealized, $"Target '{targetName}' does not expose render bounds.");
        }

        if (!scrollViewer.TryGetContentViewportClipRect(out var viewportBounds))
        {
            throw new InkkOopsCommandException(InkkOopsFailureCategory.Unrealized, "ScrollViewer does not have an active viewport.");
        }

        var nextVerticalOffset = scrollViewer.VerticalOffset;
        var targetTop = itemBounds.Y - padding;
        var targetBottom = itemBounds.Y + itemBounds.Height + padding;
        var viewportTop = viewportBounds.Y;
        var viewportBottom = viewportBounds.Y + viewportBounds.Height;

        if (targetTop < viewportTop)
        {
            nextVerticalOffset -= viewportTop - targetTop;
        }
        else if (targetBottom > viewportBottom)
        {
            nextVerticalOffset += targetBottom - viewportBottom;
        }

        scrollViewer.ScrollToVerticalOffset(nextVerticalOffset);
    }

    private async Task<InkkOopsPipeResponse> GetTelemetryAsync(InkkOopsPipeRequest request, CancellationToken cancellationToken)
    {
        var artifactName = string.IsNullOrWhiteSpace(request.ArtifactName) ? "live-telemetry" : request.ArtifactName;
        var telemetry = await _session.Host.CaptureTelemetryAsync(artifactName, cancellationToken).ConfigureAwait(false);
        return Complete(request, value: telemetry);
    }

    private async Task<InkkOopsPipeResponse> TakeScreenshotAsync(InkkOopsPipeRequest request, CancellationToken cancellationToken)
    {
        var artifactName = string.IsNullOrWhiteSpace(request.ArtifactName) ? "screenshot" : request.ArtifactName;
        await _session.CaptureFrameAsync(artifactName, cancellationToken).ConfigureAwait(false);
        return Complete(request, value: $"Screenshot saved to artifact '{artifactName}'.");
    }

    private async Task<InkkOopsPipeResponse> ProbeScrollbarThumbDragAsync(InkkOopsPipeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TargetName))
        {
            throw new ArgumentException("TargetName is required for probe-scrollbar-thumb-drag.", nameof(request));
        }

        var axis = ParseScrollbarProbeAxis(request.Axis);
        var fromPercent = ResolveScrollbarProbePercent(request.From, axis, defaultValue: 1f);
        var toPercent = ResolveScrollbarProbePercent(request.To, axis, defaultValue: 0f);
        var sampleCount = Math.Clamp(request.SampleCount > 0 ? request.SampleCount : request.FrameCount > 0 ? request.FrameCount : 12, 1, 60);
        var dwellFrames = Math.Max(1, request.DwellFrames);
        var travelFrames = Math.Max(1, request.TravelFrames > 0 ? request.TravelFrames : 8);
        var artifactPrefix = string.IsNullOrWhiteSpace(request.ArtifactName) ? "scrollbar-thumb-drag" : request.ArtifactName.Trim();
        var motion = new InkkOopsPointerMotion(
            TravelFrames: travelFrames,
            StepDistance: ResolveStepDistance(request),
            Easing: ParseEasing(request.Easing));

        await SetScrollViewerOffsetForProbeAsync(request, axis, 0f, cancellationToken).ConfigureAwait(false);
        await _session.WaitFramesAsync(dwellFrames, cancellationToken).ConfigureAwait(false);

        var initial = await CaptureScrollbarProbeGeometryAsync(request, axis, cancellationToken).ConfigureAwait(false);
        await _session.PressPointerAsync(initial.ThumbCenter, ParseMouseButton(request), cancellationToken).ConfigureAwait(false);
        await _session.MovePointerAsync(initial.GetPointAtPercent(fromPercent), motion, cancellationToken).ConfigureAwait(false);
        await _session.ReleasePointerAsync(initial.GetPointAtPercent(fromPercent), ParseMouseButton(request), cancellationToken).ConfigureAwait(false);
        await _session.WaitFramesAsync(dwellFrames, cancellationToken).ConfigureAwait(false);
        await CaptureScrollbarProbeSampleAsync(request, artifactPrefix, "released-at-from", axis, fromPercent, cancellationToken).ConfigureAwait(false);

        var heldStart = await CaptureScrollbarProbeGeometryAsync(request, axis, cancellationToken).ConfigureAwait(false);
        await _session.PressPointerAsync(heldStart.ThumbCenter, ParseMouseButton(request), cancellationToken).ConfigureAwait(false);
        await _session.WaitFramesAsync(dwellFrames, cancellationToken).ConfigureAwait(false);
        await CaptureScrollbarProbeSampleAsync(request, artifactPrefix, "held-00", axis, fromPercent, cancellationToken).ConfigureAwait(false);

        var sampleLines = new StringBuilder();
        sampleLines.AppendLine($"artifact_root={_artifacts.DirectoryPath}");
        sampleLines.AppendLine($"target={request.TargetName}");
        sampleLines.AppendLine($"axis={axis}");
        sampleLines.AppendLine($"from={fromPercent:0.###}");
        sampleLines.AppendLine($"to={toPercent:0.###}");
        sampleLines.AppendLine($"samples={sampleCount}");
        sampleLines.AppendLine($"travel_frames={travelFrames}");

        var previousPercent = fromPercent;
        for (var sampleIndex = 1; sampleIndex <= sampleCount; sampleIndex++)
        {
            var percent = fromPercent + ((toPercent - fromPercent) * sampleIndex / sampleCount);
            var geometry = await CaptureScrollbarProbeGeometryAsync(request, axis, cancellationToken).ConfigureAwait(false);
            var targetPoint = geometry.GetPointAtPercent(percent);
            await _session.MovePointerAsync(targetPoint, motion, cancellationToken).ConfigureAwait(false);
            await _session.WaitFramesAsync(dwellFrames, cancellationToken).ConfigureAwait(false);

            var sampleName = $"held-{sampleIndex:00}-p{percent * 100f:000}";
            var sample = await CaptureScrollbarProbeSampleAsync(request, artifactPrefix, sampleName, axis, percent, cancellationToken).ConfigureAwait(false);
            sampleLines.AppendLine($"sample[{sampleIndex:00}]=percent:{percent:0.###} previous:{previousPercent:0.###} frame:{sample.FrameArtifact}.png diagnostics:{sample.DiagnosticsArtifact} bright:{sample.BrightPixelCount} avgLuma:{sample.AverageLuma:0.###}");
            previousPercent = percent;
        }

        var finalGeometry = await CaptureScrollbarProbeGeometryAsync(request, axis, cancellationToken).ConfigureAwait(false);
        await _session.ReleasePointerAsync(finalGeometry.GetPointAtPercent(toPercent), ParseMouseButton(request), cancellationToken).ConfigureAwait(false);
        await _session.WaitFramesAsync(dwellFrames, cancellationToken).ConfigureAwait(false);
        await CaptureScrollbarProbeSampleAsync(request, artifactPrefix, "after-release", axis, toPercent, cancellationToken).ConfigureAwait(false);

        var summaryPath = _artifacts.WriteTextArtifact($"{artifactPrefix}-summary.txt", sampleLines.ToString());
        return Complete(request, value: $"summary={summaryPath}{Environment.NewLine}{sampleLines}");
    }

    private async Task SetScrollViewerOffsetForProbeAsync(InkkOopsPipeRequest request, ScrollbarProbeAxis axis, float percent, CancellationToken cancellationToken)
    {
        await _session.ExecuteOnUiThreadAsync(
            () =>
            {
                var element = _session.ResolveRequiredTarget(CreateTarget(request));
                var scrollViewer = ResolveScrollbarProbeScrollViewer(element);
                if (axis == ScrollbarProbeAxis.Vertical)
                {
                    var max = Math.Max(0f, scrollViewer.ExtentHeight - scrollViewer.ViewportHeight);
                    scrollViewer.ScrollToVerticalOffset(max * Math.Clamp(percent, 0f, 1f));
                }
                else
                {
                    var max = Math.Max(0f, scrollViewer.ExtentWidth - scrollViewer.ViewportWidth);
                    scrollViewer.ScrollToHorizontalOffset(max * Math.Clamp(percent, 0f, 1f));
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ScrollbarProbeGeometry> CaptureScrollbarProbeGeometryAsync(InkkOopsPipeRequest request, ScrollbarProbeAxis axis, CancellationToken cancellationToken)
    {
        return await _session.QueryOnUiThreadAsync(
            () =>
            {
                var element = _session.ResolveRequiredTarget(CreateTarget(request));
                var scrollViewer = ResolveScrollbarProbeScrollViewer(element);
                var scrollBar = axis == ScrollbarProbeAxis.Vertical
                    ? scrollViewer.AutomationVerticalScrollBar
                    : scrollViewer.AutomationHorizontalScrollBar;
                var thumb = scrollBar.GetThumbRectForInput();
                var track = scrollBar.GetTrackRectForInput();
                if (thumb.Width <= 0f || thumb.Height <= 0f || track.Width <= 0f || track.Height <= 0f)
                {
                    throw new InkkOopsCommandException(
                        InkkOopsFailureCategory.Unrealized,
                        $"Could not resolve arranged {axis.ToString().ToLowerInvariant()} scrollbar thumb geometry for '{request.TargetName}'. thumb=({thumb.X:0.###},{thumb.Y:0.###},{thumb.Width:0.###},{thumb.Height:0.###}), track=({track.X:0.###},{track.Y:0.###},{track.Width:0.###},{track.Height:0.###}).");
                }

                return new ScrollbarProbeGeometry(axis, thumb, track);
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ScrollbarProbeSample> CaptureScrollbarProbeSampleAsync(
        InkkOopsPipeRequest request,
        string artifactPrefix,
        string sampleName,
        ScrollbarProbeAxis axis,
        float percent,
        CancellationToken cancellationToken)
    {
        var frameArtifact = $"{artifactPrefix}-{sampleName}";
        await _session.CaptureFrameAsync(frameArtifact, cancellationToken).ConfigureAwait(false);

        var state = _session.EvaluateTargetState(CreateTarget(request), CreateAnchor(request));
        var counterNames = ParseCounterNames(request.CounterNames);
        var runtimeDiagnostics = await _session.QueryOnUiThreadAsync(
            () => CaptureRuntimeDiagnosticsText(state.Element, request.Compact, counterNames),
            cancellationToken).ConfigureAwait(false);
        var diagnostics = new StringBuilder();
        diagnostics.AppendLine($"sample={sampleName}");
        diagnostics.AppendLine($"axis={axis}");
        diagnostics.AppendLine($"thumb_percent={percent:0.###}");
        diagnostics.Append(FormatTargetDiagnostics(state, runtimeDiagnostics, request.Compact));
        diagnostics.Append(CaptureScrollbarProbeRowDiagnostics(state.Element));
        var diagnosticsArtifact = $"{frameArtifact}-diagnostics.txt";
        _artifacts.WriteTextArtifact(diagnosticsArtifact, diagnostics.ToString());

        var sampleRegion = state.HasBounds
            ? new LayoutRect(
                state.Bounds.X + Math.Max(0f, request.Padding),
                state.Bounds.Y + Math.Max(0f, request.Padding),
                Math.Max(0f, state.Bounds.Width - (Math.Max(0f, request.Padding) * 2f)),
                Math.Max(0f, state.Bounds.Height - (Math.Max(0f, request.Padding) * 2f)))
            : _session.Host.GetViewportBounds();
        var frameSample = await _session.SampleCurrentFrameRegionAsync(sampleRegion, cancellationToken).ConfigureAwait(false);
        var sampleArtifact = $"{frameArtifact}-sample.txt";
        _artifacts.WriteTextArtifact(
            sampleArtifact,
            $"sample={sampleName}{Environment.NewLine}region=({frameSample.X},{frameSample.Y},{frameSample.Width},{frameSample.Height}){Environment.NewLine}bright_pixels={frameSample.BrightPixelCount}{Environment.NewLine}average_luma={frameSample.AverageLuma:0.###}{Environment.NewLine}max_luma={frameSample.MaxLuma}{Environment.NewLine}");

        return new ScrollbarProbeSample(frameArtifact, diagnosticsArtifact, frameSample.BrightPixelCount, frameSample.AverageLuma);
    }

    private static string CaptureScrollbarProbeRowDiagnostics(UIElement? element)
    {
        if (element == null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var rows = new List<TreeViewItem>();
        CollectVisualDescendants(element, rows);
        var index = 0;
        foreach (var row in rows.Take(40))
        {
            builder.Append("ProbeRow[");
            builder.Append(index.ToString("00", CultureInfo.InvariantCulture));
            builder.Append("]=");
            builder.Append("rowIndex:");
            builder.Append(row.VirtualizedTreeRowIndex.ToString(CultureInfo.InvariantCulture));
            builder.Append(" depth:");
            builder.Append(row.VirtualizedTreeDepth.ToString(CultureInfo.InvariantCulture));
            builder.Append(" snapshot:");
            builder.Append(row.HasVirtualizedDisplaySnapshotForDiagnostics ? "true" : "false");
            builder.Append(" slot:(");
            builder.Append(row.LayoutSlot.X.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(row.LayoutSlot.Y.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(row.LayoutSlot.Width.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(row.LayoutSlot.Height.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(") headerOffset:");
            builder.Append(row.HeaderTextOffsetForDiagnostics.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" renderY:");
            builder.Append(row.VirtualizedHeaderRenderYForDiagnostics.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" snapshotRelativeY:");
            builder.Append(row.SnapshotHeaderTextRelativeYForDiagnostics.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" fontSize:");
            builder.Append(row.VirtualizedHeaderRenderFontSizeForDiagnostics.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" renderedHeader:");
            builder.AppendLine(row.RenderedHeaderForDiagnostics);
            index++;
        }

        return builder.ToString();
    }

    private static void CollectVisualDescendants<T>(UIElement element, List<T> result)
        where T : UIElement
    {
        if (element is T match)
        {
            result.Add(match);
        }

        foreach (var child in element.GetVisualChildren())
        {
            CollectVisualDescendants(child, result);
        }
    }

    private static ScrollViewer ResolveScrollbarProbeScrollViewer(UIElement element)
    {
        if (element is ScrollViewer scrollViewer)
        {
            return scrollViewer;
        }

        if (element is TreeView treeView)
        {
            return treeView.AutomationScrollViewer;
        }

        foreach (var child in element.GetVisualChildren())
        {
            try
            {
                return ResolveScrollbarProbeScrollViewer(child);
            }
            catch (InkkOopsCommandException)
            {
            }
        }

        throw new InkkOopsCommandException(
            InkkOopsFailureCategory.Unresolved,
            $"Target '{InkkOopsTargetResolver.DescribeElement(element)}' does not expose a ScrollViewer for scrollbar thumb probing.");
    }

    private static ScrollbarProbeAxis ParseScrollbarProbeAxis(string? axis)
    {
        if (string.IsNullOrWhiteSpace(axis) || string.Equals(axis, "vertical", StringComparison.OrdinalIgnoreCase) || string.Equals(axis, "y", StringComparison.OrdinalIgnoreCase))
        {
            return ScrollbarProbeAxis.Vertical;
        }

        if (string.Equals(axis, "horizontal", StringComparison.OrdinalIgnoreCase) || string.Equals(axis, "x", StringComparison.OrdinalIgnoreCase))
        {
            return ScrollbarProbeAxis.Horizontal;
        }

        throw new ArgumentException($"Unknown scrollbar probe axis '{axis}'. Expected vertical or horizontal.", nameof(axis));
    }

    private static float ResolveScrollbarProbePercent(string? text, ScrollbarProbeAxis axis, float defaultValue)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return defaultValue;
        }

        var normalized = text.Trim().ToLowerInvariant();
        return normalized switch
        {
            "top" => 0f,
            "left" => 0f,
            "bottom" => 1f,
            "right" => 1f,
            "start" => 0f,
            "end" => 1f,
            _ when float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) => Math.Clamp(value, 0f, 1f),
            _ => throw new ArgumentException($"Unknown scrollbar probe endpoint '{text}' for {axis.ToString().ToLowerInvariant()} axis.", nameof(text))
        };
    }

    private enum ScrollbarProbeAxis
    {
        Vertical,
        Horizontal
    }

    private readonly record struct ScrollbarProbeGeometry(ScrollbarProbeAxis Axis, LayoutRect ThumbRect, LayoutRect TrackRect)
    {
        public Vector2 ThumbCenter => new(
            ThumbRect.X + (ThumbRect.Width / 2f),
            ThumbRect.Y + (ThumbRect.Height / 2f));

        public Vector2 GetPointAtPercent(float percent)
        {
            percent = Math.Clamp(percent, 0f, 1f);
            if (Axis == ScrollbarProbeAxis.Vertical)
            {
                var travel = Math.Max(0f, TrackRect.Height - ThumbRect.Height);
                return new Vector2(
                    ThumbRect.X + (ThumbRect.Width / 2f),
                    TrackRect.Y + (ThumbRect.Height / 2f) + (travel * percent));
            }

            var horizontalTravel = Math.Max(0f, TrackRect.Width - ThumbRect.Width);
            return new Vector2(
                TrackRect.X + (ThumbRect.Width / 2f) + (horizontalTravel * percent),
                ThumbRect.Y + (ThumbRect.Height / 2f));
        }
    }

    private readonly record struct ScrollbarProbeSample(string FrameArtifact, string DiagnosticsArtifact, int BrightPixelCount, float AverageLuma);

    private async Task<InkkOopsPipeResponse> GetTargetDiagnosticsAsync(InkkOopsPipeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TargetName))
        {
            throw new ArgumentException("TargetName is required for this request.", nameof(request));
        }

        var state = _session.EvaluateTargetState(CreateTarget(request), CreateAnchor(request));
        var counterNames = ParseCounterNames(request.CounterNames);
        var runtimeDiagnostics = await _session.QueryOnUiThreadAsync(
            () => CaptureRuntimeDiagnosticsText(state.Element, request.Compact, counterNames),
            cancellationToken).ConfigureAwait(false);

        return Complete(request, value: FormatTargetDiagnostics(state, runtimeDiagnostics, request.Compact));
    }

    private InkkOopsPipeResponse GetHostInfo(InkkOopsPipeRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"default_pipe={InkkOopsHostConfiguration.BuiltInDefaultNamedPipeName}");
        builder.AppendLine($"artifact_root={_artifacts.DirectoryPath}");
        builder.AppendLine($"script_count={_scriptCatalog.ListScripts().Count()}");
        builder.AppendLine($"host_type={_session.Host.GetType().Name}");
        return Complete(request, value: builder.ToString());
    }

    private async Task<InkkOopsPipeResponse> RunScriptAsync(InkkOopsPipeRequest request, CancellationToken cancellationToken)
    {
        if (!_scriptCatalog.TryResolve(request.ScriptName, out var scriptDefinition) || scriptDefinition == null)
        {
            return new InkkOopsPipeResponse
            {
                Status = InkkOopsRunStatus.NotFound.ToString(),
                RequestKind = InkkOopsPipeRequestKinds.RunScript,
                ScriptName = request.ScriptName,
                Message = $"Unknown script '{request.ScriptName}'."
            };
        }

        var runner = new InkkOopsScriptRunner();
        var result = await runner.RunAsync(scriptDefinition.CreateScript(), _session, cancellationToken).ConfigureAwait(false);
        return new InkkOopsPipeResponse
        {
            Status = result.Status.ToString(),
            RequestKind = InkkOopsPipeRequestKinds.RunScript,
            ScriptName = result.ScriptName,
            ArtifactDirectory = result.ArtifactDirectory,
            Message = result.FailureMessage
        };
    }

    private static void ValidateTargetAndProperty(InkkOopsPipeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TargetName))
        {
            throw new ArgumentException("TargetName is required for this request.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.PropertyName))
        {
            throw new ArgumentException("PropertyName is required for this request.", nameof(request));
        }
    }

    private static object? ConvertStringValue(string? text, Type targetType)
    {
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (string.IsNullOrWhiteSpace(text))
        {
            return underlyingType == typeof(string) ? string.Empty : null;
        }

        if (underlyingType == typeof(string))
        {
            return text;
        }

        if (underlyingType == typeof(bool) && bool.TryParse(text, out var boolValue))
        {
            return boolValue;
        }

        if (underlyingType.IsEnum)
        {
            return Enum.Parse(underlyingType, text, ignoreCase: true);
        }

        return Convert.ChangeType(text, underlyingType, CultureInfo.InvariantCulture);
    }

    private static InkkOopsTargetReference CreateTarget(InkkOopsPipeRequest request)
    {
        var targetSelector = InkkOopsTargetSelector.Name(request.TargetName);
        if (string.IsNullOrWhiteSpace(request.ScopeTargetName))
        {
            return new InkkOopsTargetReference(targetSelector);
        }

        var scopeSelector = InkkOopsTargetSelector.Name(request.ScopeTargetName);
        return new InkkOopsTargetReference(InkkOopsTargetSelector.Within(scopeSelector, targetSelector));
    }

    private static InkkOopsTargetReference CreateScrollProviderTarget(InkkOopsPipeRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.TargetName))
        {
            return CreateTarget(request);
        }

        if (!string.IsNullOrWhiteSpace(request.OwnerTargetName))
        {
            return new InkkOopsTargetReference(request.OwnerTargetName);
        }

        throw new ArgumentException("TargetName or OwnerTargetName is required for scroll-to and scroll-by requests.", nameof(request));
    }

    private static InkkOopsTargetReference CreateOwnerTarget(InkkOopsPipeRequest request)
    {
        return new InkkOopsTargetReference(request.OwnerTargetName);
    }

    private static InkkOopsPointerAnchor CreateAnchor(InkkOopsPipeRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Anchor) &&
            request.Anchor.Trim().Equals("offset", StringComparison.OrdinalIgnoreCase))
        {
            return InkkOopsPointerAnchor.OffsetBy(request.OffsetX, request.OffsetY);
        }

        if (request.OffsetX != 0f || request.OffsetY != 0f)
        {
            return InkkOopsPointerAnchor.OffsetBy(request.OffsetX, request.OffsetY);
        }

        return NormalizeAnchorName(request.Anchor) switch
        {
            "top-left" => InkkOopsPointerAnchor.TopLeft,
            "topleft" => InkkOopsPointerAnchor.TopLeft,
            "top-right" => InkkOopsPointerAnchor.TopRight,
            "topright" => InkkOopsPointerAnchor.TopRight,
            "bottom-left" => InkkOopsPointerAnchor.BottomLeft,
            "bottomleft" => InkkOopsPointerAnchor.BottomLeft,
            "bottom-right" => InkkOopsPointerAnchor.BottomRight,
            "bottomright" => InkkOopsPointerAnchor.BottomRight,
            _ => InkkOopsPointerAnchor.Center
        };
    }

    private static string NormalizeAnchorName(string? anchor)
    {
        return string.IsNullOrWhiteSpace(anchor)
            ? string.Empty
            : anchor.Trim().ToLowerInvariant();
    }

    private static InkkOopsPointerMotion CreateMotion(InkkOopsPipeRequest request)
    {
        var easing = ParseEasing(request.Easing);
        if (request.TravelFrames > 0)
        {
            return InkkOopsPointerMotion.WithTravelFrames(request.TravelFrames, easing, ResolveStepDistance(request));
        }

        if (request.StepDistance > 0f)
        {
            return InkkOopsPointerMotion.WithStepDistance(request.StepDistance, easing);
        }

        return string.IsNullOrWhiteSpace(request.Easing)
            ? InkkOopsPointerMotion.Default
            : InkkOopsPointerMotion.WithStepDistance(InkkOopsPointerMotion.Default.StepDistance, easing);
    }

    private static float ResolveStepDistance(InkkOopsPipeRequest request)
    {
        return request.StepDistance > 0f ? request.StepDistance : InkkOopsPointerMotion.Default.StepDistance;
    }

    private static InkkOopsPointerEasing ParseEasing(string? easing)
    {
        if (string.IsNullOrWhiteSpace(easing))
        {
            return InkkOopsPointerEasing.Linear;
        }

        return easing.Trim().ToLowerInvariant() switch
        {
            "ease-in-out" => InkkOopsPointerEasing.EaseInOut,
            "easeinout" => InkkOopsPointerEasing.EaseInOut,
            _ => InkkOopsPointerEasing.Linear
        };
    }

    private static string FormatTargetDiagnostics(InkkOopsTargetStateSnapshot state, string runtimeDiagnostics, bool compact)
    {
        var builder = new StringBuilder();
        if (!compact)
        {
            builder.AppendLine($"selector={state.Resolution.Selector}");
        }

        builder.AppendLine($"resolution.status={state.Resolution.Status}");
        builder.AppendLine($"element={InkkOopsTargetResolver.DescribeElement(state.Element)}");
        builder.AppendLine($"interactive={state.IsInteractive}");
        builder.AppendLine($"failure.category={state.FailureCategory}");
        if (compact)
        {
            builder.AppendLine($"visible={state.IsVisible}");
            builder.AppendLine($"enabled={state.IsEnabled}");
            builder.AppendLine($"in_viewport={state.IsInViewport}");
        }
        else
        {
            builder.AppendLine($"resolution.source={state.Resolution.Source}");
        builder.AppendLine($"peer={(state.Peer == null ? "null" : InkkOopsTargetResolver.DescribePeer(state.Peer))}");
        builder.AppendLine($"failure.reason={state.Reason}");
        builder.AppendLine($"visible={state.IsVisible}");
        builder.AppendLine($"enabled={state.IsEnabled}");
        builder.AppendLine($"in_viewport={state.IsInViewport}");
        builder.AppendLine($"hit_test_visible_at_action_point={state.IsHitTestVisibleAtActionPoint}");
        if (state.HasBounds)
        {
            builder.AppendLine($"bounds=({state.Bounds.X:0.###},{state.Bounds.Y:0.###},{state.Bounds.Width:0.###},{state.Bounds.Height:0.###})");
        }

        if (state.HasViewportBounds)
        {
            builder.AppendLine($"viewport=({state.ViewportBounds.X:0.###},{state.ViewportBounds.Y:0.###},{state.ViewportBounds.Width:0.###},{state.ViewportBounds.Height:0.###})");
        }

        if (state.HasActionPoint)
        {
            builder.AppendLine($"action_point=({state.ActionPoint.X:0.###},{state.ActionPoint.Y:0.###})");
        }

        foreach (var note in state.Resolution.Notes)
        {
            builder.AppendLine($"resolution.note={note}");
        }

        foreach (var candidate in state.Resolution.Candidates)
        {
            builder.AppendLine($"resolution.candidate={candidate}");
        }
        }

        if (!string.IsNullOrWhiteSpace(runtimeDiagnostics))
        {
            builder.Append(runtimeDiagnostics);
        }

        return builder.ToString();
    }

    private static string CaptureRuntimeDiagnosticsText(UIElement? element, bool compact, IReadOnlySet<string> counterNames)
    {
        if (element == null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var currentType = element.GetType(); currentType != null; currentType = currentType.BaseType)
        {
            foreach (var method in currentType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (method.GetParameters().Length != 0 ||
                    !method.Name.StartsWith("Get", StringComparison.Ordinal) ||
                    !method.Name.EndsWith("SnapshotForDiagnostics", StringComparison.Ordinal) ||
                    method.ReturnType == typeof(void))
                {
                    continue;
                }

                var snapshot = method.Invoke(element, null);
                AppendSnapshot(builder, method.ReturnType.Name, snapshot, compact, counterNames);
            }
        }

        return builder.ToString();
    }

    private static void AppendSnapshot(StringBuilder builder, string snapshotName, object? snapshot, bool compact, IReadOnlySet<string> counterNames)
    {
        if (snapshot == null)
        {
            return;
        }

        foreach (var property in snapshot.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0)
            {
                continue;
            }

            if (compact && counterNames.Count > 0 &&
                !counterNames.Contains(property.Name) &&
                !counterNames.Contains($"{snapshotName}.{property.Name}"))
            {
                continue;
            }

            builder.Append(snapshotName);
            builder.Append('.');
            builder.Append(property.Name);
            builder.Append('=');
            builder.AppendLine(InkkOopsCommandUtilities.FormatObject(property.GetValue(snapshot)));
        }
    }

    private static IReadOnlySet<string> ParseCounterNames(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return text.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
    }

    private async Task<InkkOopsPipeResponse> TextInputAsync(InkkOopsPipeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length == 0)
        {
            throw new ArgumentException("Text must contain at least one character.", nameof(request));
        }

        foreach (var c in request.Text)
        {
            await new InkkOopsTextInputCommand(c)
                .ExecuteAsync(_session, cancellationToken)
                .ConfigureAwait(false);
        }

        return Complete(request, value: request.Text);
    }

    private async Task<InkkOopsPipeResponse> PointerDownAsync(InkkOopsPipeRequest request, CancellationToken cancellationToken)
    {
        var button = ParseMouseButton(request);
        if (request.X is float x && request.Y is float y)
        {
            await new InkkOopsPointerDownCommand(new Vector2(x, y), button)
                .ExecuteAsync(_session, cancellationToken)
                .ConfigureAwait(false);
            return Complete(request);
        }

        if (!string.IsNullOrWhiteSpace(request.TargetName))
        {
            var point = _session.ResolveRequiredActionPoint(CreateTarget(request), CreateAnchor(request));
            await _session.PressPointerAsync(point, button, cancellationToken).ConfigureAwait(false);
            return Complete(request);
        }

        throw new ArgumentException("Either TargetName or both X and Y are required for pointer-down.", nameof(request));
    }

    private async Task<InkkOopsPipeResponse> PointerUpAsync(InkkOopsPipeRequest request, CancellationToken cancellationToken)
    {
        var button = ParseMouseButton(request);
        if (request.X is float x && request.Y is float y)
        {
            await new InkkOopsPointerUpCommand(new Vector2(x, y), button)
                .ExecuteAsync(_session, cancellationToken)
                .ConfigureAwait(false);
            return Complete(request);
        }

        if (!string.IsNullOrWhiteSpace(request.TargetName))
        {
            var point = _session.ResolveRequiredActionPoint(CreateTarget(request), CreateAnchor(request));
            await _session.ReleasePointerAsync(point, button, cancellationToken).ConfigureAwait(false);
            return Complete(request);
        }

        throw new ArgumentException("Either TargetName or both X and Y are required for pointer-up.", nameof(request));
    }

    private async Task<InkkOopsPipeResponse> MovePointerPathAsync(InkkOopsPipeRequest request, CancellationToken cancellationToken)
    {
        var waypoints = ParseWaypoints(request);
        if (waypoints.Count == 0)
        {
            // Fall back to a single point from X/Y
            if (request.X is not float x || request.Y is not float y)
            {
                throw new ArgumentException("Either Waypoints or both X and Y are required for move-pointer-path.", nameof(request));
            }

            waypoints = new List<Vector2> { new Vector2(x, y) };
        }

        await new InkkOopsMovePointerPathCommand(waypoints, CreateMotion(request))
            .ExecuteAsync(_session, cancellationToken)
            .ConfigureAwait(false);
        return Complete(request);
    }

    private static MouseButton ParseMouseButton(InkkOopsPipeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ButtonName))
        {
            return MouseButton.Left;
        }

        return request.ButtonName.Trim().ToLowerInvariant() switch
        {
            "left" => MouseButton.Left,
            "right" => MouseButton.Right,
            "middle" => MouseButton.Middle,
            "xbutton1" => MouseButton.XButton1,
            "xbutton2" => MouseButton.XButton2,
            _ => MouseButton.Left
        };
    }

    private static IReadOnlyList<Vector2> ParseWaypoints(InkkOopsPipeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Waypoints))
        {
            return Array.Empty<Vector2>();
        }

        try
        {
            var points = System.Text.Json.JsonSerializer.Deserialize<List<WaypointJson>>(request.Waypoints);
            if (points == null || points.Count == 0)
            {
                return Array.Empty<Vector2>();
            }

            return points.Select(p => new Vector2(p.X, p.Y)).ToArray();
        }
        catch
        {
            throw new ArgumentException(
                $"Waypoints must be a JSON array of {{X,Y}} objects, e.g. '[{{\"X\":100,\"Y\":200}},{{\"X\":300,\"Y\":400}}]'. Received: '{request.Waypoints}'",
                nameof(request));
        }
    }

    private sealed record WaypointJson(float X, float Y);

    private static Keys ParseKey(InkkOopsPipeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.KeyName))
        {
            throw new ArgumentException("KeyName is required for key-down/key-up.", nameof(request));
        }

        if (!Enum.TryParse<Keys>(request.KeyName, ignoreCase: true, out var key))
        {
            throw new ArgumentException($"Unknown key name '{request.KeyName}'. Valid values are defined in Microsoft.Xna.Framework.Input.Keys.", nameof(request));
        }

        return key;
    }

    private static AutomationEventType ParseAutomationEventType(InkkOopsPipeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EventType))
        {
            throw new ArgumentException("EventType is required for assert-automation-event.", nameof(request));
        }

        if (!Enum.TryParse<AutomationEventType>(request.EventType, ignoreCase: true, out var eventType))
        {
            throw new ArgumentException($"Unknown AutomationEventType '{request.EventType}'.", nameof(request));
        }

        return eventType;
    }

    private static string ResolveArtifactName(InkkOopsPipeRequest request, string fallback)
    {
        return string.IsNullOrWhiteSpace(request.ArtifactName) ? fallback : request.ArtifactName;
    }

    private static string NormalizeKind(string? requestKind)
    {
        return string.IsNullOrWhiteSpace(requestKind)
            ? InkkOopsPipeRequestKinds.RunScript
            : requestKind.Trim();
    }

    private static InkkOopsPipeResponse Complete(InkkOopsPipeRequest request, string message = "", string value = "")
    {
        return new InkkOopsPipeResponse
        {
            Status = InkkOopsRunStatus.Completed.ToString(),
            RequestKind = NormalizeKind(request.RequestKind),
            ScriptName = request.ScriptName,
            Message = message,
            Value = value
        };
    }

    private static InkkOopsPipeResponse Fail(InkkOopsPipeRequest request, string message)
    {
        return new InkkOopsPipeResponse
        {
            Status = InkkOopsRunStatus.Failed.ToString(),
            RequestKind = NormalizeKind(request.RequestKind),
            ScriptName = request.ScriptName,
            Message = message,
            Value = string.Empty
        };
    }
}