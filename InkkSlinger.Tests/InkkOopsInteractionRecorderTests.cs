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
                recorder.RecordFrame(new Point(1280, 820), CreateMouseState(20, 30));
                recorder.RecordFrame(new Point(1280, 820), CreateMouseState(140, 160));
                recorder.RecordFrame(new Point(1280, 820), CreateMouseState(140, 160, left: ButtonState.Pressed));
                recorder.RecordFrame(new Point(1280, 820), CreateMouseState(140, 160, left: ButtonState.Released));
                recorder.RecordFrame(new Point(930, 610), CreateMouseState(140, 160));
                recorder.RecordFrame(new Point(930, 610), CreateMouseState(140, 160, scrollWheel: 120));
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
