namespace InkkSlinger;

public interface IScrollInfo
{
    bool CanHorizontallyScroll { get; set; }

    bool CanVerticallyScroll { get; set; }

    float ExtentWidth { get; }

    float ExtentHeight { get; }

    float ViewportWidth { get; }

    float ViewportHeight { get; }

    float HorizontalOffset { get; }

    float VerticalOffset { get; }

    ScrollViewer? ScrollOwner { get; set; }

    void LineUp();

    void LineDown();

    void LineLeft();

    void LineRight();

    void PageUp();

    void PageDown();

    void PageLeft();

    void PageRight();

    void MouseWheelUp();

    void MouseWheelDown();

    void MouseWheelLeft();

    void MouseWheelRight();

    void SetHorizontalOffset(float offset);

    void SetVerticalOffset(float offset);

    LayoutRect MakeVisible(UIElement visual, LayoutRect rectangle);
}
