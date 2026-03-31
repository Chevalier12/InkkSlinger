namespace InkkSlinger;

public sealed class InkkOopsFrameworkElementDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 10;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        if (element is not FrameworkElement frameworkElement)
        {
            return;
        }

        var invalidation = frameworkElement.InvalidationDiagnosticsForTests;
        var slot = frameworkElement.LayoutSlot;
        builder.Add("slot", $"{slot.X:0.##},{slot.Y:0.##},{slot.Width:0.##},{slot.Height:0.##}");
        builder.Add("desired", $"{frameworkElement.DesiredSize.X:0.##},{frameworkElement.DesiredSize.Y:0.##}");
        builder.Add("actual", $"{frameworkElement.ActualWidth:0.##},{frameworkElement.ActualHeight:0.##}");
        builder.Add("previousAvailable", $"{frameworkElement.PreviousAvailableSizeForTests.X:0.##},{frameworkElement.PreviousAvailableSizeForTests.Y:0.##}");
        builder.Add("measureCalls", frameworkElement.MeasureCallCount);
        builder.Add("measureWork", frameworkElement.MeasureWorkCount);
        builder.Add("arrangeCalls", frameworkElement.ArrangeCallCount);
        builder.Add("arrangeWork", frameworkElement.ArrangeWorkCount);
        builder.Add("measureValid", frameworkElement.IsMeasureValidForTests);
        builder.Add("arrangeValid", frameworkElement.IsArrangeValidForTests);
        builder.Add("measureInvalidations", frameworkElement.MeasureInvalidationCount);
        builder.Add("measureInvalidationDirect", invalidation.DirectMeasureInvalidationCount);
        builder.Add("measureInvalidationPropagated", invalidation.PropagatedMeasureInvalidationCount);
        builder.Add("measureInvalidationLast", invalidation.LastMeasureInvalidationSummary);
        builder.Add("measureInvalidationTopSources", invalidation.TopMeasureInvalidationSources);
        builder.Add("measureInvalidationLastLayoutFrame", invalidation.LastMeasureInvalidationLayoutFrame);
        builder.Add("measureInvalidationLastDrawFrame", invalidation.LastMeasureInvalidationDrawFrame);
        builder.Add("arrangeInvalidations", frameworkElement.ArrangeInvalidationCount);
        builder.Add("arrangeInvalidationDirect", invalidation.DirectArrangeInvalidationCount);
        builder.Add("arrangeInvalidationPropagated", invalidation.PropagatedArrangeInvalidationCount);
        builder.Add("arrangeInvalidationLast", invalidation.LastArrangeInvalidationSummary);
        builder.Add("arrangeInvalidationTopSources", invalidation.TopArrangeInvalidationSources);
        builder.Add("arrangeInvalidationLastLayoutFrame", invalidation.LastArrangeInvalidationLayoutFrame);
        builder.Add("arrangeInvalidationLastDrawFrame", invalidation.LastArrangeInvalidationDrawFrame);
        builder.Add("renderInvalidations", frameworkElement.RenderInvalidationCount);
        builder.Add("renderInvalidationDirect", invalidation.DirectRenderInvalidationCount);
        builder.Add("renderInvalidationPropagated", invalidation.PropagatedRenderInvalidationCount);
        builder.Add("renderInvalidationLast", invalidation.LastRenderInvalidationSummary);
        builder.Add("renderInvalidationTopSources", invalidation.TopRenderInvalidationSources);
        builder.Add("renderInvalidationLastLayoutFrame", invalidation.LastRenderInvalidationLayoutFrame);
        builder.Add("renderInvalidationLastDrawFrame", invalidation.LastRenderInvalidationDrawFrame);
        builder.Add("visible", frameworkElement.Visibility == Visibility.Visible);
        builder.Add("enabled", frameworkElement.IsEnabled);
    }
}
