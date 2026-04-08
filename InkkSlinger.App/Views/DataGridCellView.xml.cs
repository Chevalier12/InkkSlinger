using System;

namespace InkkSlinger;

public partial class DataGridCellView : UserControl
{
    public DataGridCellView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("DataGridCell");
        }
    }
}




