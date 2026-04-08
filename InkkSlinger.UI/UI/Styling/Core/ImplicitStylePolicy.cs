namespace InkkSlinger;

internal static class ImplicitStylePolicy
{
    public static bool ShouldApply(Style? currentStyle, Style? trackedImplicitStyle)
    {
        if (currentStyle == null)
        {
            return true;
        }

        return trackedImplicitStyle != null && ReferenceEquals(currentStyle, trackedImplicitStyle);
    }

    public static bool ShouldApply(Style? currentStyle, Style? trackedImplicitStyle, Style? fallbackStyle)
    {
        if (ShouldApply(currentStyle, trackedImplicitStyle))
        {
            return true;
        }

        return fallbackStyle != null && ReferenceEquals(currentStyle, fallbackStyle);
    }

    public static bool CanClearImplicit(Style? currentStyle, Style? trackedImplicitStyle)
    {
        return trackedImplicitStyle != null && ReferenceEquals(currentStyle, trackedImplicitStyle);
    }
}
