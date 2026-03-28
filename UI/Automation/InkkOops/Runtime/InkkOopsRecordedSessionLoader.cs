using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;

namespace InkkSlinger;

public static class InkkOopsRecordedSessionLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static InkkOopsScript LoadFromJson(string recordingPath)
    {
        if (string.IsNullOrWhiteSpace(recordingPath))
        {
            throw new ArgumentException("Recording path is required.", nameof(recordingPath));
        }

        var fullPath = Path.GetFullPath(recordingPath);
        var json = File.ReadAllText(fullPath);
        var session = JsonSerializer.Deserialize<RecordedSessionDocument>(json, JsonOptions)
                      ?? throw new InvalidOperationException($"Could not deserialize recording '{fullPath}'.");
        var scriptName = Path.GetFileNameWithoutExtension(fullPath);
        var captureName = SanitizeArtifactName(scriptName);
        var builder = new InkkOopsScriptBuilder($"recording-replay-{scriptName}");

        foreach (var action in session.Actions)
        {
            switch (action.Kind)
            {
                case InkkOopsInteractionRecorder.RecordedActionKind.WaitFrames:
                    builder.WaitFrames(action.FrameCount ?? 1);
                    break;
                case InkkOopsInteractionRecorder.RecordedActionKind.ResizeWindow:
                    builder.ResizeWindow(action.Width ?? 1, action.Height ?? 1);
                    break;
                case InkkOopsInteractionRecorder.RecordedActionKind.MovePointer:
                    builder.MovePointer(new Vector2(action.X ?? 0, action.Y ?? 0));
                    break;
                case InkkOopsInteractionRecorder.RecordedActionKind.PointerDown:
                    builder.PointerDown(new Vector2(action.X ?? 0, action.Y ?? 0));
                    break;
                case InkkOopsInteractionRecorder.RecordedActionKind.PointerUp:
                    builder.PointerUp(new Vector2(action.X ?? 0, action.Y ?? 0));
                    break;
                case InkkOopsInteractionRecorder.RecordedActionKind.Wheel:
                    builder.Wheel(action.WheelDelta ?? 0);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported recorded action kind '{action.Kind}'.");
            }
        }

        builder
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable)
            .CaptureFrame($"{captureName}-final")
            .DumpTelemetry($"{captureName}-final");

        return builder.Build();
    }

    private static string SanitizeArtifactName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "recording";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var buffer = new char[value.Length];
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            buffer[i] = Array.IndexOf(invalidChars, ch) >= 0 ? '-' : ch;
        }

        return new string(buffer);
    }

    private sealed class RecordedSessionDocument
    {
        public List<InkkOopsInteractionRecorder.RecordedAction> Actions { get; set; } = new();
    }
}
