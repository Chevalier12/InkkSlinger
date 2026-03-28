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
    private int _pendingWaitFrames;
    private bool _hasLastState;
    private int _lastPointerX;
    private int _lastPointerY;
    private bool _lastLeftPressed;
    private int _lastWheelValue;
    private Microsoft.Xna.Framework.Point _lastClientSize;
    private bool _disposed;

    public InkkOopsInteractionRecorder(string rootPath, Microsoft.Xna.Framework.Point initialClientSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        var timestamp = _startedUtc.ToString("yyyyMMdd-HHmmssfff");
        _directoryPath = Path.GetFullPath(Path.Combine(rootPath, $"{timestamp}-recorded-session"));
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
        File.WriteAllText(Path.Combine(_directoryPath, "recording.json"), sessionJson, Encoding.UTF8);

        var builder = new StringBuilder();
        builder.AppendLine("using System.Numerics;");
        builder.AppendLine();
        builder.AppendLine("return new InkkOopsScriptBuilder(\"recorded-session\")");
        foreach (var action in _actions)
        {
            builder.Append("    .");
            switch (action.Kind)
            {
                case RecordedActionKind.WaitFrames:
                    builder.Append("WaitFrames(").Append(action.FrameCount).Append(')');
                    break;
                case RecordedActionKind.ResizeWindow:
                    builder.Append("ResizeWindow(").Append(action.Width).Append(", ").Append(action.Height).Append(')');
                    break;
                case RecordedActionKind.MovePointer:
                    builder.Append("MovePointer(new Vector2(").Append(action.X!.Value).Append("f, ").Append(action.Y!.Value).Append("f))");
                    break;
                case RecordedActionKind.PointerDown:
                    builder.Append("PointerDown(new Vector2(").Append(action.X!.Value).Append("f, ").Append(action.Y!.Value).Append("f))");
                    break;
                case RecordedActionKind.PointerUp:
                    builder.Append("PointerUp(new Vector2(").Append(action.X!.Value).Append("f, ").Append(action.Y!.Value).Append("f))");
                    break;
                case RecordedActionKind.Wheel:
                    builder.Append("Wheel(").Append(action.WheelDelta).Append(')');
                    break;
            }

            builder.AppendLine();
        }

        builder.AppendLine("    .Build();");
        File.WriteAllText(Path.Combine(_directoryPath, "recorded-script.txt"), builder.ToString(), Encoding.UTF8);
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
