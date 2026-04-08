namespace InkkSlinger;

public sealed class InkkOopsComboBoxDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 40;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        if (element is not ComboBox comboBox)
        {
            return;
        }

        var runtime = comboBox.GetComboBoxSnapshotForDiagnostics();
        var telemetry = ComboBox.GetAggregateTelemetrySnapshotForDiagnostics();

        builder.Add("comboBoxIsDropDownOpen", runtime.IsDropDownOpen);
        builder.Add("comboBoxHasDropDownPopup", runtime.HasDropDownPopup);
        builder.Add("comboBoxIsDropDownPopupOpen", runtime.IsDropDownPopupOpen);
        builder.Add("comboBoxHasDropDownList", runtime.HasDropDownList);
        builder.Add("comboBoxIsSynchronizingDropDown", runtime.IsSynchronizingDropDown);
        builder.Add("comboBoxSelectedIndex", runtime.SelectedIndex);
        builder.Add("comboBoxSelectedText", Escape(runtime.SelectedText));
        builder.Add("comboBoxItemContainerCount", runtime.ItemContainerCount);
        builder.Add("comboBoxDropDownItemCount", runtime.DropDownItemCount);
        builder.Add("comboBoxLayoutSlot", $"{runtime.LayoutSlotWidth:0.##},{runtime.LayoutSlotHeight:0.##}");

        builder.Add("comboBoxRuntimeHandlePointerDownCalls", runtime.HandlePointerDownCallCount);
        builder.Add("comboBoxRuntimeHandlePointerDownMs", FormatMilliseconds(runtime.HandlePointerDownMilliseconds));
        builder.Add("comboBoxRuntimePointerHits", runtime.HandlePointerDownHitCount);
        builder.Add("comboBoxRuntimePointerMisses", runtime.HandlePointerDownMissCount);
        builder.Add("comboBoxRuntimePointerOpenToggles", runtime.HandlePointerDownOpenToggleCount);
        builder.Add("comboBoxRuntimePointerCloseToggles", runtime.HandlePointerDownCloseToggleCount);
        builder.Add("comboBoxRuntimeItemsChangedCalls", runtime.ItemsChangedCallCount);
        builder.Add("comboBoxRuntimeSelectionChangedCalls", runtime.SelectionChangedCallCount);
        builder.Add("comboBoxRuntimeSelectionChangedMs", FormatMilliseconds(runtime.SelectionChangedMilliseconds));
        builder.Add("comboBoxRuntimeSelectionContainerScans", runtime.SelectionChangedContainerScanCount);
        builder.Add("comboBoxRuntimeSelectionContainerMatches", runtime.SelectionChangedContainerMatchCount);
        builder.Add("comboBoxRuntimeSelectionDropDownSync", runtime.SelectionChangedDropDownSyncCount);
        builder.Add("comboBoxRuntimeSelectionDropDownSyncSkipped", runtime.SelectionChangedDropDownSyncSkippedCount);
        builder.Add("comboBoxRuntimeCreateContainerCalls", runtime.CreateContainerCallCount);
        builder.Add("comboBoxRuntimePrepareContainerCalls", runtime.PrepareContainerCallCount);
        builder.Add("comboBoxRuntimePrepareConfigured", runtime.PrepareContainerConfiguredFromItemCount);
        builder.Add("comboBoxRuntimePrepareTypographySync", runtime.PrepareContainerTypographySyncCount);
        builder.Add("comboBoxRuntimePrepareUnexpected", runtime.PrepareContainerUnexpectedElementCount);
        builder.Add("comboBoxRuntimeDependencyPropertyChanged", runtime.DependencyPropertyChangedCallCount);
        builder.Add("comboBoxRuntimeDependencyPropertyRefresh", runtime.DependencyPropertyRefreshTriggerCount);
        builder.Add("comboBoxRuntimeMeasureOverrideCalls", runtime.MeasureOverrideCallCount);
        builder.Add("comboBoxRuntimeMeasureOverrideMs", FormatMilliseconds(runtime.MeasureOverrideMilliseconds));
        builder.Add("comboBoxRuntimeMeasureEmptyText", runtime.MeasureOverrideEmptyTextCount);
        builder.Add("comboBoxRuntimeMeasureTextWidth", runtime.MeasureOverrideTextMeasureCount);
        builder.Add("comboBoxRuntimeRenderCalls", runtime.RenderCallCount);
        builder.Add("comboBoxRuntimeRenderMs", FormatMilliseconds(runtime.RenderMilliseconds));
        builder.Add("comboBoxRuntimeRenderBorders", runtime.RenderBorderDrawCount);
        builder.Add("comboBoxRuntimeRenderEmptyTextSkips", runtime.RenderEmptyTextSkipCount);
        builder.Add("comboBoxRuntimeRenderTextDraws", runtime.RenderTextDrawCount);
        builder.Add("comboBoxRuntimeOpenStateChangedCalls", runtime.DropDownOpenStateChangedCallCount);
        builder.Add("comboBoxRuntimeOpenStateChangedMs", FormatMilliseconds(runtime.DropDownOpenStateChangedMilliseconds));
        builder.Add("comboBoxRuntimeOpenStateSyncSkips", runtime.DropDownOpenStateChangedSyncSkipCount);
        builder.Add("comboBoxRuntimeOpenStateOpenPath", runtime.DropDownOpenStateChangedOpenPathCount);
        builder.Add("comboBoxRuntimeOpenStateClosePath", runtime.DropDownOpenStateChangedClosePathCount);
        builder.Add("comboBoxRuntimeOpenDropDownCalls", runtime.OpenDropDownCallCount);
        builder.Add("comboBoxRuntimeOpenDropDownMs", FormatMilliseconds(runtime.OpenDropDownMilliseconds));
        builder.Add("comboBoxRuntimeOpenDropDownHostMissing", runtime.OpenDropDownHostMissingCount);
        builder.Add("comboBoxRuntimeOpenDropDownPopupShow", runtime.OpenDropDownPopupShowCount);
        builder.Add("comboBoxRuntimeOpenDropDownPopupUnavailable", runtime.OpenDropDownPopupUnavailableCount);
        builder.Add("comboBoxRuntimeCloseDropDownCalls", runtime.CloseDropDownCallCount);
        builder.Add("comboBoxRuntimeCloseDropDownPopupMissing", runtime.CloseDropDownPopupMissingCount);
        builder.Add("comboBoxRuntimeEnsureDropDownControlsCalls", runtime.EnsureDropDownControlsCallCount);
        builder.Add("comboBoxRuntimeEnsureDropDownControlsMs", FormatMilliseconds(runtime.EnsureDropDownControlsMilliseconds));
        builder.Add("comboBoxRuntimeDropDownListCreates", runtime.EnsureDropDownListCreateCount);
        builder.Add("comboBoxRuntimeDropDownListReuses", runtime.EnsureDropDownListReuseCount);
        builder.Add("comboBoxRuntimeDropDownPopupCreates", runtime.EnsureDropDownPopupCreateCount);
        builder.Add("comboBoxRuntimeDropDownPopupReuses", runtime.EnsureDropDownPopupReuseCount);
        builder.Add("comboBoxRuntimePopupClosedEvents", runtime.DropDownPopupClosedEventCount);
        builder.Add("comboBoxRuntimePopupClosedSyncClose", runtime.DropDownPopupClosedSyncCloseCount);
        builder.Add("comboBoxRuntimePopupClosedAlreadyClosed", runtime.DropDownPopupClosedAlreadyClosedCount);
        builder.Add("comboBoxRuntimeDropDownSelectionChangedCalls", runtime.DropDownSelectionChangedCallCount);
        builder.Add("comboBoxRuntimeDropDownSelectionChangedMs", FormatMilliseconds(runtime.DropDownSelectionChangedMilliseconds));
        builder.Add("comboBoxRuntimeDropDownSelectionNullList", runtime.DropDownSelectionChangedNullListSkipCount);
        builder.Add("comboBoxRuntimeDropDownSelectionSyncSkip", runtime.DropDownSelectionChangedSynchronizingSkipCount);
        builder.Add("comboBoxRuntimeDropDownSelectionApply", runtime.DropDownSelectionChangedApplySelectionCount);
        builder.Add("comboBoxRuntimeRefreshDropDownItemsCalls", runtime.RefreshDropDownItemsCallCount);
        builder.Add("comboBoxRuntimeRefreshDropDownItemsMs", FormatMilliseconds(runtime.RefreshDropDownItemsMilliseconds));
        builder.Add("comboBoxRuntimeRefreshDropDownItemsNullList", runtime.RefreshDropDownItemsNullListSkipCount);
        builder.Add("comboBoxRuntimeRefreshProjectedItems", runtime.RefreshDropDownItemsProjectedItemCount);
        builder.Add("comboBoxRuntimeRefreshSelectedIndexSync", runtime.RefreshDropDownItemsSelectedIndexSyncCount);
        builder.Add("comboBoxRuntimeFindHostPanelCalls", runtime.FindHostPanelCallCount);
        builder.Add("comboBoxRuntimeFindHostPanelFound", runtime.FindHostPanelFoundCount);
        builder.Add("comboBoxRuntimeFindHostPanelMissing", runtime.FindHostPanelMissingCount);
        builder.Add("comboBoxRuntimeGetDisplayTextCalls", runtime.GetDisplayTextCallCount);
        builder.Add("comboBoxRuntimeGetDisplayTextMs", FormatMilliseconds(runtime.GetDisplayTextMilliseconds));
        builder.Add("comboBoxRuntimeDisplayTextComboBoxText", runtime.GetDisplayTextComboBoxItemTextCount);
        builder.Add("comboBoxRuntimeDisplayTextComboBoxLabel", runtime.GetDisplayTextComboBoxItemLabelCount);
        builder.Add("comboBoxRuntimeDisplayTextComboBoxContentToString", runtime.GetDisplayTextComboBoxItemContentToStringCount);
        builder.Add("comboBoxRuntimeDisplayTextListBoxLabel", runtime.GetDisplayTextListBoxItemLabelCount);
        builder.Add("comboBoxRuntimeDisplayTextLabel", runtime.GetDisplayTextLabelCount);
        builder.Add("comboBoxRuntimeDisplayTextResolved", runtime.GetDisplayTextResolveDisplayPathCount);
        builder.Add("comboBoxRuntimeDisplayTextEmpty", runtime.GetDisplayTextEmptyResultCount);
        builder.Add("comboBoxRuntimeBuildDropDownContainerCalls", runtime.BuildDropDownContainerCallCount);
        builder.Add("comboBoxRuntimeConfigureContainerCalls", runtime.ConfigureContainerFromItemCallCount);
        builder.Add("comboBoxRuntimeSyncTypographyCalls", runtime.SyncContainerTypographyCallCount);
        builder.Add("comboBoxRuntimeSyncTypographyStyleSkip", runtime.SyncContainerTypographyStyleSkipCount);
        builder.Add("comboBoxRuntimeSyncTypographyForegroundSet", runtime.SyncContainerTypographyForegroundSetCount);
        builder.Add("comboBoxRuntimeSyncTypographyFontFamilySet", runtime.SyncContainerTypographyFontFamilySetCount);
        builder.Add("comboBoxRuntimeSyncTypographyFontSizeSet", runtime.SyncContainerTypographyFontSizeSetCount);
        builder.Add("comboBoxRuntimeSyncTypographyFontWeightSet", runtime.SyncContainerTypographyFontWeightSetCount);
        builder.Add("comboBoxRuntimeSyncTypographyFontStyleSet", runtime.SyncContainerTypographyFontStyleSetCount);

        builder.Add("comboBoxHandlePointerDownCalls", telemetry.HandlePointerDownCallCount);
        builder.Add("comboBoxHandlePointerDownMs", FormatMilliseconds(telemetry.HandlePointerDownMilliseconds));
        builder.Add("comboBoxPointerHits", telemetry.HandlePointerDownHitCount);
        builder.Add("comboBoxPointerMisses", telemetry.HandlePointerDownMissCount);
        builder.Add("comboBoxPointerOpenToggles", telemetry.HandlePointerDownOpenToggleCount);
        builder.Add("comboBoxPointerCloseToggles", telemetry.HandlePointerDownCloseToggleCount);
        builder.Add("comboBoxItemsChangedCalls", telemetry.ItemsChangedCallCount);
        builder.Add("comboBoxSelectionChangedCalls", telemetry.SelectionChangedCallCount);
        builder.Add("comboBoxSelectionChangedMs", FormatMilliseconds(telemetry.SelectionChangedMilliseconds));
        builder.Add("comboBoxSelectionContainerScans", telemetry.SelectionChangedContainerScanCount);
        builder.Add("comboBoxSelectionContainerMatches", telemetry.SelectionChangedContainerMatchCount);
        builder.Add("comboBoxSelectionDropDownSync", telemetry.SelectionChangedDropDownSyncCount);
        builder.Add("comboBoxSelectionDropDownSyncSkipped", telemetry.SelectionChangedDropDownSyncSkippedCount);
        builder.Add("comboBoxCreateContainerCalls", telemetry.CreateContainerCallCount);
        builder.Add("comboBoxPrepareContainerCalls", telemetry.PrepareContainerCallCount);
        builder.Add("comboBoxPrepareConfigured", telemetry.PrepareContainerConfiguredFromItemCount);
        builder.Add("comboBoxPrepareTypographySync", telemetry.PrepareContainerTypographySyncCount);
        builder.Add("comboBoxPrepareUnexpected", telemetry.PrepareContainerUnexpectedElementCount);
        builder.Add("comboBoxDependencyPropertyChanged", telemetry.DependencyPropertyChangedCallCount);
        builder.Add("comboBoxDependencyPropertyRefresh", telemetry.DependencyPropertyRefreshTriggerCount);
        builder.Add("comboBoxMeasureOverrideCalls", telemetry.MeasureOverrideCallCount);
        builder.Add("comboBoxMeasureOverrideMs", FormatMilliseconds(telemetry.MeasureOverrideMilliseconds));
        builder.Add("comboBoxMeasureEmptyText", telemetry.MeasureOverrideEmptyTextCount);
        builder.Add("comboBoxMeasureTextWidth", telemetry.MeasureOverrideTextMeasureCount);
        builder.Add("comboBoxRenderCalls", telemetry.RenderCallCount);
        builder.Add("comboBoxRenderMs", FormatMilliseconds(telemetry.RenderMilliseconds));
        builder.Add("comboBoxRenderBorders", telemetry.RenderBorderDrawCount);
        builder.Add("comboBoxRenderEmptyTextSkips", telemetry.RenderEmptyTextSkipCount);
        builder.Add("comboBoxRenderTextDraws", telemetry.RenderTextDrawCount);
        builder.Add("comboBoxOpenStateChangedCalls", telemetry.DropDownOpenStateChangedCallCount);
        builder.Add("comboBoxOpenStateChangedMs", FormatMilliseconds(telemetry.DropDownOpenStateChangedMilliseconds));
        builder.Add("comboBoxOpenStateSyncSkips", telemetry.DropDownOpenStateChangedSyncSkipCount);
        builder.Add("comboBoxOpenStateOpenPath", telemetry.DropDownOpenStateChangedOpenPathCount);
        builder.Add("comboBoxOpenStateClosePath", telemetry.DropDownOpenStateChangedClosePathCount);
        builder.Add("comboBoxOpenDropDownCalls", telemetry.OpenDropDownCallCount);
        builder.Add("comboBoxOpenDropDownMs", FormatMilliseconds(telemetry.OpenDropDownMilliseconds));
        builder.Add("comboBoxOpenDropDownHostMissing", telemetry.OpenDropDownHostMissingCount);
        builder.Add("comboBoxOpenDropDownPopupShow", telemetry.OpenDropDownPopupShowCount);
        builder.Add("comboBoxOpenDropDownPopupUnavailable", telemetry.OpenDropDownPopupUnavailableCount);
        builder.Add("comboBoxCloseDropDownCalls", telemetry.CloseDropDownCallCount);
        builder.Add("comboBoxCloseDropDownPopupMissing", telemetry.CloseDropDownPopupMissingCount);
        builder.Add("comboBoxEnsureDropDownControlsCalls", telemetry.EnsureDropDownControlsCallCount);
        builder.Add("comboBoxEnsureDropDownControlsMs", FormatMilliseconds(telemetry.EnsureDropDownControlsMilliseconds));
        builder.Add("comboBoxDropDownListCreates", telemetry.EnsureDropDownListCreateCount);
        builder.Add("comboBoxDropDownListReuses", telemetry.EnsureDropDownListReuseCount);
        builder.Add("comboBoxDropDownPopupCreates", telemetry.EnsureDropDownPopupCreateCount);
        builder.Add("comboBoxDropDownPopupReuses", telemetry.EnsureDropDownPopupReuseCount);
        builder.Add("comboBoxPopupClosedEvents", telemetry.DropDownPopupClosedEventCount);
        builder.Add("comboBoxPopupClosedSyncClose", telemetry.DropDownPopupClosedSyncCloseCount);
        builder.Add("comboBoxPopupClosedAlreadyClosed", telemetry.DropDownPopupClosedAlreadyClosedCount);
        builder.Add("comboBoxDropDownSelectionChangedCalls", telemetry.DropDownSelectionChangedCallCount);
        builder.Add("comboBoxDropDownSelectionChangedMs", FormatMilliseconds(telemetry.DropDownSelectionChangedMilliseconds));
        builder.Add("comboBoxDropDownSelectionNullList", telemetry.DropDownSelectionChangedNullListSkipCount);
        builder.Add("comboBoxDropDownSelectionSyncSkip", telemetry.DropDownSelectionChangedSynchronizingSkipCount);
        builder.Add("comboBoxDropDownSelectionApply", telemetry.DropDownSelectionChangedApplySelectionCount);
        builder.Add("comboBoxRefreshDropDownItemsCalls", telemetry.RefreshDropDownItemsCallCount);
        builder.Add("comboBoxRefreshDropDownItemsMs", FormatMilliseconds(telemetry.RefreshDropDownItemsMilliseconds));
        builder.Add("comboBoxRefreshDropDownItemsNullList", telemetry.RefreshDropDownItemsNullListSkipCount);
        builder.Add("comboBoxRefreshProjectedItems", telemetry.RefreshDropDownItemsProjectedItemCount);
        builder.Add("comboBoxRefreshSelectedIndexSync", telemetry.RefreshDropDownItemsSelectedIndexSyncCount);
        builder.Add("comboBoxFindHostPanelCalls", telemetry.FindHostPanelCallCount);
        builder.Add("comboBoxFindHostPanelFound", telemetry.FindHostPanelFoundCount);
        builder.Add("comboBoxFindHostPanelMissing", telemetry.FindHostPanelMissingCount);
        builder.Add("comboBoxGetDisplayTextCalls", telemetry.GetDisplayTextCallCount);
        builder.Add("comboBoxGetDisplayTextMs", FormatMilliseconds(telemetry.GetDisplayTextMilliseconds));
        builder.Add("comboBoxDisplayTextComboBoxText", telemetry.GetDisplayTextComboBoxItemTextCount);
        builder.Add("comboBoxDisplayTextComboBoxLabel", telemetry.GetDisplayTextComboBoxItemLabelCount);
        builder.Add("comboBoxDisplayTextComboBoxContentToString", telemetry.GetDisplayTextComboBoxItemContentToStringCount);
        builder.Add("comboBoxDisplayTextListBoxLabel", telemetry.GetDisplayTextListBoxItemLabelCount);
        builder.Add("comboBoxDisplayTextLabel", telemetry.GetDisplayTextLabelCount);
        builder.Add("comboBoxDisplayTextResolved", telemetry.GetDisplayTextResolveDisplayPathCount);
        builder.Add("comboBoxDisplayTextEmpty", telemetry.GetDisplayTextEmptyResultCount);
        builder.Add("comboBoxBuildDropDownContainerCalls", telemetry.BuildDropDownContainerCallCount);
        builder.Add("comboBoxConfigureContainerCalls", telemetry.ConfigureContainerFromItemCallCount);
        builder.Add("comboBoxSyncTypographyCalls", telemetry.SyncContainerTypographyCallCount);
        builder.Add("comboBoxSyncTypographyStyleSkip", telemetry.SyncContainerTypographyStyleSkipCount);
        builder.Add("comboBoxSyncTypographyForegroundSet", telemetry.SyncContainerTypographyForegroundSetCount);
        builder.Add("comboBoxSyncTypographyFontFamilySet", telemetry.SyncContainerTypographyFontFamilySetCount);
        builder.Add("comboBoxSyncTypographyFontSizeSet", telemetry.SyncContainerTypographyFontSizeSetCount);
        builder.Add("comboBoxSyncTypographyFontWeightSet", telemetry.SyncContainerTypographyFontWeightSetCount);
        builder.Add("comboBoxSyncTypographyFontStyleSet", telemetry.SyncContainerTypographyFontStyleSetCount);

        _ = context;
    }

    private static string Escape(string value)
    {
        return value.Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        return milliseconds.ToString("0.###");
    }
}