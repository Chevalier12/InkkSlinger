using System;

namespace InkkSlinger;

public enum FillBehavior
{
    HoldEnd,
    Stop
}

public enum HandoffBehavior
{
    SnapshotAndReplace,
    Compose
}

public enum EasingMode
{
    EaseOut,
    EaseIn,
    EaseInOut
}

public enum ClockState
{
    Stopped,
    Active,
    Filling
}

public enum TimeSeekOrigin
{
    BeginTime,
    Duration
}

public enum KeyTimeType
{
    TimeSpan,
    Uniform,
    Paced
}

public readonly struct KeyTime : IEquatable<KeyTime>
{
    public static readonly KeyTime Uniform = new(KeyTimeType.Uniform, null);
    public static readonly KeyTime Paced = new(KeyTimeType.Paced, null);

    public KeyTime(TimeSpan timeSpan)
        : this(KeyTimeType.TimeSpan, timeSpan)
    {
    }

    private KeyTime(KeyTimeType type, TimeSpan? timeSpan)
    {
        Type = type;
        TimeSpan = timeSpan;
    }

    public KeyTimeType Type { get; }

    public TimeSpan? TimeSpan { get; }

    public bool IsTimeSpan => Type == KeyTimeType.TimeSpan && TimeSpan.HasValue;

    public static implicit operator KeyTime(TimeSpan timeSpan) => new(timeSpan);

    public bool Equals(KeyTime other)
    {
        return Type == other.Type && Nullable.Equals(TimeSpan, other.TimeSpan);
    }

    public override bool Equals(object? obj)
    {
        return obj is KeyTime other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine((int)Type, TimeSpan);
    }
}

public readonly struct Duration : IEquatable<Duration>
{
    public static readonly Duration Automatic = new(null, isAutomatic: true, isForever: false);
    public static readonly Duration Forever = new(null, isAutomatic: false, isForever: true);

    public Duration(TimeSpan timeSpan)
        : this(timeSpan, isAutomatic: false, isForever: false)
    {
    }

    private Duration(TimeSpan? timeSpan, bool isAutomatic, bool isForever)
    {
        TimeSpan = timeSpan;
        IsAutomatic = isAutomatic;
        IsForever = isForever;
    }

    public TimeSpan? TimeSpan { get; }

    public bool HasTimeSpan => TimeSpan.HasValue;

    public bool IsAutomatic { get; }

    public bool IsForever { get; }

    public static implicit operator Duration(TimeSpan timeSpan) => new(timeSpan);

    public bool Equals(Duration other)
    {
        return Nullable.Equals(TimeSpan, other.TimeSpan) &&
               IsAutomatic == other.IsAutomatic &&
               IsForever == other.IsForever;
    }

    public override bool Equals(object? obj)
    {
        return obj is Duration other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(TimeSpan, IsAutomatic, IsForever);
    }
}

public readonly struct RepeatBehavior : IEquatable<RepeatBehavior>
{
    public static readonly RepeatBehavior Forever = new(null, null, true);

    public RepeatBehavior(double count)
        : this(count, null, false)
    {
    }

    public RepeatBehavior(TimeSpan duration)
        : this(null, duration, false)
    {
    }

    private RepeatBehavior(double? count, TimeSpan? duration, bool isForever)
    {
        Count = count;
        Duration = duration;
        IsForever = isForever;
    }

    public double? Count { get; }

    public TimeSpan? Duration { get; }

    public bool HasCount => Count.HasValue;

    public bool HasDuration => Duration.HasValue;

    public bool IsForever { get; }

    public bool Equals(RepeatBehavior other)
    {
        return Nullable.Equals(Count, other.Count) &&
               Nullable.Equals(Duration, other.Duration) &&
               IsForever == other.IsForever;
    }

    public override bool Equals(object? obj)
    {
        return obj is RepeatBehavior other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Count, Duration, IsForever);
    }
}

internal readonly struct AnimationTarget
{
    public AnimationTarget(DependencyObject target, DependencyProperty property)
    {
        Target = target;
        Property = property;
    }

    public DependencyObject Target { get; }

    public DependencyProperty Property { get; }
}

internal readonly struct StoryboardControlKey : IEquatable<StoryboardControlKey>
{
    public StoryboardControlKey(FrameworkElement scope, string name)
    {
        Scope = scope;
        Name = name;
    }

    public FrameworkElement Scope { get; }

    public string Name { get; }

    public bool Equals(StoryboardControlKey other)
    {
        return ReferenceEquals(Scope, other.Scope) &&
               string.Equals(Name, other.Name, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is StoryboardControlKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Scope, Name);
    }
}
