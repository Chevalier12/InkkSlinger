using System;

namespace InkkSlinger;

public partial class DataGridRowHeaderView : UserControl
{
    public DataGridRowHeaderView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("DataGridRowHeader");
        }
    }
}




