namespace InkkSlinger;

public sealed class InkkOopsExpanderDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 40;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        if (element is not Expander expander)
        {
            return;
        }

        builder.Add("isExpanded", expander.IsExpanded);
        builder.Add("expandDirection", expander.ExpandDirection);

        var telemetry = Expander.GetExpanderSnapshotForDiagnostics();
        builder.Add("measureOverrideCalls", telemetry.MeasureOverrideCallCount);
        builder.Add("measureOverrideMs", $"{telemetry.MeasureOverrideMilliseconds:0.###}");
        builder.Add("headerMeasureCount", telemetry.HeaderMeasureCount);
        builder.Add("contentMeasuredWhenExpanded", telemetry.ContentMeasuredWhenExpandedCount);
        builder.Add("contentMeasuredWhenCollapsed", telemetry.ContentMeasuredWhenCollapsedCount);
        builder.Add("arrangeOverrideCalls", telemetry.ArrangeOverrideCallCount);
        builder.Add("arrangeOverrideMs", $"{telemetry.ArrangeOverrideMilliseconds:0.###}");
        builder.Add("expandDirectionDown", telemetry.ExpandDirectionDownCount);
        builder.Add("expandDirectionUp", telemetry.ExpandDirectionUpCount);
        builder.Add("expandDirectionLeft", telemetry.ExpandDirectionLeftCount);
        builder.Add("expandDirectionRight", telemetry.ExpandDirectionRightCount);
        builder.Add("renderCalls", telemetry.RenderCallCount);
        builder.Add("renderMs", $"{telemetry.RenderMilliseconds:0.###}");
        builder.Add("expandCount", telemetry.ExpandCount);
        builder.Add("collapseCount", telemetry.CollapseCount);
        builder.Add("headerPointerDownCount", telemetry.HeaderPointerDownCount);
        builder.Add("headerPointerUpToggleCount", telemetry.HeaderPointerUpToggleCount);
        builder.Add("headerUpdateCount", telemetry.HeaderUpdateCount);
    }
}