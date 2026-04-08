namespace InkkSlinger;

internal static class StyleValueCloneUtility
{
    public static object? CloneForAssignment(object? value)
    {
        return value switch
        {
            null => null,
            Freezable freezable => freezable.IsFrozen ? freezable : freezable.Clone(),
            _ => value
        };
    }
}
