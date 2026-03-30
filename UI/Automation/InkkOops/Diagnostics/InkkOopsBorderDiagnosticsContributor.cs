namespace InkkSlinger;

public sealed class InkkOopsBorderDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 40;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        if (element is not Border border)
        {
            return;
        }

        builder.Add("desired", $"{border.DesiredSize.X:0.##},{border.DesiredSize.Y:0.##}");
        builder.Add("previousAvailable", $"{border.PreviousAvailableSizeForTests.X:0.##},{border.PreviousAvailableSizeForTests.Y:0.##}");
        builder.Add("measureCalls", border.MeasureCallCount);
        builder.Add("measureWork", border.MeasureWorkCount);
        builder.Add("arrangeCalls", border.ArrangeCallCount);
        builder.Add("arrangeWork", border.ArrangeWorkCount);
        builder.Add("measureValid", border.IsMeasureValidForTests);
        builder.Add("arrangeValid", border.IsArrangeValidForTests);
    }
}
