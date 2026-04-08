namespace InkkSlinger;

public static class InkkOopsExitCodes
{
    public const int Success = 0;
    public const int Failed = 1;
    public const int Busy = 2;
    public const int NotFound = 3;

    public static int FromStatus(InkkOopsRunStatus status)
    {
        return status switch
        {
            InkkOopsRunStatus.Completed => Success,
            InkkOopsRunStatus.Busy => Busy,
            InkkOopsRunStatus.NotFound => NotFound,
            _ => Failed
        };
    }

    public static int FromStatus(string? status)
    {
        return System.Enum.TryParse<InkkOopsRunStatus>(status, ignoreCase: true, out var parsed)
            ? FromStatus(parsed)
            : Failed;
    }
}
