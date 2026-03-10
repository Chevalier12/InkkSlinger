using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class OverlayDismissAndFocusParityTests
{
    [Fact]
    public void OutsideClick_TopmostPopupDismiss_ConsumesClick_DoesNotInvokeUnderlyingButton()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var buttonClicks = 0;
        var button = new Button { Width = 140f, Height = 40f, Text = "Underlay" };
        button.Click += (_, _) => buttonClicks++;
        host.AddChild(button);
        Canvas.SetLeft(button, 24f);
        Canvas.SetTop(button, 24f);

        var popup = CreatePopup(dismissOnOutsideClick: true, canClose: true);
        popup.Left = 220f;
        popup.Top = 110f;
        popup.Show(host);
        RunLayout(uiRoot);

        Click(uiRoot, new Vector2(button.LayoutSlot.X + 8f, button.LayoutSlot.Y + 8f));

        Assert.False(popup.IsOpen);
        Assert.Equal(0, buttonClicks);
    }

    [Fact]
    public void OutsideClick_WithPopupAndContextMenu_ClosesOnlyTopOverlay_PerClick()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var popup = CreatePopup(dismissOnOutsideClick: true, canClose: true);
        popup.Left = 220f;
        popup.Top = 110f;
        popup.Show(host);

        var contextMenu = CreateSimpleContextMenu();
        contextMenu.StaysOpen = false;
        contextMenu.OpenAt(host, 120f, 80f);
        Panel.SetZIndex(contextMenu, Panel.GetZIndex(popup) + 1);
        RunLayout(uiRoot);

        Click(uiRoot, new Vector2(12f, 12f));

        Assert.False(contextMenu.IsOpen);
        Assert.True(popup.IsOpen);

        Click(uiRoot, new Vector2(14f, 14f));

        Assert.False(popup.IsOpen);
    }

    [Fact]
    public void Popup_CloseOnEscape_RestoresFocusToElementFocusedBeforeOpen()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var originalFocus = AddFocusableTextBox(host, 24f, 24f);
        var distractor = AddFocusableTextBox(host, 24f, 76f);
        RunLayout(uiRoot);

        FocusByClick(uiRoot, originalFocus);

        var popup = CreatePopup(dismissOnOutsideClick: false, canClose: true);
        popup.Left = 220f;
        popup.Top = 110f;
        popup.Show(host);
        RunLayout(uiRoot);

        FocusByClick(uiRoot, distractor);
        Assert.Same(distractor, FocusManager.GetFocusedElement());

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Escape));

        Assert.False(popup.IsOpen);
        Assert.Same(originalFocus, FocusManager.GetFocusedElement());
    }

    [Fact]
    public void Popup_CloseOnOutsideClick_RestoresFocusToElementFocusedBeforeOpen()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var originalFocus = AddFocusableTextBox(host, 24f, 24f);
        var distractor = AddFocusableTextBox(host, 24f, 76f);
        RunLayout(uiRoot);

        FocusByClick(uiRoot, originalFocus);

        var popup = CreatePopup(dismissOnOutsideClick: true, canClose: true);
        popup.Left = 220f;
        popup.Top = 110f;
        popup.Show(host);
        RunLayout(uiRoot);

        Click(uiRoot, new Vector2(distractor.LayoutSlot.X + 8f, distractor.LayoutSlot.Y + 8f));

        Assert.False(popup.IsOpen);
        Assert.Same(originalFocus, FocusManager.GetFocusedElement());
    }

    [Fact]
    public void Popup_CloseOnEscape_DoesNotRestoreFocusToDetachedElement()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var originalFocus = AddFocusableTextBox(host, 24f, 24f);
        var distractor = AddFocusableTextBox(host, 24f, 76f);
        RunLayout(uiRoot);

        FocusByClick(uiRoot, originalFocus);

        var popup = CreatePopup(dismissOnOutsideClick: false, canClose: true);
        popup.Left = 220f;
        popup.Top = 110f;
        popup.Show(host);
        RunLayout(uiRoot);

        FocusByClick(uiRoot, distractor);
        host.RemoveChild(originalFocus);
        RunLayout(uiRoot);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Escape));

        Assert.False(popup.IsOpen);
        Assert.Same(distractor, FocusManager.GetFocusedElement());
    }

    [Fact]
    public void Popup_CloseViaCloseButton_RestoresFocusToElementFocusedBeforeOpen()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var originalFocus = AddFocusableTextBox(host, 24f, 24f);
        var distractor = AddFocusableTextBox(host, 24f, 76f);
        RunLayout(uiRoot);

        FocusByClick(uiRoot, originalFocus);

        var popup = CreatePopup(dismissOnOutsideClick: false, canClose: true);
        popup.Left = 220f;
        popup.Top = 90f;
        popup.Width = 220f;
        popup.Height = 120f;
        popup.Show(host);
        RunLayout(uiRoot);

        FocusByClick(uiRoot, distractor);
        Assert.Same(distractor, FocusManager.GetFocusedElement());

        var closePoint = new Vector2(
            popup.LayoutSlot.X + popup.LayoutSlot.Width - 10f,
            popup.LayoutSlot.Y + 10f);
        Click(uiRoot, closePoint);

        Assert.False(popup.IsOpen);
        Assert.Same(originalFocus, FocusManager.GetFocusedElement());
    }

    [Fact]
    public void RightClickDismiss_DoesNotOpenAnotherContextMenuFromSameClick()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var target = new Button { Width = 160f, Height = 40f, Text = "Target" };
        host.AddChild(target);
        Canvas.SetLeft(target, 24f);
        Canvas.SetTop(target, 24f);

        var underlayMenu = CreateSimpleContextMenu();
        ContextMenu.SetContextMenu(target, underlayMenu);

        var popup = CreatePopup(dismissOnOutsideClick: true, canClose: true);
        popup.Left = 220f;
        popup.Top = 110f;
        popup.Show(host);
        RunLayout(uiRoot);

        var point = new Vector2(target.LayoutSlot.X + 8f, target.LayoutSlot.Y + 8f);
        RightClick(uiRoot, point);

        Assert.False(popup.IsOpen);
        Assert.False(underlayMenu.IsOpen);
    }

    [Fact]
    public void OutsideClick_InsidePopupBounds_DoesNotDismiss()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var popup = CreatePopup(dismissOnOutsideClick: true, canClose: true);
        popup.Left = 100f;
        popup.Top = 80f;
        popup.Show(host);
        RunLayout(uiRoot);

        // Click well inside the popup's own bounds - must not dismiss.
        var insidePoint = new Vector2(popup.LayoutSlot.X + 20f, popup.LayoutSlot.Y + popup.TitleBarHeight + 10f);
        Click(uiRoot, insidePoint);

        Assert.True(popup.IsOpen);
    }

    [Fact]
    public void OutsideClick_DismissOnOutsideClickFalse_DoesNotDismiss()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var buttonClicks = 0;
        var button = new Button { Width = 140f, Height = 40f, Text = "Behind" };
        button.Click += (_, _) => buttonClicks++;
        host.AddChild(button);
        Canvas.SetLeft(button, 24f);
        Canvas.SetTop(button, 24f);

        var popup = CreatePopup(dismissOnOutsideClick: false, canClose: true);
        popup.Left = 220f;
        popup.Top = 110f;
        popup.Show(host);
        RunLayout(uiRoot);

        Click(uiRoot, new Vector2(button.LayoutSlot.X + 8f, button.LayoutSlot.Y + 8f));

        // Popup stays open and the button click fires normally (not consumed).
        Assert.True(popup.IsOpen);
        Assert.Equal(1, buttonClicks);
    }

    [Fact]
    public void Escape_WithCanCloseFalse_DoesNotDismissPopup()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var popup = CreatePopup(dismissOnOutsideClick: false, canClose: false);
        popup.Left = 100f;
        popup.Top = 80f;
        popup.Show(host);
        RunLayout(uiRoot);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Escape));

        Assert.True(popup.IsOpen);
    }

    [Fact]
    public void EscapeWithShiftModifier_DoesNotDismissPopup()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var popup = CreatePopup(dismissOnOutsideClick: false, canClose: true);
        popup.Left = 100f;
        popup.Top = 80f;
        popup.Show(host);
        RunLayout(uiRoot);

        // Escape with Shift held - the modifiers check prevents dismiss.
        var shiftKeyboard = new KeyboardState(Keys.LeftShift, Keys.Escape);
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Escape, shiftKeyboard));

        Assert.True(popup.IsOpen);
    }

    [Fact]
    public void ZOrder_HigherZIndexPopup_IsDismissedFirstOnEscape()
    {
        var (uiRoot, host) = CreateUiRootWithHost();

        var lowerPopup = CreatePopup(dismissOnOutsideClick: true, canClose: true);
        lowerPopup.Left = 20f;
        lowerPopup.Top = 20f;
        lowerPopup.Show(host);

        var higherPopup = CreatePopup(dismissOnOutsideClick: true, canClose: true);
        higherPopup.Left = 60f;
        higherPopup.Top = 60f;
        higherPopup.Show(host);

        // Give it an explicitly higher z-index to be unambiguous.
        Panel.SetZIndex(higherPopup, Panel.GetZIndex(lowerPopup) + 5);
        RunLayout(uiRoot);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Escape));

        Assert.False(higherPopup.IsOpen);
        Assert.True(lowerPopup.IsOpen);
    }

    [Fact]
    public void ZOrder_HigherZIndexPopup_IsDismissedFirstOnOutsideClick()
    {
        var (uiRoot, host) = CreateUiRootWithHost();

        var lowerPopup = CreatePopup(dismissOnOutsideClick: true, canClose: true);
        lowerPopup.Left = 20f;
        lowerPopup.Top = 20f;
        lowerPopup.Show(host);

        var higherPopup = CreatePopup(dismissOnOutsideClick: true, canClose: true);
        higherPopup.Left = 240f;
        higherPopup.Top = 150f;
        higherPopup.Show(host);

        Panel.SetZIndex(higherPopup, Panel.GetZIndex(lowerPopup) + 5);
        RunLayout(uiRoot);

        // Click outside both popups - only the topmost one should be dismissed.
        Click(uiRoot, new Vector2(4f, 4f));

        Assert.False(higherPopup.IsOpen);
        Assert.True(lowerPopup.IsOpen);
    }

    [Fact]
    public void Popup_ReopenedAfterClose_CapturesFreshFocusBeforeOpen()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var firstFocus = AddFocusableTextBox(host, 24f, 24f);
        var secondFocus = AddFocusableTextBox(host, 24f, 76f);
        RunLayout(uiRoot);

        // First open - focus on firstFocus.
        FocusByClick(uiRoot, firstFocus);
        var popup = CreatePopup(dismissOnOutsideClick: false, canClose: true);
        popup.Left = 220f;
        popup.Top = 110f;
        popup.Show(host);
        RunLayout(uiRoot);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Escape));
        Assert.False(popup.IsOpen);
        Assert.Same(firstFocus, FocusManager.GetFocusedElement());

        // Second open - now focus on secondFocus before opening again.
        FocusByClick(uiRoot, secondFocus);
        popup.Show(host);
        RunLayout(uiRoot);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Escape));
        Assert.False(popup.IsOpen);
        Assert.Same(secondFocus, FocusManager.GetFocusedElement());
    }

    [Fact]
    public void Popup_ProgrammaticClose_DoesNotCrashWhenNoRestoreConsumerPresent()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var original = AddFocusableTextBox(host, 24f, 24f);
        RunLayout(uiRoot);

        FocusByClick(uiRoot, original);
        var popup = CreatePopup(dismissOnOutsideClick: false, canClose: true);
        popup.Left = 220f;
        popup.Top = 110f;
        popup.Show(host);
        RunLayout(uiRoot);

        // Close programmatically - no input pipeline involved, so TryConsumePendingFocusRestore
        // is never called. Verify no exception and that the popup is actually closed.
        var ex = Record.Exception(() => popup.Close());
        Assert.Null(ex);
        Assert.False(popup.IsOpen);
    }

    [Fact]
    public void OutsideClick_NonDismissPopupWithContextMenuAbove_ContextMenuClosesClickNotConsumed()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var buttonClicks = 0;
        var button = new Button { Width = 140f, Height = 40f, Text = "Behind" };
        button.Click += (_, _) => buttonClicks++;
        host.AddChild(button);
        Canvas.SetLeft(button, 24f);
        Canvas.SetTop(button, 24f);

        // Popup that does NOT auto-dismiss on outside click.
        var popup = CreatePopup(dismissOnOutsideClick: false, canClose: true);
        popup.Left = 220f;
        popup.Top = 110f;
        popup.Show(host);

        // Context menu on top (default StaysOpen=false -> auto-dismiss).
        var contextMenu = CreateSimpleContextMenu();
        contextMenu.StaysOpen = false;
        contextMenu.OpenAt(host, 120f, 80f);
        Panel.SetZIndex(contextMenu, Panel.GetZIndex(popup) + 1);
        RunLayout(uiRoot);

        // Click outside both overlays - topmost (context menu) should close.
        Click(uiRoot, new Vector2(button.LayoutSlot.X + 8f, button.LayoutSlot.Y + 8f));

        Assert.False(contextMenu.IsOpen);
        // Popup stays open (not configured to dismiss on outside click).
        Assert.True(popup.IsOpen);
        // Context menu dismiss consumed the click, so button was NOT invoked.
        Assert.Equal(0, buttonClicks);
    }

    [Fact]
    public void RightClickInsidePopup_AllowsContextMenuToOpen()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var target = new Button
        {
            Width = 120f,
            Height = 32f,
            Text = "Target"
        };
        var contextMenu = CreateSimpleContextMenu();
        ContextMenu.SetContextMenu(target, contextMenu);

        var popup = CreatePopup(dismissOnOutsideClick: true, canClose: true);
        popup.Content = target;
        popup.Left = 100f;
        popup.Top = 80f;
        popup.Show(host);
        RunLayout(uiRoot);

        // Right-click inside popup bounds should not dismiss, and should still open the target context menu.
        var insidePoint = new Vector2(target.LayoutSlot.X + 8f, target.LayoutSlot.Y + 8f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(insidePoint, rightPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(insidePoint, rightReleased: true));

        Assert.True(popup.IsOpen);
        Assert.True(contextMenu.IsOpen);
    }

    [Fact]
    public void PreviewKeyDown_HandledEscape_DoesNotDismissPopup()
    {
        var (uiRoot, host) = CreateUiRootWithHost();

        var popup = CreatePopup(dismissOnOutsideClick: false, canClose: true);
        popup.Left = 100f;
        popup.Top = 80f;
        popup.Show(host);

        // Place a focused element inside the popup that intercepts Escape at Preview stage.
        var interceptor = new TextBox { Width = 120f, Height = 32f };
        var escapeIntercepted = false;
        interceptor.AddHandler<KeyRoutedEventArgs>(UIElement.PreviewKeyDownEvent, (_, e) =>
        {
            if (e.Key == Keys.Escape)
            {
                e.Handled = true;
                escapeIntercepted = true;
            }
        });
        popup.Content = interceptor;

        RunLayout(uiRoot);
        FocusByClick(uiRoot, interceptor);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Escape));

        Assert.True(escapeIntercepted);
        Assert.True(popup.IsOpen);
    }

    private static Popup CreatePopup(bool dismissOnOutsideClick, bool canClose)
    {
        return new Popup
        {
            Width = 160f,
            Height = 90f,
            CanClose = canClose,
            DismissOnOutsideClick = dismissOnOutsideClick,
            Content = new Label { Text = "popup" }
        };
    }

    private static ContextMenu CreateSimpleContextMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "item" });
        return menu;
    }

    private static TextBox AddFocusableTextBox(Canvas host, float left, float top)
    {
        var textBox = new TextBox
        {
            Width = 160f,
            Height = 36f
        };
        host.AddChild(textBox);
        Canvas.SetLeft(textBox, left);
        Canvas.SetTop(textBox, top);
        return textBox;
    }

    private static (UiRoot UiRoot, Canvas Host) CreateUiRootWithHost()
    {
        FocusManager.ClearFocus();
        FocusManager.ClearPointerCapture();
        var host = new Canvas
        {
            Width = 480f,
            Height = 300f
        };
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);
        return (uiRoot, host);
    }

    private static void FocusByClick(UiRoot uiRoot, UIElement target)
    {
        Click(uiRoot, new Vector2(target.LayoutSlot.X + 8f, target.LayoutSlot.Y + 8f));
        Assert.Same(target, FocusManager.GetFocusedElement());
    }

    private static void Click(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));
    }

    private static void RightClick(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, rightPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, rightReleased: true));
    }

    private static InputDelta CreateKeyDownDelta(Keys key, KeyboardState? keyboard = null)
    {
        var state = keyboard ?? default;
        var pointer = new Vector2(12f, 12f);
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(state, default, pointer),
            PressedKeys = new List<Keys> { key },
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = false,
            WheelDelta = 0,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static InputDelta CreatePointerDelta(
        Vector2 pointer,
        bool leftPressed = false,
        bool leftReleased = false,
        bool rightPressed = false,
        bool rightReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = true,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = rightPressed,
            RightReleased = rightReleased,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void RunLayout(UiRoot uiRoot)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 480, 300));
    }
}
