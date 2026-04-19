using System;
using System.Collections.Generic;

namespace InkkSlinger;

public sealed class DefaultInkkOopsDiagnosticsFilterPolicy : IInkkOopsDiagnosticsFilterPolicy
{
    private static readonly int[] TargetedActionIndexes =
    [
        303, 304, 305, 306, 307,
        323, 324, 325, 326, 327,
        599, 600, 601, 602, 603,
        613, 614, 615,
        661, 662, 663, 664, 665,
        671, 672, 673, 674,
    ];

    private static readonly InkkOopsDiagnosticsFilter TargetedActionFilter = new()
    {
        NodeRetention = InkkOopsDiagnosticsNodeRetention.MatchedNodesAndAncestors,
        Rules = CreateColorPickerJumpRules()
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

    private static InkkOopsDiagnosticsFactRule[] CreateColorPickerJumpRules()
    {
        var rules = new List<InkkOopsDiagnosticsFactRule>
        {
            new() { DisplayNameContains = "ColorPickerControl", Key = "colorPickerSelectedColor", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new() { DisplayNameContains = "ColorPickerControl", Key = "colorPickerHasPendingSelectedColorSync", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new() { DisplayNameContains = "ColorPickerControl", Key = "colorPickerSelectedColorSyncDeferred", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new() { DisplayNameContains = "HueSpectrumControl", Key = "colorSpectrumSelectedColor", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new() { DisplayNameContains = "AlphaSpectrumControl", Key = "colorSpectrumSelectedColor", Comparison = InkkOopsDiagnosticsComparison.Exists }
        };

        AddNamedRules(
            rules,
            "DesignerSourceColorPropertyEditor",
            "name",
            "slot",
            "actual",
            "renderSize",
            "desired",
            "previousAvailable",
            "measureValid",
            "arrangeValid",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "arrangeInvalidationLast",
            "renderInvalidationLast",
            "frameworkInvalidateVisualCalls",
            "userControlHasTemplateRoot",
            "userControlHasContentElement",
            "userControlContentElementType",
            "userControlRuntimeArrangeOverrideCalls",
            "userControlRuntimeRenderCalls");

        AddNamedRules(
            rules,
            "InteractivePopup",
            "name",
            "slot",
            "actual",
            "renderSize",
            "desired",
            "previousAvailable",
            "measureValid",
            "arrangeValid",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "arrangeInvalidationLast",
            "renderInvalidationLast",
            "frameworkInvalidateVisualCalls");

        AddNamedRules(
            rules,
            "ColorPickerControl",
            "name",
            "slot",
            "actual",
            "renderSize",
            "desired",
            "previousAvailable",
            "measureValid",
            "arrangeValid",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "arrangeInvalidationLast",
            "renderInvalidationLast",
            "frameworkInvalidateVisualCalls",
            "colorPickerSpectrumRect",
            "colorPickerSelector",
            "colorPickerSelectorRadius",
            "colorPickerSelectedColor",
            "colorPickerHue",
            "colorPickerSaturation",
            "colorPickerValue",
            "colorPickerAlpha",
            "colorPickerDragging",
            "colorPickerMouseOver",
            "colorPickerHasPendingSelectedColorSync",
            "colorPickerSelectedColorSyncDeferred",
            "colorPickerSynchronizingSelectedColor",
            "colorPickerSynchronizingComponents",
            "colorPickerRuntimeRequestSelectedColorSyncCalls",
            "colorPickerRuntimeRequestSelectedColorSyncDragDeferred",
            "colorPickerRuntimeQueueDeferredSelectedColorSyncCalls",
            "colorPickerRuntimeQueueDeferredSelectedColorSyncAlreadyQueued",
            "colorPickerRuntimeFlushDeferredSelectedColorSyncCalls",
            "colorPickerRuntimeFlushDeferredSelectedColorSyncNoPending",
            "colorPickerRuntimeFlushDeferredSelectedColorSyncRequeueWhileDragging",
            "colorPickerRuntimeFlushPendingSelectedColorSyncAfterDragCalls",
            "colorPickerRuntimeFlushPendingSelectedColorSyncAfterDragNoPending",
            "colorPickerRuntimeSyncSelectedColorCalls",
            "colorPickerRuntimeSyncSelectedColorMs",
            "colorPickerRuntimeSyncSelectedColorReentrantSkip",
            "colorPickerRuntimeSyncSelectedColorNoOp",
            "colorPickerRuntimeSelectedColorChangedCalls",
            "colorPickerRuntimeSelectedColorChangedExternalSync",
            "colorPickerRuntimeSelectedColorChangedComponentWriteback",
            "colorPickerRuntimeSelectedColorChangedRaised",
            "colorPickerRuntimeHueChangedCalls",
            "colorPickerRuntimeSaturationChangedCalls",
            "colorPickerRuntimeValueChangedCalls",
            "colorPickerRuntimeAlphaChangedCalls");

        AddNamedRules(
            rules,
            "HueSpectrumControl",
            "name",
            "slot",
            "actual",
            "renderSize",
            "desired",
            "previousAvailable",
            "measureValid",
            "arrangeValid",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "arrangeInvalidationLast",
            "renderInvalidationLast",
            "frameworkInvalidateVisualCalls",
            "colorSpectrumRect",
            "colorSpectrumSelectorNormalizedOffset",
            "colorSpectrumSelectorPosition",
            "colorSpectrumMode",
            "colorSpectrumSelectedColor",
            "colorSpectrumHue",
            "colorSpectrumSaturation",
            "colorSpectrumValue",
            "colorSpectrumAlpha",
            "colorSpectrumOrientation",
            "colorSpectrumDragging",
            "colorSpectrumMouseOver",
            "colorSpectrumHasPendingSelectedColorSync",
            "colorSpectrumSelectedColorSyncDeferred",
            "colorSpectrumSynchronizingSelectedColor",
            "colorSpectrumSynchronizingComponents",
            "colorSpectrumRuntimeUpdateFromPointerHuePath",
            "colorSpectrumRuntimeRequestSelectedColorSyncCalls",
            "colorSpectrumRuntimeRequestSelectedColorSyncDragDeferred",
            "colorSpectrumRuntimeQueueDeferredSelectedColorSyncCalls",
            "colorSpectrumRuntimeQueueDeferredSelectedColorSyncAlreadyQueued",
            "colorSpectrumRuntimeFlushDeferredSelectedColorSyncCalls",
            "colorSpectrumRuntimeFlushDeferredSelectedColorSyncNoPending",
            "colorSpectrumRuntimeFlushDeferredSelectedColorSyncRequeueWhileDragging",
            "colorSpectrumRuntimeFlushPendingSelectedColorSyncAfterDragCalls",
            "colorSpectrumRuntimeFlushPendingSelectedColorSyncAfterDragNoPending",
            "colorSpectrumRuntimeSyncSelectedColorCalls",
            "colorSpectrumRuntimeSyncSelectedColorMs",
            "colorSpectrumRuntimeSyncSelectedColorReentrantSkip",
            "colorSpectrumRuntimeSyncSelectedColorNoOp",
            "colorSpectrumRuntimeSelectedColorChangedCalls",
            "colorSpectrumRuntimeSelectedColorChangedExternalSync",
            "colorSpectrumRuntimeSelectedColorChangedComponentWriteback",
            "colorSpectrumRuntimeSelectedColorChangedRaised",
            "colorSpectrumRuntimeHueChangedCalls",
            "colorSpectrumRuntimeAlphaChangedCalls");

        AddNamedRules(
            rules,
            "AlphaSpectrumControl",
            "name",
            "slot",
            "actual",
            "renderSize",
            "desired",
            "previousAvailable",
            "measureValid",
            "arrangeValid",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "arrangeInvalidationLast",
            "renderInvalidationLast",
            "frameworkInvalidateVisualCalls",
            "colorSpectrumRect",
            "colorSpectrumSelectorNormalizedOffset",
            "colorSpectrumSelectorPosition",
            "colorSpectrumMode",
            "colorSpectrumSelectedColor",
            "colorSpectrumHue",
            "colorSpectrumSaturation",
            "colorSpectrumValue",
            "colorSpectrumAlpha",
            "colorSpectrumOrientation",
            "colorSpectrumDragging",
            "colorSpectrumMouseOver",
            "colorSpectrumHasPendingSelectedColorSync",
            "colorSpectrumSelectedColorSyncDeferred",
            "colorSpectrumSynchronizingSelectedColor",
            "colorSpectrumSynchronizingComponents",
            "colorSpectrumRuntimeUpdateFromPointerAlphaPath",
            "colorSpectrumRuntimeRequestSelectedColorSyncCalls",
            "colorSpectrumRuntimeRequestSelectedColorSyncDragDeferred",
            "colorSpectrumRuntimeQueueDeferredSelectedColorSyncCalls",
            "colorSpectrumRuntimeQueueDeferredSelectedColorSyncAlreadyQueued",
            "colorSpectrumRuntimeFlushDeferredSelectedColorSyncCalls",
            "colorSpectrumRuntimeFlushDeferredSelectedColorSyncNoPending",
            "colorSpectrumRuntimeFlushDeferredSelectedColorSyncRequeueWhileDragging",
            "colorSpectrumRuntimeFlushPendingSelectedColorSyncAfterDragCalls",
            "colorSpectrumRuntimeFlushPendingSelectedColorSyncAfterDragNoPending",
            "colorSpectrumRuntimeSyncSelectedColorCalls",
            "colorSpectrumRuntimeSyncSelectedColorMs",
            "colorSpectrumRuntimeSyncSelectedColorReentrantSkip",
            "colorSpectrumRuntimeSyncSelectedColorNoOp",
            "colorSpectrumRuntimeSelectedColorChangedCalls",
            "colorSpectrumRuntimeSelectedColorChangedExternalSync",
            "colorSpectrumRuntimeSelectedColorChangedComponentWriteback",
            "colorSpectrumRuntimeSelectedColorChangedRaised",
            "colorSpectrumRuntimeHueChangedCalls",
            "colorSpectrumRuntimeAlphaChangedCalls");

        AddNamedRules(
            rules,
            "ColorPickerItem",
            "name",
            "slot",
            "actual",
            "renderSize",
            "desired",
            "previousAvailable",
            "measureValid",
            "arrangeValid",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "arrangeInvalidationLast",
            "renderInvalidationLast",
            "frameworkInvalidateVisualCalls",
            "hasVisibleBackground",
            "hasVisibleBorder",
            "runtimeMeasureOverrideCalls",
            "runtimeArrangeOverrideCalls",
            "runtimeRenderCalls");

        AddNamedRules(
            rules,
            "HueSpectrumItem",
            "name",
            "slot",
            "actual",
            "renderSize",
            "desired",
            "previousAvailable",
            "measureValid",
            "arrangeValid",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "arrangeInvalidationLast",
            "renderInvalidationLast",
            "frameworkInvalidateVisualCalls",
            "hasVisibleBackground",
            "hasVisibleBorder",
            "runtimeMeasureOverrideCalls",
            "runtimeArrangeOverrideCalls",
            "runtimeRenderCalls");

        AddNamedRules(
            rules,
            "AlphaSpectrumItem",
            "name",
            "slot",
            "actual",
            "renderSize",
            "desired",
            "previousAvailable",
            "measureValid",
            "arrangeValid",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "arrangeInvalidationLast",
            "renderInvalidationLast",
            "frameworkInvalidateVisualCalls",
            "hasVisibleBackground",
            "hasVisibleBorder",
            "runtimeMeasureOverrideCalls",
            "runtimeArrangeOverrideCalls",
            "runtimeRenderCalls");

        AddElementTypeRules(
            rules,
            "Border",
            "hasVisibleBackground",
            "hasVisibleBorder",
            "runtimeMeasureOverrideCalls",
            "runtimeArrangeOverrideCalls",
            "runtimeRenderCalls");

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
