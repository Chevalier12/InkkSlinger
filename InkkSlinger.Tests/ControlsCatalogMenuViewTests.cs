using System;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogMenuViewTests
{
    [Fact]
    public void MenuCatalog_ShouldBuildWorkspaceAndInspectorMenus()
    {
        var (_, view, uiRoot) = CreateCatalogHost();
        RunLayout(uiRoot, 1280, 860, 16);

        var workspaceMenuHost = Assert.IsType<ContentControl>(view.FindName("WorkspaceMenuHost"));
        var inspectorMenuHost = Assert.IsType<ContentControl>(view.FindName("InspectorMenuHost"));
        var workspaceMenu = Assert.IsType<Menu>(workspaceMenuHost.Content);
        var inspectorMenu = Assert.IsType<Menu>(inspectorMenuHost.Content);
        var workspaceState = Assert.IsType<TextBlock>(view.FindName("WorkspaceMenuStateLabel"));
        var inspectorState = Assert.IsType<TextBlock>(view.FindName("InspectorMenuStateLabel"));

        var workspaceHeaders = workspaceMenu.GetTopLevelItems().Select(static item => item.Header).ToArray();
        var inspectorHeaders = inspectorMenu.GetTopLevelItems().Select(static item => item.Header).ToArray();

        Assert.Equal(new[] { "_File", "_Edit", "_View", "_Help" }, workspaceHeaders);
        Assert.Equal(new[] { "Session", "Diagnostics", "Share" }, inspectorHeaders);
        Assert.Equal("Workspace menu path: closed", workspaceState.Text);
        Assert.Equal("Inspector menu path: closed", inspectorState.Text);
    }

    [Fact]
    public void MenuCatalog_OpenExportTrail_ShouldOpenNestedWorkspacePathAndUpdateTelemetry()
    {
        var (_, view, uiRoot) = CreateCatalogHost();
        RunLayout(uiRoot, 1280, 860, 16);

        InvokeViewHandler(view, "OnOpenExportTrailClick");
        RunLayout(uiRoot, 1280, 860, 32);

        var fileMenu = GetPrivateField<MenuItem>(view, "_workspaceFileMenu");
        var exportMenu = GetPrivateField<MenuItem>(view, "_workspaceExportMenu");
        var openTransitions = Assert.IsType<TextBlock>(view.FindName("OpenTransitionsLabel"));
        var workspaceState = Assert.IsType<TextBlock>(view.FindName("WorkspaceMenuStateLabel"));
        var lastOpenPath = Assert.IsType<TextBlock>(view.FindName("LastOpenPathLabel"));

        Assert.True(fileMenu.IsSubmenuOpen);
        Assert.True(exportMenu.IsSubmenuOpen);
        Assert.Equal("Opens: 2", openTransitions.Text);
        Assert.Equal("Workspace menu path: File > Export", workspaceState.Text);
        Assert.Equal("Last open path: File > Export", lastOpenPath.Text);
    }

    [Fact]
    public void MenuCatalog_InvokingLeafAction_ShouldUpdateSurfaceAndTelemetry()
    {
        var (_, view, uiRoot) = CreateCatalogHost();
        RunLayout(uiRoot, 1280, 860, 16);

        InvokeViewHandler(view, "OnOpenExportTrailClick");
        RunLayout(uiRoot, 1280, 860, 32);

        var telemetryBundleItem = GetPrivateField<MenuItem>(view, "_workspaceTelemetryBundleItem");
        var invoked = InvokeMenuItemPointerUp(telemetryBundleItem);
        RunLayout(uiRoot, 1280, 860, 48);

        var invocationCount = Assert.IsType<TextBlock>(view.FindName("InvocationCountLabel"));
        var lastAction = Assert.IsType<TextBlock>(view.FindName("LastActionLabel"));
        var workspaceTitle = Assert.IsType<TextBlock>(view.FindName("WorkspaceTitleLabel"));
        var workspaceSummary = Assert.IsType<TextBlock>(view.FindName("WorkspaceSummaryLabel"));

        Assert.True(invoked);
        Assert.Equal("Invocations: 1", invocationCount.Text);
        Assert.Equal("Last action: File > Export > Telemetry bundle", lastAction.Text);
        Assert.Equal("Telemetry bundle ready", workspaceTitle.Text);
        Assert.Contains("runtime telemetry", workspaceSummary.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MenuCatalog_OpenAutomationTrail_ShouldOpenInspectorNestedPath()
    {
        var (_, view, uiRoot) = CreateCatalogHost();
        RunLayout(uiRoot, 1280, 860, 16);

        InvokeViewHandler(view, "OnOpenAutomationTrailClick");
        RunLayout(uiRoot, 1280, 860, 32);

        var diagnosticsMenu = GetPrivateField<MenuItem>(view, "_inspectorDiagnosticsMenu");
        var automationMenu = GetPrivateField<MenuItem>(view, "_inspectorAutomationMenu");
        var inspectorState = Assert.IsType<TextBlock>(view.FindName("InspectorMenuStateLabel"));
        var lastTarget = Assert.IsType<TextBlock>(view.FindName("LastTargetLabel"));

        Assert.True(diagnosticsMenu.IsSubmenuOpen);
        Assert.True(automationMenu.IsSubmenuOpen);
        Assert.Equal("Inspector menu path: Diagnostics > Automation", inspectorState.Text);
        Assert.Equal("Last target: Inspector strip", lastTarget.Text);
    }

    [Fact]
    public void MenuCatalog_ClickingTopLevelFile_ShouldPlaceSubmenuBelowMenuRow()
    {
        var (_, view, uiRoot) = CreateCatalogHost();
        RunLayout(uiRoot, 1280, 860, 16);

        var fileMenu = GetPrivateField<MenuItem>(view, "_workspaceFileMenu");
        var handled = InvokeMenuItemPointerDown(fileMenu);
        RunLayout(uiRoot, 1280, 860, 32);

        Assert.True(handled);
        Assert.True(fileMenu.IsSubmenuOpen);
        Assert.True(fileMenu.TryGetOpenSubmenuBounds(out var submenuBounds));
        Assert.True(
            submenuBounds.Y >= fileMenu.LayoutSlot.Y + fileMenu.LayoutSlot.Height - 0.01f,
            $"Expected File submenu to open below the top-level row. Item={FormatRect(fileMenu.LayoutSlot)}, Submenu={FormatRect(submenuBounds)}");
    }

    private static (ControlsCatalogView Catalog, MenuView View, UiRoot UiRoot) CreateCatalogHost()
    {
        var catalog = new ControlsCatalogView();
        catalog.ShowControl("Menu");

        var previewHost = Assert.IsType<ContentControl>(catalog.FindName("PreviewHost"));
        var view = Assert.IsType<MenuView>(previewHost.Content);
        var uiRoot = new UiRoot(catalog);
        return (catalog, view, uiRoot);
    }

    private static void InvokeViewHandler(MenuView view, string methodName)
    {
        var method = typeof(MenuView).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(view, new object?[] { null, new RoutedSimpleEventArgs(Button.ClickEvent) });
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field!.GetValue(instance));
    }

    private static bool InvokeMenuItemPointerUp(MenuItem item)
    {
        var method = typeof(MenuItem).GetMethod("HandlePointerUpFromInput", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<bool>(method!.Invoke(item, Array.Empty<object>()));
    }

    private static bool InvokeMenuItemPointerDown(MenuItem item)
    {
        var method = typeof(MenuItem).GetMethod("HandlePointerDownFromInput", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<bool>(method!.Invoke(item, Array.Empty<object>()));
    }

    private static string FormatRect(LayoutRect rect)
    {
        return $"X={rect.X:0.##}, Y={rect.Y:0.##}, W={rect.Width:0.##}, H={rect.Height:0.##}";
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }
}