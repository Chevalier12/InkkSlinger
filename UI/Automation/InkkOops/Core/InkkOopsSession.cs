using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsSession
{
    private InkkOopsTargetResolutionReport? _lastResolutionReport;
    private Vector2? _lastActionPoint;

    public InkkOopsSession(IInkkOopsHost host, InkkOopsArtifacts artifacts)
    {
        Host = host ?? throw new ArgumentNullException(nameof(host));
        Artifacts = artifacts ?? throw new ArgumentNullException(nameof(artifacts));
    }

    public IInkkOopsHost Host { get; }

    public InkkOopsArtifacts Artifacts { get; }

    public UiRoot UiRoot => Host.UiRoot;

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
        RecordActionPoint(position);
        return Host.MovePointerAsync(position, cancellationToken);
    }

    public Task PressPointerAsync(Vector2 position, CancellationToken cancellationToken = default)
    {
        RecordActionPoint(position);
        return Host.PressPointerAsync(position, cancellationToken);
    }

    public Task ReleasePointerAsync(Vector2 position, CancellationToken cancellationToken = default)
    {
        RecordActionPoint(position);
        return Host.ReleasePointerAsync(position, cancellationToken);
    }

    public Task WheelAsync(int delta, CancellationToken cancellationToken = default)
    {
        return Host.WheelAsync(delta, cancellationToken);
    }

    public Task CaptureFrameAsync(string artifactName, CancellationToken cancellationToken = default)
    {
        return Host.CaptureFrameAsync(artifactName, cancellationToken);
    }

    public async Task WriteTelemetryAsync(string artifactName, CancellationToken cancellationToken = default)
    {
        await Host.WriteTelemetryAsync(artifactName, cancellationToken).ConfigureAwait(false);

        var path = Artifacts.GetPath(artifactName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ? artifactName : artifactName + ".txt");
        var builder = new StringBuilder();
        if (File.Exists(path))
        {
            builder.Append(File.ReadAllText(path));
            if (builder.Length > 0 && builder[^1] != '\n')
            {
                builder.AppendLine();
            }
        }

        builder.AppendLine($"last_resolution={_lastResolutionReport?.Describe() ?? "none"}");
        builder.AppendLine(_lastActionPoint is Vector2 actionPoint
            ? $"last_action_point=({actionPoint.X:0.###},{actionPoint.Y:0.###})"
            : "last_action_point=none");
        File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
    }

    public IReadOnlyList<AutomationEventRecord> GetAutomationEventsSnapshot()
    {
        return Host.GetAutomationEventsSnapshot();
    }

    public void ClearAutomationEvents()
    {
        Host.ClearAutomationEvents();
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
            var actionPoint = hasBounds ? InkkOopsCommandUtilities.GetAnchorPoint(bounds, chosenAnchor) : Vector2.Zero;
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
}
