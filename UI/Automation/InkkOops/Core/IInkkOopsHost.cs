using System;
using System.Numerics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public interface IInkkOopsHost
{
    UiRoot UiRoot { get; }

    IReadOnlyList<IInkkOopsSemanticLogContributor> SemanticLogContributors { get; }

    string ArtifactRoot { get; }

    UIElement? GetVisualRootElement();

    LayoutRect GetViewportBounds();

    Task ResizeWindowAsync(int width, int height, CancellationToken cancellationToken = default);

    Task MaximizeWindowAsync(CancellationToken cancellationToken = default);

    Task AdvanceFrameAsync(int frameCount, CancellationToken cancellationToken = default);

    Task WaitForIdleAsync(InkkOopsIdlePolicy policy, CancellationToken cancellationToken = default);

    Task MovePointerAsync(Vector2 position, CancellationToken cancellationToken = default);

    Task PressPointerAsync(Vector2 position, CancellationToken cancellationToken = default);

    Task ReleasePointerAsync(Vector2 position, CancellationToken cancellationToken = default);

    Task WheelAsync(int delta, CancellationToken cancellationToken = default);

    Task CaptureFrameAsync(string artifactName, CancellationToken cancellationToken = default);

    Task WriteTelemetryAsync(string artifactName, CancellationToken cancellationToken = default);

    UIElement? FindElement(string identifier);

    AutomationPeer? FindAutomationPeer(UIElement element);

    IReadOnlyList<AutomationPeer> GetAutomationPeersSnapshot();

    IReadOnlyList<AutomationEventRecord> GetAutomationEventsSnapshot();

    void ClearAutomationEvents();

    Task ExecuteOnUiThreadAsync(Action action, CancellationToken cancellationToken = default);

    Task<T> QueryOnUiThreadAsync<T>(Func<T> query, CancellationToken cancellationToken = default);
}
