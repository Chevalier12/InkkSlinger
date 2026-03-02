using System;

namespace InkkSlinger;

internal readonly struct CalendarDateRange
{
    public CalendarDateRange(DateTime start, DateTime end)
    {
        var normalizedStart = start.Date;
        var normalizedEnd = end.Date;
        if (normalizedEnd < normalizedStart)
        {
            (normalizedStart, normalizedEnd) = (normalizedEnd, normalizedStart);
        }

        Start = normalizedStart;
        End = normalizedEnd;
    }

    public DateTime Start { get; }

    public DateTime End { get; }

    public bool Contains(DateTime date)
    {
        var normalized = date.Date;
        return normalized >= Start && normalized <= End;
    }
}
