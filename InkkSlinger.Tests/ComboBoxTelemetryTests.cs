using System;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ComboBoxTelemetryTests
{
    [Fact]
    public void ComboBox_RuntimeTelemetry_Captures_OpenSelectionRefresh_AndTypographyPaths()
    {
        _ = ComboBox.GetTelemetryAndReset();

        var (uiRoot, comboBox) = CreateFixture("TelemetryCombo", 24f);
        RunLayout(uiRoot);

        var style = new Style(typeof(ComboBoxItem));
        style.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, new Color(0x22, 0x55, 0x99)));
        comboBox.ItemContainerStyle = style;
        RunLayout(uiRoot);

        var clickPoint = new Vector2(
            comboBox.LayoutSlot.X + (comboBox.LayoutSlot.Width / 2f),
            comboBox.LayoutSlot.Y + (comboBox.LayoutSlot.Height / 2f));
        Assert.True(comboBox.HandlePointerDownFromInput(clickPoint));
        RunLayout(uiRoot);

        var dropDown = Assert.IsType<ListBox>(comboBox.DropDownListForTesting);
        dropDown.SelectedIndex = 1;
        RunLayout(uiRoot);

        comboBox.ItemContainerStyle = null;
        comboBox.Foreground = new Color(255, 220, 100);
        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        comboBox.IsDropDownOpen = false;
        RunLayout(uiRoot);

        var snapshot = comboBox.GetComboBoxSnapshotForDiagnostics();

        Assert.False(snapshot.IsDropDownOpen);
        Assert.True(snapshot.HasDropDownPopup);
        Assert.True(snapshot.HasDropDownList);
        Assert.False(snapshot.IsDropDownPopupOpen);
        Assert.Equal(1, snapshot.SelectedIndex);
        Assert.Equal("Beta Label", snapshot.SelectedText);
        Assert.Equal(3, snapshot.DropDownItemCount);
        Assert.True(snapshot.HandlePointerDownCallCount >= 1);
        Assert.Equal(1, snapshot.HandlePointerDownHitCount);
        Assert.Equal(0, snapshot.HandlePointerDownMissCount);
        Assert.True(snapshot.HandlePointerDownOpenToggleCount >= 1);
        Assert.True(snapshot.ItemsChangedCallCount >= 3);
        Assert.True(snapshot.SelectionChangedCallCount > 0);
        Assert.True(snapshot.SelectionChangedContainerScanCount >= 3);
        Assert.True(snapshot.SelectionChangedContainerMatchCount >= 3);
        Assert.True(snapshot.SelectionChangedDropDownSyncCount + snapshot.SelectionChangedDropDownSyncSkippedCount > 0);
        Assert.True(snapshot.CreateContainerCallCount > 0);
        Assert.True(snapshot.PrepareContainerCallCount > 0);
        Assert.True(snapshot.DependencyPropertyRefreshTriggerCount > 0);
        Assert.True(snapshot.MeasureOverrideCallCount > 0);
        Assert.True(snapshot.MeasureOverrideTextMeasureCount > 0);
        Assert.True(snapshot.DropDownOpenStateChangedOpenPathCount > 0);
        Assert.True(snapshot.DropDownOpenStateChangedClosePathCount > 0);
        Assert.True(snapshot.OpenDropDownCallCount > 0);
        Assert.True(snapshot.OpenDropDownPopupShowCount > 0);
        Assert.Equal(0, snapshot.OpenDropDownHostMissingCount);
        Assert.True(snapshot.EnsureDropDownListCreateCount > 0);
        Assert.True(snapshot.EnsureDropDownPopupCreateCount > 0);
        Assert.True(snapshot.EnsureDropDownPopupReuseCount > 0);
        Assert.True(snapshot.DropDownPopupClosedEventCount > 0);
        Assert.True(snapshot.DropDownSelectionChangedApplySelectionCount > 0);
        Assert.True(snapshot.RefreshDropDownItemsCallCount > 0);
        Assert.True(snapshot.RefreshDropDownItemsProjectedItemCount >= 3);
        Assert.True(snapshot.FindHostPanelFoundCount > 0);
        Assert.True(snapshot.GetDisplayTextCallCount > 0);
        Assert.True(snapshot.GetDisplayTextLabelCount > 0);
        Assert.True(snapshot.BuildDropDownContainerCallCount > 0);
        Assert.True(snapshot.ConfigureContainerFromItemCallCount > 0);
        Assert.True(snapshot.SyncContainerTypographyCallCount > 0);
        Assert.True(snapshot.SyncContainerTypographyStyleSkipCount > 0);
        Assert.True(snapshot.SyncContainerTypographyForegroundSetCount > 0);
        Assert.True(snapshot.SyncContainerTypographyFontFamilySetCount > 0);
        Assert.True(snapshot.SyncContainerTypographyFontSizeSetCount > 0);
        Assert.True(snapshot.SyncContainerTypographyFontWeightSetCount > 0);
        Assert.True(snapshot.SyncContainerTypographyFontStyleSetCount > 0);
    }

    [Fact]
    public void ComboBox_AggregateTelemetry_CapturesActivity_AndResets()
    {
        _ = ComboBox.GetTelemetryAndReset();

        var host = new Canvas
        {
            Width = 520f,
            Height = 320f
        };
        var first = CreateComboBox("FirstCombo");
        var second = CreateComboBox("SecondCombo");
        host.AddChild(first);
        host.AddChild(second);
        Canvas.SetLeft(first, 24f);
        Canvas.SetTop(first, 24f);
        Canvas.SetLeft(second, 24f);
        Canvas.SetTop(second, 96f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        first.IsDropDownOpen = true;
        RunLayout(uiRoot);
        first.IsDropDownOpen = false;
        RunLayout(uiRoot);

        second.SelectedIndex = 2;
        second.Foreground = new Color(180, 240, 200);
        RunLayout(uiRoot);

        var diagnostics = ComboBox.GetAggregateTelemetrySnapshotForDiagnostics();

        Assert.Equal(2, diagnostics.ConstructorCallCount);
        Assert.True(diagnostics.ItemsChangedCallCount >= 6);
        Assert.True(diagnostics.SelectionChangedCallCount > 0);
        Assert.True(diagnostics.MeasureOverrideCallCount > 0);
        Assert.True(diagnostics.OpenDropDownCallCount > 0);
        Assert.True(diagnostics.CloseDropDownCallCount > 0);
        Assert.True(diagnostics.EnsureDropDownListCreateCount > 0);
        Assert.True(diagnostics.EnsureDropDownPopupCreateCount > 0);
        Assert.True(diagnostics.RefreshDropDownItemsProjectedItemCount >= 3);
        Assert.True(diagnostics.FindHostPanelFoundCount > 0);
        Assert.True(diagnostics.GetDisplayTextCallCount > 0);
        Assert.True(diagnostics.ConfigureContainerFromItemCallCount > 0);
        Assert.True(diagnostics.SyncContainerTypographyCallCount > 0);

        var aggregate = ComboBox.GetTelemetryAndReset();

        Assert.Equal(2, aggregate.ConstructorCallCount);
        Assert.True(aggregate.MeasureOverrideCallCount > 0);
        Assert.True(aggregate.OpenDropDownCallCount > 0);
        Assert.True(aggregate.RefreshDropDownItemsCallCount > 0);
        Assert.True(aggregate.GetDisplayTextCallCount > 0);

        var cleared = ComboBox.GetTelemetryAndReset();

        Assert.Equal(0, cleared.ConstructorCallCount);
        Assert.Equal(0, cleared.MeasureOverrideCallCount);
        Assert.Equal(0, cleared.OpenDropDownCallCount);
        Assert.Equal(0, cleared.RefreshDropDownItemsCallCount);
        Assert.Equal(0, cleared.GetDisplayTextCallCount);
    }

    [Fact]
    public async Task DiagnosticsPipeline_Emits_ComboBoxContributorFacts()
    {
        _ = ComboBox.GetTelemetryAndReset();

        var root = new Canvas { Name = "Root" };
        var comboBox = CreateComboBox("FormatChoice");
        root.AddChild(comboBox);
        Canvas.SetLeft(comboBox, 24f);
        Canvas.SetTop(comboBox, 24f);

        using var host = new InkkOopsTestHost(root);
        comboBox.IsDropDownOpen = true;
        await host.AdvanceFrameAsync(1);
        comboBox.IsDropDownOpen = false;
        await host.AdvanceFrameAsync(1);

        var diagnostics = new InkkOopsVisualTreeDiagnostics(
        [
            new InkkOopsGenericElementDiagnosticsContributor(),
            new InkkOopsFrameworkElementDiagnosticsContributor(),
            new InkkOopsComboBoxDiagnosticsContributor()
        ]);

        var snapshot = diagnostics.Capture(
            root,
            new InkkOopsDiagnosticsContext
            {
                UiRoot = host.UiRoot,
                Viewport = host.GetViewportBounds(),
                HoveredElement = comboBox,
                FocusedElement = null,
                ArtifactName = "combobox"
            });
        var text = new DefaultInkkOopsDiagnosticsSerializer().SerializeVisualTree(snapshot);

        Assert.Contains("ComboBox#FormatChoice", text);
        Assert.Contains("comboBoxSelectedIndex=0", text);
        Assert.Contains("comboBoxSelectedText=Alpha", text);
        Assert.Contains("comboBoxRuntimeOpenDropDownCalls=", text);
        Assert.Contains("comboBoxRuntimeRefreshDropDownItemsCalls=", text);
        Assert.Contains("comboBoxOpenDropDownCalls=", text);
        Assert.Contains("comboBoxGetDisplayTextCalls=", text);
        Assert.Contains("frameworkGlobalMeasureCalls=", text);
    }

    private static (UiRoot UiRoot, ComboBox ComboBox) CreateFixture(string name, float top)
    {
        var host = new Canvas
        {
            Width = 420f,
            Height = 260f
        };
        var comboBox = CreateComboBox(name);
        host.AddChild(comboBox);
        Canvas.SetLeft(comboBox, 24f);
        Canvas.SetTop(comboBox, top);
        return (new UiRoot(host), comboBox);
    }

    private static ComboBox CreateComboBox(string name)
    {
        var comboBox = new ComboBox
        {
            Name = name,
            Width = 160f,
            Height = 36f,
            Padding = new Thickness(4f),
            Foreground = new Color(230, 230, 220),
            FontSize = 18f
        };
        comboBox.Items.Add("Alpha");
        comboBox.Items.Add(new Label { Content = "Beta Label" });
        comboBox.Items.Add(new ComboBoxItem { Text = "Gamma" });
        comboBox.SelectedIndex = 0;
        return comboBox;
    }

    private static void RunLayout(UiRoot uiRoot)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 640, 360));
    }
}