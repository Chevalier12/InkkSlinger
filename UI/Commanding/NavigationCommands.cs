namespace InkkSlinger;

public static class NavigationCommands
{
    public static readonly RoutedUICommand NextPage = new(text: "Next Page", name: nameof(NextPage), ownerType: typeof(NavigationCommands));
    public static readonly RoutedUICommand PreviousPage = new(text: "Previous Page", name: nameof(PreviousPage), ownerType: typeof(NavigationCommands));
    public static readonly RoutedUICommand FirstPage = new(text: "First Page", name: nameof(FirstPage), ownerType: typeof(NavigationCommands));
    public static readonly RoutedUICommand LastPage = new(text: "Last Page", name: nameof(LastPage), ownerType: typeof(NavigationCommands));
    public static readonly RoutedUICommand GoToPage = new(text: "Go To Page", name: nameof(GoToPage), ownerType: typeof(NavigationCommands));

    public static readonly RoutedUICommand IncreaseZoom = new(text: "Increase Zoom", name: nameof(IncreaseZoom), ownerType: typeof(NavigationCommands));
    public static readonly RoutedUICommand DecreaseZoom = new(text: "Decrease Zoom", name: nameof(DecreaseZoom), ownerType: typeof(NavigationCommands));
    public static readonly RoutedUICommand FitToWidth = new(text: "Fit To Width", name: nameof(FitToWidth), ownerType: typeof(NavigationCommands));
}
