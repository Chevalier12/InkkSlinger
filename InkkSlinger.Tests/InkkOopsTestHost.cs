using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Vector2 = System.Numerics.Vector2;

namespace InkkSlinger.Tests;

internal sealed class InkkOopsTestHost : IInkkOopsHost, IDisposable
{
    private readonly UIElement _visualRoot;
    private readonly List<AutomationEventRecord> _automationEvents = new();
    private Vector2 _pointerPosition;
    private bool _hasPointer;
    private int _width;
    private int _height;

    public InkkOopsTestHost(UIElement visualRoot, int width = 800, int height = 600)
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        _visualRoot = visualRoot;
        _width = width;
        _height = height;
        UiRoot = new UiRoot(visualRoot);
        UiRoot.Automation.AutomationEventRaised += OnAutomationEventRaised;
        ArtifactRoot = Path.Combine(Path.GetTempPath(), "inkkoops-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ArtifactRoot);
        AdvanceFrameAsync(1).GetAwaiter().GetResult();
    }

    public UiRoot UiRoot { get; }

    public string ArtifactRoot { get; }

    public UIElement? GetVisualRootElement()
    {
        return _visualRoot;
    }

    public LayoutRect GetViewportBounds()
    {
        return new LayoutRect(0f, 0f, _width, _height);
    }

    public Task ResizeWindowAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        _width = width;
        _height = height;
        return AdvanceFrameAsync(1, cancellationToken);
    }

    public Task MaximizeWindowAsync(CancellationToken cancellationToken = default)
    {
        return AdvanceFrameAsync(1, cancellationToken);
    }

    public Task AdvanceFrameAsync(int frameCount, CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < frameCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            UiRoot.Update(
                new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
                new Microsoft.Xna.Framework.Graphics.Viewport(0, 0, _width, _height));
        }

        return Task.CompletedTask;
    }

    public async Task WaitForIdleAsync(InkkOopsIdlePolicy policy, CancellationToken cancellationToken = default)
    {
        IdleSnapshot? previous = null;
        for (var i = 0; i < 16; i++)
        {
            var snapshot = CaptureIdleSnapshot(policy);
            if (snapshot.IsSatisfied && previous is not null && snapshot.IsEquivalentTo(previous.Value))
            {
                return;
            }

            previous = snapshot;
            await AdvanceFrameAsync(1, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task MovePointerAsync(Vector2 position, CancellationToken cancellationToken = default)
    {
        var previous = _hasPointer ? _pointerPosition : position;
        _pointerPosition = position;
        _hasPointer = true;
        UiRoot.RunInputDeltaForTests(CreateDelta(previous, position, pointerMoved: true));
        return Task.CompletedTask;
    }

    public Task PressPointerAsync(Vector2 position, CancellationToken cancellationToken = default)
    {
        var previous = _hasPointer ? _pointerPosition : position;
        _pointerPosition = position;
        _hasPointer = true;
        UiRoot.RunInputDeltaForTests(CreateDelta(previous, position, pointerMoved: true, leftPressed: true));
        return Task.CompletedTask;
    }

    public Task ReleasePointerAsync(Vector2 position, CancellationToken cancellationToken = default)
    {
        var previous = _hasPointer ? _pointerPosition : position;
        _pointerPosition = position;
        _hasPointer = true;
        UiRoot.RunInputDeltaForTests(CreateDelta(previous, position, pointerMoved: true, leftReleased: true));
        return Task.CompletedTask;
    }

    public Task WheelAsync(int delta, CancellationToken cancellationToken = default)
    {
        var current = _hasPointer ? _pointerPosition : Vector2.Zero;
        UiRoot.RunInputDeltaForTests(CreateDelta(current, current, wheelDelta: delta));
        return Task.CompletedTask;
    }

    public Task CaptureFrameAsync(string artifactName, CancellationToken cancellationToken = default)
    {
        File.WriteAllText(Path.Combine(ArtifactRoot, artifactName + ".txt"), "capture");
        return Task.CompletedTask;
    }

    public Task WriteTelemetryAsync(string artifactName, CancellationToken cancellationToken = default)
    {
        File.WriteAllText(Path.Combine(ArtifactRoot, artifactName + ".txt"), UiRoot.GetDirtyRegionSummaryForTests());
        return Task.CompletedTask;
    }

    public UIElement? FindElement(string identifier)
    {
        return _visualRoot is FrameworkElement frameworkElement
            ? frameworkElement.FindName(identifier)
            : null;
    }

    public AutomationPeer? FindAutomationPeer(UIElement element)
    {
        return UiRoot.Automation.GetPeer(element);
    }

    public IReadOnlyList<AutomationPeer> GetAutomationPeersSnapshot()
    {
        return UiRoot.Automation.GetTreeSnapshot();
    }

    public IReadOnlyList<AutomationEventRecord> GetAutomationEventsSnapshot()
    {
        return _automationEvents.ToArray();
    }

    public void ClearAutomationEvents()
    {
        _automationEvents.Clear();
    }

    public Task ExecuteOnUiThreadAsync(Action action, CancellationToken cancellationToken = default)
    {
        action();
        return Task.CompletedTask;
    }

    public Task<T> QueryOnUiThreadAsync<T>(Func<T> query, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(query());
    }

    public void Dispose()
    {
        UiRoot.Automation.AutomationEventRaised -= OnAutomationEventRaised;
        UiRoot.Shutdown();
        Dispatcher.ResetForTests();
        try
        {
            Directory.Delete(ArtifactRoot, recursive: true);
        }
        catch
        {
            // ignore cleanup failures in tests
        }
    }

    private void OnAutomationEventRaised(object? sender, AutomationEventArgs args)
    {
        _automationEvents.Add(new AutomationEventRecord(
            args.EventType,
            args.Peer.RuntimeId,
            args.PropertyName,
            args.OldValue,
            args.NewValue,
            args.OldPeer?.RuntimeId,
            args.NewPeer?.RuntimeId));
    }

    private static InputDelta CreateDelta(
        Vector2 previous,
        Vector2 current,
        bool pointerMoved = false,
        bool leftPressed = false,
        bool leftReleased = false,
        int wheelDelta = 0)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, previous),
            Current = new InputSnapshot(default, default, current),
            PressedKeys = Array.Empty<Keys>(),
            ReleasedKeys = Array.Empty<Keys>(),
            TextInput = Array.Empty<char>(),
            PointerMoved = pointerMoved || leftPressed || leftReleased,
            WheelDelta = wheelDelta,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private IdleSnapshot CaptureIdleSnapshot(InkkOopsIdlePolicy policy)
    {
        var baseSatisfied = UiRoot.PendingDeferredOperationCount == 0 &&
                            !UiRoot.HasPendingMeasureInvalidation &&
                            !UiRoot.HasPendingArrangeInvalidation &&
                            !UiRoot.HasPendingRenderInvalidation;
        var inputSatisfied = baseSatisfied && FocusManager.GetCapturedPointerElement() == null;
        var satisfied = policy switch
        {
            InkkOopsIdlePolicy.InputStable => inputSatisfied,
            InkkOopsIdlePolicy.DiagnosticsStable => inputSatisfied,
            _ => baseSatisfied
        };

        return new IdleSnapshot(
            satisfied,
            UiRoot.GetDirtyRegionSummaryForTests(),
            InkkOopsTargetResolver.DescribeElement(UiRoot.GetHoveredElementForDiagnostics()),
            InkkOopsTargetResolver.DescribeElement(FocusManager.GetFocusedElement()));
    }

    private readonly record struct IdleSnapshot(
        bool IsSatisfied,
        string DirtySummary,
        string HoveredElement,
        string FocusedElement)
    {
        public bool IsEquivalentTo(IdleSnapshot other)
        {
            return DirtySummary == other.DirtySummary &&
                   HoveredElement == other.HoveredElement &&
                   FocusedElement == other.FocusedElement;
        }
    }
}
