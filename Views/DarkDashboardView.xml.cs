using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class DarkDashboardView : UserControl
{
    public DarkDashboardView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "DarkDashboardView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
    }

    public void SetFont(SpriteFont? font)
    {
        if (font == null)
            return;

        ApplyFontRecursive(this, font);
    }

    // ═══════════════════════════════════════════════════════════
    //  Navigation handlers
    // ═══════════════════════════════════════════════════════════

    private void OnNavDashboardClick(object? sender, RoutedSimpleEventArgs args)
        => SetActiveNav(NavDashboard);

    private void OnNavProjectsClick(object? sender, RoutedSimpleEventArgs args)
        => SetActiveNav(NavProjects);

    private void OnNavAnalyticsClick(object? sender, RoutedSimpleEventArgs args)
        => SetActiveNav(NavAnalytics);

    private void OnNavMessagesClick(object? sender, RoutedSimpleEventArgs args)
        => SetActiveNav(NavMessages);

    private void OnNavFilesClick(object? sender, RoutedSimpleEventArgs args)
        => SetActiveNav(NavFiles);

    private void OnNavSettingsClick(object? sender, RoutedSimpleEventArgs args)
        => SetActiveNav(NavSettings);

    private void OnNavTeamClick(object? sender, RoutedSimpleEventArgs args)
        => SetActiveNav(NavTeam);

    private void SetActiveNav(Button? activeButton)
    {
        var navButtons = new[] { NavDashboard, NavProjects, NavAnalytics, NavMessages, NavFiles, NavSettings, NavTeam };
        var activeStyle = Resources.TryGetValue("ActiveNavButton", out var s) ? s as Style : null;
        var normalStyle = Resources.TryGetValue("NavButton", out var n) ? n as Style : null;

        foreach (var btn in navButtons)
        {
            if (btn == null) continue;
            btn.Style = btn == activeButton ? activeStyle : normalStyle;
        }

        UpdateStatus($"Navigated to {activeButton?.Text?.Replace("◆  ", "").Replace("◇  ", "") ?? "unknown"}");
    }

    // ═══════════════════════════════════════════════════════════
    //  Top bar actions
    // ═══════════════════════════════════════════════════════════

    private void OnExportClick(object? sender, RoutedSimpleEventArgs args)
        => UpdateStatus("Exporting data...");

    private void OnNewProjectClick(object? sender, RoutedSimpleEventArgs args)
        => UpdateStatus("Creating new project...");

    // ═══════════════════════════════════════════════════════════
    //  Quick action handlers
    // ═══════════════════════════════════════════════════════════

    private void OnQuickNewProjectClick(object? sender, RoutedSimpleEventArgs args)
        => UpdateStatus("New project wizard opened.");

    private void OnInviteMemberClick(object? sender, RoutedSimpleEventArgs args)
        => UpdateStatus("Invite member dialog opened.");

    private void OnUploadFilesClick(object? sender, RoutedSimpleEventArgs args)
        => UpdateStatus("File upload started...");

    private void OnGenerateReportClick(object? sender, RoutedSimpleEventArgs args)
        => UpdateStatus("Generating report...");

    private void OnViewAllActivityClick(object? sender, RoutedSimpleEventArgs args)
        => UpdateStatus("Viewing all activity...");

    // ═══════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════

    private void UpdateStatus(string message)
    {
        if (StatusLabel != null)
            StatusLabel.Text = message;
    }

    private static void ApplyFontRecursive(UIElement? element, SpriteFont font)
    {
        if (element == null) return;

        if (element is Label label)
            label.Font = font;
        if (element is TextBlock textBlock)
            textBlock.Font = font;
        if (element is Button button)
            button.Font = font;
        if (element is TextBox textBox)
            textBox.Font = font;

        foreach (var child in element.GetVisualChildren())
            ApplyFontRecursive(child, font);
    }
}
