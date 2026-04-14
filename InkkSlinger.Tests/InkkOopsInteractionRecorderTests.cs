using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class InkkOopsInteractionRecorderTests
{
    [Fact]
    public void Recorder_WritesStructuredRecordingOnly()
    {
        var root = Path.Combine(Path.GetTempPath(), $"inkkoops-recorder-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            string directoryPath;
            using (var recorder = new InkkOopsInteractionRecorder(root, new Point(1280, 820)))
            {
                recorder.RecordFrame(new Point(1280, 820), CreateMouseState(20, 30), default);
                recorder.RecordFrame(new Point(1280, 820), CreateMouseState(140, 160), default);
                recorder.RecordFrame(new Point(1280, 820), CreateMouseState(140, 160, left: ButtonState.Pressed), default);
                recorder.RecordFrame(new Point(1280, 820), CreateMouseState(140, 160, left: ButtonState.Released), default);
                recorder.RecordFrame(new Point(930, 610), CreateMouseState(140, 160), default);
                recorder.RecordFrame(new Point(930, 610), CreateMouseState(140, 160, scrollWheel: 120), default);
                directoryPath = recorder.DirectoryPath;
            }

            var jsonPath = Path.Combine(directoryPath, "recording.json");
            var inkkrPath = Path.Combine(directoryPath, "recording.inkkr");
            Assert.True(File.Exists(jsonPath));
            Assert.True(File.Exists(inkkrPath));
            Assert.False(File.Exists(Path.Combine(directoryPath, "recorded-script.txt")));

            var json = File.ReadAllText(jsonPath);
            var inkkr = File.ReadAllText(inkkrPath);

            Assert.Equal(json, inkkr);

            Assert.Contains("\"Kind\": 1", json); // ResizeWindow
            Assert.Contains("\"Kind\": 2", json); // MovePointer
            Assert.Contains("\"Kind\": 3", json); // PointerDown
            Assert.Contains("\"Kind\": 4", json); // PointerUp
            Assert.Contains("\"Kind\": 5", json); // Wheel
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Recorder_PersistsSnapshotBeforeDispose()
    {
        var root = Path.Combine(Path.GetTempPath(), $"inkkoops-recorder-live-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            using var recorder = new InkkOopsInteractionRecorder(root, new Point(1280, 820));

            var jsonPath = Path.Combine(recorder.DirectoryPath, "recording.json");
            Assert.True(File.Exists(jsonPath));

            recorder.RecordFrame(new Point(1280, 820), CreateMouseState(20, 30), default);
            recorder.RecordFrame(new Point(1280, 820), CreateMouseState(140, 160), default);

            var json = File.ReadAllText(jsonPath);
            Assert.Contains("\"actionCount\": 3", json);
            Assert.Contains("\"Kind\": 2", json); // MovePointer
            Assert.Contains("\"Kind\": 0", json); // WaitFrames snapshot between frames
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Recorder_WritesRecordedProjectPathMetadata()
    {
        var root = Path.Combine(Path.GetTempPath(), $"inkkoops-recorder-project-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            const string projectPath = @"C:\repo\InkkSlinger.Designer\InkkSlinger.Designer.csproj";
            using var recorder = new InkkOopsInteractionRecorder(root, new Point(1280, 820), projectPath, new DefaultInkkOopsArtifactNamingPolicy());

            var metadata = InkkOopsRecordedSessionLoader.ReadMetadata(recorder.DirectoryPath);

            Assert.Equal(projectPath, metadata.RecordedProjectPath);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Recorder_DoesNotCrashWhenSnapshotOverwriteFails()
    {
        var root = Path.Combine(Path.GetTempPath(), $"inkkoops-recorder-lock-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            using var recorder = new InkkOopsInteractionRecorder(root, new Point(1280, 820));
            var jsonPath = Path.Combine(recorder.DirectoryPath, "recording.json");
            var inkkrPath = Path.Combine(recorder.DirectoryPath, "recording.inkkr");
            using var jsonLock = new FileStream(jsonPath, FileMode.Open, FileAccess.Read, FileShare.None);
            using var inkkrLock = new FileStream(inkkrPath, FileMode.Open, FileAccess.Read, FileShare.None);

            var exception = Record.Exception(() =>
            {
                recorder.RecordFrame(new Point(1280, 820), CreateMouseState(20, 30), default);
                recorder.RecordFrame(new Point(1280, 820), CreateMouseState(140, 160), default);
            });

            Assert.Null(exception);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void RecordedSessionLoader_DeserializesLowercaseActionsPayload()
    {
        var root = Path.Combine(Path.GetTempPath(), $"inkkoops-loader-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var jsonPath = Path.Combine(root, "recording.json");
            File.WriteAllText(
                jsonPath,
                """
                {
                  "startedUtc": "2026-03-27T23:25:01.7260274Z",
                  "completedUtc": "2026-03-27T23:25:14.6960197Z",
                  "actionCount": 3,
                  "artifactDirectory": "C:\\temp\\recording",
                  "actions": [
                    { "Kind": 1, "Width": 1280, "Height": 820 },
                    { "Kind": 0, "FrameCount": 51 },
                    { "Kind": 2, "X": 685, "Y": 631 }
                  ]
                }
                """);

            var script = InkkOopsRecordedSessionLoader.LoadFromJson(jsonPath);
            var commandDescriptions = script.Commands.Select(static command => command.Describe()).ToArray();

            Assert.Equal(3, commandDescriptions.Length);
            Assert.Equal("ResizeWindow(1280, 820)", commandDescriptions[0]);
            Assert.Equal("WaitFrames(51)", commandDescriptions[1]);
            Assert.Equal("MovePointer(685, 631, travelFrames: 0, easing: Linear)", commandDescriptions[2]);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void RecordedSessionLoader_AcceptsRecordedSessionDirectoryPath()
    {
        var root = Path.Combine(Path.GetTempPath(), $"inkkoops-loader-dir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            File.WriteAllText(
                Path.Combine(root, "recording.json"),
                """
                {
                  "actions": [
                    { "kind": 0, "frameCount": 2 }
                  ]
                }
                """);

            var script = InkkOopsRecordedSessionLoader.LoadFromJson(root);
            var commandDescriptions = script.Commands.Select(static command => command.Describe()).ToArray();

            Assert.Single(commandDescriptions);
            Assert.Equal("WaitFrames(2)", commandDescriptions[0]);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void RecordedSessionLoader_ReadMetadata_ReturnsRecordedProjectPath()
    {
        var root = Path.Combine(Path.GetTempPath(), $"inkkoops-loader-metadata-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            const string projectPath = @"C:\repo\InkkSlinger.DemoApp\InkkSlinger.DemoApp.csproj";
            File.WriteAllText(
                Path.Combine(root, "recording.json"),
                $$"""
                {
                  "recordedProjectPath": "{{projectPath.Replace("\\", "\\\\")}}",
                  "actions": [
                    { "kind": 0, "frameCount": 2 }
                  ]
                }
                """);

            var metadata = InkkOopsRecordedSessionLoader.ReadMetadata(root);

            Assert.Equal(projectPath, metadata.RecordedProjectPath);
            Assert.EndsWith("recording.json", metadata.RecordingPath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Recorder_Writes_Key_And_TextInput_Actions()
    {
        var root = Path.Combine(Path.GetTempPath(), $"inkkoops-recorder-keys-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            string directoryPath;
            using (var recorder = new InkkOopsInteractionRecorder(root, new Point(1280, 820)))
            {
                recorder.RecordFrame(new Point(1280, 820), CreateMouseState(20, 30), default);
                recorder.RecordFrame(new Point(1280, 820), CreateMouseState(20, 30), CreateKeyboardState(Keys.LeftControl));
                recorder.RecordFrame(new Point(1280, 820), CreateMouseState(20, 30), CreateKeyboardState(Keys.LeftControl, Keys.S));
                recorder.RecordTextInput('s');
                recorder.RecordFrame(new Point(1280, 820), CreateMouseState(20, 30), CreateKeyboardState(Keys.LeftControl));
                recorder.RecordFrame(new Point(1280, 820), CreateMouseState(20, 30), default);
                directoryPath = recorder.DirectoryPath;
            }

            var json = File.ReadAllText(Path.Combine(directoryPath, "recording.json"));

            Assert.Contains("\"Kind\": 6", json);
            Assert.Contains("\"Kind\": 7", json);
            Assert.Contains("\"Kind\": 8", json);
            Assert.Contains("\"Key\": 162", json);
            Assert.Contains("\"Key\": 83", json);
            Assert.Contains("\"Character\": \"s\"", json);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void RecordedSessionLoader_Loads_Key_And_TextInput_Actions()
    {
        var root = Path.Combine(Path.GetTempPath(), $"inkkoops-loader-keys-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var jsonPath = Path.Combine(root, "recording.json");
            File.WriteAllText(
                jsonPath,
                """
                {
                  "actions": [
                    { "kind": 6, "key": 162 },
                    { "kind": 6, "key": 83 },
                    { "kind": 8, "character": "s" },
                    { "kind": 7, "key": 83 },
                    { "kind": 7, "key": 162 }
                  ]
                }
                """);

            var script = InkkOopsRecordedSessionLoader.LoadFromJson(jsonPath);
            var commandDescriptions = script.Commands.Select(static command => command.Describe()).ToArray();

            Assert.Equal(["KeyDown(LeftControl)", "KeyDown(S)", "TextInput(s)", "KeyUp(S)", "KeyUp(LeftControl)"], commandDescriptions);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static KeyboardState CreateKeyboardState(params Keys[] keys)
    {
        return keys.Length == 0 ? default : new KeyboardState(keys);
    }

    private static MouseState CreateMouseState(
        int x,
        int y,
        int scrollWheel = 0,
        ButtonState left = ButtonState.Released)
    {
        return new MouseState(
            x,
            y,
            scrollWheel,
            left,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released);
    }
}
