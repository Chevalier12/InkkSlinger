using System;

namespace InkkSlinger;

public partial class DataGridRowView : UserControl
{
    public DataGridRowView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("DataGridRow");
        }
    }
}




