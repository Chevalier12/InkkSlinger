using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public sealed class InkkOopsInteractionRecorder : IDisposable
{
    private readonly List<RecordedAction> _actions = new();
    private readonly string _directoryPath;
    private readonly DateTime _startedUtc = DateTime.UtcNow;
    private readonly IInkkOopsArtifactNamingPolicy _namingPolicy;
    private int _pendingWaitFrames;
    private bool _hasLastState;
    private int _lastPointerX;
    private int _lastPointerY;
    private bool _lastLeftPressed;
    private int _lastWheelValue;
    private Microsoft.Xna.Framework.Point _lastClientSize;
    private bool _disposed;

    public InkkOopsInteractionRecorder(string rootPath, Microsoft.Xna.Framework.Point initialClientSize)
        : this(rootPath, initialClientSize, new DefaultInkkOopsArtifactNamingPolicy())
    {
    }

    public InkkOopsInteractionRecorder(
        string rootPath,
        Microsoft.Xna.Framework.Point initialClientSize,
        IInkkOopsArtifactNamingPolicy namingPolicy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        _namingPolicy = namingPolicy ?? throw new ArgumentNullException(nameof(namingPolicy));
        _directoryPath = Path.GetFullPath(Path.Combine(rootPath, _namingPolicy.CreateRecordingDirectoryName(_startedUtc)));
        Directory.CreateDirectory(_directoryPath);
        _lastClientSize = initialClientSize;
        _actions.Add(RecordedAction.ResizeWindow(initialClientSize.X, initialClientSize.Y));
    }

    public string DirectoryPath => _directoryPath;

    public void RecordFrame(Microsoft.Xna.Framework.Point clientSize, MouseState mouseState)
    {
        if (!_hasLastState)
        {
            _lastPointerX = mouseState.X;
            _lastPointerY = mouseState.Y;
            _lastLeftPressed = mouseState.LeftButton == ButtonState.Pressed;
            _lastWheelValue = mouseState.ScrollWheelValue;
            _lastClientSize = clientSize;
            _hasLastState = true;
            return;
        }

        _pendingWaitFrames++;

        if (_lastClientSize.X != clientSize.X || _lastClientSize.Y != clientSize.Y)
        {
            FlushWaitFrames();
            _actions.Add(RecordedAction.ResizeWindow(clientSize.X, clientSize.Y));
            _lastClientSize = clientSize;
        }

        if (_lastPointerX != mouseState.X || _lastPointerY != mouseState.Y)
        {
            FlushWaitFrames();
            _actions.Add(RecordedAction.MovePointer(mouseState.X, mouseState.Y));
            _lastPointerX = mouseState.X;
            _lastPointerY = mouseState.Y;
        }

        var leftPressed = mouseState.LeftButton == ButtonState.Pressed;
        if (_lastLeftPressed != leftPressed)
        {
            FlushWaitFrames();
            _actions.Add(leftPressed
                ? RecordedAction.PointerDown(mouseState.X, mouseState.Y)
                : RecordedAction.PointerUp(mouseState.X, mouseState.Y));
            _lastLeftPressed = leftPressed;
        }

        var wheelDelta = mouseState.ScrollWheelValue - _lastWheelValue;
        if (wheelDelta != 0)
        {
            FlushWaitFrames();
            _actions.Add(RecordedAction.Wheel(wheelDelta));
            _lastWheelValue = mouseState.ScrollWheelValue;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        WriteArtifacts();
    }

    private void FlushWaitFrames()
    {
        if (_pendingWaitFrames <= 0)
        {
            return;
        }

        _actions.Add(RecordedAction.WaitFrames(_pendingWaitFrames));
        _pendingWaitFrames = 0;
    }

    private void WriteArtifacts()
    {
        FlushWaitFrames();

        var completedUtc = DateTime.UtcNow;
        var sessionJson = JsonSerializer.Serialize(
            new
            {
                startedUtc = _startedUtc,
                completedUtc,
                actionCount = _actions.Count,
                artifactDirectory = _directoryPath,
                actions = _actions
            },
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(_directoryPath, _namingPolicy.GetRecordingJsonFileName()), sessionJson, Encoding.UTF8);
        File.WriteAllText(Path.Combine(_directoryPath, _namingPolicy.GetRecordingInkkrFileName()), sessionJson, Encoding.UTF8);

    }

    public enum RecordedActionKind
    {
        WaitFrames,
        ResizeWindow,
        MovePointer,
        PointerDown,
        PointerUp,
        Wheel
    }

    public sealed record RecordedAction(
        RecordedActionKind Kind,
        int? FrameCount = null,
        int? Width = null,
        int? Height = null,
        int? X = null,
        int? Y = null,
        int? WheelDelta = null)
    {
        public static RecordedAction WaitFrames(int frameCount) => new(RecordedActionKind.WaitFrames, FrameCount: frameCount);

        public static RecordedAction ResizeWindow(int width, int height) => new(RecordedActionKind.ResizeWindow, Width: width, Height: height);

        public static RecordedAction MovePointer(int x, int y) => new(RecordedActionKind.MovePointer, X: x, Y: y);

        public static RecordedAction PointerDown(int x, int y) => new(RecordedActionKind.PointerDown, X: x, Y: y);

        public static RecordedAction PointerUp(int x, int y) => new(RecordedActionKind.PointerUp, X: x, Y: y);

        public static RecordedAction Wheel(int delta) => new(RecordedActionKind.Wheel, WheelDelta: delta);
    }
}
