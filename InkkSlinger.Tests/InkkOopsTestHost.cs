using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Vector2 = System.Numerics.Vector2;

namespace InkkSlinger.Tests;

internal sealed class InkkOopsTestHost : IInkkOopsHost, IDisposable
{
    private const float PointerMoveStepDistance = 24f;

    private readonly UIElement _visualRoot;
    private readonly List<AutomationEventRecord> _automationEvents = new();
    private readonly string _displayedFps;
    private readonly List<Vector2> _pointerTrace = new();
    private readonly HashSet<Keys> _automationHeldKeys = new();
    private Vector2 _pointerPosition;
    private bool _hasPointer;
    private int _width;
    private int _height;

    public InkkOopsTestHost(
        UIElement visualRoot,
        int width = 800,
        int height = 600,
        string displayedFps = "60.0")
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        _visualRoot = visualRoot;
        _displayedFps = displayedFps;
        _width = width;
        _height = height;
        UiRoot = new UiRoot(visualRoot);
        UiRoot.Automation.AutomationEventRaised += OnAutomationEventRaised;
        ArtifactRoot = Path.Combine(Path.GetTempPath(), "inkkoops-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ArtifactRoot);
        AdvanceFrameAsync(1).GetAwaiter().GetResult();
    }

    public UiRoot UiRoot { get; }

    public string ArtifactRoot { get; private set; }

    public int AdvancedFrameCount { get; private set; }

    public IReadOnlyList<Vector2> PointerTrace => _pointerTrace;

    public void SetArtifactRoot(string artifactRoot)
    {
        ArtifactRoot = artifactRoot;
        Directory.CreateDirectory(ArtifactRoot);
    }

    public UIElement? GetVisualRootElement()
    {
        return _visualRoot;
    }

    public LayoutRect GetViewportBounds()
    {
        return new LayoutRect(0f, 0f, _width, _height);
    }

    public string GetDisplayedFps()
    {
        return _displayedFps;
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
            AdvancedFrameCount++;
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
        return MovePointerAsync(position, InkkOopsPointerMotion.Default, cancellationToken);
    }

    public Task MovePointerAsync(Vector2 position, InkkOopsPointerMotion motion, CancellationToken cancellationToken = default)
    {
        return MovePointerSmoothAsync(position, motion, cancellationToken);
    }

    public Task PressPointerAsync(Vector2 position, MouseButton button = MouseButton.Left, CancellationToken cancellationToken = default)
    {
        ApplyPointerPress(position, button);
        return Task.CompletedTask;
    }

    public Task ReleasePointerAsync(Vector2 position, MouseButton button = MouseButton.Left, CancellationToken cancellationToken = default)
    {
        ApplyPointerRelease(position, button);
        return Task.CompletedTask;
    }

    public Task WheelAsync(int delta, CancellationToken cancellationToken = default)
    {
        var current = _hasPointer ? _pointerPosition : Vector2.Zero;
        UiRoot.RunInputDeltaForTests(CreateDelta(current, current, wheelDelta: delta));
        return Task.CompletedTask;
    }

    public Task KeyDownAsync(Keys key, CancellationToken cancellationToken = default)
    {
        var pointer = _hasPointer ? _pointerPosition : Vector2.Zero;
        var previousKeyboard = CreateKeyboardState(_automationHeldKeys);
        _automationHeldKeys.Add(key);
        var currentKeyboard = CreateKeyboardState(_automationHeldKeys);
        UiRoot.RunInputDeltaForTests(CreateDelta(
            pointer,
            pointer,
            previousKeyboard: previousKeyboard,
            currentKeyboard: currentKeyboard,
            pressedKeys: [key]));
        return Task.CompletedTask;
    }

    public Task KeyUpAsync(Keys key, CancellationToken cancellationToken = default)
    {
        var pointer = _hasPointer ? _pointerPosition : Vector2.Zero;
        var previousKeyboard = CreateKeyboardState(_automationHeldKeys);
        _automationHeldKeys.Remove(key);
        var currentKeyboard = CreateKeyboardState(_automationHeldKeys);
        UiRoot.RunInputDeltaForTests(CreateDelta(
            pointer,
            pointer,
            previousKeyboard: previousKeyboard,
            currentKeyboard: currentKeyboard,
            releasedKeys: [key]));
        return Task.CompletedTask;
    }

    public Task TextInputAsync(char character, CancellationToken cancellationToken = default)
    {
        var pointer = _hasPointer ? _pointerPosition : Vector2.Zero;
        var keyboard = CreateKeyboardState(_automationHeldKeys);
        UiRoot.RunInputDeltaForTests(CreateDelta(
            pointer,
            pointer,
            previousKeyboard: keyboard,
            currentKeyboard: keyboard,
            textInput: [character]));
        return Task.CompletedTask;
    }

    public Task CaptureFrameAsync(string artifactName, CancellationToken cancellationToken = default)
    {
        File.WriteAllText(Path.Combine(ArtifactRoot, artifactName + ".txt"), "capture");
        return Task.CompletedTask;
    }

    public Task<string> CaptureTelemetryAsync(string artifactName, CancellationToken cancellationToken = default)
    {
        var hovered = UiRoot.GetHoveredElementForDiagnostics();
        var focused = FocusManager.GetFocusedElement();
        var uiRootTelemetry = UiRoot.GetUiRootTelemetrySnapshot();
        var frameworkTelemetry = FrameworkElement.GetAggregateTelemetrySnapshotForDiagnostics();
        var controlTelemetry = Control.GetAggregateTelemetrySnapshotForDiagnostics();
        var buttonTelemetry = Button.GetAggregateTelemetrySnapshotForDiagnostics();
        var scrollViewerTelemetry = ScrollViewer.GetAggregateTelemetrySnapshotForDiagnostics();
        var richTextBoxTelemetry = RichTextBox.GetAggregateTelemetrySnapshotForDiagnostics();
        var editorTelemetry = IDE_Editor.GetAggregateTelemetrySnapshotForDiagnostics();
        var builder = new StringBuilder();
        builder.AppendLine($"artifact_name={artifactName}");
        builder.AppendLine($"hovered={InkkOopsTargetResolver.DescribeElement(hovered)}");
        builder.AppendLine($"focused={InkkOopsTargetResolver.DescribeElement(focused)}");
        builder.AppendLine($"dirty_regions={UiRoot.GetDirtyRegionSummaryForTests()}");
        builder.AppendLine($"uiRootLayoutExecutedFrameCount={UiRoot.LayoutExecutedFrameCount}");
        builder.AppendLine($"uiRootUpdateCallCount={uiRootTelemetry.UpdateCallCount}");
        builder.AppendLine($"uiRootUpdateElapsedMs={uiRootTelemetry.UpdateElapsedMs:0.###}");
        builder.AppendLine($"frameworkMeasureCallCount={frameworkTelemetry.MeasureCallCount}");
        builder.AppendLine($"frameworkMeasureMilliseconds={frameworkTelemetry.MeasureMilliseconds:0.###}");
        builder.AppendLine($"frameworkArrangeCallCount={frameworkTelemetry.ArrangeCallCount}");
        builder.AppendLine($"controlDependencyPropertyChangedCallCount={controlTelemetry.DependencyPropertyChangedCallCount}");
        builder.AppendLine($"buttonRenderCallCount={buttonTelemetry.RenderCallCount}");
        builder.AppendLine($"buttonRenderMilliseconds={buttonTelemetry.RenderMilliseconds:0.###}");
        builder.AppendLine($"buttonTextLayoutCacheHitCount={buttonTelemetry.TextLayoutCacheHitCount}");
        builder.AppendLine($"scrollViewerArrangeOverrideCallCount={scrollViewerTelemetry.ArrangeOverrideCallCount}");
        builder.AppendLine($"richTextBoxHostedRootRenderCallCount={richTextBoxTelemetry.HostedRootRenderCallCount}");
        builder.AppendLine($"ideEditorLayoutUpdatedCallCount={editorTelemetry.EditorLayoutUpdatedCallCount}");
        return Task.FromResult(builder.ToString());
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

    private async Task MovePointerSmoothAsync(Vector2 position, InkkOopsPointerMotion motion, CancellationToken cancellationToken)
    {
        var start = GetCurrentPointerPositionForMotion();
        var steps = CalculatePointerMoveStepCount(start, position, motion);
        for (var i = 1; i <= steps; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var t = ApplyPointerEasing(i / (float)steps, motion.Easing);
            var sample = Vector2.Lerp(start, position, t);
            ApplyPointerMove(sample);
            if (i < steps)
            {
                await AdvanceFrameAsync(1, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private Vector2 GetCurrentPointerPositionForMotion()
    {
        return _hasPointer
            ? _pointerPosition
            : new Vector2(_width / 2f, _height / 2f);
    }

    private void ApplyPointerMove(Vector2 position)
    {
        var previous = _hasPointer ? _pointerPosition : GetCurrentPointerPositionForMotion();
        _pointerPosition = position;
        _hasPointer = true;
        _pointerTrace.Add(position);
        UiRoot.RunInputDeltaForTests(CreateDelta(previous, position, pointerMoved: true));
    }

    private void ApplyPointerPress(Vector2 position, MouseButton button)
    {
        var previous = _hasPointer ? _pointerPosition : GetCurrentPointerPositionForMotion();
        _pointerPosition = position;
        _hasPointer = true;
        UiRoot.RunInputDeltaForTests(CreateDelta(previous, position, pointerMoved: true, buttonPressed: button));
    }

    private void ApplyPointerRelease(Vector2 position, MouseButton button)
    {
        var previous = _hasPointer ? _pointerPosition : GetCurrentPointerPositionForMotion();
        _pointerPosition = position;
        _hasPointer = true;
        UiRoot.RunInputDeltaForTests(CreateDelta(previous, position, pointerMoved: true, buttonReleased: button));
    }

    private static int CalculatePointerMoveStepCount(Vector2 start, Vector2 end, InkkOopsPointerMotion motion)
    {
        if (motion.TravelFrames > 0)
        {
            return motion.TravelFrames + 1;
        }

        var distance = Vector2.Distance(start, end);
        var stepDistance = motion.StepDistance > 0f ? motion.StepDistance : PointerMoveStepDistance;
        return Math.Max(1, (int)MathF.Ceiling(distance / stepDistance));
    }

    private static float ApplyPointerEasing(float t, InkkOopsPointerEasing easing)
    {
        return easing switch
        {
            InkkOopsPointerEasing.EaseInOut => t * t * (3f - (2f * t)),
            _ => t
        };
    }

    private static InputDelta CreateDelta(
        Vector2 previous,
        Vector2 current,
        KeyboardState? previousKeyboard = null,
        KeyboardState? currentKeyboard = null,
        IReadOnlyList<Keys>? pressedKeys = null,
        IReadOnlyList<Keys>? releasedKeys = null,
        IReadOnlyList<char>? textInput = null,
        bool pointerMoved = false,
        MouseButton? buttonPressed = null,
        MouseButton? buttonReleased = null,
        int wheelDelta = 0)
    {
        var leftPressed = buttonPressed == MouseButton.Left;
        var leftReleased = buttonReleased == MouseButton.Left;
        var rightPressed = buttonPressed == MouseButton.Right;
        var rightReleased = buttonReleased == MouseButton.Right;
        var middlePressed = buttonPressed == MouseButton.Middle;
        var middleReleased = buttonReleased == MouseButton.Middle;
        return new InputDelta
        {
            Previous = new InputSnapshot(previousKeyboard ?? default, default, previous),
            Current = new InputSnapshot(currentKeyboard ?? default, default, current),
            PressedKeys = pressedKeys ?? Array.Empty<Keys>(),
            ReleasedKeys = releasedKeys ?? Array.Empty<Keys>(),
            TextInput = textInput ?? Array.Empty<char>(),
            PointerMoved = pointerMoved || buttonPressed != null || buttonReleased != null,
            WheelDelta = wheelDelta,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = rightPressed,
            RightReleased = rightReleased,
            MiddlePressed = middlePressed,
            MiddleReleased = middleReleased
        };
    }

    private static KeyboardState CreateKeyboardState(HashSet<Keys> keys)
    {
        return keys.Count == 0 ? default : new KeyboardState([.. keys]);
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
