using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogFactoryTests
{
    [Fact]
    public void CatalogViewFactory_ShouldCoverAllRegisteredControls()
    {
        foreach (var controlName in ControlViews.All)
        {
            Assert.True(ControlViews.HasCatalogView(controlName), $"Missing catalog view factory for {controlName}.");
        }
    }

    [Fact]
    public void BuildSampleElement_ShouldReturnConcreteSamplesForSupportedControls()
    {
        string[] intentionallyInformational =
        [
            "CatchMe",
            "DataGridCell",
            "DataGridColumnHeader",
            "DataGridDetailsPresenter",
            "DataGridRow",
            "DataGridRowHeader",
            "InkCanvas",
            "InkPresenter",
            "MediaElement",
            "Window"
        ];

        foreach (var controlName in ControlViews.All)
        {
            if (System.Array.IndexOf(intentionallyInformational, controlName) >= 0)
            {
                continue;
            }

            var sample = ControlDemoSupport.BuildSampleElement(controlName);
            if (sample is Label label)
            {
                Assert.DoesNotContain("not implemented as a UIElement", label.GetContentText());
                Assert.DoesNotContain("exists but has no parameterless constructor", label.GetContentText());
                Assert.DoesNotContain("Could not create", label.GetContentText());
                Assert.DoesNotContain("Failed to create", label.GetContentText());
            }
        }
    }
}
