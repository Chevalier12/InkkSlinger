namespace InkkSlinger;

public class ToolTip : Popup
{
    public ToolTip()
    {
        CanDragMove = false;
        CanClose = false;
        DismissOnOutsideClick = true;
        PlacementMode = PopupPlacementMode.Bottom;
        TitleBarHeight = 0f;
        Title = string.Empty;
    }

    public void ShowFor(Panel host, UIElement target, float horizontalOffset = 0f, float verticalOffset = 6f)
    {
        PlacementTarget = target;
        HorizontalOffset = horizontalOffset;
        VerticalOffset = verticalOffset;
        Show(host);
    }
}
