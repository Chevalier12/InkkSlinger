using System;
using System.Collections.Generic;

namespace InkkSlinger;

public sealed class DefaultInkkOopsDiagnosticsFilterPolicy : IInkkOopsDiagnosticsFilterPolicy
{
    private static readonly int[] TargetedActionIndexes =
    [
        326, 328, 330, 332, 334, 336, 338, 342, 344, 346, 348, 350, 352,
        451, 453, 455, 457, 459, 461, 463, 465, 466, 468, 470, 472,
        490, 492, 496, 500, 506, 526, 528, 536, 538
    ];

    private static readonly InkkOopsDiagnosticsFilter TargetedActionFilter = new()
    {
        NodeRetention = InkkOopsDiagnosticsNodeRetention.MatchedNodesAndAncestors,
        Rules = CreatePropertyInspectorColorEditorRules()
    };

    public InkkOopsDiagnosticsFilter CreateFilter(string artifactName)
    {
        if (!TryGetActionIndex(artifactName, out var actionIndex))
        {
            return InkkOopsDiagnosticsFilter.None;
        }

        return Array.IndexOf(TargetedActionIndexes, actionIndex) >= 0
            ? TargetedActionFilter
            : InkkOopsDiagnosticsFilter.None;
    }

    private static InkkOopsDiagnosticsFactRule[] CreatePropertyInspectorColorEditorRules()
    {
        var rules = new List<InkkOopsDiagnosticsFactRule>
        {
            new() { Key = "hovered", Comparison = InkkOopsDiagnosticsComparison.Equal, Value = true },
            new() { Key = "focused", Comparison = InkkOopsDiagnosticsComparison.Equal, Value = true },
            new() { Key = "captured", Comparison = InkkOopsDiagnosticsComparison.Equal, Value = true }
        };

        AddElementTypeRules(
            rules,
            "ComboBox",
            "name",
            "slot",
            "actual",
            "renderSize",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "arrangeMs",
            "comboBoxIsDropDownOpen",
            "comboBoxHasDropDownPopup",
            "comboBoxIsDropDownPopupOpen",
            "comboBoxHasDropDownList",
            "comboBoxSelectedIndex",
            "comboBoxItemContainerCount",
            "comboBoxDropDownItemCount",
            "comboBoxRuntimeOpenDropDownCalls",
            "comboBoxRuntimeOpenDropDownMs",
            "comboBoxRuntimeOpenDropDownPopupShow",
            "comboBoxRuntimeOpenDropDownPopupUnavailable",
            "comboBoxRuntimeDropDownPopupCreates",
            "comboBoxRuntimeDropDownPopupReuses",
            "comboBoxRuntimeRefreshDropDownItemsCalls",
            "comboBoxRuntimeRefreshDropDownItemsMs",
            "comboBoxRuntimeRefreshProjectedItems",
            "comboBoxOpenDropDownCalls",
            "comboBoxOpenDropDownMs",
            "comboBoxDropDownPopupCreates",
            "comboBoxDropDownPopupReuses",
            "comboBoxRefreshDropDownItemsCalls",
            "comboBoxRefreshDropDownItemsMs",
            "comboBoxRefreshProjectedItems");

        AddNamedRules(
            rules,
            "SourceColorPropertyEditor",
            "name",
            "slot",
            "actual",
            "renderSize",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "arrangeMs",
            "comboBoxIsDropDownOpen",
            "comboBoxHasDropDownPopup",
            "comboBoxIsDropDownPopupOpen",
            "comboBoxHasDropDownList",
            "comboBoxSelectedIndex");

        AddElementTypeRules(
            rules,
            "ColorPicker",
            "slot",
            "actual",
            "renderSize",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "arrangeMs",
            "colorPickerSpectrumRect",
            "colorPickerSelector",
            "colorPickerSelectorRadius",
            "colorPickerDragging",
            "colorPickerMouseOver",
            "colorPickerRuntimePointerDownCalls",
            "colorPickerRuntimePointerDownMs",
            "colorPickerRuntimePointerHits",
            "colorPickerRuntimePointerMisses",
            "colorPickerRuntimePointerMoveCalls",
            "colorPickerRuntimePointerMoveMs",
            "colorPickerRuntimePointerMoveDrag",
            "colorPickerRuntimeUpdateFromPointerCalls",
            "colorPickerRuntimeUpdateFromPointerMs",
            "colorPickerRuntimeSyncSelectedColorCalls",
            "colorPickerRuntimeSyncSelectedColorMs",
            "colorPickerRuntimeRenderCalls",
            "colorPickerRuntimeRenderMs",
            "colorPickerRuntimeTextureCacheHits",
            "colorPickerRuntimeTextureCacheMisses",
            "colorPickerRuntimeTextureBuilds",
            "colorPickerRuntimeTextureBuildMs",
            "colorPickerPointerMoveCalls",
            "colorPickerPointerMoveMs",
            "colorPickerUpdateFromPointerCalls",
            "colorPickerUpdateFromPointerMs",
            "colorPickerSyncSelectedColorCalls",
            "colorPickerSyncSelectedColorMs",
            "colorPickerRenderCalls",
            "colorPickerRenderMs",
            "colorPickerTextureCacheHits",
            "colorPickerTextureCacheMisses",
            "colorPickerTextureBuilds",
            "colorPickerTextureBuildMs");

        AddElementTypeRules(
            rules,
            "ColorSpectrum",
            "slot",
            "actual",
            "renderSize",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "arrangeMs",
            "colorSpectrumRect",
            "colorSpectrumSelectorNormalizedOffset",
            "colorSpectrumSelectorPosition",
            "colorSpectrumMode",
            "colorSpectrumOrientation",
            "colorSpectrumDragging",
            "colorSpectrumMouseOver",
            "colorSpectrumRuntimePointerDownCalls",
            "colorSpectrumRuntimePointerDownMs",
            "colorSpectrumRuntimePointerHits",
            "colorSpectrumRuntimePointerMisses",
            "colorSpectrumRuntimePointerMoveCalls",
            "colorSpectrumRuntimePointerMoveMs",
            "colorSpectrumRuntimePointerMoveDrag",
            "colorSpectrumRuntimeUpdateFromPointerCalls",
            "colorSpectrumRuntimeUpdateFromPointerMs",
            "colorSpectrumRuntimeUpdateFromPointerHuePath",
            "colorSpectrumRuntimeUpdateFromPointerAlphaPath",
            "colorSpectrumRuntimeSyncSelectedColorCalls",
            "colorSpectrumRuntimeSyncSelectedColorMs",
            "colorSpectrumRuntimeRenderCalls",
            "colorSpectrumRuntimeRenderMs",
            "colorSpectrumRuntimeTextureCacheHits",
            "colorSpectrumRuntimeTextureCacheMisses",
            "colorSpectrumRuntimeTextureBuilds",
            "colorSpectrumRuntimeTextureBuildMs",
            "colorSpectrumPointerMoveCalls",
            "colorSpectrumPointerMoveMs",
            "colorSpectrumUpdateFromPointerCalls",
            "colorSpectrumUpdateFromPointerMs",
            "colorSpectrumUpdateFromPointerHuePath",
            "colorSpectrumUpdateFromPointerAlphaPath",
            "colorSpectrumSyncSelectedColorCalls",
            "colorSpectrumSyncSelectedColorMs",
            "colorSpectrumRenderCalls",
            "colorSpectrumRenderMs",
            "colorSpectrumTextureCacheHits",
            "colorSpectrumTextureCacheMisses",
            "colorSpectrumTextureBuilds",
            "colorSpectrumTextureBuildMs");

        AddElementTypeRules(
            rules,
            "StackPanel",
            "orientation",
            "children",
            "childSummary",
            "stackPanelGlobalMeasureCalls",
            "stackPanelGlobalMeasureMs",
            "stackPanelGlobalArrangedChildren",
            "stackPanelGlobalArrangeCalls",
            "stackPanelGlobalArrangeMs");

        AddElementTypeRules(
            rules,
            "Border",
            "desired",
            "previousAvailable",
            "hasVisibleBackground",
            "hasVisibleBorder",
            "runtimeMeasureOverrideCalls",
            "runtimeMeasureOverrideMs",
            "runtimeArrangeOverrideCalls",
            "runtimeArrangeOverrideMs",
            "runtimeRenderCalls",
            "runtimeRenderMs");

        return [.. rules];
    }

    private static void AddNamedRules(List<InkkOopsDiagnosticsFactRule> rules, string displayNameContains, params string[] keys)
    {
        for (var i = 0; i < keys.Length; i++)
        {
            rules.Add(new InkkOopsDiagnosticsFactRule
            {
                DisplayNameContains = displayNameContains,
                Key = keys[i],
                Comparison = InkkOopsDiagnosticsComparison.Exists
            });
        }
    }

    private static void AddElementTypeRules(List<InkkOopsDiagnosticsFactRule> rules, string elementTypeName, params string[] keys)
    {
        for (var i = 0; i < keys.Length; i++)
        {
            rules.Add(new InkkOopsDiagnosticsFactRule
            {
                ElementTypeName = elementTypeName,
                Key = keys[i],
                Comparison = InkkOopsDiagnosticsComparison.Exists
            });
        }
    }

    private static bool TryGetActionIndex(string artifactName, out int actionIndex)
    {
        const string prefix = "action[";
        actionIndex = -1;

        if (!artifactName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var endIndex = artifactName.IndexOf(']', prefix.Length);
        return endIndex > prefix.Length
            && int.TryParse(artifactName[prefix.Length..endIndex], out actionIndex);
    }
}
