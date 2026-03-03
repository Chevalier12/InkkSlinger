using System;

namespace InkkSlinger;

public sealed class NavigationService
{
    private readonly Frame _owner;

    internal NavigationService(Frame owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public bool CanGoBack => _owner.CanGoBack;

    public bool CanGoForward => _owner.CanGoForward;

    public bool Navigate(object content)
    {
        return _owner.Navigate(content);
    }

    public void GoBack()
    {
        _owner.GoBack();
    }

    public void GoForward()
    {
        _owner.GoForward();
    }
}
