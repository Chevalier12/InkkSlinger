namespace InkkSlinger;

public static class FocusManager
{
    private static UIElement? _focusedElement;
    private static UIElement? _capturedPointerElement;

    public static UIElement? GetFocusedElement()
    {
        return _focusedElement;
    }

    public static void SetFocus(UIElement? element)
    {
        _focusedElement = element;
    }

    public static void ClearFocus()
    {
        _focusedElement = null;
    }

    public static UIElement? GetCapturedPointerElement()
    {
        return _capturedPointerElement;
    }

    public static void CapturePointer(UIElement? element)
    {
        _capturedPointerElement = element;
    }

    public static void ReleasePointer(UIElement? element)
    {
        if (ReferenceEquals(_capturedPointerElement, element))
        {
            _capturedPointerElement = null;
        }
    }

    public static void ClearPointerCapture()
    {
        _capturedPointerElement = null;
    }
}
