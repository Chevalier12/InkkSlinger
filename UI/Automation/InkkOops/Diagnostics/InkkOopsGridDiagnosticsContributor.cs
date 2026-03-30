namespace InkkSlinger;

public sealed class InkkOopsGridDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 50;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        if (element is not Grid grid)
        {
            return;
        }

        builder.Add("desired", $"{grid.DesiredSize.X:0.##},{grid.DesiredSize.Y:0.##}");
        builder.Add("previousAvailable", $"{grid.PreviousAvailableSizeForTests.X:0.##},{grid.PreviousAvailableSizeForTests.Y:0.##}");
        builder.Add("measureCalls", grid.MeasureCallCount);
        builder.Add("measureWork", grid.MeasureWorkCount);
        builder.Add("arrangeCalls", grid.ArrangeCallCount);
        builder.Add("arrangeWork", grid.ArrangeWorkCount);
        builder.Add("measureValid", grid.IsMeasureValidForTests);
        builder.Add("arrangeValid", grid.IsArrangeValidForTests);
    }
}
