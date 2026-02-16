using System;

namespace InkkSlinger;

public class Timeline
{
    public TimeSpan BeginTime { get; set; } = TimeSpan.Zero;

    public Duration Duration { get; set; } = Duration.Automatic;

    public RepeatBehavior RepeatBehavior { get; set; } = new(1d);

    public bool AutoReverse { get; set; }

    public float SpeedRatio { get; set; } = 1f;

    public FillBehavior FillBehavior { get; set; } = FillBehavior.HoldEnd;

    internal virtual TimeSpan ResolveNaturalDuration()
    {
        if (Duration.HasTimeSpan)
        {
            return Duration.TimeSpan!.Value;
        }

        return TimeSpan.Zero;
    }
}
