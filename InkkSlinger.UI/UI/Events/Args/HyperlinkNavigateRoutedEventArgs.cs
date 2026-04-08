namespace InkkSlinger;

public sealed class HyperlinkNavigateRoutedEventArgs : RoutedEventArgs
{
    public HyperlinkNavigateRoutedEventArgs(RoutedEvent routedEvent, string navigateUri)
        : base(routedEvent)
    {
        NavigateUri = navigateUri ?? string.Empty;
    }

    public string NavigateUri { get; }
}
