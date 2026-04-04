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
        return LoadFromJson(recordingPath, new DefaultInkkOopsArtifactNamingPolicy());
    }

    public static InkkOopsScript LoadFromJson(
        string recordingPath,
        IInkkOopsArtifactNamingPolicy namingPolicy)
    {
        if (string.IsNullOrWhiteSpace(recordingPath))
        {
            throw new ArgumentException("Recording path is required.", nameof(recordingPath));
        }
        
        ArgumentNullException.ThrowIfNull(namingPolicy);

        var fullPath = Path.GetFullPath(recordingPath);
        var json = File.ReadAllText(fullPath);
        var session = JsonSerializer.Deserialize<RecordedSessionDocument>(json, JsonOptions)
                      ?? throw new InvalidOperationException($"Could not deserialize recording '{fullPath}'.");
        var builder = new InkkOopsScriptBuilder(namingPolicy.CreateReplayScriptName(fullPath));

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

        return builder.Build();
    }

    private sealed class RecordedSessionDocument
    {
        public List<InkkOopsInteractionRecorder.RecordedAction> Actions { get; set; } = new();
    }
}
