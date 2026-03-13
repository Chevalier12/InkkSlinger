using System;

namespace InkkSlinger;

public partial class DataGridView : UserControl
{
    public DataGridView()
    {
        InitializeComponent();
        var demoHost = this.FindName("DemoHost") as ContentControl;

        if (demoHost is not null)
        {
            var sample = ControlDemoSupport.BuildSampleElement("DataGrid");
            demoHost.Content = sample;
        }
    }
}




