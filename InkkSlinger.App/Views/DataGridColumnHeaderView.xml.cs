using System;

namespace InkkSlinger;

public partial class DataGridColumnHeaderView : UserControl
{
    public DataGridColumnHeaderView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("DataGridColumnHeader");
        }
    }
}




