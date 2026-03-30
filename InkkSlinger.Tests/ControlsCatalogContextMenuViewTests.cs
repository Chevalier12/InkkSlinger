using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogContextMenuViewTests
{
    [Fact]
    public void ContextMenuCatalog_ShouldWireAttachedTargetsAndMenus()
    {
        var (catalog, view, uiRoot) = CreateCatalogHost();
        _ = catalog;
        RunLayout(uiRoot, 1280, 820, 16);

        var surfaceTarget = Assert.IsType<Border>(view.FindName("SurfaceTarget"));
        var inspectorTarget = Assert.IsType<Button>(view.FindName("InspectorTarget"));
        var persistentTarget = Assert.IsType<Border>(view.FindName("PersistentTarget"));
        var attachedHintLabel = Assert.IsType<TextBlock>(view.FindName("AttachedHintLabel"));

        var surfaceMenu = Assert.IsType<ContextMenu>(ContextMenu.GetContextMenu(surfaceTarget));
        var inspectorMenu = Assert.IsType<ContextMenu>(ContextMenu.GetContextMenu(inspectorTarget));
        var persistentMenu = Assert.IsType<ContextMenu>(ContextMenu.GetContextMenu(persistentTarget));

        Assert.True(surfaceMenu.Items.Count >= 5);
        Assert.True(inspectorMenu.Items.Count >= 3);
        Assert.True(persistentMenu.StaysOpen);
        Assert.Contains("review dock", attachedHintLabel.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ContextMenuCatalog_AnchoredLaunch_ShouldOpenMenuAndUpdatePlacementTelemetry()
    {
        var (_, view, uiRoot) = CreateCatalogHost();
        RunLayout(uiRoot, 1280, 820, 16);

        InvokeViewHandler(view, "OnOpenAnchoredMenuClick");
        RunLayout(uiRoot, 1280, 820, 32);

        var openMenu = GetPrivateField<ContextMenu>(view, "_placementMenu");
        var openedCountLabel = Assert.IsType<TextBlock>(view.FindName("OpenedCountLabel"));
        var placementStateLabel = Assert.IsType<TextBlock>(view.FindName("PlacementStateLabel"));

        Assert.True(openMenu.IsOpen);
        Assert.Equal("Opened: 1", openedCountLabel.Text);
        Assert.Contains("Bottom on placement anchor", placementStateLabel.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ContextMenuCatalog_RightClickOnSurfaceTarget_ShouldOpenAttachedMenu()
    {
        var (_, view, uiRoot) = CreateCatalogHost();
        RunLayout(uiRoot, 1280, 820, 16);

        var surfaceTarget = Assert.IsType<Border>(view.FindName("SurfaceTarget"));
        var pointer = GetCenter(surfaceTarget);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, rightPressed: true, pointerMoved: false));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, rightReleased: true, pointerMoved: false));
        RunLayout(uiRoot, 1280, 820, 32);

        var surfaceMenu = Assert.IsType<ContextMenu>(ContextMenu.GetContextMenu(surfaceTarget));
        var lastTargetLabel = Assert.IsType<TextBlock>(view.FindName("LastTargetLabel"));

        Assert.True(surfaceMenu.IsOpen);
        Assert.Equal("Last target: Design surface", lastTargetLabel.Text);
    }

    [Fact]
    public void ContextMenuCatalog_PersistentMenu_ShouldRemainOpenAfterLeafInvocation()
    {
        var (_, view, uiRoot) = CreateCatalogHost();
        RunLayout(uiRoot, 1280, 820, 16);

        InvokeViewHandler(view, "OnOpenPersistentMenuClick");
        RunLayout(uiRoot, 1280, 820, 32);

        var openMenu = GetPrivateField<ContextMenu>(view, "_persistentMenu");
        Assert.True(openMenu.IsOpen);

        var compactDensityItem = openMenu.Items.OfType<MenuItem>().FirstOrDefault(static item => item.Header == "Compact density");
        Assert.NotNull(compactDensityItem);

        var invoked = InvokeMenuPointerUp(openMenu, compactDensityItem!);
        RunLayout(uiRoot, 1280, 820, 48);

        var persistentStatusLabel = Assert.IsType<TextBlock>(view.FindName("PersistentStatusLabel"));

        Assert.True(invoked);
        Assert.True(openMenu.IsOpen);
        Assert.Contains("open after 'Compact density'", persistentStatusLabel.Text, StringComparison.Ordinal);
    }

    private static (ControlsCatalogView Catalog, ContextMenuView View, UiRoot UiRoot) CreateCatalogHost()
    {
        var catalog = new ControlsCatalogView();
        catalog.ShowControl("ContextMenu");

        var previewHost = Assert.IsType<ContentControl>(catalog.FindName("PreviewHost"));
        var view = Assert.IsType<ContextMenuView>(previewHost.Content);
        var uiRoot = new UiRoot(catalog);
        return (catalog, view, uiRoot);
    }

    private static void InvokeViewHandler(ContextMenuView view, string methodName)
    {
        var method = typeof(ContextMenuView).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(view, new object?[] { null, new RoutedSimpleEventArgs(Button.ClickEvent) });
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field!.GetValue(instance));
    }

    private static bool InvokeMenuPointerUp(ContextMenu menu, MenuItem item)
    {
        var method = typeof(ContextMenu).GetMethod("HandlePointerUpFromInput", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<bool>(method!.Invoke(menu, new object[] { item }));
    }

    private static Vector2 GetCenter(UIElement element)
    {
        return new Vector2(
            element.LayoutSlot.X + (element.LayoutSlot.Width * 0.5f),
            element.LayoutSlot.Y + (element.LayoutSlot.Height * 0.5f));
    }

    private static InputDelta CreatePointerDelta(
        Vector2 pointer,
        bool pointerMoved = true,
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
            PointerMoved = pointerMoved,
            WheelDelta = 0,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = rightPressed,
            RightReleased = rightReleased,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }
}