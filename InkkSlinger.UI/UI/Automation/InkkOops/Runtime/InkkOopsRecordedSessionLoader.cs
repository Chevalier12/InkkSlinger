using System;
using System.Collections.Generic;
using System.Numerics;
using System.IO;
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
        var (fullPath, session) = LoadDocument(recordingPath);
        var builder = new InkkOopsScriptBuilder("recording-playback");

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
                case InkkOopsInteractionRecorder.RecordedActionKind.KeyDown:
                    if (action.Key is not null)
                    {
                        builder.KeyDown(action.Key.Value);
                    }
                    break;
                case InkkOopsInteractionRecorder.RecordedActionKind.KeyUp:
                    if (action.Key is not null)
                    {
                        builder.KeyUp(action.Key.Value);
                    }
                    break;
                case InkkOopsInteractionRecorder.RecordedActionKind.TextInput:
                    if (action.Character is not null)
                    {
                        builder.TextInput(action.Character.Value);
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported recorded action kind '{action.Kind}'.");
            }
        }

        return builder.Build();
    }

    public static InkkOopsRecordedSessionMetadata ReadMetadata(string recordingPath)
    {
        var (fullPath, session) = LoadDocument(recordingPath);
        return new InkkOopsRecordedSessionMetadata(fullPath, session.RecordedProjectPath ?? string.Empty);
    }

    public static string GetRecordingDirectoryPath(string recordingPath)
    {
        var fullPath = ResolveRecordingPath(recordingPath);
        return Path.GetDirectoryName(fullPath)
               ?? throw new InvalidOperationException($"Recording '{fullPath}' does not have a parent directory.");
    }

    private static (string FullPath, RecordedSessionDocument Document) LoadDocument(string recordingPath)
    {
        if (string.IsNullOrWhiteSpace(recordingPath))
        {
            throw new ArgumentException("Recording path is required.", nameof(recordingPath));
        }

        var fullPath = ResolveRecordingPath(recordingPath);
        var json = System.IO.File.ReadAllText(fullPath);
        var session = JsonSerializer.Deserialize<RecordedSessionDocument>(json, JsonOptions)
                      ?? throw new InvalidOperationException($"Could not deserialize recording '{fullPath}'.");
        return (fullPath, session);
    }

    private static string ResolveRecordingPath(string recordingPath)
    {
        var fullPath = System.IO.Path.GetFullPath(recordingPath);
        if (!System.IO.Directory.Exists(fullPath))
        {
            return fullPath;
        }

        var jsonPath = System.IO.Path.Combine(fullPath, "recording.json");
        if (System.IO.File.Exists(jsonPath))
        {
            return jsonPath;
        }

        var inkkrPath = System.IO.Path.Combine(fullPath, "recording.inkkr");
        if (System.IO.File.Exists(inkkrPath))
        {
            return inkkrPath;
        }

        throw new System.IO.FileNotFoundException(
            $"No recording file was found in '{fullPath}'. Expected 'recording.json' or 'recording.inkkr'.",
            fullPath);
    }

    private sealed class RecordedSessionDocument
    {
        public string RecordedProjectPath { get; set; } = string.Empty;

        public List<InkkOopsInteractionRecorder.RecordedAction> Actions { get; set; } = new();
    }
}

public sealed record InkkOopsRecordedSessionMetadata(string RecordingPath, string RecordedProjectPath);
