using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsSession
{
    private InkkOopsTargetResolutionReport? _lastResolutionReport;
    private Vector2? _lastActionPoint;
    private int? _currentActionCommandIndex;
    private readonly HashSet<int> _actionDiagnosticsIndexes;
    private readonly IReadOnlyList<InkkOopsObjectObserver> _objectObservers;
    private readonly Dictionary<string, List<string>> _objectObserverArtifactLines = new(StringComparer.OrdinalIgnoreCase);

    public InkkOopsSession(
        IInkkOopsHost host,
        InkkOopsArtifacts artifacts,
        IEnumerable<int>? actionDiagnosticsIndexes = null,
        IEnumerable<InkkOopsObjectObserver>? objectObservers = null)
    {
        Host = host ?? throw new ArgumentNullException(nameof(host));
        Artifacts = artifacts ?? throw new ArgumentNullException(nameof(artifacts));
        _actionDiagnosticsIndexes = actionDiagnosticsIndexes == null
            ? []
            : [.. actionDiagnosticsIndexes.Where(static index => index >= 0)];
        _objectObservers = objectObservers == null
            ? []
            : [.. objectObservers
                .OrderBy(static observer => observer.Order)
                .ThenBy(static observer => observer.GetType().FullName, StringComparer.Ordinal)];
        Host.SetArtifactRoot(artifacts.DirectoryPath);
    }

    public IInkkOopsHost Host { get; }

    public InkkOopsArtifacts Artifacts { get; }

    public UiRoot UiRoot => Host.UiRoot;

    public IReadOnlyCollection<int> ActionDiagnosticsIndexes => new ReadOnlyCollection<int>(_actionDiagnosticsIndexes.OrderBy(static index => index).ToArray());

    public InkkOopsTargetResolutionReport ResolveTarget(InkkOopsTargetReference target)
    {
        var report = InkkOopsTargetResolver.Resolve(Host, target);
        RecordResolution(report);
        return report;
    }

    public UIElement ResolveRequiredTarget(InkkOopsTargetReference target)
    {
        var report = ResolveTarget(target);
        return report.Status switch
        {
            InkkOopsTargetResolutionStatus.Resolved when report.Element != null => report.Element,
            InkkOopsTargetResolutionStatus.Ambiguous => throw new InkkOopsCommandException(
                InkkOopsFailureCategory.Ambiguous,
                $"Target '{target}' resolved ambiguously.{Environment.NewLine}{report.Describe()}"),
            _ => throw new InkkOopsCommandException(
                InkkOopsFailureCategory.Unresolved,
                $"Could not resolve InkkOops target '{target}'.{Environment.NewLine}{report.Describe()}")
        };
    }

    public AutomationPeer ResolveRequiredAutomationPeer(InkkOopsTargetReference target)
    {
        var report = ResolveTarget(target);
        if (report.Status == InkkOopsTargetResolutionStatus.Ambiguous)
        {
            throw new InkkOopsCommandException(
                InkkOopsFailureCategory.Ambiguous,
                $"Target '{target}' resolved ambiguously.{Environment.NewLine}{report.Describe()}");
        }

        if (report.Peer != null)
        {
            return report.Peer;
        }

        throw new InkkOopsCommandException(
            InkkOopsFailureCategory.Unresolved,
            $"Could not resolve automation peer for target '{target}'.{Environment.NewLine}{report.Describe()}");
    }

    public Task ExecuteOnUiThreadAsync(Action action, CancellationToken cancellationToken = default)
    {
        return Host.ExecuteOnUiThreadAsync(action, cancellationToken);
    }

    public Task<T> QueryOnUiThreadAsync<T>(Func<T> query, CancellationToken cancellationToken = default)
    {
        return Host.QueryOnUiThreadAsync(query, cancellationToken);
    }

    public Task ResizeWindowAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        return Host.ResizeWindowAsync(width, height, cancellationToken);
    }

    public Task MaximizeWindowAsync(CancellationToken cancellationToken = default)
    {
        return Host.MaximizeWindowAsync(cancellationToken);
    }

    public Task WaitFramesAsync(int frameCount, CancellationToken cancellationToken = default)
    {
        return Host.AdvanceFrameAsync(frameCount, cancellationToken);
    }

    public Task WaitForIdleAsync(InkkOopsIdlePolicy policy = InkkOopsIdlePolicy.LayoutAndRender, CancellationToken cancellationToken = default)
    {
        return Host.WaitForIdleAsync(policy, cancellationToken);
    }

    public Task MovePointerAsync(Vector2 position, CancellationToken cancellationToken = default)
    {
        return TrackPointerMoveAsync(position, InkkOopsPointerMotion.Default, cancellationToken);
    }

    public Task MovePointerAsync(Vector2 position, InkkOopsPointerMotion motion, CancellationToken cancellationToken = default)
    {
        return TrackPointerMoveAsync(position, motion, cancellationToken);
    }

    public Task PressPointerAsync(Vector2 position, MouseButton button = MouseButton.Left, CancellationToken cancellationToken = default)
    {
        return TrackPointerDownAsync(position, button, cancellationToken);
    }

    public Task ReleasePointerAsync(Vector2 position, MouseButton button = MouseButton.Left, CancellationToken cancellationToken = default)
    {
        return TrackPointerUpAsync(position, button, cancellationToken);
    }

    public Task KeyDownAsync(Microsoft.Xna.Framework.Input.Keys key, CancellationToken cancellationToken = default)
    {
        return Host.KeyDownAsync(key, cancellationToken);
    }

    public Task KeyUpAsync(Microsoft.Xna.Framework.Input.Keys key, CancellationToken cancellationToken = default)
    {
        return Host.KeyUpAsync(key, cancellationToken);
    }

    public Task TextInputAsync(char character, CancellationToken cancellationToken = default)
    {
        return Host.TextInputAsync(character, cancellationToken);
    }

    public Task WheelAsync(int delta, CancellationToken cancellationToken = default)
    {
        return TrackWheelAsync(delta, cancellationToken);
    }

    internal void BeginActionCommand(int commandIndex)
    {
        _currentActionCommandIndex = commandIndex;
    }

    internal void EndActionCommand()
    {
        _currentActionCommandIndex = null;
    }

    internal bool ShouldCaptureActionDiagnostics(int actionIndex)
    {
        return _actionDiagnosticsIndexes.Contains(actionIndex);
    }

    public Task CaptureFrameAsync(string artifactName, CancellationToken cancellationToken = default)
    {
        return Host.CaptureFrameAsync(artifactName, cancellationToken);
    }

    public async Task WriteTelemetryAsync(string artifactName, CancellationToken cancellationToken = default)
    {
        var telemetryText = await Host.CaptureTelemetryAsync(artifactName, cancellationToken).ConfigureAwait(false);
        var builder = new StringBuilder();
        builder.Append(telemetryText);
        if (builder.Length > 0 && builder[^1] != '\n')
        {
            builder.AppendLine();
        }

        builder.AppendLine($"last_resolution={_lastResolutionReport?.Describe() ?? "none"}");
        builder.AppendLine(_lastActionPoint is Vector2 actionPoint
            ? $"last_action_point=({actionPoint.X:0.###},{actionPoint.Y:0.###})"
            : "last_action_point=none");
        var fileName = artifactName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ? artifactName : artifactName + ".txt";
        Artifacts.BufferTextArtifact(fileName, builder.ToString());
    }

    public async Task WriteActionDiagnosticsAsync(int actionIndex, string actionDescription, CancellationToken cancellationToken = default)
    {
        var artifactName = $"action[{actionIndex}]";
        var telemetryText = await Host.CaptureTelemetryAsync(artifactName, cancellationToken).ConfigureAwait(false);
        var builder = new StringBuilder();
        builder.AppendLine($"action_index={actionIndex}");
        builder.AppendLine($"action_description={actionDescription}");
        builder.Append(telemetryText);
        if (builder.Length > 0 && builder[^1] != '\n')
        {
            builder.AppendLine();
        }

        builder.AppendLine($"last_resolution={_lastResolutionReport?.Describe() ?? "none"}");
        builder.AppendLine(_lastActionPoint is Vector2 actionPoint
            ? $"last_action_point=({actionPoint.X:0.###},{actionPoint.Y:0.###})"
            : "last_action_point=none");
        Artifacts.BufferTextArtifact(artifactName + ".txt", builder.ToString());
    }

    public async Task WriteObjectObserverArtifactsAsync(int actionIndex, string actionDescription, CancellationToken cancellationToken = default)
    {
        if (_objectObservers.Count == 0)
        {
            return;
        }

        for (var i = 0; i < _objectObservers.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var observer = _objectObservers[i];
            var context = new InkkOopsObjectObserverContext
            {
                Session = this,
                ActionIndex = actionIndex,
                ActionDescription = actionDescription
            };
            var line = await QueryOnUiThreadAsync(() => observer.CaptureLine(context), cancellationToken).ConfigureAwait(false);
            AppendObjectObserverLine(observer.DumpFileName, line);
        }
    }

    public IReadOnlyList<AutomationEventRecord> GetAutomationEventsSnapshot()
    {
        return Host.GetAutomationEventsSnapshot();
    }

    public void ClearAutomationEvents()
    {
        Host.ClearAutomationEvents();
    }

    private void AppendObjectObserverLine(string fileName, string line)
    {
        if (!_objectObserverArtifactLines.TryGetValue(fileName, out var lines))
        {
            lines = new List<string>();
            _objectObserverArtifactLines[fileName] = lines;
        }

        lines.Add(line);
        Artifacts.BufferTextArtifact(fileName, string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    public InkkOopsTargetStateSnapshot EvaluateTargetState(
        InkkOopsTargetReference target,
        InkkOopsPointerAnchor? anchor = null)
    {
        var report = ResolveTarget(target);
        if (report.Status != InkkOopsTargetResolutionStatus.Resolved || report.Element == null)
        {
            return InkkOopsTargetStateSnapshot.Unresolved(
                report,
                report.Status == InkkOopsTargetResolutionStatus.Ambiguous
                    ? InkkOopsFailureCategory.Ambiguous
                    : InkkOopsFailureCategory.Unresolved);
        }

        return QueryOnUiThreadAsync(() =>
        {
            var element = report.Element;
            var viewport = Host.GetViewportBounds();
            var hasBounds = element.TryGetRenderBoundsInRootSpace(out var bounds) && bounds.Width > 0f && bounds.Height > 0f;
            var chosenAnchor = anchor ?? InkkOopsPointerAnchor.Center;
            var actionPoint = hasBounds ? InkkOopsCommandUtilities.GetPreferredActionPoint(element, bounds, viewport, chosenAnchor) : Vector2.Zero;
            var hasActionPoint = hasBounds;
            var isVisible = element.IsVisible && element.Visibility == Visibility.Visible && element.Opacity > 0f;
            var isEnabled = element.IsEnabled && element.IsHitTestVisible;
            var isInViewport = hasActionPoint && InkkOopsCommandUtilities.Contains(viewport, actionPoint);
            var hitTestVisible = hasActionPoint && element.HitTest(actionPoint);
            var failureCategory = InkkOopsFailureCategory.None;
            var reason = string.Empty;

            if (!hasBounds)
            {
                failureCategory = InkkOopsFailureCategory.Unrealized;
                reason = "target does not have arranged render bounds";
            }
            else if (!isVisible)
            {
                failureCategory = InkkOopsFailureCategory.NotInteractive;
                reason = "target is not visible";
            }
            else if (!isEnabled)
            {
                failureCategory = InkkOopsFailureCategory.Disabled;
                reason = "target is disabled or not hit test visible";
            }
            else if (!isInViewport)
            {
                failureCategory = InkkOopsFailureCategory.Offscreen;
                reason = "action point is outside the viewport";
            }
            else if (!hitTestVisible)
            {
                failureCategory = InkkOopsFailureCategory.Clipped;
                reason = "target does not hit test at the chosen action point";
            }

            var interactive = failureCategory == InkkOopsFailureCategory.None;
            var snapshot = new InkkOopsTargetStateSnapshot
            {
                Resolution = report,
                Element = element,
                Peer = report.Peer,
                Bounds = bounds,
                HasBounds = hasBounds,
                ViewportBounds = viewport,
                HasViewportBounds = true,
                ActionPoint = actionPoint,
                HasActionPoint = hasActionPoint,
                IsVisible = isVisible,
                IsEnabled = isEnabled,
                IsInViewport = isInViewport,
                IsHitTestVisibleAtActionPoint = hitTestVisible,
                IsInteractive = interactive,
                FailureCategory = failureCategory,
                Reason = reason
            };

            if (snapshot.HasActionPoint)
            {
                RecordActionPoint(snapshot.ActionPoint);
            }

            return snapshot;
        }).GetAwaiter().GetResult();
    }

    public Vector2 ResolveRequiredActionPoint(
        InkkOopsTargetReference target,
        InkkOopsPointerAnchor? anchor = null)
    {
        var state = EvaluateTargetState(target, anchor);
        if (state.IsInteractive && state.HasActionPoint)
        {
            return state.ActionPoint;
        }

        var category = state.FailureCategory == InkkOopsFailureCategory.None
            ? InkkOopsFailureCategory.NotInteractive
            : state.FailureCategory;
        throw new InkkOopsCommandException(
            category,
            $"Target '{target}' is not interactive. reason={state.Reason}{Environment.NewLine}{state.Resolution.Describe()}");
    }

    public void RecordResolution(InkkOopsTargetResolutionReport report)
    {
        _lastResolutionReport = report;
    }

    public void RecordActionPoint(Vector2 position)
    {
        _lastActionPoint = position;
    }

    private async Task TrackPointerMoveAsync(Vector2 position, InkkOopsPointerMotion motion, CancellationToken cancellationToken)
    {
        RecordActionPoint(position);
        var before = await CaptureActionSnapshotAsync(cancellationToken).ConfigureAwait(false);
        await Host.MovePointerAsync(position, motion, cancellationToken).ConfigureAwait(false);
        var after = await CaptureActionSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var displayedFps = await CaptureDisplayedFpsAsync(cancellationToken).ConfigureAwait(false);

        foreach (var entry in InkkOopsActionLogFormatter.CreatePointerMoveEntries(GetCurrentActionCommandIndex(), position, before.Hovered, after.Hovered, displayedFps))
        {
            Artifacts.LogActionEntry(entry.Subject, entry.Details);
        }
    }

    private async Task TrackPointerDownAsync(Vector2 position, MouseButton button, CancellationToken cancellationToken)
    {
        RecordActionPoint(position);
        var before = await CaptureActionSnapshotAsync(cancellationToken).ConfigureAwait(false);
        await Host.PressPointerAsync(position, button, cancellationToken).ConfigureAwait(false);
        var after = await CaptureActionSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var displayedFps = await CaptureDisplayedFpsAsync(cancellationToken).ConfigureAwait(false);

        var entry = InkkOopsActionLogFormatter.CreatePointerDownEntry(GetCurrentActionCommandIndex(), position, before.Hovered, before.Captured, after.Hovered, after.Captured, after.ClickDown, displayedFps);
        Artifacts.LogActionEntry(entry.Subject, entry.Details);
    }

    private async Task TrackPointerUpAsync(Vector2 position, MouseButton button, CancellationToken cancellationToken)
    {
        RecordActionPoint(position);
        var before = await CaptureActionSnapshotAsync(cancellationToken).ConfigureAwait(false);
        await Host.ReleasePointerAsync(position, button, cancellationToken).ConfigureAwait(false);
        var after = await CaptureActionSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var displayedFps = await CaptureDisplayedFpsAsync(cancellationToken).ConfigureAwait(false);

        var entry = InkkOopsActionLogFormatter.CreatePointerUpEntry(GetCurrentActionCommandIndex(), position, before.Hovered, before.Captured, after.Hovered, after.ClickUp, displayedFps);
        Artifacts.LogActionEntry(entry.Subject, entry.Details);
    }

    private async Task TrackWheelAsync(int delta, CancellationToken cancellationToken)
    {
        var before = await CaptureActionSnapshotAsync(cancellationToken).ConfigureAwait(false);
        await Host.WheelAsync(delta, cancellationToken).ConfigureAwait(false);
        var after = await CaptureActionSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var displayedFps = await CaptureDisplayedFpsAsync(cancellationToken).ConfigureAwait(false);

        var entry = InkkOopsActionLogFormatter.CreateWheelEntry(GetCurrentActionCommandIndex(), delta, before.Hovered, before.Captured, after.Hovered, after.Captured, displayedFps);
        Artifacts.LogActionEntry(entry.Subject, entry.Details);
    }

    private Task<InkkOopsActionRuntimeSnapshot> CaptureActionSnapshotAsync(CancellationToken cancellationToken)
    {
        return QueryOnUiThreadAsync(
            () => new InkkOopsActionRuntimeSnapshot(
                UiRoot.GetHoveredElementForDiagnostics(),
                FocusManager.GetCapturedPointerElement(),
                UiRoot.GetLastClickDownTargetForDiagnostics(),
                UiRoot.GetLastClickUpTargetForDiagnostics()),
            cancellationToken);
    }

    private int GetCurrentActionCommandIndex()
    {
        return _currentActionCommandIndex ?? -1;
    }

    private Task<string> CaptureDisplayedFpsAsync(CancellationToken cancellationToken)
    {
        return QueryOnUiThreadAsync(() => Host.GetDisplayedFps(), cancellationToken);
    }

    private readonly record struct InkkOopsActionRuntimeSnapshot(
        UIElement? Hovered,
        UIElement? Captured,
        UIElement? ClickDown,
        UIElement? ClickUp);
}
