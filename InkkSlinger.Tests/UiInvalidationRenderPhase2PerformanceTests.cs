using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Xunit;
using Xunit.Abstractions;

namespace InkkSlinger.Tests;

public class UiInvalidationRenderPhase2PerformanceTests
{
    private readonly ITestOutputHelper _output;

    public UiInvalidationRenderPhase2PerformanceTests(ITestOutputHelper output)
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
    public void SparseDirtyRegions_MaintainHighDrawSkipRatio()
    {
        const int frameCount = 60_000;
        const int dirtyEveryNFrames = 60;
        var root = new UiRoot(new Panel());

        try
        {
            Assert.True(root.ExecuteDrawPassForTesting()); // first frame

            var watch = Stopwatch.StartNew();
            for (var i = 1; i <= frameCount; i++)
            {
                if (i % dirtyEveryNFrames == 0)
                {
                    root.MarkVisualDirty(new LayoutRect(i % 300, i % 200, 20f, 20f));
                }

                root.ExecuteDrawPassForTesting();
            }

            watch.Stop();

            var executed = root.DrawExecutedFrameCount;
            var skipped = root.DrawSkippedFrameCount;
            var skipRatio = skipped / Math.Max(1d, executed + skipped);
            var throughput = frameCount / Math.Max(0.000001d, watch.Elapsed.TotalSeconds);

            _output.WriteLine(
                $"SparseDirtyRegions | Frames={frameCount} ElapsedMs={watch.Elapsed.TotalMilliseconds:0.###} " +
                $"Throughput={throughput:0.###}fpsEq Executed={executed} Skipped={skipped} SkipRatio={skipRatio:P2}");

            Assert.True(skipRatio > 0.90d);
        }
        finally
        {
            root.Shutdown();
        }
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void StableViewport_MaintainsHighLayoutSkipRatio()
    {
        const int updateCount = 20_000;
        var root = new UiRoot(new Panel());

        try
        {
            var size = new Vector2(1280f, 720f);
            var watch = Stopwatch.StartNew();
            for (var i = 0; i < updateCount; i++)
            {
                var total = TimeSpan.FromMilliseconds((i + 1) * 16d);
                root.Update(new GameTime(total, TimeSpan.FromMilliseconds(16d)), size);
            }

            watch.Stop();

            var executed = root.LastUpdateTiming.LayoutExecutedFrames;
            var skipped = root.LastUpdateTiming.LayoutSkippedFrames;
            var skipRatio = root.LastUpdateTiming.LayoutSkipRatio;
            _output.WriteLine(
                $"StableViewportLayoutSkip | Updates={updateCount} ElapsedMs={watch.Elapsed.TotalMilliseconds:0.###} " +
                $"Executed={executed} Skipped={skipped} SkipRatio={skipRatio:P2}");

            Assert.True(skipRatio > 0.95d);
        }
        finally
        {
            root.Shutdown();
        }
    }

}
