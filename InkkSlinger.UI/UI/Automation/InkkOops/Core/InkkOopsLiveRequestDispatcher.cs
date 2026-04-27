using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsLiveRequestDispatcher : IDisposable
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
                InkkOopsPipeRequestKinds.HoverTarget => await ExecuteCommandAsync(request, new InkkOopsHoverTargetCommand(CreateTarget(request)), cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.ClickTarget => await ExecuteCommandAsync(request, new InkkOopsClickTargetCommand(CreateTarget(request)), cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.InvokeTarget => await ExecuteCommandAsync(request, new InkkOopsInvokeTargetCommand(CreateTarget(request)), cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.WaitFrames => await WaitFramesAsync(request, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.WaitForElement => await WaitForTargetAsync(request, InkkOopsWaitCondition.Exists, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.WaitForVisible => await WaitForTargetAsync(request, InkkOopsWaitCondition.Visible, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.WaitForEnabled => await WaitForTargetAsync(request, InkkOopsWaitCondition.Enabled, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.WaitForInViewport => await WaitForTargetAsync(request, InkkOopsWaitCondition.InViewport, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.WaitForInteractive => await WaitForTargetAsync(request, InkkOopsWaitCondition.Interactive, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.WaitForIdle => await ExecuteCommandAsync(request, new InkkOopsWaitForIdleCommand(), cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.Wheel => await WheelAsync(request, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.ScrollTo => await ExecuteCommandAsync(request, new InkkOopsScrollToCommand(CreateTarget(request), request.HorizontalPercent, request.VerticalPercent), cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.ScrollBy => await ExecuteCommandAsync(request, new InkkOopsScrollByCommand(CreateTarget(request), request.HorizontalPercent, request.VerticalPercent), cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.ScrollIntoView => await ScrollIntoViewAsync(request, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.GetTelemetry => await GetTelemetryAsync(request, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.GetTargetDiagnostics => await GetTargetDiagnosticsAsync(request, cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.GetHostInfo => GetHostInfo(request),
                InkkOopsPipeRequestKinds.DragTarget => await ExecuteCommandAsync(request, new InkkOopsDragTargetCommand(CreateTarget(request), request.DeltaX, request.DeltaY), cancellationToken).ConfigureAwait(false),
                InkkOopsPipeRequestKinds.RunScript => await RunScriptAsync(request, cancellationToken).ConfigureAwait(false),
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

        var command = new InkkOopsWaitForElementCommand(CreateTarget(request), Math.Max(1, request.FrameCount), condition);
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
            await new InkkOopsWheelTargetCommand(CreateTarget(request), request.WheelDelta)
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

    private async Task<InkkOopsPipeResponse> GetTargetDiagnosticsAsync(InkkOopsPipeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TargetName))
        {
            throw new ArgumentException("TargetName is required for this request.", nameof(request));
        }

        var state = _session.EvaluateTargetState(CreateTarget(request));
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

    private static InkkOopsTargetReference CreateOwnerTarget(InkkOopsPipeRequest request)
    {
        return new InkkOopsTargetReference(request.OwnerTargetName);
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