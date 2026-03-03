namespace InkkSlinger;

public class Page : ContentControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(Page),
            new FrameworkPropertyMetadata(string.Empty));

    private NavigationService? _navigationService;

    public string Title
    {
        get => GetValue<string>(TitleProperty) ?? string.Empty;
        set => SetValue(TitleProperty, value);
    }

    public NavigationService? NavigationService => _navigationService;

    internal void SetNavigationService(NavigationService? navigationService)
    {
        _navigationService = navigationService;
    }
}
