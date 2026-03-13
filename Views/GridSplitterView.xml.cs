using System;

namespace InkkSlinger;

public partial class GridSplitterView : UserControl
{
    public GridSplitterView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("GridSplitter");
        }
    }
}




