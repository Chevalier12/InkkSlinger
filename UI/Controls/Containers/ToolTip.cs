using Microsoft.Xna.Framework;

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
        BorderThickness = 1f;
        Padding = new Thickness(6f, 4f, 6f, 4f);
        Background = new Color(24, 30, 42);
        BorderBrush = new Color(98, 152, 205);
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
