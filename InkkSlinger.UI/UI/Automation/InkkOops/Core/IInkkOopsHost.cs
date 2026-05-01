using System;
using System.Numerics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public interface IInkkOopsHost
{
    UiRoot UiRoot { get; }

    string ArtifactRoot { get; }

    void SetArtifactRoot(string artifactRoot);

    UIElement? GetVisualRootElement();

    LayoutRect GetViewportBounds();

    string GetDisplayedFps();

    string GetLastPointerMotionTelemetrySummary();

    Task ResizeWindowAsync(int width, int height, CancellationToken cancellationToken = default);

    Task MaximizeWindowAsync(CancellationToken cancellationToken = default);

    Task AdvanceFrameAsync(int frameCount, CancellationToken cancellationToken = default);

    Task WaitForIdleAsync(InkkOopsIdlePolicy policy, CancellationToken cancellationToken = default);

    Task MovePointerAsync(Vector2 position, CancellationToken cancellationToken = default);

    Task MovePointerAsync(Vector2 position, InkkOopsPointerMotion motion, CancellationToken cancellationToken = default);

    Task PressPointerAsync(Vector2 position, MouseButton button = MouseButton.Left, CancellationToken cancellationToken = default);

    Task ReleasePointerAsync(Vector2 position, MouseButton button = MouseButton.Left, CancellationToken cancellationToken = default);

    Task KeyDownAsync(Microsoft.Xna.Framework.Input.Keys key, CancellationToken cancellationToken = default);

    Task KeyUpAsync(Microsoft.Xna.Framework.Input.Keys key, CancellationToken cancellationToken = default);

    Task TextInputAsync(char character, CancellationToken cancellationToken = default);

    Task WheelAsync(int delta, CancellationToken cancellationToken = default);

    Task CaptureFrameAsync(string artifactName, CancellationToken cancellationToken = default);

    Task<InkkOopsFrameRegionSample> SampleCurrentFrameRegionAsync(LayoutRect region, CancellationToken cancellationToken = default);

    Task<string> CaptureTelemetryAsync(string artifactName, CancellationToken cancellationToken = default);

    UIElement? FindElement(string identifier);

    AutomationPeer? FindAutomationPeer(UIElement element);

    IReadOnlyList<AutomationPeer> GetAutomationPeersSnapshot();

    IReadOnlyList<AutomationEventRecord> GetAutomationEventsSnapshot();

    void ClearAutomationEvents();

    Task ExecuteOnUiThreadAsync(Action action, CancellationToken cancellationToken = default);

    Task<T> QueryOnUiThreadAsync<T>(Func<T> query, CancellationToken cancellationToken = default);
}

public readonly record struct InkkOopsFrameRegionSample(
    int X,
    int Y,
    int Width,
    int Height,
    int TotalPixelCount,
    int BrightPixelCount,
    int MaxLuma,
    float AverageLuma);
