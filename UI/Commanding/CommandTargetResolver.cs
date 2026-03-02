namespace InkkSlinger;

public static class CommandTargetResolver
{
    public static UIElement Resolve(UIElement? commandTarget, UIElement fallbackTarget)
    {
        if (commandTarget != null)
        {
            return commandTarget;
        }

        return FocusManager.GetFocusedElement() ?? fallbackTarget;
    }
}
