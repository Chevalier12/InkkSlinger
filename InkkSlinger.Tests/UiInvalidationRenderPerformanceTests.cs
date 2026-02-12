using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Xunit;
using Xunit.Abstractions;

namespace InkkSlinger.Tests;

public class UiInvalidationRenderPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public UiInvalidationRenderPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        AnimationManager.Current.ResetForTests();
        InputManager.ResetForTests();
        FocusManager.ResetForTests();
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void IdleDrawLoop_SkipsAlmostAllFrames_AndLogsThroughput()
    {
        const int frameCount = 200_000;
        var root = new UiRoot(new Panel());

        try
        {
            Assert.True(root.ExecuteDrawPassForTesting()); // warm-up/first-frame

            var stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < frameCount; i++)
            {
                root.ExecuteDrawPassForTesting();
            }

            stopwatch.Stop();

            var executed = root.DrawExecutedFrameCount;
            var skipped = root.DrawSkippedFrameCount;
            var skipRatio = (double)skipped / Math.Max(1, executed + skipped);
            var throughput = frameCount / Math.Max(0.000001d, stopwatch.Elapsed.TotalSeconds);

            WriteMetric(
                nameof(IdleDrawLoop_SkipsAlmostAllFrames_AndLogsThroughput),
                frameCount,
                stopwatch.Elapsed.TotalMilliseconds,
                throughput,
                executed,
                skipped,
                skipRatio);

            Assert.Equal(1, executed);
            Assert.Equal(frameCount, skipped);
            Assert.True(skipRatio > 0.99d);
        }
        finally
        {
            root.Shutdown();
        }
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void ForcedDirtyDrawLoop_ExecutesEveryFrame_AndLogsThroughput()
    {
        const int frameCount = 200_000;
        var root = new UiRoot(new Panel());

        try
        {
            var stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < frameCount; i++)
            {
                root.MarkVisualDirty();
                root.ExecuteDrawPassForTesting();
            }

            stopwatch.Stop();

            var executed = root.DrawExecutedFrameCount;
            var skipped = root.DrawSkippedFrameCount;
            var skipRatio = (double)skipped / Math.Max(1, executed + skipped);
            var throughput = frameCount / Math.Max(0.000001d, stopwatch.Elapsed.TotalSeconds);

            WriteMetric(
                nameof(ForcedDirtyDrawLoop_ExecutesEveryFrame_AndLogsThroughput),
                frameCount,
                stopwatch.Elapsed.TotalMilliseconds,
                throughput,
                executed,
                skipped,
                skipRatio);

            Assert.Equal(frameCount, executed);
            Assert.Equal(0, skipped);
            Assert.Equal(0d, skipRatio);
        }
        finally
        {
            root.Shutdown();
        }
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void ActiveAnimationLoop_ForcesDraws_AndLogsThroughput()
    {
        const int frameCount = 10_000;
        const double stepMs = 16.0d;

        var panel = new Panel();
        var root = new UiRoot(panel);

        try
        {
            var storyboard = new Storyboard();
            storyboard.Children.Add(new DoubleAnimation
            {
                TargetProperty = nameof(UIElement.Opacity),
                From = 1f,
                To = 0.3f,
                Duration = new Duration(TimeSpan.FromMinutes(30))
            });

            storyboard.Begin(panel, isControllable: true);

            var stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < frameCount; i++)
            {
                var total = TimeSpan.FromMilliseconds((i + 1) * stepMs);
                var elapsed = TimeSpan.FromMilliseconds(stepMs);
                AnimationManager.Current.Update(new GameTime(total, elapsed));
                root.ExecuteDrawPassForTesting();
            }

            stopwatch.Stop();

            var executed = root.DrawExecutedFrameCount;
            var skipped = root.DrawSkippedFrameCount;
            var skipRatio = (double)skipped / Math.Max(1, executed + skipped);
            var throughput = frameCount / Math.Max(0.000001d, stopwatch.Elapsed.TotalSeconds);

            WriteMetric(
                nameof(ActiveAnimationLoop_ForcesDraws_AndLogsThroughput),
                frameCount,
                stopwatch.Elapsed.TotalMilliseconds,
                throughput,
                executed,
                skipped,
                skipRatio);

            Assert.Equal(frameCount, executed);
            Assert.Equal(0, skipped);
            Assert.Equal(0d, skipRatio);
        }
        finally
        {
            root.Shutdown();
        }
    }

    private void WriteMetric(
        string testName,
        int frames,
        double elapsedMs,
        double throughputFpsEquivalent,
        int executed,
        int skipped,
        double skipRatio)
    {
        var line =
            $"{testName} | Frames={frames} | ElapsedMs={elapsedMs:0.###} | ThroughputFramesPerSec={throughputFpsEquivalent:0.###} | " +
            $"Executed={executed} | Skipped={skipped} | SkipRatio={skipRatio:P2}";

        _output.WriteLine(line);
        Console.WriteLine(line);
    }
}
