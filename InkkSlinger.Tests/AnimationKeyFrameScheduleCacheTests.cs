using System;
using System.Collections.Generic;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class AnimationKeyFrameScheduleCacheTests
{
    [Fact]
    public void ScheduleCache_ReusesSchedule_WhenKeyFramesAreUnchanged()
    {
        var frames = new List<DoubleKeyFrame>
        {
            new LinearDoubleKeyFrame(10f, TimeSpan.Zero) { KeyTime = KeyTime.Uniform },
            new LinearDoubleKeyFrame(20f, TimeSpan.Zero) { KeyTime = KeyTime.Uniform }
        };
        var cache = new KeyFrameTiming.ScheduleCache<DoubleKeyFrame>();

        var first = cache.GetOrResolve(
            frames,
            static frame => frame.KeyTime,
            static frame => frame.Value,
            0f,
            TimeSpan.FromSeconds(1),
            static (from, to) => MathF.Abs(ToSingle(to) - ToSingle(from)));

        var second = cache.GetOrResolve(
            frames,
            static frame => frame.KeyTime,
            static frame => frame.Value,
            0f,
            TimeSpan.FromSeconds(1),
            static (from, to) => MathF.Abs(ToSingle(to) - ToSingle(from)));

        Assert.Same(first, second);
    }

    [Fact]
    public void ScheduleCache_RebuildsSchedule_WhenKeyTimeChanges()
    {
        var frames = new List<DoubleKeyFrame>
        {
            new LinearDoubleKeyFrame(10f, TimeSpan.Zero) { KeyTime = KeyTime.Uniform },
            new LinearDoubleKeyFrame(20f, TimeSpan.Zero) { KeyTime = KeyTime.Uniform }
        };
        var cache = new KeyFrameTiming.ScheduleCache<DoubleKeyFrame>();

        var first = cache.GetOrResolve(
            frames,
            static frame => frame.KeyTime,
            static frame => frame.Value,
            0f,
            TimeSpan.FromSeconds(1),
            distanceCalculator: null);

        frames[0].KeyTime = TimeSpan.FromMilliseconds(250);

        var second = cache.GetOrResolve(
            frames,
            static frame => frame.KeyTime,
            static frame => frame.Value,
            0f,
            TimeSpan.FromSeconds(1),
            distanceCalculator: null);

        Assert.NotSame(first, second);
        Assert.Equal(TimeSpan.FromMilliseconds(250), second[0].Time);
    }

    [Fact]
    public void DoubleAnimationUsingKeyFrames_UsesMutatedKeyTimeAfterCachedSample()
    {
        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromSeconds(1)
        };
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(10f, TimeSpan.Zero) { KeyTime = KeyTime.Uniform });
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(20f, TimeSpan.Zero) { KeyTime = KeyTime.Uniform });

        var beforeMutation = animation.GetCurrentValue(0f, null, 0.25f);

        animation.KeyFrames[0].KeyTime = TimeSpan.FromMilliseconds(250);

        var afterMutation = animation.GetCurrentValue(0f, null, 0.25f);

        Assert.Equal(5f, Assert.IsType<float>(beforeMutation), precision: 3);
        Assert.Equal(10f, Assert.IsType<float>(afterMutation), precision: 3);
    }

    private static float ToSingle(object? value)
    {
        return value switch
        {
            float number => number,
            double number => (float)number,
            int number => number,
            _ => 0f
        };
    }
}